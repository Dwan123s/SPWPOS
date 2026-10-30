namespace SPWPOS.Domain.Entities;

public sealed class CalendarEventLink : OwnedRecord
{
    public Guid ConnectionId { get; set; }
    public string Kind { get; set; } = "Event";
    public Guid LocalId { get; set; }
    public string GoogleEventId { get; set; } = "";
    public string Etag { get; set; } = "";
    public string BaseJson { get; set; } = "null";
    public string? ConflictJson { get; set; }
    public string? Resolution { get; set; }
    public bool RemoteDeleted { get; set; }
    public bool ReadOnly { get; set; }
    public string? SeriesId { get; set; }
}
