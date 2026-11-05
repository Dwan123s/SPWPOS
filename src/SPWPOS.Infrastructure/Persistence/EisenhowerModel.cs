using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Persistence;

public sealed partial class ApplicationDbContext
{
    public DbSet<PriorityAlertState> PriorityAlertStates => Set<PriorityAlertState>();
}
internal static class EisenhowerModel
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<WorkTask>().Property(x => x.ImportanceLevel).HasMaxLength(12).HasDefaultValue("Unknown");
        b.Entity<WorkTask>().Property(x => x.ImportanceSource).HasMaxLength(12).HasDefaultValue("Unknown");
        b.Entity<WorkTask>().Property(x => x.ImportanceReason).HasMaxLength(1000).HasDefaultValue("");
        b.Entity<WorkTask>().Ignore(x => x.EffectiveImportance);
        b.Entity<WorkTask>().Property(x => x.UrgencyOverride).HasMaxLength(12).HasDefaultValue("Auto");
        b.Entity<WorkTask>().Property(x => x.ChecklistJson).HasMaxLength(40000).HasDefaultValue("[]");
        b.Entity<TeamTask>().Property(x => x.ImportanceLevel).HasMaxLength(12).HasDefaultValue("Unknown");
        b.Entity<TeamTask>().Property(x => x.ImportanceSource).HasMaxLength(12).HasDefaultValue("Unknown");
        b.Entity<TeamTask>().Property(x => x.ImportanceReason).HasMaxLength(1000).HasDefaultValue("");
        b.Entity<PriorityAlertState>(e => {
            e.HasKey(x => x.Id); e.HasIndex(x => new { x.UserId, x.TaskId }).IsUnique();
            e.Property(x => x.LastSignal).HasMaxLength(20);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
