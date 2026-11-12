using System.Globalization;
using System.Text.RegularExpressions;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private PlanView RespectSchedulingExclusions(PlanView plan, PlanningState state, Guid[] affected, AgentTurnInput input)
    {
        var preserved = ExplicitlyPreservedTasks(input.Text, state);
        var excluded = state.Tasks.Where(task =>
            (plan.Sessions.Any(session => session.TaskId == task.Id && !session.Preserved) ||
                state.Sessions.Any(session => session.TaskId == task.Id && session.Status == WorkSessionStatus.Scheduled &&
                    session.Start >= plan.Start && session.Start < plan.End && !plan.Sessions.Any(x => x.Id == session.Id && x.Preserved))) &&
            (preserved.Contains(task.Id) || !affected.Contains(task.Id) &&
                ChatMatch(input.Text, @"\b(?:khong|dung|cho)\s+(?:xep|len)\s+(?:lich\s+)?(?:cho\s+)?" +
                    Regex.Escape(MatchText(task.Name)) + @"\b").Success)).ToArray();
        if (excluded.Length == 0) return plan;
        var bias = input.UseEstimateBias ? engine.Biases(state) : [new BiasView("All", 0, 1, false)];
        var warnings = excluded.Select(task => new PlanWarning(task.Id, task.Name,
            state.Dependencies.Any(edge => edge.PredecessorId == task.Id && affected.Contains(edge.TaskId))
                ? "DEPENDENCY_UNSCHEDULED" : "EXCLUDED_DEADLINE_TASK",
            "Công việc cần được dành thời gian trước hạn nhưng yêu cầu hiện tại không cho phép xếp việc này. Chưa lưu thay đổi; hãy điều chỉnh phạm vi hoặc giờ rảnh.",
            engine.Remaining(task, bias)));
        return plan with { Sessions = plan.Sessions.Where(session => session.Preserved).ToArray(),
            Warnings = plan.Warnings.Concat(warnings).ToArray() };
    }

    private static Match ChatMatch(string text, string pattern) => Regex.Match(MatchText(text), pattern,
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // Quotes must come from the current request or its unresolved parent. Only
    // recognized Telex equivalents are accepted; no fuzzy spelling/numeric match
    // can manufacture evidence from model prose or a completed request.
    private static bool SourceQuote(string? quote, string source) => !string.IsNullOrWhiteSpace(quote) &&
        quote.Length <= 500 && (source.Contains(quote, StringComparison.OrdinalIgnoreCase) ||
            VietnameseTelex.Normalize(source.Normalize(System.Text.NormalizationForm.FormC)).Contains(
                VietnameseTelex.Normalize(quote.Normalize(System.Text.NormalizationForm.FormC)), StringComparison.OrdinalIgnoreCase));

    private static int? QuotedMinutes(string? quote)
    {
        var text = MatchText(quote ?? "");
        if (text.Contains("nua tieng") || text.Contains("nua gio")) return 30;
        var hours = ChatMatch(text, @"\b(?<hours>\d{1,3}(?:[.,]\d{1,2})?)\s*(?:tieng|gio|hours?|hrs?|h)(?:\s*(?<minutes>\d{1,2})\s*(?:phut|minutes?|mins?|p|m))?\b");
        if (hours.Success && decimal.TryParse(hours.Groups["hours"].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var countHours))
        {
            var minutes = countHours * 60 + (hours.Groups["minutes"].Success ? int.Parse(hours.Groups["minutes"].Value, CultureInfo.InvariantCulture) : 0)
                + (text.Contains("ruoi") ? 30 : 0);
            return minutes == decimal.Truncate(minutes) && minutes <= 10080 ? (int)minutes : null;
        }
        var english = ChatMatch(text, @"\b(\d{1,4})\s*(?:minutes?|mins?|p)\b");
        if (english.Success) return int.Parse(english.Groups[1].Value, CultureInfo.InvariantCulture);
        var match = ChatMatch(text, @"\b(\d{1,4}|mot|hai|ba|bon|nam|sau|bay|tam|chin|muoi)\s*(phut|tieng|gio)\b");
        if (!match.Success) return null;
        var words = new[] { "", "mot", "hai", "ba", "bon", "nam", "sau", "bay", "tam", "chin", "muoi" };
        var count = int.TryParse(match.Groups[1].Value, out var value) ? value : Array.IndexOf(words, match.Groups[1].Value);
        return match.Groups[2].Value == "phut" ? count : count * 60 + (text.Contains("ruoi") ? 30 : 0);
    }

    private static string? GroundDurationQuote(AgentTaskCommand item, string source)
    {
        if (SourceQuote(item.DurationEvidence, source) && QuotedMinutes(item.DurationEvidence) == item.EstimatedMinutes)
            return item.DurationEvidence;
        // Recover only a literal duration directly attached to this exact task name.
        // Available hours elsewhere in the request are never task-duration evidence.
        var matches = Regex.Matches(VietnameseTelex.Normalize(source), Regex.Escape(VietnameseTelex.Normalize(item.Name)) +
            @"\s*(?:(?:cần|trong|tổng cộng|khoảng|dự kiến|mất)\s+){0,3}(?<duration>\d{1,4}\s*(?:phút|tiếng|giờ)(?:\s+rưỡi)?)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return matches.Cast<Match>().Select(x => x.Groups["duration"].Value)
            .FirstOrDefault(x => QuotedMinutes(x) == item.EstimatedMinutes);
    }

    private static bool GroundDeadlineQuote(string quote, string source)
    {
        if (!SourceQuote(quote, source)) return false;
        quote = VietnameseTelex.Normalize(quote); source = VietnameseTelex.Normalize(source);
        const string marker = @"\b(truoc|han|chot|deadline|dl|xong|hoan thanh|before|due)\b";
        if (ChatMatch(quote, marker).Success) return true;
        // A short verbatim time quote may omit the immediately preceding deadline label.
        // Keep the label and quote in the same clause; do not borrow one across punctuation.
        for (var index = source.IndexOf(quote, StringComparison.OrdinalIgnoreCase); index >= 0;
            index = source.IndexOf(quote, index + quote.Length, StringComparison.OrdinalIgnoreCase))
            if (ChatMatch(source[..index], marker + @"[^.!?;,\r\n]{0,80}$").Success) return true;
        return false;
    }

    private TimeSlot? RequestedWorkWindow(string text, WorkPreference preference, DateTimeOffset? reference = null)
    {
        var normalized = AgentInputRules.NormalizeTimeText(text);
        // “đổi hạn chót sang 22:00” uses sang as “to”, not “morning”.
        normalized = Regex.Replace(normalized, @"\b(?:han(?: chot)?|deadline|dl)\s+sang\b", "deadline",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        const string periodPattern = @"\b(toi|sang|chieu)\s+(?:thu\b|ngay\b|\d|nay\b|mai\b|hom\b|do\b)";
        // Multiple allowed days/periods cannot be represented by choosing one.
        if (RequestMatches(normalized, periodPattern).Count > 1) return null;
        var period = ChatMatch(normalized, periodPattern);
        const string rangePattern = @"\b(?:tu\s+)?(\d{1,2})\s*(?:h|gio|:)(\d{2})?\s*(?:den|[-–])\s*(\d{1,2})\s*(?:h|gio|:)(\d{2})?";
        if (RequestMatches(MatchText(text), rangePattern).Count > 1) return null;
        var exact = ChatMatch(text, rangePattern);
        if (!period.Success && !exact.Success) return null;
        var zone = PlanningEngine.Zone(preference.TimeZoneId);
        var date = LocalDate(reference ?? Now, preference);
        var specific = ChatMatch(text, @"\b(\d{1,2})/(\d{1,2})(?:/(\d{4}))?\b");
        var iso = ChatMatch(text, @"\b\d{4}-\d{2}-\d{2}\b");
        if (iso.Success)
        {
            if (!DateOnly.TryParseExact(iso.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return null;
        }
        else if (specific.Success)
        {
            var year = specific.Groups[3].Success ? int.Parse(specific.Groups[3].Value, CultureInfo.InvariantCulture) : date.Year;
            if (!DateOnly.TryParseExact($"{year:0000}-{int.Parse(specific.Groups[2].Value, CultureInfo.InvariantCulture):00}-{int.Parse(specific.Groups[1].Value, CultureInfo.InvariantCulture):00}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return null;
        }
        else if (MatchText(text).Contains("mai")) date = LocalDate(Now, preference).AddDays(1);
        else
        {
            var weekday = ChatMatch(text, @"\bthu\s+(hai|ba|tu|nam|sau|bay|2|3|4|5|6|7)\b|\bchu nhat\b");
            if (weekday.Success)
            {
                var names = new[] { "", "hai", "ba", "tu", "nam", "sau", "bay" };
                var requested = weekday.Value == "chu nhat" ? 0 : int.TryParse(weekday.Groups[1].Value, out var n) ? n - 1 : Array.IndexOf(names, weekday.Groups[1].Value);
                date = date.AddDays((requested - (int)date.DayOfWeek + 7) % 7);
            }
            else if (reference is null && !MatchText(text).Contains("hom nay") && !normalized.Contains("toi nay")) return null;
        }
        var hours = period.Groups[1].Value switch { "sang" => (360, 720), "chieu" => (720, 1080), _ => (1080, 1440) };
        if (exact.Success)
        {
            var from = int.Parse(exact.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + (exact.Groups[2].Success ? int.Parse(exact.Groups[2].Value, CultureInfo.InvariantCulture) : 0);
            var to = int.Parse(exact.Groups[3].Value, CultureInfo.InvariantCulture) * 60 + (exact.Groups[4].Success ? int.Parse(exact.Groups[4].Value, CultureInfo.InvariantCulture) : 0);
            if (from >= to || to > 1440) return null;
            hours = (from, to);
        }
        return new(PlanningEngine.At(date, hours.Item1, zone), PlanningEngine.At(date, hours.Item2, zone));
    }

    private static AgentIntent NormalizeAgentCategories(AgentIntent intent, PlanningState state, string[] activeTaskTypes, string request)
    {
        // A fallback cannot override an explicit no-substitution requirement,
        // including when the model has already replaced that required category.
        var required = RequestedAgentCategory(request);
        if (required.Success && CategorySubstitutionForbidden(request))
        {
            var code = required.Groups["code"].Value.ToUpperInvariant();
            if (intent.Tasks.Length > 0 && !activeTaskTypes.Contains(code, StringComparer.Ordinal) ||
                intent.Events.Length > 0 && !AgentCategoryPolicy.EventTypes.Contains(code, StringComparer.Ordinal))
                return ClarifyAgentIntent(intent with { Options = [] }, $"Loại {code} bạn yêu cầu hiện chưa khả dụng. Bạn muốn chọn một loại đang hoạt động hay cho phép dùng loại Khác? Mình chưa lưu thay đổi.");
        }
        var requestedTaskCategory = intent.Tasks.Length == 1 && required.Success
            ? AgentCategoryPolicy.Normalize(required.Groups["code"].Value, activeTaskTypes, false) : null;
        return intent with
        {
            Events = intent.Events.Select(item => {
                var category = AgentCategoryPolicy.Normalize(item.EventType, AgentCategoryPolicy.EventTypes, item.ExistingId is not null);
                return item with { EventType = category.Length > 0 ? category : state.Events.SingleOrDefault(x => x.Id == item.ExistingId)?.EventType ?? "" };
            }).ToArray(),
            Tasks = intent.Tasks.Select(item => {
                var category = requestedTaskCategory ?? AgentCategoryPolicy.Normalize(item.TaskType, activeTaskTypes, item.TaskId is not null);
                return item with { TaskType = category.Length > 0 ? category : state.Tasks.SingleOrDefault(x => x.Id == item.TaskId)?.TaskType ?? "" };
            }).ToArray()
        };
    }

    private AgentIntent NormalizeAgentIntent(AgentIntent intent, string request, PlanningState state, AgentPendingRequest? pending)
    {
        var source = pending is null ? request : pending.OriginalText + "\n" + string.Join("\n", pending.Answers ?? []) + "\n" + request;
        var first = intent.Tasks.FirstOrDefault();
        var windowReference = pending?.WorkWindowStart ?? state.Tasks.FirstOrDefault(x => x.Id == first?.TaskId)?.WorkWindowStart;
        // Use task-specific source text in mixed requests. Availability, event
        // hours and another task's dates must not override an execution window.
        var taskWindowSource = first?.WorkWindowEvidence is { Length: > 0 } evidence && SourceQuote(evidence, source) ? evidence : null;
        var canInfer = intent.Tasks.Length <= 1 && intent.WeeklySlots.Length == 0 && intent.Exceptions.Length == 0 && intent.Events.Length == 0;
        var window = taskWindowSource is not null ? RequestedWorkWindow(taskWindowSource, state.Preference, windowReference) :
            canInfer ? RequestedWorkWindow(request, state.Preference, pending?.WorkWindowStart) ??
                (pending is null ? null : RequestedWorkWindow(pending.OriginalText, state.Preference, pending.WorkWindowStart)) : null;
        AgentIntent Ask(string question, string[]? options = null)
        {
            var proposed = ProposedDuration(intent.Reply + "\n" + question);
            return intent with {
            Reply = ClarificationReply(intent.Reply), Question = question, Options = options ?? [],
            ResultKind = "CLARIFICATION",
            ReplaceWeeklyAvailability = false, WeeklySlots = [], Exceptions = [], Events = [], Tasks = [], ScheduleTaskIds = [], Reschedule = false,
            FreeTimeQuery = null, ScheduleReviewQuery = null,
            OptimizationQuery = null,
            Pending = new(pending?.OriginalText ?? request, first?.TaskId ?? pending?.TaskId,
                window?.Start ?? pending?.WorkWindowStart, window?.End ?? pending?.WorkWindowEnd,
                first?.DurationEvidence is { } duration && SourceQuote(duration, source) ? QuotedMinutes(duration) : QuotedMinutes(request) ?? pending?.EstimatedMinutes,
                first?.DeadlineEvidence is { } deadline && SourceQuote(deadline, source) ? first.Deadline : pending?.Deadline,
                pending is null ? [] : (pending.Answers ?? []).Append(request).TakeLast(4).ToArray(),
                proposed ?? pending?.ProposedMinutes, pending?.DurationProposalAccepted == true &&
                    (proposed is null || proposed == pending.ProposedMinutes))
            };
        }
        // Recover one mixed response as clarification. The mutation guard below
        // remains mandatory, including when applying a persisted preview.
        if (!string.IsNullOrWhiteSpace(intent.Question)) return Ask(intent.Question, intent.Options);
        if (ConsequentialRequestQuestion(intent, request, state) is { } safetyQuestion) return Ask(safetyQuestion);
        if (AgentInputRules.AmbiguousClock(request) is { } unresolved) return Ask(unresolved.Question, unresolved.Options);
        var protectedIds = ExplicitlyPreservedTasks(request, state);
        if (protectedIds.Count > 0)
            intent = intent with { Tasks = intent.Tasks.Where(x => x.TaskId is null || !protectedIds.Contains(x.TaskId.Value)).ToArray(),
                ScheduleTaskIds = (intent.Reschedule && intent.ScheduleTaskIds.Length == 0 ? state.Tasks.Where(x =>
                    x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled &&
                    MatchText(request).Contains(MatchText(x.Name), StringComparison.Ordinal)).Select(x => x.Id) : intent.ScheduleTaskIds)
                    .Where(id => !protectedIds.Contains(id)).Distinct().ToArray(), Reschedule = false };
        var tasks = new List<AgentTaskCommand>();
        foreach (var item in intent.Tasks)
        {
            var original = item.TaskId is { } id ? state.Tasks.SingleOrDefault(x => x.Id == id) : null;
            // Model schemas often repeat stored fields. They are not edits and
            // do not require the user to quote facts already saved in their account.
            var task = item with { EstimatedMinutes = item.EstimatedMinutes == original?.EstimatedMinutes ? null : item.EstimatedMinutes,
                Deadline = item.Deadline == original?.Deadline ? null : item.Deadline };
            if (intent.Tasks.Length == 1 && item.DurationEvidence is not null)
            {
                var continuity = ChatMatch(source, @"\b(?:lien tuc|lien mach|lam lien|mot mach|mot leo|khong (?:duoc |the )?(?:chia|cat|ngat quang)|uninterrupted|continuous|in one sitting|without (?:a )?break)\b").Success;
                var withoutNegatedSplitting = Regex.Replace(MatchText(request),
                    @"\bkhong(?: (?:duoc|the))? (?:chia(?: nho)?|cat|ngat quang)\b", " ",
                    RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                var splitting = ChatMatch(withoutNegatedSplitting, @"\b(?:chia (?:duoc|phien|nho)|co the chia|splittable)\b").Success;
                if (continuity && !splitting) task = task with { Splittable = false };
                else if (splitting) task = task with { Splittable = true };
                else if (original is null && !continuity) task = task with { Splittable = true };
                else if (original is not null && !continuity && !splitting) task = task with { Splittable = original.Splittable };
            }
            if (ActivityContinuity.RequiresContinuousSession(task.Name, task.TaskType) || original is { RequiresContinuousSession: true })
                task = task with { Splittable = false };
            if (original?.WorkWindowStart is not null && window is null &&
                !ChatMatch(request, @"\b(?:doi|chuyen|mo rong|thu hep)\b[^.!?;\r\n]{0,60}\b(?:khung|gio|ngay)\b").Success)
                task = task with { WorkWindowStart = original.WorkWindowStart, WorkWindowEnd = original.WorkWindowEnd };
            if (item.TaskId is null && (item.EstimatedMinutes is null || item.Deadline is null && window is null))
                return Ask(item.EstimatedMinutes is null ? $"Bạn bổ sung thời lượng (phút) cho “{item.Name}” nhé?" : $"Bạn bổ sung hạn hoàn thành (ngày, giờ) cho “{item.Name}” nhé?");
            // Non-null evidence is required by the live provider schema. Null is
            // reserved for older persisted commands/adapters, not a model escape.
            if (item.DurationEvidence is not null && task.EstimatedMinutes is not null)
            {
                var duration = intent.Tasks.Length == 1 && item.TaskId is null && pending is { DurationProposalAccepted: true, ProposedMinutes: { } minutes } &&
                    task.EstimatedMinutes == minutes && SourceQuote(item.Name, pending.OriginalText)
                    ? $"{minutes} phút" : GroundDurationQuote(item, source);
                if (duration is null) return Ask("Bạn muốn dành bao nhiêu phút cho công việc này? Mình chưa lưu thời lượng chưa được xác nhận.");
                task = task with { DurationEvidence = duration };
            }
            if (item.DeadlineEvidence is not null && task.Deadline is not null &&
                !GroundDeadlineQuote(item.DeadlineEvidence, source))
                return Ask("Bạn cần hoàn thành công việc trước ngày, giờ nào? Ngày thực hiện và deadline được giữ riêng.");
            if (window is not null)
            {
                var inheritedDeadline = task.Deadline ?? original?.Deadline ?? pending?.Deadline;
                task = task with { WorkWindowStart = window.Start, WorkWindowEnd = window.End, Deadline = inheritedDeadline ?? window.End,
                    DeadlineFromWorkWindow = inheritedDeadline is null || task.Deadline is null && original is { IsHardDeadline: false } };
            }
            else if (pending?.WorkWindowStart is { } from && pending.WorkWindowEnd is { } to)
                task = task with { WorkWindowStart = task.WorkWindowStart ?? from, WorkWindowEnd = task.WorkWindowEnd ?? to };
            if (task.WorkWindowStart is not null && item.WorkWindowEvidence is not null && window is null &&
                !SourceQuote(item.WorkWindowEvidence, source) && original?.WorkWindowStart != task.WorkWindowStart)
                return Ask("Bạn muốn thực hiện công việc trong ngày và khung giờ nào?");
            if (intent.Tasks.Length == 1)
            {
                var completion = ChatMatch(request, @"\b(?:xong|hoan thanh|deadline|han).*?\btruoc\s+(\d{1,2})\s*(?:h|gio|:)(\d{2})?");
                if (completion.Success && int.TryParse(completion.Groups[1].Value, out var h) && h < 24)
                {
                    if (h < 12 && ChatMatch(AgentInputRules.NormalizeTimeText(request), @"\b(chieu|toi|dem|pm)\b").Success) h += 12;
                    else if (h == 12 && ChatMatch(request, @"\b(sang|am)\b").Success) h = 0;
                    var minute = completion.Groups[2].Success ? int.Parse(completion.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
                    if (minute < 60)
                    {
                        var date = LocalDate(task.WorkWindowStart ?? original?.WorkWindowStart ?? task.Deadline ?? original?.Deadline ?? Now, state.Preference);
                        task = task with { Deadline = PlanningEngine.At(date, h * 60 + minute, PlanningEngine.Zone(state.Preference.TimeZoneId)), DeadlineFromWorkWindow = false };
                    }
                }
            }
            tasks.Add(task);
        }
        // Repeated existing fields still mean "schedule this task", even when
        // BuildAgentChanges correctly drops the resulting no-op update.
        return intent with { Tasks = tasks.ToArray(), ScheduleTaskIds = intent.ScheduleTaskIds.Concat(
            tasks.Where(x => x.TaskId is not null && !state.Sessions.Any(s => s.TaskId == x.TaskId &&
                s.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused && s.End > Now))
                .Select(x => x.TaskId!.Value)).Distinct().ToArray(), Pending = null };
    }

    private static AgentIntent? SavedTaskReviewIntent(string request, PlanningState state)
    {
        // A clearly read-only question about named saved work needs no model
        // interpretation. Keep mixed actions and unfamiliar tasks on the AI path.
        if (!request.Contains('?') || !ChatMatch(request, @"\b(?:chi kiem tra|chi xem|(?:dung|khong) (?:sua|thay doi) lich)\b").Success ||
            !ChatMatch(request, @"\b(?:kip|deadline|han chot)\b").Success ||
            ChatMatch(request, @"\b(?:tao|them|cap nhat)\b|^\s*(?:doi|chuyen|sua|xep|len lich|sap xep)\b").Success ||
            !state.Tasks.Any(task => !task.IsDeleted && task.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled &&
                MatchText(task.Name).Length > 0 && MatchText(request).Contains(MatchText(task.Name), StringComparison.Ordinal))) return null;
        return new("", "", false, [], [], [], [], [], false, 14, ResultKind: "ANSWER");
    }

    private string ScopedReadOnlyReply(AgentIntent intent, string request, PlanningState state, AgentMessage[] history, string[] notices)
    {
        if (intent.FreeTimeQuery is { } query) return VerifiedFreeTimeReply(query, state);
        if (notices.Length > 0 && RequestMatches(MatchText(request), @"\b(?:toan bo|tat ca|all)\b").Count > 0)
            return string.Join(" ", notices) + " Chưa đủ dữ liệu để kết luận cho toàn bộ công việc. Hãy nêu tên công việc hoặc thu hẹp phạm vi cần kiểm tra.";
        return GroundedReadOnlyReply(intent, request, state, history);
    }

    private string GroundedReadOnlyReply(AgentIntent intent, string request, PlanningState state, AgentMessage[] history)
    {
        if (!ChatMatch(request, @"\b(deadline|kip|han chot)\b").Success)
            return ChatMatch(intent.Reply, @"^(?:(?:toi|minh)\s+)?da\s+(luu|tao|cap nhat|xep|them|ghi nhan)\b").Success
                ? "Chưa có thay đổi nào được lưu trong yêu cầu này. Bạn muốn tạo hoặc xếp lịch công việc cụ thể nào?" : intent.Reply;
        var failed = history.Where(x => x.Status == "FAILED").ToArray();
        var active = state.Tasks.Where(x => !x.IsDeleted && x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled).ToArray();
        var named = active.Where(x => MatchText(request).Contains(MatchText(x.Name), StringComparison.Ordinal) ||
            MatchText(x.Name).Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(word => word.Length >= 4 && MatchText(request).Contains(word, StringComparison.Ordinal))).ToArray();
        if (failed.Length > 0 && named.Length == 0)
            return "Mình chưa tìm thấy công việc được nhắc đến trong dữ liệu đã lưu. Có yêu cầu trước chưa hoàn tất, nên chưa thể xác nhận kịp deadline. Bạn muốn thử tạo và xếp lịch lại không?";
        if (named.Length == 0) return "Mình chưa xác định được công việc bạn muốn kiểm tra trong dữ liệu đã lưu. Bạn cho mình tên công việc cụ thể nhé; hiện chưa thể xác nhận kịp deadline.";
        var bias = new[] { new BiasView("All", 0, 1, false) };
        var sessions = state.Sessions.Where(x => (x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused) && x.End > Now).ToArray();
        var listSessions = ChatMatch(request, @"\b(?:neu|liet ke|hien thi|xem)\b[^.!?;\r\n]{0,80}\bphien\b").Success;
        return string.Join("\n", named.Take(12).Select(task =>
        {
            if (task.Deadline is not { } deadline || !task.HasPlanningDetails)
                return $"{task.Name}: cần bổ sung hạn chót và thời gian dự kiến để kiểm tra khả năng hoàn thành.";
            var work = sessions.Where(x => x.TaskId == task.Id).ToArray();
            var valid = work.Where(x => x.End <= task.Deadline && PlanningEngine.InsideWorkWindow(task, x.Start, x.End) &&
                engine.FreeSlots(state, x.Start, x.End).Any(s => s.Start <= x.Start && s.End >= x.End) &&
                !sessions.Any(s => s.Id != x.Id && s.Start < x.End && x.Start < s.End)).ToArray();
            var completion = engine.CompletionTimes(state, sessions.Select(x => new ProposedSession(x.Id, x.TaskId, x.Start, x.End, x.ReasonCode, true)), bias, Now);
            var dependenciesValid = state.Dependencies.Where(x => x.TaskId == task.Id).All(x => completion.TryGetValue(x.PredecessorId, out var finished) && valid.All(s => s.Start >= finished));
            var minutes = (int)valid.Sum(x => (x.End - x.Start).TotalMinutes);
            var remaining = engine.Remaining(task, bias);
            var answer = $"{task.Name}: " + (minutes >= remaining && dependenciesValid && (task.Splittable || valid.Any(x => (x.End - x.Start).TotalMinutes >= remaining))
                ? $"đã có {minutes} phút trong lịch trước hạn {LocalTime(deadline, state.Preference)}."
                : $"chưa đủ phiên hợp lệ trước hạn {LocalTime(deadline, state.Preference)} (có {minutes}/{remaining} phút). Cần xem phương án xếp lại.");
            return answer + (listSessions && work.Length > 0 ? "\nPhiên đang lưu: " + string.Join("; ", work.OrderBy(x => x.Start).Take(12)
                .Select(x => LocalWindow(x.Start, x.End, state.Preference) + (valid.Any(s => s.Id == x.Id) ? "" : " (cần kiểm tra)"))) +
                (work.Length > 12 ? $"; còn {work.Length - 12} phiên khác trong lịch." : "") : "");
        })) + (failed.Length > 0 ? "\nCó yêu cầu trước thất bại; các công việc chưa được tạo không nằm trong kết quả trên." : "");
    }

    private static string[] AgentCategoryNotices(AgentIntent interpreted, AgentIntent normalized, string request, string[] activeTaskTypes)
    {
        if (!string.IsNullOrWhiteSpace(normalized.Question)) return [];
        var substituted = interpreted.Tasks.Zip(normalized.Tasks).Where(x => x.Second.TaskType == "OTHER" &&
                !activeTaskTypes.Contains(x.First.TaskType?.Trim().ToUpperInvariant(), StringComparer.Ordinal))
            .Select(x => x.First.TaskType).Concat(interpreted.Events.Zip(normalized.Events)
                .Where(x => x.Second.EventType == "OTHER" && !AgentCategoryPolicy.EventTypes.Contains(x.First.EventType?.Trim().ToUpperInvariant(), StringComparer.Ordinal))
                .Select(x => x.First.EventType)).ToList();
        var requested = RequestedAgentCategory(request);
        if (requested.Success && (normalized.Tasks.Length == 1 && normalized.Tasks[0].TaskType == "OTHER" ||
            normalized.Events.Length == 1 && normalized.Events[0].EventType == "OTHER"))
            substituted.Add(requested.Groups["code"].Value);
        return substituted.Where(x => !string.IsNullOrWhiteSpace(x) && !x.Trim().Equals("OTHER", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).Take(6)
            .Select(code => $"Loại “{code}” chưa khả dụng, nên mình dùng “Khác”.").ToArray();
    }

    private static string AgentCategoryLabel(string code) => code switch {
        "WORK" => "Công việc", "OTHER" => "Khác", "CLASS" => "Lớp học", "MEETING" => "Cuộc họp",
        "APPOINTMENT" => "Cuộc hẹn", "STUDY" => "Học tập", "ADMIN" => "Việc cá nhân",
        "MEETING_PREP" => "Chuẩn bị cuộc họp", "PROGRAMMING" => "Lập trình", "WRITING" => "Viết nội dung", _ => code };

    private static string AgentActionReply(AgentChangeView[] changes, bool applied, PlanView? plan = null)
    {
        if (changes.Length != 1) return applied ? "Mình đã cập nhật lịch theo yêu cầu của bạn." : "Mình đã chuẩn bị phương án cập nhật lịch cho bạn.";
        var change = changes[0];
        if (!applied) return $"Mình đã chuẩn bị phương án cho “{change.Name}”.";
        var edited = !string.IsNullOrEmpty(change.Before);
        if (change.Kind == "EVENT") return edited ? $"Mình đã cập nhật “{change.Name}” trong lịch." : $"Mình đã thêm “{change.Name}” vào lịch.";
        if (change.Kind == "TASK") return $"Mình đã {(edited ? "cập nhật" : "tạo")} công việc “{change.Name}”" +
            (plan?.Sessions.Any(x => !x.Preserved && x.TaskId == change.TaskId) == true ? " và xếp lịch cho bạn." : ".");
        return $"Mình đã cập nhật lịch cho “{change.Name}”.";
    }

    private static string AppliedAgentOutcome(AgentTurnView view, AgentChangeSet applied, PlanningState state)
    {
        var tasks = applied.Tasks.Concat(state.Tasks).DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        var changes = view.Changes.Take(12).Select(x => x.Kind == "TASK" && x.TaskId is { } id && tasks.TryGetValue(id, out var task)
            ? $"{x.Name} · {AgentCategoryLabel(task.TaskType)}: {x.After}"
            : x.Kind == "EVENT" && applied.Events.FirstOrDefault(e => e.Name == x.Name) is { } item
                ? $"{x.Name} · {AgentCategoryLabel(item.EventType)}: {x.After}" : $"{x.Name}: {x.After}");
        var sessions = view.Plan?.Sessions.Where(x => !x.Preserved).Take(12)
            .Select(x => $"{(tasks.TryGetValue(x.TaskId, out var task) ? task.Name : "Phiên làm việc")}: {LocalWindow(x.Start, x.End, state.Preference)}").ToArray() ?? [];
        return "Lịch đã lưu:\n" + string.Join("\n", changes) +
            (sessions.Length > 0 ? "\nThời gian thực hiện:\n" + string.Join("\n", sessions) :
                state.Sessions.Any(x => x.Status == WorkSessionStatus.Scheduled) ? "\nCác phiên làm việc khác được giữ nguyên." : "");
    }
}
