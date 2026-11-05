namespace SPWPOS.Domain.Entities;

public sealed class Friendship
{
    // One canonical row for an unordered pair prevents duplicate and crossed requests.
    public Guid UserLowId { get; set; }
    public Guid UserHighId { get; set; }
    public Guid RequestedById { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
