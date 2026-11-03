using SPWPOS.Domain.Entities;
namespace SPWPOS.Application.Workspace;

public sealed partial class PlanningEngine
{
    // A shortage is first checked with the saved calendar intact. Only then try
    // borrowing future flexible sessions from work with a later deadline. The
    // result is a confirmation-only proposal, never a mutation of input data.
    private PlanView RescueImminentDeadline(PlanningState state, PlanInput input, DateTimeOffset now, PlanView baseline)
    {
        if (!input.AllowDeadlineRescue || !state.Preference.AutoReschedule || !baseline.Warnings.Any(w => w.UnscheduledMinutes > 0)) return baseline;
        var start = input.Start > now ? input.Start : now;
        var end = input.WindowEnd ?? start.AddDays(input.HorizonDays);
        var selected = input.AffectedTaskIds is { Length: > 0 }
            ? AffectedClosure(state, input.AffectedTaskIds) : state.Tasks.Select(t => t.Id).ToHashSet();
        var bias = input.UseEstimateBias ? Biases(state) : [new BiasView("All", 0, 1, false)];
        var tasks = state.Tasks.ToDictionary(t => t.Id);
        var targets = state.Tasks.Where(t => selected.Contains(t.Id) && t.Deadline > start &&
            t.Deadline - now <= NearDeadlineWindow && t.EffectiveImportance == "Important" &&
            !state.Tasks.Any(child => child.ParentId == t.Id && !child.IsDeleted) &&
            (baseline.HasUrgentReplan || baseline.Warnings.Any(w => w.TaskId == t.Id && w.UnscheduledMinutes > 0)))
            .OrderBy(t => t.Deadline).ThenBy(t => t.Id).Take(MaximumRepairAttempts).ToArray();
        var attempts = 0;
        foreach (var target in targets)
        {
            var prerequisites = new HashSet<Guid> { target.Id };
            IncludePrerequisites(state, prerequisites);
            var movable = state.Sessions.Where(s => s.Status == WorkSessionStatus.Scheduled && !s.IsLocked &&
                s.ActualSeconds == 0 && s.SegmentStartedAt is null && s.Start >= start && s.Start < end &&
                s.Start < target.Deadline && s.End > start && !prerequisites.Contains(s.TaskId) &&
                tasks.TryGetValue(s.TaskId, out var t) && !t.IsDeleted && Remaining(t, bias) > 0 && t.Deadline > target.Deadline)
                .OrderBy(s => tasks[s.TaskId].EffectiveImportance == "Important")
                .ThenByDescending(s => tasks[s.TaskId].Deadline)
                .ThenBy(s => s.Start).ThenBy(s => s.Id).ToArray();
            var released = new HashSet<Guid>();
            var displacedTasks = new HashSet<Guid>();
            // Try complete task groups, bounded independently of calendar size.
            foreach (var group in movable.GroupBy(s => s.TaskId).Take(MaximumRepairAttempts))
            {
                if (attempts++ >= MaximumRepairAttempts) return baseline;
                var trialReleased = new HashSet<Guid>(released);
                trialReleased.UnionWith(group.Select(s => s.Id));
                var trialDisplaced = new HashSet<Guid>(displacedTasks) { group.Key };
                var affected = AffectedClosure(state, selected.Concat(trialDisplaced));
                var retained = state.Sessions.Where(s => !trialReleased.Contains(s.Id) && !selected.Contains(s.TaskId))
                    .Select(s => s.Id).ToHashSet();
                var protectDeadlines = new HashSet<Guid>(trialDisplaced) { target.Id };
                var policy = new UrgentReplanPolicy(retained, [], protectDeadlines, [target.Id]);
                var candidate = GenerateCore(state, input with { AffectedTaskIds = affected.ToArray() }, now, policy);
                // Every borrowed task must retain all of its required work and
                // its original deadline, including deadlines marked soft.
                if (trialDisplaced.Any(id => candidate.Warnings.Any(w => w.TaskId == id && w.UnscheduledMinutes > 0) ||
                    candidate.Sessions.Any(s => s.TaskId == id && s.End > tasks[id].Deadline))) continue;
                // Do not touch a saved session unless it was deliberately released
                // or belonged to the user's original scheduling scope.
                if (state.Sessions.Any(s => retained.Contains(s.Id) && s.Status == WorkSessionStatus.Scheduled &&
                    s.End > start && s.Start < end && !candidate.Sessions.Any(p => p.Id == s.Id && p.Preserved && p.Start == s.Start && p.End == s.End))) continue;
                // An immovable first candidate must not poison the next attempt.
                // Accumulate only groups whose own work still fits before its deadline.
                released = trialReleased;
                displacedTasks = trialDisplaced;
                if (candidate.Warnings.Any(w => w.TaskId == target.Id && w.UnscheduledMinutes > 0) ||
                    !ImprovesWithoutRegression(state, baseline, candidate)) continue;
                return candidate with { PriorityPolicy = baseline.PriorityPolicy, HasUrgentReplan = true,
                    Explanations = candidate.Explanations.Append(
                        $"Việc «{target.Name}» quan trọng và còn hạn trong 48 giờ bị thiếu thời gian. Đề xuất dời các phiên linh hoạt có deadline muộn hơn, giữ đủ thời lượng và deadline của mọi việc. Cần xác nhận trước khi lưu.").ToArray() };
            }
        }
        return baseline;
    }
}
