using SPWPOS.Domain.Entities;
namespace SPWPOS.Application.Workspace;

public sealed partial class PlanningEngine
{
    // EMA of chronological local-day completion rates. Scheduled/NeedsReview
    // sessions are not failures until the user confirms their actual outcome.
    // Alpha=.2, the five-session cold start and clamps are product policies.
    public (double Factor, int Samples) Capacity(PlanningState state, DateTimeOffset now)
    {
        var elapsed = state.Sessions.Where(x => x.End < now && x.End > x.Start &&
            x.Status is WorkSessionStatus.Completed or WorkSessionStatus.Skipped or WorkSessionStatus.Missed).ToArray();
        var zone = Zone(state.Preference.TimeZoneId);
        var dailyRates = elapsed.GroupBy(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.Start, zone).DateTime)).OrderBy(x => x.Key)
            .Select(day => day.Where(x => x.Status == WorkSessionStatus.Completed).Sum(x => (x.End - x.Start).TotalMinutes) /
                day.Sum(x => (x.End - x.Start).TotalMinutes));
        return (elapsed.Length < 5 ? ColdCapacityFactor : Math.Clamp(AdaptiveLearning.Ema(dailyRates, ColdCapacityFactor), .25, 1), elapsed.Length);
    }

    // A daily work budget is calculated from the complete local day. Clipping the
    // requested range must not apply the capacity factor to the remainder again.
    // Consumed minutes are planned occupancy already completed/started, not a
    // productivity claim; actual focus is reported separately by execution data.
    public CapacityDayView[] CapacityDays(PlanningState state, DateTimeOffset start, DateTimeOffset end,
        DateTimeOffset now, bool useCapacityFactor = true, IEnumerable<WorkSession>? reservations = null)
    {
        if (end <= start) return [];
        var zone = Zone(state.Preference.TimeZoneId);
        var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, zone).DateTime);
        var last = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(end.AddTicks(-1), zone).DateTime);
        var factor = useCapacityFactor ? Capacity(state, now).Factor : 1;
        var reserved = (reservations ?? state.Sessions.Where(x => x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused)).ToArray();
        var completed = state.Sessions.Where(x => x.Status == WorkSessionStatus.Completed).ToArray();
        var blocks = reserved.Concat(completed).Select(x => new TimeSlot(x.Start.AddMinutes(-state.Preference.BreakMinutes), x.End.AddMinutes(state.Preference.BreakMinutes))).ToArray();
        var result = new List<CapacityDayView>();
        static int Minutes(IEnumerable<TimeSlot> slots) => (int)Math.Floor(Merge(slots).Sum(x => (x.End - x.Start).TotalMinutes));
        static TimeSlot? Clip(DateTimeOffset from, DateTimeOffset to, DateTimeOffset a, DateTimeOffset b)
        { var left = from > a ? from : a; var right = to < b ? to : b; return right > left ? new(left, right) : null; }
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var dayStart = At(day, 0, zone); var dayEnd = At(day.AddDays(1), 0, zone);
            var rangeStart = start > dayStart ? start : dayStart; var rangeEnd = end < dayEnd ? end : dayEnd;
            var fullFree = Minutes(FreeSlots(state, dayStart, dayEnd));
            var budget = Math.Min(state.Preference.MaximumDailyMinutes, (int)Math.Floor(fullFree * factor));
            var consumedSlots = completed.Select(x => Clip(x.Start, x.End, dayStart, dayEnd))
                .Concat(state.Sessions.Where(x => x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused)
                    .Select(x => Clip(x.Start, x.End < start ? x.End : start, dayStart, dayEnd)))
                .OfType<TimeSlot>();
            var consumed = Minutes(consumedSlots);
            var reservedMinutes = Minutes(reserved.Select(x => Clip(x.Start, x.End, rangeStart, rangeEnd)).OfType<TimeSlot>());
            var freeMinutes = Minutes(FreeSlots(state, rangeStart, rangeEnd));
            var remainingBudget = Math.Max(0, budget - consumed);
            var effective = Math.Min(freeMinutes, remainingBudget);
            var schedulable = Math.Min(Minutes(FreeSlots(state, rangeStart, rangeEnd, blocks)), Math.Max(0, remainingBudget - reservedMinutes));
            result.Add(new(day, freeMinutes, budget, consumed, reservedMinutes, effective, schedulable));
        }
        return result.ToArray();
    }

    public static HashSet<Guid> AffectedClosure(PlanningState state, IEnumerable<Guid> requested)
    {
        var affected = requested.ToHashSet();
        // Explicitly requesting a container means requesting its descendants.
        bool changed;
        do
        {
            changed = false;
            foreach (var task in state.Tasks.Where(x => !x.IsDeleted && x.ParentId != null))
                if (affected.Contains(task.ParentId!.Value)) changed |= affected.Add(task.Id);
        } while (changed);
        // A changed child affects the completion of its container and all
        // downstream dependencies; it must not reschedule unrelated siblings.
        do
        {
            changed = false;
            foreach (var task in state.Tasks.Where(x => !x.IsDeleted && x.ParentId != null))
                if (affected.Contains(task.Id)) changed |= affected.Add(task.ParentId!.Value);
            foreach (var edge in state.Dependencies)
                if (affected.Contains(edge.PredecessorId)) changed |= affected.Add(edge.TaskId);
        } while (changed);
        return affected;
    }

    // Shared by generation and apply validation. Containers have no sessions of
    // their own; their predicted finish includes every required child and their
    // explicit prerequisites. Partial work never satisfies a dependency.
    public Dictionary<Guid, DateTimeOffset> CompletionTimes(PlanningState state, IEnumerable<ProposedSession> sessions,
        BiasView[] biases, DateTimeOffset now)
    {
        var tasks = state.Tasks.Where(x => !x.IsDeleted && x.Status != WorkTaskStatus.Cancelled).ToArray();
        var done = tasks.Where(x => x.Status == WorkTaskStatus.Completed).ToDictionary(x => x.Id, _ => now);
        var children = tasks.Where(x => x.ParentId != null).ToLookup(x => x.ParentId!.Value);
        var dependencies = state.Dependencies.ToLookup(x => x.TaskId, x => x.PredecessorId);
        var planned = sessions.DistinctBy(x => x.Id).ToLookup(x => x.TaskId);
        bool changed;
        do
        {
            changed = false;
            foreach (var task in tasks.Where(x => !done.ContainsKey(x.Id)))
            {
                var prerequisites = dependencies[task.Id].ToArray();
                if (prerequisites.Any(x => !done.ContainsKey(x))) continue;
                var earliest = prerequisites.Select(x => done[x]).DefaultIfEmpty(now).Max();
                var required = children[task.Id].ToArray();
                DateTimeOffset finish;
                if (required.Length > 0)
                {
                    if (required.Any(x => !done.ContainsKey(x.Id))) continue;
                    finish = required.Select(x => done[x.Id]).Append(earliest).Max();
                }
                else
                {
                    var work = planned[task.Id].ToArray();
                    if (work.Length == 0 || work.Sum(x => (x.End - x.Start).TotalMinutes) < Remaining(task, biases) ||
                        prerequisites.Length > 0 && work.Any(x => x.Start < earliest)) continue;
                    finish = work.Max(x => x.End);
                }
                done[task.Id] = finish; changed = true;
            }
        } while (changed);
        return done;
    }
}
