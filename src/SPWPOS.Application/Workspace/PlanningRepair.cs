using SPWPOS.Domain.Entities;
namespace SPWPOS.Application.Workspace;

public sealed partial class PlanningEngine
{
    private const int MaximumRepairAttempts = 8;

    private PlanView GenerateCore(PlanningState state, PlanInput input, DateTimeOffset now,
        UrgentReplanPolicy? urgency = null, bool urgentFirst = true)
    {
        var best = GenerateGreedy(state, input, now, urgency, urgentFirst);
        if (urgentFirst &&
            best.Warnings.Any(x => x.UnscheduledMinutes > 0))
        {
            var deadlineFirst = GenerateGreedy(state, input, now, urgency, urgentFirst: false);
            if (ImprovesWithoutRegression(state, best, deadlineFirst)) best = deadlineFirst;
        }
        if (!best.Warnings.Any(x => x.UnscheduledMinutes > 0)) return best;

        // Retry the same validated scheduling algorithm with a bounded number of
        // different task orders. Locked/active sessions, affected scope, budgets,
        // breaks, work windows and urgent displacement rules remain in force.
        var promoted = new HashSet<Guid>();
        var attempted = new HashSet<Guid>();
        for (var attempt = 0; attempt < MaximumRepairAttempts; attempt++)
        {
            var blockedIds = best.Warnings.Where(x => x.UnscheduledMinutes > 0).Select(x => x.TaskId).ToHashSet();
            var blocked = state.Tasks.Where(x => blockedIds.Contains(x.Id) && !attempted.Contains(x.Id))
                .OrderBy(x => x.CanSplit).ThenByDescending(x => x.WorkWindowStart != null || x.WorkWindowEnd != null)
                .ThenBy(x => x.Deadline).ThenBy(x => x.Id).FirstOrDefault();
            if (blocked is null) break;
            attempted.Add(blocked.Id);
            var trialOrder = new HashSet<Guid>(promoted) { blocked.Id };
            IncludePrerequisites(state, trialOrder);
            var candidate = GenerateGreedy(state, input, now, urgency, urgentFirst, trialOrder);
            if (!ImprovesWithoutRegression(state, best, candidate)) continue;
            best = candidate; promoted = trialOrder;
            if (!best.Warnings.Any(x => x.UnscheduledMinutes > 0)) break;
        }
        return promoted.Count == 0 ? best : best with { Explanations = best.Explanations.Append(
            "Đã điều chỉnh thứ tự công việc để tận dụng khung giờ bị giới hạn, không tăng phần việc chưa xếp hoặc làm công việc khác trễ hơn.").ToArray() };
    }

    private static void IncludePrerequisites(PlanningState state, HashSet<Guid> promoted)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var edge in state.Dependencies)
                if (promoted.Contains(edge.TaskId)) changed |= promoted.Add(edge.PredecessorId);
            foreach (var child in state.Tasks.Where(x => !x.IsDeleted && x.ParentId != null))
                if (promoted.Contains(child.ParentId!.Value)) changed |= promoted.Add(child.Id);
        } while (changed);
    }

    private static bool ImprovesWithoutRegression(PlanningState state, PlanView before, PlanView after)
    {
        static Dictionary<Guid, int> Shortages(PlanView plan) => plan.Warnings.GroupBy(x => x.TaskId)
            .ToDictionary(x => x.Key, x => x.Sum(w => w.UnscheduledMinutes));
        var oldShortages = Shortages(before); var newShortages = Shortages(after);
        if (newShortages.Values.Sum() >= oldShortages.Values.Sum() ||
            newShortages.Any(x => x.Value > oldShortages.GetValueOrDefault(x.Key))) return false;
        // A smaller deficit is not an improvement if another task now misses a
        // soft deadline, or finishes even later than the previous proposal.
        var oldEnds = before.Sessions.ToLookup(x => x.TaskId);
        var newEnds = after.Sessions.ToLookup(x => x.TaskId);
        return state.Tasks.All(task => task.Deadline is not { } deadline ||
            newEnds[task.Id].Select(x => x.End).Append(deadline).Max() <=
            oldEnds[task.Id].Select(x => x.End).Append(deadline).Max());
    }
}
