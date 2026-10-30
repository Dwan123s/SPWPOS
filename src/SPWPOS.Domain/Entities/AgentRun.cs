namespace SPWPOS.Domain.Entities;

public sealed class AgentRun : OwnedRecord
{
    public Guid ConversationId { get; set; }
    public Guid TurnId { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public Guid SnapshotRevision { get; set; }
    public string Status { get; set; } = "RUNNING";
    public string Goal { get; set; } = "";
    public string StateJson { get; set; } = "";
    public int ModelTurns { get; set; }
    public int ToolCalls { get; set; }
    public int Simulations { get; set; }
    public int Continuations { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset LeaseUntil { get; set; }
}
