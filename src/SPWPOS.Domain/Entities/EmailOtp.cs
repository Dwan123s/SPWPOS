using SPWPOS.Domain.Enums;
namespace SPWPOS.Domain.Entities;

public sealed class EmailOtp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public OtpPurpose Purpose { get; set; }
    public byte[] OtpHash { get; set; } = [];
    public DateTimeOffset ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public bool IsUsed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
