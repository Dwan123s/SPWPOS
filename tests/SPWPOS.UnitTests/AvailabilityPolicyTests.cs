using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed class AvailabilityPolicyTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-02T00:00:00Z");
    private readonly PlanningEngine engine = new();
    private static PlanningState State(AvailabilityPolicy? policy = null) => new([], [], [], [], [], [], new() { TimeZoneId = "UTC", MaximumDailyMinutes = 1440 }, Policy: policy);
    [Fact] public void No_unapproved_preset_creates_work_hours()
    { Assert.Empty(engine.FreeSlots(State(), Start, Start.AddDays(1))); }
    [Fact] public void Meals_and_merged_commute_are_subtracted_once_per_actual_date()
    {
        var state = State(new()) with { Events = [new() { Start = Start.AddHours(8), End = Start.AddHours(9) }, new() { Start = Start.AddHours(9), End = Start.AddHours(10) }] };
        var slots = engine.FreeSlots(state, Start, Start.AddDays(2));
        Assert.Contains(slots, x => x.Start == Start.AddHours(7) && x.End == Start.AddHours(7.5));
        Assert.Contains(slots, x => x.Start == Start.AddHours(10.5) && x.End == Start.AddHours(11.75));
        Assert.Contains(slots, x => x.Start == Start.AddDays(1).AddHours(7) && x.End == Start.AddDays(1).AddHours(11.75));
        Assert.DoesNotContain(slots, x => x.Start < Start.AddHours(12.75) && x.End > Start.AddHours(11.75));
        Assert.DoesNotContain(slots, x => x.End > Start.AddHours(22.5) && x.Start < Start.AddDays(1));
    }
    [Fact] public void Short_gaps_remain_visible_but_cannot_be_focus_slots()
    {
        var day = (int)Start.DayOfWeek; var state = State(new()) with { Availability = [new() { DayOfWeek = day, StartMinute = 1140, EndMinute = 1170 }, new() { DayOfWeek = day, StartMinute = 1200, EndMinute = 1290 }] };
        Assert.Equal(2, engine.FreeSlots(state, Start, Start.AddDays(1)).Length); Assert.Single(engine.FocusSlots(state, Start, Start.AddDays(1)));
    }
    [Fact] public void Date_overrides_and_existing_weekly_hours_are_not_expanded_by_policy()
    {
        var state = State(new()) with { Availability = [new() { DayOfWeek = (int)Start.DayOfWeek, StartMinute = 1140, EndMinute = 1320 }], Exceptions = [new() { Date = DateOnly.FromDateTime(Start.Date), StartMinute = 1200, EndMinute = 1260, IsAvailable = true, OverridesWeeklyAvailability = true }] };
        var slot = Assert.Single(engine.FreeSlots(state, Start, Start.AddDays(7))); Assert.Equal(Start.AddHours(20), slot.Start); Assert.Equal(Start.AddHours(21), slot.End);
    }
    [Fact] public void No_overnight_plan_may_cross_the_shared_hard_deadline()
    {
        var task = new WorkTask { Name = "Khẩn", Deadline = Start.AddHours(23), EstimatedMinutes = 120, Priority = 4, IsHardDeadline = true };
        var state = State() with { Tasks = [task], Availability = [new() { DayOfWeek = (int)Start.DayOfWeek, StartMinute = 1380, EndMinute = 1440 }, new() { DayOfWeek = (int)Start.AddDays(1).DayOfWeek, StartMinute = 0, EndMinute = 120 }] };
        var plan = engine.Generate(state, new(Start.AddHours(20), 2, [task.Id], true, UseCapacityFactor: false, UseEstimateBias: false), Start);
        Assert.DoesNotContain(plan.Sessions, x => x.TaskId == task.Id); Assert.Contains(plan.Warnings, x => x.TaskId == task.Id && x.UnscheduledMinutes == 120);
    }
}
