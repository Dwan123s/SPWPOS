namespace SPWPOS.Domain.Entities;

public sealed class WorkPreference : OwnedRecord
{
    public int MaximumDailyMinutes { get; set; } = 480;
    public bool AutoReschedule { get; set; } = true;
    public int PlanningHorizonDays { get; set; } = 14;
    public string PreferredPeriod { get; set; } = "NONE";
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public int MinimumSessionMinutes { get; set; } = 25;
    public int MaximumSessionMinutes { get; set; } = 90;
    public int BreakMinutes { get; set; } = 10;
    public int PreferredStartMinute { get; set; } = 8 * 60;
    public int PreferredEndMinute { get; set; } = 18 * 60;
    public Guid Revision { get; set; } = Guid.NewGuid();
    public Guid MutationVersion { get; set; } = Guid.NewGuid();
}
