using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private async Task<NoraRunView?> RuntimeTraceAsync(Guid uid, Guid turnId, CancellationToken ct)
    {
        var run = await Mine<AgentRun>(uid).AsNoTracking().SingleOrDefaultAsync(x => x.TurnId == turnId, ct);
        if (run is null) return null;
        var labels = NoraToolRegistry.Create([]).ToDictionary(x => x.Name, x => x.Label);
        var rows = await Mine<AgentToolCall>(uid).AsNoTracking().Where(x => x.RunId == run.Id).OrderBy(x => x.Sequence).ToArrayAsync(ct);
        return new(run.Id, "Agent", run.Status, run.ModelTurns, run.ToolCalls,
            rows.Select(x => new NoraToolTrace(x.Sequence, x.Tool, labels.GetValueOrDefault(x.Tool, x.Tool), x.Status, x.DurationMilliseconds)).ToArray());
    }

    private async Task<RuntimeExecution> RunNoraAgentAsync(Guid uid, Guid turnId, AgentTurnInput input,
        PlanningState snapshot, AgentMessage[] history, AgentPendingRequest? pending, string[] types, AgentImage? image, AgentTurn? imageSource,
        Func<AgentStreamEvent, CancellationToken, Task>? publish, CancellationToken ct)
    {
        var limits = noraOptions?.Value ?? new NoraAgentOptions();
        var provider = (INoraAgentModel)interpreter;
        var source = input.Text;
        var run = await Mutate(uid, async () =>
        {
            var rows = await Mine<AgentRun>(uid).Where(x => x.ConversationId == input.ConversationId).ToArrayAsync(ct);
            Require(!rows.Any(x => x.Status == "RUNNING" && x.LeaseUntil > Now), "AGENT_BUSY", "NORA đang xử lý một yêu cầu trong hội thoại này.", 409);
            var previous = rows.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.CreatedAt).FirstOrDefault();
            var previousTurn = previous is null ? null : await Mine<AgentTurn>(uid).AsNoTracking().SingleOrDefaultAsync(x => x.Id == previous.TurnId, ct);
            var recoverableStop = previousTurn?.Status == "BLOCKED" && TurnView(previousTurn).ErrorCode is
                "AGENT_BUDGET_EXCEEDED" or "TOOL_BUDGET_EXCEEDED" or "AGENT_TIME_LIMIT";
            var same = rows.SingleOrDefault(x => x.TurnId == turnId);
            var resume = same ?? ((history.LastOrDefault()?.Status == "NEEDS_INPUT" && previous?.Status == "WAITING_FOR_INPUT" || recoverableStop) &&
                previous!.UpdatedAt > (recoverableStop ? Now.AddMinutes(-30) : Now.AddHours(-24)) ? previous : null);
            foreach (var abandoned in rows.Where(x => x.Status == "RUNNING")) { abandoned.Status = "INTERRUPTED"; abandoned.Version = Guid.NewGuid(); }
            if (resume is null)
            {
                resume = new AgentRun { UserId = uid, ConversationId = input.ConversationId, TurnId = turnId,
                    Goal = input.Text, CreatedAt = await Mine<AgentTurn>(uid).Where(x => x.Id == turnId).Select(x => x.CreatedAt).SingleAsync(ct) };
                db.AgentRuns.Add(resume);
            }
            else if (resume.TurnId != turnId)
            {
                resume.Continuations++; resume.TurnId = turnId;
                source = await ContinuedAgentRequestAsync(uid, resume, turnId, input.Text, ct);
                resume.Goal = BoundedAgentRequest(source, 8000);
            }
            else source = await ContinuedAgentRequestAsync(uid, resume, turnId, input.Text, ct);
            resume.SnapshotRevision = snapshot.Preference.Revision;
            resume.Status = "RUNNING"; resume.Version = Guid.NewGuid(); resume.UpdatedAt = Now;
            resume.LeaseUntil = Now.AddSeconds(limits.TimeoutSeconds + 60);
            return resume;
        }, ct, false);
        var runId = run.Id; var leaseVersion = run.Version;
        agentFileLog?.RunStarted(runId, turnId, input.ConversationId, input, run.Continuations);
        var drafts = new Dictionary<string, AgentIntent>(StringComparer.Ordinal);
        var candidates = new Dictionary<string, RuntimeCandidate>(StringComparer.Ordinal);
        var state = Copy(snapshot);
        AgentIntent? lastDraft = null;
        NoraReminderCommand? reminderCommand = null;
        string? verifiedReply = null;
        var readObserved = false;
        var calendarObserved = false;
        NoraRequestUnderstanding? understanding = null;
        var singleCallStep = true;
        var calendarRanges = new List<AgentFreeTimeQuery>();
        var modelTurns = run.ModelTurns; var toolCalls = run.ToolCalls; var simulations = run.Simulations;
        var simulationLimit = simulations + limits.MaxSimulations;
        string? lastToolFailure = null;
        var priorSteps = await Mine<AgentToolCall>(uid).AsNoTracking().Where(x => x.RunId == runId).OrderBy(x => x.Sequence)
            .Select(x => new NoraToolTrace(x.Sequence, x.Tool, "", x.Status, x.DurationMilliseconds)).ToArrayAsync(ct);
        var catalog = NoraToolRegistry.Create(types);
        priorSteps = priorSteps.Select(x => x with { Label = catalog.FirstOrDefault(t => t.Name == x.Tool)?.Label ?? x.Tool }).ToArray();

        async Task Checkpoint(string status, CancellationToken token, NoraToolTrace? step = null, string hash = "", string code = "")
        {
            db.ChangeTracker.Clear();
            await Mutate(uid, async () =>
            {
                var saved = await Find<AgentRun>(uid, runId, token);
                Require(saved.Version == leaseVersion && saved.Status == "RUNNING", "AGENT_STALE", "Phiên xử lý đã được thay thế. Lịch chưa thay đổi.", 409);
                Require((await Preference(uid, token)).Revision == snapshot.Preference.Revision,
                    "AGENT_STALE", "Lịch vừa thay đổi. Gửi lại yêu cầu để NORA đọc dữ liệu mới.", 409);
                saved.Status = status; saved.ModelTurns = modelTurns; saved.ToolCalls = toolCalls; saved.Simulations = simulations;
                saved.StateJson = status == "WAITING_FOR_INPUT" ? JsonSerializer.Serialize(lastDraft, AgentJson) : "";
                saved.Version = Guid.NewGuid(); saved.UpdatedAt = Now;
                if (status != "RUNNING") saved.LeaseUntil = Now;
                if (step is not null) db.AgentToolCalls.Add(new() { UserId = uid, RunId = runId, Sequence = step.Sequence,
                    Tool = step.Tool, Status = step.Status, ArgumentsHash = hash, ResultCode = code,
                    DurationMilliseconds = step.DurationMs, CreatedAt = Now });
                leaseVersion = saved.Version;
            }, token, false);
        }

        async Task<PlanningState> Calendar(AgentFreeTimeQuery range, CancellationToken token)
        {
            if (calendarRanges.Any(r => r.Start <= range.Start && r.End >= range.End))
                return state with { Sessions = state.Sessions.Where(s => s.Start < range.End && s.End > range.Start &&
                    s.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed).ToArray() };
            var result = await CalendarStateAsync(uid, state, range.Start, range.End, token);
            state = state with { Sessions = result.Sessions.Concat(state.Sessions).DistinctBy(x => x.Id).ToArray() };
            calendarRanges.Add(range);
            return result;
        }
        RuntimeCandidate Candidate(JsonElement args)
        {
            Require(candidates.TryGetValue(args.GetProperty("candidateId").GetString()!, out var value),
                "CANDIDATE_NOT_FOUND", "Chỉ được dùng mã phương án do công cụ thử lịch của lượt này cấp.");
            return value;
        }
        string Text(JsonElement args, string name, int max)
        {
            var value = args.GetProperty(name).GetString()!;
            Require(value.Length <= max, "TEXT_LIMIT", "Nội dung trả lời vượt giới hạn.");
            return value.Trim();
        }
        async Task<NoraToolResult> Execute(NoraToolCall call, CancellationToken token)
        {
            var args = call.Arguments;
            switch (call.Name)
            {
                case "manage_reminder":
                {
                    var operation=Text(args,"operation",10);
                    Require(operation == "List" || understanding is null || understanding.Intent == "Mutation",
                        "REMINDER_INTENT_MISMATCH", "Đặt hoặc hủy lời nhắc cần yêu cầu rõ ràng từ bạn.");
                    DateTimeOffset? at=null;
                    if(args.GetProperty("at").ValueKind != JsonValueKind.Null)
                    {
                        var value=args.GetProperty("at").GetString()!;
                        Require(Regex.IsMatch(value,@"(?:Z|[+-]\d{2}:\d{2})$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)) &&
                            DateTimeOffset.TryParse(value,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _),
                            "INVALID_REMINDER_TIME","Giờ nhắc cần ngày giờ và múi giờ đầy đủ.");
                        at=DateTimeOffset.Parse(value,System.Globalization.CultureInfo.InvariantCulture);
                    }
                    Guid? id=null;
                    if(args.GetProperty("reminderId").ValueKind != JsonValueKind.Null)
                    {
                        Require(Guid.TryParse(args.GetProperty("reminderId").GetString(),out var parsed),"INVALID_REMINDER","Mã lời nhắc không hợp lệ.");
                        id=parsed;
                    }
                    reminderCommand=new(operation,Text(args,"message",500),at,id);
                    return new("TERMINAL",new NoraTerminal("REMINDER"));
                }
                case "get_tasks":
                {
                    readObserved = true;
                    var query = Text(args, "query", 200);
                    var tasks = state.Tasks.Where(x => !x.IsDeleted && x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled &&
                        (query.Length == 0 || x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Id.ToString() == query)).ToArray();
                    if (singleCallStep && understanding?.Intent == "TaskQuery")
                    {
                        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
                        var lines = tasks.Take(30).Select(t => $"- {t.Name}: " +
                            (t.EstimatedMinutes > 0 ? $"{t.EstimatedMinutes} phút" : "chưa ước lượng") +
                            (t.Deadline is { } due ? $", hạn {TimeZoneInfo.ConvertTime(due, zone):dd/MM/yyyy HH:mm}" : ", chưa có hạn") + $", tiến độ {t.Progress}%.");
                        var reply = tasks.Length == 0 ? "Mình chưa tìm thấy công việc đang hoạt động khớp yêu cầu."
                            : $"Mình tìm thấy {tasks.Length} công việc" + (tasks.Length > 30 ? " (hiển thị 30 việc đầu)" : "") + ":\n" + string.Join("\n", lines);
                        return new("TERMINAL", new NoraTerminal("ANSWER", reply));
                    }
                    return new("OK", new { total = tasks.Length, truncated = tasks.Length > 100,
                        tasks = tasks.Take(100).Select(x => new { x.Id, x.Name, x.TaskType, x.Priority, x.EstimatedMinutes,
                            x.Deadline, x.WorkWindowStart, x.WorkWindowEnd, x.Splittable, importance = ImportanceDto(x), status = x.Status.ToString() }),
                        dependencies = state.Dependencies.Where(x => tasks.Take(100).Any(t => t.Id == x.TaskId)).Take(300).ToArray() });
                }
                case "get_preferences":
                    readObserved = true;
                    return new("OK", new { preference = new { state.Preference.TimeZoneId, state.Preference.MaximumDailyMinutes, state.Preference.AutoReschedule,
                        state.Preference.MinimumSessionMinutes, state.Preference.MaximumSessionMinutes, state.Preference.BreakMinutes,
                        state.Preference.PreferredPeriod, state.Preference.PreferredStartMinute, state.Preference.PreferredEndMinute },
                        weeklyAvailability = state.Availability.Select(x => new { x.DayOfWeek, x.StartMinute, x.EndMinute }),
                        availabilityPolicy = state.Policy is { } policy ? PolicyDto(policy) : null,
                        dailyRoutine = CurrentRoutineDays(state),
                        datedOverrides = state.Exceptions.Where(x => x.Date >= LocalDate(Now, state.Preference) && x.Date <= LocalDate(Now.AddDays(31), state.Preference))
                            .Take(200).Select(x => new { x.Id, x.Date, x.StartMinute, x.EndMinute, x.IsAvailable, x.Note }),
                        overridesTruncated = state.Exceptions.Count(x => x.Date >= LocalDate(Now, state.Preference) && x.Date <= LocalDate(Now.AddDays(31), state.Preference)) > 200 });
                case "get_calendar":
                case "find_free_slots":
                case "answer_calendar":
                case "answer_free_time":
                case "analyze_workload":
                {
                    var range = RuntimeRange(args, state.Preference);
                    var calendar = await Calendar(range, token); readObserved = true; calendarObserved = true;
                    if (call.Name is "answer_calendar" or "answer_free_time" || singleCallStep && (
                        understanding?.Intent == "CalendarQuery" && call.Name == "get_calendar" ||
                        understanding?.Intent == "FreeTimeQuery" && call.Name is "find_free_slots" or "get_calendar"))
                    {
                        verifiedReply = call.Name == "answer_calendar" || understanding?.Intent == "CalendarQuery"
                            ? VerifiedScheduleOverviewReply(range, calendar) : VerifiedFreeTimeReply(range, calendar);
                        return new("TERMINAL", new NoraTerminal("ANSWER", verifiedReply));
                    }
                    if (call.Name == "get_calendar")
                    {
                        verifiedReply = VerifiedScheduleOverviewReply(range, calendar);
                        var events = calendar.Events.Where(x => x.Start < range.End && x.End > range.Start).ToArray();
                        var taskLookup = state.Tasks.ToDictionary(x => x.Id);
                        return new("OK", new { range, totalEvents = events.Length, totalSessions = calendar.Sessions.Length,
                            truncated = events.Length > 100 || calendar.Sessions.Length > 100,
                            events = events.Take(100).Select(x => new { x.Id, x.Name, x.Start, x.End, x.EventType }),
                            sessions = calendar.Sessions.Take(100).Select(x => new { x.Id, x.TaskId, taskName = taskLookup.GetValueOrDefault(x.TaskId)?.Name,
                                deadline = taskLookup.GetValueOrDefault(x.TaskId)?.Deadline, estimatedMinutes = taskLookup.GetValueOrDefault(x.TaskId)?.EstimatedMinutes,
                                x.Start, x.End, x.IsLocked, status = x.Status.ToString() }),
                            busyOverrides = calendar.Exceptions.Where(x => !x.IsAvailable && x.Date >= LocalDate(range.Start, calendar.Preference) && x.Date <= LocalDate(range.End, calendar.Preference)).ToArray() });
                    }
                    if (call.Name == "find_free_slots")
                    {
                        verifiedReply = VerifiedFreeTimeReply(range, calendar);
                        var slots = AgentFreeTimeSlots(calendar, range.Start > Now ? range.Start : Now, range.End);
                        return new("OK", new { range, total = slots.Length, truncated = slots.Length > 100, slots = slots.Take(100).ToArray() });
                    }
                    verifiedReply = null;
                    var analysis = engine.Analyze(calendar, new PlanInput(range.Start, Math.Clamp((int)Math.Ceiling((range.End - range.Start).TotalDays), 1, 30)) { WindowEnd = range.End }, Now);
                    if (LocalDate(range.Start, state.Preference) == LocalDate(Now, state.Preference) &&
                        LocalDate(range.End.AddTicks(-1), state.Preference) == LocalDate(Now, state.Preference))
                    {
                        var workload = await GetWorkloadAsync(uid, token);
                        verifiedReply = workload.Next.Reason;
                        return new("OK", new { analysis, next = workload.Next });
                    }
                    return new("OK", analysis);
                }
                case "prepare_changes":
                {
                    lastDraft = NormalizeRuntimeDraft(args.GetProperty("intent"), source, state, pending, types, input.CalendarKind);
                    Require(lastDraft.Question.Length == 0, "CLARIFICATION_REQUIRED", lastDraft.Question);
                    if (singleCallStep && understanding?.Intent == "Mutation")
                    {
                        simulations++;
                        Require(simulations <= simulationLimit, "SIMULATION_LIMIT", "Đã đạt giới hạn thử lịch của lượt này.");
                        await Calendar(new(Now, Now.AddDays(31)), token);
                        var candidate = await SimulateRuntimeAsync(uid, lastDraft, Copy(state), input, source,
                            image is not null || IsTimetableRequest(source), "keep_existing", token, configureTimetableHours: image is not null);
                        candidates[candidate.Id] = candidate;
                        if (candidate.Feasible) return new("TERMINAL", new NoraTerminal("PROPOSAL", AgentExplanation(lastDraft.Reply), CandidateId: candidate.Id));
                        var draftId = Guid.NewGuid().ToString("N"); drafts[draftId] = lastDraft;
                        return new("OK", new { draftId, candidate = RuntimeCandidateFacts(candidate, state) });
                    }
                    var id = Guid.NewGuid().ToString("N"); drafts[id] = lastDraft;
                    return new("OK", new { draftId = id, businessDataSaved = false,
                        effectiveTasks = lastDraft.Tasks.Select(x => new { x.TaskId, x.Name, x.Deadline, x.EstimatedMinutes,
                            x.Priority, x.Splittable, x.WorkWindowStart, x.WorkWindowEnd }) });
                }
                case "propose_changes":
                case "simulate_plan":
                {
                    AgentIntent draft;
                    if (call.Name == "propose_changes")
                    {
                        draft = NormalizeRuntimeDraft(args.GetProperty("intent"), source, state, pending, types, input.CalendarKind);
                        lastDraft = draft;
                        Require(draft.Question.Length == 0, "CLARIFICATION_REQUIRED", draft.Question);
                    }
                    else
                    {
                        Require(drafts.TryGetValue(args.GetProperty("draftId").GetString()!, out var prepared), "DRAFT_NOT_FOUND", "Bản nháp không thuộc lượt xử lý này.");
                        draft = prepared;
                    }
                    Require(simulations < simulationLimit, "SIMULATION_LIMIT", "Đã đạt giới hạn thử lịch của lượt này. Hãy chọn phương án đã kiểm tra hoặc hỏi người dùng.");
                    simulations++;
                    // Load the complete affected calendar before planning; the ordinary
                    // history cap must never hide reservations from a simulation.
                    await Calendar(new(Now, Now.AddDays(31)), token);
                    var candidate = await SimulateRuntimeAsync(uid, draft, Copy(state), input, source,
                        image is not null || IsTimetableRequest(source), args.GetProperty("strategy").GetString()!, token, configureTimetableHours: image is not null);
                    candidates[candidate.Id] = candidate;
                    if ((call.Name == "propose_changes" || singleCallStep && understanding?.Intent == "Mutation") && candidate.Feasible)
                        return new("TERMINAL", new NoraTerminal("PROPOSAL", AgentExplanation(draft.Reply), CandidateId: candidate.Id));
                    return new("OK", RuntimeCandidateFacts(candidate, state));
                }
                case "evaluate_candidate":
                    return new("OK", RuntimeCandidateFacts(Candidate(args), state));
                case "finish_proposal":
                {
                    var candidate = Candidate(args);
                    Require(candidate.Feasible, "CANDIDATE_INFEASIBLE", "Phương án còn thiếu thời gian hoặc vi phạm hạn. Hãy sửa điều kiện được cho phép hoặc hỏi người dùng.");
                    return new("TERMINAL", new NoraTerminal("PROPOSAL", AgentExplanation(Text(args, "reply", 4000)), CandidateId: candidate.Id));
                }
                case "finish_answer":
                    Require(readObserved, "ANSWER_NOT_GROUNDED", "Đọc dữ liệu thật bằng công cụ trước khi trả lời.");
                    Require(calendarObserved || !(ScheduleOverviewRequested(source) || ChatMatch(source,
                        @"\b(?:gio ranh|gio trong|khoang ranh|khoang trong|phan tich lich|danh gia lich|nhan xet lich)\b").Success),
                        "CALENDAR_NOT_OBSERVED", "Cần đọc lịch thật trong phạm vi yêu cầu trước khi kết luận về lịch hoặc giờ rảnh.");
                    return new("TERMINAL", new NoraTerminal("ANSWER", verifiedReply ?? AgentExplanation(Text(args, "reply", 4000))));
                case "ask_user":
                {
                    var question = Text(args, "question", 1000);
                    var options = args.GetProperty("options").EnumerateArray().Select(x => x.GetString()!).ToArray();
                    Require(question.Length > 0 && options.Length <= 6 && options.All(x => x.Length is > 0 and <= 120), "INVALID_QUESTION", "Hỏi một câu ngắn, tối đa 6 lựa chọn.");
                    Require(!(HasSessionMoveRequest(source) && simulations == run.Simulations &&
                        (ChatMatch(question, @"\b(?:chuyen|doi|doi gio|khung gio khac)\b[^.!?]{0,80}\b(?:kiem tra|cuoc hop|cuoc hen|lich kham)\b").Success ||
                         ChatMatch(question, @"\b(?:kiem tra|cuoc hop|cuoc hen|lich kham)\b[^.!?]{0,80}\b(?:sang|khung gio nao|gio khac)\b").Success)),
                        "WRONG_MOVE_TARGET", "Người dùng đã cho phép dời phiên làm việc để giữ giờ hẹn. Hãy đọc đúng phiên theo giờ/ngày và thử phương án đó trước; chỉ hỏi ngày/giờ kết thúc nếu còn thiếu, không đảo thành dời cuộc hẹn.");
                    return new("TERMINAL", new NoraTerminal("QUESTION", AgentExplanation(Text(args, "reply", 4000)), question, options));
                }
                default: return new("ERROR", ErrorCode: "UNKNOWN_TOOL");
            }
        }

        var localNow = TimeZoneInfo.ConvertTime(Now, PlanningEngine.Zone(state.Preference.TimeZoneId));
        var today = LocalDate(Now, state.Preference);
        var monday = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        var normalizedSource = MatchText(input.Text);
        var matchingTasks = state.Tasks.Where(t => !t.IsDeleted && t.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled &&
            (input.Text.Contains(t.Id.ToString(), StringComparison.OrdinalIgnoreCase) ||
             MatchText(t.Name).Length >= 3 && normalizedSource.Contains(MatchText(t.Name), StringComparison.Ordinal))).Take(9).ToArray();
        var initialData = new { currentTime = localNow,
            clock = new { currentLocal = localNow, today, tomorrow = today.AddDays(1), currentWeekStart = monday, nextWeekStart = monday.AddDays(7) },
            timeZone = state.Preference.TimeZoneId, activeTaskTypes = types, request = source, latestMessage = input.Text, history, pendingRequest = pending,
            imageContext = image is null ? null : new { reused = imageSource is not null, sourceTurnId = imageSource?.Id ?? turnId,
                originalText = imageSource?.Text ?? input.Text, sourceStatus = imageSource?.Status ?? "CURRENT",
                uploadedAt = imageSource?.CreatedAt ?? run.CreatedAt, imageCount = image.GetImages().Length },
            matchingSavedTasks = matchingTasks.Take(8).Select(t => new { t.Id, t.Name, t.Deadline, t.EstimatedMinutes, t.Priority,
                t.TaskType, t.Splittable, t.WorkWindowStart, t.WorkWindowEnd, importance = ImportanceDto(t) }).ToArray(),
            matchingSavedTasksTruncated = matchingTasks.Length > 8,
            timetableAutomation = new { hasFixedSchedule = state.Events.Length > 0,
                hasConfirmedWorkHours = state.Policy != null || state.Availability.Length > 0 || state.Exceptions.Any(x => x.IsAvailable),
                policy = "Use the supplied fixed timetable as the foundation. Import complete fixed commitments with propose_changes; the server computes meals/sleep/rest and free-time suggestions for that timetable week, without extra model calls. Never invent actual work hours, tasks or deadlines. Schedule urgent work in verified gaps while preserving fixed commitments." },
            remainingModelTurns = limits.MaxModelTurns, remainingToolCalls = limits.MaxToolCalls,
            remainingSimulations = limits.MaxSimulations,
            policy = "Read data using tools. All actions are drafts until a server-validated proposal is applied. Fixed/locked/running commitments are protected." };
        var parts = new List<object> { new { text = JsonSerializer.Serialize(initialData, AgentJson) } };
        if (image is not null) foreach (var part in image.GetImages())
            if (part.Data is { Length: > 0 }) parts.Add(new { inlineData = new { mimeType = part.MimeType, data = part.Data } });
        try
        {
            var result = await new NoraAgentRunner().RunAsync(TaskInterpreter.AgentRuntimeInstructions,
                JsonSerializer.SerializeToElement(new { role = "user", parts }), catalog, limits, modelTurns, toolCalls,
                async (request, token) =>
                {
                    modelTurns++; await Checkpoint("RUNNING", token);
                    agentFileLog?.ModelStarted(runId, turnId, modelTurns);
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var reply = await RunJob(uid, "AI_REQUEST", () => provider.NextAgentStepAsync(request, token), token);
                    singleCallStep = reply.Calls.Length == 1;
                    agentFileLog?.ModelCompleted(runId, turnId, modelTurns, watch.ElapsedMilliseconds, reply);
                    if (understanding is null && reply.Understanding is { } interpreted)
                    {
                        NoraRequestPolicy.Validate(interpreted); understanding = interpreted;
                        if (publish is not null) await publish(new("phase", "INTENT", interpreted.Summary), token);
                    }
                    return reply;
                }, Execute, async (step, hash, observation, token) =>
                {
                    toolCalls = step.Sequence;
                    if (observation.Status == "ERROR") lastToolFailure = observation.Message;
                    await Checkpoint("RUNNING", token, step, hash, observation.ErrorCode ?? "");
                    agentFileLog?.ToolCompleted(runId, turnId, step, hash, observation);
                    if (publish is not null) await publish(new("tool", $"TOOL_{step.Sequence}",
                        step.Label + (step.Status == "ERROR" ? ": cần điều chỉnh" : ": đã kiểm tra")), token);
                }, ct, requireUnderstanding: provider.SupportsRequestUnderstanding);
            var terminal = result.Terminal;
            modelTurns = result.ModelTurns; toolCalls = result.ToolCalls;
            var status = terminal.Kind switch { "PROPOSAL" or "REMINDER" => "PROPOSED", "ANSWER" => "COMPLETED", "QUESTION" => "WAITING_FOR_INPUT", _ => "BLOCKED" };
            // The bounded timeout may have expired. Final metadata uses a separate,
            // short token; it cannot apply any business changes.
            using var finish = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Checkpoint(status, finish.Token);
            agentFileLog?.RunCompleted(runId, turnId, status, modelTurns, toolCalls, simulations);
            var view = new NoraRunView(runId, "Agent", status, modelTurns, toolCalls, priorSteps.Concat(result.Trace).ToArray(), result.Understanding);
            if (terminal.Kind == "PROPOSAL")
            {
                var candidate = candidates[terminal.CandidateId!];
                return new(candidate.Intent with { Reply = terminal.Reply }, candidate, null, view);
            }
            if (terminal.Kind == "QUESTION")
                return new(NormalizeAgentIntent(EmptyRuntimeIntent(terminal.Reply) with {
                    Question = terminal.Question, Options = terminal.Options }, input.Text, state, pending), null, null, view);
            if (terminal.Kind == "REMINDER") return new(EmptyRuntimeIntent() with {Reminder=reminderCommand},null,null,view);
            if (terminal.Kind == "ANSWER") return new(EmptyRuntimeIntent(terminal.Reply), null, terminal.Reply, view);
            var stoppedReply = terminal.Reply is "AGENT_BUDGET_EXCEEDED" or "TOOL_BUDGET_EXCEEDED" or "AGENT_TIME_LIMIT"
                ? "NORA đã chạm giới hạn xử lý của lượt này trước khi hoàn tất. Lịch chưa thay đổi. Bạn có thể nhắn “Tiếp tục xử lý” trong hội thoại này; mình sẽ dùng lại thông tin bạn đã gửi."
                : terminal.Reply == "AGENT_INTENT_MISMATCH" ? "Mình chưa khớp được cách xử lý với điều bạn đang hỏi. Chưa có thay đổi nào được lưu. Bạn nói rõ hơn kết quả bạn muốn nhé."
                : terminal.Reply == "AGENT_REPEATED_ACTION" ? "Mình đã dừng vì đang lặp lại bước kiểm tra đã có kết quả. Lịch chưa thay đổi; bạn có thể nhắn tiếp để làm rõ phần cần xử lý."
                : lastToolFailure is { Length: > 0 } ? $"Chưa hoàn tất yêu cầu: {lastToolFailure} Lịch chưa thay đổi. Bạn có thể bổ sung ngay trong hội thoại này."
                : "NORA chưa hoàn tất được yêu cầu. Lịch chưa thay đổi. Bạn có thể nhắn tiếp trong hội thoại này để mình kiểm tra lại.";
            return new(EmptyRuntimeIntent(stoppedReply) with { Pending = pending ?? new(source, null, null, null, null, null) }, null, null, view, terminal.Reply);
        }
        catch (Exception failure)
        {
            agentFileLog?.Failed(runId, turnId, "EXECUTION", failure, ct.IsCancellationRequested);
            // Do not leave an abandoned lease after cancellation or provider errors.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                db.ChangeTracker.Clear();
                var saved = await Mine<AgentRun>(uid).SingleAsync(x => x.Id == runId, cleanup.Token);
                if (saved.Version == leaseVersion && saved.Status == "RUNNING")
                {
                    saved.Status = ct.IsCancellationRequested ? "CANCELLED" : "FAILED";
                    saved.ModelTurns = modelTurns; saved.ToolCalls = toolCalls; saved.Simulations = simulations;
                    saved.StateJson = ""; saved.LeaseUntil = Now; saved.UpdatedAt = Now; saved.Version = Guid.NewGuid();
                    await db.SaveChangesAsync(cleanup.Token);
                }
            }
            catch (Exception error) when (error is OperationCanceledException or DbUpdateException) { }
            throw;
        }
    }
}
