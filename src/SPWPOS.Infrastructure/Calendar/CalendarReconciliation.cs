using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Calendar;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Calendar;

public sealed partial class GoogleCalendarService
{
    private async Task<CalendarSnapshot?> LocalAsync(CalendarEventLink link, CancellationToken ct)
    {
        if (link.Kind == "Event")
            return await db.FixedEvents.AsNoTracking().Where(x => x.UserId == link.UserId && x.Id == link.LocalId)
                .Select(x => new CalendarSnapshot(x.Name, x.Start, x.End)).SingleOrDefaultAsync(ct);
        if (link.Kind != "Session") return null;
        return await (from session in db.WorkSessions.AsNoTracking()
                      join task in db.WorkTasks.AsNoTracking() on new { session.UserId, Id = session.TaskId } equals new { task.UserId, task.Id }
                      where session.UserId == link.UserId && session.Id == link.LocalId && session.Status != WorkSessionStatus.Cancelled && !task.IsDeleted
                      select new CalendarSnapshot(task.Name, session.Start, session.End)).SingleOrDefaultAsync(ct);
    }

    // Time is a single field group: merging independent start/end edits could
    // accidentally produce a negative or unintended duration.
    public static bool TryMerge(CalendarSnapshot? baseline, CalendarSnapshot? local, CalendarSnapshot? remote, out CalendarSnapshot? merged)
    {
        merged = null;
        if (local == remote) { merged = local; return true; }
        if (local == baseline) { merged = remote; return true; }
        if (remote == baseline) { merged = local; return true; }
        if (baseline is null || local is null || remote is null) return false;
        if (local.Title != baseline.Title && remote.Title != baseline.Title && local.Title != remote.Title) return false;
        var localTime = local.Start != baseline.Start || local.End != baseline.End;
        var remoteTime = remote.Start != baseline.Start || remote.End != baseline.End;
        if (localTime && remoteTime && (local.Start != remote.Start || local.End != remote.End)) return false;
        merged = new(local.Title != baseline.Title ? local.Title : remote.Title,
            localTime ? local.Start : remote.Start, localTime ? local.End : remote.End);
        return merged.End > merged.Start;
    }

