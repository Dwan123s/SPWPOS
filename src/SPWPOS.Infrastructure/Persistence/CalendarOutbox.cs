using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Persistence;

public sealed partial class ApplicationDbContext
{
    public DbSet<GoogleCalendarConnection> GoogleCalendarConnections => Set<GoogleCalendarConnection>();
    public DbSet<CalendarOAuthRequest> CalendarOAuthRequests => Set<CalendarOAuthRequest>();
    public DbSet<CalendarEventLink> CalendarEventLinks => Set<CalendarEventLink>();
    public DbSet<CalendarSyncOutbox> CalendarSyncOutbox => Set<CalendarSyncOutbox>();
    public DbSet<CalendarWatchChannel> CalendarWatchChannels => Set<CalendarWatchChannel>();
    public bool SuppressCalendarOutbox { get; set; }

    private void CaptureCalendarChanges()
    {
        if (SuppressCalendarOutbox) return;
        // These rows share the same SaveChanges transaction as the originating
        // mutation. They contain identifiers only, never descriptions or tokens.
        var changes = ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(x => x.Entity is FixedEvent or WorkSession || x.Entity is WorkTask &&
                (x.State == EntityState.Deleted || x.Property(nameof(WorkTask.Name)).IsModified))
            .Select(x => (OwnedRecord)x.Entity).ToArray();
        if (changes.Length == 0) return;
        var userIds = changes.Select(x => x.UserId).Distinct().ToArray();
        var connected = GoogleCalendarConnections.AsNoTracking().Where(x => userIds.Contains(x.UserId) && x.Status != "Disconnected")
            .Select(x => x.UserId).ToHashSet();
        foreach (var row in changes)
        {
            if (!connected.Contains(row.UserId)) continue;
            var kind = row is FixedEvent ? "Event" : row is WorkSession ? "Session" : "Task";
            if (!CalendarSyncOutbox.Local.Any(x => Entry(x).State == EntityState.Added && x.UserId == row.UserId && x.Kind == kind && x.LocalId == row.Id))
                CalendarSyncOutbox.Add(new() { UserId = row.UserId, Kind = kind, LocalId = row.Id });
        }
    }
}
