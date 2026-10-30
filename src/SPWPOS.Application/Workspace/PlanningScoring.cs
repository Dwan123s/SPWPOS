using SPWPOS.Domain.Entities;
namespace SPWPOS.Application.Workspace;

public sealed record ScheduleSlotCandidate(DateTimeOffset Start, DateTimeOffset End,
    double LatenessPenalty, double IdleTimePenalty, double FragmentationPenalty, double PreferredPeriodBonus)
{
    // Lower is better. Hard constraints are checked before this soft objective.
    public double PenaltyScore => 10000 * LatenessPenalty + 2 * IdleTimePenalty + 1000 * FragmentationPenalty - 500 * PreferredPeriodBonus;
}

public sealed partial class PlanningEngine
{
    public ScheduleSlotCandidate ScoreCandidate(WorkTask task, TimeSlot window, int remaining,
        WorkPreference preference, DateTimeOffset earliest, int budget)
    {
        var start = window.Start > earliest ? window.Start : earliest;
        var available = Math.Min(Math.Max(0, (window.End - start).TotalMinutes), Math.Max(0, budget));
        var duration = Math.Min(remaining, Math.Min(available, preference.MaximumSessionMinutes));
        if (duration <= 0 || !task.Splittable && duration < remaining)
            return new(start, start, double.MaxValue / 10000, 0, 0, 0);
        var end = start.AddMinutes(duration);
        var slack = (task.Deadline - end).TotalMinutes;
        // Strong lateness cost; a small reciprocal cost also discourages finishing
        // immediately against a deadline. No soft score can relax a hard deadline.
        var lateness = Math.Max(0, -slack) + (slack >= 0 ? 1 / (slack + 1) : 1);
        var tail = (window.End - end).TotalMinutes;
        var fragmentation = Math.Max(0, remaining - duration) / Math.Max(1, remaining);
        var idle = (tail > 0 && tail < preference.MinimumSessionMinutes ? tail : 0) +
            Math.Max(0, (start - earliest).TotalMinutes) / 1440;
        var zone = Zone(preference.TimeZoneId);
        var local = TimeZoneInfo.ConvertTime(start, zone);
        var day = DateOnly.FromDateTime(local.DateTime);
        var hours = preference.PreferredPeriod switch { "MORNING" => (360, 720), "AFTERNOON" => (720, 1080),
            "EVENING" => (1080, 1440), _ => (preference.PreferredStartMinute, preference.PreferredEndMinute) };
        var preferred = start >= At(day, hours.Item1, zone) && end <= At(day, hours.Item2, zone) ? 1 : 0;
        return new(start, end, lateness, idle, fragmentation, preferred);
    }
}
