using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed class PlanningDeadlineRescueTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-03T16:30:00+07:00");
    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, TimeSpan.FromHours(7));
    private readonly PlanningEngine engine = new();
    private static PlanningState State()
    {
        var report = new WorkTask { Name = "Report SWP", EstimatedMinutes = 240, Deadline = At(4, 12),
            ImportanceLevel = "Important", IsHardDeadline = true, Priority = 2 };
        var later = new WorkTask { Name = "Report SSG105", EstimatedMinutes = 120, Deadline = At(7, 12), IsHardDeadline = true };
        var session = new WorkSession { TaskId = later.Id, Start = At(3, 18, 30), End = At(3, 20, 30) };
        return new([report, later], [], [new() { DayOfWeek = 2, StartMinute = 18 * 60 + 30, EndMinute = 22 * 60 },
            new() { DayOfWeek = 3, StartMinute = 8 * 60, EndMinute = 12 * 60 },
            new() { DayOfWeek = 4, StartMinute = 8 * 60, EndMinute = 18 * 60 }], [],
            [new() { Name = "Morning class", Start = At(4, 8), End = At(4, 11) }], [session],
            new() { TimeZoneId = "Asia/Ho_Chi_Minh", MaximumSessionMinutes = 120, BreakMinutes = 0 });
    }
    private PlanView Plan(PlanningState state) => engine.Generate(state,
        new(Now, 7, [state.Tasks[0].Id], true, UseCapacityFactor: false, UseEstimateBias: false), Now);

    [Fact] public void Important_report_due_at_noon_borrows_time_from_later_deadline_without_priority_four()
    {
        var state = State(); var old = state.Sessions[0];
        var plan = Plan(state);
        Assert.Empty(plan.Warnings); Assert.True(plan.HasUrgentReplan); Assert.True(plan.RequiresConfirmation);
        var report = plan.Sessions.Where(s => s.TaskId == state.Tasks[0].Id).ToArray();
        Assert.Equal(240, report.Sum(s => (s.End - s.Start).TotalMinutes));
        Assert.All(report, s => Assert.True(s.End <= At(4, 12)));
        var moved = plan.Sessions.Where(s => s.TaskId == old.TaskId).ToArray();
        Assert.NotEmpty(moved);
        Assert.All(moved, s => Assert.True(s.Start >= report.Max(r => r.End) && s.End <= state.Tasks[1].Deadline));
        Assert.Equal(120, moved.Sum(s => (s.End - s.Start).TotalMinutes));
        Assert.Equal(At(3, 18, 30), old.Start); Assert.Equal(2, state.Tasks[0].Priority);
        Assert.All(plan.Sessions, s => Assert.True(s.End <= At(4, 8) || s.Start >= At(4, 11)));
    }

    [Fact] public void Enough_unused_time_does_not_move_other_work()
    {
        var state = State(); state.Tasks[0].EstimatedMinutes = 60;
        var plan = Plan(state);
        Assert.Empty(plan.Warnings); Assert.False(plan.HasUrgentReplan);
        Assert.Contains(plan.Sessions, s => s.Id == state.Sessions[0].Id && s.Preserved);
    }

    [Theory]
    [InlineData(true, WorkSessionStatus.Scheduled, 0)]
    [InlineData(false, WorkSessionStatus.Running, 0)]
    [InlineData(false, WorkSessionStatus.Paused, 0)]
    [InlineData(false, WorkSessionStatus.Scheduled, 60)]
    public void Locked_active_or_already_worked_sessions_cannot_be_borrowed(bool locked, WorkSessionStatus status, int actual)
    {
        var state = State(); state.Sessions[0].IsLocked = locked; state.Sessions[0].Status = status;
        state.Sessions[0].ActualSeconds = actual;
        var plan = Plan(state);
        Assert.False(plan.HasUrgentReplan);
        Assert.Contains(plan.Warnings, w => w.TaskId == state.Tasks[0].Id && w.UnscheduledMinutes > 0);
        Assert.Contains(plan.Sessions, s => s.Id == state.Sessions[0].Id && s.Preserved);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Earlier_or_equal_deadline_work_is_not_displaced(bool equal)
    {
        var state = State(); state.Tasks[1].Deadline = equal ? At(4, 12) : At(4, 11);
        var plan = Plan(state);
        Assert.False(plan.HasUrgentReplan);
        Assert.Contains(plan.Warnings, w => w.TaskId == state.Tasks[0].Id && w.UnscheduledMinutes > 0);
        Assert.Contains(plan.Sessions, s => s.Id == state.Sessions[0].Id && s.Preserved);
    }

    [Fact] public void It_does_not_sacrifice_a_later_deadline_when_there_is_nowhere_to_move_that_work()
    {
        var state = State(); state.Tasks[1].WorkWindowStart = At(3, 18, 30); state.Tasks[1].WorkWindowEnd = At(3, 22);
        var plan = Plan(state);
        Assert.False(plan.HasUrgentReplan);
        Assert.Contains(plan.Warnings, w => w.TaskId == state.Tasks[0].Id && w.UnscheduledMinutes > 0);
        Assert.Contains(plan.Sessions, s => s.Id == state.Sessions[0].Id && s.Preserved);
    }

    [Fact] public void Automatic_rearrangement_can_be_disabled()
    {
        var state = State(); state.Preference.AutoReschedule = false;
        var plan = Plan(state);
        Assert.False(plan.HasUrgentReplan);
        Assert.Contains(plan.Sessions, s => s.Id == state.Sessions[0].Id && s.Preserved);
    }

    [Theory] [InlineData("Unknown", 1)] [InlineData("Important", 4)]
    public void Unknown_importance_or_a_distant_deadline_does_not_trigger_automatic_rescue(string importance, int days)
    {
        var state = State(); state.Tasks[0].ImportanceLevel = importance;
        state.Tasks[0].Deadline = At(3 + days, 12); state.Tasks[0].WorkWindowEnd = At(4, 12);
        var plan = Plan(state);
        Assert.False(plan.HasUrgentReplan);
        Assert.Contains(plan.Sessions, s => s.Id == state.Sessions[0].Id && s.Preserved);
    }

    [Fact] public void Request_can_explicitly_forbid_borrowing_other_sessions()
    {
        var state = State();
        var plan = engine.Generate(state, new(Now, 7, [state.Tasks[0].Id], true,
            UseCapacityFactor: false, UseEstimateBias: false) { AllowDeadlineRescue = false }, Now);
        Assert.False(plan.HasUrgentReplan);
        Assert.Contains(plan.Sessions, s => s.Id == state.Sessions[0].Id && s.Preserved);
    }

    [Fact] public void Unrelated_later_session_of_the_same_task_is_preserved()
    {
        var state = State(); state.Tasks[1].EstimatedMinutes = 180;
        var later = new WorkSession { TaskId = state.Tasks[1].Id, Start = At(5, 15), End = At(5, 16) };
        state = state with { Sessions = [state.Sessions[0], later] };
        var plan = Plan(state);
        Assert.Empty(plan.Warnings); Assert.True(plan.HasUrgentReplan);
        Assert.Contains(plan.Sessions, s => s.Id == later.Id && s.Preserved && s.Start == later.Start && s.End == later.End);
        Assert.Equal(180, plan.Sessions.Where(s => s.TaskId == later.TaskId).Sum(s => (s.End - s.Start).TotalMinutes));
    }

    [Fact] public void An_immovable_candidate_is_skipped_so_a_different_flexible_task_can_move()
    {
        var state = State(); state.Sessions[0].Start = At(3, 19); state.Sessions[0].End = At(3, 21);
        var pinned = new WorkTask { Name = "Only allowed now", EstimatedMinutes = 30, Deadline = At(8, 12),
            WorkWindowStart = At(3, 18, 30), WorkWindowEnd = At(3, 19), IsHardDeadline = true };
        var saved = new WorkSession { TaskId = pinned.Id, Start = At(3, 18, 30), End = At(3, 19) };
        state = state with { Tasks = [..state.Tasks, pinned], Sessions = [..state.Sessions, saved] };
        var plan = Plan(state);
        Assert.Empty(plan.Warnings); Assert.True(plan.HasUrgentReplan);
        Assert.Contains(plan.Sessions, s => s.Id == saved.Id && s.Preserved && s.Start == saved.Start && s.End == saved.End);
        Assert.Equal(240, plan.Sessions.Where(s => s.TaskId == state.Tasks[0].Id).Sum(s => (s.End - s.Start).TotalMinutes));
    }

    [Fact] public void Multiple_smaller_flexible_tasks_can_make_room_together()
    {
        var state = State(); state.Tasks[1].EstimatedMinutes = 60; state.Sessions[0].End = At(3, 19, 30);
        var other = new WorkTask { Name = "Later work", EstimatedMinutes = 60, Deadline = At(6, 12), IsHardDeadline = true };
        state = state with { Tasks = [..state.Tasks, other], Sessions = [..state.Sessions,
            new() { TaskId = other.Id, Start = At(3, 19, 30), End = At(3, 20, 30) }] };
        var plan = Plan(state);
        Assert.Empty(plan.Warnings); Assert.True(plan.HasUrgentReplan);
        Assert.Equal(240, plan.Sessions.Where(s => s.TaskId == state.Tasks[0].Id).Sum(s => (s.End - s.Start).TotalMinutes));
        foreach (var task in state.Tasks.Skip(1))
        {
            Assert.Equal(60, plan.Sessions.Where(s => s.TaskId == task.Id).Sum(s => (s.End - s.Start).TotalMinutes));
            Assert.All(plan.Sessions.Where(s => s.TaskId == task.Id), s => Assert.True(s.End <= task.Deadline));
        }
    }

    [Fact] public void Meal_times_and_ten_minute_breaks_are_preserved()
    {
        var state = State(); state.Preference.BreakMinutes = 10;
        state = state with { Policy = new() { AwakeStart = 420, AwakeEnd = 1320,
            LunchStart = 720, LunchEnd = 750, DinnerStart = 1080, DinnerEnd = 1110, CommuteMinutes = 0, FocusMinutes = 25 } };
        var plan = Plan(state);
        Assert.Empty(plan.Warnings); Assert.True(plan.HasUrgentReplan);
        var ordered = plan.Sessions.OrderBy(s => s.Start).ToArray();
        Assert.All(ordered.Zip(ordered.Skip(1)), pair => Assert.True(pair.Second.Start >= pair.First.End.AddMinutes(10)));
        Assert.All(ordered, s => {
            var local = s.Start.ToOffset(TimeSpan.FromHours(7)); var day = local.Day;
            Assert.True(s.End <= At(day, 18) || s.Start >= At(day, 18, 30));
            Assert.True(s.End <= At(day, 12) || s.Start >= At(day, 12, 30));
        });
        Assert.Equal(240, plan.Sessions.Where(s => s.TaskId == state.Tasks[0].Id).Sum(s => (s.End - s.Start).TotalMinutes));
    }
}
