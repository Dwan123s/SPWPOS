using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed class DeadlineProtectionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T08:00:00Z");
    private static WorkTask Work(string name, int hours, int priority = 2) => new() {
        Name = name, Deadline = Now.AddHours(hours), Priority = priority, EstimatedMinutes = 60,
        MinimumSessionMinutes = 25, Splittable = false, IsHardDeadline = true
    };
    private static PlanningState State(params WorkTask[] tasks) => new(tasks, [],
        Enumerable.Range(0, 7).Select(day => new WeeklyAvailability { DayOfWeek = day, StartMinute = 480, EndMinute = 600 }).ToArray(),
        [], [], [], new() { TimeZoneId = "UTC", BreakMinutes = 0, MaximumDailyMinutes = 480, MaximumSessionMinutes = 120 });
    private static PlanView Plan(PlanningState state, WorkTask added) => new PlanningEngine().Generate(state,
        new(Now, 7, [added.Id], true, UseCapacityFactor: false, UseEstimateBias: false), Now);

    [Theory] [InlineData(2, 2)] [InlineData(4, 96)]
    public void New_task_reserves_time_for_unscheduled_near_deadline_or_urgent_work(int priority, int hours)
    {
        var old = Work("Việc cần ưu tiên", hours, priority); var added = Work("Việc mới", 120);
        var plan = Plan(State(old, added), added);
        Assert.Empty(plan.Warnings);
        Assert.Equal(Now, Assert.Single(plan.Sessions, x => x.TaskId == old.Id).Start);
        Assert.Equal(Now.AddHours(1), Assert.Single(plan.Sessions, x => x.TaskId == added.Id).Start);
        Assert.True(plan.HasDeadlineProtection); Assert.True(plan.RequiresConfirmation);
    }

    [Fact]
    public void New_task_cannot_take_the_only_hour_available_for_a_near_deadline()
    {
        var old = Work("Hạn trong một giờ", 1); var added = Work("Việc mới", 120);
        added.WorkWindowStart = Now; added.WorkWindowEnd = Now.AddHours(1);
        var plan = Plan(State(old, added), added);
        Assert.Equal(Now, Assert.Single(plan.Sessions, x => x.TaskId == old.Id).Start);
        Assert.Contains(plan.Warnings, x => x.TaskId == added.Id && x.UnscheduledMinutes == 60);
    }

    [Fact]
    public void Existing_partial_sessions_are_kept_and_only_the_uncovered_work_is_added()
    {
        var old = Work("Việc đang xếp dở", 2); old.EstimatedMinutes = 120; old.Splittable = true;
        var added = Work("Việc mới", 120);
        var saved = new WorkSession { TaskId = old.Id, Start = Now, End = Now.AddHours(1) };
        var plan = Plan(State(old, added) with { Sessions = [saved] }, added);
        Assert.Contains(plan.Sessions, x => x.Id == saved.Id && x.Preserved);
        Assert.Equal(Now.AddHours(1), Assert.Single(plan.Sessions, x => x.TaskId == old.Id && !x.Preserved).Start);
        Assert.Equal(Now.AddDays(1), Assert.Single(plan.Sessions, x => x.TaskId == added.Id).Start);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void Prerequisites_of_protected_work_are_scheduled_too()
    {
        var predecessor = Work("Chuẩn bị", 1); predecessor.Priority = 1;
        var old = Work("Việc gần hạn", 2); var added = Work("Việc mới", 120);
        var state = State(predecessor, old, added) with { Dependencies = [new() { TaskId = old.Id, PredecessorId = predecessor.Id }] };
        var plan = Plan(state, added);
        Assert.Empty(plan.Warnings);
        Assert.Equal(Now, Assert.Single(plan.Sessions, x => x.TaskId == predecessor.Id).Start);
        Assert.Equal(Now.AddHours(1), Assert.Single(plan.Sessions, x => x.TaskId == old.Id).Start);
    }

    [Theory] [InlineData("covered")] [InlineData("distant")] [InlineData("completed")] [InlineData("cancelled")] [InlineData("expired")]
    public void Work_that_needs_no_deadline_protection_does_not_expand_the_proposal(string mode)
    {
        var old = Work("Việc cũ", mode == "distant" ? 120 : mode == "expired" ? -1 : 2);
        if (mode == "completed") old.Status = WorkTaskStatus.Completed;
        if (mode == "cancelled") old.Status = WorkTaskStatus.Cancelled;
        var added = Work("Việc mới", 120);
        var state = State(old, added);
        if (mode == "covered") state = state with { Sessions = [new() { TaskId = old.Id, Start = Now, End = Now.AddHours(1) }] };
        var plan = Plan(state, added);
        Assert.DoesNotContain(plan.Sessions, x => x.TaskId == old.Id && !x.Preserved);
        Assert.Empty(plan.Warnings);
        Assert.False(plan.HasDeadlineProtection);
    }

    [Fact]
    public void An_urgent_new_task_still_leaves_the_only_early_hour_to_an_existing_deadline_when_both_can_fit()
    {
        var old = Work("Hạn sớm", 1); var added = Work("Việc khẩn mới", 2, 4);
        var plan = Plan(State(old, added), added);
        Assert.Empty(plan.Warnings);
        Assert.Equal(Now, Assert.Single(plan.Sessions, x => x.TaskId == old.Id).Start);
        Assert.Equal(Now.AddHours(1), Assert.Single(plan.Sessions, x => x.TaskId == added.Id).Start);
    }

    [Fact]
    public void Existing_near_soft_deadline_cannot_be_silently_exceeded_to_fit_new_work()
    {
        var old = Work("Hạn mềm", 1); old.IsHardDeadline = false;
        old.EstimatedMinutes = 120; old.Splittable = true;
        var added = Work("Việc mới", 120);
        var plan = Plan(State(old, added), added);
        Assert.Contains(plan.Warnings, x => x.TaskId == old.Id && x.UnscheduledMinutes == 60);
        Assert.DoesNotContain(plan.Sessions, x => x.TaskId == old.Id && x.End > old.Deadline);
    }

    [Fact]
    public void Priority_does_not_manufacture_a_shortage_when_a_new_earlier_deadline_and_old_urgent_work_can_both_fit()
    {
        var old = Work("Việc khẩn", 96, 4); var added = Work("Hạn trong một giờ", 1);
        var plan = Plan(State(old, added), added);
        Assert.Empty(plan.Warnings);
        Assert.Equal(Now, Assert.Single(plan.Sessions, x => x.TaskId == added.Id).Start);
        Assert.Equal(Now.AddHours(1), Assert.Single(plan.Sessions, x => x.TaskId == old.Id).Start);
    }

    [Fact]
    public void An_existing_session_after_a_soft_deadline_cannot_be_counted_as_valid_deadline_coverage()
    {
        var old = Work("Việc đã trễ", 1); old.IsHardDeadline = false;
        var added = Work("Việc mới", 120);
        var late = new WorkSession { TaskId = old.Id, Start = Now.AddHours(1), End = Now.AddHours(2) };
        var plan = Plan(State(old, added) with { Sessions = [late] }, added);
        Assert.True(plan.HasDeadlineProtection);
        Assert.Contains(plan.Warnings, x => x.TaskId == old.Id && x.Code == "SOFT_DEADLINE_EXCEEDED");
        Assert.Contains(plan.Sessions, x => x.Id == late.Id && x.Preserved);
    }
}
