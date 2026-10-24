using SPWPOS.Domain.Entities;
namespace SPWPOS.Application.Workspace;

public sealed partial class PlanningEngine
{
    public static readonly TimeSpan NearDeadlineWindow = TimeSpan.FromHours(48);
    public const int MaximumUrgentDisplacementDays = 2;
    private sealed record UrgentMove(DateTimeOffset Anchor, DateTimeOffset Earliest, DateTimeOffset Latest, bool NearDeadline);
    private sealed record UrgentReplanPolicy(HashSet<Guid> RetainedSessionIds, Dictionary<Guid, UrgentMove> Displaced,
        HashSet<Guid>? ProtectedTaskIds = null, HashSet<Guid>? RequestedUrgentTaskIds = null);

    private PlanView GenerateUrgent(PlanningState state, PlanInput input, DateTimeOffset now, UrgentReplanPolicy? protection = null)
    {
        var start = input.Start > now ? input.Start : now;
        var end = start.AddDays(input.HorizonDays);
        var bias = input.UseEstimateBias ? Biases(state) : [new BiasView("All", 0, 1, false)];
        var selected = input.AffectedTaskIds is null ? null : AffectedClosure(state, input.AffectedTaskIds);
        var urgent = state.Tasks.Where(x => x.Priority == 4 && Remaining(x, bias) > 0 &&
            (selected is null || selected.Contains(x.Id)) &&
            (protection?.RequestedUrgentTaskIds is null || protection.RequestedUrgentTaskIds.Contains(x.Id))).Select(x => x.Id).ToHashSet();
        if (urgent.Count == 0) return GenerateCore(state, input, now, protection);

        // A prerequisite cannot give up the very session needed by urgent work.
        var prerequisites = new HashSet<Guid>(urgent);
        bool changed;
        do
        {
            changed = false;
            foreach (var edge in state.Dependencies)
                if (prerequisites.Contains(edge.TaskId)) changed |= prerequisites.Add(edge.PredecessorId);
            foreach (var child in state.Tasks.Where(x => x.ParentId != null && !x.IsDeleted))
                if (prerequisites.Contains(child.ParentId!.Value)) changed |= prerequisites.Add(child.Id);
        } while (changed);

        var tasks = state.Tasks.ToDictionary(x => x.Id);
        var movable = state.Sessions.Where(x => x.Status == WorkSessionStatus.Scheduled && !x.IsLocked &&
            x.Start >= start && x.Start < end && !prerequisites.Contains(x.TaskId) && protection?.RetainedSessionIds.Contains(x.Id) != true &&
            tasks.ContainsKey(x.TaskId)).ToArray();
        if (movable.Length == 0) return GenerateCore(state, input, now, protection);
        var movableIds = movable.Select(x => x.Id).ToHashSet();
        var trial = GenerateCore(state with { Sessions = state.Sessions.Where(x => !movableIds.Contains(x.Id)).ToArray() },
            input with { AffectedTaskIds = urgent.ToArray() }, now, protection);
        if (trial.Warnings.Any(x => urgent.Contains(x.TaskId) && x.UnscheduledMinutes > 0))
            return GenerateCore(state, input, now, protection);
        var urgentSlots = trial.Sessions.Where(x => urgent.Contains(x.TaskId) && !x.Preserved).ToArray();
        var hitSessions = movable.Where(x => urgentSlots.Any(u =>
            x.Start < u.End.AddMinutes(state.Preference.BreakMinutes) && u.Start.AddMinutes(-state.Preference.BreakMinutes) < x.End))
            .ToArray();
        var hitIds = hitSessions.Select(x => x.Id).ToHashSet();
        var directlyAffected = hitSessions.Select(x => x.TaskId).ToHashSet();
        if (directlyAffected.Count == 0) return GenerateCore(state, input, now, protection);

        var closure = AffectedClosure(state, directlyAffected);
        var released = movable.Where(x => hitIds.Contains(x.Id) || closure.Contains(x.TaskId) && !directlyAffected.Contains(x.TaskId)).ToArray();
        var releasedIds = released.Select(x => x.Id).ToHashSet();
        var retained = state.Sessions.Where(x => !releasedIds.Contains(x.Id) && !urgent.Contains(x.TaskId)).Select(x => x.Id).ToHashSet();
        var zone = Zone(state.Preference.TimeZoneId);
        DateTimeOffset Shift(DateTimeOffset at, int days)
        {
            var local = TimeZoneInfo.ConvertTime(at, zone);
            return At(DateOnly.FromDateTime(local.DateTime).AddDays(days), local.Hour * 60 + local.Minute, zone).AddSeconds(local.Second).AddTicks(local.Ticks % TimeSpan.TicksPerSecond);
        }
        var moves = released.GroupBy(x => x.TaskId).ToDictionary(group => group.Key, group =>
        {
            var task = tasks[group.Key];
            var near = task.Deadline - now <= NearDeadlineWindow;
            var latest = near ? task.Deadline : group.Min(x => Shift(x.End, MaximumUrgentDisplacementDays));
            if (latest > task.Deadline) latest = task.Deadline;
            var successors = state.Dependencies.Where(x => x.PredecessorId == task.Id).Select(x => x.TaskId).ToHashSet();
            foreach (var dependent in state.Sessions.Where(x => retained.Contains(x.Id) && successors.Contains(x.TaskId) && x.Start >= start))
                if (dependent.Start < latest) latest = dependent.Start;
            return new UrgentMove(group.Min(x => x.Start), near ? start : group.Max(x => x.Start), latest, near);
        });
        if (protection is not null) retained.UnionWith(protection.RetainedSessionIds);
        var policy = new UrgentReplanPolicy(retained, moves, protection?.ProtectedTaskIds, protection?.RequestedUrgentTaskIds ?? urgent);
        var affected = input.AffectedTaskIds is null ? null : selected!.Concat(closure).Distinct().ToArray();
        var request = input with { AffectedTaskIds = affected };
        var plan = GenerateCore(state, request, now, policy);
        // Urgent-first greedy placement must not manufacture a missed near
        // deadline when deadline-first can complete both jobs safely.
        if (plan.Warnings.Any(x => moves.ContainsKey(x.TaskId) && x.UnscheduledMinutes > 0))
        {
            var fallback = GenerateCore(state, request, now, policy, urgentFirst: false);
            if (!fallback.Warnings.Any(x => urgent.Contains(x.TaskId) && x.UnscheduledMinutes > 0) &&
                fallback.Warnings.Sum(x => x.UnscheduledMinutes) < plan.Warnings.Sum(x => x.UnscheduledMinutes)) plan = fallback;
        }
        return plan with { HasUrgentReplan = true, Explanations = plan.Explanations.Append(
            "Việc khẩn được ưu tiên. Việc còn hạn trong 48 giờ dời sát lịch cũ và vẫn kịp hạn; việc còn hạn xa ưu tiên ngày kế tiếp, tối đa 2 ngày. Giữ các phiên không liên quan, phiên khóa và đang thực hiện; không đổi hạn chót.").ToArray() };
    }

