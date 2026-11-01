using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class AgentFreeTimeTests
{
    private static AgentAiTestDouble AutomaticTimetable() => new() { Response = _ => AgentAiTestDouble.Empty() with {
        Events = [new("Toán", At(480), At(600), EventType: "CLASS"),
            new("Lập trình", At(840), At(960), EventType: "CLASS")] } };

    [Fact]
    public async Task A_week_reply_after_a_read_only_lookup_keeps_the_same_conversations_uploaded_timetable()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Question = "Áp dụng thời khóa biểu cho tuần nào?" } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); var conversation = Guid.NewGuid();
        var image = new AgentImage("Lịch.png", "image/png", "iVBORw0KGgo=");
        await UseAgent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Nhập thời khóa biểu trong ảnh.", Image: image), Ct));
        ai.Response = _ => AgentAiTestDouble.Empty();
        await UseAgent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Chỉ xem lịch đã lưu, chưa thay đổi gì."), Ct));
        var callsBefore = ai.ImageCalls;
        ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Toán", At(480), At(600), EventType: "CLASS")] };
        var saved = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Áp dụng trong 1 tuần, bắt đầu ngày mai."), Ct));
        Assert.Equal("APPLIED", saved.Status); Assert.Equal(callsBefore + 1, ai.ImageCalls);
        Assert.True(saved.SuggestRoutine);
        await using var scope = h.Provider.CreateAsyncScope();
        var preview = await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().PreviewAvailabilityPolicyAsync(user.Id, Ct, saved.Id);
        var expectedMonday = Day.AddDays(-(((int)Day.DayOfWeek + 6) % 7));
        Assert.Equal(expectedMonday, preview.WeekStart); Assert.Equal(expectedMonday.AddDays(6), preview.WeekEnd);
        Assert.Equal(7, preview.Days!.Length);
        Assert.Equal(image.Data, ai.LastImage!.Data);
        Assert.Empty(await h.DbAsync(db => db.WeeklyAvailability.ToArrayAsync(Ct)));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Timetable_uses_only_explicitly_supplied_hours_and_reports_exact_gaps_then_undoes_changes(bool streaming)
    {
        var ai = AutomaticTimetable(); var original = ai.Response;
        ai.Response = now => original(now) with { WeeklySlots = Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 420, 1320)).ToArray() };
        await using var h = await Harness(ai); var user = await h.RegisterAsync();
        await h.DbAsync(async db => {
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Họp nhóm", Start = At(660), End = At(720) });
            db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 1200, EndMinute = 1260, IsAvailable = false, Note = "Ăn tối" });
            var task = new WorkTask { UserId = user.Id, Name = "Báo cáo", Deadline = At(1320), EstimatedMinutes = 60 };
            db.WorkTasks.Add(task);
            db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task.Id, Start = At(1020), End = At(1080) });
            await db.SaveChangesAsync(Ct);
        });
        var result = await Send(h, user.Id, "Nhập thời gian biểu ngày mai. Tôi có thể làm việc mỗi ngày 07:00–22:00, trừ lịch bận; cấu hình giờ này và phân tích.", streaming);
        Assert.Equal("APPLIED", result.Status); Assert.True(result.CanUndo);
        Assert.Contains("Đã lưu thời khóa biểu", result.Reply);
        Assert.DoesNotContain("mặc định", result.Reply);
        var day = Assert.Single(result.Reply.Split('\n'), x => x.StartsWith("- 22/10:"));
        foreach (var gap in new[] { "07:00–08:00 (60 phút)", "10:00–11:00 (60 phút)", "12:00–14:00 (120 phút)",
            "16:00–17:00 (60 phút)", "18:00–20:00 (120 phút)", "21:00–22:00 (60 phút)" }) Assert.Contains(gap, day);
        Assert.Contains("Tổng 480 phút", day); Assert.Contains("Họp nhóm", day); Assert.Contains("Báo cáo", day); Assert.Contains("Ăn tối", day);
        Assert.Contains("Khoảng dài nhất:", result.Reply); Assert.Contains("tối đa 480 phút/ngày", result.Reply);
        var hours = await h.DbAsync(db => db.WeeklyAvailability.ToArrayAsync(Ct));
        Assert.Equal(7, hours.Length); Assert.All(hours, x => { Assert.Equal(420, x.StartMinute); Assert.Equal(1320, x.EndMinute); });
        var undone = await UseAgent(h, s => s.UndoAgentTurnAsync(user.Id, result.Id, Ct));
        Assert.Equal("UNDONE", undone.Status);
        Assert.Empty(await h.DbAsync(db => db.WeeklyAvailability.ToArrayAsync(Ct)));
        Assert.Equal("Họp nhóm", (await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct))).Name);
        Assert.Single(await h.DbAsync(db => db.AvailabilityExceptions.ToArrayAsync(Ct)));
        Assert.Equal(At(1020), (await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct))).Start);
    }

    [Theory] [InlineData("weekly")] [InlineData("dated")] [InlineData("incoming")]
    public async Task Timetable_preserves_explicit_hours_instead_of_expanding_them(string kind)
    {
        var ai = AutomaticTimetable();
        if (kind == "incoming") ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Toán", At(480), At(600), EventType: "CLASS")],
            Exceptions = [new(Day, 1140, 1320, true)] };
        await using var h = await Harness(ai); var user = await h.RegisterAsync();
        if (kind == "weekly") await Setup(h, user.Id);
        if (kind == "dated") await h.DbAsync(async db => {
            db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 1140, EndMinute = 1320, IsAvailable = true });
            await db.SaveChangesAsync(Ct);
        });
        var turn = await Send(h, user.Id, "Nhập lịch học ngày mai và phân tích giờ rảnh.");
        Assert.Equal("APPLIED", turn.Status);
        Assert.DoesNotContain("mặc định", turn.Reply);
        Assert.Contains("19:00–22:00 (180 phút)", turn.Reply);
        Assert.DoesNotContain("07:00–08:00", turn.Reply);
        Assert.Equal(kind == "weekly" ? 1 : 0, await h.DbAsync(db => db.WeeklyAvailability.CountAsync(Ct)));
    }

    [Fact]
    public async Task Timetable_preview_saves_nothing_until_applied_and_never_invents_work_hours()
    {
        await using var h = await Harness(AutomaticTimetable()); var user = await h.RegisterAsync();
        var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Nhập thời khóa biểu ngày mai.", false);
        var preview = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal("PREVIEW", preview.Status); Assert.Contains("chưa lưu thay đổi", preview.Reply);
        Assert.Empty(await h.DbAsync(db => db.WeeklyAvailability.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        var saved = await UseAgent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct));
        Assert.Equal("APPLIED", saved.Status); Assert.DoesNotContain("chưa lưu thay đổi", saved.Reply);
        Assert.Contains("Giờ rảnh đề xuất:", saved.Reply);
        Assert.Contains("bữa trưa/tối 30 phút", saved.Reply);
        Assert.Contains("chưa lưu thành giờ làm việc", saved.Reply);
        Assert.True(saved.SuggestRoutine);
        Assert.Empty(await h.DbAsync(db => db.AvailabilityPolicies.ToArrayAsync(Ct)));
        Assert.DoesNotContain("Rảnh: 07:00", saved.Reply);
        await UseAgent(h, s => s.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal(0, await h.DbAsync(db => db.WeeklyAvailability.CountAsync(Ct)));
        Assert.Equal(2, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
    }

    [Fact]
    public async Task Secretary_imports_fixed_timetable_then_confirms_real_hours_and_schedules_urgent_work_in_a_gap()
    {
        var ai = AutomaticTimetable(); await using var h = await Harness(ai); var user = await h.RegisterAsync();
        var conversation = Guid.NewGuid();
        var imported = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Nhập thời gian biểu cố định ngày mai: Toán 08:00–10:00, Lập trình 14:00–16:00, chỉ một lần."), Ct));
        Assert.Equal("APPLIED", imported.Status);
        Assert.Empty(await h.DbAsync(db => db.WeeklyAvailability.ToArrayAsync(Ct)));
        Assert.Contains("không cần tự khai báo các khung giờ rảnh", imported.Reply);
        var fixedHours = await h.DbAsync(db => db.FixedEvents.OrderBy(x => x.Start).Select(x => new { x.Start, x.End }).ToArrayAsync(Ct));
        ai.Response = _ => AgentAiTestDouble.Empty() with { WeeklySlots = [new((int)Day.DayOfWeek, 420, 1320)] };
        var configured = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Tôi có thể làm việc thứ Năm 07:00–22:00, trừ thời gian biểu. Cấu hình giúp tôi."), Ct));
        Assert.Equal("APPLIED", configured.Status); Assert.Contains("10:00–14:00 (240 phút)", configured.Reply);
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Bài tập gấp", At(1320), 60, 4, "STUDY", true,
            DurationEvidence: "60 phút", DeadlineEvidence: "hạn chót 22:00 ngày mai")] };
        var scheduled = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Bài tập gấp cần 60 phút, hạn chót 22:00 ngày mai. Ưu tiên khẩn cấp và tự xếp vào giờ rảnh đã xác nhận."), Ct));
        Assert.Equal("APPLIED", scheduled.Status);
        var sessions = await h.DbAsync(db => db.WorkSessions.Where(x => x.Status == WorkSessionStatus.Scheduled).ToArrayAsync(Ct));
        Assert.Equal(60, sessions.Sum(x => (x.End - x.Start).TotalMinutes));
        Assert.All(sessions, x => {
            Assert.True(x.Start >= At(420) && x.End <= At(1320));
            Assert.DoesNotContain(fixedHours, lesson => x.Start < lesson.End && lesson.Start < x.End);
        });
        Assert.Equal(fixedHours, await h.DbAsync(db => db.FixedEvents.OrderBy(x => x.Start).Select(x => new { x.Start, x.End }).ToArrayAsync(Ct)));
        Assert.Equal(4, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).Priority);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Timetable_does_not_save_defaults_when_class_conflicts_or_details_are_missing(bool conflict)
    {
        var ai = AutomaticTimetable();
        if (!conflict) ai.Response = _ => AgentAiTestDouble.Empty() with { Question = "Thời khóa biểu áp dụng từ ngày nào?" };
        await using var h = await Harness(ai); var user = await h.RegisterAsync();
        if (conflict) await h.DbAsync(async db => {
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Khám bệnh", Start = At(510), End = At(570) });
            await db.SaveChangesAsync(Ct);
        });
        if (conflict) await Assert.ThrowsAsync<AuthException>(() => Send(h, user.Id, "Nhập thời khóa biểu ngày mai."));
        else Assert.Equal("NEEDS_INPUT", (await Send(h, user.Id, "Nhập thời khóa biểu.")).Status);
        Assert.Empty(await h.DbAsync(db => db.WeeklyAvailability.ToArrayAsync(Ct)));
        Assert.False(await h.DbAsync(db => db.FixedEvents.AnyAsync(x => x.EventType == "CLASS", Ct)));
    }
}
