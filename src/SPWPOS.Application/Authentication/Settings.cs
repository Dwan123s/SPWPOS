namespace SPWPOS.Application.Authentication;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "SPWPOS";
    public string Audience { get; set; } = "SPWPOS.Client";
    // Base64 encoded >= 32 random bytes; never include in committed configuration.
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
public sealed class OtpSettings
{
    public int Length { get; set; } = 6;
    public int ExpirationMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 60;
    public string SecretKey { get; set; } = "";
}
public sealed class PasswordSettings
{
    public int MinLength { get; set; } = 10;
    public int MaxLength { get; set; } = 128;
    public int MemoryKiB { get; set; } = 32768;
    public int Iterations { get; set; } = 3;
    // libsodium uses p=1. This value is validated on startup.
    public int Parallelism { get; set; } = 1;
}
public sealed class EmailSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string SenderEmail { get; set; } = "";
    public string SenderName { get; set; } = "SPWPOS";
    public bool EnableSsl { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 15;
}
