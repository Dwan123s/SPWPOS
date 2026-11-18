using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Persistence;

public sealed partial class ApplicationDbContext
{
    public DbSet<TeamFriendInvitation> TeamFriendInvitations => Set<TeamFriendInvitation>();
}

internal sealed class TeamFriendInvitationModel : IEntityTypeConfiguration<TeamFriendInvitation>
{
    public void Configure(EntityTypeBuilder<TeamFriendInvitation> e)
    {
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TeamId, x.RecipientId }).IsUnique();
        e.HasIndex(x => new { x.RecipientId, x.Status, x.ExpiresAt });
        e.Property(x => x.Status).HasMaxLength(16);
        e.Property(x => x.Version).IsConcurrencyToken();
        e.HasOne<TeamProject>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
    }
}
