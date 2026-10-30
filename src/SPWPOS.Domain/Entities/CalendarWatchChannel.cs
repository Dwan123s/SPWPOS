namespace SPWPOS.Domain.Entities;

public sealed class CalendarWatchChannel : OwnedRecord
{
    public Guid ConnectionId { get; set; }
    public string ResourceId { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}
