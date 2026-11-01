using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class AgentFreeTimeTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public async Task An_agenda_question_shows_saved_items_in_order_with_free_time_without_mutation_or_a_second_ai_call(bool streaming, bool reviewQuery)
    {
        var ai = Ai();
        if (reviewQuery) ai.Response = _ => AgentAiTestDouble.Empty() with { ScheduleReviewQuery = Query, ResultKind = "ANSWER" };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Họp dự án", Start = At(1200), End = At(1260) });
            var task = new WorkTask { UserId = user.Id, Name = "Viết báo cáo", Deadline = At(1320) };
            db.WorkTasks.Add(task);
            db.WorkSessions.AddRange(
                new WorkSession { UserId = user.Id, TaskId = task.Id, Start = At(540), End = At(600) },
                new WorkSession { UserId = user.Id, TaskId = task.Id, Start = At(660), End = At(720), Status = WorkSessionStatus.Cancelled });
            await db.SaveChangesAsync(Ct);
        });
        var revision = await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct));
        var result = await Send(h, user.Id, "lịch trình ngày mai của tôi", streaming);
        Assert.Equal("ANSWERED", result.Status); Assert.Null(result.Plan); Assert.Empty(result.Changes); Assert.False(result.CanUndo);
        Assert.Contains("Ngày mai (22/10/2026)", result.Reply); Assert.Contains("2 mục trong lịch", result.Reply);
        Assert.Contains("09:00–10:00: Viết báo cáo", result.Reply); Assert.Contains("20:00–21:00: Họp dự án", result.Reply);
        Assert.True(result.Reply.IndexOf("Viết báo cáo", StringComparison.Ordinal) < result.Reply.IndexOf("Họp dự án", StringComparison.Ordinal));
        Assert.Contains("19:00–20:00 (60 phút)", result.Reply); Assert.Contains("21:00–22:00 (60 phút)", result.Reply);
        Assert.DoesNotContain("11:00–12:00", result.Reply); Assert.DoesNotContain("Lịch chưa thay đổi", result.Reply);
        Assert.DoesNotContain("Lịch đã đối chiếu", result.Reply);
        Assert.Equal(1, ai.Calls);
        Assert.Equal(revision, await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.PlanProposals.CountAsync(Ct)));
        Assert.Equal(2, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task An_empty_agenda_distinguishes_no_bookings_from_missing_availability(bool configured)
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        if (!configured) await h.DbAsync(async db => {
            db.WeeklyAvailability.RemoveRange(await db.WeeklyAvailability.ToArrayAsync(Ct)); await db.SaveChangesAsync(Ct);
        });
        var result = await Send(h, user.Id, "Xem lịch ngày mai");
        Assert.Contains("chưa có cuộc hẹn hay phiên làm việc nào được lưu", result.Reply);
        Assert.Contains(configured ? "19:00–22:00 (180 phút)" : "chưa có giờ làm việc được bạn xác nhận", result.Reply);
        Assert.DoesNotContain("rảnh cả ngày", result.Reply); Assert.Equal(1, ai.Calls);
    }

    [Fact]
    public async Task Agenda_queries_keep_the_requested_hours_and_include_busy_exceptions()
    {
        var ai = Ai(); ai.Response = _ => AgentAiTestDouble.Empty() with { FreeTimeQuery = new(At(780), At(1080)) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.FixedEvents.AddRange(
                new FixedEvent { UserId = user.Id, Name = "Họp sáng", Start = At(540), End = At(600) },
                new FixedEvent { UserId = user.Id, Name = "Hẹn chiều", Start = At(840), End = At(900) });
            db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 960, EndMinute = 1020, IsAvailable = false, Note = "Đưa con đi học" });
            await db.SaveChangesAsync(Ct);
        });
        var result = await Send(h, user.Id, "Xem lịch ngày mai từ 13:00 đến 18:00");
        Assert.DoesNotContain("Họp sáng", result.Reply); Assert.DoesNotContain("19:00", result.Reply);
        Assert.Contains("14:00–15:00: Hẹn chiều", result.Reply); Assert.Contains("16:00–17:00: Đưa con đi học", result.Reply);
    }

    [Fact]
    public async Task A_single_free_slot_is_a_conversation_instead_of_a_status_report()
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Send(h, user.Id, "Ngày mai tôi rảnh lúc nào?");
        Assert.Contains("Ngày mai (22/10/2026), bạn còn rảnh từ 19:00–22:00 (180 phút)", result.Reply);
        Assert.DoesNotContain("Các khoảng bạn còn rảnh theo", result.Reply);
        Assert.DoesNotContain("Lịch chưa thay đổi", result.Reply);
        Assert.Equal("ANSWERED", result.Status); Assert.Empty(result.Changes); Assert.Equal(1, ai.Calls);
    }
}
