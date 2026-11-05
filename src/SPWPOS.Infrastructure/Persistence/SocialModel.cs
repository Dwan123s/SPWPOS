using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Persistence;

public sealed partial class ApplicationDbContext
{
    public DbSet<Friendship> Friendships => Set<Friendship>();
}
public static class SocialModel
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<UserProfile>().Property(x => x.CoverUrl).HasMaxLength(3000000);
        var f = b.Entity<Friendship>();
        f.HasKey(x => new { x.UserLowId, x.UserHighId });
        f.ToTable("Friendships", t => {
            t.HasCheckConstraint("CK_Friendships_Pair", "[UserLowId] <> [UserHighId]");
            t.HasCheckConstraint("CK_Friendships_Requester", "[RequestedById] = [UserLowId] OR [RequestedById] = [UserHighId]");
            t.HasCheckConstraint("CK_Friendships_Status", "[Status] IN ('Pending', 'Accepted')");
        });
        f.Property(x => x.Status).HasMaxLength(12);
        f.Property(x => x.Version).IsConcurrencyToken();
        f.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserLowId).OnDelete(DeleteBehavior.Restrict);
        f.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserHighId).OnDelete(DeleteBehavior.Restrict);
        f.HasIndex(x => new { x.UserHighId, x.Status });
        f.HasIndex(x => new { x.UserLowId, x.Status });
    }
}
