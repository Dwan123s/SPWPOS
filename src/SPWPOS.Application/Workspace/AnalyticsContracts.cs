using System.ComponentModel.DataAnnotations;
namespace SPWPOS.Application.Workspace;

public sealed record AnalyticsInput(DateOnly? Start = null, DateOnly? End = null, [Range(1, 90)] int Days = 7);
public sealed record AnalyticsDay(DateOnly Date, long PlannedSeconds, long FocusSeconds, long OpenFocusSeconds,
    int CompletedTasks, int OnTimeTasks);
public sealed record AnalyticsTotals(long PlannedSeconds, long FocusSeconds, long OpenFocusSeconds, int CompletedTasks,
    int OnTimeTasks, int AdjustedExecutions, long RecordedActualSeconds);
public sealed record AnalyticsView(DateOnly Start, DateOnly End, string TimeZoneId, DateTimeOffset AsOf,
    AnalyticsDay[] Days, AnalyticsTotals Current, AnalyticsTotals Previous, double? FocusChangePercent,
    string[] Notices);
public sealed record TaskPerformance(Guid Id, string Name, string Status, int Progress, int EstimatedMinutes,
    long RecordedActualSeconds, DateTimeOffset? CompletedAt);
public sealed record TaskPerformancePage(TaskPerformance[] Items, int Page, int PageSize, int Total);
