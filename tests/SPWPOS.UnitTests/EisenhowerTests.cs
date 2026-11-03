using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed class EisenhowerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-02T08:00:00Z");
    private readonly PlanningEngine engine = new();
    private static WorkTask Task(string importance = "Important", int hours = 72, int minutes = 60) => new() {
        Name = "Proposal", ImportanceLevel = importance, Deadline = Now.AddHours(hours), EstimatedMinutes = minutes, IsHardDeadline = true };
    private static PlanningState State(params WorkTask[] tasks) => new(tasks, [],
        Enumerable.Range(0,7).Select(day => new WeeklyAvailability { DayOfWeek = day, StartMinute = 480, EndMinute = 1080 }).ToArray(), [],
        [new FixedEvent { Name = "Class", Start = Now.AddHours(1), End = Now.AddHours(2) }], [],
        new WorkPreference { TimeZoneId = "UTC", MinimumSessionMinutes = 25, MaximumSessionMinutes = 90, MaximumDailyMinutes = 480, BreakMinutes = 0 });

    [Theory] [InlineData("Important", 12, "Q1")] [InlineData("Important", 72, "Q2")]
    [InlineData("Lower", 12, "Q3")] [InlineData("Lower", 72, "Q4")]
    public void Matrix_separates_importance_and_urgency(string importance, int hours, string expected)
    {
        var task = Task(importance, hours); var state = State(task);
        var before = System.Text.Json.JsonSerializer.Serialize(state);
        var first = Assert.Single(engine.AssessEisenhower(state, Now));
        Assert.Equal(expected, first.Quadrant);
        Assert.Equal(first, Assert.Single(engine.AssessEisenhower(state, Now)) with { ReasonCodes = first.ReasonCodes });
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(state));
    }
    [Fact] public void Missing_schedule_or_importance_and_far_deadlines_remain_unknown()
    {
        var t = Task();
        var result = Assert.Single(engine.AssessEisenhower(State(t) with { Events = [] }, Now));
        Assert.Equal("Unknown", result.Urgency); Assert.Contains("SCHEDULE_REQUIRED", result.ReasonCodes);
        result = Assert.Single(engine.AssessEisenhower(State(Task("Unknown")), Now)); Assert.Equal("Unknown", result.Quadrant);
        result = Assert.Single(engine.AssessEisenhower(State(Task(hours: 800)), Now)); Assert.Contains("OUTSIDE_HORIZON", result.ReasonCodes);
    }
    [Fact] public void Same_deadline_can_be_safe_or_urgent_for_different_schedules()
    {
        var task = Task(hours: 48, minutes: 180);
        var rich = Assert.Single(engine.AssessEisenhower(State(task), Now));
        var tight = State(task) with { Availability = [new WeeklyAvailability { DayOfWeek = 1, StartMinute = 480, EndMinute = 660 }] };
        var constrained = Assert.Single(engine.AssessEisenhower(tight, Now));
        Assert.Equal("Q2", rich.Quadrant); Assert.Equal("Q1", constrained.Quadrant); Assert.True(constrained.AtRisk);
    }
    [Fact] public void Competing_tasks_cannot_each_claim_the_same_capacity_and_containers_are_not_counted_twice()
    {
        var a = Task(hours: 48, minutes: 120); var b = Task(hours: 48, minutes: 120);
        var state = State(a,b) with { Availability = [new WeeklyAvailability { DayOfWeek = 1, StartMinute = 480, EndMinute = 720 }] };
        var assessment = engine.AssessEisenhower(state, Now); Assert.All(assessment, x => Assert.True(x.AtRisk));
        var parent = Task(); a.ParentId = parent.Id;
        var grouped = engine.AssessEisenhower(State(parent,a), Now); Assert.Single(grouped); Assert.Equal(a.Id, grouped[0].Id);
    }
    [Fact] public void Existing_reserved_work_is_not_double_counted_and_low_impact_deadlines_remain_protected()
    {
        var low = Task("Lower", hours: 3, minutes: 60); var important = Task(hours: 48, minutes: 180);
        var state = State(low,important) with { EisenhowerScheduling = true };
        var baseline = engine.Generate(state with { EisenhowerScheduling = false }, new(Now, 3), Now);
        var plan = engine.Generate(state, new(Now, 3), Now);
        Assert.Empty(plan.Warnings); Assert.Equal(baseline.Sessions.Sum(x => (x.End-x.Start).TotalMinutes), plan.Sessions.Sum(x => (x.End-x.Start).TotalMinutes));
        Assert.All(plan.Sessions.Where(x => x.TaskId == low.Id), session => Assert.True(session.End <= low.Deadline));
        var locked = new WorkSession { TaskId = low.Id, Start = Now, End = Now.AddHours(1), IsLocked = true };
        var protectedPlan = engine.Generate(state with { Sessions = [locked] }, new(Now, 3), Now);
        Assert.Contains(protectedPlan.Sessions, session => session.Id == locked.Id && session.Preserved);
    }
    [Fact] public void Overdue_and_continuous_tasks_have_explicit_risk_without_changing_deadlines()
    {
        var task = Task(hours: -1); var saved = task.Deadline;
        var assessment = Assert.Single(engine.AssessEisenhower(State(task), Now));
        Assert.True(assessment.Overdue); Assert.True(assessment.AtRisk); Assert.Equal(saved, task.Deadline);
        task = Task(hours: 48, minutes: 180); task.Splittable = false;
        assessment = Assert.Single(engine.AssessEisenhower(State(task), Now)); Assert.True(assessment.AtRisk);
    }
}
