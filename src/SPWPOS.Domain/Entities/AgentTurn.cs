namespace SPWPOS.Domain.Entities;

public sealed class AgentTurn : OwnedRecord
{
    public Guid ConversationId { get; set; }
    public Guid RequestId { get; set; }
    public string InputFingerprint { get; set; } = "";
    public string Text { get; set; } = "";
    public string Status { get; set; } = "PROCESSING";
    public string CommandJson { get; set; } = "";
    public string ChangesJson { get; set; } = "";
    public string ResultJson { get; set; } = "";
    public string UndoJson { get; set; } = "";
    public Guid ExpectedRevision { get; set; }
    public Guid? AppliedRevision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