    private async Task ReconcileAsync(GoogleCalendarConnection c, CalendarRemoteEvent remote, CancellationToken ct)
    {
        if (remote.IsSeries) { await StoreSeriesAsync(c, remote, ct); return; }
        var link = await db.CalendarEventLinks.SingleOrDefaultAsync(x => x.ConnectionId == c.Id && x.GoogleEventId == remote.Id, ct);
        if (link?.Kind == "Series") { if (remote.Deleted) await DeleteSeriesAsync(c, link, ct); return; }
        if (link is null && (remote.Deleted || remote.Snapshot is null || remote.Snapshot.End <= Now)) return;
        if (link is not null && link.ConflictJson is null && link.Resolution is null)
        {
            var baseline = Deserialize<CalendarSnapshot>(link.BaseJson);
            if (baseline == (remote.Deleted ? null : remote.Snapshot) && baseline == await LocalAsync(link, ct))
            {
                // Etag-only/reminder changes do not invalidate an otherwise
                // unchanged NORA proposal or alter workspace revision.
                link.Etag = remote.Etag; if (!remote.Deleted) link.ReadOnly = remote.ReadOnly;
                await db.SaveChangesAsync(ct); return;
            }
        }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await EnsureActiveAsync(c, ct);
        // Share the same optimistic workspace gate as normal task/event writes.
        // No Google request occurs while this short SQL transaction is open.
        var preference = await db.WorkPreferences.SingleOrDefaultAsync(x => x.UserId == c.UserId, ct);
        if (preference is null) { preference = new() { UserId = c.UserId }; db.WorkPreferences.Add(preference); }
        else await db.Entry(preference).ReloadAsync(ct);
        preference.MutationVersion = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        var workspaceChanged = false;
        if (link is null)
        {
            var localId = Guid.NewGuid();
            link = new() { UserId = c.UserId, ConnectionId = c.Id, LocalId = localId, GoogleEventId = remote.Id,
                Kind = "Event", Etag = remote.Etag, ReadOnly = remote.ReadOnly, SeriesId = remote.SeriesId };
            db.CalendarEventLinks.Add(link);
            await ApplyLocalAsync(link, remote.Snapshot, ct);
            workspaceChanged = true;
            link.BaseJson = Serialize(remote.Snapshot);
        }
        else
        {
            var baseline = Deserialize<CalendarSnapshot>(link.BaseJson);
            var local = await LocalAsync(link, ct);
            var google = remote.Deleted ? null : remote.Snapshot;
            var selected = Deserialize<CalendarRemoteEvent>(link.ConflictJson);
            // A decision applies to the remote version the user reviewed.
            // Another remote edit requires a new decision.
            var resolution = selected?.Etag == remote.Etag && selected?.Deleted == remote.Deleted ? link.Resolution : null;
            link.Resolution = null;
            if (!remote.Deleted) link.ReadOnly = remote.ReadOnly;
            link.SeriesId ??= remote.SeriesId;
            var protectedExecution = link.Kind == "Session" && (google is null || local?.Start != google.Start || local?.End != google.End) &&
                await db.WorkSessions.AnyAsync(x => x.UserId == c.UserId && x.Id == link.LocalId &&
                    (x.ActualSeconds > 0 || x.Status == WorkSessionStatus.Running || x.Status == WorkSessionStatus.Paused || x.Status == WorkSessionStatus.Completed), ct);
            var mergeable = TryMerge(baseline, local, google, out var merged);
            if (resolution == "local" && !link.ReadOnly) { merged = local; mergeable = true; }
            if (resolution == "google" && !protectedExecution) { merged = google; mergeable = true; }
            if (protectedExecution && google != local && google != baseline && resolution != "local" ||
                link.ReadOnly && local != baseline && local != google && resolution != "google" || !mergeable ||
                link.RemoteDeleted && local is not null && google is null && resolution != "local")
            {
                link.ConflictJson = Serialize(remote);
            }
            else
            {
                link.ConflictJson = null;
                if (merged != local) { await ApplyLocalAsync(link, merged, ct); workspaceChanged = true; }
                link.BaseJson = Serialize(google); link.Etag = remote.Etag;
                // A missing newly-created event is pending creation, whereas an
                // acknowledged remote deletion is a retained tombstone.
                link.RemoteDeleted = remote.Deleted && (baseline is not null || link.RemoteDeleted || merged is null);
                if (merged != google)
                {
                    if (remote.Deleted)
                    {
                        if (resolution == "local") link.GoogleEventId = "p" + Guid.NewGuid().ToString("N");
                        link.Etag = ""; link.RemoteDeleted = false;
                    }
                    if (!db.CalendarSyncOutbox.Local.Any(x => db.Entry(x).State == EntityState.Added && x.UserId == c.UserId && x.Kind == link.Kind && x.LocalId == link.LocalId) &&
                        !await db.CalendarSyncOutbox.AnyAsync(x => x.UserId == c.UserId && x.Kind == link.Kind && x.LocalId == link.LocalId, ct))
                        db.CalendarSyncOutbox.Add(new() { UserId = c.UserId, Kind = link.Kind, LocalId = link.LocalId });
                }
            }
        }
        if (workspaceChanged) preference.Revision = Guid.NewGuid();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task ApplyLocalAsync(CalendarEventLink link, CalendarSnapshot? snapshot, CancellationToken ct)
    {
        if (snapshot is not null && snapshot.End <= snapshot.Start) throw new CalendarProviderException(422, "invalid_event_time");
        if (link.Kind == "Event")
        {
            var item = await db.FixedEvents.SingleOrDefaultAsync(x => x.UserId == link.UserId && x.Id == link.LocalId, ct);
            if (item is not null) await db.Entry(item).ReloadAsync(ct);
            if (snapshot is null) { if (item is not null) db.FixedEvents.Remove(item); return; }
            if (item is null) { item = new() { Id = link.LocalId, UserId = link.UserId }; db.FixedEvents.Add(item); }
            item.Name = snapshot.Title; item.Start = snapshot.Start; item.End = snapshot.End;
            await WarnCalendarConflictsAsync(link, snapshot, false, ct);
            return;
        }
        var session = await db.WorkSessions.SingleOrDefaultAsync(x => x.UserId == link.UserId && x.Id == link.LocalId, ct);
        if (session is null) return;
        await db.Entry(session).ReloadAsync(ct);
        if (snapshot is null) { session.Status = WorkSessionStatus.Cancelled; session.UpdatedAt = Now; return; }
        session.Start = snapshot.Start; session.End = snapshot.End; session.IsLocked = true; session.UpdatedAt = Now;
        var task = await db.WorkTasks.SingleAsync(x => x.UserId == link.UserId && x.Id == session.TaskId, ct);
        await db.Entry(task).ReloadAsync(ct);
        if (task.Name != snapshot.Title)
        {
            task.Name = snapshot.Title; task.UpdatedAt = Now;
            db.CalendarSyncOutbox.Add(new() { UserId = link.UserId, Kind = "Task", LocalId = task.Id });
        }
        var outsideTaskWindow = task.IsHardDeadline && snapshot.End > task.Deadline ||
            task.WorkWindowStart is { } from && snapshot.Start < from || task.WorkWindowEnd is { } to && snapshot.End > to;
        await WarnCalendarConflictsAsync(link, snapshot, outsideTaskWindow, ct);
    }

    private async Task WarnCalendarConflictsAsync(CalendarEventLink link, CalendarSnapshot snapshot, bool outsideTaskWindow, CancellationToken ct)
    {
        if (snapshot.End <= Now) return;
        var overlaps = await db.FixedEvents.AnyAsync(x => x.UserId == link.UserId && x.Id != link.LocalId && x.Start < snapshot.End && x.End > snapshot.Start, ct) ||
            await db.WorkSessions.AnyAsync(x => x.UserId == link.UserId && x.Id != link.LocalId && x.Status != WorkSessionStatus.Cancelled && x.Start < snapshot.End && x.End > snapshot.Start, ct);
        if (!overlaps && !outsideTaskWindow) return;
        var key = "calendar-conflict:" + Hash(link.Id + Serialize(snapshot));
        if (await db.InAppNotifications.AnyAsync(x => x.UserId == link.UserId && x.DedupeKey == key, ct)) return;
        db.InAppNotifications.Add(new() { UserId = link.UserId, Kind = "CALENDAR_CONFLICT", Title = "Lịch Google cần bạn xem lại", CreatedAt = Now,
            Message = $"{snapshot.Title}: " + (outsideTaskWindow ? "giờ mới nằm ngoài hạn chót hoặc khung giờ công việc. " : "") +
                (overlaps ? "Khung giờ đang trùng với lịch khác. " : "") + "Mình đã giữ thay đổi từ Google; bạn có thể điều chỉnh phần việc linh hoạt.",
            DedupeKey = key });
    }
}
