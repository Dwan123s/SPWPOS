using System.Globalization;
using System.Text.RegularExpressions;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static Match ChatMatch(string text, string pattern) => Regex.Match(MatchText(text), pattern,
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // Quotes must come from the current request or its unresolved parent, never
    // from model prose or another completed request's duration/deadline.
    private static bool SourceQuote(string? quote, string source) => !string.IsNullOrWhiteSpace(quote) &&
        quote.Length <= 500 && source.Contains(quote, StringComparison.OrdinalIgnoreCase);

    private static int? QuotedMinutes(string? quote)
    {
        var text = MatchText(quote ?? "");
        if (text.Contains("nua tieng") || text.Contains("nua gio")) return 30;
        var match = ChatMatch(text, @"\b(\d{1,4}|mot|hai|ba|bon|nam|sau|bay|tam|chin|muoi)\s*(phut|tieng|gio)\b");
        if (!match.Success) return null;
        var words = new[] { "", "mot", "hai", "ba", "bon", "nam", "sau", "bay", "tam", "chin", "muoi" };
        var count = int.TryParse(match.Groups[1].Value, out var value) ? value : Array.IndexOf(words, match.Groups[1].Value);
        return match.Groups[2].Value == "phut" ? count : count * 60 + (text.Contains("ruoi") ? 30 : 0);
    }

    private TimeSlot? RequestedWorkWindow(string text, WorkPreference preference, DateTimeOffset? reference = null)
    {
        var period = ChatMatch(text, @"\b(toi|sang|chieu)\s+(?:thu\b|ngay\b|\d|mai\b|hom\b|do\b)");
        var exact = ChatMatch(text, @"\btu\s+(\d{1,2})\s*(?:h|gio|:)(\d{2})?\s*(?:den|[-–])\s*(\d{1,2})\s*(?:h|gio|:)(\d{2})?");
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
            else if (reference is null && !MatchText(text).Contains("hom nay")) return null;
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

    private AgentIntent NormalizeAgentIntent(AgentIntent intent, string request, PlanningState state, AgentPendingRequest? pending)
    {
        var source = pending is null ? request : pending.OriginalText + "\n" + string.Join("\n", pending.Answers ?? []) + "\n" + request;
        var window = RequestedWorkWindow(request, state.Preference, pending?.WorkWindowStart) ??
            (pending is null ? null : RequestedWorkWindow(pending.OriginalText, state.Preference, pending.WorkWindowStart));
        var first = intent.Tasks.FirstOrDefault();
        AgentIntent Ask(string question, string[]? options = null) => intent with
        {
            Reply = "Mình cần xác nhận thêm trước khi thay đổi lịch.", Question = question, Options = options ?? [],
            ResultKind = "CLARIFICATION",
            ReplaceWeeklyAvailability = false, WeeklySlots = [], Exceptions = [], Events = [], Tasks = [], ScheduleTaskIds = [], Reschedule = false,
            Pending = new(pending?.OriginalText ?? request, first?.TaskId ?? pending?.TaskId,
                window?.Start ?? pending?.WorkWindowStart, window?.End ?? pending?.WorkWindowEnd,
                first?.DurationEvidence is { } duration && SourceQuote(duration, source) ? QuotedMinutes(duration) : QuotedMinutes(request) ?? pending?.EstimatedMinutes,
                first?.DeadlineEvidence is { } deadline && SourceQuote(deadline, source) ? first.Deadline : pending?.Deadline,
                pending is null ? [] : (pending.Answers ?? []).Append(request).TakeLast(4).ToArray())
        };
        // Recover one mixed response as clarification. The mutation guard below
        // remains mandatory, including when applying a persisted preview.
        if (!string.IsNullOrWhiteSpace(intent.Question)) return Ask(intent.Question, intent.Options);
        if (AgentInputRules.AmbiguousClock(request) is { } unresolved) return Ask(unresolved.Question, unresolved.Options);
        var tasks = new List<AgentTaskCommand>();
        foreach (var item in intent.Tasks)
        {
            var original = item.TaskId is { } id ? state.Tasks.SingleOrDefault(x => x.Id == id) : null;
            var task = item;
            if (item.TaskId is null && (item.EstimatedMinutes is null || item.Deadline is null && window is null))
                return Ask(item.EstimatedMinutes is null ? "Bạn muốn dành bao nhiêu phút cho công việc này?" : "Bạn cần hoàn thành công việc trước ngày, giờ nào?");
            // Non-null evidence is required by the live provider schema. Null is
            // reserved for older persisted commands/adapters, not a model escape.
            if (item.DurationEvidence is not null && item.EstimatedMinutes is not null &&
                (!SourceQuote(item.DurationEvidence, source) || QuotedMinutes(item.DurationEvidence) != item.EstimatedMinutes))
                return Ask("Bạn muốn dành bao nhiêu phút cho công việc này? Mình chưa lưu thời lượng chưa được xác nhận.");
            if (item.DeadlineEvidence is not null && item.Deadline is not null &&
                (!SourceQuote(item.DeadlineEvidence, source) || !ChatMatch(item.DeadlineEvidence, @"\b(truoc|han|deadline|xong|hoan thanh|before|due)\b").Success))
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
                    if (h < 12 && ChatMatch(request, @"\b(chieu|toi|dem|pm)\b").Success) h += 12;
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
        return intent with { Tasks = tasks.ToArray(), Pending = null };
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
        return string.Join("\n", named.Take(12).Select(task =>
        {
            var work = sessions.Where(x => x.TaskId == task.Id).ToArray();
            var valid = work.Where(x => x.End <= task.Deadline && PlanningEngine.InsideWorkWindow(task, x.Start, x.End) &&
                engine.FreeSlots(state, x.Start, x.End).Any(s => s.Start <= x.Start && s.End >= x.End) &&
                !sessions.Any(s => s.Id != x.Id && s.Start < x.End && x.Start < s.End)).ToArray();
            var completion = engine.CompletionTimes(state, sessions.Select(x => new ProposedSession(x.Id, x.TaskId, x.Start, x.End, x.ReasonCode, true)), bias, Now);
            var dependenciesValid = state.Dependencies.Where(x => x.TaskId == task.Id).All(x => completion.TryGetValue(x.PredecessorId, out var finished) && valid.All(s => s.Start >= finished));
            var minutes = (int)valid.Sum(x => (x.End - x.Start).TotalMinutes);
            var remaining = engine.Remaining(task, bias);
            return $"{task.Name}: " + (minutes >= remaining && dependenciesValid && (task.Splittable || valid.Any(x => (x.End - x.Start).TotalMinutes >= remaining))
                ? $"đã có {minutes} phút trong lịch trước hạn {LocalTime(task.Deadline, state.Preference)}."
                : $"chưa đủ phiên hợp lệ trước hạn {LocalTime(task.Deadline, state.Preference)} (có {minutes}/{remaining} phút). Cần xem phương án xếp lại.");
        })) + (failed.Length > 0 ? "\nCó yêu cầu trước thất bại; các công việc chưa được tạo không nằm trong kết quả trên." : "");
    }

    private static string AppliedAgentReply(AgentTurnView view, WorkPreference preference)
    {
        var changes = view.Changes.Take(12).Select(x => $"{x.Name}: {x.After}");
        var sessions = view.Plan?.Sessions.Where(x => !x.Preserved).Take(12).Select(x => LocalWindow(x.Start, x.End, preference)) ?? [];
        return "Đã lưu các thay đổi:\n" + string.Join("\n", changes) + (sessions.Any() ? "\nPhiên làm việc:\n" + string.Join("\n", sessions) : "\nKhông có phiên làm việc cần chuyển.");
    }
}
