using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed class PlanningObjectiveTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-28T08:00:00Z");
    private readonly PlanningEngine engine = new();
    private static WorkPreference Preference => new() { TimeZoneId = "UTC", MaximumSessionMinutes = 120, MinimumSessionMinutes = 25, BreakMinutes = 0, PreferredPeriod = "AFTERNOON" };
    private static WorkTask Task => new() { Name = "Viết", EstimatedMinutes = 60, Deadline = Now.AddHours(10), IsHardDeadline = true };
    private static PlanningState State(WorkTask task) => new([task], [], Enumerable.Range(0,7).Select(day => new WeeklyAvailability { DayOfWeek = day, StartMinute = 480, EndMinute = 1080 }).ToArray(), [], [], [], Preference);

    [Fact] public void Planner_selects_lower_penalty_instead_of_first_free_window()
    {
        var task = Task; var state = State(task);
        var plan = engine.Generate(state, new(Now, 1, UseCapacityFactor: false, UseEstimateBias: false), Now);
        Assert.Equal(Now.AddHours(4), Assert.Single(plan.Sessions).Start);
        var morning = engine.ScoreCandidate(task, new(Now, Now.AddHours(4)), 60, Preference, Now, 480);
        var afternoon = engine.ScoreCandidate(task, new(Now.AddHours(4), Now.AddHours(10)), 60, Preference, Now, 480);
        Assert.True(afternoon.PenaltyScore < morning.PenaltyScore);
    }
    [Fact] public void Scoring_prefers_complete_sessions_and_heavily_penalizes_lateness()
    {
        var task = Task; var preference = Preference; preference.PreferredPeriod = "NONE";
        var shortSlot = engine.ScoreCandidate(task, new(Now, Now.AddMinutes(30)), 60, preference, Now, 480);
        var complete = engine.ScoreCandidate(task, new(Now.AddHours(1), Now.AddHours(2)), 60, preference, Now, 480);
        var late = engine.ScoreCandidate(task, new(task.Deadline!.Value, task.Deadline.Value.AddHours(1)), 60, preference, Now, 480);
        Assert.True(complete.PenaltyScore < shortSlot.PenaltyScore); Assert.True(late.PenaltyScore > shortSlot.PenaltyScore);
    }
    [Fact] public void Hard_deadline_overrides_productive_hours()
    {
        var task = Task; task.Deadline = Now.AddHours(2);
        var plan = engine.Generate(State(task), new(Now, 1, UseCapacityFactor: false, UseEstimateBias: false), Now);
        Assert.Empty(plan.Warnings); Assert.True(Assert.Single(plan.Sessions).End <= task.Deadline);
    }
    [Fact] public void Bias_uses_chronological_ema_and_clamps_extreme_samples()
    {
        var rows = new[] { 1d, 1d, 2d }.Select((ratio, i) => new EstimationObservation { TaskType = "WRITING", EstimatedMinutes = 60, ActualMinutes = 60 * ratio, ObservedAt = Now.AddDays(i) }).ToArray();
        var state = State(Task) with { Observations = rows.Reverse().ToArray() };
        Assert.Equal(1.2, engine.Biases(state)[0].Ratio);
        rows[2].ActualMinutes = 60000;
        Assert.Equal(3, engine.Biases(state)[0].Ratio); // Clamp the EMA, not the raw observation before averaging.
        foreach (var row in rows) row.ActualMinutes = 100000;
        Assert.Equal(3, engine.Biases(state)[0].Ratio);
        foreach (var row in rows) row.ActualMinutes = .01;
        Assert.Equal(.5, engine.Biases(state)[0].Ratio);
    }
}
