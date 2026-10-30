namespace SPWPOS.Domain.Entities;

public sealed class FixedEvent : OwnedRecord
{
    public bool? IsUrgent { get; set; }
    public string Description { get; set; } = "";
    public string EventType { get; set; } = "OTHER";
    public string Name { get; set; } = "";
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
}
