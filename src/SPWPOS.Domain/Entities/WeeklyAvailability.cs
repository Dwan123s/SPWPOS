namespace SPWPOS.Domain.Entities;

public sealed class WeeklyAvailability : OwnedRecord
{
    public int DayOfWeek { get; set; }
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
}
