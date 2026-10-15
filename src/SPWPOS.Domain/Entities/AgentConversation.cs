namespace SPWPOS.Domain.Entities;

public sealed class AgentConversation : OwnedRecord
{
    public string Title { get; set; } = "Hội thoại mới";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
