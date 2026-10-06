using SPWPOS.Domain.Enums;
namespace SPWPOS.Domain.Entities;

public sealed class SecurityAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public SecurityEventType EventType { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsSuccessful { get; set; }
    // Only allowlisted internal codes; never raw request data or exception text.
    public string? Metadata { get; set; }
}
