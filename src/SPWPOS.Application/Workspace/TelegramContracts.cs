namespace SPWPOS.Application.Workspace;

public sealed record TelegramStatus(bool Configured, bool Connected, bool Enabled, string? DisplayName,
    DateTimeOffset? ConnectedAt, DateTimeOffset? LinkExpiresAt, string? ErrorCode, string? ConfigurationError = null);
public sealed record TelegramLink(string Url, DateTimeOffset ExpiresAt);
public sealed record TelegramPreference(bool Enabled);
public interface ITelegramNotificationService
{
    Task<TelegramStatus> StatusAsync(Guid userId, CancellationToken ct);
    Task<TelegramLink> ConnectAsync(Guid userId, CancellationToken ct);
    Task SetEnabledAsync(Guid userId, bool enabled, CancellationToken ct);
    Task DisconnectAsync(Guid userId, CancellationToken ct);
}
