namespace SPWPOS.Domain.Entities;

public sealed class AvailabilityException : OwnedRecord
{
    public bool OverridesWeeklyAvailability { get; set; }
    public DateOnly Date { get; set; }
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public bool IsAvailable { get; set; }
    public string Note { get; set; } = "";
}
