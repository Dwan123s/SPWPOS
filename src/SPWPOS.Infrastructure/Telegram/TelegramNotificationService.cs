using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Telegram;

public sealed class TelegramNotificationService(ApplicationDbContext db, ITelegramGateway gateway,
    IOptions<TelegramOptions> options, TimeProvider clock) : ITelegramNotificationService
{
    private DateTimeOffset Now => clock.GetUtcNow();
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private async Task<string> Address(Guid uid, CancellationToken ct) =>
        await db.Users.Where(x => x.Id == uid && x.IsActive && x.EmailConfirmed).Select(x => x.NormalizedEmail).SingleOrDefaultAsync(ct)
        ?? throw new AuthException("FORBIDDEN", "Tài khoản không khả dụng.", 403);
    private void Configured()
    {
        if (!options.Value.Configured) throw new AuthException("TELEGRAM_NOT_CONFIGURED", "Máy chủ chưa bật kết nối Telegram.", 503);
    }
    public async Task<TelegramStatus> StatusAsync(Guid uid, CancellationToken ct)
    {
        var c = await db.TelegramConnections.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == uid, ct);
        return new(options.Value.Configured, c?.ChatId is not null, c?.Enabled == true, c?.DisplayName, c?.ConnectedAt,
            c?.LinkExpiresAt > Now ? c.LinkExpiresAt : null, c?.ErrorCode, options.Value.ConfigurationError);
    }
    public async Task<TelegramLink> ConnectAsync(Guid uid, CancellationToken ct)
    {
        Configured();
        string username;
        try { username = await gateway.BotUsernameAsync(ct); }
        catch (TelegramDeliveryException e) when (e.Code == 409) { throw new AuthException("TELEGRAM_WEBHOOK_ACTIVE", "Bot đang dùng webhook của dịch vụ khác. Quản trị viên cần cấu hình bot riêng cho PLANORA.", 409); }
        catch (TelegramDeliveryException) { throw new AuthException("TELEGRAM_UNAVAILABLE", "Chưa kết nối được bot Telegram. Hãy thử lại sau.", 503); }
        await using var tx = await AccountTransaction.BeginAsync(db, await Address(uid, ct), ct);
        var c = await db.TelegramConnections.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        if (c is null) { c = new() { UserId = uid }; db.TelegramConnections.Add(c); }
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        c.LinkTokenHash = Hash(token); c.LinkExpiresAt = Now.AddMinutes(10); c.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new($"https://t.me/{username}?start={token}", c.LinkExpiresAt.Value);
    }
    public async Task SetEnabledAsync(Guid uid, bool enabled, CancellationToken ct)
    {
        if (enabled) Configured();
        await using var tx = await AccountTransaction.BeginAsync(db, await Address(uid, ct), ct);
        var c = await db.TelegramConnections.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        if (c?.ChatId is null || enabled && c.ErrorCode is not null)
            throw new AuthException("TELEGRAM_NOT_CONNECTED", "Hãy kết nối lại Telegram và nhấn Start trước.", 409);
        c.Enabled = enabled; c.Version = Guid.NewGuid();
        if (!enabled) await CancelPending(uid, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task DisconnectAsync(Guid uid, CancellationToken ct)
    {
        await using var tx = await AccountTransaction.BeginAsync(db, await Address(uid, ct), ct);
        var c = await db.TelegramConnections.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        if (c is not null) Clear(c);
        await CancelPending(uid, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private static void Clear(TelegramConnection c)
    {
        c.ChatId = null; c.DisplayName = null; c.ConnectedAt = null; c.Enabled = false;
        c.LinkTokenHash = null; c.LinkExpiresAt = null; c.ErrorCode = null; c.BindingId = Guid.NewGuid(); c.Version = Guid.NewGuid();
    }
    private async Task CancelPending(Guid uid, CancellationToken ct)
    {
        foreach (var n in await db.InAppNotifications.Where(x => x.UserId == uid && x.TelegramPending).ToArrayAsync(ct)) n.TelegramPending = false;
    }
    public async Task ProcessUpdateAsync(TelegramUpdate update, CancellationToken ct)
    {
        if (update.ChatType != "private" || update.ChatId <= 0 || update.ChatId != update.SenderId) return;
        var parts = update.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var stop = parts.Length == 1 && parts[0] == "/stop";
        var start = parts.Length == 2 && parts[0] == "/start" && parts[1].Length == 43;
        if (!stop && !start) return;
        var hash = start ? Hash(parts[1]) : "";
        var candidate = await db.TelegramConnections.AsNoTracking().SingleOrDefaultAsync(
            x => stop ? x.ChatId == update.ChatId : x.LinkTokenHash == hash, ct);
        if (candidate is null) return;
        var address = await db.Users.Where(x => x.Id == candidate.UserId && (stop || x.IsActive && x.EmailConfirmed)).Select(x => x.NormalizedEmail).SingleOrDefaultAsync(ct);
        if (address is null) return;
        await using var tx = await AccountTransaction.BeginAsync(db, address, ct);
        var c = await db.TelegramConnections.SingleAsync(x => x.Id == candidate.Id, ct);
        if (stop)
        {
            if (c.ChatId != update.ChatId) return;
            Clear(c);
        }
        else
        {
            if (c.LinkTokenHash != hash || c.LinkExpiresAt <= Now || c.LinkExpiresAt is null) return;
            if (await db.TelegramConnections.AnyAsync(x => x.ChatId == update.ChatId && x.Id != c.Id, ct)) return;
            c.ChatId = update.ChatId; c.DisplayName = update.DisplayName; c.Enabled = true; c.ConnectedAt = Now;
            c.BindingId = Guid.NewGuid(); c.Version = Guid.NewGuid(); c.LinkTokenHash = null; c.LinkExpiresAt = null; c.ErrorCode = null;
        }
        await CancelPending(c.UserId, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task SendPendingAsync(Guid uid, CancellationToken ct)
    {
        if (!options.Value.Configured) return;
        var address = await db.Users.Where(x => x.Id == uid).Select(x => x.NormalizedEmail).SingleOrDefaultAsync(ct);
        if (address is null) return;
        await using var tx = await AccountTransaction.BeginAsync(db, address, ct);
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == uid, ct);
        var c = await db.TelegramConnections.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        var zone = await db.WorkPreferences.AsNoTracking().Where(x => x.UserId == uid).Select(x => x.TimeZoneId).SingleOrDefaultAsync(ct) ?? "Asia/Ho_Chi_Minh";
        var now = Now;
        var rows = await db.InAppNotifications.Where(x => x.UserId == uid && x.TelegramPending && x.TelegramSentAt == null &&
            x.NextTelegramAttemptAt <= now).OrderBy(x => x.CreatedAt).Take(20).ToArrayAsync(ct);
        foreach (var n in rows)
        {
            if (!user.IsActive || !user.EmailConfirmed || c is not { Enabled: true, ChatId: not null } || n.TelegramBindingId != c.BindingId)
            { n.TelegramPending = false; continue; }
            var details = await CurrentAsync(uid, n, now, ct);
            if (details is null) { n.TelegramPending = false; continue; }
            n.TelegramAttempts++;
            try
            {
                await gateway.SendAsync(c.ChatId.Value, TelegramMessageFormatter.Format(n, details, zone, now), ct);
                n.TelegramSentAt = Now; n.TelegramPending = false; c.ErrorCode = null;
            }
            catch (TelegramDeliveryException ex)
            {
                if (ex.Code is 400 or 401 or 403)
                {
                    c.ErrorCode = ex.Code == 403 ? "BOT_BLOCKED" : "DELIVERY_REJECTED"; c.Enabled = false;
                    c.Version = Guid.NewGuid(); await CancelPending(uid, ct); break;
                }
                n.NextTelegramAttemptAt = now.AddSeconds(Math.Clamp(Math.Max(ex.RetryAfterSeconds,
                    30 * Math.Pow(2, Math.Min(n.TelegramAttempts, 7))), 30, 86400));
            }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task<TelegramReminderDetails?> CurrentAsync(Guid uid, InAppNotification n, DateTimeOffset now, CancellationToken ct)
    {
        if (n.Kind is "EVENT_REMINDER" or "EVENT_STARTED")
        {
            var e = await db.FixedEvents.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == uid && x.Id == n.FixedEventId, ct);
            var started = n.Kind == "EVENT_STARTED";
            return e is not null && e.End > now &&
                (started ? e.Start <= now && e.Start >= now.AddMinutes(-2) : e.Start > now) &&
                n.DedupeKey == $"{(started ? "event-started" : "event")}:{e.Id}:{e.Start.UtcTicks}"
                ? new(e.Name, e.Start, e.End) : null;
        }
        if (n.Kind is "DEADLINE" or "SESSION_REMINDER" or "SESSION_REVIEW" or "SESSION_STARTED")
        {
            var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == uid && x.Id == n.TaskId, ct);
            if (task is null || task.IsDeleted || task.Status is WorkTaskStatus.Completed or WorkTaskStatus.Cancelled) return null;
            if (n.Kind == "DEADLINE") return task.Deadline > now && n.DedupeKey == $"deadline:{task.Id}:{task.Deadline.UtcTicks}"
                ? new(task.Name, Deadline: task.Deadline) : null;
            var s = await db.WorkSessions.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == uid && x.Id == n.SessionId, ct);
            var current = s is not null && (n.Kind == "SESSION_REMINDER"
                ? s.Status == WorkSessionStatus.Scheduled && s.Start > now && n.DedupeKey == $"session:{s.Id}:{s.Start.UtcTicks}"
                : n.Kind == "SESSION_STARTED" ? (s.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running) &&
                    s.Start <= now && s.Start >= now.AddMinutes(-2) && s.End > now && n.DedupeKey == $"session-started:{s.Id}:{s.Start.UtcTicks}"
                : (s.Status is WorkSessionStatus.NeedsReview or WorkSessionStatus.Paused or WorkSessionStatus.Completed) && s.End<=now &&
                    n.CreatedAt > now.AddDays(-1) && n.DedupeKey == $"review:{s.Id}:{s.End.UtcTicks}");
            return current ? new(task.Name, s!.Start, s.End) : null;
        }
        return n.CreatedAt > now.AddDays(-1) ? new() : null;
    }
}
