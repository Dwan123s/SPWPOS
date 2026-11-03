using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static readonly JsonSerializerOptions AgentJson = new(JsonSerializerDefaults.Web);
    private sealed record AgentChangeSet(WeeklyAvailability[]? Weekly, AvailabilityException[] Exceptions, FixedEvent[] Events, WorkTask[] Tasks, Guid[]? RemovedExceptionIds = null,
        Guid[]? UrgentEventIds = null, Guid[]? ClippedTimetableEventIds = null, bool AnalyzeTimetable = false);
    private sealed record AgentUndo(WeeklyAvailability[]? Weekly, Guid[] ExceptionIds, Guid[] EventIds,
        AvailabilityException[] OriginalExceptions, FixedEvent[] OriginalEvents,
        WorkTask[] OriginalTasks, Guid[] NewTaskIds, WorkSession[] OriginalSessions, Guid[] NewSessionIds, Guid? PlanId);
    private static T Copy<T>(T item) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(item, AgentJson), AgentJson)!;
    public NoteAgentCapabilities GetAgentCapabilities() => interpreter is IPlanningAgentInterpreter agent
        ? agent.Capabilities : new(false, Provider: interpreter.Capabilities.Provider);

    private static AgentTurnView TurnView(AgentTurn turn)
    {
        if (turn.ResultJson.Length > 0)
        {
            var view = JsonSerializer.Deserialize<AgentTurnView>(turn.ResultJson, AgentJson)!;
            return view with { Image = PublicAgentImage(view.Image, turn.Id) };
        }
        return new(turn.Id, turn.ConversationId, turn.RequestId, turn.Text, turn.Status == "DELETED" ? "Hội thoại đã được xóa. Yêu cầu cũ không được thực hiện lại." : "Đang xử lý yêu cầu…", turn.Status, "", null, [], [], turn.CreatedAt);
    }
    public async Task<AgentTurnView[]> GetAgentTurnsAsync(Guid uid, CancellationToken ct)
    {
        var rows = await Mine<AgentTurn>(uid).AsNoTracking().Where(x => x.Status != "DELETED" &&
            !db.AgentConversations.Any(c => c.UserId == uid && c.Id == x.ConversationId && (c.ArchivedAt != null || c.DeletedAt != null)))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(100).ToArrayAsync(ct);
        var revision = await Mine<WorkPreference>(uid).Select(x => x.Revision).SingleOrDefaultAsync(ct);
        return rows.Reverse().Select(x => TurnView(x) with { CanUndo = x.Status == "APPLIED" && x.AppliedRevision == revision }).ToArray();
    }

    public Task<AgentTurnView> SendAgentTurnAsync(Guid uid, AgentTurnInput input, CancellationToken ct) =>
        SendAgentTurnCoreAsync(uid, input, null, ct);
    public Task<AgentTurnView> SendAgentTurnStreamingAsync(Guid uid, AgentTurnInput input,
        Func<AgentStreamEvent, CancellationToken, Task> publish, CancellationToken ct) =>
        SendAgentTurnCoreAsync(uid, input, publish, ct);
    private async Task<AgentTurnView> SendAgentTurnCoreAsync(Guid uid, AgentTurnInput input,
        Func<AgentStreamEvent, CancellationToken, Task>? publish, CancellationToken ct)
    {
        Task Phase(string stage, string message) => publish?.Invoke(new("phase", stage, message), ct) ?? Task.CompletedTask;
        ValidateAgentImage(input.Image);
        Require(input.CalendarKind is "Auto" or "Event" or "WorkSession", "VALIDATION_ERROR", "Chọn loại lịch hợp lệ.");
        Require(input.ConversationId != Guid.Empty && input.RequestId != Guid.Empty && !string.IsNullOrWhiteSpace(input.Text) && input.Text.Length <= 2000,
            "VALIDATION_ERROR", "Nhập yêu cầu tối đa 2000 ký tự và mã hội thoại hợp lệ.");
        var autoApply = input.AutoApply && !PreviewOnly(input.Text);
        await Phase("READING", "Đang tiếp nhận yêu cầu của bạn");
        // Reserve the idempotency key before calling AI, then release the account lock.
        var reserved = await Mutate(uid, async () =>
        {
            var existing = await Mine<AgentTurn>(uid).SingleOrDefaultAsync(x => x.RequestId == input.RequestId, ct);
            if (existing is not null)
            {
                Require(existing.InputFingerprint.Length > 0 ? existing.InputFingerprint == AgentFingerprint(input) :
                    existing.Text == input.Text.Trim() && existing.ConversationId == input.ConversationId && !input.UseCapacityFactor && !input.UseEstimateBias && input.CalendarKind == "Auto",
                    "AGENT_REQUEST_REUSED", "Mã yêu cầu đã dùng cho một nội dung hoặc chính sách khác.", 409);
                if (existing.Status != "FAILED")
                {
                    Require(existing.Status != "PROCESSING" || existing.UpdatedAt < Now.AddMinutes(-2), "AGENT_BUSY", "Yêu cầu đang được xử lý. Chờ một chút rồi thử lại.", 409);
                    if (existing.Status != "PROCESSING") return (Turn: existing, Process: false);
                }
                var retainedImage = StoredAgentImage(existing);
                existing.Status = "PROCESSING"; existing.UpdatedAt = Now;
                existing.ResultJson = JsonSerializer.Serialize(new AgentTurnView(existing.Id, existing.ConversationId, existing.RequestId, existing.Text,
                    "Đang xử lý yêu cầu…", "PROCESSING", "", null, [], [], existing.CreatedAt, Image: input.Image ?? retainedImage), AgentJson);
                return (Turn: existing, Process: true);
            }
            var conversation = await Mine<AgentConversation>(uid).SingleOrDefaultAsync(x => x.Id == input.ConversationId, ct);
            Require(conversation?.DeletedAt is null, "CONVERSATION_DELETED", "Hội thoại này đã bị xóa. Hãy bắt đầu hội thoại mới.", 410);
            Require(conversation?.ArchivedAt is null, "CONVERSATION_ARCHIVED", "Khôi phục hội thoại trước khi gửi thêm yêu cầu.", 409);
            if (conversation is null)
            {
                conversation = new() { Id = input.ConversationId, UserId = uid, CreatedAt = Now, Title = input.Text.Trim()[..Math.Min(120, input.Text.Trim().Length)] };
                db.AgentConversations.Add(conversation);
            }
            conversation.UpdatedAt = Now; conversation.Version = Guid.NewGuid();
            var previousCreated = await Mine<AgentTurn>(uid).Where(x => x.ConversationId == input.ConversationId).OrderByDescending(x => x.CreatedAt).Select(x => x.CreatedAt).FirstOrDefaultAsync(ct);
            var turn = new AgentTurn { UserId = uid, ConversationId = input.ConversationId, RequestId = input.RequestId,
                InputFingerprint = AgentFingerprint(input), Text = input.Text.Trim(), CreatedAt = previousCreated >= Now ? previousCreated.AddTicks(1) : Now, UpdatedAt = Now, ExpiresAt = Now.AddMinutes(30) };
            if (input.Image is not null) turn.ResultJson = JsonSerializer.Serialize(new AgentTurnView(turn.Id, turn.ConversationId, turn.RequestId, turn.Text,
                "Đang xem ảnh…", "PROCESSING", "", null, [], [], turn.CreatedAt, Image: input.Image), AgentJson);
            db.AgentTurns.Add(turn); return (Turn: turn, Process: true);
        }, ct, false);
        if (!reserved.Process)
        {
            var saved = TurnView(reserved.Turn);
            return autoApply && saved.Status == "PREVIEW" && saved.Plan?.RequiresConfirmation != true
                ? await ApplyAgentTurnAsync(uid, saved.Id, ct) : saved;
        }
        var turnId = reserved.Turn.Id;
        // Keep the original chat text/fingerprint; the explicit UI choice is trusted request context for inference.
        input = input with { Text = CalendarKindPrompt(input) };
        try
        {
            // Shared by all model calls for this turn, including clarification
            // rechecks and review explanations. Calendar writes still use ct.
            using var aiBudget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            aiBudget.CancelAfter(TimeSpan.FromSeconds(90));
            var replyStarted = false;
            Task ReplyDelta(string delta, CancellationToken token)
            {
                if (delta.Length > 0) replyStarted = true;
                return publish?.Invoke(new("delta", Delta: delta), token) ?? Task.CompletedTask;
            }
            Require(interpreter is IPlanningAgentInterpreter && GetAgentCapabilities().AiAvailable,
                "AGENT_AI_NOT_CONFIGURED", "NORA chưa kết nối được với dịch vụ AI. Lịch đã lưu được giữ nguyên; hãy kiểm tra kết nối và thử lại.", 503);
            var state = await State(uid, ct); var revision = state.Preference.Revision;
            var historyRows = await Mine<AgentTurn>(uid).AsNoTracking().Where(x => x.ConversationId == input.ConversationId && x.Id != turnId &&
                x.Status != "PROCESSING" && x.Status != "DELETED").OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(8).ToArrayAsync(ct);
            var history = historyRows.Reverse().Select(x => new AgentMessage(x.Text,
                x.Status == "FAILED" ? "Yêu cầu thất bại; chưa lưu thay đổi từ yêu cầu này. Kiểm tra dữ liệu hiện tại trước khi trả lời."
                    : TurnView(x).Reply + " " + TurnView(x).Outcome + " " + TurnView(x).Question, x.Status)).ToArray();
            var latest = historyRows.FirstOrDefault();
            var visionImage = input.Image ?? historyRows.TakeWhile(row => row.Status is "NEEDS_INPUT" or "BLOCKED" or "FAILED")
                .Select(StoredAgentImage).FirstOrDefault(image => image?.Data is { Length: > 0 });
            // A brief calendar lookup between clarification replies does not
            // discard an uploaded timetable from this conversation.
            if (visionImage is null && TimetableFollowup(input.Text))
                visionImage = historyRows.Where(row => row.Status is "NEEDS_INPUT" or "BLOCKED" or "FAILED")
                    .Select(StoredAgentImage).FirstOrDefault(image => image?.Data is { Length: > 0 });
            var pending = latest?.Status is "NEEDS_INPUT" or "BLOCKED" && latest.CommandJson.Length > 0
                ? JsonSerializer.Deserialize<AgentIntent>(latest.CommandJson, AgentJson)?.Pending : null;
            pending = ConfirmDurationProposal(pending, input.Text);
            var activeTaskTypes = await db.TaskTypes.AsNoTracking().Where(x => x.IsActive).Select(x => x.Code).OrderBy(x => x).ToArrayAsync(ct);
            var nativeAgent = noraOptions?.Value.Mode == "Agent" && interpreter is INoraAgentModel { AgentAvailable: true };
            var (context, contextNotices) = nativeAgent ? ("", Array.Empty<string>())
                : BuildAgentContext(await AgentCalendarStateAsync(uid, state, ct), input.Text, activeTaskTypes, pending);
            await Phase("UNDERSTANDING", "Đang hiểu yêu cầu và soạn phản hồi");
            var agent = (IPlanningAgentInterpreter)interpreter;
            RuntimeExecution? runtime = null;
            string? scheduleReviewReply = null;
            AgentIntent intent;
            if (nativeAgent)
            {
                runtime = await RunNoraAgentAsync(uid, turnId, input, state, history, pending, activeTaskTypes, visionImage, publish, ct);
                intent = runtime.Intent;
                scheduleReviewReply = runtime.ReadReply;
            }
            else
            {
                intent = (visionImage is null ? SavedTaskReviewIntent(input.Text, state) : null) ?? await InterpretWithRecoveryAsync(uid, token => visionImage is not null
                    ? agent.InterpretImageAsync(input.Text, context, history, state.Preference.TimeZoneId, Now, visionImage,
                        publish is null ? null : ReplyDelta, token)
                    : publish is null
                    ? agent.InterpretAsync(input.Text, context, history, state.Preference.TimeZoneId, Now, token)
                    : agent.InterpretStreamingAsync(input.Text, context, history, state.Preference.TimeZoneId, Now,
                        ReplyDelta, token), () => replyStarted,
                    () => Phase("RECHECKING", "AI đang bận. Mình đang thử lại yêu cầu của bạn…"), aiBudget.Token);
                intent = LimitAgentBatch(CoalesceEquivalentCalendarQueries(intent));
                ValidateIntent(intent, true);
                if (ClarificationRecoveryContext(intent, input.Text, context, activeTaskTypes) is { } recoveredContext)
                {
                    await Phase("RECHECKING", "Đang đối chiếu chi tiết bạn đã cung cấp");
                    // Exactly one bounded recheck. A separate reservation records its
                    // provider usage and respects the ordinary quota/concurrency limits.
                    intent = await RunJob(uid, "AI_REQUEST", () => visionImage is not null
                        ? agent.InterpretImageAsync(input.Text, recoveredContext, history, state.Preference.TimeZoneId, Now, visionImage, null, aiBudget.Token)
                        : agent.InterpretAsync(input.Text, recoveredContext, history, state.Preference.TimeZoneId, Now, aiBudget.Token), aiBudget.Token);
                }
                intent = LimitAgentBatch(intent);
                intent = RecognizeAgentOptimization(intent, input.Text, state.Preference);
                intent = RecognizeScheduleReview(intent, input.Text);
                if (intent.ScheduleReviewQuery is not null)
                {
                    intent = GroundAgentDates(intent, input.Text, state.Preference, Now);
                    ValidateIntent(intent);
                    if (intent.ScheduleReviewQuery is not null)
                    {
                        await Phase("ANALYZING", "Đang phân tích lịch đã lưu, việc gần hạn và các phương án phù hợp");
                        scheduleReviewReply = await ExplainScheduleReviewAsync(uid, intent, input.Text, state, activeTaskTypes, aiBudget.Token);
                    }
                }
            }
            if (runtime?.ReadReply is null && intent.Question.Length == 0)
                await Phase("PLANNING", "Đang kiểm tra khung giờ, deadline và xung đột");
            // Another HTTP request could have modified the account during the AI call.
            db.ChangeTracker.Clear();
            var view = await Mutate(uid, async () =>
            {
                var turn = await Find<AgentTurn>(uid, turnId, ct); var current = await State(uid, ct);
                Require(current.Preference.Revision == revision, "AGENT_STALE", "Lịch vừa thay đổi trong khi AI xử lý. Hãy thử lại với dữ liệu mới.", 409);
                ValidateIntent(intent, true);
                var currentTaskTypes = intent.Tasks.Length == 0 ? [] : await db.TaskTypes.AsNoTracking().Where(x => x.IsActive).Select(x => x.Code).ToArrayAsync(ct);
                var interpreted = intent;
                if (runtime is null)
                {
                    intent = NormalizeAgentCategories(intent, current, currentTaskTypes, input.Text);
                    intent = ConstrainEventReplanning(intent, input.Text, current);
                    intent = NormalizeDatedFreeReplacement(intent, input.Text);
                    intent = NormalizeAgentIntent(intent, input.Text, current, pending);
                    intent = GroundAgentDates(intent, input.Text, current.Preference, Now);
                }
                intent = RespectCalendarKind(intent, input.CalendarKind);
                var categoryNotices = AgentCategoryNotices(interpreted, intent, input.Text, currentTaskTypes);
                ValidateIntent(intent);
                if (runtime?.ReadReply is { } directReply && !HasAgentActions(intent))
                    return SaveTurn(turn, new(turn.Id, turn.ConversationId, turn.RequestId, turn.Text, directReply,
                        "ANSWERED", "", null, [], [], turn.CreatedAt, Run: runtime.View));
                if (intent.FreeTimeQuery is not null) current = await AgentCalendarStateAsync(uid, current, ct);
                if (intent.OptimizationQuery is { } optimizationScope)
                {
                    var calendar = await CalendarStateAsync(uid, current, optimizationScope.Start, optimizationScope.End, ct);
                    current = current with { Sessions = calendar.Sessions.Concat(current.Sessions).DistinctBy(x => x.Id).ToArray() };
                }
                turn.ExpectedRevision = revision; turn.CommandJson = JsonSerializer.Serialize(intent, AgentJson); turn.ExpiresAt = Now.AddMinutes(30);
                if (runtime?.BlockCode is { } blockCode)
                    return SaveTurn(turn, new(turn.Id, turn.ConversationId, turn.RequestId, turn.Text, intent.Reply,
                        "BLOCKED", "", null, [], [], turn.CreatedAt, ErrorCode: blockCode, Run: runtime.View));
                if (intent.Question.Length > 0)
                {
                    var options = intent.Options ?? [];
                    return SaveTurn(turn, new(turn.Id, turn.ConversationId, turn.RequestId, turn.Text, intent.Reply,
                        "NEEDS_INPUT", intent.Question, null, [], contextNotices, turn.CreatedAt, false, options, Run: runtime?.View));
                }
                var eventRequest = ChatMatch(input.Text, @"\b(khong (?:can )?(?:khan|gap|uu tien)|binh thuong)\b").Success ? input.Text :
                    string.Join("\n", historyRows.TakeWhile(row => row.Status == "NEEDS_INPUT").Reverse().Select(row => row.Text).Append(input.Text));
                var timetableContext = visionImage is not null || IsTimetableRequest(input.Text) ||
                    historyRows.TakeWhile(row => row.Status is "NEEDS_INPUT" or "FAILED" or "BLOCKED").Any(row => IsTimetableRequest(row.Text));
                if (timetableContext) current = await AgentCalendarStateAsync(uid, current, ct);
                var selected = runtime?.Candidate;
                var (changes, candidate, affected, summary, timetableNotices) = selected is null
                    ? await BuildAgentChangesAsync(uid, intent, current, input.Text, ct, eventRequest, timetableContext)
                    : (selected.Changes, selected.State, selected.Affected, selected.Summary, selected.Notices);
                var hasChanges = changes.Weekly is not null || changes.Exceptions.Length > 0 || changes.Events.Length > 0 || changes.Tasks.Length > 0;
                var planInput = new PlanInput(intent.OptimizationQuery is { } scope && scope.Start > Now.AddMinutes(5) ? scope.Start : Now.AddMinutes(5),
                    intent.HorizonDays, affected, true, "AVAILABILITY_CHANGED", UseCapacityFactor: input.UseCapacityFactor, UseEstimateBias: input.UseEstimateBias)
                    { WindowEnd = intent.OptimizationQuery?.End, AllowDeadlineRescue = DeadlineRescueAllowed(input.Text) };
                var planningCandidate = selected is null ? ProtectUnselectedSessionReferences(eventRequest, candidate, intent.Events) : candidate;
                PlanView? plan = selected is not null ? selected.Plan : changes.UrgentEventIds is { Length: > 0 } urgentEventIds
                    ? engine.GenerateForUrgentEvents(planningCandidate, planInput, Now, urgentEventIds)
                    : affected.Length > 0 ? engine.Generate(planningCandidate, planInput, Now) : null;
                if (plan is not null && intent.OptimizationQuery is not null) plan = plan with { IsOptimization = true };
                if (selected is null && plan is not null)
                    plan = RespectSchedulingExclusions(plan, candidate, affected, input);
                if (plan is not null)
                {
                    // Reuse equivalent scheduled rows. An unchanged timetable must
                    // not cancel/recreate sessions or create a misleading undo.
                    var used = plan.Sessions.Where(x => x.Preserved).Select(x => x.Id).ToHashSet();
                    plan = plan with { Sessions = plan.Sessions.Select(session =>
                    {
                        if (session.Preserved) return session;
                        var same = current.Sessions.FirstOrDefault(x => !used.Contains(x.Id) && x.Status == WorkSessionStatus.Scheduled &&
                            x.TaskId == session.TaskId && x.Start == session.Start && x.End == session.End);
                        if (same is null) return session;
                        used.Add(same.Id); return session with { Id = same.Id, Preserved = true };
                    }).ToArray() };
                    hasChanges |= plan.Sessions.Any(x => !x.Preserved) || current.Sessions.Any(x => x.Status == WorkSessionStatus.Scheduled &&
                        x.Start >= plan.Start && x.Start < plan.End && !plan.Sessions.Any(s => s.Preserved && s.Id == x.Id));
                }
                var warnings = plan?.Warnings.Select(x => $"{x.TaskName}: {x.Message}" + (x.UnscheduledMinutes > 0 ? $" Thiếu {x.UnscheduledMinutes} phút." : "")).ToArray() ?? [];
                var blocked = plan?.Warnings.Any(x => x.UnscheduledMinutes > 0 || x.Code == "SOFT_DEADLINE_EXCEEDED") == true;
                if (plan is not null)
                {
                    var retained = plan.Sessions.Where(x => x.Preserved).Select(x => x.Id).ToHashSet();
                    var movements = current.Sessions.Where(x => x.Status == WorkSessionStatus.Scheduled && x.Start >= plan.Start && x.Start < plan.End && !retained.Contains(x.Id))
                        .GroupBy(x => x.TaskId).Select(group => new AgentChangeView("SESSION", candidate.Tasks.Single(x => x.Id == group.Key).Name,
                            string.Join("; ", group.Select(x => LocalWindow(x.Start, x.End, current.Preference))),
                            string.Join("; ", plan.Sessions.Where(x => !x.Preserved && x.TaskId == group.Key).Select(x => LocalWindow(x.Start, x.End, current.Preference))), group.Key,
                            Reason: changes.UrgentEventIds is { Length: > 0 } && candidate.Tasks.Single(x => x.Id == group.Key).Priority == 4
                                ? "Lịch khẩn mới được ưu tiên trước lịch khẩn cũ. Dời phiên khẩn cũ, giữ đủ thời lượng và deadline." :
                                intent.OptimizationQuery is not null ? "Xếp lại trong phạm vi bạn chọn, theo hạn chót, mức ưu tiên và giờ làm việc đã cài đặt." : ""));
                    summary = summary.Concat(movements.Where(x => x.Before != x.After)).ToArray();
                    if (intent.OptimizationQuery is not null)
                        summary = summary.Concat(plan.Sessions.Where(x => !x.Preserved && !summary.Any(change => change.TaskId == x.TaskId))
                            .GroupBy(x => x.TaskId).Select(group => new AgentChangeView("SESSION", candidate.Tasks.Single(x => x.Id == group.Key).Name,
                                "", string.Join("; ", group.Select(x => LocalWindow(x.Start, x.End, current.Preference))), group.Key))).ToArray();
                }
                turn.ChangesJson = JsonSerializer.Serialize(changes, AgentJson);
                if (blocked && intent.Tasks is [var retryTask] && changes.Tasks is [var retry])
                    turn.CommandJson = JsonSerializer.Serialize(intent with { Pending = new(pending?.OriginalText ?? input.Text,
                        retryTask.TaskId, retry.WorkWindowStart, retry.WorkWindowEnd, retryTask.EstimatedMinutes ?? retry.EstimatedMinutes,
                        retry.Deadline, pending is null ? [] : (pending.Answers ?? []).Append(input.Text).TakeLast(4).ToArray()) }, AgentJson);
                var blockedDetail = warnings.Length > 0 ? $" ({string.Join("; ", warnings)})" : "";
                var replyMessage = blocked
                    ? $"Chưa thể lưu toàn bộ yêu cầu vì lịch chưa khả thi{blockedDetail}. Lịch chưa thay đổi. " +
                        (BlockedWorkWindowSuggestions(candidate, plan!, input.UseEstimateBias) is { Length: > 0 } alternatives ? alternatives : "Bạn muốn điều chỉnh điều kiện nào?")
                    : intent.OptimizationQuery is not null ? OptimizationReply(plan, summary, current.Preference, hasChanges)
                    : hasChanges && plan?.HasUrgentReplan == true
                        ? UrgentReplanReply(changes, candidate, summary)
                    : hasChanges && plan?.HasDeadlineProtection == true
                        ? "Có việc khẩn hoặc gần hạn chưa có đủ lịch. Đề xuất đã dành thời gian cho các việc này trước khi xếp việc mới. Chưa lưu thay đổi; xem các phiên bên dưới rồi bấm Xác nhận kế hoạch để áp dụng."
                    : hasChanges ? AgentExplanation(intent.Reply) is { Length: > 0 } explanation ? explanation : AgentActionReply(summary, false)
                    : timetableNotices.Length > 0 && changes.Events.Length == 0 && intent.Events.Length > 0
                        ? "Thời khóa biểu này không còn buổi học nào cần thêm vào lịch từ hiện tại trở đi. " + string.Join(" ", timetableNotices)
                    : affected.Length > 0 || intent.Events.Length > 0 || intent.Tasks.Length > 0 || intent.Exceptions.Length > 0 || intent.ReplaceWeeklyAvailability
                        ? "Lịch hiện tại đã đúng với yêu cầu; không có thay đổi cần lưu."
                        : scheduleReviewReply ?? ScopedReadOnlyReply(intent, input.Text, current, history, contextNotices);
                if (!blocked && changes.AnalyzeTimetable)
                    replyMessage = TimetableAnalysis(candidate, changes.Events, plan,
                        applied: false);
                return SaveTurn(turn, new(turn.Id, turn.ConversationId, turn.RequestId, turn.Text,
                    replyMessage,
                    blocked ? "BLOCKED" : hasChanges ? "PREVIEW" : "ANSWERED", "", hasChanges || blocked ? plan : null, summary, warnings.Concat(contextNotices).Concat(categoryNotices).Concat(hasChanges || blocked ? timetableNotices : []).ToArray(), turn.CreatedAt,
                    Outcome: hasChanges && !blocked ? "Đã kiểm tra phương án. Các thay đổi đang chờ bạn áp dụng." : "", Run: runtime?.View));
            }, ct, false);
            if (autoApply && view.Status == "PREVIEW" && view.Plan?.RequiresConfirmation != true)
            {
                await Phase("SAVING", "Đang lưu lịch đã kiểm tra");
                return await ApplyAgentTurnAsync(uid, view.Id, ct);
            }
            return view;
        }
        catch (Exception e) when (e is AuthException or OperationCanceledException or IOException)
        {
            var failure = e is OperationCanceledException && !ct.IsCancellationRequested
                ? new AuthException("AGENT_AI_TIMEOUT", "AI phản hồi quá lâu. Hãy thử lại; lịch chưa thay đổi.", 503) : e;
            db.ChangeTracker.Clear();
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await Mutate(uid, async () =>
                {
                    var turn = await Find<AgentTurn>(uid, turnId, cleanup.Token);
                    if (turn.Status is "APPLIED" or "UNDONE") return;
                    var message = failure is AuthException error ? error.Message : "Yêu cầu đã dừng. Lịch chưa thay đổi; bạn có thể thử lại.";
                    var runView = noraOptions?.Value.Mode == "Agent" ? await RuntimeTraceAsync(uid, turn.Id, cleanup.Token) : null;
                    SaveTurn(turn, new(turn.Id, turn.ConversationId, turn.RequestId, turn.Text, message, "FAILED", "", null, [], [], turn.CreatedAt,
                        ErrorCode: failure is AuthException authFailure ? authFailure.Code : "AGENT_CANCELLED", Run: runView));
                }, cleanup.Token, false);
            }
            catch (Exception cleanupError) when (cleanupError is AuthException or OperationCanceledException or DbUpdateException) { /* Retry can reclaim PROCESSING after its lease expires. */ }
            if (!ReferenceEquals(failure, e)) throw failure;
            throw;
        }
    }

    private static AgentTurnView SaveTurn(AgentTurn turn, AgentTurnView view)
    {
        var image = StoredAgentImage(turn);
        turn.Status = view.Status;
        turn.ResultJson = JsonSerializer.Serialize(view with { Image = image ?? view.Image }, AgentJson);
        return view with { Image = PublicAgentImage(image ?? view.Image, turn.Id) };
    }
    private static DateOnly LocalDate(DateTimeOffset at, WorkPreference preference) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, PlanningEngine.Zone(preference.TimeZoneId)).DateTime);
    private static void ValidateIntent(AgentIntent intent, bool shapeOnly = false)
    {
        Require(intent is not null && intent.Reply is not null && intent.Reply.Length <= 4000 && intent.Question is not null && intent.Question.Length <= 1000 &&
            intent.WeeklySlots is { Length: <= 28 } && intent.Exceptions is { Length: <= 28 } && intent.Events is { Length: <= 12 } &&
            (intent.Options is null || intent.Options.Length <= 6 && intent.Options.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 120)) &&
            intent.Tasks is { Length: <= 12 } && intent.ScheduleTaskIds is { Length: <= 500 } && intent.HorizonDays is >= 1 and <= 30,
            "AGENT_INVALID_INTENT", "AI trả yêu cầu vượt giới hạn hoặc thiếu dữ liệu hợp lệ.", 503);
        Require(intent.Tasks.All(x => x is not null) && intent.Events.All(x => x is not null) && intent.Exceptions.All(x => x is not null) && intent.WeeklySlots.All(x => x is not null),
            "AGENT_INVALID_INTENT", "AI trả danh sách thay đổi chưa hợp lệ; lịch chưa thay đổi.", 503);
        Require(intent.FreeTimeQuery is null || intent.Question.Length == 0 && !HasAgentActions(intent) &&
            intent.FreeTimeQuery.End > intent.FreeTimeQuery.Start && intent.FreeTimeQuery.End - intent.FreeTimeQuery.Start <= TimeSpan.FromDays(31),
            "AGENT_INVALID_INTENT", "Yêu cầu xem giờ rảnh phải có phạm vi hợp lệ và không kèm thay đổi lịch.", 503);
        Require(intent.ScheduleReviewQuery is null || intent.FreeTimeQuery is null && intent.Question.Length == 0 && !HasAgentActions(intent) &&
            intent.ScheduleReviewQuery.End > intent.ScheduleReviewQuery.Start && intent.ScheduleReviewQuery.End - intent.ScheduleReviewQuery.Start <= TimeSpan.FromDays(31),
            "AGENT_INVALID_INTENT", "Yêu cầu tư vấn lịch phải có phạm vi hợp lệ và không kèm thay đổi lịch.", 503);
        Require(intent.OptimizationQuery is null || intent.Reschedule && intent.Question.Length == 0 && intent.FreeTimeQuery is null &&
            intent.ScheduleReviewQuery is null && !intent.ReplaceWeeklyAvailability && intent.WeeklySlots.Length == 0 && intent.Exceptions.Length == 0 &&
            intent.Events.Length == 0 && intent.Tasks.Length == 0 && intent.OptimizationQuery.End > intent.OptimizationQuery.Start &&
            intent.OptimizationQuery.End - intent.OptimizationQuery.Start <= TimeSpan.FromDays(31),
            "AGENT_INVALID_INTENT", "Yêu cầu tối ưu cần phạm vi hợp lệ và chỉ xếp lại công việc đã có.", 503);
        if (shapeOnly) return;
        var kind = intent.Question.Length > 0 ? "CLARIFICATION" : HasAgentActions(intent) ? "MUTATION" : "ANSWER";
        Require(intent.ResultKind is null || intent.ResultKind == kind, "AGENT_INVALID_INTENT", "Trạng thái câu trả lời không khớp với yêu cầu thay đổi; lịch chưa thay đổi.", 503);
        Require(intent.Question.Length == 0 || !intent.ReplaceWeeklyAvailability && intent.WeeklySlots.Length == 0 && intent.Exceptions.Length == 0 && intent.Events.Length == 0 && intent.Tasks.Length == 0 && intent.ScheduleTaskIds.Length == 0 && !intent.Reschedule,
            "AGENT_INVALID_INTENT", "Yêu cầu cần làm rõ không được kèm thay đổi dữ liệu.", 503);
    }

    private async Task<(AgentChangeSet Changes, PlanningState State, Guid[] Affected, AgentChangeView[] Summary, string[] TimetableNotices)> BuildAgentChangesAsync(Guid uid, AgentIntent intent, PlanningState state, string request, CancellationToken ct, string? eventRequest = null, bool timetableContext = false)
    {
        var summary = new List<AgentChangeView>();
        WeeklyAvailability[]? weekly = null;
        if (intent.ReplaceWeeklyAvailability || intent.WeeklySlots.Length > 0)
        {
            foreach (var slot in intent.WeeklySlots) { Require(slot is not null && slot.DayOfWeek is >= 0 and <= 6, "VALIDATION_ERROR", "Ngày trong tuần không hợp lệ."); Window(slot.StartMinute, slot.EndMinute); }
            foreach (var group in intent.WeeklySlots.GroupBy(x => x.DayOfWeek))
            {
                var ordered = group.OrderBy(x => x.StartMinute).ToArray();
                Require(!ordered.Zip(ordered.Skip(1)).Any(x => x.First.EndMinute > x.Second.StartMinute), "AVAILABILITY_OVERLAP", "Các khoảng rảnh hàng tuần trùng nhau.");
            }
            weekly = intent.WeeklySlots.Select(x => new WeeklyAvailability { UserId = uid, DayOfWeek = x.DayOfWeek, StartMinute = x.StartMinute, EndMinute = x.EndMinute }).ToArray();
            if (!intent.ReplaceWeeklyAvailability)
                weekly = weekly.Concat(state.Availability.Where(x => !intent.WeeklySlots.Any(slot => slot.DayOfWeek == x.DayOfWeek)).Select(Copy)).ToArray();
            static string WeeklyText(IEnumerable<WeeklyAvailability> slots) => string.Join("; ", slots.OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartMinute)
                .Select(x => $"{(x.DayOfWeek == 0 ? "Chủ nhật" : "Thứ " + (x.DayOfWeek + 1))} {x.StartMinute / 60:00}:{x.StartMinute % 60:00}–{x.EndMinute / 60:00}:{x.EndMinute % 60:00}"));
            summary.Add(new("AVAILABILITY", "Lịch rảnh hàng tuần", WeeklyText(state.Availability), WeeklyText(weekly)));
            if (WeeklyText(state.Availability) == WeeklyText(weekly)) { weekly = null; summary.RemoveAt(summary.Count - 1); }
        }
        var exceptions = new List<AvailabilityException>(); var editedExceptionIds = new HashSet<Guid>();
        var replaceDatedFreeHours = ChatMatch(request, @"\bthay (?:the )?(?:gio|khung(?: gio)?) ranh\b").Success;
        foreach (var item in intent.Exceptions)
        {
            Require(item is not null && item.Note is not null && item.Note.Length <= 300,
                "VALIDATION_ERROR", "Ngoại lệ không hợp lệ hoặc ghi chú vượt quá 300 ký tự.");
            Require(item!.Date >= LocalDate(Now, state.Preference), "VALIDATION_ERROR", "Ngoại lệ không được áp dụng cho ngày trong quá khứ.");
            Require(item.Date <= LocalDate(Now.AddDays(30), state.Preference), "VALIDATION_ERROR", "Ngoại lệ chỉ áp dụng trong phạm vi 30 ngày tới.");
            Window(item.StartMinute, item.EndMinute);
            var original = item.ExistingId is { } existingId ? state.Exceptions.SingleOrDefault(x => x.Id == existingId) : null;
            Require(item.ExistingId is null || original is not null, "NOT_FOUND", "Ngoại lệ không thuộc tài khoản.", 404);
            Require(item.ExistingId is null || editedExceptionIds.Add(item.ExistingId.Value), "VALIDATION_ERROR", "Ngoại lệ bị chỉnh sửa nhiều lần trong cùng yêu cầu.");
            if (item.ExistingId is null && state.Exceptions.Concat(exceptions).Any(x => x.Date == item.Date && x.StartMinute == item.StartMinute && x.EndMinute == item.EndMinute && x.IsAvailable == item.IsAvailable && x.Note == item.Note)) continue;
            var change = new AvailabilityException { Id = item.ExistingId ?? Guid.NewGuid(), UserId = uid, Date = item.Date, StartMinute = item.StartMinute, EndMinute = item.EndMinute, IsAvailable = item.IsAvailable, Note = item.Note };
            change.OverridesWeeklyAvailability = item.IsAvailable && (replaceDatedFreeHours || original?.OverridesWeeklyAvailability == true ||
                ChatMatch(request, @"\b(chi ranh|ngoai khoang|ngoai gio|ban het)\b").Success);
            if (original is not null && original.Date == change.Date && original.StartMinute == change.StartMinute && original.EndMinute == change.EndMinute && original.IsAvailable == change.IsAvailable && original.Note == change.Note && original.OverridesWeeklyAvailability == change.OverridesWeeklyAvailability) continue;
            exceptions.Add(change);
            summary.Add(new("EXCEPTION", change.OverridesWeeklyAvailability ? "Giờ rảnh duy nhất trong ngày" : item.IsAvailable ? "Rảnh thêm" : "Bận", original is null ? "" : $"{original.Date:yyyy-MM-dd} · {original.StartMinute / 60:00}:{original.StartMinute % 60:00}–{original.EndMinute / 60:00}:{original.EndMinute % 60:00} · {(original.IsAvailable ? "Rảnh" : "Bận")}",
                $"{item.Date:yyyy-MM-dd} {item.StartMinute / 60:00}:{item.StartMinute % 60:00}–{item.EndMinute / 60:00}:{item.EndMinute % 60:00}"));
        }
        // Editing one saved interval is a partial update. Only newly supplied
        // exclusive intervals replace other unmentioned free hours on that date.
        var replacedDates = exceptions.Where(x => x.OverridesWeeklyAvailability && x.IsAvailable &&
            (replaceDatedFreeHours || !editedExceptionIds.Contains(x.Id))).Select(x => x.Date).ToHashSet();
        var removedExceptions = state.Exceptions.Where(x => x.IsAvailable && replacedDates.Contains(x.Date) &&
            !exceptions.Any(e => e.Id == x.Id) && !intent.Exceptions.Any(e => e.IsAvailable && e.Date == x.Date && e.StartMinute == x.StartMinute && e.EndMinute == x.EndMinute)).ToArray();
        foreach (var previous in removedExceptions)
        {
            summary.Add(new("EXCEPTION", "Thay thế khung rảnh cũ", $"{previous.Date:yyyy-MM-dd} {previous.StartMinute / 60:00}:{previous.StartMinute % 60:00}–{previous.EndMinute / 60:00}:{previous.EndMinute % 60:00}", "Chỉ sử dụng khung giờ mới trong ngày này."));
        }
        var events = new List<FixedEvent>(); var editedEventIds = new HashSet<Guid>();
        var cutoff = Now; var skippedClasses = 0; var clippedClasses = new HashSet<Guid>();
        foreach (var item in intent.Events)
        {
            Require(item is not null, "VALIDATION_ERROR", "Sự kiện không hợp lệ.");
            var trimTimetable = timetableContext && item!.ExistingId is null && item.EventType == "CLASS";
            Require(trimTimetable || item!.Start >= cutoff, "VALIDATION_ERROR", "Thời gian bắt đầu sự kiện đã trôi qua. Vui lòng chọn khung giờ trong tương lai hoặc chỉ rõ ngày mai.");
            var original = item.ExistingId is { } existingId ? state.Events.SingleOrDefault(x => x.Id == existingId) : null;
            Require(item.ExistingId is null || original is not null, "NOT_FOUND", "Sự kiện không thuộc tài khoản.", 404);
            Require(item.ExistingId is null || original!.Start > Now && item.RepeatWeeks == 1 && editedEventIds.Add(item.ExistingId.Value), "VALIDATION_ERROR", "Chỉ sửa một lần xuất hiện chưa bắt đầu.");
            var occurrences = EventOccurrences(uid, new(item.Name, item.Start, item.End, item.Description, item.EventType, item.RepeatWeeks,
                IsUrgent: original is not null && IsUrgentEvent(original)), state.Preference);
            if (trimTimetable)
            {
                // Expand at the original local weekday/clock time first. Filtering
                // must never extend a series to replace its elapsed occurrences.
                skippedClasses += occurrences.Count(occurrence => occurrence.End <= cutoff);
                occurrences = occurrences.Where(occurrence => occurrence.End > cutoff).ToArray();
                foreach (var occurrence in occurrences.Where(occurrence => occurrence.Start < cutoff))
                { occurrence.Start = cutoff; clippedClasses.Add(occurrence.Id); }
                if (occurrences.Length == 0) continue;
            }
            Require(occurrences[0].Start <= cutoff.AddDays(30), "VALIDATION_ERROR", "Sự kiện mới chỉ được lên lịch trong vòng 30 ngày tới.");
            foreach (var occurrence in occurrences)
                if (RequestedUrgentEvent(occurrence, eventRequest ?? request)) occurrence.IsUrgent = true;
            if (item.ExistingId is null && occurrences.All(occurrence => state.Events.Any(x => x.Name == occurrence.Name &&
                (x.Start == occurrence.Start || clippedClasses.Contains(occurrence.Id) && x.Start <= occurrence.Start) &&
                x.End == occurrence.End && x.Description == occurrence.Description && x.EventType == occurrence.EventType && IsUrgentEvent(x) == occurrence.IsUrgent))) continue;
            if (item.ExistingId is { } occurrenceId) occurrences[0].Id = occurrenceId;
            if (original is not null && original.Name == occurrences[0].Name && original.Start == occurrences[0].Start && original.End == occurrences[0].End && original.Description == occurrences[0].Description && original.EventType == occurrences[0].EventType && IsUrgentEvent(original) == occurrences[0].IsUrgent) continue;
            events.AddRange(occurrences);
            foreach (var occurrence in occurrences) summary.Add(new("EVENT", occurrence.Name, original is null ? "" : LocalWindow(original.Start, original.End, state.Preference), LocalWindow(occurrence.Start, occurrence.End, state.Preference)));
        }
        Guid[] urgentEventIds = [];
        if (events.Count > 0)
        {
            var eventState = await CalendarStateAsync(uid, state with { Availability = weekly ?? state.Availability,
                Exceptions = state.Exceptions.Where(x => !exceptions.Any(e => e.Id == x.Id) && !removedExceptions.Any(e => e.Id == x.Id)).Concat(exceptions).ToArray() },
                events.Min(x => x.Start), events.Max(x => x.End).AddDays(8), ct);
            var prepared = PrepareAgentEvents(eventState, events.ToArray(), eventRequest ?? request, timetableContext);
            foreach (var moved in prepared.Events.Where(item => !events.Any(original => original.Id == item.Id)))
            {
                var original = state.Events.Single(item => item.Id == moved.Id);
                summary.Add(new("EVENT", moved.Name, LocalWindow(original.Start, original.End, state.Preference), LocalWindow(moved.Start, moved.End, state.Preference),
                    Reason: EventMoveReason(original)));
            }
            events = prepared.Events.ToList(); urgentEventIds = prepared.UrgentIds;
        }
        var taskChanges = new List<WorkTask>(); var affected = intent.ScheduleTaskIds.ToHashSet(); var editedTaskIds = new HashSet<Guid>();
        Require(affected.All(id => state.Tasks.Any(x => x.Id == id && x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled)), "NOT_FOUND", "Công việc không thuộc tài khoản hoặc đã kết thúc.", 404);
        foreach (var item in intent.Tasks)
        {
            Require(item is not null && item.Name is not null && item.TaskType is not null && (item.TaskId is null || editedTaskIds.Add(item.TaskId.Value)), "VALIDATION_ERROR", "Công việc bị lặp hoặc không hợp lệ.");
            WorkTask task;
            if (item.TaskId is { } id)
            {
                var original = state.Tasks.SingleOrDefault(x => x.Id == id);
                Require(original is not null && original.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled, "NOT_FOUND", "Không tìm thấy công việc đang hoạt động thuộc tài khoản.", 404);
                task = Copy(original); task.Deadline = item.Deadline ?? task.Deadline; task.EstimatedMinutes = item.EstimatedMinutes ?? task.EstimatedMinutes;
                if (!string.IsNullOrWhiteSpace(item.TaskType)) task.TaskType = item.TaskType;
                task.Priority = item.Priority ?? task.Priority; task.Splittable = item.Splittable ?? task.Splittable;
                task.WorkWindowStart = item.WorkWindowStart ?? task.WorkWindowStart; task.WorkWindowEnd = item.WorkWindowEnd ?? task.WorkWindowEnd;
                if (item.Deadline is not null) task.IsHardDeadline = !item.DeadlineFromWorkWindow;
                if (task.Deadline == original.Deadline && task.EstimatedMinutes == original.EstimatedMinutes && task.Priority == original.Priority &&
                    task.Splittable == original.Splittable && task.IsHardDeadline == original.IsHardDeadline && task.WorkWindowStart == original.WorkWindowStart && task.WorkWindowEnd == original.WorkWindowEnd && task.TaskType == original.TaskType && item.Importance == null) continue;
                task.UpdatedAt = Now;
                summary.Add(new("TASK", task.Name, AgentTaskSummary(original, state.Preference), AgentTaskSummary(task, state.Preference), task.Id));
            }
            else
            {
                Require(item.Deadline is not null && item.EstimatedMinutes is not null, "AGENT_MISSING_TASK_DETAILS", "Cần deadline và thời lượng cho công việc mới.");
                task = NewTask(uid, new(item.Name, "", item.Deadline.Value, item.EstimatedMinutes.Value, item.Priority ?? 2, item.TaskType,
                    item.Splittable ?? true, Math.Min(state.Preference.MinimumSessionMinutes, Math.Max(5, item.EstimatedMinutes.Value)), IsHardDeadline: !item.DeadlineFromWorkWindow, CreationSource: "NATURAL_LANGUAGE"), Now);
                task.WorkWindowStart = item.WorkWindowStart; task.WorkWindowEnd = item.WorkWindowEnd;
                summary.Add(new("TASK", task.Name, "", AgentTaskSummary(task, state.Preference), task.Id));
            }
            if (item.Importance != null && (item.TaskId == null || task.ImportanceConfirmedAt == null ||
                System.Text.RegularExpressions.Regex.IsMatch(request, "quan trọng|quan trong|importance|ít quan trọng|it quan trong", System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))))
            {
                var suggestion = SuggestedImportance(item.Importance, request);
                var explicitCorrection = item.TaskId != null && System.Text.RegularExpressions.Regex.IsMatch(request, "quan trọng|quan trong|importance", System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
                if (suggestion.Level != "Unknown" || item.TaskId == null) WriteImportance(task, suggestion, explicitCorrection ? Now : null, explicitCorrection ? "User" : "AI");
            }
            var summaryIndex = summary.FindLastIndex(change => change.Kind == "TASK" && change.TaskId == task.Id);
            if (summaryIndex >= 0) summary[summaryIndex] = summary[summaryIndex] with { After = AgentTaskSummary(task, state.Preference) };
            ValidTask(new(task.Name, task.Description, task.Deadline, task.EstimatedMinutes, task.Priority, task.TaskType, task.Splittable, task.MinimumSessionMinutes));
            Require(task.WorkWindowStart is null && task.WorkWindowEnd is null || task.WorkWindowStart is not null && task.WorkWindowEnd > task.WorkWindowStart &&
                task.WorkWindowEnd <= Now.AddDays(31) && task.Deadline > task.WorkWindowStart, "TASK_WORK_WINDOW", "Khung giờ thực hiện không hợp lệ hoặc nằm sau deadline.", 409);
            Require((task.Deadline > Now || item.Importance != null && state.Tasks.Any(original => original.Id == task.Id && SameTaskContract(original, task))) && task.Deadline.Year <= 2100, "DEADLINE_CONFLICT", "Deadline phải ở tương lai.", 409);
            Require(await db.TaskTypes.AnyAsync(x => x.Code == task.TaskType && x.IsActive, ct), "INVALID_TASK_TYPE", "Loại công việc không hợp lệ.");
            taskChanges.Add(task); affected.Add(task.Id);
        }
        Require(state.Tasks.Count(x => x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled) + taskChanges.Count(x => !state.Tasks.Any(t => t.Id == x.Id)) <= 500,
            "TASK_LIMIT", "Tối đa 500 công việc đang hoạt động.");
        var candidate = state with { Availability = weekly ?? state.Availability, Exceptions = state.Exceptions.Where(x => !exceptions.Any(e => e.Id == x.Id) && !removedExceptions.Any(e => e.Id == x.Id)).Concat(exceptions).ToArray(),
            Events = state.Events.Where(x => !events.Any(e => e.Id == x.Id)).Concat(events).ToArray(), Tasks = state.Tasks.Where(x => !taskChanges.Any(t => t.Id == x.Id)).Concat(taskChanges).ToArray() };
        if (weekly is null && exceptions.Count == 0 && removedExceptions.Length == 0 && events.Count == 0 && taskChanges.Count == 0 && affected.Count == 0 && !intent.Reschedule)
            return (new(null, [], [], []), candidate, [], [], TimetableCutoffNotices(skippedClasses, clippedClasses.Count));
        ValidateProtectedSessions(candidate);
        foreach (var session in candidate.Sessions.Where(x => x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused || x.Status == WorkSessionStatus.Scheduled && x.IsLocked))
        {
            var task = candidate.Tasks.Single(x => x.Id == session.TaskId);
            Require(!task.IsHardDeadline || session.End <= task.Deadline, "LOCKED_SESSION_CONFLICT", "Deadline mới xung đột với phiên đã khóa hoặc đang thực hiện.", 409);
        }
        if (intent.OptimizationQuery is null) foreach (var id in TimeConflictTaskIds(candidate)) affected.Add(id);
        Require(!candidate.Sessions.Any(x => x.Status == WorkSessionStatus.Scheduled && x.Start >= Now && x.Start < Now.AddMinutes(5) &&
            !engine.FreeSlots(candidate, x.Start, x.End).Any(s => s.Start <= x.Start && s.End >= x.End)),
            "LOCKED_SESSION_CONFLICT", "Thay đổi trùng với phiên sắp bắt đầu trong 5 phút tới. Hãy xử lý phiên đó trước.", 409);
        if (intent.Reschedule && affected.Count == 0)
            foreach (var id in intent.OptimizationQuery is { } scope ? OptimizationTaskIds(candidate, scope) :
                candidate.Tasks.Where(x => x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled).Select(x => x.Id)) affected.Add(id);
        Require(intent.OptimizationQuery is not null || !candidate.Sessions.Any(x => affected.Contains(x.TaskId) && x.Status == WorkSessionStatus.Scheduled && x.Start >= Now.AddMinutes(5).AddDays(intent.HorizonDays) &&
            !engine.FreeSlots(candidate, x.Start, x.End).Any(s => s.Start <= x.Start && s.End >= x.End)), "AGENT_HORIZON_CONFLICT", "Có phiên bị ảnh hưởng ngoài phạm vi đang lập lịch. Hãy mở rộng phạm vi hoặc chỉnh riêng phiên đó.", 409);
        if (ChatMatch(request, @"\b(?:quan trong|importance)\b").Success && !ChatMatch(request, @"\b(?:(?:xep|sap xep|len) lich|reschedule|schedule)\b").Success &&
            !intent.Reschedule && intent.OptimizationQuery == null && weekly == null && exceptions.Count == 0 && events.Count == 0 && removedExceptions.Length == 0 &&
            taskChanges.Count > 0 && taskChanges.All(task => state.Tasks.Any(original => original.Id == task.Id && SameTaskContract(original, task)))) affected.Clear();
        return (new(weekly, exceptions.ToArray(), events.ToArray(), taskChanges.ToArray(), removedExceptions.Select(x => x.Id).ToArray(), urgentEventIds, clippedClasses.ToArray(),
            timetableContext && events.Count > 0 || (weekly is not null || exceptions.Count > 0) &&
                candidate.Events.Any(x => x.End > Now && x.Start <= Now.AddDays(30) && x.EventType == "CLASS")),
            candidate, affected.ToArray(), summary.ToArray(), TimetableCutoffNotices(skippedClasses, clippedClasses.Count));
    }

    private static string LocalTime(DateTimeOffset time, WorkPreference preference) => TimeZoneInfo.ConvertTime(time, PlanningEngine.Zone(preference.TimeZoneId)).ToString("dd'/'MM'/'yyyy HH:mm");
    private static string AgentTaskSummary(WorkTask task, WorkPreference preference) =>
        $"{task.EstimatedMinutes} phút · hạn chót {LocalTime(task.Deadline, preference)}" +
        (task.Priority != 2 ? " · ưu tiên " + (task.Priority switch { 1 => "thấp", 3 => "cao", _ => "khẩn cấp" }) : "") +
        (task.EffectiveImportance == "Important" ? " · quan trọng: " + task.ImportanceReason : task.EffectiveImportance == "Lower" ? " · mức ảnh hưởng thấp hơn: " + task.ImportanceReason : "") +
        (!task.Splittable ? " · làm liên tục" : "") +
        (task.WorkWindowStart is { } from && task.WorkWindowEnd is { } to ? $" · thực hiện {LocalWindow(from, to, preference)}" : "");
    private static string LocalWindow(DateTimeOffset start, DateTimeOffset end, WorkPreference preference) => $"{LocalTime(start, preference)} → {LocalTime(end, preference)}";

    private async Task<PlanProposal> StoreAgentPlanAsync(Guid uid, PlanView plan, PlanningState state, string trigger, CancellationToken ct)
    {
        var proposal = new PlanProposal { UserId = uid, CreatedAt = Now, ExpiresAt = Now.AddMinutes(30), Revision = state.Preference.Revision,
            Kind = "RESCHEDULE", TriggerEvent = trigger, HorizonStart = plan.Start, HorizonEnd = plan.End };
        proposal.PreviousPlanId = await Mine<PlanProposal>(uid).Where(x => x.Status == "APPLIED").OrderByDescending(x => x.AppliedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        plan = plan with { Id = proposal.Id, Kind = proposal.Kind, PreviousPlanId = proposal.PreviousPlanId };
        proposal.PayloadJson = JsonSerializer.Serialize(plan); db.PlanProposals.Add(proposal); StorePlanDecisions(uid, proposal, plan, state);
        await db.SaveChangesAsync(ct); return proposal;
    }

    public Task<AgentTurnView> ApplyAgentTurnAsync(Guid uid, Guid id, CancellationToken ct) => Mutate(uid, async () =>
    {
        var turn = await Find<AgentTurn>(uid, id, ct);
        if (turn.Status == "APPLIED") return TurnView(turn);
        Require(turn.Status == "PREVIEW" && turn.ExpiresAt > Now, "AGENT_PREVIEW_EXPIRED", "Bản xem trước không thể áp dụng hoặc đã hết hạn. Hãy gửi yêu cầu mới.", 409);
        var state = await State(uid, ct);
        Require(state.Preference.Revision == turn.ExpectedRevision, "AGENT_STALE", "Dữ liệu đã thay đổi. Gửi lại yêu cầu để kiểm tra lịch mới.", 409);
        var changes = JsonSerializer.Deserialize<AgentChangeSet>(turn.ChangesJson, AgentJson)!; var view = TurnView(turn);
        var timetableAnalysis = changes.AnalyzeTimetable;
        if (timetableAnalysis) state = await AgentCalendarStateAsync(uid, state, ct);
        foreach (var task in changes.Tasks)
        {
            Require(await db.TaskTypes.AnyAsync(x => x.Code == task.TaskType && x.IsActive, ct), "INVALID_TASK_TYPE", "Loại công việc không còn hoạt động; hãy tạo lại bản xem trước.", 409);
            Require((task.Deadline > Now || view.Plan == null && state.Tasks.Any(original => original.Id == task.Id && SameTaskContract(original, task))) && (task.WorkWindowStart is null && task.WorkWindowEnd is null || task.WorkWindowStart is not null && task.WorkWindowEnd > task.WorkWindowStart && task.Deadline > task.WorkWindowStart),
                "TASK_WORK_WINDOW", "Deadline hoặc khung giờ thực hiện trong bản xem trước không còn hợp lệ.", 409);
        }
        var applicationTime = Now;
        var clippedTimetableIds = changes.ClippedTimetableEventIds ?? [];
        Require(changes.Events.All(x => x.Start >= applicationTime || clippedTimetableIds.Contains(x.Id) && x.EventType == "CLASS" && x.End > applicationTime),
            "AGENT_PREVIEW_EXPIRED", "Giờ sự kiện đã qua. Hãy gửi yêu cầu mới.", 409);
        foreach (var item in changes.Events.Where(x => clippedTimetableIds.Contains(x.Id) && x.Start < applicationTime))
        {
            var previousWindow = LocalWindow(item.Start, item.End, state.Preference);
            item.Start = applicationTime;
            var currentWindow = LocalWindow(item.Start, item.End, state.Preference);
            view = view with { Changes = view.Changes.Select(change => change.Kind == "EVENT" && change.Name == item.Name && change.After == previousWindow
                ? change with { After = currentWindow } : change).ToArray() };
        }
        var candidate = state with { Availability = changes.Weekly ?? state.Availability, Exceptions = state.Exceptions.Where(x => !changes.Exceptions.Any(e => e.Id == x.Id) && !(changes.RemovedExceptionIds ?? []).Contains(x.Id)).Concat(changes.Exceptions).ToArray(),
            Events = state.Events.Where(x => !changes.Events.Any(e => e.Id == x.Id)).Concat(changes.Events).ToArray(), Tasks = state.Tasks.Where(x => !changes.Tasks.Any(t => t.Id == x.Id)).Concat(changes.Tasks).ToArray() };
        ValidateProtectedSessions(candidate);
        var plan = view.Plan;
        Require(plan?.PriorityPolicy == null || plan.PriorityPolicy == (state.EisenhowerScheduling ? "eisenhower-v1" : "legacy"),
            "AGENT_PREVIEW_STALE", "Chính sách ưu tiên đã thay đổi. Hãy xem phương án mới.", 409);
        var eventCalendar = candidate with { Sessions = candidate.Sessions.Where(session => plan is null || session.Status != WorkSessionStatus.Scheduled ||
            session.Start < plan.Start || session.Start >= plan.End || plan.Sessions.Any(proposed => proposed.Preserved && proposed.Id == session.Id)).ToArray() };
        foreach (var item in changes.Events) ValidateEventConflicts(eventCalendar, [item], item.Id);
        if (plan is not null)
        {
            Require(plan.Start >= Now && plan.Sessions.Where(x => !x.Preserved).All(x => x.Start >= Now), "AGENT_PREVIEW_EXPIRED", "Giờ trong bản xem trước đã qua. Hãy gửi yêu cầu mới.", 409);
            var preserved = plan.Sessions.Where(x => x.Preserved).Select(x => x.Id).ToHashSet();
            var occupied = candidate.Sessions.Where(x => x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused ||
                x.Status == WorkSessionStatus.Scheduled && (x.Start < plan.Start || x.Start >= plan.End || preserved.Contains(x.Id)))
                .Select(x => new ProposedSession(x.Id, x.TaskId, x.Start, x.End, x.ReasonCode, true)).ToList();
            foreach (var session in plan.Sessions.Where(x => !x.Preserved).OrderBy(x => x.Start))
            { ValidateSession(candidate, session.TaskId, session.Start, session.End, occupied, Guid.Empty, false, plan.UseEstimateBias); occupied.Add(session); }
        }
        var originals = state.Tasks.Where(x => changes.Tasks.Any(t => t.Id == x.Id)).Select(Copy).ToArray();
        var originalExceptions = state.Exceptions.Where(x => changes.Exceptions.Any(e => e.Id == x.Id) || (changes.RemovedExceptionIds ?? []).Contains(x.Id)).Select(Copy).ToArray();
        var originalEvents = state.Events.Where(x => changes.Events.Any(e => e.Id == x.Id)).Select(Copy).ToArray();
        var oldSessions = plan is null ? [] : state.Sessions.Where(x => x.Status == WorkSessionStatus.Scheduled && x.Start >= plan.Start && x.Start < plan.End &&
            !plan.Sessions.Any(s => s.Preserved && s.Id == x.Id)).Select(Copy).ToArray();
        if (changes.Weekly is not null) { db.WeeklyAvailability.RemoveRange(state.Availability); db.WeeklyAvailability.AddRange(changes.Weekly); }
        db.AvailabilityExceptions.RemoveRange(state.Exceptions.Where(x => (changes.RemovedExceptionIds ?? []).Contains(x.Id)));
        foreach (var item in changes.Exceptions)
        {
            var tracked = state.Exceptions.SingleOrDefault(x => x.Id == item.Id);
            if (tracked is null) db.AvailabilityExceptions.Add(item); else db.Entry(tracked).CurrentValues.SetValues(item);
        }
        foreach (var item in changes.Events)
        {
            var tracked = state.Events.SingleOrDefault(x => x.Id == item.Id);
            if (tracked is null) db.FixedEvents.Add(item); else db.Entry(tracked).CurrentValues.SetValues(item);
        }
        foreach (var task in changes.Tasks)
        {
            var tracked = state.Tasks.SingleOrDefault(x => x.Id == task.Id);
            if (tracked is null) db.WorkTasks.Add(task); else db.Entry(tracked).CurrentValues.SetValues(task);
        }
        await db.SaveChangesAsync(ct);
        Guid? planId = null;
        if (plan is not null)
        {
            var proposal = await StoreAgentPlanAsync(uid, plan, candidate, "MANUAL", ct); planId = proposal.Id;
            plan = plan with { Id = proposal.Id, Kind = proposal.Kind, PreviousPlanId = proposal.PreviousPlanId };
            await ApplyPlanCoreAsync(uid, proposal, plan, candidate, ct);
            plan = plan with { IsApplied = true, Status = "APPLIED", AppliedAt = Now };
        }
        turn.UndoJson = JsonSerializer.Serialize(new AgentUndo(changes.Weekly is null ? null : state.Availability.Select(Copy).ToArray(),
            changes.Exceptions.Where(x => !originalExceptions.Any(e => e.Id == x.Id)).Select(x => x.Id).ToArray(), changes.Events.Where(x => !originalEvents.Any(e => e.Id == x.Id)).Select(x => x.Id).ToArray(), originalExceptions, originalEvents, originals,
            changes.Tasks.Where(x => !originals.Any(t => t.Id == x.Id)).Select(x => x.Id).ToArray(), oldSessions,
            plan?.Sessions.Where(x => !x.Preserved).Select(x => x.Id).ToArray() ?? [], planId), AgentJson);
        state.Preference.Revision = Guid.NewGuid(); turn.AppliedRevision = state.Preference.Revision; turn.UpdatedAt = Now;
        await db.SaveChangesAsync(ct); await UpdateLearningAsync(uid, ct); await PersistRiskAsync(uid, new(Now, state.Preference.PlanningHorizonDays), ct);
        if (view.Run is { } appliedRun)
        {
            var run = await Mine<AgentRun>(uid).SingleOrDefaultAsync(x => x.Id == appliedRun.Id, ct);
            if (run is not null) { run.Status = "APPLIED"; run.UpdatedAt = Now; run.Version = Guid.NewGuid(); }
            view = view with { Run = appliedRun with { Status = "APPLIED" } };
        }
        return SaveTurn(turn, view with { Status = "APPLIED", SuggestRoutine = timetableAnalysis,
            Reply = timetableAnalysis
                ? TimetableAnalysis(candidate, changes.Events, plan,
                    applied: true)
                : plan?.IsOptimization == true ? "Đã lưu phương án tối ưu bạn vừa xác nhận. Bạn có thể xem lịch mới bên dưới hoặc hoàn tác nếu muốn quay lại lịch cũ."
                : plan?.HasUrgentReplan == true
                ? "Đã lưu phương án bạn xác nhận:\n" + string.Join("\n", view.Changes.Where(change => change.Kind is "EVENT" or "SESSION").Select(change =>
                    $"- {change.Name}: {change.After}." + (change.Before.Length > 0 ? $" Giờ cũ: {change.Before}." : "")))
                : view.Reply == AgentActionReply(view.Changes, false) || string.IsNullOrWhiteSpace(view.Reply)
                ? AgentActionReply(view.Changes, true, plan) : view.Reply,
            Outcome = AppliedAgentOutcome(view with { Plan = plan }, changes, state), Plan = plan, CanUndo = true });
    }, ct, false);

    public Task<AgentTurnView> UndoAgentTurnAsync(Guid uid, Guid id, CancellationToken ct) => Mutate(uid, async () =>
    {
        var turn = await Find<AgentTurn>(uid, id, ct);
        if (turn.Status == "UNDONE") return TurnView(turn);
        var preference = await Preference(uid, ct);
        Require(turn.Status == "APPLIED" && turn.AppliedRevision == preference.Revision, "AGENT_UNDO_STALE", "Không thể hoàn tác vì dữ liệu đã thay đổi. Hãy dùng chat để điều chỉnh lịch hiện tại.", 409);
        var undo = JsonSerializer.Deserialize<AgentUndo>(turn.UndoJson, AgentJson)!;
        var fresh = await Mine<WorkSession>(uid).Where(x => undo.NewSessionIds.Contains(x.Id)).ToArrayAsync(ct);
        Require(fresh.All(x => x.Status == WorkSessionStatus.Scheduled && x.Start > Now) && undo.OriginalSessions.All(x => x.Start > Now),
            "AGENT_UNDO_STARTED", "Không thể hoàn tác các phiên đã đến giờ hoặc đã bắt đầu.", 409);
        var events = await Mine<FixedEvent>(uid).Where(x => undo.EventIds.Contains(x.Id)).ToArrayAsync(ct);
        var editedEvents = await Mine<FixedEvent>(uid).Where(x => undo.OriginalEvents.Select(e => e.Id).Contains(x.Id)).ToArrayAsync(ct);
        Require(events.Concat(editedEvents).All(x => x.Start > Now) && undo.OriginalEvents.All(x => x.Start > Now), "AGENT_UNDO_STARTED", "Sự kiện đã bắt đầu nên không thể hoàn tác.", 409);
        foreach (var session in fresh) session.Status = WorkSessionStatus.Cancelled;
        foreach (var original in undo.OriginalSessions) db.Entry(await Find<WorkSession>(uid, original.Id, ct)).CurrentValues.SetValues(original);
        foreach (var original in undo.OriginalTasks) db.Entry(await Find<WorkTask>(uid, original.Id, ct)).CurrentValues.SetValues(original);
        foreach (var task in await Mine<WorkTask>(uid).Where(x => undo.NewTaskIds.Contains(x.Id)).ToArrayAsync(ct)) { task.IsDeleted = true; task.DeletedAt = Now; }
        db.FixedEvents.RemoveRange(events);
        foreach (var original in undo.OriginalEvents) db.Entry(await Find<FixedEvent>(uid, original.Id, ct)).CurrentValues.SetValues(original);
        foreach (var original in undo.OriginalExceptions)
        {
            var existing = await Mine<AvailabilityException>(uid).SingleOrDefaultAsync(x => x.Id == original.Id, ct);
            if (existing is null) db.AvailabilityExceptions.Add(original); else db.Entry(existing).CurrentValues.SetValues(original);
        }
        db.AvailabilityExceptions.RemoveRange(await Mine<AvailabilityException>(uid).Where(x => undo.ExceptionIds.Contains(x.Id)).ToArrayAsync(ct));
        if (undo.Weekly is not null)
        {
            db.WeeklyAvailability.RemoveRange(await Mine<WeeklyAvailability>(uid).ToArrayAsync(ct));
            await db.SaveChangesAsync(ct); db.WeeklyAvailability.AddRange(undo.Weekly);
        }
        if (undo.PlanId is { } planId)
        {
            var proposal = await Find<PlanProposal>(uid, planId, ct); proposal.Status = "UNDONE"; proposal.IsApplied = false;
            if (proposal.PreviousPlanId is { } previous) (await Find<PlanProposal>(uid, previous, ct)).Status = "APPLIED";
        }
        preference.Revision = Guid.NewGuid(); turn.UpdatedAt = Now;
        await AddNotification(uid, "SCHEDULE_CHANGE", "Đã hoàn tác yêu cầu trợ lý", "Đã khôi phục các thay đổi của yêu cầu gần nhất.", "undo:" + turn.Id, null, ct);
        await db.SaveChangesAsync(ct); await UpdateLearningAsync(uid, ct); await PersistRiskAsync(uid, new(Now, preference.PlanningHorizonDays), ct);
        var undoneView = TurnView(turn);
        if (undoneView.Run is { } undoneRun)
        {
            var run = await Mine<AgentRun>(uid).SingleOrDefaultAsync(x => x.Id == undoneRun.Id, ct);
            if (run is not null) { run.Status = "UNDONE"; run.UpdatedAt = Now; run.Version = Guid.NewGuid(); }
            undoneView = undoneView with { Run = undoneRun with { Status = "UNDONE" } };
        }
        return SaveTurn(turn, undoneView with { Status = "UNDONE", Reply = "Đã hoàn tác yêu cầu và khôi phục lịch trước đó.", Outcome = "", CanUndo = false });
    }, ct, false);
}
