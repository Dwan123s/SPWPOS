using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private sealed record WorkloadMember(Guid UserId, string Name);

    private Task<WorkloadMember[]> TeamWorkloadMembers(Guid teamId, CancellationToken ct) =>
        (from membership in db.TeamMemberships.AsNoTracking()
         join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
         join profile in db.UserProfiles.AsNoTracking() on user.Id equals profile.UserId into profiles
         from profile in profiles.DefaultIfEmpty()
         where membership.TeamId == teamId && membership.Active && user.IsActive && user.EmailConfirmed
         orderby membership.Role descending, user.FirstName, user.LastName, user.Id
         select new WorkloadMember(user.Id, profile != null && profile.DisplayName != "" ? profile.DisplayName : user.FirstName + " " + user.LastName))
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
        var assignments = await (from a in db.TeamTaskAssignments.AsNoTracking()
                                 join t in db.TeamTasks.AsNoTracking() on a.TeamTaskId equals t.Id
                                 where t.TeamId == teamId select a).ToArrayAsync(ct);
        var assignmentsByUser = assignments.ToLookup(a => a.UserId);
        var sharedTasks = teamTasks.ToDictionary(t => t.Id);
        var workload = new Dictionary<Guid, TeamWorkloadTask>();
        foreach (var task in teamTasks.Where(t => InWorkloadWeek(t, start, end, week)))
            workload[task.Id] = SummarizeTeamTask(task, now);

        var members = new List<TeamWorkloadMember>();
        foreach (var member in await TeamWorkloadMembers(teamId, ct))
        {
            var state = await State(member.UserId, ct);
            var memberAssignments = assignmentsByUser[member.UserId];
            var workTaskIds = memberAssignments.Select(a => a.WorkTaskId).ToHashSet();
            var teamSessions = state.Sessions.Where(s => workTaskIds.Contains(s.TaskId)).ToArray();
            members.Add(new(member.UserId, member.Name, state.Preference.TimeZoneId,
                await TeamCapacityDays(member.UserId, state, teamSessions, first, now, ct)));

            var workInWeek = teamSessions.Where(s => s.Start < end && s.End > start).Select(s => s.TaskId).ToHashSet();
            var relevant = memberAssignments.Where(a => a.Active && sharedTasks[a.TeamTaskId].AssigneeId == member.UserId &&
                (workload.ContainsKey(a.TeamTaskId) || workInWeek.Contains(a.WorkTaskId))).ToArray();
            if (relevant.Length == 0) continue;

            var memberTasks = state.Tasks.ToDictionary(t => t.Id);
            var hasHours = HasWorkHours(state);
            var forecast = hasHours && relevant.Any(a => sharedTasks[a.TeamTaskId].Status == "Assigned")
                ? ForecastWorkload(state, now) : null;
            var warnings = (forecast?.Warnings ?? []).ToLookup(w => w.TaskId);
            var upcoming = teamSessions.Where(s => s.End > now &&
                s.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused).ToArray();
            foreach (var assignment in relevant)
            {
                var task = sharedTasks[assignment.TeamTaskId];
                var row = SummarizeTeamTask(task, now);
                if (task.Status == "Assigned" && memberTasks.TryGetValue(assignment.WorkTaskId, out var ownTask))
                {
                    var remaining = WorkloadRemaining(ownTask);
                    var unplanned = Math.Max(0, remaining - WorkloadCoverage(state, ownTask, upcoming, now));
                    int? shortfall = forecast is not null && task.Deadline <= now.AddDays(30) && task.Deadline > now
                        ? WorkloadShortfall(forecast, ownTask, warnings[ownTask.Id].ToArray()) : null;
                    var risk = task.Deadline <= now ? "OVERDUE" : shortfall > 0 ? "BEHIND" :
                        shortfall is null ? "UNKNOWN" : unplanned > 0 ? "UNPLANNED" : "ON_TRACK";
                    row = row with { RemainingMinutes = remaining, UnscheduledMinutes = unplanned, ShortfallMinutes = shortfall, Risk = risk };
                }
                workload[task.Id] = row;
            }
        }
        await snapshot.CommitAsync(ct);
        var refresh = workload.Values.Where(t => t.Status is "Open" or "Assigned" && t.Deadline > now)
            .Select(t => t.Deadline.AddSeconds(1)).Append(now.AddMinutes(5)).Min();
        return new(now, refresh, first, first.AddDays(6), members.ToArray(),
            workload.Values.OrderBy(t => t.Deadline).ThenBy(t => t.Id).ToArray());
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
            _ => "UNKNOWN"
        };
        return new(task.Id, task.Name, task.Version, task.Status, task.AssigneeId, task.DesignatedUserId, task.Deadline,
            task.Status is "Completed" or "PendingReview" ? 0 : task.EstimatedMinutes, null, null, risk);
    }

    private async Task<TeamWorkloadDay[]> TeamCapacityDays(Guid uid, PlanningState state, WorkSession[] teamSessions,
        DateOnly first, DateTimeOffset now, CancellationToken ct)
    {
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        var start = PlanningEngine.At(first, 0, zone);
        var end = PlanningEngine.At(first.AddDays(7), 0, zone);
        var sessionIds = teamSessions.Select(s => s.Id).ToArray();
        var executions = sessionIds.Length == 0 ? [] : await Mine<SessionExecution>(uid).AsNoTracking()
            .Where(e => sessionIds.Contains(e.SessionId) && e.EndedAt >= start && e.EndedAt < end)
            .Select(e => new { End = e.EndedAt!.Value, e.ActualSeconds }).ToArrayAsync(ct);
        var actualByDay = executions.ToLookup(e => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(e.End, zone).DateTime));
        var hasHours = HasWorkHours(state);
        var full = engine.CapacityDays(state, start, end, now, false);
        var remaining = engine.CapacityDays(state, start > now ? start : now, end, now, false).ToDictionary(d => d.Date);
        var withoutEvents = state with { Events = [] };
        return full.Select(day =>
        {
            var lo = PlanningEngine.At(day.Date, 0, zone);
            var hi = PlanningEngine.At(day.Date.AddDays(1), 0, zone);
            var working = (int)engine.FreeSlots(withoutEvents, lo, hi).Sum(s => (s.End - s.Start).TotalMinutes);
            var actual = actualByDay[day.Date].ToArray();
            return new TeamWorkloadDay(day.Date, hasHours ? working : null,
                hasHours ? Math.Max(0, working - day.CalendarFreeMinutes) : null,
                day.ReservedMinutes + day.ConsumedMinutes,
                hasHours ? remaining.GetValueOrDefault(day.Date)?.SchedulableMinutes : null,
                actual.Length > 0 ? actual.Sum(e => e.ActualSeconds) : null);
        }).ToArray();
    }

    public async Task<TeamWorkloadCandidate[]> GetTeamWorkloadCandidatesAsync(Guid uid, Guid teamId, Guid taskId, CancellationToken ct)
    {
        var now = Now;
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await TeamMember(uid, teamId, ct, leader: true);
        var shared = await db.TeamTasks.AsNoTracking().SingleOrDefaultAsync(t => t.TeamId == teamId && t.Id == taskId, ct)
            ?? throw new AuthException("NOT_FOUND", "Không tìm thấy công việc nhóm.", 404);
        Require(shared.Status == "Open" && shared.AssigneeId is null, "TASK_UNAVAILABLE", "Công việc đã được nhận.", 409);
        Require(TeamDeadlineInRange(shared.Deadline), "TEAM_DEADLINE_RANGE", "Cần cập nhật hạn chót để kiểm tra người nhận.", 409);
        var start = now.AddMinutes(5);
        var candidates = new List<TeamWorkloadCandidate>();
        foreach (var member in await TeamWorkloadMembers(teamId, ct))
        {
            var state = await State(member.UserId, ct);
            if (!HasWorkHours(state))
            {
                candidates.Add(new(member.UserId, "UNKNOWN", null));
                continue;
            }
            var minutes = engine.CapacityDays(state, start, shared.Deadline, now, false).Sum(d => d.SchedulableMinutes);
            var task = NewTeamClaimTask(member.UserId, SharedDto(shared), shared, state, now);
            var candidate = state with
            {
                Tasks = state.Tasks.Append(task).ToArray(),
                Sessions = state.Sessions.Select(s => s.Status == WorkSessionStatus.Scheduled ? CloneLocked(s) : s).ToArray()
            };
            var request = new PlanInput(start, 30, [task.Id], true, UseCapacityFactor: false, UseEstimateBias: false)
            {
                WindowEnd = task.Deadline, AllowDeadlineRescue = false
            };
            var plan = engine.Generate(candidate, request, now);
            candidates.Add(new(member.UserId, ClaimFeasible(candidate, task, plan) ? "FITS" : "NEEDS_REPLAN", minutes));
        }
        await snapshot.CommitAsync(ct);
        return candidates.OrderBy(c => c.Status == "FITS" ? 0 : c.Status == "NEEDS_REPLAN" ? 1 : 2)
            .ThenByDescending(c => c.SchedulableMinutes).ToArray();
    }
}
