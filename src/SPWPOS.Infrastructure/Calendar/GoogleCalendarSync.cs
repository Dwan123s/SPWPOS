using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Calendar;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Calendar;

public sealed partial class GoogleCalendarService
{
    public async Task<bool> SyncNextAsync(CancellationToken ct)
    {
        if (!Options.Configured) return false;
        var id = await db.GoogleCalendarConnections.AsNoTracking()
            .Where(x => x.Status == "Connected" && (x.LeaseUntil == null || x.LeaseUntil < Now) &&
                (x.NextSyncAt <= Now || x.FailureCount == 0 && db.CalendarSyncOutbox.Any(j => j.UserId == x.UserId)))
            .OrderBy(x => x.NextSyncAt).Select(x => (Guid?)x.UserId).FirstOrDefaultAsync(ct);
        if (id is null) return false;
        await SyncUserAsync(id.Value, ct); return true;
    }

    public async Task SyncUserAsync(Guid userId, CancellationToken ct)
    {
        if (!Options.Configured) return;
        db.ChangeTracker.Clear(); db.SuppressCalendarOutbox = true;
        var lease = Guid.NewGuid(); var until = Now.AddMinutes(10);
        var claimed = await db.GoogleCalendarConnections.Where(x => x.UserId == userId && x.Status == "Connected" &&
            (x.LeaseUntil == null || x.LeaseUntil < Now)).ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntil, until).SetProperty(x => x.LeaseId, lease), ct);
        if (claimed != 1) return;
        var c = await db.GoogleCalendarConnections.SingleAsync(x => x.UserId == userId, ct);
        try
        {
            var access = await gateway.RefreshAsync(Unprotect(c), ct);
            if (c.CalendarId.Length == 0)
            {
                // Calendar insertion has no caller-defined ID. Persist a setup
                // state before sending, so an ambiguous timeout is not retried
                // automatically and cannot silently create many calendars.
                c.Status = "SettingUp"; c.LastError = "Đang tạo lịch PLANORA trên Google."; await db.SaveChangesAsync(ct);
                c.CalendarId = await gateway.CreateCalendarAsync(access, ct);
                c.Status = "Connected"; c.LastError = ""; await db.SaveChangesAsync(ct);
            }
            if (c.SyncToken is null) await QueueInitialAsync(c, ct);
            await PullAsync(c, access, ct);
            await RefreshSeriesAsync(c, access, ct);
            var rows = await db.CalendarSyncOutbox.Where(x => x.UserId == userId).OrderBy(x => x.CreatedAt).Take(500).ToArrayAsync(ct);
            var targets = rows.Where(x => x.Kind != "Task").Select(x => (x.Kind, x.LocalId)).ToHashSet();
            var taskIds = rows.Where(x => x.Kind == "Task").Select(x => x.LocalId).ToArray();
            if (taskIds.Length > 0)
                foreach (var id in await db.WorkSessions.AsNoTracking().Where(x => x.UserId == userId && taskIds.Contains(x.TaskId)).Select(x => x.Id).ToArrayAsync(ct))
                    targets.Add(("Session", id));
            foreach (var (kind, localId) in targets)
            {
                await EnsureActiveAsync(c, ct);
                var link = await db.CalendarEventLinks.SingleOrDefaultAsync(x => x.ConnectionId == c.Id && x.Kind == kind && x.LocalId == localId, ct);
                if (link is null)
                {
                    var candidate = new CalendarEventLink { UserId = userId, ConnectionId = c.Id, Kind = kind, LocalId = localId };
                    var snapshot = await LocalAsync(candidate, ct);
                    if (snapshot is null || snapshot.End <= Now) continue;
                    candidate.GoogleEventId = "p" + candidate.Id.ToString("N");
                    db.CalendarEventLinks.Add(candidate); await db.SaveChangesAsync(ct); link = candidate;
                }
                await PushAsync(c, access, link, ct);
            }
            // Acknowledge only the exact rows observed in this batch. A local
            // mutation arriving during network IO leaves its own durable row.
            db.CalendarSyncOutbox.RemoveRange(rows); await db.SaveChangesAsync(ct);
            await MaintainWatchAsync(c, access, ct);
            c.LastSyncedAt = Now; c.LastError = ""; c.FailureCount = 0;
            c.NextSyncAt = Now.AddSeconds(Options.PollSeconds + Random.Shared.Next(0, 10));
            await db.SaveChangesAsync(ct);
        }
        catch (CalendarProviderException error)
        {
            c.FailureCount++; c.NextSyncAt = Now.AddSeconds(Math.Min(3600, 15 * Math.Pow(2, Math.Min(c.FailureCount, 8))) + Random.Shared.Next(1, 15));
            if (error.Reconnect) { c.Status = "Reconnect"; c.LastError = "Google đã hết hoặc thu hồi quyền truy cập. Hãy kết nối lại."; }
            else if (c.Status == "SettingUp") c.LastError = "Chưa xác nhận được lịch mới trên Google. Kiểm tra Google Calendar trước khi kết nối lại để tránh tạo lịch trùng.";
            else if (error.Status == 403 && error.Reason is not "rateLimitExceeded" and not "userRateLimitExceeded")
            { c.Status = "Reconnect"; c.LastError = "Google từ chối quyền Calendar. Kiểm tra API/quyền truy cập rồi kết nối lại."; }
            else if (error.Status == 404) { c.Status = "Reconnect"; c.LastError = "Không tìm thấy lịch PLANORA trên Google. Kiểm tra lịch và quyền truy cập."; }
            else c.LastError = "Google Calendar tạm thời chưa đồng bộ được. Hệ thống sẽ thử lại; thay đổi của bạn vẫn được giữ.";
            await db.SaveChangesAsync(ct);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            c.Status = "Reconnect"; c.LastError = "Không mở được khóa kết nối. Kiểm tra kho khóa máy chủ và kết nối lại."; await db.SaveChangesAsync(ct);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            var next = Now.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(c.FailureCount, 7))) + Random.Shared.Next(1, 15));
            var message = c.Status == "SettingUp" ? "Chưa xác nhận được lịch mới trên Google. Kiểm tra Google Calendar trước khi kết nối lại để tránh tạo lịch trùng." :
                "Mất kết nối tới Google. Thay đổi vẫn được giữ và sẽ tự gửi lại.";
            db.ChangeTracker.Clear();
            await db.GoogleCalendarConnections.Where(x => x.Id == c.Id && x.Version == c.Version)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextSyncAt, next).SetProperty(x => x.LastError, message)
                    .SetProperty(x => x.FailureCount, x => x.FailureCount + 1), CancellationToken.None);
        }
        finally
        {
            // Use a separate command even after cancellation/concurrency failure.
            await db.GoogleCalendarConnections.Where(x => x.UserId == userId && x.LeaseId == lease)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null), CancellationToken.None);
            db.SuppressCalendarOutbox = false;
        }
    }

    private async Task EnsureActiveAsync(GoogleCalendarConnection c, CancellationToken ct)
    {
        if (!await db.GoogleCalendarConnections.AsNoTracking().AnyAsync(x => x.Id == c.Id && x.Status == "Connected" && x.Version == c.Version && x.LeaseId == c.LeaseId, ct))
            throw new DbUpdateConcurrencyException("Calendar connection changed during sync.");
    }
    private async Task PullAsync(GoogleCalendarConnection c, string access, CancellationToken ct)
    {
        var token = c.SyncToken; string? page = null; var seen = new HashSet<string>();
        while (true)
        {
            CalendarPage result;
            try { result = await gateway.ListAsync(access, c.CalendarId, token, page, ct); }
            catch (CalendarProviderException error) when (error.Status == 410 && token is not null)
            { token = null; page = null; seen.Clear(); c.SyncToken = null; await db.SaveChangesAsync(ct); continue; }
            foreach (var item in result.Items)
            {
                seen.Add(item.Id); await EnsureActiveAsync(c, ct);
                if (item.IsSeries) await StoreSeriesAsync(c, item, ct);
                else await ReconcileAsync(c, item, ct);
            }
            page = result.PageToken;
            if (page is null)
            {
                if (result.SyncToken is null) throw new CalendarProviderException(502, "missing_sync_token");
                if (token is null)
                {
                    // Expired deletion tombstones may be absent in a full scan.
                    // Verify missing linked events individually, never wipe tasks.
                    var missing = await db.CalendarEventLinks.Where(x => x.ConnectionId == c.Id && x.BaseJson != "null" && !x.RemoteDeleted && x.SeriesId == null).ToArrayAsync(ct);
                    foreach (var link in missing.Where(x => !seen.Contains(x.GoogleEventId)))
                    {
                        var remote = await gateway.GetAsync(access, c.CalendarId, link.GoogleEventId, ct);
                        if (link.Kind == "Series") { if (remote is null || remote.Deleted) await DeleteSeriesAsync(c, link, ct); }
                        else await ReconcileAsync(c, remote ?? new(link.GoogleEventId, link.Etag, null, true), ct);
                    }
                }
                c.SyncToken = result.SyncToken; await db.SaveChangesAsync(ct);
                break;
            }
        }
    }
    private async Task StoreSeriesAsync(GoogleCalendarConnection c, CalendarRemoteEvent remote, CancellationToken ct)
    {
        var link = await db.CalendarEventLinks.SingleOrDefaultAsync(x => x.ConnectionId == c.Id && x.GoogleEventId == remote.Id, ct);
        if (link is not null && link.Kind != "Series")
        {
            // Converting an already linked session into a whole series must not
            // destroy the original task mapping.
            link.ReadOnly = true; link.ConflictJson = Serialize(remote); link.Resolution = null;
            await db.SaveChangesAsync(ct); return;
        }
        if (link is null) { link = new() { UserId = c.UserId, ConnectionId = c.Id, Kind = "Series", LocalId = Guid.NewGuid(), GoogleEventId = remote.Id }; db.CalendarEventLinks.Add(link); }
        link.ReadOnly = true; link.RemoteDeleted = false; link.Etag = remote.Etag; link.BaseJson = "{}";
        await db.SaveChangesAsync(ct);
    }
    private async Task DeleteSeriesAsync(GoogleCalendarConnection c, CalendarEventLink master, CancellationToken ct)
    {
        foreach (var instance in await db.CalendarEventLinks.Where(x => x.ConnectionId == c.Id && x.SeriesId == master.GoogleEventId && !x.RemoteDeleted).ToArrayAsync(ct))
            await ReconcileAsync(c, new(instance.GoogleEventId, instance.Etag, null, true, true, master.GoogleEventId), ct);
        master.RemoteDeleted = true; await db.SaveChangesAsync(ct);
    }
    private async Task RefreshSeriesAsync(GoogleCalendarConnection c, string access, CancellationToken ct)
    {
        var days = await db.WorkPreferences.Where(x => x.UserId == c.UserId).Select(x => (int?)x.PlanningHorizonDays).SingleOrDefaultAsync(ct) ?? 14;
        var end = Now.AddDays(Math.Max(90, days + 1));
        foreach (var series in await db.CalendarEventLinks.Where(x => x.ConnectionId == c.Id && x.Kind == "Series" && !x.RemoteDeleted).ToArrayAsync(ct))
        {
            IReadOnlyList<CalendarRemoteEvent> instances;
            try { instances = await gateway.InstancesAsync(access, c.CalendarId, series.GoogleEventId, Now.AddDays(-1), end, ct); }
            catch (CalendarProviderException error) when (error.Status is 404 or 410) { await DeleteSeriesAsync(c, series, ct); continue; }
            foreach (var instance in instances) await ReconcileAsync(c, instance, ct);
            var ids = instances.Select(x => x.Id).ToHashSet();
            var old = await db.CalendarEventLinks.Where(x => x.ConnectionId == c.Id && x.SeriesId == series.GoogleEventId && !x.RemoteDeleted).ToArrayAsync(ct);
            foreach (var link in old.Where(x => !ids.Contains(x.GoogleEventId)))
            {
                var baseline = Deserialize<CalendarSnapshot>(link.BaseJson);
                if (baseline is not null && baseline.End > Now.AddDays(-1) && baseline.Start < end)
                    await ReconcileAsync(c, new(link.GoogleEventId, link.Etag, null, true, true, series.GoogleEventId), ct);
            }
        }
    }
    private async Task PushAsync(GoogleCalendarConnection c, string access, CalendarEventLink link, CancellationToken ct)
    {
        if (link.Kind == "Series") return;
        var remote = await gateway.GetAsync(access, c.CalendarId, link.GoogleEventId, ct);
        var missingEtag = Deserialize<CalendarRemoteEvent>(link.ConflictJson) is { Deleted: true } conflict ? conflict.Etag : link.Etag;
        await ReconcileAsync(c, remote ?? new(link.GoogleEventId, missingEtag, null, true, link.ReadOnly, link.SeriesId), ct);
        if (link.ConflictJson is not null || link.ReadOnly) return;
        var local = await LocalAsync(link, ct); var baseline = Deserialize<CalendarSnapshot>(link.BaseJson);
        if (local == baseline) return;
        await EnsureActiveAsync(c, ct);
        try
        {
            if (local is null)
            {
                if (link.Etag.Length > 0) await gateway.DeleteAsync(access, c.CalendarId, link.GoogleEventId, link.Etag, ct);
                link.RemoteDeleted = true; link.BaseJson = "null";
            }
            else
            {
                var created = link.Etag.Length == 0;
                var updated = await gateway.WriteAsync(access, c.CalendarId, link.GoogleEventId, local, created ? null : link.Etag,
                    created ? new(c.ReminderMinutes, c.RemindAtStart) : null, ct);
                link.Etag = updated.Etag; link.BaseJson = Serialize(local); link.RemoteDeleted = false;
            }
            await db.SaveChangesAsync(ct);
        }
        catch (CalendarProviderException error) when (error.Status is 409 or 412)
        {
            var latest = await gateway.GetAsync(access, c.CalendarId, link.GoogleEventId, ct);
            await ReconcileAsync(c, latest ?? new(link.GoogleEventId, link.Etag, null, true), ct);
            // Keep this batch durable even if reconciliation only refreshed etag.
            throw new CalendarProviderException(429, "concurrent_update");
        }
    }

    private async Task MaintainWatchAsync(GoogleCalendarConnection c, string access, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Options.WebhookUrl)) return;
        var channels = await db.CalendarWatchChannels.Where(x => x.ConnectionId == c.Id).ToArrayAsync(ct);
        if (!channels.Any(x => x.ResourceId.Length > 0 && x.ExpiresAt > Now.AddHours(12)))
        {
            var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            var channel = new CalendarWatchChannel { UserId = c.UserId, ConnectionId = c.Id, TokenHash = Hash(token), ExpiresAt = Now.AddMinutes(10) };
            db.CalendarWatchChannels.Add(channel); await db.SaveChangesAsync(ct);
            var watch = await gateway.WatchAsync(access, c.CalendarId, channel.Id.ToString("N"), token, ct);
            channel.ResourceId = watch.ResourceId; channel.ExpiresAt = watch.Expiration; await db.SaveChangesAsync(ct);
            foreach (var old in channels)
            {
                try { if (old.ResourceId.Length > 0) await gateway.StopWatchAsync(access, old.Id.ToString("N"), old.ResourceId, ct); }
                catch (CalendarProviderException) { }
                db.CalendarWatchChannels.Remove(old);
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
