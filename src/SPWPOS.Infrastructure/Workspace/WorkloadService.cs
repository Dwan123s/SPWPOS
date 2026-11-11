using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static bool HasWorkHours(PlanningState state) =>
        state.Policy is { ConfirmedAt: var confirmed } && confirmed != default ||
        state.Availability.Length > 0 || state.Exceptions.Any(x => x.IsAvailable);

    private static bool ActiveWork(WorkTask task) => !task.IsDeleted &&
        task.Status is not (WorkTaskStatus.Completed or WorkTaskStatus.Cancelled);

    private bool ValidWorkReservation(PlanningState state, WorkTask task, WorkSession session) =>
        session.End <= task.Deadline && PlanningEngine.InsideWorkWindow(task, session.Start, session.End) &&
        engine.FreeSlots(state, session.Start, session.End).Any(g => g.Start <= session.Start && g.End >= session.End);

    public async Task<WorkloadView> GetWorkloadAsync(Guid uid, CancellationToken ct)
    {
        var now = Now;
        await using var snapshot = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var state = await State(uid, ct);
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        DateOnly Day(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);
        DateTimeOffset Midnight(DateOnly day) => PlanningEngine.At(day, 0, zone);
        var today = Day(now);
        var first = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        var last = first.AddDays(6);
        var weekStart = Midnight(first);
        var weekEnd = Midnight(last.AddDays(1));
        var hasHours = HasWorkHours(state);
        var capacity = engine.CapacityDays(state, now, weekEnd, now, false).ToDictionary(x => x.Date);
        var tasks = state.Tasks.ToDictionary(t => t.Id);
        var upcoming = state.Sessions.Where(s => s.End > now &&
            s.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused).ToArray();
        var reviews = (await WorkTimeReviews(uid, true, ct)).Select(r => r.SessionId).ToHashSet();
        var outcomes = (await AppliedExtraTimes(uid, ct)).Where(p => p.Outcome != null)
            .ToDictionary(p => p.SessionId, p => p.Outcome!.Outcome);
        var team = await (from a in db.TeamTaskAssignments.AsNoTracking().Where(a => a.UserId == uid)
                          join t in db.TeamTasks.AsNoTracking() on a.TeamTaskId equals t.Id
                          where a.Active select new { a.WorkTaskId, t.Status }).ToDictionaryAsync(x => x.WorkTaskId, x => x.Status, ct);
        var closed = await Mine<SessionExecution>(uid).AsNoTracking().Where(e => e.EndedAt >= weekStart && e.EndedAt < weekEnd)
            .Select(e => new { e.SessionId, e.EndedAt, e.ActualSeconds }).ToArrayAsync(ct);
        var executions = closed.ToDictionary(e => e.SessionId);
        var active = state.Tasks.Where(ActiveWork).ToArray();
        var checkedPlan = hasHours && active.Length > 0 ? engine.Generate(state,
            new(now, 30, UseCapacityFactor: false, UseEstimateBias: false) { AllowDeadlineRescue = false }, now) : null;
        var warnings = (checkedPlan?.Warnings ?? []).ToLookup(w => w.TaskId);
        var conflicts = TimeConflictTaskIds(state).ToHashSet();
        var parentIds = state.Tasks.Where(t => t.ParentId != null).Select(t => t.ParentId!.Value).ToHashSet();
        var work = state.Tasks.Where(t => !t.IsDeleted && !parentIds.Contains(t.Id) && t.Status != WorkTaskStatus.Cancelled)
            .Select(t =>
            {
                var remaining = ActiveWork(t) ? engine.Remaining(t, [new("All", 0, 1, false)]) : 0;
                var covered = upcoming.Where(s => s.TaskId == t.Id && ValidWorkReservation(state, t, s))
                    .Sum(s => Math.Max(0, (int)(s.End - (s.Start < now ? now : s.Start)).TotalMinutes));
                var taskWarnings = warnings[t.Id].ToArray();
                var warning = taskWarnings.FirstOrDefault(w => w.UnscheduledMinutes > 0 || w.Code == "SOFT_DEADLINE_EXCEEDED");
                var shortfall = warning?.UnscheduledMinutes ?? 0;
                if (checkedPlan != null && taskWarnings.Any(w => w.Code == "SOFT_DEADLINE_EXCEEDED"))
                    shortfall += checkedPlan.Sessions.Where(s => s.TaskId == t.Id && !s.Preserved && s.End > t.Deadline)
                        .Sum(s => (int)(s.End - (s.Start > t.Deadline ? s.Start : t.Deadline)).TotalMinutes);
                var checklist = ReadTaskChecklist(t);
                return new WorkloadTask(t.Id, t.Name, t.EstimatedMinutes, remaining, Math.Max(0, remaining - covered),
                    hasHours && t.Deadline <= now.AddDays(30) ? shortfall : null,
                    t.Deadline, ActiveWork(t) && t.IsHardDeadline && t.Deadline < now, t.Status.ToString(),
                    team.GetValueOrDefault(t.Id), conflicts.Contains(t.Id) ? "Phiên đã xếp bị xung đột." : warning?.Message,
                    !hasHours ? "UNKNOWN" : t.Deadline > now.AddDays(30) ? "OUTSIDE_HORIZON" : "ASSESSED",
                    checklist.Count(item => item.Completed), checklist.Length);
            }).ToArray();
        var weekSessions = state.Sessions.Where(s => s.Start < weekEnd && s.End > weekStart && tasks.ContainsKey(s.TaskId)).ToArray();
        var scheduledTasks = weekSessions.Select(s => s.TaskId).ToHashSet();
        var weekTasks = work.Where(t => scheduledTasks.Contains(t.Id) || t.Deadline >= weekStart && t.Deadline < weekEnd ||
            tasks[t.Id].CompletedAt >= weekStart && tasks[t.Id].CompletedAt < weekEnd).ToArray();
        var days = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = first.AddDays(offset);
            var lo = Midnight(date);
            var hi = Midnight(date.AddDays(1));
            var sessions = weekSessions.Where(s => s.Start < hi && s.End > lo).OrderBy(s => s.Start).ToArray();
            var actual = closed.Where(e => Day(e.EndedAt!.Value) == date).ToArray();
            var cap = capacity.GetValueOrDefault(date);
            int? working = hasHours ? (int)engine.FreeSlots(state with { Events = [] }, lo, hi).Sum(g => (g.End - g.Start).TotalMinutes) : null;
            var status = date < today ? "PAST" : !hasHours ? "UNCONFIGURED" :
                work.Any(t => Day(t.Deadline) == date && t.ShortfallMinutes > 0) ? "SHORTFALL" :
                working == 0 ? "NO_HOURS" : cap?.SchedulableMinutes > 0 ? "AVAILABLE" : "FULL";
            return new WorkloadDay(date, working, cap?.ReservedMinutes, hasHours && date >= today ? cap?.SchedulableMinutes : null,
                sessions.Sum(s => (long)((s.End < hi ? s.End : hi) - (s.Start > lo ? s.Start : lo)).TotalSeconds),
                actual.Length == 0 ? null : actual.Sum(e => e.ActualSeconds), status,
                status == "SHORTFALL" ? "Xem việc có nguy cơ trễ." : status == "UNCONFIGURED" ? "Thiết lập giờ làm." : null,
                sessions.Select(s => new WorkloadSession(s.Id, s.TaskId, tasks[s.TaskId].Name, s.Start, s.End,
                    s.Status.ToString(), executions.TryGetValue(s.Id, out var execution) && Day(execution.EndedAt!.Value) == date ? execution.ActualSeconds : null,
                    reviews.Contains(s.Id), team.ContainsKey(s.TaskId), s.ActualSeconds +
                    (s.SegmentStartedAt is { } segment && s.End <= now ? Math.Max(0, (long)(s.End - segment).TotalSeconds) : 0),
                    outcomes.GetValueOrDefault(s.Id))).ToArray());
        }).ToArray();
        var ordered = upcoming.Where(s => tasks.TryGetValue(s.TaskId, out var t) && ActiveWork(t))
            .OrderBy(s => tasks[s.TaskId].Deadline).ThenByDescending(s => tasks[s.TaskId].Priority).ThenBy(s => s.Start).ToArray();
        var review = state.Sessions.Where(s => reviews.Contains(s.Id) && tasks.ContainsKey(s.TaskId))
            .OrderBy(s => tasks[s.TaskId].Deadline).ThenByDescending(s => tasks[s.TaskId].Priority).FirstOrDefault();
        bool CanStart(WorkSession session)
        {
            try { ValidateExecutionStart(state, session, tasks[session.TaskId], session.Status == WorkSessionStatus.Paused); }
            catch (AuthException) { return false; }
            return true;
        }
        var nextSession = ordered.FirstOrDefault(s => s.Status == WorkSessionStatus.Running)
            ?? ordered.FirstOrDefault(s => CanStart(s));
        var risk = work.Where(t => t.Status is "Todo" or "InProgress" && (t.Overdue || t.Reason != null))
            .OrderBy(t => t.Deadline).ThenByDescending(t => tasks[t.Id].Priority).FirstOrDefault();
        var next = review != null ? new WorkloadAction("REVIEW", $"Ghi nhận kết quả: {tasks[review.TaskId].Name}.", review.TaskId, review.Id) :
            nextSession != null ? new("SESSION", $"Phiên tiếp theo: {tasks[nextSession.TaskId].Name}.", nextSession.TaskId, nextSession.Id) :
            risk != null ? new("RISK", $"{risk.Name}: {risk.Reason ?? "Đã quá hạn."}", risk.Id) :
            !hasHours ? new("SETUP", "Xác nhận giờ làm để tìm chỗ cho công việc.") :
            active.Length == 0 ? new("CAPTURE", "Ghi việc bạn muốn làm trước.") :
            work.All(t => t.UnscheduledMinutes == 0) ? new("OPEN_CALENDAR", "Các công việc đã có thời gian. Mở lịch để xem phiên tiếp theo.") :
            new("PLAN", "Xem phương án cho công việc chưa xếp đủ thời gian.");
        var refresh = ordered.SelectMany(s => new[] { s.Start.AddMinutes(-15), s.Start, s.End }).Where(t => t > now)
            .Concat(active.Select(t => t.Deadline.AddSeconds(1)).Where(t => t > now))
            .Concat(state.Events.Select(e => e.End).Where(t => t > now))
            .Append(Midnight(today.AddDays(1))).Min();
        if (snapshot is not null) await snapshot.CommitAsync(ct);
        return new(now, refresh, state.Preference.Revision, zone.Id, first, last, days, weekTasks,
            weekTasks.Count(t => t.Status == "Completed" && (t.TeamStatus is null or "Completed")), next);
    }
}
