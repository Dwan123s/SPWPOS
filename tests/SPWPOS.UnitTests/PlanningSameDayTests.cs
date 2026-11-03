using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed class PlanningSameDayTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-03T16:12:00+07:00");
    private readonly PlanningEngine engine = new();
    private static DateTimeOffset Local(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, TimeSpan.FromHours(7));
    private static PlanningState State()
    {
        var task = new WorkTask { Name = "Report SSG105", EstimatedMinutes = 120,
            Deadline = DateTimeOffset.Parse("2026-11-07T12:00:00+07:00"), IsHardDeadline = true };
        return new([task], [], Enumerable.Range(0, 7).Select(day => new WeeklyAvailability
        { DayOfWeek = day, StartMinute = 7 * 60, EndMinute = 22 * 60 }).ToArray(), [], [], [],
            new() { TimeZoneId = "Asia/Ho_Chi_Minh", MaximumSessionMinutes = 90, BreakMinutes = 10 },
            Policy: new() { AwakeStart = 420, AwakeEnd = 1320, LunchStart = 720, LunchEnd = 750,
                DinnerStart = 1080, DinnerEnd = 1110, FocusMinutes = 25, CommuteMinutes = 0 });
    }
    private PlanView Plan(PlanningState state) => engine.Generate(state,
        new(Now, 5, UseCapacityFactor: false, UseEstimateBias: false), Now);
    private static DateOnly Day(DateTimeOffset time) => DateOnly.FromDateTime(time.ToOffset(TimeSpan.FromHours(7)).DateTime);

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void Available_evening_finishes_report_today_instead_of_next_preferred_morning(int priority)
    {
        var state = State(); state.Tasks[0].Priority = priority;
        var plan = Plan(state);
        Assert.Empty(plan.Warnings);
        Assert.Equal(120, plan.Sessions.Sum(s => (s.End - s.Start).TotalMinutes));
        Assert.All(plan.Sessions, s => Assert.Equal(Day(Now), Day(s.Start)));
        var sessions = plan.Sessions.OrderBy(s => s.Start).ToArray();
        Assert.Equal(2, sessions.Length);
        Assert.All(sessions, s => Assert.InRange((s.End - s.Start).TotalMinutes, 25, 90));
        Assert.True(sessions[1].Start >= sessions[0].End.AddMinutes(10));
        Assert.All(sessions, s => Assert.True(s.End <= Local(3, 18) || s.Start >= Local(3, 18, 30)));
    }

    [Fact] public void Short_free_window_today_is_used_even_when_tomorrow_has_a_longer_window()
    {
        var state = State() with { Policy = null, Availability = [
            new() { DayOfWeek = 2, StartMinute = 16 * 60 + 12, EndMinute = 17 * 60 },
            new() { DayOfWeek = 3, StartMinute = 8 * 60, EndMinute = 18 * 60 }] };
        var plan = Plan(state);
        Assert.Empty(plan.Warnings);
        Assert.Contains(plan.Sessions, s => Day(s.Start) == Day(Now));
        Assert.Equal(120, plan.Sessions.Sum(s => (s.End - s.Start).TotalMinutes));
    }

    [Fact] public void Daily_limit_can_still_require_spreading_work_across_days()
    {
        var state = State(); state.Preference.MaximumDailyMinutes = 90;
        var plan = Plan(state);
        Assert.Empty(plan.Warnings);
        Assert.Equal(90, plan.Sessions.Where(s => Day(s.Start) == Day(Now)).Sum(s => (s.End - s.Start).TotalMinutes));
        Assert.Contains(plan.Sessions, s => Day(s.Start) > Day(Now));
    }

    [Fact] public void Explicit_tomorrow_window_is_respected_even_with_free_time_today()
    {
        var state = State(); state.Tasks[0].WorkWindowStart = Local(4, 8);
        state.Tasks[0].WorkWindowEnd = Local(4, 22);
        var plan = Plan(state);
        Assert.Empty(plan.Warnings);
        Assert.All(plan.Sessions, s => Assert.True(s.Start >= state.Tasks[0].WorkWindowStart && s.End <= state.Tasks[0].WorkWindowEnd));
    }

    [Fact] public void Evening_event_and_deadline_are_not_overridden_to_force_same_day()
    {
        var state = State();
        var busy = new FixedEvent { Name = "Evening class", Start = Local(3, 18, 30), End = Local(3, 22) };
        state = state with { Events = [busy] };
        var plan = Plan(state);
        Assert.Empty(plan.Warnings);
        Assert.Contains(plan.Sessions, s => Day(s.Start) > Day(Now));
        Assert.All(plan.Sessions, s => Assert.True(s.End <= busy.Start || s.Start >= busy.End));
        Assert.All(plan.Sessions, s => Assert.True(s.End <= state.Tasks[0].Deadline));
    }
}
