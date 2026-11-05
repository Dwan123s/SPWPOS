using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.UnitTests;

public sealed class ContinuousActivityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);
    private static PlanningState State(WorkTask task, bool fragmented = true) => new([task], [],
        Enumerable.Range(0, 7).SelectMany(day => fragmented
            ? new[] { new WeeklyAvailability { DayOfWeek = day, StartMinute = 480, EndMinute = 540 },
                new WeeklyAvailability { DayOfWeek = day, StartMinute = 600, EndMinute = 660 } }
            : new[] { new WeeklyAvailability { DayOfWeek = day, StartMinute = 480, EndMinute = 600 } }).ToArray(),
        [], [], [], new() { TimeZoneId = "UTC", MinimumSessionMinutes = 25, MaximumSessionMinutes = 240,
            MaximumDailyMinutes = 480, BreakMinutes = 0, PreferredStartMinute = 480, PreferredEndMinute = 1080 });
    private static WorkTask Activity(string name, string type = "OTHER", bool splittable = true) => new()
    {
        Name = name, TaskType = type, EstimatedMinutes = 120, Deadline = Now.AddHours(3),
        Splittable = splittable, MinimumSessionMinutes = 25, IsHardDeadline = true
    };
    private static PlanInput Input => new(Now, 1, UseCapacityFactor: false, UseEstimateBias: false);

    [Theory]
    [InlineData("Cuộc họp nhóm")]
    [InlineData("Họp với khách hàng")]
    [InlineData("Hop nhom du an")]
    [InlineData("Meeting with client")]
    [InlineData("Lớp học tiếng Anh")]
    [InlineData("Khám bệnh tại bệnh viện")]
    [InlineData("Phỏng vấn ứng viên")]
    [InlineData("Ca làm việc buổi sáng")]
    [InlineData("Kỳ thi chứng chỉ")]
    public void Continuous_activities_cannot_use_two_separate_one_hour_gaps_even_when_marked_splittable(string name)
    {
        var task = Activity(name);
        var plan = new PlanningEngine().Generate(State(task), Input, Now);
        Assert.Empty(plan.Sessions);
        Assert.Contains(plan.Warnings, warning => warning.TaskId == task.Id && warning.UnscheduledMinutes == 120);
    }

    [Fact]
    public void A_two_hour_meeting_uses_one_contiguous_two_hour_slot()
    {
        var task = Activity("Cuộc họp nhóm");
        var session = Assert.Single(new PlanningEngine().Generate(State(task, false), Input, Now).Sessions);
        Assert.Equal(120, (session.End - session.Start).TotalMinutes);
    }

    [Theory]
    [InlineData("Chuẩn bị tài liệu cho cuộc họp", "MEETING_PREP")]
    [InlineData("Meeting preparation", "MEETING_PREP")]
    [InlineData("Viết biên bản cuộc họp", "WRITING")]
    [InlineData("Ôn tập cho kỳ thi", "STUDY")]
    [InlineData("Viết báo cáo", "OTHER")]
    [InlineData("Hợp đồng dịch vụ", "WRITING")]
    [InlineData("Thi hành nhiệm vụ", "OTHER")]
    public void Preparation_and_ordinary_work_can_still_be_split(string name, string type)
    {
        var task = Activity(name, type);
        var plan = new PlanningEngine().Generate(State(task), Input, Now);
        Assert.Equal(2, plan.Sessions.Length);
        Assert.Equal(120, plan.Sessions.Sum(session => (session.End - session.Start).TotalMinutes));
    }

    [Fact]
    public void Explicitly_continuous_work_cannot_be_split_and_fixed_events_keep_their_full_duration()
    {
        var task = Activity("Viết báo cáo", splittable: false);
        var meeting = new FixedEvent { Name = "Họp nhóm", EventType = "MEETING", Start = Now, End = Now.AddHours(2) };
        var state = State(task) with { Events = [meeting] };
        var plan = new PlanningEngine().Generate(state, Input, Now);
        Assert.Empty(plan.Sessions);
        Assert.Equal(Now, meeting.Start);
        Assert.Equal(Now.AddHours(2), meeting.End);
        Assert.Single(state.Events);
    }

    [Theory]
    [InlineData("MEETING")]
    [InlineData("CLASS")]
    [InlineData("APPOINTMENT")]
    public void Explicit_activity_type_enforces_continuity_even_with_an_uninformative_title(string type)
    {
        var task = Activity("Review Q4", type);
        Assert.Empty(new PlanningEngine().Generate(State(task), Input, Now).Sessions);
    }

    [Fact]
    public void Insufficient_contiguous_time_is_explained_and_does_not_mutate_legacy_task_data()
    {
        var task = Activity("Họp nhóm");
        var state = State(task);
        var plan = new PlanningEngine().Generate(state, Input, Now);
        var warning = Assert.Single(plan.Warnings);
        Assert.Equal("NO_CONTIGUOUS_SLOT", warning.Code);
        Assert.Contains("liên tục", warning.Message);
        Assert.True(task.Splittable);
        Assert.Contains("NO_CONTIGUOUS_SLOT", Assert.Single(new PlanningEngine().Analyze(state, Input, Now).Tasks).Reasons);
    }
}
