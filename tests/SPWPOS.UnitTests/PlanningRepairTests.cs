using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed class PlanningRepairTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    private readonly PlanningEngine engine = new();
    private static WorkTask Work(string name, int minutes, int deadlineHour, bool splittable = true) => new()
    {
        Name = name, EstimatedMinutes = minutes, Deadline = Now.AddHours(deadlineHour - 8),
        Splittable = splittable, MinimumSessionMinutes = 25, IsHardDeadline = true
    };
    private static PlanningState State(params WorkTask[] tasks) => new(tasks, [], [
        new() { DayOfWeek = (int)Now.DayOfWeek, StartMinute = 540, EndMinute = 600 },
        new() { DayOfWeek = (int)Now.DayOfWeek, StartMinute = 660, EndMinute = 690 }], [], [], [],
        new() { TimeZoneId = "UTC", MinimumSessionMinutes = 25, MaximumSessionMinutes = 90,
            MaximumDailyMinutes = 480, BreakMinutes = 0, PreferredStartMinute = 0, PreferredEndMinute = 1440 });
    private PlanView Generate(PlanningState state) => engine.Generate(state,
        new(Now, 1, UseCapacityFactor: false, UseEstimateBias: false), Now);

    [Fact]
    public void Short_earlier_deadline_does_not_consume_the_only_continuous_slot_for_long_work()
    {
        var a = Work("A", 30, 12); var b = Work("B", 60, 13, false);
        var plan = Generate(State(a, b));
        Assert.Empty(plan.Warnings);
        Assert.Equal(Now.AddHours(1), Assert.Single(plan.Sessions, x => x.TaskId == b.Id).Start);
        Assert.Equal(Now.AddHours(3), Assert.Single(plan.Sessions, x => x.TaskId == a.Id).Start);
        Assert.Equal(90, plan.Sessions.Sum(x => (x.End - x.Start).TotalMinutes));
        var analysis = engine.Analyze(State(a, b), new(Now, 1, UseCapacityFactor: false, UseEstimateBias: false), Now);
        Assert.DoesNotContain(analysis.Tasks, x => x.Reasons.Contains("DEADLINE_CAPACITY_DEFICIT"));
    }

    [Fact]
    public void Repair_also_handles_a_splittable_task_with_a_restricted_work_window()
    {
        var a = Work("Flexible", 30, 12); var b = Work("Fixed work window", 60, 13);
        b.WorkWindowStart = Now.AddHours(1); b.WorkWindowEnd = Now.AddHours(2);
        var plan = Generate(State(a, b));
        Assert.Empty(plan.Warnings);
        Assert.Equal(b.WorkWindowStart, Assert.Single(plan.Sessions, x => x.TaskId == b.Id).Start);
    }

    [Fact]
    public void Repair_promotes_prerequisites_with_the_blocked_task()
    {
        var a = Work("A", 30, 12); var prerequisite = Work("Prepare", 30, 13);
        var b = Work("B", 60, 14, false);
        var state = State(a, prerequisite, b) with {
            Availability = [new() { DayOfWeek = (int)Now.DayOfWeek, StartMinute = 510, EndMinute = 600 },
                new() { DayOfWeek = (int)Now.DayOfWeek, StartMinute = 660, EndMinute = 690 }],
            Dependencies = [new() { TaskId = b.Id, PredecessorId = prerequisite.Id }]
        };
        var plan = Generate(state);
        Assert.Empty(plan.Warnings);
        var preparation = Assert.Single(plan.Sessions, x => x.TaskId == prerequisite.Id);
        var work = Assert.Single(plan.Sessions, x => x.TaskId == b.Id);
        Assert.True(preparation.End <= work.Start);
        Assert.Equal(Now.AddHours(1), work.Start);
        Assert.Equal(Now.AddHours(3), Assert.Single(plan.Sessions, x => x.TaskId == a.Id).Start);
    }

    [Fact]
    public void Repair_does_not_sacrifice_a_scheduled_task_when_the_work_really_cannot_fit()
    {
        var a = Work("Earlier deadline", 60, 12); var b = Work("Later deadline", 60, 13, false);
        var plan = Generate(State(a, b));
        Assert.Equal(60, plan.Sessions.Where(x => x.TaskId == a.Id).Sum(x => (x.End - x.Start).TotalMinutes));
        Assert.Contains(plan.Warnings, x => x.TaskId == b.Id && x.UnscheduledMinutes > 0);
        Assert.DoesNotContain(plan.Warnings, x => x.TaskId == a.Id);
    }

    [Fact]
    public void Repair_keeps_locked_sessions_and_input_records_unchanged()
    {
        var a = Work("A", 30, 12); var b = Work("B", 60, 13, false);
        var keptTask = Work("Locked", 30, 14);
        var kept = new WorkSession { TaskId = keptTask.Id, IsLocked = true, Start = Now.AddHours(1), End = Now.AddHours(1.5) };
        var state = State(a, b, keptTask) with { Sessions = [kept] };
        var plan = Generate(state);
        Assert.Contains(plan.Sessions, x => x.Id == kept.Id && x.Preserved && x.Start == kept.Start && x.End == kept.End);
        Assert.Contains(plan.Warnings, x => x.TaskId == b.Id && x.UnscheduledMinutes > 0);
        Assert.Equal(WorkSessionStatus.Scheduled, kept.Status);
        Assert.Equal(0, a.Progress); Assert.Equal(0, b.Progress);
    }

    [Fact]
    public void Repair_cannot_bypass_the_daily_budget()
    {
        var a = Work("A", 30, 12); var b = Work("B", 60, 13, false);
        var state = State(a, b); state.Preference.MaximumDailyMinutes = 60;
        var plan = Generate(state);
        Assert.True(plan.Sessions.Sum(x => (x.End - x.Start).TotalMinutes) <= 60);
        Assert.Contains(plan.Warnings, x => x.UnscheduledMinutes > 0);
    }

    [Fact]
    public void Repair_does_not_trade_a_missing_task_for_a_new_soft_deadline_violation()
    {
        var a = Work("A", 30, 10); a.IsHardDeadline = false;
        var b = Work("B", 60, 13, false);
        var plan = Generate(State(a, b));
        Assert.DoesNotContain(plan.Warnings, x => x.Code == "SOFT_DEADLINE_EXCEEDED");
        Assert.Contains(plan.Warnings, x => x.TaskId == b.Id && x.UnscheduledMinutes > 0);
        Assert.All(plan.Sessions.Where(x => x.TaskId == a.Id), x => Assert.True(x.End <= a.Deadline));
    }
}
