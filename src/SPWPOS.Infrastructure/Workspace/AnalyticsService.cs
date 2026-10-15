using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public async Task<AnalyticsView> GetAnalyticsAsync(Guid uid, AnalyticsInput input, CancellationToken ct)
    {
        var preference = await Mine<WorkPreference>(uid).AsNoTracking().SingleOrDefaultAsync(ct) ?? new();
        var zone = PlanningEngine.Zone(preference.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Now, zone).DateTime);
        var end = input.End ?? today; var start = input.Start ?? end.AddDays(1 - input.Days);
        var length = end.DayNumber - start.DayNumber + 1;
        Require(input.Days is >= 1 and <= 90 && length is >= 1 and <= 90 && end <= today,
            "INVALID_ANALYTICS_RANGE", "Chọn khoảng từ 1 đến 90 ngày, không vượt hôm nay.");
        var previousStart = start.AddDays(-length);
        var lowerBound = PlanningEngine.At(previousStart, 0, zone); var until = PlanningEngine.At(end.AddDays(1), 0, zone);
        var sessions = await Mine<WorkSession>(uid).AsNoTracking().Where(x => x.Start < until && x.End > lowerBound &&
            x.Status != WorkSessionStatus.Cancelled).Select(x => new { x.Start, x.End }).ToArrayAsync(ct);
        var segments = await (from segment in Mine<ExecutionSegment>(uid).AsNoTracking()
            join execution in Mine<SessionExecution>(uid).AsNoTracking() on segment.ExecutionId equals execution.Id
            where segment.Start < until && (segment.End == null || segment.End > lowerBound)
            select new { segment.Start, segment.End }).ToArrayAsync(ct);
        // Completion is attributed to CompletedAt, never the scheduled session end.
        // Containers are excluded to avoid rewarding/counting the same leaf work twice.
        var completions = await Mine<WorkTask>(uid).AsNoTracking().Where(x => !x.IsDeleted && x.Status == WorkTaskStatus.Completed &&
            x.CompletedAt >= lowerBound && x.CompletedAt < until && !db.WorkTasks.Any(child => child.UserId == uid && !child.IsDeleted && child.ParentId == x.Id))
            .Select(x => new { x.CompletedAt, x.Deadline }).ToArrayAsync(ct);
        var recorded = await Mine<SessionExecution>(uid).AsNoTracking().Where(x => x.EndedAt >= lowerBound && x.EndedAt < until)
            .Select(x => new { x.EndedAt, x.ActualSeconds, x.IsUserAdjusted }).ToArrayAsync(ct);
        static long Overlap(DateTimeOffset a, DateTimeOffset b, DateTimeOffset lo, DateTimeOffset hi)
            => (long)Math.Max(0, ((b < hi ? b : hi) - (a > lo ? a : lo)).TotalSeconds);
        var allDays = Enumerable.Range(0, length * 2).Select(offset =>
        {
            var date = previousStart.AddDays(offset); var lo = PlanningEngine.At(date, 0, zone); var hi = PlanningEngine.At(date.AddDays(1), 0, zone);
            return new AnalyticsDay(date, sessions.Sum(x => Overlap(x.Start, x.End, lo, hi)),
                segments.Where(x => x.End != null).Sum(x => Overlap(x.Start, x.End!.Value < Now ? x.End.Value : Now, lo, hi)),
                segments.Where(x => x.End == null).Sum(x => Overlap(x.Start, Now, lo, hi)),
                completions.Count(x => x.CompletedAt >= lo && x.CompletedAt < hi),
                completions.Count(x => x.CompletedAt >= lo && x.CompletedAt < hi && x.CompletedAt <= x.Deadline));
        }).ToArray();
        AnalyticsTotals Total(AnalyticsDay[] days)
        {
            var lo = PlanningEngine.At(days[0].Date, 0, zone); var hi = PlanningEngine.At(days[^1].Date.AddDays(1), 0, zone);
            var rows = recorded.Where(x => x.EndedAt >= lo && x.EndedAt < hi).ToArray();
            return new(days.Sum(x => x.PlannedSeconds), days.Sum(x => x.FocusSeconds), days.Sum(x => x.OpenFocusSeconds),
                days.Sum(x => x.CompletedTasks), days.Sum(x => x.OnTimeTasks), rows.Count(x => x.IsUserAdjusted), rows.Sum(x => x.ActualSeconds));
        }
        var currentDays = allDays[length..]; var current = Total(currentDays); var previous = Total(allDays[..length]);
        var notices = new List<string>();
        if (current.CompletedTasks < 3) notices.Add("INSUFFICIENT_COMPLETION_SAMPLES");
        if (current.OpenFocusSeconds > 0) notices.Add("OPEN_TIMER_PROVISIONAL");
        if (current.AdjustedExecutions > 0) notices.Add("MANUAL_ACTUAL_SEPARATE_FROM_FOCUS");
        if (previous.FocusSeconds == 0) notices.Add("NO_COMPARISON_BASELINE");
        return new(start, end, zone.Id, Now, currentDays, current, previous,
            previous.FocusSeconds == 0 ? null : Math.Round(100d * (current.FocusSeconds - previous.FocusSeconds) / previous.FocusSeconds, 1), notices.ToArray());
    }

    public async Task<TaskPerformancePage> GetTaskPerformanceAsync(Guid uid, int page, int pageSize, string? search, CancellationToken ct)
    {
        Require(page is >= 1 and <= 100000 && pageSize is >= 1 and <= 100 && (search?.Length ?? 0) <= 100,
            "VALIDATION_ERROR", "Phân trang hoặc từ khóa không hợp lệ.");
        var term = (search ?? "").Trim();
        var query = Mine<WorkTask>(uid).AsNoTracking().Where(x => !x.IsDeleted && (term == "" || x.Name.Contains(term)));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new TaskPerformance(x.Id, x.Name, x.Status.ToString(), x.Progress, x.EstimatedMinutes,
                db.SessionExecutions.Where(e => e.UserId == uid && db.WorkSessions.Any(s => s.UserId == uid && s.Id == e.SessionId && s.TaskId == x.Id))
                    .Sum(e => (long?)e.ActualSeconds) ?? 0, x.CompletedAt)).ToArrayAsync(ct);
        return new(items, page, pageSize, total);
    }
}
