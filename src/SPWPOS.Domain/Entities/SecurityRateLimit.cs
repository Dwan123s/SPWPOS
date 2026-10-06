namespace SPWPOS.Domain.Entities;

public sealed class SecurityRateLimit
{
    public string Id { get; set; } = "";
    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset LastRequestAt { get; set; }
    public int Count { get; set; }
}
