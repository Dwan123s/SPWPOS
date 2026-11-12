using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private Task<TeamWorkloadMember[]> TeamWorkloadMembers(Guid teamId, CancellationToken ct) =>
        (from membership in db.TeamMemberships.AsNoTracking()
         join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
         join profile in db.UserProfiles.AsNoTracking() on user.Id equals profile.UserId into profiles
         from profile in profiles.DefaultIfEmpty()
         where membership.TeamId == teamId && membership.Active && user.IsActive && user.EmailConfirmed
         orderby membership.Role descending, user.FirstName, user.LastName, user.Id
         select new TeamWorkloadMember(user.Id, profile != null && profile.DisplayName != "" ? profile.DisplayName : user.FirstName + " " + user.LastName))
        .ToArrayAsync(ct);

    public async Task<TeamWorkloadView> GetTeamWorkloadAsync(Guid uid, Guid teamId, int week, CancellationToken ct)
    {
        var now = Now;
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await TeamMember(uid, teamId, ct, leader: true);
        var viewerZoneId = await Mine<WorkPreference>(uid).Select(p => p.TimeZoneId).SingleOrDefaultAsync(ct);
        var viewerZone = PlanningEngine.Zone(viewerZoneId ?? new WorkPreference().TimeZoneId);
        var first = WorkloadWeekStart(now, viewerZone, week);
        var start = PlanningEngine.At(first, 0, viewerZone);
        var end = PlanningEngine.At(first.AddDays(7), 0, viewerZone);
        var teamTasks = await db.TeamTasks.AsNoTracking().Where(t => t.TeamId == teamId).ToArrayAsync(ct);
        var tasks = teamTasks.Where(t => InWorkloadWeek(t, start, end, week))
            .OrderBy(t => t.Deadline).ThenBy(t => t.Id).Select(t => SummarizeTeamTask(t, now)).ToArray();
        var members = await TeamWorkloadMembers(teamId, ct);
        await snapshot.CommitAsync(ct);
        var refresh = tasks.Where(t => t.Status is "Open" or "Assigned" && t.Deadline > now)
            .Select(t => t.Deadline.AddSeconds(1)).Append(now.AddMinutes(5)).Min();
        return new(now, refresh, first, first.AddDays(6), members, tasks);
    }

    private static bool InWorkloadWeek(TeamTask task, DateTimeOffset start, DateTimeOffset end, int week) =>
        task.Status is "Open" or "Assigned" or "PendingReview" or "Completed" &&
        (task.Deadline >= start && task.Deadline < end ||
         task.CompletionReviewedAt >= start && task.CompletionReviewedAt < end ||
         week == 0 && task.Deadline < start && task.Status is "Open" or "Assigned" or "PendingReview");

    private static TeamWorkloadTask SummarizeTeamTask(TeamTask task, DateTimeOffset now)
    {
        var risk = task.Status switch
        {
            "Completed" => "COMPLETED",
            "PendingReview" => "REVIEW",
            _ when task.Deadline <= now => "OVERDUE",
            "Open" when task.DesignatedUserId is null => "UNASSIGNED",
            "Open" => "WAITING",
            _ => "ASSIGNED"
        };
        return new(task.Id, task.Name, task.Version, task.Status, task.AssigneeId, task.DesignatedUserId, task.Deadline,
            task.EstimatedMinutes, task.Progress, risk);
    }
}
