using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Persistence;

public sealed partial class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<EmailOtp> EmailOtps => Set<EmailOtp>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetTicket> PasswordResetTickets => Set<PasswordResetTicket>();
    public DbSet<SecurityAuditLog> SecurityAuditLogs => Set<SecurityAuditLog>();
    public DbSet<SecurityRateLimit> SecurityRateLimits => Set<SecurityRateLimit>();
    public DbSet<PasswordChangeNotification> PasswordChangeNotifications => Set<PasswordChangeNotification>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<PlanningNote> PlanningNotes => Set<PlanningNote>();
    public DbSet<AgentTurn> AgentTurns => Set<AgentTurn>();
    public DbSet<AgentConversation> AgentConversations => Set<AgentConversation>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<AgentToolCall> AgentToolCalls => Set<AgentToolCall>();
    public DbSet<AiUsageRecord> AiUsageRecords => Set<AiUsageRecord>();
    public DbSet<RecoverySetting> RecoverySettings => Set<RecoverySetting>();
    public DbSet<RecoveryCase> RecoveryCases => Set<RecoveryCase>();
    public DbSet<TaskCategory> TaskCategories => Set<TaskCategory>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<WeeklyAvailability> WeeklyAvailability => Set<WeeklyAvailability>();
    public DbSet<AvailabilityException> AvailabilityExceptions => Set<AvailabilityException>();
    public DbSet<FixedEvent> FixedEvents => Set<FixedEvent>();
    public DbSet<WorkPreference> WorkPreferences => Set<WorkPreference>();
    public DbSet<PlanProposal> PlanProposals => Set<PlanProposal>();
    public DbSet<WorkSession> WorkSessions => Set<WorkSession>();
    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();
    public DbSet<TelegramConnection> TelegramConnections => Set<TelegramConnection>();
    public DbSet<TelegramUpdateCursor> TelegramUpdateCursors => Set<TelegramUpdateCursor>();
    public DbSet<TaskTypeDefinition> TaskTypes => Set<TaskTypeDefinition>();
    public DbSet<AccountStatusLog> AccountStatusLogs => Set<AccountStatusLog>();
    public DbSet<PlanItem> PlanItems => Set<PlanItem>();
    public DbSet<PlanItemReason> PlanItemReasons => Set<PlanItemReason>();
    public DbSet<StoredPlanWarning> PlanWarnings => Set<StoredPlanWarning>();
    public DbSet<SessionExecution> SessionExecutions => Set<SessionExecution>();
    public DbSet<ExecutionSegment> ExecutionSegments => Set<ExecutionSegment>();
    public DbSet<EstimationObservation> EstimationObservations => Set<EstimationObservation>();
    public DbSet<PersonalCapacityProfile> PersonalCapacityProfiles => Set<PersonalCapacityProfile>();
    public DbSet<TaskTypeBias> TaskTypeBiases => Set<TaskTypeBias>();
    public DbSet<CapacitySnapshot> CapacitySnapshots => Set<CapacitySnapshot>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<RiskReason> RiskReasons => Set<RiskReason>();
    public DbSet<SystemJobRun> SystemJobRuns => Set<SystemJobRun>();
    // Rotate the token for every writer, including agent, execution, note import
    // and container updates. Fixed clocks and rapid saves cannot reuse a token.
    private void RefreshTaskVersions()
    {
        foreach (var entry in ChangeTracker.Entries<WorkTask>().Where(x => x.State == EntityState.Modified))
            entry.Entity.Version = Guid.NewGuid();
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectTeamTaskContracts();
        RefreshTaskVersions();
        CaptureCalendarChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProtectTeamTaskContracts();
        RefreshTaskVersions();
        CaptureCalendarChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        WorkspaceModel.Configure(builder);
        WorkloadHistoryModel.Configure(builder);
        CalendarModel.Configure(builder);
        AgentRuntimeModel.Configure(builder);
        PrivateTeamModel.Configure(builder);
        TelegramModel.Configure(builder);
    }
}
