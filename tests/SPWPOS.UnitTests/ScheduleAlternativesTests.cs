using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class AgentFreeTimeTests
{
    [Fact]
    public async Task Alternatives_rank_the_closest_fitting_time_first_instead_of_a_more_distant_later_time()
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.WeeklyAvailability.RemoveRange(await db.WeeklyAvailability.ToArrayAsync(Ct));
            db.WeeklyAvailability.AddRange(new() { UserId = user.Id, DayOfWeek = (int)Day.DayOfWeek, StartMinute = 480, EndMinute = 720 },
                new() { UserId = user.Id, DayOfWeek = (int)Day.DayOfWeek, StartMinute = 780, EndMinute = 1020 },
                new() { UserId = user.Id, DayOfWeek = (int)Day.DayOfWeek, StartMinute = 1260, EndMinute = 1320 });
            db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 1080, EndMinute = 1140, IsAvailable = false });
            await db.SaveChangesAsync(Ct);
        });
        var error = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.SaveEventAsync(user.Id, null,
            new("Họp", At(1080), At(1140)), Ct)));
        var choices = error.Message.Split("Đề xuất giữ nguyên thời lượng: ")[1].Split(". Bạn chọn")[0].Split("; ");
        Assert.Equal(new[] { "22/10/2026 16:00–17:00", "22/10/2026 21:00–22:00", "22/10/2026 11:00–12:00" }, choices);
        Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
    }

    [Fact]
    public async Task A_nearby_next_day_slot_ranks_before_a_much_earlier_slot_on_the_requested_day()
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.WeeklyAvailability.RemoveRange(await db.WeeklyAvailability.ToArrayAsync(Ct));
            db.WeeklyAvailability.AddRange(new() { UserId = user.Id, DayOfWeek = (int)Day.DayOfWeek, StartMinute = 480, EndMinute = 540 },
                new() { UserId = user.Id, DayOfWeek = (int)Day.AddDays(1).DayOfWeek, StartMinute = 0, EndMinute = 60 });
            db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 1380, EndMinute = 1440, IsAvailable = false });
            await db.SaveChangesAsync(Ct);
        });
        var error = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.SaveEventAsync(user.Id, null,
            new("Họp", At(1380), At(1440)), Ct)));
        Assert.StartsWith("23/10/2026 00:00–01:00; 22/10/2026 08:00–09:00", error.Message.Split("Đề xuất giữ nguyên thời lượng: ")[1]);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Equal_distance_alternatives_prefer_the_later_slot_and_never_offer_a_past_slot(bool earlierSlotIsPast)
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        if (earlierSlotIsPast) h.Clock.Advance(At(1200).AddMinutes(-30) - h.Clock.GetUtcNow());
        await h.DbAsync(async db => {
            db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 1200, EndMinute = 1260, IsAvailable = false });
            await db.SaveChangesAsync(Ct);
        });
        var error = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.SaveEventAsync(user.Id, null,
            new("Họp", At(1200), At(1260)), Ct)));
        Assert.StartsWith("22/10/2026 21:00–22:00", error.Message.Split("Đề xuất giữ nguyên thời lượng: ")[1]);
        if (earlierSlotIsPast) Assert.DoesNotContain("22/10/2026 19:00–20:00", error.Message);
        else Assert.Contains("22/10/2026 19:00–20:00", error.Message);
    }

    [Fact]
    public async Task A_nearby_slot_ending_at_midnight_uses_a_chip_compatible_end_time()
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            var hours = await db.WeeklyAvailability.SingleAsync(Ct); hours.StartMinute = 1380; hours.EndMinute = 1440;
            db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 1260, EndMinute = 1320, IsAvailable = false });
            await db.SaveChangesAsync(Ct);
        });
        var error = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.SaveEventAsync(user.Id, null,
            new("Họp", At(1260), At(1320)), Ct)));
        Assert.StartsWith("22/10/2026 23:00–24:00", error.Message.Split("Đề xuất giữ nguyên thời lượng: ")[1]);
    }

    private static async Task<T> Work<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action)
    {
        await using var scope = h.Provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());
    }

    [Theory] [InlineData("event", false)] [InlineData("exception", false)] [InlineData("session", false)]
    [InlineData("event", true)] [InlineData("exception", true)] [InlineData("session", true)]
    public async Task Manual_and_AI_events_cannot_be_saved_in_busy_time_and_immediately_suggest_valid_alternatives(string blocker, bool agent)
    {
        var ai = Ai(); ai.Response = _ => AgentAiTestDouble.Empty() with {
            ResultKind = "MUTATION", Events = [new("Họp mới", At(1200), At(1260))] };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            if (blocker == "event") db.FixedEvents.Add(new() { UserId = user.Id, Name = "Họp đã có", Start = At(1200), End = At(1260) });
            else if (blocker == "exception") db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 1200, EndMinute = 1260, IsAvailable = false });
            else {
                var task = new WorkTask { UserId = user.Id, Name = "Việc đã xếp", Deadline = At(1440) }; db.WorkTasks.Add(task);
                db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task.Id, Start = At(1200), End = At(1260), Status = WorkSessionStatus.Scheduled });
            }
            await db.SaveChangesAsync(Ct);
        });
        var revision = await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct));
        var error = await Assert.ThrowsAsync<AuthException>(async () => {
            if (agent) await Send(h, user.Id, "Thêm cuộc họp mới ngày mai từ 20:00 đến 21:00.");
            else await Work(h, s => s.SaveEventAsync(user.Id, null, new("Họp mới", At(1200), At(1260)), Ct));
        });
        Assert.Equal(409, error.StatusCode);
        Assert.Equal(blocker switch { "event" => "FIXED_EVENT_OVERLAP", "exception" => "BUSY_TIME_CONFLICT", _ => "SESSION_OVERLAP" }, error.Code);
        Assert.Contains("Đề xuất giữ nguyên thời lượng", error.Message);
        Assert.Contains("21:00–22:00", error.Message); Assert.Contains("19:00–20:00", error.Message);
        Assert.Equal(blocker == "event" ? 1 : 0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
        Assert.Equal(revision, await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct)));
        if (agent) {
            var stored = await h.DbAsync(db => db.AgentTurns.SingleAsync(Ct));
            Assert.Equal("FAILED", stored.Status); Assert.Contains("21:00", stored.ResultJson);
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Moving_a_work_session_into_busy_time_suggests_a_slot_that_preserves_duration_and_deadline(bool occupiedBySession)
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var sessionId = Guid.NewGuid();
        await h.DbAsync(async db => {
            var task = new WorkTask { UserId = user.Id, Name = "Việc cần dời", Deadline = At(1320), IsHardDeadline = true }; db.WorkTasks.Add(task);
            db.WorkSessions.Add(new() { Id = sessionId, UserId = user.Id, TaskId = task.Id, Start = At(1140), End = At(1200), Status = WorkSessionStatus.Scheduled });
            if (occupiedBySession) {
                var other = new WorkTask { UserId = user.Id, Name = "Việc khác", Deadline = At(1440) }; db.WorkTasks.Add(other);
                db.WorkSessions.Add(new() { UserId = user.Id, TaskId = other.Id, Start = At(1200), End = At(1260), Status = WorkSessionStatus.Scheduled });
            } else db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 1200, EndMinute = 1260, IsAvailable = false });
            await db.SaveChangesAsync(Ct);
        });
        var error = await Assert.ThrowsAsync<AuthException>(() => Work(h, async s => {
            await s.EditSessionAsync(user.Id, sessionId, new(At(1200), At(1260)), Ct); return true;
        }));
        Assert.Contains("21:00–22:00", error.Message);
        Assert.DoesNotContain("29/10", error.Message); // Beyond this task's hard deadline.
        var original = await h.DbAsync(db => db.WorkSessions.SingleAsync(x => x.Id == sessionId, Ct));
        Assert.Equal(At(1140), original.Start); Assert.Equal(At(1200), original.End);
    }

    [Fact]
    public async Task An_event_can_touch_an_existing_event_boundary_and_editing_does_not_conflict_with_itself()
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var first = await Work(h, s => s.SaveEventAsync(user.Id, null, new("Họp cũ", At(1140), At(1200)), Ct));
        await Work(h, s => s.SaveEventAsync(user.Id, first, new("Họp đổi tên", At(1140), At(1200)), Ct));
        await Work(h, s => s.SaveEventAsync(user.Id, null, new("Họp tiếp", At(1200), At(1260)), Ct));
        Assert.Equal(2, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
    }

    [Fact]
    public async Task Suggested_recurring_event_times_are_checked_against_every_occurrence()
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 1200, EndMinute = 1260, IsAvailable = false });
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Bận tuần sau", Start = At(1260).AddDays(7), End = At(1320).AddDays(7) });
            await db.SaveChangesAsync(Ct);
        });
        var error = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.SaveEventAsync(user.Id, null,
            new("Họp hai tuần", At(1200), At(1260), RepeatWeeks: 2), Ct)));
        Assert.Contains("19:00–20:00", error.Message); Assert.DoesNotContain("21:00–22:00", error.Message);
        Assert.Equal(1, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
    }

    [Fact]
    public async Task A_combined_AI_request_cannot_add_an_event_over_its_own_new_busy_exception()
    {
        var ai = Ai(); ai.Response = _ => AgentAiTestDouble.Empty() with {
            Events = [new("Họp", At(1200), At(1260))], Exceptions = [new(Day, 1200, 1260, false)] };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var error = await Assert.ThrowsAsync<AuthException>(() => Send(h, user.Id,
            "Ngày mai đánh dấu bận 20:00–21:00 và thêm cuộc họp 20:00–21:00."));
        Assert.Equal("BUSY_TIME_CONFLICT", error.Code); Assert.Contains("21:00–22:00", error.Message);
        Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.AvailabilityExceptions.CountAsync(Ct)));
    }

    [Fact]
    public async Task A_suggested_work_session_keeps_the_configured_break_between_sessions()
    {
        var ai = Ai(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var id = Guid.NewGuid();
        await h.DbAsync(async db => {
            (await db.WeeklyAvailability.SingleAsync(Ct)).EndMinute = 1380;
            (await db.WorkPreferences.SingleAsync(Ct)).BreakMinutes = 15;
            var task = new WorkTask { UserId = user.Id, Name = "Việc", Deadline = At(1380), IsHardDeadline = true };
            var other = new WorkTask { UserId = user.Id, Name = "Việc khác", Deadline = At(1380) }; db.WorkTasks.AddRange(task, other);
            db.WorkSessions.AddRange(new() { Id = id, UserId = user.Id, TaskId = task.Id, Start = At(1140), End = At(1200) },
                new() { UserId = user.Id, TaskId = other.Id, Start = At(1200), End = At(1260) });
            await db.SaveChangesAsync(Ct);
        });
        var error = await Assert.ThrowsAsync<AuthException>(() => Work(h, async s => {
            await s.EditSessionAsync(user.Id, id, new(At(1200), At(1260)), Ct); return true;
        }));
        Assert.Contains("21:15–22:15", error.Message); Assert.DoesNotContain("21:00–22:00", error.Message);
    }

    [Fact]
    public async Task A_task_requested_inside_busy_time_proposes_a_new_window_without_saving_or_relaxing_its_deadline()
    {
        var ai = Ai(); ai.Response = _ => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Báo cáo", At(1320), 60, 2, "WRITING", false, At(1200), At(1260),
                "60 phút", "hạn chót 22:00 ngày 22/10/2026", "từ 20:00 đến 21:00 ngày 22/10/2026")] };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 1200, EndMinute = 1260, IsAvailable = false });
            await db.SaveChangesAsync(Ct);
        });
        var turn = await Send(h, user.Id, "Làm Báo cáo 60 phút, chỉ làm từ 20:00 đến 21:00 ngày 22/10/2026, hạn chót 22:00 ngày 22/10/2026. Xếp lịch giúp tôi.");
        Assert.Equal("BLOCKED", turn.Status); Assert.Contains("nếu bạn đồng ý đổi khung giờ", turn.Reply);
        Assert.Contains("21:00–22:00", turn.Reply); Assert.DoesNotContain("29/10", turn.Reply);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
        ai.Response = _ => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Báo cáo", At(1320), 60, 2, "WRITING", false, At(1260), At(1320),
                "60 phút", "hạn chót 22:00 ngày 22/10/2026", "từ 21:00 đến 22:00 ngày 22/10/2026")] };
        var selected = await Send(h, user.Id, "Tôi chọn từ 21:00 đến 22:00 ngày 22/10/2026, xếp theo đề xuất này.", conversationId: turn.ConversationId);
        Assert.Equal("APPLIED", selected.Status);
        using var context = System.Text.Json.JsonDocument.Parse(ai.Context);
        Assert.Equal(60, context.RootElement.GetProperty("pendingRequest").GetProperty("estimatedMinutes").GetInt32());
        var saved = await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct));
        Assert.Equal(At(1260), saved.Start); Assert.Equal(At(1320), saved.End);
        Assert.Equal(1, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }
}
