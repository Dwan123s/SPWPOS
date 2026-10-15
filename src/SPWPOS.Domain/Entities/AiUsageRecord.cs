namespace SPWPOS.Domain.Entities;

public sealed class AiUsageRecord : OwnedRecord
{
    public string Operation { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string? ModelVersion { get; set; }
    public string Status { get; set; } = "RESERVED";
    public DateTimeOffset ReservedAt { get; set; }
    public DateTimeOffset LeaseUntil { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long LatencyMilliseconds { get; set; }
}
