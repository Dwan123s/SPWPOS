using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Persistence;

public static class WorkloadHistoryModel
{
    private static EntityTypeBuilder<T> Owned<T>(ModelBuilder b, string table) where T : OwnedRecord
    {
        var e = b.Entity<T>(); e.ToTable(table); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.UserId, x.Id });
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.UserId); return e;
    }
    public static void Configure(ModelBuilder b)
    {
        var profile = b.Entity<UserProfile>(); profile.ToTable("user_profiles"); profile.HasKey(x => x.UserId);
        profile.HasOne<ApplicationUser>().WithOne().HasForeignKey<UserProfile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        profile.Property(x => x.DisplayName).HasMaxLength(100); profile.Property(x => x.TimeZoneId).HasMaxLength(100);
        profile.Property(x => x.Bio).HasMaxLength(1000); profile.Property(x => x.AvatarUrl).HasMaxLength(3000000);
        var settings = b.Entity<NotificationSetting>(); settings.ToTable("notification_settings"); settings.HasKey(x => x.UserId);
        settings.HasOne<ApplicationUser>().WithOne().HasForeignKey<NotificationSetting>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        settings.Property(x => x.WorkloadWarningLevel).HasMaxLength(10);
        var types = b.Entity<TaskTypeDefinition>(); types.ToTable("task_types"); types.HasKey(x => x.Code);
        types.Property(x => x.Code).HasMaxLength(60); types.Property(x => x.DisplayName).HasMaxLength(60);
        types.HasData(new TaskTypeDefinition { Code = "PROGRAMMING", DisplayName = "Programming" },
            new TaskTypeDefinition { Code = "WRITING", DisplayName = "Writing" }, new TaskTypeDefinition { Code = "STUDY", DisplayName = "Study" },
            new TaskTypeDefinition { Code = "MEETING_PREP", DisplayName = "Meeting preparation" }, new TaskTypeDefinition { Code = "ADMIN", DisplayName = "Admin / chores" },
            new TaskTypeDefinition { Code = "WORK", DisplayName = "Work" }, new TaskTypeDefinition { Code = "OTHER", DisplayName = "Other" });
        b.Entity<WorkTask>().HasOne<TaskTypeDefinition>().WithMany().HasForeignKey(x => x.TaskType).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WorkTask>().Property(x => x.CreationSource).HasMaxLength(16);
        b.Entity<FixedEvent>().Property(x => x.Description).HasMaxLength(1000); b.Entity<FixedEvent>().Property(x => x.EventType).HasMaxLength(12);
        b.Entity<WorkPreference>().Property(x => x.PreferredPeriod).HasMaxLength(10);
        var audit = Owned<AccountStatusLog>(b, "account_status_logs"); audit.Property(x => x.Action).HasMaxLength(10); audit.Property(x => x.Reason).HasMaxLength(500);
        audit.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.Restrict);
        var plan = b.Entity<PlanProposal>(); plan.Property(x => x.Kind).HasMaxLength(10); plan.Property(x => x.TriggerEvent).HasMaxLength(24); plan.Property(x => x.Status).HasMaxLength(12);
        plan.HasOne<PlanProposal>().WithMany().HasForeignKey(x => new { x.UserId, x.PreviousPlanId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var item = Owned<PlanItem>(b, "plan_items"); item.Property(x => x.Action).HasMaxLength(12);
        item.HasOne<PlanProposal>().WithMany().HasForeignKey(x => new { x.UserId, x.PlanId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        item.HasOne<WorkTask>().WithMany().HasForeignKey(x => new { x.UserId, x.TaskId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        item.HasOne<WorkSession>().WithMany().HasForeignKey(x => new { x.UserId, x.PreviousSessionId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var reason = Owned<PlanItemReason>(b, "plan_item_reasons"); reason.Property(x => x.Code).HasMaxLength(80);
        reason.HasOne<PlanItem>().WithMany().HasForeignKey(x => new { x.UserId, x.ItemId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var warning = Owned<StoredPlanWarning>(b, "plan_warnings"); warning.Property(x => x.Code).HasMaxLength(80); warning.Property(x => x.Detail).HasMaxLength(1000);
        warning.HasOne<PlanProposal>().WithMany().HasForeignKey(x => new { x.UserId, x.PlanId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        warning.HasOne<WorkTask>().WithMany().HasForeignKey(x => new { x.UserId, x.TaskId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var session = b.Entity<WorkSession>(); session.Property(x => x.SkipReason).HasMaxLength(255);
        session.HasIndex(x => x.SourceItemId).IsUnique().HasFilter("[SourceItemId] IS NOT NULL");
        session.HasOne<PlanItem>().WithMany().HasForeignKey(x => new { x.UserId, x.SourceItemId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var execution = Owned<SessionExecution>(b, "session_executions"); execution.HasIndex(x => x.SessionId).IsUnique();
        execution.HasOne<WorkSession>().WithMany().HasForeignKey(x => new { x.UserId, x.SessionId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var segment = Owned<ExecutionSegment>(b, "execution_segments");
        segment.HasOne<SessionExecution>().WithMany().HasForeignKey(x => new { x.UserId, x.ExecutionId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        segment.HasIndex(x => x.ExecutionId).IsUnique().HasFilter("[End] IS NULL");
        var obs = Owned<EstimationObservation>(b, "estimation_observations"); obs.HasIndex(x => x.TaskId).IsUnique(); obs.Property(x => x.TaskType).HasMaxLength(60);
        obs.HasOne<WorkTask>().WithMany().HasForeignKey(x => new { x.UserId, x.TaskId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        obs.HasOne<TaskTypeDefinition>().WithMany().HasForeignKey(x => x.TaskType).OnDelete(DeleteBehavior.Restrict); obs.HasIndex(x => new { x.UserId, x.TaskType });
        var capacity = b.Entity<PersonalCapacityProfile>(); capacity.ToTable("personal_capacity_profiles"); capacity.HasKey(x => x.UserId);
        capacity.HasOne<ApplicationUser>().WithOne().HasForeignKey<PersonalCapacityProfile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        var bias = b.Entity<TaskTypeBias>(); bias.ToTable("task_type_biases"); bias.HasKey(x => new { x.UserId, x.TaskType }); bias.Property(x => x.TaskType).HasMaxLength(60);
        bias.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        bias.HasOne<TaskTypeDefinition>().WithMany().HasForeignKey(x => x.TaskType).OnDelete(DeleteBehavior.Restrict);
        Owned<CapacitySnapshot>(b, "capacity_snapshots").HasIndex(x => new { x.UserId, x.WeekStart }).IsUnique();
        var risk = Owned<RiskAssessment>(b, "risk_assessments"); risk.Property(x => x.Scope).HasMaxLength(10); risk.Property(x => x.Level).HasMaxLength(10);
        risk.HasIndex(x => new { x.UserId, x.Scope, x.TaskId }).IsUnique().HasFilter("[IsCurrent] = 1");
        risk.HasOne<WorkTask>().WithMany().HasForeignKey(x => new { x.UserId, x.TaskId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var rr = Owned<RiskReason>(b, "risk_reasons"); rr.Property(x => x.Code).HasMaxLength(80);
        rr.HasOne<RiskAssessment>().WithMany().HasForeignKey(x => new { x.UserId, x.AssessmentId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var n = b.Entity<InAppNotification>();
        n.HasOne<WorkTask>().WithMany().HasForeignKey(x => new { x.UserId, x.TaskId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        n.HasOne<WorkSession>().WithMany().HasForeignKey(x => new { x.UserId, x.SessionId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        n.HasOne<PlanProposal>().WithMany().HasForeignKey(x => new { x.UserId, x.PlanId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var job = b.Entity<SystemJobRun>(); job.ToTable("system_job_runs"); job.HasKey(x => x.Id);
        job.Property(x => x.JobType).HasMaxLength(24); job.Property(x => x.Status).HasMaxLength(10); job.Property(x => x.ErrorMessage).HasMaxLength(1000);
        job.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); job.HasIndex(x => new { x.Status, x.StartedAt });
    }
}
