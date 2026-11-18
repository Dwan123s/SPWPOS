using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.UnitTests;

public sealed class EisenhowerPriorityRuleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-18T06:00:00Z");

    [Theory]
    [InlineData(-1, "Urgent", "Q1")]
    [InlineData(0, "Urgent", "Q1")]
    [InlineData(1440, "Urgent", "Q1")]
    [InlineData(1441, "NotUrgent", "Q2")]
    [InlineData(50000, "NotUrgent", "Q2")]
    public void Deadline_alone_defines_urgency_at_the_24_hour_boundary(int minutes, string urgency, string quadrant)
    {
        var deadline = Now.AddMinutes(minutes);
        var task = new WorkTask { Name = "Báo cáo", ImportanceLevel = "Important", Deadline = deadline,
            UrgencyOverride = urgency == "Urgent" ? "NotUrgent" : "Urgent", EstimatedMinutes = 60 };
        var state = new PlanningState([task], [], [], [], [], [], new WorkPreference());
        var matrix = Assert.Single(new PlanningEngine().AssessEisenhower(state, Now));
        var summary = EisenhowerPriority.Describe("Important", deadline.ToOffset(TimeSpan.FromHours(7)), Now);
        Assert.Equal(urgency, matrix.Urgency); Assert.Equal(quadrant, matrix.Quadrant);
        Assert.Equal(matrix.Quadrant, summary.Quadrant); Assert.Equal("Auto", matrix.UrgencySource);
    }

    [Fact]
    public void Undated_tasks_remain_visible_without_inventing_deadlines_or_urgency()
    {
        var task = new WorkTask { Name = "Ý tưởng", ImportanceLevel = "Important", Deadline = null, EstimatedMinutes = 0 };
        var row = Assert.Single(new PlanningEngine().AssessEisenhower(new([task], [], [], [], [], [], new()), Now));
        Assert.Equal("Unknown", row.Quadrant); Assert.Equal("Unknown", row.Urgency);
        Assert.Equal("Important", row.Importance); Assert.Null(row.Deadline);
    }

    [Fact]
    public void Scheduling_uses_Eisenhower_instead_of_legacy_priority_numbers()
    {
        var important = new WorkTask { Name = "Quan trọng", ImportanceLevel = "Important", Deadline = Now.AddDays(2), EstimatedMinutes = 60, Priority = 1 };
        var lower = new WorkTask { Name = "Ít quan trọng", ImportanceLevel = "Lower", Deadline = Now.AddDays(2), EstimatedMinutes = 60, Priority = 4 };
        var state = new PlanningState([lower, important], [], Enumerable.Range(0, 7).Select(day =>
            new WeeklyAvailability { DayOfWeek = day, StartMinute = 0, EndMinute = 1440 }).ToArray(), [], [], [],
            new WorkPreference { TimeZoneId = "UTC", MinimumSessionMinutes = 25, MaximumSessionMinutes = 90, BreakMinutes = 0 }, EisenhowerScheduling: true);
        var plan = new PlanningEngine().Generate(state, new(Now.AddMinutes(5), 3), Now);
        Assert.Equal(important.Id, plan.Sessions.OrderBy(session => session.Start).First().TaskId);
    }
}
