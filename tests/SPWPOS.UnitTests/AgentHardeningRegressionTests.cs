using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PlanningAgentTests
{
    [Theory]
    [InlineData("1,5 giờ", 90)] [InlineData("1h15p", 75)] [InlineData("5 phút", 5)]
    [InlineData("45 minutes", 45)] [InlineData("1 giờ 15 phút", 75)]
    public async Task Hardening_accepts_grounded_duration_forms_without_changing_requested_minutes(string duration, int minutes)
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Demo", now.AddHours(10), minutes, 2, "OTHER", true,
                DurationEvidence: duration, DeadlineEvidence: "hạn chót 18:00 hôm nay")]
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request($"Demo cần {duration}, hạn chót 18:00 hôm nay."), Ct));
        Assert.Equal("APPLIED", result.Status);
        Assert.Equal(minutes, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).EstimatedMinutes);
        Assert.Equal(minutes, result.Plan!.Sessions.Sum(x => (x.End - x.Start).TotalMinutes));
    }

    [Fact]
    public async Task Hardening_unicode_normalization_does_not_lose_literal_duration_evidence()
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Luyện chữ", now.AddHours(10), 30, 2, "STUDY", true,
                DurationEvidence: "30 phút", DeadlineEvidence: "hạn chót 18:00 hôm nay")]
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request("Luyện chữ cần 30 phút, hạn chót 18:00 hôm nay.".Normalize(NormalizationForm.FormD)), Ct));
        Assert.Equal("APPLIED", result.Status);
    }

    [Theory]
    [InlineData("Meeting ngày mai 09:00–10:00 CST.")]
    [InlineData("Từ ngày mai, cứ hàng tuần họp 15:00–16:00 tới vô cực.")]
    [InlineData("Họp hai tuần một lần, tổng 3 lần, bắt đầu ngày mai 15:00–16:00.")]
    [InlineData("Book họp ngày mai 15:00–16:00 rồi gửi email mời cho an@example.test.")]
    [InlineData("Meeting ngày 31/02/2027 15:00–16:00.")]
    public async Task Hardening_blocks_consequential_request_ambiguity_even_when_model_emits_a_mutation(string text)
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with {
            Events = [new("Meeting", now.AddDays(1).AddHours(7), now.AddDays(1).AddHours(8),
                RepeatWeeks: text.Contains("tuần", StringComparison.Ordinal) ? 3 : 1)]
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(text), Ct));
        Assert.Equal("NEEDS_INPUT", result.Status); Assert.NotEmpty(result.Question);
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }

    [Fact]
    public async Task Hardening_existing_saved_fields_need_no_new_quote_when_values_are_unchanged()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        var id = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Poster", "", now.AddHours(10), 60), Ct));
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(id, "Poster", now.AddHours(10), 60,
            null, "", null, DurationEvidence: "", DeadlineEvidence: "")] };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Xếp Poster theo dữ liệu đã lưu."), Ct));
        Assert.Equal("APPLIED", result.Status);
        Assert.Equal(60, result.Plan!.Sessions.Sum(x => (x.End - x.Start).TotalMinutes));
    }

    [Theory] [InlineData("Giữ nguyên")] [InlineData("Không cần dời")]
    public async Task Hardening_preview_keeps_explicitly_preserved_unrelated_work_even_if_model_selects_it(string preservation)
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        var a = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Report", "", now.AddDays(5), 60), Ct));
        var b = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Read API", "", now.AddDays(5), 60), Ct));
        var original = new WorkSession { UserId = user.Id, TaskId = b, Start = now.AddDays(1).AddHours(2), End = now.AddDays(1).AddHours(3) };
        await h.DbAsync(async db => { db.WorkSessions.Add(original); await db.SaveChangesAsync(Ct); });
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(a, "Report", null, 45, null, "", null,
            DurationEvidence: "45 phút")], ScheduleTaskIds = [a, b], Reschedule = true };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request($"Chỉ xem trước Report thành 45 phút. {preservation} Read API, chưa lưu."), Ct));
        Assert.Equal("PREVIEW", result.Status);
        Assert.Contains(result.Plan!.Sessions, session => session.Id == original.Id && session.Preserved && session.Start == original.Start && session.End == original.End);
        Assert.Equal(60, (await h.DbAsync(db => db.WorkTasks.SingleAsync(x => x.Id == a, Ct))).EstimatedMinutes);
    }

    [Theory]
    [InlineData("Họp từ 10:00 đến 11:00 ngày mai.")]
    [InlineData("Họp lúc 10:00–10:30 ngày mai.")]
    [InlineData("Họp từ 12:00–13:00 ngày mai.")]
    public void Hardening_explicit_24_hour_ranges_are_not_am_pm_questions(string text)
        => Assert.Null(AgentInputRules.AmbiguousClock(text));

    [Fact]
    public async Task Hardening_multiple_allowed_days_are_not_narrowed_to_one_evening()
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Demo", new DateTimeOffset(now.AddDays(1).Date.AddHours(22), TimeSpan.Zero), 60, 2, "OTHER", true,
                DurationEvidence: "60 phút", DeadlineEvidence: "hạn chót 22:00 ngày mai", WorkWindowEvidence: "")]
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request("Demo cần 60 phút, làm tối nay hoặc tối mai, hạn chót 22:00 ngày mai."), Ct));
        Assert.Equal("APPLIED", result.Status);
        var task = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct));
        Assert.Null(task.WorkWindowStart); Assert.Null(task.WorkWindowEnd);
    }

    [Fact]
    public async Task Hardening_grounded_task_window_is_not_replaced_by_availability_hours()
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with {
            Exceptions = [new(DateOnly.FromDateTime(now.Date.AddDays(1)), 480, 720, true, "")],
            Tasks = [new(null, "Demo", new DateTimeOffset(now.AddDays(1).Date.AddHours(22), TimeSpan.Zero), 60, 2, "OTHER", true,
                new DateTimeOffset(now.Date.AddDays(1).AddHours(10), TimeSpan.Zero), new DateTimeOffset(now.Date.AddDays(1).AddHours(12), TimeSpan.Zero),
                DurationEvidence: "60 phút", DeadlineEvidence: "hạn chót 22:00 ngày mai", WorkWindowEvidence: "10:00–12:00 ngày mai")]
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request("Ngày mai tôi rảnh từ 08:00 đến 12:00. Demo cần 60 phút, chỉ làm 10:00–12:00 ngày mai, hạn chót 22:00 ngày mai."), Ct));
        Assert.Equal("APPLIED", result.Status);
        var task = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct));
        Assert.Equal(h.Clock.GetUtcNow().Date.AddDays(1).AddHours(10), task.WorkWindowStart!.Value.UtcDateTime);
        Assert.Equal(60, result.Plan!.Sessions.Sum(x => (x.End - x.Start).TotalMinutes));
    }

    [Fact]
    public async Task Hardening_ordinary_event_moves_only_the_explicitly_authorized_task()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        var report = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Report", "", now.AddDays(5), 60), Ct));
        var read = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Read API", "", now.AddDays(5), 60), Ct));
        var a = new WorkSession { UserId = user.Id, TaskId = report, Start = now.AddHours(2), End = now.AddHours(3) };
        var b = new WorkSession { UserId = user.Id, TaskId = read, Start = now.AddDays(1).AddHours(2), End = now.AddDays(1).AddHours(3) };
        await h.DbAsync(async db => { db.WorkSessions.AddRange(a, b); await db.SaveChangesAsync(Ct); });
        ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Meeting", a.Start, a.End, EventType: "MEETING")] };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request("Thêm Meeting hôm nay 10:00–11:00 rồi dời riêng Report bị trùng. Giữ nguyên Read API."), Ct));
        Assert.Equal("APPLIED", result.Status);
        var saved = await h.DbAsync(db => db.WorkSessions.Where(x => x.Status == WorkSessionStatus.Scheduled).ToArrayAsync(Ct));
        Assert.Contains(saved, s => s.Id == b.Id && s.Start == b.Start && s.End == b.End);
        var moved = Assert.Single(saved, s => s.TaskId == report);
        Assert.True(moved.End <= a.Start || moved.Start >= a.End);
        Assert.Equal(60, (moved.End - moved.Start).TotalMinutes);
        Assert.Equal(a.Start, (await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct))).Start);
    }

    [Fact]
    public async Task Hardening_schedule_saved_window_cannot_move_it_to_another_weekday()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow(); var from = new DateTimeOffset(now.Date.AddDays(1).AddHours(18), TimeSpan.Zero); var to = from.AddHours(3);
        var task = new WorkTask { UserId = user.Id, Name = "Poster", EstimatedMinutes = 60,
            Deadline = now.AddDays(3), WorkWindowStart = from, WorkWindowEnd = to, IsHardDeadline = true };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); await db.SaveChangesAsync(Ct); });
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(task.Id, "Poster", null, null, null, "", null,
            from.AddDays(-1), to.AddDays(-1), WorkWindowEvidence: "khung thực hiện thứ Năm")] };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request("Xếp Poster theo dữ liệu đã lưu, chỉ làm trong khung thực hiện thứ Năm."), Ct));
        Assert.Equal("APPLIED", result.Status);
        Assert.All(result.Plan!.Sessions, s => Assert.True(s.Start >= from && s.End <= to));
        Assert.Equal(from, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).WorkWindowStart);
    }

    [Fact]
    public async Task Hardening_partial_free_interval_edit_retains_other_exclusive_intervals()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow(); var day = DateOnly.FromDateTime(now.Date.AddDays(1));
        var from = new DateTimeOffset(now.Date.AddDays(1).AddHours(19), TimeSpan.Zero);
        var task = new WorkTask { UserId = user.Id, Name = "Budget", EstimatedMinutes = 70, MinimumSessionMinutes = 15,
            Deadline = from.AddHours(2), IsHardDeadline = true, WorkWindowStart = from, WorkWindowEnd = from.AddHours(2) };
        var first = new AvailabilityException { UserId = user.Id, Date = day, StartMinute = 1140, EndMinute = 1170, IsAvailable = true, OverridesWeeklyAvailability = true };
        var second = new AvailabilityException { UserId = user.Id, Date = day, StartMinute = 1200, EndMinute = 1260, IsAvailable = true, OverridesWeeklyAvailability = true };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); db.AvailabilityExceptions.AddRange(first, second); await db.SaveChangesAsync(Ct); });
        ai.Response = _ => AgentAiTestDouble.Empty() with { Exceptions = [new(day, 1215, 1260, true, ExistingId: second.Id)], ScheduleTaskIds = [task.Id] };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request("Khúc 20:00–21:00 đổi thành 20:15–21:00."), Ct));
        Assert.Equal("APPLIED", result.Status);
        var saved = await h.DbAsync(db => db.AvailabilityExceptions.ToArrayAsync(Ct));
        Assert.Equal(2, saved.Length); Assert.Contains(saved, x => x.Id == first.Id && x.StartMinute == 1140 && x.EndMinute == 1170);
        Assert.Equal(70, result.Plan!.Sessions.Sum(x => (x.End - x.Start).TotalMinutes));
    }

    [Theory] [InlineData("trong đúng khung đó", true)] [InlineData("liên tục không chia", false)]
    public async Task Hardening_only_explicit_continuity_makes_new_work_unsplittable(string instruction, bool splittable)
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Demo", now.AddHours(10), 60, 2, "OTHER", false,
                DurationEvidence: "60 phút", DeadlineEvidence: "hạn chót 18:00 hôm nay")]
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request($"Demo cần 60 phút {instruction}, hạn chót 18:00 hôm nay."), Ct));
        Assert.Equal("APPLIED", result.Status);
        Assert.Equal(splittable, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).Splittable);
    }

    [Theory] [InlineData(false, false)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task Hardening_followup_can_widen_a_saved_window_on_its_referenced_date(bool replaceWithSplitRanges, bool omitTrailingFree)
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow(); var from = new DateTimeOffset(now.Date.AddDays(1).AddHours(19), TimeSpan.Zero);
        var task = new WorkTask { UserId = user.Id, Name = "Demo", EstimatedMinutes = 60, MinimumSessionMinutes = 15,
            Deadline = from.AddHours(1), IsHardDeadline = true, WorkWindowStart = from, WorkWindowEnd = from.AddHours(1) };
        await h.DbAsync(async db => {
            db.WorkTasks.Add(task);
            if (replaceWithSplitRanges) db.AvailabilityExceptions.Add(new() { UserId = user.Id,
                Date = DateOnly.FromDateTime(from.DateTime), StartMinute = 1140, EndMinute = 1200, IsAvailable = true, OverridesWeeklyAvailability = true });
            await db.SaveChangesAsync(Ct);
        });
        ai.Response = _ => AgentAiTestDouble.Empty() with {
            Exceptions = replaceWithSplitRanges ? (omitTrailingFree ? [new(DateOnly.FromDateTime(from.DateTime), 1110, 1140, true),
                new(DateOnly.FromDateTime(from.DateTime), 1140, 1170, false)] : [new(DateOnly.FromDateTime(from.DateTime), 1110, 1140, true),
                new(DateOnly.FromDateTime(from.DateTime), 1140, 1170, false), new(DateOnly.FromDateTime(from.DateTime), 1170, 1200, true)]) :
                [new(DateOnly.FromDateTime(from.DateTime), 1140, 1170, false)],
            Tasks = [new(task.Id, "Demo", null, null, null, "", null, from, from.AddHours(1), WorkWindowEvidence: "18:30–20:00")]
        };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request((replaceWithSplitRanges ? "Thay giờ rảnh ngày đó thành 18:30–20:00. " : "") +
                "Cho Demo làm trong 18:30–20:00 ngày đó, bận 19:00–19:30, hạn vẫn 20:00."), Ct));
        Assert.Equal("APPLIED", result.Status);
        Assert.Equal(from.AddMinutes(-30), (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).WorkWindowStart);
        Assert.Equal(60, result.Plan!.Sessions.Sum(x => (x.End - x.Start).TotalMinutes));
        Assert.All(result.Plan.Sessions, s => Assert.True(s.End <= from || s.Start >= from.AddMinutes(30)));
    }

    [Fact]
    public async Task Hardening_identical_read_queries_include_agenda_and_free_time_without_mutation()
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with {
            FreeTimeQuery = new(new DateTimeOffset(now.Date, TimeSpan.Zero), new DateTimeOffset(now.Date.AddDays(1), TimeSpan.Zero)),
            ScheduleReviewQuery = new(new DateTimeOffset(now.Date, TimeSpan.Zero), new DateTimeOffset(now.Date.AddDays(1), TimeSpan.Zero))
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        await Work(h, s => s.SaveEventAsync(user.Id, null, new("Standup", now.AddHours(1), now.AddHours(2)), Ct));
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Xem lịch hôm nay, còn rảnh giờ nào? Chỉ đọc."), Ct));
        Assert.Equal("ANSWERED", result.Status); Assert.Contains("Standup", result.Reply);
        Assert.Empty(result.Changes); Assert.Null(result.Plan);
        Assert.Equal(1, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }
}
