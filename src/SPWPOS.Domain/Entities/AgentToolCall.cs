namespace SPWPOS.Domain.Entities;

public sealed class AgentToolCall : OwnedRecord
{
    public Guid RunId { get; set; }
    public int Sequence { get; set; }
    public string Tool { get; set; } = "";
    public string Status { get; set; } = "";
    public string ArgumentsHash { get; set; } = "";
    public string ResultCode { get; set; } = "";
    public long DurationMilliseconds { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
