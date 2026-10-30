using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Calendar;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;

namespace SPWPOS.Infrastructure.Calendar;

public sealed partial class GoogleCalendarService(ApplicationDbContext db, IGoogleCalendarGateway gateway,
    IOptions<GoogleCalendarOptions> options, IDataProtectionProvider protection, TimeProvider clock) : IGoogleCalendarService
{
    private GoogleCalendarOptions Options => options.Value;
    private DateTimeOffset Now => clock.GetUtcNow();
    private IDataProtector Protector => protection.CreateProtector("PLANORA.GoogleCalendar.RefreshToken.v1");
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private string Protect(Guid uid, string value) => Protector.CreateProtector(uid.ToString("N")).Protect(value);
    private string Unprotect(GoogleCalendarConnection c) => Protector.CreateProtector(c.UserId.ToString("N")).Unprotect(c.ProtectedRefreshToken);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T? Deserialize<T>(string? json) => json is null ? default : JsonSerializer.Deserialize<T>(json, Json);
    private static void ValidateReminder(CalendarReminderInput input)
    {
        if (input.ReminderMinutes is < 0 or > 40320) throw new AuthException("VALIDATION_ERROR", "Thời gian nhắc phải từ 0 đến 40320 phút.", 400);
    }
    private void RequireConfiguration()
    {
        if (!Options.Configured) throw new AuthException("CALENDAR_NOT_CONFIGURED", "Máy chủ chưa cấu hình Google Calendar. Xem hướng dẫn cấu hình tích hợp.", 503);
    }
    private async Task<GoogleCalendarConnection> Connection(Guid uid, CancellationToken ct) =>
        await db.GoogleCalendarConnections.SingleOrDefaultAsync(x => x.UserId == uid && x.Status != "Disconnected", ct)
        ?? throw new AuthException("CALENDAR_NOT_CONNECTED", "Bạn chưa kết nối Google Calendar.", 404);

    public async Task<CalendarConnectionView> StatusAsync(Guid userId, CancellationToken ct)
    {
        if (!Options.Configured) return new(false, false, "NotConfigured", null, null, null, 0, 0, 15, false, false, null);
        var c = await db.GoogleCalendarConnections.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (c is null || c.Status == "Disconnected") return new(true, false, "Disconnected", null, null, null, 0, 0, 15, false,
            !string.IsNullOrWhiteSpace(Options.WebhookUrl), null);
        var pending = await db.CalendarSyncOutbox.CountAsync(x => x.UserId == userId, ct);
        var conflicts = await db.CalendarEventLinks.CountAsync(x => x.ConnectionId == c.Id && x.ConflictJson != null, ct);
        var status = c.Status != "Connected" ? c.Status : c.LastError.Length > 0 ? "Retrying" : conflicts > 0 ? "Conflict" : pending > 0 ? "Pending" : c.LeaseId != null ? "Syncing" : c.LastSyncedAt == null ? "Pending" : "Synced";
        return new(true, true, status, c.Email, c.LastSyncedAt, c.LastError, pending, conflicts, c.ReminderMinutes, c.RemindAtStart,
            !string.IsNullOrWhiteSpace(Options.WebhookUrl), "https://calendar.google.com/calendar/u/0/r");
    }
    public async Task<CalendarConnectView> ConnectAsync(Guid userId, CalendarReminderInput input, CancellationToken ct)
    {
        RequireConfiguration(); ValidateReminder(input);
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.CalendarOAuthRequests.Add(new() { UserId = userId, StateHash = Hash(state), ExpiresAt = Now.AddMinutes(10),
            ReminderMinutes = input.ReminderMinutes, RemindAtStart = input.RemindAtStart });
        await db.SaveChangesAsync(ct); return new(gateway.AuthorizationUrl(state));
    }
    public async Task CompleteOAuthAsync(string state, string? code, string? error, CancellationToken ct)
    {
        RequireConfiguration();
        if (state.Length is < 32 or > 256) throw new AuthException("CALENDAR_OAUTH_STATE", "Liên kết kết nối đã hết hạn. Hãy kết nối lại từ Cài đặt.", 400);
        var hash = Hash(state);
        var request = await db.CalendarOAuthRequests.AsNoTracking().SingleOrDefaultAsync(x => x.StateHash == hash && x.ExpiresAt > Now, ct);
        if (request is null || await db.CalendarOAuthRequests.Where(x => x.Id == request.Id).ExecuteDeleteAsync(ct) != 1)
            throw new AuthException("CALENDAR_OAUTH_STATE", "Liên kết đã dùng hoặc hết hạn. Hãy kết nối lại từ Cài đặt.", 400);
        if (error is not null || string.IsNullOrWhiteSpace(code)) throw new AuthException("CALENDAR_CONSENT_DENIED", "Bạn chưa cấp quyền Google Calendar. Lịch hiện có vẫn được giữ.", 400);
        if (!await db.Users.AnyAsync(x => x.Id == request.UserId && x.IsActive && x.EmailConfirmed, ct))
            throw new AuthException("FORBIDDEN", "Tài khoản không khả dụng.", 403);
        var grant = await gateway.ExchangeAsync(code, ct);
        var c = await db.GoogleCalendarConnections.SingleOrDefaultAsync(x => x.UserId == request.UserId, ct);
        if (c is not null && c.GoogleSubject != grant.Subject)
            throw new AuthException("CALENDAR_ACCOUNT_MISMATCH", "Hãy chọn lại tài khoản Google đã kết nối trước đó để giữ đúng lịch liên kết.", 409);
        if (c is null) { c = new() { UserId = request.UserId, GoogleSubject = grant.Subject }; db.GoogleCalendarConnections.Add(c); }
        if (grant.RefreshToken is not null) c.ProtectedRefreshToken = Protect(c.UserId, grant.RefreshToken);
        if (string.IsNullOrWhiteSpace(c.ProtectedRefreshToken)) throw new AuthException("CALENDAR_OFFLINE_ACCESS", "Google chưa cấp quyền đồng bộ nền. Hãy kết nối lại và cấp quyền truy cập.", 400);
        c.Email = grant.Email; c.Status = "Connected"; c.LastError = ""; c.FailureCount = 0;
        c.ReminderMinutes = request.ReminderMinutes; c.RemindAtStart = request.RemindAtStart;
        c.Version = Guid.NewGuid(); c.NextSyncAt = Now; c.SyncToken = null;
        // New/reconnected scopes trigger a full comparison, preserving mappings.
        await db.SaveChangesAsync(ct);
        await QueueInitialAsync(c, ct);
    }
    private async Task QueueInitialAsync(GoogleCalendarConnection c, CancellationToken ct)
    {
        var pending = await db.CalendarSyncOutbox.Where(x => x.UserId == c.UserId).Select(x => new { x.Kind, x.LocalId }).ToArrayAsync(ct);
        var events = await db.FixedEvents.AsNoTracking().Where(x => x.UserId == c.UserId && x.End > Now).Select(x => x.Id).ToArrayAsync(ct);
        var sessions = await db.WorkSessions.AsNoTracking().Where(x => x.UserId == c.UserId && x.End > Now && x.Status != WorkSessionStatus.Cancelled).Select(x => x.Id).ToArrayAsync(ct);
        db.CalendarSyncOutbox.AddRange(events.Where(id => !pending.Any(x => x.Kind == "Event" && x.LocalId == id)).Select(id => new CalendarSyncOutbox { UserId = c.UserId, LocalId = id, Kind = "Event" }));
        db.CalendarSyncOutbox.AddRange(sessions.Where(id => !pending.Any(x => x.Kind == "Session" && x.LocalId == id)).Select(id => new CalendarSyncOutbox { UserId = c.UserId, LocalId = id, Kind = "Session" }));
        await db.SaveChangesAsync(ct);
    }
    public async Task RequestSyncAsync(Guid userId, CancellationToken ct)
    {
        RequireConfiguration(); var c = await Connection(userId, ct);
        if (c.Status == "Reconnect") throw new AuthException("CALENDAR_RECONNECT", "Hãy kết nối lại để Google cấp quyền truy cập.", 409);
        await db.GoogleCalendarConnections.Where(x => x.Id == c.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextSyncAt, Now), ct);
    }
    public async Task UpdateRemindersAsync(Guid userId, CalendarReminderInput input, CancellationToken ct)
    {
        ValidateReminder(input); var c = await Connection(userId, ct);
        c.ReminderMinutes = input.ReminderMinutes; c.RemindAtStart = input.RemindAtStart; c.Version = Guid.NewGuid();
        // This preference is for newly created events. Existing per-event Google
        // reminders are preserved by ordinary title/time patches.
        await db.SaveChangesAsync(ct);
    }
    public async Task DisconnectAsync(Guid userId, CancellationToken ct)
    {
        var c = await Connection(userId, ct);
        var secret = c.ProtectedRefreshToken;
        c.Status = "Disconnected"; c.ProtectedRefreshToken = ""; c.Version = Guid.NewGuid(); c.LastError = "";
        await db.SaveChangesAsync(ct);
        await db.CalendarSyncOutbox.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        // Revoke after committing the disconnected state. Even if Google is
        // unreachable, no worker can acquire this account again.
        if (secret.Length > 0)
        {
            try
            {
                c.ProtectedRefreshToken = secret; var token = Unprotect(c); c.ProtectedRefreshToken = "";
                var access = await gateway.RefreshAsync(token, ct);
                foreach (var channel in await db.CalendarWatchChannels.Where(x => x.ConnectionId == c.Id).ToArrayAsync(ct))
                {
                    try { if (channel.ResourceId.Length > 0) await gateway.StopWatchAsync(access, channel.Id.ToString("N"), channel.ResourceId, ct); }
                    catch (CalendarProviderException) { }
                }
                await gateway.RevokeAsync(token, ct);
            }
            catch (Exception ex) when (ex is CalendarProviderException or HttpRequestException or CryptographicException or TaskCanceledException) { }
        }
        await db.CalendarWatchChannels.Where(x => x.ConnectionId == c.Id).ExecuteDeleteAsync(ct);
    }
    public async Task<IReadOnlyList<CalendarConflictView>> ConflictsAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.CalendarEventLinks.AsNoTracking().Where(x => x.UserId == userId && x.ConflictJson != null).Take(100).ToArrayAsync(ct);
        var result = new List<CalendarConflictView>();
        foreach (var row in rows)
        {
            var local = await LocalAsync(row, ct); var remote = Deserialize<CalendarRemoteEvent>(row.ConflictJson)?.Snapshot;
            var protectedTime = row.Kind == "Session" && (remote is null || local?.Start != remote.Start || local?.End != remote.End) &&
                await db.WorkSessions.AnyAsync(x => x.UserId == userId && x.Id == row.LocalId &&
                    (x.ActualSeconds > 0 || x.Status == WorkSessionStatus.Running || x.Status == WorkSessionStatus.Paused || x.Status == WorkSessionStatus.Completed), ct);
            result.Add(new(row.Id, row.Kind, Deserialize<CalendarSnapshot>(row.BaseJson), local, remote, row.ReadOnly,
                !protectedTime && Deserialize<CalendarRemoteEvent>(row.ConflictJson)?.IsSeries != true));
        }
        return result;
    }
    public async Task ResolveAsync(Guid userId, Guid id, string keep, CancellationToken ct)
    {
        if (keep is not "local" and not "google") throw new AuthException("VALIDATION_ERROR", "Chọn giữ lịch PLANORA hoặc Google.", 400);
        var c = await Connection(userId, ct);
        var link = await db.CalendarEventLinks.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId && x.ConnectionId == c.Id && x.ConflictJson != null, ct)
            ?? throw new AuthException("NOT_FOUND", "Không tìm thấy xung đột.", 404);
        if (link.ReadOnly && keep == "local") throw new AuthException("CALENDAR_READ_ONLY", "Hãy chỉnh lịch lặp/cả ngày trực tiếp trên Google Calendar.", 409);
        if (keep == "google" && !(await ConflictsAsync(userId, ct)).Any(x => x.Id == id && x.CanKeepGoogle))
            throw new AuthException("CALENDAR_EXECUTION_PROTECTED", "Phiên đã có ghi nhận thực hiện. Hãy giữ PLANORA để bảo toàn lịch sử phiên.", 409);
        link.Resolution = keep; db.CalendarSyncOutbox.Add(new() { UserId = userId, Kind = link.Kind, LocalId = link.LocalId });
        await db.SaveChangesAsync(ct); await RequestSyncAsync(userId, ct);
    }
    public async Task ReceiveWebhookAsync(string channelId, string resourceId, string token, CancellationToken ct)
    {
        if (!Options.Configured || token.Length is < 32 or > 256 || !Guid.TryParseExact(channelId, "N", out var id)) return;
        var channel = await db.CalendarWatchChannels.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.ExpiresAt > Now, ct);
        if (channel is null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(channel.TokenHash), Encoding.ASCII.GetBytes(Hash(token)))) return;
        // A Pending watch can receive its initial sync before watch() returns.
        if (channel.ResourceId.Length > 0 && channel.ResourceId != resourceId) return;
        await db.GoogleCalendarConnections.Where(x => x.Id == channel.ConnectionId && x.Status == "Connected")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextSyncAt, Now), ct);
    }
}
