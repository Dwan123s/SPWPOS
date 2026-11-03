using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Persistence;

public sealed partial class ApplicationDbContext
{
    internal bool TeamPlanningWrite { get; set; }
    private void ProtectTeamTaskContracts()
    {
        if (TeamPlanningWrite) return;
        var protectedNames = new[] { nameof(WorkTask.Name), nameof(WorkTask.Description), nameof(WorkTask.EstimatedMinutes), nameof(WorkTask.Deadline), nameof(WorkTask.Priority), nameof(WorkTask.Splittable), nameof(WorkTask.IsHardDeadline), nameof(WorkTask.IsDeleted) };
        var ids = ChangeTracker.Entries<WorkTask>().Where(e => e.State == EntityState.Deleted || e.State == EntityState.Modified && e.Properties.Any(p => p.IsModified && protectedNames.Contains(p.Metadata.Name))).Select(e => e.Entity.Id).ToArray();
        if (ids.Length > 0 && TeamTaskAssignments.Any(x => x.Active && ids.Contains(x.WorkTaskId)))
            throw new SPWPOS.Application.Authentication.AuthException("TEAM_CONTRACT_PROTECTED", "Yêu cầu task nhóm phải được thay đổi và xác nhận trong nhóm. Dùng Trả việc để ngừng nhận.", 409);
    }
    public DbSet<AvailabilityPolicy> AvailabilityPolicies => Set<AvailabilityPolicy>();
    public DbSet<PrivatePlanningProposal> PrivatePlanningProposals => Set<PrivatePlanningProposal>();
    public DbSet<TeamProject> TeamProjects => Set<TeamProject>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<TeamInvite> TeamInvites => Set<TeamInvite>();
    public DbSet<TeamTask> TeamTasks => Set<TeamTask>();
    public DbSet<TeamTaskAssignment> TeamTaskAssignments => Set<TeamTaskAssignment>();
    public DbSet<TeamClaimProposal> TeamClaimProposals => Set<TeamClaimProposal>();
    public DbSet<TeamActivity> TeamActivities => Set<TeamActivity>();
    public DbSet<TeamChatMessage> TeamChatMessages => Set<TeamChatMessage>();
    public DbSet<TeamCompletionSubmission> TeamCompletionSubmissions => Set<TeamCompletionSubmission>();
    public DbSet<TeamCompletionFile> TeamCompletionFiles => Set<TeamCompletionFile>();
    public DbSet<TeamTaskDocument> TeamTaskDocuments => Set<TeamTaskDocument>();
}
internal static class PrivateTeamModel
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<TeamMembership>().Property(x => x.ApprovalStatus).HasMaxLength(16).HasDefaultValue("Approved");
        b.Entity<TeamMembership>().HasIndex(x => new { x.TeamId, x.ApprovalStatus });
        b.Entity<AvailabilityPolicy>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.UserId).IsUnique(); e.Property(x => x.Version).IsConcurrencyToken(); e.Property(x => x.RoutineDaysJson).HasDefaultValue("[]"); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<PrivatePlanningProposal>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.UserId, x.ExpiresAt }); e.Property(x => x.Applied).IsConcurrencyToken(); e.Property(x => x.Kind).HasMaxLength(20); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeamProject>(e => { e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(120); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeamMembership>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.TeamId, x.UserId }).IsUnique(); e.Property(x => x.Role).HasMaxLength(10); e.Property(x => x.Version).IsConcurrencyToken(); e.Property(x => x.MutationVersion).IsConcurrencyToken(); e.HasOne<TeamProject>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeamInvite>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.CodeHash).IsUnique(); e.Property(x => x.CodeHash).HasMaxLength(64); e.Property(x => x.Version).IsConcurrencyToken(); e.HasOne<TeamProject>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeamTask>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.TeamId); e.Property(x => x.Name).HasMaxLength(200); e.Property(x => x.Description).HasMaxLength(4000); e.Property(x => x.CompletionReviewNote).HasMaxLength(500); e.Property(x => x.Status).HasMaxLength(20); e.Property(x => x.Version).IsConcurrencyToken(); e.HasOne<TeamProject>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeamTaskAssignment>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.TeamTaskId).IsUnique().HasFilter("[Active] = 1"); e.HasIndex(x => x.ProposalId).IsUnique(); e.HasIndex(x => x.WorkTaskId).IsUnique(); e.Property(x => x.Version).IsConcurrencyToken(); e.HasOne<TeamTask>().WithMany().HasForeignKey(x => x.TeamTaskId).OnDelete(DeleteBehavior.Restrict); e.HasOne<WorkTask>().WithMany().HasForeignKey(x => x.WorkTaskId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeamClaimProposal>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.UserId, x.ExpiresAt }); e.Property(x => x.Status).HasMaxLength(20).IsConcurrencyToken(); e.HasOne<TeamTask>().WithMany().HasForeignKey(x => x.TeamTaskId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeamActivity>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.TeamId, x.CreatedAt }); e.Property(x => x.Kind).HasMaxLength(40); e.HasOne<TeamProject>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeamChatMessage>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.TeamId, x.Id }); e.HasIndex(x => new { x.TeamId, x.UserId, x.RequestId, x.IsSystem }).IsUnique(); e.Property(x => x.Text).HasMaxLength(2000); e.HasOne<TeamProject>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeamCompletionSubmission>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.TeamTaskId); e.Property(x => x.Note).HasMaxLength(1000); e.HasOne<TeamTask>().WithMany().HasForeignKey(x => x.TeamTaskId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeamCompletionFile>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.SubmissionId); e.Property(x => x.Name).HasMaxLength(180); e.Property(x => x.MimeType).HasMaxLength(100); e.HasOne<TeamCompletionSubmission>().WithMany().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeamTaskDocument>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.TeamId); e.Property(x => x.Name).HasMaxLength(180); e.Property(x => x.MimeType).HasMaxLength(100); e.HasOne<TeamProject>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeamTask>().HasOne<TeamTaskDocument>().WithMany().HasForeignKey(x => x.SourceDocumentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TeamTask>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.DesignatedUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
