namespace SPWPOS.Domain.Entities;

public sealed class CalendarSyncOutbox : OwnedRecord
{
    public string Kind { get; set; } = "Event";
    public Guid LocalId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
