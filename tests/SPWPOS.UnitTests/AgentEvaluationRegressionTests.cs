using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PlanningAgentTests
{
    // Regressions from NORA_SANDBOX_EVALUATION_2026-10-19 and its patch verification.
    [Theory]
    [InlineData("Chỉ xem thử lịch, chưa đồng ý lưu.")]
    [InlineData("Preview only, do not save yet.")]
    public async Task Explicit_preview_only_survives_automatic_retry_until_explicit_apply(string text)
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var request = Request(text);
        var preview = await Agent(h, s => s.SendAgentTurnAsync(user.Id, request, Ct));
        var retry = await Agent(h, s => s.SendAgentTurnAsync(user.Id, request, Ct));
        Assert.Equal("PREVIEW", preview.Status); Assert.Equal(preview.Id, retry.Id); Assert.Equal("PREVIEW", retry.Status);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        var applied = await Agent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct));
        Assert.Equal("APPLIED", applied.Status); Assert.Equal(1, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Ordinary_negative_constraint_does_not_disable_authorized_apply()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Viết báo cáo, đừng chia nhỏ phiên."), Ct));
        Assert.Equal("APPLIED", result.Status);
    }

    [Theory]
    [InlineData("Thứ Năm ngày 28/10/2026 họp 18–19.", false)]
    [InlineData("Thứ 5 ngày 28/10/2026 họp 18–19.", false)]
    [InlineData("Thứ Năm ngày 28/10/2026 họp 18–19.", true)]
    public async Task Conflicting_weekday_and_date_requires_a_correct_calendar_clarification(string text, bool incorrectQuestion)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Question = incorrectQuestion ? "28/10/2026 là thứ Ba, bạn chọn ngày nào?" : "",
            Events = [new("Họp", DateTimeOffset.Parse("2026-10-28T18:00:00+07:00"), DateTimeOffset.Parse("2026-10-28T19:00:00+07:00"))] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(text), Ct));
        Assert.Equal("NEEDS_INPUT", result.Status); Assert.Contains("28/10/2026", result.Question);
        Assert.Contains("thứ 4", result.Question);
        Assert.Empty(result.Changes); Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
    }

    [Fact] public async Task Tomorrow_is_validated_against_account_local_date_after_utc_midnight_boundary()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Events = [new("Họp", DateTimeOffset.Parse("2026-10-27T18:00:00+07:00"), DateTimeOffset.Parse("2026-10-27T18:30:00+07:00"))] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await Work(h, s => s.SetPreferenceAsync(user.Id, new("Asia/Ho_Chi_Minh", 25, 240, 0, 480, 1320), Ct));
        h.Clock.Advance(DateTimeOffset.Parse("2026-10-26T18:30:00Z") - h.Clock.GetUtcNow());
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Ngày mai họp 18h–18h30."), Ct));
        Assert.Equal("NEEDS_INPUT", result.Status); Assert.Contains("28/10/2026", result.Question);
        Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
    }

    [Fact] public async Task Partial_weekly_slots_preserve_unmentioned_days()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { WeeklySlots = [new(1, 1140, 1260)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Đổi giờ rảnh thứ Hai thành 19–21."), Ct));
        Assert.Equal("APPLIED", result.Status);
        var slots = await h.DbAsync(db => db.WeeklyAvailability.AsNoTracking().ToArrayAsync(Ct));
        Assert.Equal(7, slots.Length); Assert.Equal(1140, Assert.Single(slots, x => x.DayOfWeek == 1).StartMinute);
        Assert.All(slots.Where(x => x.DayOfWeek != 1), x => Assert.Equal(480, x.StartMinute));
    }

    [Fact] public async Task Context_serializes_deadlines_and_events_in_account_timezone()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await Work(h, s => s.SetPreferenceAsync(user.Id, new("Asia/Ho_Chi_Minh", 25, 240, 0, 480, 1320), Ct));
        await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Viết", "", DateTimeOffset.Parse("2026-10-15T15:00:00Z"), 60), Ct));
        await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Hạn Viết là mấy giờ?"), Ct));
        using var context = JsonDocument.Parse(ai.Context);
        var deadline = Assert.Single(context.RootElement.GetProperty("tasks").EnumerateArray()).GetProperty("deadline").GetDateTimeOffset();
        Assert.Equal(22, deadline.Hour); Assert.Equal(TimeSpan.FromHours(7), deadline.Offset);
        Assert.Equal("2026-10-15", context.RootElement.GetProperty("clock").GetProperty("tomorrow").GetString());
    }

    [Fact] public async Task Clarification_preserves_useful_reason_without_a_false_save_claim()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Reply = "Đã lưu công việc. Khung giờ này trùng phiên đã khóa.", Question = "Bạn muốn chọn giờ khác không?" } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct));
        Assert.Contains("trùng phiên đã khóa", result.Reply); Assert.DoesNotContain("Đã lưu", result.Reply);
        Assert.Equal("NEEDS_INPUT", result.Status);
    }

    [Fact] public async Task Global_answer_over_truncated_context_uses_verified_scope_and_no_global_guarantee()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Reply = "Tất cả công việc đã đủ lịch, phạm vi đến 2025-11-13." } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.WorkTasks.AddRange(Enumerable.Range(0, 121).Select(i => new SPWPOS.Domain.Entities.WorkTask {
                UserId = user.Id, Name = $"Việc {i}", Deadline = h.Clock.GetUtcNow().AddDays(1), EstimatedMinutes = 30 }));
            await db.SaveChangesAsync(Ct);
        });
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Kiểm tra toàn bộ công việc đã đủ lịch chưa, không sửa."), Ct));
        Assert.Equal("ANSWERED", result.Status); Assert.Contains("100/121", result.Reply);
        Assert.Contains("13/11/2026", result.Reply); Assert.DoesNotContain("2025", result.Reply);
        Assert.DoesNotContain("Tất cả công việc đã đủ lịch", result.Reply); Assert.Empty(result.Changes);
    }

    [Fact] public async Task Unscheduled_dependency_is_blocked_even_when_the_planner_proposes_no_session_changes()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var deadline = h.Clock.GetUtcNow().AddHours(10);
        var first = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Đọc đề", "", deadline, 60), Ct));
        var second = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Viết bài", "", deadline, 60), Ct));
        await h.DbAsync(async db => { db.TaskDependencies.Add(new() { UserId = user.Id, TaskId = second, PredecessorId = first }); await db.SaveChangesAsync(Ct); });
        ai.Response = _ => AgentAiTestDouble.Empty() with { ScheduleTaskIds = [second] };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Chỉ xếp Viết bài, không xếp Đọc đề."), Ct));
        Assert.Equal("BLOCKED", result.Status); Assert.Contains(result.Plan!.Warnings, x => x.Code == "DEPENDENCY_UNSCHEDULED");
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct))); Assert.False(result.CanUndo);
    }

    [Theory]
    [InlineData("Báo cáo cần 90 phút, hạn chót 18:00 ngày 14/10/2026.", 90, "90 phút", "18:00 ngày 14/10/2026", "APPLIED")]
    [InlineData("Báo cáo 45 phút, hạn chót 18:00 ngày 14/10/2026.", 45, "45 phót", "hạn chót 18:00 ngày 14/10/2026", "APPLIED")]
    [InlineData("Báo cáo. Mình rảnh 90 phút, hạn chót 18:00 ngày 14/10/2026.", 90, "", "hạn chót 18:00 ngày 14/10/2026", "NEEDS_INPUT")]
    [InlineData("Báo cáo 45 phút, ngày thực hiện 14/10/2026 lúc 18:00.", 45, "45 phút", "18:00", "NEEDS_INPUT")]
    public async Task Source_grounding_recovers_only_explicit_task_duration_and_deadline_clauses(string request, int minutes,
        string durationEvidence, string deadlineEvidence, string expected)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Báo cáo",
            DateTimeOffset.Parse("2026-10-14T18:00:00Z"), minutes, 2, "WRITING", true,
            DurationEvidence: durationEvidence, DeadlineEvidence: deadlineEvidence, WorkWindowEvidence: "")] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(request), Ct));
        Assert.Equal(expected, result.Status);
        Assert.Equal(expected == "APPLIED" ? 1 : 0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Context_distinguishes_daily_capacity_on_two_dates_and_anchors_next_week()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        h.Clock.Advance(DateTimeOffset.Parse("2026-10-26T02:00:00Z") - h.Clock.GetUtcNow());
        await Work(h, s => s.SetAvailabilityAsync(user.Id, new([new(1, 1140, 1320), new(2, 1140, 1320)]), Ct));
        await Work(h, s => s.SetPreferenceAsync(user.Id, new("Asia/Ho_Chi_Minh", 15, 60, 15, 1140, 1320, MaximumDailyMinutes: 120), Ct));
        await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Kiểm tra giờ rảnh hai ngày tới."), Ct));
        using var context = JsonDocument.Parse(ai.Context);
        Assert.True(context.RootElement.TryGetProperty("currentCalendarCapacity", out var capacity));
        var days = capacity.GetProperty("perDate").EnumerateArray().Take(2).ToArray();
        Assert.Equal(new[] { "2026-10-26", "2026-10-27" }, days.Select(x => x.GetProperty("date").GetString()));
        Assert.All(days, x => Assert.Equal(120, x.GetProperty("schedulableMinutes").GetInt32()));
        Assert.Equal("2026-11-02", context.RootElement.GetProperty("clock").GetProperty("nextWeekStart").GetString());
    }
}
