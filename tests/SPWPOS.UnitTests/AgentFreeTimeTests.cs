using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class AgentFreeTimeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateOnly Day = new(2026, 10, 22);
    private static DateTimeOffset At(int minute) => PlanningEngine.At(Day, minute, PlanningEngine.Zone("Asia/Ho_Chi_Minh"));
    private static AgentFreeTimeQuery Query => new(At(0), At(1440));
    private static AgentAiTestDouble Ai() => new() { Response = _ => AgentAiTestDouble.Empty() with {
        Reply = "Ngày mai bạn rảnh 19:00 đến 22:00, ngoại trừ 20:00 đến 21:00.",
        ResultKind = "ANSWER", FreeTimeQuery = Query } };
    private static async Task<ServiceHarness> Harness(AgentAiTestDouble ai)
    {
        var h = await ServiceHarness.CreateAsync(s => s.AddSingleton<ITaskInterpreter>(ai));
        // UTC is still the 20th; the account-local date is already the 21st.
        h.Clock.Advance(DateTimeOffset.Parse("2026-10-20T17:30:00Z") - h.Clock.GetUtcNow());
        return h;
    }
    private static Task Setup(ServiceHarness h, Guid uid) => h.DbAsync(async db => {
        db.WorkPreferences.Add(new() { UserId = uid, TimeZoneId = "Asia/Ho_Chi_Minh", BreakMinutes = 0 });
        db.WeeklyAvailability.Add(new() { UserId = uid, DayOfWeek = (int)Day.DayOfWeek, StartMinute = 1140, EndMinute = 1320 });
        await db.SaveChangesAsync(Ct);
    });
    private static async Task<AgentTurnView> Send(ServiceHarness h, Guid uid, string text = "Ngày mai tôi có thể làm việc trong khoảng giờ rảnh nào? Chỉ xem giờ rảnh trong cài đặt và các mục lịch đã lưu, chưa thay đổi lịch.", bool streaming = false, Guid? conversationId = null)
    {
        await using var scope = h.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlanningAgentService>();
        var input = new AgentTurnInput(conversationId ?? Guid.NewGuid(), Guid.NewGuid(), text, true);
        return streaming ? await service.SendAgentTurnStreamingAsync(uid, input, (_, _) => Task.CompletedTask, Ct)
            : await service.SendAgentTurnAsync(uid, input, Ct);
    }

    [Theory]
    [InlineData("exception", false)] [InlineData("event", false)] [InlineData("session", false)] [InlineData("event", true)]
    public async Task Free_time_is_split_into_exact_intervals_without_changing_the_calendar(string blocker, bool streaming)
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            if (blocker == "exception") db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 1200, EndMinute = 1260, IsAvailable = false });
            else if (blocker == "event") db.FixedEvents.Add(new() { UserId = user.Id, Name = "Họp", Start = At(1200), End = At(1260) });
            else {
                var task = new WorkTask { UserId = user.Id, Name = "Việc đã xếp", Deadline = At(1440) }; db.WorkTasks.Add(task);
                db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task.Id, Start = At(1200), End = At(1260), Status = WorkSessionStatus.Scheduled });
            }
            await db.SaveChangesAsync(Ct);
        });
        var revision = await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct));
        var turn = await Send(h, user.Id, streaming: streaming);
        Assert.Equal("ANSWERED", turn.Status); Assert.Empty(turn.Changes); Assert.Null(turn.Plan); Assert.False(turn.CanUndo);
        Assert.Contains("22/10/2026", turn.Reply); Assert.Contains("19:00–20:00 (60 phút)", turn.Reply);
        Assert.Contains("21:00–22:00 (60 phút)", turn.Reply); Assert.Contains("Tổng cộng 120 phút", turn.Reply);
        Assert.DoesNotContain("ngoại trừ", turn.Reply); Assert.DoesNotContain("19:00–22:00", turn.Reply);
        Assert.Equal(revision, await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.PlanProposals.CountAsync(Ct)));
        using var context = JsonDocument.Parse(ai.Context);
        var verified = context.RootElement.GetProperty("verifiedFreeTime");
        var intervals = verified.GetProperty("intervals").EnumerateArray().Where(x => x.GetProperty("date").GetString() == "2026-10-22").ToArray();
        Assert.Equal(2, intervals.Length); Assert.All(intervals, x => Assert.Equal(60, x.GetProperty("minutes").GetInt32()));
        Assert.Equal("2026-10-22T19:00:00+07:00", intervals[0].GetProperty("start").GetString());
    }

    [Fact]
    public async Task Dated_replacement_and_overlapping_busy_intervals_are_applied_before_answering()
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.AvailabilityExceptions.AddRange(
                new() { UserId = user.Id, Date = Day, StartMinute = 720, EndMinute = 960, IsAvailable = true, OverridesWeeklyAvailability = true },
                new() { UserId = user.Id, Date = Day, StartMinute = 600, EndMinute = 660, IsAvailable = true },
                new() { UserId = user.Id, Date = Day, StartMinute = 810, EndMinute = 900, IsAvailable = false });
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Họp", Start = At(780), End = At(840) });
            await db.SaveChangesAsync(Ct);
        });
        var turn = await Send(h, user.Id);
        Assert.Contains("12:00–13:00", turn.Reply); Assert.Contains("15:00–16:00", turn.Reply);
        Assert.DoesNotContain("19:00", turn.Reply); Assert.DoesNotContain("10:00", turn.Reply);
        Assert.Contains("Tổng cộng 120 phút", turn.Reply);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Missing_or_fully_occupied_availability_never_becomes_invented_free_time(bool fullyOccupied)
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            if (fullyOccupied) db.FixedEvents.Add(new() { UserId = user.Id, Name = "Bận", Start = At(1140), End = At(1320) });
            else db.WeeklyAvailability.RemoveRange(await db.WeeklyAvailability.ToArrayAsync(Ct));
            await db.SaveChangesAsync(Ct);
        });
        var turn = await Send(h, user.Id);
        Assert.Contains("không còn khoảng rảnh khả dụng", turn.Reply); Assert.DoesNotContain("19:00", turn.Reply);
        Assert.Equal("ANSWERED", turn.Status);
    }

    [Theory] [InlineData(WorkSessionStatus.Cancelled)] [InlineData(WorkSessionStatus.Skipped)] [InlineData(WorkSessionStatus.Missed)]
    public async Task Unoccupied_session_statuses_and_unscheduled_tasks_do_not_block_free_time(WorkSessionStatus status)
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            var task = new WorkTask { UserId = user.Id, Name = "Việc chưa xếp", Deadline = At(1440) }; db.WorkTasks.Add(task);
            db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task.Id, Start = At(1200), End = At(1260), Status = status });
            await db.SaveChangesAsync(Ct);
        });
        var turn = await Send(h, user.Id);
        Assert.Contains("19:00–22:00 (180 phút)", turn.Reply);
    }

    [Fact]
    public async Task Events_beyond_the_raw_context_page_still_block_the_verified_free_intervals()
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.FixedEvents.AddRange(Enumerable.Range(0, 100).Select(i => new FixedEvent {
                UserId = user.Id, Name = $"Sự kiện {i}", Start = At(0).AddDays(-1).AddMinutes(60 + i), End = At(0).AddDays(-1).AddMinutes(61 + i) }));
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Bận tối mai", Start = At(1200), End = At(1260) });
            await db.SaveChangesAsync(Ct);
        });
        var turn = await Send(h, user.Id);
        Assert.Contains("19:00–20:00", turn.Reply); Assert.Contains("21:00–22:00", turn.Reply);
        using var context = JsonDocument.Parse(ai.Context);
        Assert.True(context.RootElement.GetProperty("scope").GetProperty("events").GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task A_query_can_be_narrowed_to_requested_hours()
    {
        var ai = Ai(); ai.Response = _ => AgentAiTestDouble.Empty() with { FreeTimeQuery = new(At(1170), At(1290)) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var turn = await Send(h, user.Id, "Chỉ xem giờ rảnh ngày mai từ 19:30 đến 21:30.");
        Assert.Contains("19:30–21:30 (120 phút)", turn.Reply);
    }

    [Theory] [InlineData("wrong-day")] [InlineData("outside-scope")] [InlineData("mutation")]
    public async Task An_unverified_query_cannot_be_reported_as_confirmed_free_time(string mode)
    {
        var ai = Ai(); ai.Response = _ => mode switch {
            "wrong-day" => AgentAiTestDouble.Empty() with { FreeTimeQuery = new(At(0).AddDays(-1), At(0)) },
            "outside-scope" => AgentAiTestDouble.Empty() with { FreeTimeQuery = new(At(0).AddDays(40), At(0).AddDays(41)) },
            _ => AgentAiTestDouble.Empty() with { FreeTimeQuery = Query, Events = [new("Họp", At(1200), At(1260))] }
        };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        if (mode == "mutation") {
            var error = await Assert.ThrowsAsync<AuthException>(() => Send(h, user.Id)); Assert.Equal("AGENT_INVALID_INTENT", error.Code);
        } else {
            var turn = await Send(h, user.Id, mode == "wrong-day" ? "Ngày mai tôi rảnh giờ nào?" : "Cho tôi xem giờ rảnh trong khoảng đã chọn.");
            Assert.Equal(mode == "wrong-day" ? "NEEDS_INPUT" : "ANSWERED", turn.Status);
            Assert.DoesNotContain("19:00", turn.Reply);
        }
        Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
    }
}
