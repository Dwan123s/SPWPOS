using System.Globalization;
using System.Text.Json;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private sealed record ReviewEntry(Guid Id, Guid? TaskId, string Name, DateTimeOffset Start, DateTimeOffset End, string Kind, string Status);
    private sealed record ReviewTask(Guid Id, string Name, DateTimeOffset Deadline, int RemainingMinutes, int SavedMinutes, string Concern);
    private sealed record ReviewSuggestion(Guid TaskId, string Name, DateTimeOffset Start, DateTimeOffset End);
    private sealed record ReviewReport(object Facts, string Details, string Fallback);

    private static bool ScheduleOverviewRequested(string request)
    {
        var text = MatchText(request);
        return RequestMatches(text, @"\b(?:lich trinh|xem lich|lich (?:hom nay|ngay mai)|(?:hom nay|ngay mai) (?:toi |minh )?co (?:nhung )?gi)\b").Count > 0 &&
            RequestMatches(text, @"\b(?:phan tich|danh gia|nhan xet|tu van|hop ly|review|analy[sz]e|gio ranh|gio trong|ranh (?:gio|luc|khi)|khoang (?:ranh|trong))\b").Count == 0;
    }

    private static AgentIntent RecognizeScheduleReview(AgentIntent intent, string request)
    {
        // Older providers sometimes classify analysis as a free-time lookup.
        // Only promote an already read-only intent; never discard requested actions.
        if (intent.ScheduleReviewQuery is null && intent.FreeTimeQuery is not null && !HasAgentActions(intent) &&
            (ScheduleOverviewRequested(request) || RequestMatches(MatchText(request), @"\b(?:phan tich|danh gia|nhan xet|tu van|review|analy[sz]e)\b").Count > 0))
            return intent with { ScheduleReviewQuery = intent.FreeTimeQuery, FreeTimeQuery = null };
        return intent;
    }

    private async Task<string> ExplainScheduleReviewAsync(Guid uid, AgentIntent intent, string request, PlanningState state,
        string[] activeTaskTypes, CancellationToken ct)
    {
        var query = intent.ScheduleReviewQuery!;
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        var today = PlanningEngine.At(LocalDate(Now, state.Preference), 0, zone);
        if (query.Start < today.AddDays(-30) || query.End > Now.AddDays(30))
            return "Mình có thể phân tích lịch trong 30 ngày trước và 30 ngày tới. Bạn chọn một khoảng tối đa 31 ngày trong phạm vi này nhé. Lịch chưa thay đổi.";
        // Include the whole local days for daily budgets, plus all future
        // reservations used to check deadlines and dependency completion.
        var from = PlanningEngine.At(LocalDate(query.Start < Now ? query.Start : Now, state.Preference), 0, zone);
        var until = query.End > Now.AddDays(30) ? query.End : Now.AddDays(30);
        until = PlanningEngine.At(LocalDate(until, state.Preference).AddDays(1), 0, zone);
        var calendar = await CalendarStateAsync(uid, state, from, until, ct);
        if (ScheduleOverviewRequested(request)) return VerifiedScheduleOverviewReply(query, calendar);
        var report = BuildScheduleReview(query, calendar);
        var context = JsonSerializer.Serialize(new {
            activeTaskTypes, verifiedScheduleReview = report.Facts,
            clock = new { currentLocal = TimeZoneInfo.ConvertTime(Now, zone), today = LocalDate(Now, state.Preference) }
        }, AgentJson);
        var explanation = await RunJob(uid, "AI_REQUEST", () => ((IPlanningAgentInterpreter)interpreter)
            .InterpretAsync(request, context, [], state.Preference.TimeZoneId, Now, ct), ct);
        var advice = "";
        try
        {
            ValidateIntent(explanation);
            if (explanation.ScheduleReviewQuery == query && explanation.FreeTimeQuery is null &&
                !HasAgentActions(explanation) && explanation.Question.Length == 0 &&
                !string.IsNullOrWhiteSpace(explanation.Reply) && explanation.Reply.Length is >= 350 and <= 2200 &&
                RequestMatches(MatchText(explanation.Reply), @"\b(?:da|vua) (?:tu dong )?(?:luu|doi|doi lich|tao|cap nhat|xep|len lich|ap dung|them)\b").Count == 0)
                advice = explanation.Reply.Trim();
        }
        catch (AuthException error) when (error.Code == "AGENT_INVALID_INTENT") { /* Never turn advice into a mutation. */ }
        return (advice.Length > 0 ? advice : report.Fallback) + "\n\n" + report.Details +
            "\n\nĐây là phân tích và gợi ý; lịch hiện tại chưa thay đổi. Nếu muốn, bạn có thể yêu cầu mình chuẩn bị phương án để xem trước và xác nhận.";
    }

    private string VerifiedScheduleOverviewReply(AgentFreeTimeQuery query, PlanningState state)
    {
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        var tasks = state.Tasks.ToDictionary(x => x.Id);
        var entries = state.Events.Select(x => (x.Name, x.Start, x.End))
            .Concat(state.Sessions.Select(x => (
                Name: (tasks.GetValueOrDefault(x.TaskId)?.Name ?? "Phiên làm việc") + (x.Status == WorkSessionStatus.Completed ? " (đã hoàn tất)" : ""), x.Start, x.End)))
            .Concat(state.Exceptions.Where(x => !x.IsAvailable).Select(x => (
                Name: string.IsNullOrWhiteSpace(x.Note) ? "Giờ bận" : x.Note,
                Start: PlanningEngine.At(x.Date, x.StartMinute, zone), End: PlanningEngine.At(x.Date, x.EndMinute, zone))))
            .Where(x => x.Start < query.End && x.End > query.Start).OrderBy(x => x.Start).ThenBy(x => x.End).ThenBy(x => x.Name).ToArray();
        var label = CalendarQueryLabel(query, state.Preference);
        var lines = new List<string> { entries.Length == 0
            ? $"{label}, bạn chưa có cuộc hẹn hay phiên làm việc nào được lưu."
            : $"{label}, bạn có {entries.Length} mục trong lịch. Mình sắp theo giờ để bạn dễ theo dõi nhé:\n" };
        foreach (var item in entries.Take(12))
        {
            var start = TimeZoneInfo.ConvertTime(item.Start, zone);
            var end = TimeZoneInfo.ConvertTime(item.End, zone);
            var sameDay = LocalDate(query.Start, state.Preference) == LocalDate(query.End.AddTicks(-1), state.Preference);
            var window = sameDay && start.Date == end.Date ? $"{start:HH:mm}–{end:HH:mm}"
                : $"{start:dd/MM HH:mm}–{end:dd/MM HH:mm}";
            lines.Add($"- {window}: {item.Name}");
        }
        if (entries.Length > 12) lines.Add($"Mình đang hiển thị 12/{entries.Length} mục; bạn có thể hỏi riêng một buổi để xem tiếp.");
        lines.Add("\n" + VerifiedFreeTimeReply(query, state, partOfOverview: true));
        return string.Join("\n", lines);
    }

    private ReviewReport BuildScheduleReview(AgentFreeTimeQuery query, PlanningState state)
    {
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        DateTimeOffset Local(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, zone);
        string Stamp(DateTimeOffset value) => Local(value).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        string Window(DateTimeOffset start, DateTimeOffset end) => $"{Stamp(start)}–{Local(end):HH:mm}" +
            (Local(start).Date != Local(end).Date ? $" ({Local(end):dd/MM/yyyy})" : "");
        TimeSlot Clip(DateTimeOffset start, DateTimeOffset end) => new(start < query.Start ? query.Start : start, end > query.End ? query.End : end);
        var tasks = state.Tasks.ToDictionary(x => x.Id);
        var entries = state.Events.Where(x => x.Start < query.End && x.End > query.Start)
            .Select(x => new ReviewEntry(x.Id, null, x.Name, x.Start, x.End, "EVENT", "FIXED"))
            .Concat(state.Sessions.Where(x => x.Start < query.End && x.End > query.Start)
                .Select(x => new ReviewEntry(x.Id, x.TaskId, tasks.GetValueOrDefault(x.TaskId)?.Name ?? "Phiên làm việc", x.Start, x.End, "WORK", x.Status.ToString())))
            .OrderBy(x => x.Start).ToArray();
        var occupiedMinutes = (int)PlanningEngine.Merge(entries.Select(x => Clip(x.Start, x.End))).Sum(x => (x.End - x.Start).TotalMinutes);
        var conflicts = new List<string>();
        for (var i = 0; i < entries.Length && conflicts.Count < 6; i++)
            for (var j = i + 1; j < entries.Length && entries[j].Start < entries[i].End && conflicts.Count < 6; j++)
                conflicts.Add($"“{entries[i].Name}” và “{entries[j].Name}” trùng {Window(entries[j].Start > query.Start ? entries[j].Start : query.Start,
                    new[] { entries[i].End, entries[j].End, query.End }.Min())}");
        var remainingStart = query.Start > Now ? query.Start : Now;
        var gaps = query.End > remainingStart ? AgentFreeTimeSlots(state, remainingStart, query.End) : [];
        var minimum = state.Preference.MinimumSessionMinutes;
        var shortGaps = gaps.Where(x => (x.End - x.Start).TotalMinutes < minimum).ToArray();
        var largest = gaps.OrderByDescending(x => x.End - x.Start).ThenBy(x => x.Start).FirstOrDefault();
        var bias = new[] { new BiasView("All", 0, 1, false) };
        var occupied = Occupied(state);
        var completion = engine.CompletionTimes(state, occupied, bias, Now);
        var deadlineLookahead = query.Start <= Now && query.End > Now && Now.AddHours(48) > query.End ? Now.AddHours(48) : query.End;
        var relevant = state.Tasks.Where(x => !x.IsDeleted && engine.Remaining(x, bias) > 0 &&
            !state.Tasks.Any(child => child.ParentId == x.Id && !child.IsDeleted) &&
            (x.Deadline <= deadlineLookahead || entries.Any(e => e.TaskId == x.Id)))
            .OrderBy(x => x.Deadline).ThenByDescending(x => x.Priority).ToArray();
        var checks = relevant.Select(task => {
            var saved = state.Sessions.Where(x => x.TaskId == task.Id && x.End > Now && x.End <= task.Deadline &&
                x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused).ToArray();
            var valid = saved.Where(s => {
                if (!PlanningEngine.InsideWorkWindow(task, s.Start, s.End) ||
                    !engine.FreeSlots(state, s.Start, s.End).Any(slot => slot.Start <= s.Start && slot.End >= s.End) ||
                    occupied.Any(other => other.Id != s.Id && other.Start < s.End && s.Start < other.End)) return false;
                if (s.Status == WorkSessionStatus.Scheduled && s.Start >= Now) {
                    try { ValidateSession(state, task.Id, s.Start, s.End, occupied, s.Id, false, false, false); }
                    catch (AuthException) { return false; }
                }
                return true;
            }).ToArray();
            var allocated = (int)valid.Sum(x => (x.End - (x.Start < Now ? Now : x.Start)).TotalMinutes);
            var remaining = engine.Remaining(task, bias);
            var dependencyOkay = state.Dependencies.Where(x => x.TaskId == task.Id).All(edge =>
                completion.TryGetValue(edge.PredecessorId, out var finish) && valid.All(s => s.Start >= finish));
            var concern = task.Deadline <= Now ? "Đã qua hạn, cần bạn quyết định cách xử lý; không tự gia hạn."
                : saved.Any(s => s.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused) ? "Đang thực hiện; cần đối chiếu tiến độ thực tế."
                : !dependencyOkay ? "Cần kiểm tra công việc tiền nhiệm trước khi xếp tiếp."
                : allocated < remaining || !task.Splittable && !valid.Any(s => (s.End - s.Start).TotalMinutes >= remaining)
                    ? "Chưa có đủ thời lượng trong các phiên phù hợp trước hạn."
                    : "Đã có thời lượng trước hạn trong lịch; đây không phải xác nhận công việc đã hoàn thành.";
            return new ReviewTask(task.Id, task.Name, task.Deadline, remaining, allocated, concern);
        }).ToArray();
        var suggestions = new List<ReviewSuggestion>();
        var blocks = state.Sessions.Select(x => new TimeSlot(x.Start.AddMinutes(-state.Preference.BreakMinutes), x.End.AddMinutes(state.Preference.BreakMinutes)));
        var proposalStart = remainingStart > Now.AddMinutes(5) ? remainingStart : Now.AddMinutes(5);
        var usable = query.End > proposalStart ? engine.FreeSlots(state, proposalStart, query.End, blocks) : [];
        foreach (var task in relevant.Where(t => !occupied.Any(s => s.TaskId == t.Id))
            .OrderByDescending(t => t.Priority == 4).ThenBy(t => t.Deadline).ThenByDescending(t => t.Priority).Take(20))
        {
            var duration = engine.Remaining(task, bias);
            if (duration < Math.Max(minimum, task.MinimumSessionMinutes) || duration > state.Preference.MaximumSessionMinutes) continue;
            var earliest = state.Dependencies.Where(x => x.TaskId == task.Id)
                .Select(x => completion.GetValueOrDefault(x.PredecessorId, DateTimeOffset.MaxValue)).DefaultIfEmpty(remainingStart).Max();
            if (earliest == DateTimeOffset.MaxValue) continue;
            foreach (var gap in usable)
            {
                var start = new[] { gap.Start, earliest, task.WorkWindowStart ?? gap.Start }.Max();
                var end = start.AddMinutes(duration);
                if (end > gap.End || end > task.Deadline || task.WorkWindowEnd is { } bound && end > bound) continue;
                try { ValidateSession(state, task.Id, start, end, occupied, Guid.Empty, false, false, false); }
                catch (AuthException) { continue; }
                suggestions.Add(new(task.Id, task.Name, start, end)); break;
            }
            if (suggestions.Count == 3) break;
        }
        var capacity = query.End > remainingStart ? engine.CapacityDays(state, remainingStart, query.End, Now, false) : [];
        var hasAvailability = state.Availability.Length > 0 || state.Exceptions.Any(x => x.IsAvailable &&
            x.Date >= LocalDate(query.Start, state.Preference) && x.Date <= LocalDate(query.End.AddTicks(-1), state.Preference));
        var totalFree = (int)gaps.Sum(x => (x.End - x.Start).TotalMinutes);
        var details = new List<string> { $"**Lịch đã đối chiếu: {Stamp(query.Start)} → {Stamp(query.End)}**",
            $"- Có {entries.Count(x => x.Kind == "EVENT")} lịch cố định và {entries.Count(x => x.Kind == "WORK")} phiên làm việc; tổng thời gian có lịch là {occupiedMinutes} phút (đã gộp phần trùng)." };
        foreach (var item in entries.Take(6)) details.Add($"- {item.Name}: {Window(item.Start, item.End)}" + (item.Status == "Completed" ? " · phiên đã đánh dấu hoàn tất" : ""));
        if (entries.Length > 6) details.Add($"- Đang hiển thị 6/{entries.Length} mục; các số tổng hợp vẫn tính toàn bộ lịch trong khoảng này.");
        details.Add($"\n**Thời gian còn lại và điểm cần chú ý**\n- Còn {totalFree} phút trống theo giờ rảnh đã cài đặt và lịch đã lưu; {shortGaps.Length} khoảng ngắn dưới {minimum} phút.");
        if (!hasAvailability) details.Add("- Chưa có giờ rảnh phù hợp được cài đặt. Không thể coi các giờ chưa có sự kiện là giờ bạn có thể làm việc.");
        if (largest is not null) details.Add($"- Khoảng trống dài nhất: {Window(largest.Start, largest.End)} ({(int)(largest.End - largest.Start).TotalMinutes} phút). Khoảng trống chưa tự động là một phiên làm việc hợp lệ.");
        foreach (var conflict in conflicts.Take(3)) details.Add("- " + conflict + ".");
        foreach (var task in checks.Take(4)) details.Add($"- {task.Name}: hạn {Stamp(task.Deadline)}, còn khoảng {task.RemainingMinutes} phút theo tiến độ đã lưu; có {task.SavedMinutes} phút trong lịch trước hạn. {task.Concern}");
        if (checks.Length > 4) details.Add($"- Đang nêu 4/{checks.Length} công việc liên quan, ưu tiên hạn sớm.");
        details.Add("\n**Gợi ý có thể cân nhắc**");
        foreach (var suggestion in suggestions) details.Add($"- “{suggestion.Name}”: {Window(suggestion.Start, suggestion.End)}; đủ thời lượng còn lại và đã kiểm tra các ràng buộc. Đây là một phương án riêng, chưa đặt chỗ.");
        if (suggestions.Count == 0) details.Add(shortGaps.Length > 0
            ? "- Các khoảng ngắn phù hợp để nghỉ, rà soát hoặc chuẩn bị cho phiên tiếp theo. Chưa có đề xuất thêm nguyên một công việc đã được kiểm tra đủ điều kiện trong khoảng này."
            : "- Giữ các phiên hợp lệ đã có; nếu muốn thêm hoặc dời việc, mình sẽ kiểm tra một phương án cụ thể trước khi bạn xác nhận.");
        var facts = new {
            query, asOf = Local(Now), deadlineLookaheadEnd = Local(deadlineLookahead), timeZoneId = state.Preference.TimeZoneId, hasAvailability,
            metrics = new { fixedEvents = entries.Count(x => x.Kind == "EVENT"), workSessions = entries.Count(x => x.Kind == "WORK"),
                occupiedMinutes, remainingCalendarFreeMinutes = totalFree, shortGapCount = shortGaps.Length, minimumSessionMinutes = minimum,
                schedulableMinutesWithinDailyBudgets = capacity.Sum(x => x.SchedulableMinutes) },
            entries = entries.Take(24).Select(x => new { x.Name, start = Local(x.Start), end = Local(x.End), window = Window(x.Start, x.End), x.Kind, x.Status }),
            entryScope = new { included = Math.Min(24, entries.Length), total = entries.Length, truncated = entries.Length > 24 },
            freeGaps = gaps.OrderByDescending(x => x.End - x.Start).Take(8).Select(x => new { start = Local(x.Start), end = Local(x.End), minutes = (int)(x.End - x.Start).TotalMinutes }),
            conflicts, tasks = checks.Take(12).Select(x => new { x.Name, deadline = Local(x.Deadline), priority = tasks[x.Id].Priority, x.RemainingMinutes, x.SavedMinutes, x.Concern }),
            taskScope = new { included = Math.Min(12, checks.Length), total = checks.Length, truncated = checks.Length > 12 },
            taskSuggestions = suggestions.Select(x => new { x.Name, start = Local(x.Start), end = Local(x.End), minutes = (int)(x.End - x.Start).TotalMinutes }),
            taskSuggestionsAreAlternatives = true, taskProgressIsCurrentSnapshot = true, changesSaved = false
        };
        var fallback = shortGaps.Length > 0
            ? "Lịch của bạn có những khoảng trống bị chia nhỏ, nên tổng thời gian rảnh chưa phản ánh thời gian có thể tập trung liên tục. Mình gợi ý giữ các khoảng nghỉ ngắn, đồng thời xem việc gần hạn và khoảng dài hơn trước khi quyết định xếp thêm."
            : "Mình đã đối chiếu các mục lịch và tiến độ đã lưu để bạn dễ chọn việc tiếp theo. Bạn có thể xem những việc đến hạn sớm trước; chỉ nên thêm việc khi có đủ thời lượng và vẫn giữ được các ràng buộc hiện tại.";
        return new(facts, string.Join("\n", details), fallback);
    }
}
