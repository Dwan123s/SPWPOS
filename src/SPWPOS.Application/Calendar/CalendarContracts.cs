namespace SPWPOS.Application.Calendar;

public sealed record CalendarReminderInput(int ReminderMinutes = 15, bool RemindAtStart = false);
public sealed record CalendarConnectView(string AuthorizationUrl);
public sealed record CalendarConflictView(Guid Id, string Kind, CalendarSnapshot? Baseline, CalendarSnapshot? Local, CalendarSnapshot? Google, bool ReadOnly, bool CanKeepGoogle = true);
public sealed record CalendarResolveInput(string Keep);
public sealed record CalendarConnectionView(bool Configured, bool Connected, string Status, string? Email,
    DateTimeOffset? LastSyncedAt, string? LastError, int PendingChanges, int Conflicts,
    int ReminderMinutes, bool RemindAtStart, bool WebhookEnabled, string? CalendarUrl);
public sealed record CalendarSnapshot(string Title, DateTimeOffset Start, DateTimeOffset End);
public sealed record CalendarRemoteEvent(string Id, string Etag, CalendarSnapshot? Snapshot, bool Deleted, bool ReadOnly = false,
    string? SeriesId = null, bool IsSeries = false);
public sealed record CalendarPage(IReadOnlyList<CalendarRemoteEvent> Items, string? PageToken, string? SyncToken);
public sealed record CalendarGrant(string AccessToken, string? RefreshToken, string Subject, string Email);
public sealed record CalendarWatchResult(string ResourceId, DateTimeOffset Expiration);

public interface IGoogleCalendarGateway
{
    string AuthorizationUrl(string state);
    Task<CalendarGrant> ExchangeAsync(string code, CancellationToken ct);
    Task<string> RefreshAsync(string refreshToken, CancellationToken ct);
    Task<string> CreateCalendarAsync(string accessToken, CancellationToken ct);
    Task<CalendarPage> ListAsync(string accessToken, string calendarId, string? syncToken, string? pageToken, CancellationToken ct);
    Task<CalendarRemoteEvent?> GetAsync(string accessToken, string calendarId, string eventId, CancellationToken ct);
    Task<IReadOnlyList<CalendarRemoteEvent>> InstancesAsync(string accessToken, string calendarId, string eventId,
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task<CalendarRemoteEvent> WriteAsync(string accessToken, string calendarId, string eventId, CalendarSnapshot snapshot,
        string? etag, CalendarReminderInput? reminders, CancellationToken ct);
    Task DeleteAsync(string accessToken, string calendarId, string eventId, string etag, CancellationToken ct);
    Task<CalendarWatchResult> WatchAsync(string accessToken, string calendarId, string channelId, string token, CancellationToken ct);
    Task StopWatchAsync(string accessToken, string channelId, string resourceId, CancellationToken ct);
    Task RevokeAsync(string refreshToken, CancellationToken ct);
}

public interface IGoogleCalendarService
{
    Task<CalendarConnectionView> StatusAsync(Guid userId, CancellationToken ct);
    Task<CalendarConnectView> ConnectAsync(Guid userId, CalendarReminderInput input, CancellationToken ct);
    Task CompleteOAuthAsync(string state, string? code, string? error, CancellationToken ct);
    Task RequestSyncAsync(Guid userId, CancellationToken ct);
    Task UpdateRemindersAsync(Guid userId, CalendarReminderInput input, CancellationToken ct);
    Task DisconnectAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<CalendarConflictView>> ConflictsAsync(Guid userId, CancellationToken ct);
    Task ResolveAsync(Guid userId, Guid id, string keep, CancellationToken ct);
    Task ReceiveWebhookAsync(string channelId, string resourceId, string token, CancellationToken ct);
}
