using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed partial class PlanningAgentTests
{
    // Regressions from the complete Gemini 3.8 live catalog run on 31 October 2026.
    [Fact]
    public async Task LiveRegression_oversized_batch_asks_to_split_without_saving_a_subset()
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with {
            Tasks = Enumerable.Range(1, 13).Select(i => new AgentTaskCommand(null, $"A{i:00}", now.AddDays(3), 15, 2, "OTHER", true,
                DurationEvidence: "15 phút", DeadlineEvidence: "hạn chót ngày mai")).ToArray()
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request("Xếp đủ 13 việc A01 đến A13, mỗi việc 15 phút, hạn chót ngày mai; không bỏ việc nào."), Ct));
        Assert.Equal("NEEDS_INPUT", result.Status); Assert.NotEmpty(result.Question);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Theory]
    [InlineData("một lèo, không cắt", false)]
    [InlineData("một lèo, không cắt", true)]
    [InlineData("không chia nhỏ", false)]
    [InlineData("không chia nhỏ", true)]
    [InlineData("không ngắt quãng", true)]
    [InlineData("in one sitting", true)]
    public async Task LiveRegression_uninterrupted_work_never_saves_split_sessions(string phrase, bool modelSplittable)
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Thu podcast", new DateTimeOffset(now.Date.AddHours(22), TimeSpan.Zero), 90, 2, "OTHER", modelSplittable,
                DurationEvidence: "90 phút", DeadlineEvidence: "hạn chót 22:00 hôm nay")]
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var day = (int)h.Clock.GetUtcNow().DayOfWeek;
        await Work(h, s => s.SetAvailabilityAsync(user.Id, new([new(day, 1140, 1200), new(day, 1260, 1320)]), Ct));
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request($"Thu podcast cần 90 phút {phrase}, hạn chót 22:00 hôm nay."), Ct));
        Assert.Equal("BLOCKED", result.Status);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Fact]
    public async Task LiveRegression_deadline_extension_sang_is_not_a_morning_work_window()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id, maximum: 120, daily: 180);
        await Work(h, s => s.SetAvailabilityAsync(user.Id,
            new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 1140, 1320)).ToArray()), Ct));
        var now = h.Clock.GetUtcNow();
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Chép sổ", new DateTimeOffset(now.Date.AddHours(22), TimeSpan.Zero), 240, 2, "WORK", true,
            DurationEvidence: "240 phút", DeadlineEvidence: "hạn chót 22:00 hôm nay")] };
        var first = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Chép sổ cần 240 phút, chia được, hạn chót 22:00 hôm nay."), Ct));
        Assert.Equal("BLOCKED", first.Status);
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Chép sổ", new DateTimeOffset(now.Date.AddDays(1).AddHours(22), TimeSpan.Zero), 240, 2, "WORK", true,
            DurationEvidence: "240 phút", DeadlineEvidence: "hạn chót sang 22:00 ngày mai")] };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request("Vậy đổi hạn chót sang 22:00 ngày mai, giữ 240 phút, xếp cả hai ngày cũng được."), Ct));
        Assert.Equal("APPLIED", result.Status);
        var saved = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct));
        Assert.Null(saved.WorkWindowStart); Assert.Null(saved.WorkWindowEnd);
        Assert.Equal(new DateTimeOffset(now.Date.AddDays(1).AddHours(22), TimeSpan.Zero), saved.Deadline);
        Assert.Equal(240, result.Plan!.Sessions.Sum(s => (s.End - s.Start).TotalMinutes));
        Assert.Equal(2, result.Plan.Sessions.Select(s => s.Start.Date).Distinct().Count());
    }

    [Fact]
    public async Task LiveRegression_dl_shorthand_is_grounded_deadline_evidence()
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Ôn mạng", new DateTimeOffset(now.Date.AddDays(1).AddHours(22), TimeSpan.Zero), 75, 2, "STUDY", true,
                DurationEvidence: "1h15p", DeadlineEvidence: "dl 22:00 ngày mai")]
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Ôn mạng 1h15p, dl 22:00 ngày mai, chia được."), Ct));
        Assert.Equal("APPLIED", result.Status);
        Assert.Equal(75, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).EstimatedMinutes);
    }

    [Fact]
    public async Task LiveRegression_excluded_prerequisite_is_never_added_to_a_confirmable_preview()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        var pre = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Đọc đề", "", now.AddHours(10), 60), Ct));
        var task = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Viết bài", "", now.AddHours(10), 60), Ct));
        await h.DbAsync(async db => { db.TaskDependencies.Add(new() { UserId = user.Id, TaskId = task, PredecessorId = pre }); await db.SaveChangesAsync(Ct); });
        ai.Response = _ => AgentAiTestDouble.Empty() with { ScheduleTaskIds = [task, pre], Reschedule = true };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request("Chỉ xếp Viết bài, không tạo hay di chuyển phiên cho Đọc đề. Đọc đề vẫn chưa hoàn thành và chưa có lịch."), Ct));
        Assert.Equal("BLOCKED", result.Status);
        Assert.DoesNotContain(result.Plan!.Sessions, s => s.TaskId == pre);
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Theory]
    [InlineData("xếp lại các phiên bị trùng", false)]
    [InlineData("xếp lại các phiên bị trùng. Đừng chuyển công việc không liên quan", false)]
    [InlineData("chuyển riêng các phiên bị ảnh hưởng", false)]
    [InlineData("giữ lịch khác tối đa", false)]
    [InlineData("xếp lại các phiên bị trùng", true)]
    public async Task LiveRegression_generic_conflict_repair_moves_only_overlapping_unlocked_work(string instruction, bool locked)
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        var task = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Report", "", now.AddDays(2), 60), Ct));
        var other = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Read API", "", now.AddDays(3), 60), Ct));
        var session = new WorkSession { UserId = user.Id, TaskId = task, Start = now.AddHours(2), End = now.AddHours(3), IsLocked = locked };
        var unrelated = new WorkSession { UserId = user.Id, TaskId = other, Start = now.AddDays(1).AddHours(2), End = now.AddDays(1).AddHours(3) };
        await h.DbAsync(async db => { db.WorkSessions.AddRange(session, unrelated); await db.SaveChangesAsync(Ct); });
        ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Họp", session.Start, session.End, EventType: "MEETING")] };
        if (locked)
        {
            var error = await Assert.ThrowsAsync<SPWPOS.Application.Authentication.AuthException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id,
                Request($"Thêm họp hôm nay 10:00–11:00, {instruction}."), Ct)));
            Assert.Equal("LOCKED_SESSION_CONFLICT", error.Code);
            Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        }
        else
        {
            var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
                Request($"Thêm họp hôm nay 10:00–11:00, {instruction}."), Ct));
            Assert.Equal("APPLIED", result.Status);
            var sessions = await h.DbAsync(db => db.WorkSessions.Where(s => s.Status == WorkSessionStatus.Scheduled).ToArrayAsync(Ct));
            Assert.Contains(sessions, s => s.Id == unrelated.Id && s.Start == unrelated.Start && s.End == unrelated.End);
            var moved = Assert.Single(sessions, s => s.TaskId == task);
            Assert.True(moved.End <= session.Start || moved.Start >= session.End);
            Assert.Equal(60, (moved.End - moved.Start).TotalMinutes);
        }
    }

    [Fact]
    public async Task LiveRegression_editing_an_existing_event_repairs_new_conflicts_without_duplicating_the_event()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        var task = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Report", "", now.AddDays(2), 60), Ct));
        var session = new WorkSession { UserId = user.Id, TaskId = task, Start = now.AddHours(2), End = now.AddHours(3) };
        await h.DbAsync(async db => { db.WorkSessions.Add(session); await db.SaveChangesAsync(Ct); });
        ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Họp nhóm", session.Start, session.Start.AddMinutes(30), EventType: "MEETING")] };
        var first = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request("Thêm Họp nhóm hôm nay 10:00–10:30, chuyển riêng các phiên bị ảnh hưởng."), Ct));
        Assert.Equal("APPLIED", first.Status);
        var meeting = await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct));
        var scheduled = Assert.Single(first.Plan!.Sessions, s => s.TaskId == task);
        ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Họp nhóm", scheduled.Start, scheduled.End,
            EventType: "MEETING", ExistingId: meeting.Id)] };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request($"Cuộc họp vừa thêm chuyển sang {scheduled.Start:HH:mm}–{scheduled.End:HH:mm} cùng ngày. Sửa cuộc họp cũ, đừng tạo cuộc họp thứ hai."), Ct));
        Assert.Equal("APPLIED", result.Status);
        var saved = await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct));
        Assert.Equal(meeting.Id, saved.Id); Assert.Equal(scheduled.Start, saved.Start); Assert.Equal(scheduled.End, saved.End);
        var moved = Assert.Single(await h.DbAsync(db => db.WorkSessions.Where(s => s.Status == WorkSessionStatus.Scheduled).ToArrayAsync(Ct)));
        Assert.True(moved.End <= saved.Start || moved.Start >= saved.End);
        Assert.Equal(60, (moved.End - moved.Start).TotalMinutes);
        Assert.True(moved.End <= now.AddDays(2));
    }

    [Fact]
    public async Task LiveRegression_readonly_feasibility_lists_saved_session_times_when_requested()
    {
        var ai = new AgentAiTestDouble { BeforeReply = () => throw new SPWPOS.Application.Authentication.AuthException(
            "AGENT_AI_TIMEOUT", "Provider unavailable", 503) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        var task = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Report", "", now.AddHours(10), 60), Ct));
        var session = new WorkSession { UserId = user.Id, TaskId = task, Start = now.AddHours(2), End = now.AddHours(3) };
        await h.DbAsync(async db => { db.WorkSessions.Add(session); await db.SaveChangesAsync(Ct); });
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request("Report đã được xếp đủ và kịp deadline chưa? Nêu các phiên đang lưu, lần này đừng sửa lịch."), Ct));
        Assert.Equal("ANSWERED", result.Status); Assert.Empty(result.Changes);
        Assert.Contains("14/10/2026 10:00", result.Reply);
        Assert.Contains("14/10/2026 11:00", result.Reply);
        var saved = await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct));
        Assert.Equal(session.Id, saved.Id); Assert.Equal(session.Start, saved.Start); Assert.Equal(session.End, saved.End);
    }

    [Fact]
    public async Task LiveRegression_duplicate_task_name_requires_identification_before_editing()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        var a = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Báo cáo", "", now.AddDays(1), 60), Ct));
        var b = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Báo cáo", "", now.AddDays(2), 60), Ct));
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(a, "Báo cáo", null, 45, null, "", null, DurationEvidence: "45 phút"),
            new(b, "Báo cáo", null, 45, null, "", null, DurationEvidence: "45 phút")] };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Rút Báo cáo xuống 45 phút."), Ct));
        Assert.Equal("NEEDS_INPUT", result.Status);
        Assert.All(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)), task => Assert.Equal(60, task.EstimatedMinutes));
    }
}
