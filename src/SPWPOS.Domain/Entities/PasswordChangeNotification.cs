namespace SPWPOS.Domain.Entities;
// Durable security notification; contains no password, OTP, token, or email body.
public sealed class PasswordChangeNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}
