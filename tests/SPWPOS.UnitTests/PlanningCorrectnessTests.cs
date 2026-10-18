using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed class PlanningCorrectnessTests
{
    [Fact]
    public void Selected_task_uses_only_Monday_free_windows_in_Vietnam_and_preserves_other_work()
    {
        var now = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero); // Monday 07:00 in Vietnam.
        var selected = new WorkTask { Name = "Selected", EstimatedMinutes = 180, Deadline = now.AddHours(12), MinimumSessionMinutes = 25, IsHardDeadline = true };
        var other = new WorkTask { Name = "Other", EstimatedMinutes = 60, Deadline = now.AddHours(12) };
        var occupied = new WorkSession { TaskId = other.Id, Start = now.AddHours(1), End = now.AddHours(2) };
        var state = new PlanningState([selected, other], [], [new() { DayOfWeek = 1, StartMinute = 420, EndMinute = 540 }, new() { DayOfWeek = 1, StartMinute = 900, EndMinute = 1020 }], [], [], [occupied],
            new() { TimeZoneId = "Asia/Ho_Chi_Minh", MaximumDailyMinutes = 480, MaximumSessionMinutes = 120, BreakMinutes = 0, PreferredStartMinute = 420, PreferredEndMinute = 1020 });
        var plan = engine.Generate(state, new(now, 1, [selected.Id], true, UseCapacityFactor: false), now);
        Assert.Empty(plan.Warnings);
        Assert.Contains(plan.Sessions, s => s.Id == occupied.Id && s.Preserved);
        var sessions = plan.Sessions.Where(s => s.TaskId == selected.Id).ToArray();
        Assert.Equal(180, sessions.Sum(s => (s.End - s.Start).TotalMinutes));
        Assert.All(sessions, s => Assert.True(s.Start >= now && s.End <= now.AddHours(1) || s.Start >= now.AddHours(8) && s.End <= now.AddHours(10)));
    }
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
    private readonly PlanningEngine engine = new();
    private static WorkTask Work(string name, int minutes = 60) => new() { Name = name, EstimatedMinutes = minutes, MinimumSessionMinutes = 25, Deadline = Now.AddHours(10), IsHardDeadline = true };
    private static PlanningState State(params WorkTask[] tasks) => new(tasks, [], [new() { DayOfWeek = 4, StartMinute = 480, EndMinute = 1080 }], [], [], [], new() { TimeZoneId = "UTC", MaximumDailyMinutes = 480, MaximumSessionMinutes = 240, BreakMinutes = 0 });

    [Fact] public void Task_risk_respects_daily_cap_when_the_calendar_has_more_free_time()
    {
        var state = State(Work("Report", 120)); state.Preference.MaximumDailyMinutes = 60;
        var result = engine.Analyze(state, new(Now, 1), Now);
        Assert.Equal(60, result.EffectiveCapacityMinutes);
        Assert.Equal("High", Assert.Single(result.Tasks).Level);
        Assert.Contains("DEADLINE_CAPACITY_DEFICIT", result.Tasks[0].Reasons);
    }
    [Fact] public void Parent_completion_unblocks_downstream_after_all_children_are_scheduled()
    {
        var parent = Work("Project"); var child = Work("Write"); child.ParentId = parent.Id; var next = Work("Submit");
        var state = State(parent, child, next) with { Dependencies = [new() { TaskId = next.Id, PredecessorId = parent.Id }] };
        var plan = engine.Generate(state, new(Now, 1), Now);
        Assert.Empty(plan.Warnings);
        Assert.Equal(2, plan.Sessions.Length);
        Assert.True(plan.Sessions.Single(x => x.TaskId == next.Id).Start >= plan.Sessions.Single(x => x.TaskId == child.Id).End);
        Assert.DoesNotContain(plan.Sessions, x => x.TaskId == parent.Id);
    }
    [Fact] public void Partial_child_does_not_satisfy_parent_dependency()
    {
        var parent = Work("Project"); var child = Work("Write", 600); child.ParentId = parent.Id; var next = Work("Submit");
        var state = State(parent, child, next) with { Dependencies = [new() { TaskId = next.Id, PredecessorId = parent.Id }] };
        var plan = engine.Generate(state, new(Now, 1), Now);
        Assert.Contains(plan.Warnings, x => x.TaskId == next.Id && x.Code == "DEPENDENCY_UNSCHEDULED");
        Assert.DoesNotContain(plan.Sessions, x => x.TaskId == next.Id);
    }
    [Fact] public void Rescheduling_a_child_includes_tasks_depending_on_the_container()
    {
        var parent = Work("Project"); var child = Work("Write"); child.ParentId = parent.Id; var next = Work("Submit");
        var old = new WorkSession { TaskId = next.Id, Start = Now.AddHours(1), End = Now.AddHours(2) };
        var state = State(parent, child, next) with { Dependencies = [new() { TaskId = next.Id, PredecessorId = parent.Id }], Sessions = [old] };
        var plan = engine.Generate(state, new(Now, 1, [child.Id]), Now);
        Assert.Empty(plan.Warnings);
        Assert.DoesNotContain(plan.Sessions, x => x.Id == old.Id && x.Preserved);
        Assert.Contains(plan.Sessions, x => x.TaskId == next.Id && !x.Preserved);
    }
    [Fact] public void Completed_work_and_elapsed_hours_do_not_reduce_capacity_twice()
    {
        var completed = Work("Done", 120); completed.Status = WorkTaskStatus.Completed;
        var next = Work("Rest of day", 300);
        var state = State(completed, next) with { Sessions = [new() { TaskId = completed.Id, Start = Now, End = Now.AddHours(2), Status = WorkSessionStatus.Completed, ActualSeconds = 7200 }] };
        var noon = Now.AddHours(4);
        var result = engine.Analyze(state, new(noon, 1), noon);
        Assert.Equal(360, result.EffectiveCapacityMinutes);
        var plan = engine.Generate(state, new(noon, 1), noon);
        Assert.Empty(plan.Warnings);
        Assert.Equal(300, plan.Sessions.Sum(x => (x.End - x.Start).TotalMinutes));
    }
}
