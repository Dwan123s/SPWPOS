using SPWPOS.Domain.Enums;
namespace SPWPOS.Domain.Entities;

public sealed class PasswordResetTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public OtpPurpose Purpose { get; set; } = OtpPurpose.ForgotPassword;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
