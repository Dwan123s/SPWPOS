using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Persistence;

public static class TelegramModel
{
    public static void Configure(ModelBuilder b)
    {
        var connection = b.Entity<TelegramConnection>();
        connection.HasKey(x => x.Id);
        connection.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        connection.HasIndex(x => x.UserId).IsUnique();
        connection.HasIndex(x => x.ChatId).IsUnique().HasFilter("[ChatId] IS NOT NULL");
        connection.HasIndex(x => x.LinkTokenHash).IsUnique().HasFilter("[LinkTokenHash] IS NOT NULL");
        connection.Property(x => x.LinkTokenHash).HasMaxLength(64);
        connection.Property(x => x.DisplayName).HasMaxLength(120);
        connection.Property(x => x.ErrorCode).HasMaxLength(40);
        connection.Property(x => x.Version).IsConcurrencyToken();
        var cursor = b.Entity<TelegramUpdateCursor>();
        cursor.HasKey(x => x.Id); cursor.Property(x => x.Id).HasMaxLength(64);
        b.Entity<InAppNotification>().HasIndex(x => new { x.UserId, x.TelegramPending, x.NextTelegramAttemptAt });
    }
}