    private static IEnumerable<TimeSlot> DisplacedWindows(IEnumerable<TimeSlot> windows, UrgentMove move,
        int remaining, bool splittable, int maximumSessionMinutes, TimeZoneInfo zone)
    {
        var pieces = new List<TimeSlot>();
        var length = TimeSpan.FromMinutes(splittable ? Math.Min(remaining, maximumSessionMinutes) : remaining);
        var anchorLocal = TimeZoneInfo.ConvertTime(move.Anchor, zone);
        foreach (var window in windows)
        {
            var from = window.Start > move.Earliest ? window.Start : move.Earliest;
            var to = window.End < move.Latest ? window.End : move.Latest;
            if (to <= from) continue;
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(from, zone).DateTime);
            var desired = move.NearDeadline ? move.Anchor : At(day, anchorLocal.Hour * 60 + anchorLocal.Minute, zone);
            var latest = to - length;
            var nearest = latest < from || desired < from ? from : desired > latest ? latest : desired;
            pieces.Add(new(nearest, to));
            if (nearest > from) pieces.Add(new(from, nearest));
        }
        int DayDistance(TimeSlot slot) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(slot.Start, zone).DateTime).DayNumber - DateOnly.FromDateTime(anchorLocal.DateTime).DayNumber;
        int Rank(TimeSlot slot) => move.NearDeadline ? 0 : DayDistance(slot) switch { 1 => 0, 2 => 1, _ => 2 };
        long Distance(TimeSlot slot)
        {
            var desired = move.NearDeadline ? move.Anchor : At(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(slot.Start, zone).DateTime), anchorLocal.Hour * 60 + anchorLocal.Minute, zone);
            return Math.Abs((slot.Start - desired).Ticks);
        }
        return pieces.Distinct().OrderBy(Rank).ThenBy(Distance).ThenBy(x => x.Start < move.Anchor ? 1 : 0).ThenBy(x => x.Start);
    }
}
