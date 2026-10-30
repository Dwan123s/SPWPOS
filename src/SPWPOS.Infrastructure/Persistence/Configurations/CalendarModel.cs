using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Persistence;

public static class CalendarModel
{
    public static void Configure(ModelBuilder b)
    {
        var c = b.Entity<GoogleCalendarConnection>();
        c.HasKey(x => x.Id); c.HasAlternateKey(x => new { x.UserId, x.Id });
        c.HasIndex(x => x.UserId).IsUnique();
        c.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        c.Property(x => x.GoogleSubject).HasMaxLength(255); c.Property(x => x.Email).HasMaxLength(320);
        c.Property(x => x.CalendarId).HasMaxLength(255); c.Property(x => x.Status).HasMaxLength(32);
        c.Property(x => x.LastError).HasMaxLength(500); c.Property(x => x.Version).IsConcurrencyToken();
        c.HasIndex(x => new { x.Status, x.NextSyncAt });
        var oauth = b.Entity<CalendarOAuthRequest>(); oauth.HasKey(x => x.Id);
        oauth.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        oauth.Property(x => x.StateHash).HasMaxLength(64); oauth.HasIndex(x => x.StateHash).IsUnique(); oauth.HasIndex(x => x.ExpiresAt);
        var link = b.Entity<CalendarEventLink>(); link.HasKey(x => x.Id);
        link.HasOne<GoogleCalendarConnection>().WithMany().HasForeignKey(x => new { x.UserId, x.ConnectionId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        link.Property(x => x.Kind).HasMaxLength(16); link.Property(x => x.GoogleEventId).HasMaxLength(255);
        link.Property(x => x.Etag).HasMaxLength(255); link.Property(x => x.Resolution).HasMaxLength(16);
        link.Property(x => x.SeriesId).HasMaxLength(255);
        link.HasIndex(x => new { x.ConnectionId, x.GoogleEventId }).IsUnique();
        link.HasIndex(x => new { x.ConnectionId, x.Kind, x.LocalId }).IsUnique();
        var job = b.Entity<CalendarSyncOutbox>(); job.HasKey(x => x.Id); job.Property(x => x.Kind).HasMaxLength(16);
        job.HasIndex(x => new { x.UserId, x.CreatedAt });
        job.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        var watch = b.Entity<CalendarWatchChannel>(); watch.HasKey(x => x.Id); watch.Property(x => x.ResourceId).HasMaxLength(255);
        watch.Property(x => x.TokenHash).HasMaxLength(64); watch.HasIndex(x => x.ExpiresAt);
        watch.HasOne<GoogleCalendarConnection>().WithMany().HasForeignKey(x => new { x.UserId, x.ConnectionId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}
