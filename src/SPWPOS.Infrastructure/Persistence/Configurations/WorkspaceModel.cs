using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Persistence;

public static class WorkspaceModel
{
    private static EntityTypeBuilder<T> Owned<T>(ModelBuilder b) where T : OwnedRecord
    {
        var e = b.Entity<T>(); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.UserId, x.Id });
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.UserId); return e;
    }
    public static void Configure(ModelBuilder b)
    {
        var recoverySetting=Owned<RecoverySetting>(b);recoverySetting.HasIndex(x=>x.UserId).IsUnique();recoverySetting.Property(x=>x.Version).IsConcurrencyToken();
        var recovery=Owned<RecoveryCase>(b);recovery.Property(x=>x.DedupeKey).HasMaxLength(64);recovery.Property(x=>x.Status).HasMaxLength(24);recovery.Property(x=>x.Message).HasMaxLength(1000);
        recovery.HasIndex(x=>new{x.UserId,x.DedupeKey}).IsUnique();recovery.HasIndex(x=>new{x.UserId,x.CreatedAt});
        recovery.HasOne<PlanProposal>().WithMany().HasForeignKey(x=>new{x.UserId,x.PlanId}).HasPrincipalKey(x=>new{x.UserId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        var usage=Owned<AiUsageRecord>(b);
        usage.Property(x=>x.Operation).HasMaxLength(32);usage.Property(x=>x.Provider).HasMaxLength(32);
        usage.Property(x=>x.Model).HasMaxLength(120);usage.Property(x=>x.ModelVersion).HasMaxLength(120);usage.Property(x=>x.Status).HasMaxLength(16);
        usage.HasIndex(x=>new{x.UserId,x.ReservedAt});usage.HasIndex(x=>new{x.UserId,x.Status,x.LeaseUntil});
        var turn = Owned<AgentTurn>(b);
        turn.Property(x => x.InputFingerprint).HasMaxLength(64);
        turn.Property(x => x.Text).HasMaxLength(2000);
        turn.Property(x => x.Status).HasMaxLength(24);
        turn.HasIndex(x => new { x.UserId, x.RequestId }).IsUnique();
        turn.HasIndex(x => new { x.UserId, x.CreatedAt });
        turn.HasIndex(x => new { x.UserId, x.ConversationId, x.CreatedAt, x.Id });
        var conversation = b.Entity<AgentConversation>();
        conversation.HasKey(x => new { x.UserId, x.Id });
        conversation.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        conversation.Property(x => x.Title).HasMaxLength(120);
        conversation.Property(x => x.Version).IsConcurrencyToken();
        conversation.HasIndex(x => new { x.UserId, x.DeletedAt, x.ArchivedAt, x.UpdatedAt, x.Id });
        var note = Owned<PlanningNote>(b);
        note.Property(x => x.Title).HasMaxLength(120);
        note.Property(x => x.Content).HasMaxLength(8000);
        note.Property(x => x.Version).IsConcurrencyToken();
        note.HasIndex(x => new { x.UserId, x.UpdatedAt });
        var task = Owned<WorkTask>(b);
        task.Property(x => x.Version).IsConcurrencyToken();
        task.Property(x => x.Name).HasMaxLength(200); task.Property(x => x.Description).HasMaxLength(4000);
        task.Property(x => x.TaskType).HasMaxLength(60); task.HasIndex(x => new { x.UserId, x.Deadline });
        task.HasOne<TaskCategory>().WithMany().HasForeignKey(x => new { x.UserId, x.CategoryId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        task.HasOne<WorkTask>().WithMany().HasForeignKey(x => new { x.UserId, x.ParentId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var category = Owned<TaskCategory>(b); category.Property(x => x.Name).HasMaxLength(80); category.Property(x => x.Color).HasMaxLength(7);
        category.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
        var dep = Owned<TaskDependency>(b); dep.HasIndex(x => new { x.UserId, x.TaskId, x.PredecessorId }).IsUnique();
        dep.HasOne<WorkTask>().WithMany().HasForeignKey(x => new { x.UserId, x.TaskId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        dep.HasOne<WorkTask>().WithMany().HasForeignKey(x => new { x.UserId, x.PredecessorId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        Owned<WeeklyAvailability>(b).HasIndex(x => new { x.UserId, x.DayOfWeek });
        var exception = Owned<AvailabilityException>(b); exception.HasIndex(x => new { x.UserId, x.Date }); exception.Property(x => x.Note).HasMaxLength(300);
        var fixedEvent = Owned<FixedEvent>(b); fixedEvent.Property(x => x.Name).HasMaxLength(200); fixedEvent.HasIndex(x => new { x.UserId, x.Start });
        var preference = Owned<WorkPreference>(b); preference.HasIndex(x => x.UserId).IsUnique(); preference.Property(x => x.TimeZoneId).HasMaxLength(100);
        var proposal = Owned<PlanProposal>(b); proposal.Property(x => x.PayloadJson).IsRequired(); proposal.HasIndex(x => x.ExpiresAt);
        var session = Owned<WorkSession>(b); session.Property(x => x.ReasonCode).HasMaxLength(80);
        session.HasIndex(x => new { x.UserId, x.Start }); session.HasIndex(x => x.UserId).IsUnique().HasFilter("[Status] IN (1, 2)");
        session.HasOne<WorkTask>().WithMany().HasForeignKey(x => new { x.UserId, x.TaskId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var notification = Owned<InAppNotification>(b); notification.Property(x => x.Kind).HasMaxLength(60); notification.Property(x => x.Title).HasMaxLength(200);
        notification.Property(x => x.Message).HasMaxLength(1000); notification.Property(x => x.DedupeKey).HasMaxLength(200);
        notification.HasIndex(x => new { x.UserId, x.DedupeKey }).IsUnique(); notification.HasIndex(x => new { x.UserId, x.CreatedAt });
        notification.Property(x => x.IsVisible).HasDefaultValue(true);
        notification.HasIndex(x => new { x.UserId, x.EmailPending, x.NextEmailAttemptAt });
    }
}
