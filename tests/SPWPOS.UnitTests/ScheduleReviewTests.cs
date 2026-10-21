using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class AgentFreeTimeTests
{
    private const string ReviewAdvice = "Những khoảng nghỉ ngắn đang làm lịch của bạn bị chia nhỏ. Mình gợi ý giữ chúng để nghỉ hoặc chuẩn bị tài liệu, thay vì bắt đầu một việc cần tập trung rồi phải dừng giữa chừng. Với báo cáo gần hạn, bạn nên cân nhắc khoảng dài đã được kiểm tra bên dưới để làm liền mạch; nếu muốn đổi lịch, mình sẽ chuẩn bị phương án cho bạn xác nhận. Bạn cũng nên giữ phần thời gian còn lại sau báo cáo làm khoảng đệm để rà soát kết quả và chuẩn bị cho việc tiếp theo.";
    private static AgentAiTestDouble ReviewAi(bool legacyFreeTime = false)
    {
        var ai = Ai();
        ai.Response = _ => AgentAiTestDouble.Empty() with {
            Reply = ai.Calls == 1 ? "Mình đang đối chiếu lịch của bạn." : ReviewAdvice,
            ResultKind = "ANSWER", FreeTimeQuery = legacyFreeTime && ai.Calls == 1 ? Query : null,
            ScheduleReviewQuery = legacyFreeTime && ai.Calls == 1 ? null : Query
        };
        return ai;
    }

    [Theory] [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public async Task Schedule_analysis_explains_fragmentation_and_offers_a_verified_task_slot_without_saving(bool legacyFreeTime, bool streaming)
    {
        var ai = ReviewAi(legacyFreeTime); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var report = new WorkTask { UserId = user.Id, Name = "Báo cáo gần hạn", Deadline = At(1200), EstimatedMinutes = 60, Priority = 4, IsHardDeadline = true, Splittable = false };
        await h.DbAsync(async db => {
            var availability = await db.WeeklyAvailability.SingleAsync(Ct); availability.StartMinute = 810; availability.EndMinute = 1140;
            db.WorkTasks.Add(report);
            db.FixedEvents.AddRange(new[] { (820, 850), (860, 990), (1000, 1030), (1040, 1070) }
                .Select(x => new FixedEvent { UserId = user.Id, Name = $"Lịch cố định {x.Item1}", Start = At(x.Item1), End = At(x.Item2) }));
            await db.SaveChangesAsync(Ct);
        });
        var revision = await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct));
        var result = await Send(h, user.Id, "Phân tích lịch trình ngày mai của tôi và tư vấn nên làm gì.", streaming);
        Assert.Equal("ANSWERED", result.Status); Assert.Null(result.Plan); Assert.Empty(result.Changes); Assert.False(result.CanUndo);
        Assert.Contains(ReviewAdvice, result.Reply); Assert.Contains("110 phút trống", result.Reply);
        Assert.Contains("4 khoảng ngắn dưới 25 phút", result.Reply);
        Assert.Contains("17:50–19:00 (70 phút)", result.Reply);
        Assert.Contains("Báo cáo gần hạn", result.Reply); Assert.Contains("17:50–18:50", result.Reply);
        Assert.Contains("lịch hiện tại chưa thay đổi", result.Reply);
        Assert.Equal(2, ai.Calls);
        using var context = JsonDocument.Parse(ai.Context);
        var facts = context.RootElement.GetProperty("verifiedScheduleReview");
        Assert.Equal(220, facts.GetProperty("metrics").GetProperty("occupiedMinutes").GetInt32());
        Assert.Equal(110, facts.GetProperty("metrics").GetProperty("remainingCalendarFreeMinutes").GetInt32());
        var suggestion = Assert.Single(facts.GetProperty("taskSuggestions").EnumerateArray());
        Assert.Equal("2026-10-22T17:50:00+07:00", suggestion.GetProperty("start").GetString());
        Assert.Equal(60, suggestion.GetProperty("minutes").GetInt32());
        Assert.Equal(revision, await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.PlanProposals.CountAsync(Ct)));
    }

    [Fact]
    public async Task Today_analysis_includes_elapsed_appointments_and_completed_sessions_but_only_future_free_time()
    {
        var ai = ReviewAi(); await using var h = await Harness(ai); h.Clock.Advance(At(600) - h.Clock.GetUtcNow());
        var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Họp đầu ngày", Start = At(480), End = At(540) });
            var task = new WorkTask { UserId = user.Id, Name = "Đã làm", Deadline = At(600), Status = WorkTaskStatus.Completed, Progress = 100 };
            db.WorkTasks.Add(task); db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task.Id, Start = At(540), End = At(570), Status = WorkSessionStatus.Completed });
            await db.SaveChangesAsync(Ct);
        });
        var result = await Send(h, user.Id, "Phân tích lịch trình hôm nay của tôi.");
        Assert.Contains("Họp đầu ngày", result.Reply); Assert.Contains("Đã làm", result.Reply);
        Assert.Contains("phiên đã đánh dấu hoàn tất", result.Reply);
        using var context = JsonDocument.Parse(ai.Context);
        var facts = context.RootElement.GetProperty("verifiedScheduleReview");
        Assert.Equal(90, facts.GetProperty("metrics").GetProperty("occupiedMinutes").GetInt32());
        Assert.All(facts.GetProperty("freeGaps").EnumerateArray(), gap => Assert.True(gap.GetProperty("start").GetDateTimeOffset() >= h.Clock.GetUtcNow()));
    }

    [Fact]
    public async Task Overlapping_items_are_reported_and_not_double_counted_as_busy_time()
    {
        var ai = ReviewAi(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Họp", Start = At(1140), End = At(1200) });
            var task = new WorkTask { UserId = user.Id, Name = "Báo cáo", Deadline = At(1320) };
            db.WorkTasks.Add(task); db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task.Id, Start = At(1170), End = At(1230) });
            await db.SaveChangesAsync(Ct);
        });
        var result = await Send(h, user.Id, "Nhận xét lịch ngày mai.");
        Assert.Contains("trùng 22/10/2026 19:30–20:00", result.Reply);
        using var context = JsonDocument.Parse(ai.Context);
        Assert.Equal(90, context.RootElement.GetProperty("verifiedScheduleReview").GetProperty("metrics").GetProperty("occupiedMinutes").GetInt32());
    }

    [Theory] [InlineData("daily-limit")] [InlineData("work-window")] [InlineData("dependency")]
    [InlineData("too-long")] [InlineData("short-gap")] [InlineData("deadline")]
    public async Task A_calendar_gap_does_not_become_a_task_suggestion_when_constraints_fail(string mode)
    {
        var ai = ReviewAi(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            var task = new WorkTask { UserId = user.Id, Name = "Cần kiểm tra", Deadline = At(1320), EstimatedMinutes = mode == "too-long" ? 600 : 60 };
            if (mode == "work-window") { task.WorkWindowStart = At(1080); task.WorkWindowEnd = At(1140); }
            if (mode == "deadline") task.Deadline = At(1140);
            if (mode == "short-gap") (await db.WeeklyAvailability.SingleAsync(Ct)).EndMinute = 1150;
            if (mode == "dependency") {
                var prerequisite = new WorkTask { UserId = user.Id, Name = "Tiền nhiệm", Deadline = At(1320), EstimatedMinutes = 600 };
                db.WorkTasks.Add(prerequisite); db.TaskDependencies.Add(new() { UserId = user.Id, TaskId = task.Id, PredecessorId = prerequisite.Id });
            }
            if (mode == "daily-limit") {
                (await db.WorkPreferences.SingleAsync(Ct)).MaximumDailyMinutes = 60;
                var reserved = new WorkTask { UserId = user.Id, Name = "Đã có lịch", Deadline = At(1320) }; db.WorkTasks.Add(reserved);
                db.WorkSessions.Add(new() { UserId = user.Id, TaskId = reserved.Id, Start = At(1140), End = At(1200) });
            }
            db.WorkTasks.Add(task); await db.SaveChangesAsync(Ct);
        });
        await Send(h, user.Id, "Tư vấn lịch trình ngày mai.");
        using var context = JsonDocument.Parse(ai.Context);
        Assert.Empty(context.RootElement.GetProperty("verifiedScheduleReview").GetProperty("taskSuggestions").EnumerateArray());
        Assert.Equal(0, await h.DbAsync(db => db.PlanProposals.CountAsync(Ct)));
    }

    [Fact]
    public async Task Today_review_also_considers_unscheduled_work_due_tomorrow_within_48_hours()
    {
        var ai = ReviewAi(); await using var h = await Harness(ai); h.Clock.Advance(At(600) - h.Clock.GetUtcNow());
        var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => { db.WorkTasks.Add(new() { UserId = user.Id, Name = "Hạn trưa mai", Priority = 4,
            Deadline = At(720).AddDays(1), EstimatedMinutes = 60, IsHardDeadline = true }); await db.SaveChangesAsync(Ct); });
        var result = await Send(h, user.Id, "Phân tích lịch trình hôm nay của tôi.");
        Assert.Contains("Hạn trưa mai", result.Reply);
        using var context = JsonDocument.Parse(ai.Context);
        var suggestion = Assert.Single(context.RootElement.GetProperty("verifiedScheduleReview").GetProperty("taskSuggestions").EnumerateArray());
        Assert.Equal(At(1140), suggestion.GetProperty("start").GetDateTimeOffset());
    }

    [Fact]
    public async Task A_calendar_change_during_the_explanation_rejects_the_stale_analysis()
    {
        var ai = ReviewAi(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        ai.BeforeReply = async () => { if (ai.Calls == 2) await h.DbAsync(async db => {
            (await db.WorkPreferences.SingleAsync(Ct)).Revision = Guid.NewGuid(); await db.SaveChangesAsync(Ct);
        }); };
        Assert.Equal("AGENT_STALE", (await Assert.ThrowsAsync<AuthException>(() => Send(h, user.Id, "Phân tích lịch trình ngày mai."))).Code);
        Assert.Equal(0, await h.DbAsync(db => db.PlanProposals.CountAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Fact]
    public async Task Missing_availability_is_disclosed_instead_of_treating_the_empty_calendar_as_free()
    {
        var ai = ReviewAi(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => { db.WeeklyAvailability.RemoveRange(await db.WeeklyAvailability.ToArrayAsync(Ct)); await db.SaveChangesAsync(Ct); });
        var result = await Send(h, user.Id, "Phân tích lịch ngày mai.");
        Assert.Contains("Chưa có giờ rảnh phù hợp", result.Reply); Assert.Contains("0 phút trống", result.Reply);
    }

    [Fact]
    public async Task Unsafe_advice_is_ignored_and_cannot_create_a_meeting_or_claim_a_saved_change()
    {
        var ai = ReviewAi(); ai.Response = _ => AgentAiTestDouble.Empty() with { ScheduleReviewQuery = Query,
            Reply = ai.Calls == 1 ? "Đang xem lịch." : "Mình đã lưu cuộc họp.",
            Events = ai.Calls == 1 ? [] : [new("Không được lưu", At(1200), At(1260))] };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Send(h, user.Id, "Phân tích lịch ngày mai.");
        Assert.Equal("ANSWERED", result.Status); Assert.DoesNotContain("Mình đã lưu cuộc họp", result.Reply);
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
    }

    [Theory] [InlineData("wrong-day")] [InlineData("out-of-range")] [InlineData("mutation")]
    public async Task Unverified_schedule_review_scopes_cannot_change_the_calendar(string mode)
    {
        var ai = ReviewAi(); ai.Response = _ => AgentAiTestDouble.Empty() with {
            ScheduleReviewQuery = mode == "wrong-day" ? new(At(0).AddDays(-1), At(0)) : mode == "out-of-range" ? new(At(0).AddDays(40), At(0).AddDays(41)) : Query,
            Events = mode == "mutation" ? [new("Không lưu", At(1200), At(1260))] : [] };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        if (mode == "mutation")
            Assert.Equal("AGENT_INVALID_INTENT", (await Assert.ThrowsAsync<AuthException>(() => Send(h, user.Id, "Phân tích lịch ngày mai."))).Code);
        else {
            var result = await Send(h, user.Id, mode == "wrong-day" ? "Phân tích lịch ngày mai." : "Phân tích lịch trong khoảng đã chọn.");
            Assert.Equal(mode == "wrong-day" ? "NEEDS_INPUT" : "ANSWERED", result.Status); Assert.Equal(1, ai.Calls);
        }
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
    }
}
