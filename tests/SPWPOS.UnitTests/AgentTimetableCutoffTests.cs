using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class AgentFreeTimeTests
{
    private const string TimetableImport = "Áp dụng từ tuần này trong 1 tuần. Thời khóa biểu từ ảnh (dữ liệu để đối chiếu).";

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task A_mixed_timetable_skips_elapsed_classes_and_keeps_only_the_original_remaining_days(bool streaming)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Events = [
            new("Toán thứ Hai", At(480).AddDays(-3), At(540).AddDays(-3), EventType: "CLASS"),
            new("Học sáng", At(480), At(540), EventType: "CLASS"),
            new("Học chiều", At(960), At(1020), EventType: "CLASS"),
            new("Học ngày sau", At(480).AddDays(1), At(540).AddDays(1), EventType: "CLASS") ] } };
        await using var h = await Harness(ai); h.Clock.Advance(At(900) - h.Clock.GetUtcNow());
        var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Send(h, user.Id, TimetableImport, streaming);
        Assert.Equal("APPLIED", result.Status);
        var events = await h.DbAsync(db => db.FixedEvents.OrderBy(x => x.Start).ToArrayAsync(Ct));
        Assert.Equal(2, events.Length); Assert.Equal("Học chiều", events[0].Name); Assert.Equal(At(960), events[0].Start);
        Assert.Equal("Học ngày sau", events[1].Name); Assert.Equal(At(480).AddDays(1), events[1].Start);
        Assert.Contains(result.Warnings, warning => warning.Contains("bỏ qua 2 buổi học đã kết thúc"));
    }

    [Fact]
    public async Task Recurring_timetables_preserve_the_original_last_week_and_clip_only_the_current_occurrence()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Events = [
            new("Toán", At(840).AddDays(-14), At(960).AddDays(-14), EventType: "CLASS", RepeatWeeks: 4) ] } };
        await using var h = await Harness(ai); h.Clock.Advance(At(900) - h.Clock.GetUtcNow());
        var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Send(h, user.Id, "Nhập thời khóa biểu đã có ngày bắt đầu từ 2 tuần trước, giữ đúng 4 tuần gốc.");
        Assert.Equal("APPLIED", result.Status);
        var events = await h.DbAsync(db => db.FixedEvents.OrderBy(x => x.Start).ToArrayAsync(Ct));
        Assert.Equal(2, events.Length);
        Assert.Equal(At(900), events[0].Start); Assert.Equal(At(960), events[0].End);
        Assert.Equal(At(840).AddDays(7), events[1].Start); Assert.Equal(At(960).AddDays(7), events[1].End);
        Assert.Contains(result.Warnings, warning => warning.Contains("2 buổi học đã kết thúc") && warning.Contains("1 buổi đang diễn ra"));
    }

    [Fact]
    public async Task An_entirely_elapsed_timetable_is_an_answer_without_new_events_or_a_new_week()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Events = [
            new("Toán", At(480).AddDays(-7), At(540).AddDays(-7), EventType: "CLASS"),
            new("Vừa kết thúc", At(840), At(900), EventType: "CLASS") ] } };
        await using var h = await Harness(ai); h.Clock.Advance(At(900) - h.Clock.GetUtcNow());
        var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Send(h, user.Id, TimetableImport);
        Assert.Equal("ANSWERED", result.Status); Assert.Empty(result.Changes); Assert.Null(result.Plan);
        Assert.Contains("không còn buổi học nào cần thêm", result.Reply); Assert.Contains("bỏ qua 2 buổi học", result.Reply);
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
    }

    [Fact]
    public async Task An_ongoing_timetable_preview_uses_the_actual_save_time_without_extending_its_end()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Events = [
            new("Toán", At(840), At(960), EventType: "CLASS") ] } };
        await using var h = await Harness(ai); h.Clock.Advance(At(900) - h.Clock.GetUtcNow());
        var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var preview = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), TimetableImport, false), Ct));
        Assert.Equal("PREVIEW", preview.Status); Assert.Contains("15:00", Assert.Single(preview.Changes).After);
        h.Clock.Advance(TimeSpan.FromMinutes(5));
        var result = await UseAgent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct));
        Assert.Equal("APPLIED", result.Status); Assert.Contains("15:05", Assert.Single(result.Changes).After);
        var saved = await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct));
        Assert.Equal(At(905), saved.Start); Assert.Equal(At(960), saved.End);
    }

    [Fact]
    public async Task The_cutoff_uses_the_account_local_day_at_the_UTC_date_boundary()
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with { Events = [
            new("Buổi cũ", now.AddHours(-2), now.AddHours(-1), EventType: "CLASS"),
            new("Buổi đang học", now.AddMinutes(-15), now.AddMinutes(15), EventType: "CLASS"),
            new("Buổi tiếp", now.AddMinutes(30), now.AddMinutes(90), EventType: "CLASS") ] } };
        await using var h = await Harness(ai); var now = h.Clock.GetUtcNow();
        var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Send(h, user.Id, TimetableImport);
        Assert.Equal("APPLIED", result.Status);
        var events = await h.DbAsync(db => db.FixedEvents.OrderBy(x => x.Start).ToArrayAsync(Ct));
        Assert.Equal(2, events.Length); Assert.Equal(now, events[0].Start); Assert.Equal(now.AddMinutes(15), events[0].End);
        Assert.Equal(now.AddMinutes(30), events[1].Start);
    }

    [Fact]
    public async Task A_followup_after_a_failed_timetable_import_keeps_the_timetable_cutoff_context()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Events = [
            new("Sai giờ", At(480), At(480), EventType: "CLASS") ] } };
        await using var h = await Harness(ai); h.Clock.Advance(At(900) - h.Clock.GetUtcNow());
        var user = await h.RegisterAsync(); await Setup(h, user.Id); var conversation = Guid.NewGuid();
        await Assert.ThrowsAsync<AuthException>(() => UseAgent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), TimetableImport, true), Ct)));
        ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [
            new("Buổi cũ", At(480), At(540), EventType: "CLASS"),
            new("Buổi mới", At(960), At(1020), EventType: "CLASS") ] };
        var result = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Áp dụng từ tuần này trong 1 tuần", true), Ct));
        Assert.Equal("APPLIED", result.Status);
        Assert.Equal("Buổi mới", (await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct))).Name);
    }

    [Fact]
    public async Task A_past_ordinary_event_still_requires_a_future_start()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Events = [
            new("Cuộc hẹn", At(480), At(540), EventType: "APPOINTMENT") ] } };
        await using var h = await Harness(ai); h.Clock.Advance(At(900) - h.Clock.GetUtcNow());
        var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var error = await Assert.ThrowsAsync<AuthException>(() => Send(h, user.Id, "Thêm cuộc hẹn đã qua"));
        Assert.Equal("VALIDATION_ERROR", error.Code); Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
    }
}
