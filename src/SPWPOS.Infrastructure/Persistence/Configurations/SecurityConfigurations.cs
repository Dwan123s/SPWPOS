using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Email).HasMaxLength(254).IsRequired();
        b.Property(x => x.NormalizedEmail).HasMaxLength(254).IsRequired();
        b.HasIndex(x => x.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
    }
}
public sealed class OtpConfiguration : IEntityTypeConfiguration<EmailOtp>
{
    public void Configure(EntityTypeBuilder<EmailOtp> b)
    {
        b.Property(x => x.OtpHash).HasMaxLength(32).IsRequired();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.Purpose, x.CreatedAt });
        b.HasIndex(x => new { x.UserId, x.Purpose }).IsUnique().HasFilter("[IsUsed] = 0");
        b.HasIndex(x => x.ExpiresAt);
    }
}
public sealed class RefreshConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.DeviceInfo).HasMaxLength(256);
        b.Property(x => x.IpAddress).HasMaxLength(45);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.FamilyId, x.RevokedAt });
        b.HasIndex(x => x.ExpiresAt);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ReplacedByTokenId).OnDelete(DeleteBehavior.NoAction);
    }
}
public sealed class TicketConfiguration : IEntityTypeConfiguration<PasswordResetTicket>
{
    public void Configure(EntityTypeBuilder<PasswordResetTicket> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.ExpiresAt });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class AuditConfiguration : IEntityTypeConfiguration<SecurityAuditLog>
{
    public void Configure(EntityTypeBuilder<SecurityAuditLog> b)
    {
        b.Property(x => x.IpAddress).HasMaxLength(45);
        b.Property(x => x.UserAgent).HasMaxLength(256);
        b.Property(x => x.Metadata).HasMaxLength(100);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => x.CreatedAt);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
public sealed class RateConfiguration : IEntityTypeConfiguration<SecurityRateLimit>
{
    public void Configure(EntityTypeBuilder<SecurityRateLimit> b)
    {
        b.Property(x => x.Id).HasMaxLength(80);
        b.HasIndex(x => x.LastRequestAt);
    }
}
public sealed class NotificationConfiguration : IEntityTypeConfiguration<PasswordChangeNotification>
{
    public void Configure(EntityTypeBuilder<PasswordChangeNotification> b)
    {
        b.HasIndex(x => new { x.SentAt, x.NextAttemptAt });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
