using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed class UrgentPlanningTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T08:00:00Z");
    private static WorkTask Task(string name, DateTimeOffset deadline, int priority = 2) => new() {
        Name = name, Deadline = deadline, Priority = priority, EstimatedMinutes = 60, MinimumSessionMinutes = 25,
        IsHardDeadline = true, Splittable = false
    };
    private static PlanningState State(WorkTask old, WorkTask urgent, params WorkSession[] sessions) => new(
        [old, urgent], [], Enumerable.Range(0, 7).Select(x => new WeeklyAvailability { DayOfWeek = x, StartMinute = 480, EndMinute = 1200 }).ToArray(),
        [], [], sessions, new() { TimeZoneId = "UTC", MaximumSessionMinutes = 240, MaximumDailyMinutes = 480, BreakMinutes = 0 });
    private static WorkTask Urgent()
    {
        var task = Task("Việc khẩn", Now.AddHours(4), 4);
        task.WorkWindowStart = Now.AddHours(2); task.WorkWindowEnd = Now.AddHours(3);
        return task;
    }
    private static PlanView Plan(PlanningState state, WorkTask urgent, DateTimeOffset? now = null) =>
        new PlanningEngine().Generate(state, new(now ?? Now, 7, [urgent.Id], true, UseCapacityFactor: false, UseEstimateBias: false), now ?? Now);

    [Theory] [InlineData(true)] [InlineData(false)]
    public void Urgent_work_takes_the_conflicting_slot_and_displaced_work_obeys_its_deadline_policy(bool nearDeadline)
    {
        var old = Task("Việc đã lên lịch", nearDeadline ? Now.AddHours(8) : Now.AddDays(7));
        var urgent = Urgent();
        var original = new WorkSession { TaskId = old.Id, Start = Now.AddHours(2), End = Now.AddHours(3) };
        var unrelated = Task("Giữ nguyên", Now.AddDays(7));
        var kept = new WorkSession { TaskId = unrelated.Id, Start = Now.AddHours(6), End = Now.AddHours(7) };
        var state = State(old, urgent, original, kept) with { Tasks = [old, urgent, unrelated] };
        var plan = Plan(state, urgent);
        Assert.Empty(plan.Warnings);
        var inserted = Assert.Single(plan.Sessions, x => x.TaskId == urgent.Id);
        Assert.Equal(Now.AddHours(2), inserted.Start); Assert.Equal(Now.AddHours(3), inserted.End);
        Assert.Equal("URGENT_TASK_FIRST", inserted.ReasonCode);
        var moved = Assert.Single(plan.Sessions, x => x.TaskId == old.Id);
        Assert.Equal(nearDeadline ? Now.AddHours(3) : Now.AddDays(1).AddHours(2), moved.Start);
        Assert.True(moved.End <= old.Deadline);
        Assert.Equal(nearDeadline ? "URGENT_NEAR_DEADLINE_MOVE" : "URGENT_FLEXIBLE_MOVE", moved.ReasonCode);
        Assert.Contains(plan.Sessions, x => x.Id == kept.Id && x.Preserved && x.Start == kept.Start);
        Assert.Equal(Now.AddHours(2), original.Start); // Planning never mutates saved input.
    }

    [Fact]
    public void Distant_deadline_work_uses_the_second_day_when_the_first_day_is_busy()
    {
        var old = Task("Việc dài hạn", Now.AddDays(7)); var urgent = Urgent();
        var original = new WorkSession { TaskId = old.Id, Start = Now.AddHours(2), End = Now.AddHours(3) };
        var state = State(old, urgent, original) with { Events = [new() { Start = Now.AddDays(1), End = Now.AddDays(1).AddHours(12) }] };
        var plan = Plan(state, urgent);
        Assert.Empty(plan.Warnings);
        Assert.Equal(Now.AddDays(2).AddHours(2), Assert.Single(plan.Sessions, x => x.TaskId == old.Id).Start);
        Assert.Equal(Now.AddDays(7), old.Deadline);
    }

    [Fact]
    public void It_does_not_move_flexible_work_three_days_away_or_extend_its_deadline_when_no_bounded_slot_exists()
    {
        var old = Task("Việc dài hạn", Now.AddDays(7)); var urgent = Urgent();
        var original = new WorkSession { TaskId = old.Id, Start = Now.AddHours(2), End = Now.AddHours(3) };
        var state = State(old, urgent, original) with { Availability = [
            new() { DayOfWeek = (int)Now.DayOfWeek, StartMinute = 600, EndMinute = 660 },
            new() { DayOfWeek = (int)Now.AddDays(3).DayOfWeek, StartMinute = 600, EndMinute = 720 }] };
        var plan = Plan(state, urgent);
        Assert.Contains(plan.Warnings, x => x.TaskId == old.Id && x.UnscheduledMinutes == 60);
        Assert.DoesNotContain(plan.Sessions, x => x.TaskId == old.Id && x.Start >= Now.AddDays(3));
        Assert.Equal(Now.AddDays(7), old.Deadline); Assert.Equal(WorkSessionStatus.Scheduled, original.Status);
    }

    [Theory] [InlineData(WorkSessionStatus.Scheduled)] [InlineData(WorkSessionStatus.Running)] [InlineData(WorkSessionStatus.Paused)]
    public void Locked_or_active_sessions_are_preserved_even_for_urgent_work(WorkSessionStatus status)
    {
        var old = Task("Phiên cần giữ", Now.AddDays(7)); var urgent = Urgent();
        var original = new WorkSession { TaskId = old.Id, Start = Now.AddHours(2), End = Now.AddHours(3), Status = status, IsLocked = status == WorkSessionStatus.Scheduled };
        var plan = Plan(State(old, urgent, original), urgent);
        Assert.Contains(plan.Sessions, x => x.Id == original.Id && x.Preserved);
        Assert.Contains(plan.Warnings, x => x.TaskId == urgent.Id && x.UnscheduledMinutes > 0);
    }

    [Fact]
    public void A_near_deadline_is_protected_when_running_it_first_is_the_only_way_to_complete_both_tasks()
    {
        var old = Task("Sắp hết hạn", Now.AddHours(3)); var urgent = Urgent();
        urgent.WorkWindowEnd = Now.AddHours(4);
        var original = new WorkSession { TaskId = old.Id, Start = Now.AddHours(2), End = Now.AddHours(3) };
        var state = State(old, urgent, original) with { Availability = [new() { DayOfWeek = (int)Now.DayOfWeek, StartMinute = 600, EndMinute = 720 }] };
        var plan = Plan(state, urgent);
        Assert.Empty(plan.Warnings);
        Assert.Equal(original.Start, Assert.Single(plan.Sessions, x => x.TaskId == old.Id).Start);
        Assert.Equal(Now.AddHours(3), Assert.Single(plan.Sessions, x => x.TaskId == urgent.Id).Start);
    }

    [Fact]
    public void An_impossible_collision_reports_the_old_deadline_at_risk_instead_of_silently_sacrificing_it()
    {
        var old = Task("Hết hạn lúc 11h", Now.AddHours(3)); old.IsHardDeadline = false;
        var urgent = Urgent(); var original = new WorkSession { TaskId = old.Id, Start = Now.AddHours(2), End = Now.AddHours(3) };
        var state = State(old, urgent, original) with { Availability = [new() { DayOfWeek = (int)Now.DayOfWeek, StartMinute = 600, EndMinute = 720 }] };
        var plan = Plan(state, urgent);
        Assert.Contains(plan.Warnings, x => x.TaskId == old.Id && x.UnscheduledMinutes > 0);
        Assert.DoesNotContain(plan.Sessions, x => x.TaskId == old.Id && x.End > old.Deadline);
        Assert.Equal(Now.AddHours(3), old.Deadline);
    }

    [Fact]
    public void Breaks_are_preserved_when_near_deadline_work_moves_to_the_next_slot()
    {
        var old = Task("Việc gần hạn", Now.AddHours(8)); var urgent = Urgent();
        var original = new WorkSession { TaskId = old.Id, Start = Now.AddHours(2), End = Now.AddHours(3) };
        var state = State(old, urgent, original); state.Preference.BreakMinutes = 15;
        var plan = Plan(state, urgent);
        Assert.Empty(plan.Warnings);
        Assert.Equal(Now.AddHours(3).AddMinutes(15), Assert.Single(plan.Sessions, x => x.TaskId == old.Id).Start);
    }

    [Fact]
    public void Urgent_work_cannot_displace_the_prerequisite_it_needs_to_finish_first()
    {
        var old = Task("Tiền nhiệm", Now.AddHours(8)); var urgent = Urgent();
        var original = new WorkSession { TaskId = old.Id, Start = Now.AddHours(2), End = Now.AddHours(3) };
        var state = State(old, urgent, original) with { Dependencies = [new() { TaskId = urgent.Id, PredecessorId = old.Id }] };
        var plan = Plan(state, urgent);
        Assert.Contains(plan.Sessions, x => x.Id == original.Id && x.Preserved);
        Assert.Contains(plan.Warnings, x => x.TaskId == urgent.Id && x.UnscheduledMinutes > 0);
    }

    [Fact]
    public void Only_the_conflicting_session_moves_when_the_same_task_also_has_a_later_session()
    {
        var old = Task("Việc nhiều phiên", Now.AddDays(7)); old.EstimatedMinutes = 120; old.Splittable = true;
        var urgent = Urgent();
        var first = new WorkSession { TaskId = old.Id, Start = Now.AddHours(2), End = Now.AddHours(3) };
        var later = new WorkSession { TaskId = old.Id, Start = Now.AddDays(5).AddHours(2), End = Now.AddDays(5).AddHours(3) };
        var plan = Plan(State(old, urgent, first, later), urgent);
        Assert.Empty(plan.Warnings);
        Assert.Contains(plan.Sessions, x => x.Id == later.Id && x.Preserved && x.Start == later.Start);
        Assert.Equal(Now.AddDays(1).AddHours(2), Assert.Single(plan.Sessions, x => x.TaskId == old.Id && !x.Preserved).Start);
    }
}
