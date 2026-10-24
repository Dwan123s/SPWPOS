using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class AgentFreeTimeTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Urgency_survives_a_duration_clarification_but_can_be_explicitly_cancelled(bool cancelUrgency)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Question = "Cuộc họp kéo dài bao lâu?" } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Họp dự án", IsUrgent = true, EventType = "MEETING", Start = At(900), End = At(1020) });
            await db.SaveChangesAsync(Ct);
        });
        var conversation = Guid.NewGuid();
        var question = await UseAgent(h, service => service.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Ngày mai tôi có cuộc họp phát sinh khẩn lúc 16:00.", true), Ct));
        Assert.Equal("NEEDS_INPUT", question.Status);
        ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Họp phát sinh", At(960), At(1080), EventType: "MEETING")] };
        var text = cancelUrgency ? "2 tiếng, cuộc họp này bình thường, không khẩn." : "2 tiếng.";
        if (cancelUrgency) {
            var error = await Assert.ThrowsAsync<AuthException>(() => UseAgent(h, service => service.SendAgentTurnAsync(user.Id,
                new(conversation, Guid.NewGuid(), text, true), Ct)));
            Assert.Equal("FIXED_EVENT_OVERLAP", error.Code);
        } else {
            var preview = await UseAgent(h, service => service.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), text, true), Ct));
            Assert.Equal("PREVIEW", preview.Status); Assert.Contains("lịch khẩn cũ", preview.Reply);
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task New_urgent_event_takes_precedence_over_a_saved_urgent_event_and_undo_preserves_urgency(bool legacyTitle)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Events = [new("Họp phát sinh", At(960), At(1080), EventType: "MEETING")] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var old = new FixedEvent { UserId = user.Id, Name = legacyTitle ? "Họp khẩn cũ" : "Họp dự án", EventType = "MEETING",
            Start = At(900), End = At(1020), IsUrgent = legacyTitle ? null : true };
        await h.DbAsync(async db => { db.FixedEvents.Add(old); await db.SaveChangesAsync(Ct); });
        var request = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Ngày mai có cuộc họp phát sinh khẩn từ 16:00 đến 18:00.", true);
        var preview = await UseAgent(h, service => service.SendAgentTurnAsync(user.Id, request, Ct));
        Assert.Equal("PREVIEW", preview.Status); Assert.True(preview.Plan!.RequiresConfirmation);
        var move = Assert.Single(preview.Changes, change => change.Name == old.Name);
        Assert.Contains("18:00", move.After); Assert.Contains("20:00", move.After);
        Assert.Contains("Lịch khẩn mới được ưu tiên trước lịch khẩn cũ", move.Reason);
        Assert.Contains("lịch khẩn cũ", preview.Reply);
        Assert.Equal(At(900), (await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct))).Start);
        var applied = await UseAgent(h, service => service.ApplyAgentTurnAsync(user.Id, preview.Id, Ct));
        var saved = await h.DbAsync(db => db.FixedEvents.SingleAsync(item => item.Name == "Họp phát sinh", Ct));
        Assert.True(saved.IsUrgent); Assert.Equal(At(960), saved.Start); Assert.Equal(At(1080), saved.End);
        var moved = await h.DbAsync(db => db.FixedEvents.SingleAsync(item => item.Id == old.Id, Ct));
        Assert.Equal(At(1080), moved.Start); Assert.Equal(TimeSpan.FromHours(2), moved.End - moved.Start);
        await UseAgent(h, service => service.UndoAgentTurnAsync(user.Id, applied.Id, Ct));
        var restored = await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct));
        Assert.Equal(old.Id, restored.Id); Assert.Equal(At(900), restored.Start); Assert.Equal(old.IsUrgent, restored.IsUrgent);
    }

    [Fact]
    public async Task A_later_urgent_request_remains_highest_priority_after_previous_urgent_events_were_saved_with_ordinary_titles()
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        async Task<AgentTurnView> Add(string name, int from, int to)
        {
            ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new(name, At(from), At(to), EventType: "MEETING")] };
            return await UseAgent(h, service => service.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(),
                $"Ngày mai có cuộc họp khẩn {name} từ {from / 60}:00 đến {to / 60}:00.", true), Ct));
        }
        Assert.Equal("APPLIED", (await Add("Dự án A", 900, 1020)).Status);
        var b = await Add("Dự án B", 960, 1080);
        Assert.Equal("PREVIEW", b.Status);
        await UseAgent(h, service => service.ApplyAgentTurnAsync(user.Id, b.Id, Ct));
        var c = await Add("Dự án C", 1020, 1140);
        Assert.Equal("PREVIEW", c.Status);
        Assert.Equal(2, c.Changes.Count(change => change.Before.Length > 0));
        Assert.All(c.Changes.Where(change => change.Before.Length > 0), change => Assert.Contains("lịch khẩn cũ", change.Reason));
        await UseAgent(h, service => service.ApplyAgentTurnAsync(user.Id, c.Id, Ct));
        var saved = await h.DbAsync(db => db.FixedEvents.OrderBy(item => item.Start).ToArrayAsync(Ct));
        Assert.Equal("Dự án C", saved[0].Name); Assert.Equal(At(1020), saved[0].Start);
        Assert.All(saved, item => Assert.True(item.IsUrgent));
        Assert.All(saved.Zip(saved.Skip(1)), pair => Assert.True(pair.First.End <= pair.Second.Start));
    }

    [Fact]
    public async Task An_existing_urgent_task_session_yields_to_a_new_urgent_appointment()
    {
        var ai = UrgentMeetingAi(); await using var h = await Harness(ai); var user = await h.RegisterAsync();
        var task = await SetupShopping(h, user.Id, session: true);
        await h.DbAsync(async db => { (await db.WorkTasks.SingleAsync(Ct)).Priority = 4; await db.SaveChangesAsync(Ct); });
        var preview = await UseAgent(h, service => service.SendAgentTurnAsync(user.Id,
            new(Guid.NewGuid(), Guid.NewGuid(), UrgentMeetingRequest, true), Ct));
        Assert.Equal("PREVIEW", preview.Status);
        Assert.Equal(At(1020), Assert.Single(preview.Plan!.Sessions, item => item.TaskId == task!.Id && !item.Preserved).Start);
        Assert.Contains(preview.Changes, change => change.TaskId == task!.Id && change.Reason.Contains("lịch khẩn cũ"));
    }

    [Fact]
    public async Task An_older_urgent_event_can_move_to_its_original_clock_time_the_next_day_when_today_is_full()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Events = [new("Họp mới", At(960), At(1080), EventType: "MEETING")] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.FixedEvents.AddRange(new FixedEvent { UserId = user.Id, Name = "Họp cũ", EventType = "MEETING", IsUrgent = true, Start = At(900), End = At(1020) },
                new FixedEvent { UserId = user.Id, Name = "Lịch cố định", EventType = "APPOINTMENT", Start = At(1080), End = At(1440) });
            await db.SaveChangesAsync(Ct);
        });
        var preview = await UseAgent(h, service => service.SendAgentTurnAsync(user.Id,
            new(Guid.NewGuid(), Guid.NewGuid(), "Ngày mai có cuộc họp mới khẩn từ 16:00 đến 18:00.", true), Ct));
        Assert.Equal("PREVIEW", preview.Status);
        var moved = Assert.Single(preview.Changes, item => item.Name == "Họp cũ");
        Assert.Contains("23/10/2026 15:00", moved.After); Assert.Contains("17:00", moved.After);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Urgent_meeting_explains_exactly_which_work_shift_moves_and_requires_permission_to_change_the_shift(bool nextHourBusy)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Events = [new("Họp khẩn", At(960), At(1080), EventType: "MEETING")] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync();
        await SetupShopping(h, user.Id);
        await h.DbAsync(async db => {
            var shift = await db.FixedEvents.SingleAsync(item => item.Name == "Đi mua sắm", Ct);
            shift.Name = "Đi làm"; shift.EventType = "WORK"; shift.End = At(1080);
            if (!nextHourBusy) db.FixedEvents.Remove(await db.FixedEvents.SingleAsync(item => item.Name == "Hẹn với thầy", Ct));
            await db.SaveChangesAsync(Ct);
        });
        var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Chiều mai 4h tôi có lịch họp khẩn trong 2 tiếng.", true);
        var preview = await UseAgent(h, service => service.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal("PREVIEW", preview.Status); Assert.True(preview.Plan!.RequiresConfirmation);
        var move = Assert.Single(preview.Changes, change => change.Name == "Đi làm");
        Assert.Contains("15:00", move.Before); Assert.Contains("18:00", move.Before);
        Assert.Contains(nextHourBusy ? "19:00" : "18:00", move.After);
        Assert.Contains(nextHourBusy ? "22:00" : "21:00", move.After);
        Assert.Contains("ca làm", move.Reason); Assert.Contains("được phép đổi ca", move.Reason);
        Assert.Contains("Đi làm", preview.Reply); Assert.Contains("Họp khẩn", preview.Reply);
        Assert.Contains("16:00", preview.Reply); Assert.Contains("giờ cũ", preview.Reply); Assert.Contains("giờ mới", preview.Reply);
        Assert.Contains("được phép đổi ca", preview.Reply);
        Assert.Equal(At(900), (await h.DbAsync(db => db.FixedEvents.SingleAsync(item => item.Name == "Đi làm", Ct))).Start);
        Assert.False(await h.DbAsync(db => db.FixedEvents.AnyAsync(item => item.EventType == "MEETING", Ct)));
        var replay = await UseAgent(h, service => service.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal("PREVIEW", replay.Status);
        var applied = await UseAgent(h, service => service.ApplyAgentTurnAsync(user.Id, preview.Id, Ct));
        Assert.DoesNotContain("Chưa thay đổi lịch", applied.Reply);
        Assert.Contains("Đã lưu phương án bạn xác nhận", applied.Reply);
        var saved = await h.DbAsync(db => db.FixedEvents.SingleAsync(item => item.Name == "Đi làm", Ct));
        Assert.Equal(At(nextHourBusy ? 1140 : 1080), saved.Start); Assert.Equal(TimeSpan.FromHours(3), saved.End - saved.Start);
        Assert.Equal(At(960), (await h.DbAsync(db => db.FixedEvents.SingleAsync(item => item.EventType == "MEETING", Ct))).Start);
        await UseAgent(h, service => service.UndoAgentTurnAsync(user.Id, applied.Id, Ct));
        Assert.Equal(At(900), (await h.DbAsync(db => db.FixedEvents.SingleAsync(item => item.Name == "Đi làm", Ct))).Start);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task An_immovable_work_shift_explains_which_event_alternative_hours_belong_to(bool explicitRefusal)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Events = [new("Họp khẩn", At(960), At(1080), EventType: "MEETING")] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await SetupShopping(h, user.Id);
        await h.DbAsync(async db => {
            var shift = await db.FixedEvents.SingleAsync(item => item.Name == "Đi mua sắm", Ct);
            shift.Name = "Đi làm"; shift.EventType = "WORK"; shift.End = At(1080);
            shift.Description = explicitRefusal ? "" : "Ca làm cố định, không thể đổi giờ";
            await db.SaveChangesAsync(Ct);
        });
        var request = "Chiều mai 4h tôi có lịch họp khẩn trong 2 tiếng." + (explicitRefusal ? " Không dời lịch Đi làm." : "");
        var error = await Assert.ThrowsAsync<AuthException>(() => UseAgent(h, service => service.SendAgentTurnAsync(user.Id,
            new(Guid.NewGuid(), Guid.NewGuid(), request, true), Ct)));
        Assert.Equal("FIXED_EVENT_OVERLAP", error.Code);
        Assert.Contains("cần dời “Đi làm”", error.Message);
        Assert.Contains("giữ nguyên “Đi làm”", error.Message);
        Assert.Contains("đổi giờ “Họp khẩn”", error.Message);
        Assert.Equal(At(900), (await h.DbAsync(db => db.FixedEvents.SingleAsync(item => item.Name == "Đi làm", Ct))).Start);
        Assert.False(await h.DbAsync(db => db.FixedEvents.AnyAsync(item => item.EventType == "MEETING", Ct)));
    }

    private static AgentAiTestDouble UrgentMeetingAi() => new() { Response = _ => AgentAiTestDouble.Empty() with {
        Events = [new("Cuộc họp khẩn", At(900), At(1020), EventType: "MEETING")] } };
    private const string UrgentMeetingRequest = "Ngày mai tôi có cuộc họp khẩn từ 15:00 đến 17:00, ưu tiên cuộc họp này.";
    private static async Task<WorkTask?> SetupShopping(ServiceHarness h, Guid uid, bool session = false, bool locked = false)
    {
        await Setup(h, uid);
        WorkTask? task = null;
        await h.DbAsync(async db => {
            db.WeeklyAvailability.RemoveRange(await db.WeeklyAvailability.ToArrayAsync(Ct));
            db.WeeklyAvailability.Add(new() { UserId = uid, DayOfWeek = (int)Day.DayOfWeek, StartMinute = 480, EndMinute = 1320 });
            if (session) {
                task = new() { UserId = uid, Name = "Đi mua sắm", Deadline = At(1320), EstimatedMinutes = 60, Splittable = false, IsHardDeadline = true };
                db.WorkTasks.Add(task);
                db.WorkSessions.Add(new() { UserId = uid, TaskId = task.Id, Start = At(900), End = At(960), IsLocked = locked });
            } else db.FixedEvents.Add(new() { UserId = uid, Name = "Đi mua sắm", Start = At(900), End = At(960), EventType = "OTHER" });
            db.FixedEvents.Add(new() { UserId = uid, Name = "Hẹn với thầy", Start = At(1080), End = At(1140), EventType = "APPOINTMENT" });
            await db.SaveChangesAsync(Ct);
        });
        return task;
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)]
    public async Task An_urgent_meeting_keeps_15_to_17_and_previews_shopping_after_it_then_applies_and_undoes(bool workSession, bool streaming)
    {
        var ai = UrgentMeetingAi(); await using var h = await Harness(ai); var user = await h.RegisterAsync();
        var task = await SetupShopping(h, user.Id, workSession);
        var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), UrgentMeetingRequest, true);
        var result = await UseAgent(h, service => streaming
            ? service.SendAgentTurnStreamingAsync(user.Id, input, (_, _) => Task.CompletedTask, Ct)
            : service.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal("PREVIEW", result.Status); Assert.True(result.Plan!.HasUrgentReplan); Assert.Empty(result.Plan.Warnings);
        Assert.Contains(result.Changes, change => change.Name == "Đi mua sắm" && change.Before.Contains("15:00") && change.After.Contains("17:00"));
        Assert.False(await h.DbAsync(db => db.FixedEvents.AnyAsync(x => x.EventType == "MEETING", Ct)));
        var retry = await UseAgent(h, service => service.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal(result.Id, retry.Id); Assert.Equal("PREVIEW", retry.Status);
        await UseAgent(h, service => service.ApplyAgentTurnAsync(user.Id, result.Id, Ct));
        var meeting = await h.DbAsync(db => db.FixedEvents.SingleAsync(x => x.EventType == "MEETING", Ct));
        Assert.Equal(At(900), meeting.Start); Assert.Equal(At(1020), meeting.End);
        if (task is not null) {
            var moved = await h.DbAsync(db => db.WorkSessions.SingleAsync(x => x.TaskId == task.Id && x.Status == WorkSessionStatus.Scheduled, Ct));
            Assert.Equal(At(1020), moved.Start); Assert.Equal(At(1080), moved.End);
        } else {
            var moved = await h.DbAsync(db => db.FixedEvents.SingleAsync(x => x.Name == "Đi mua sắm", Ct));
            Assert.Equal(At(1020), moved.Start); Assert.Equal(At(1080), moved.End);
        }
        Assert.Equal(At(1080), (await h.DbAsync(db => db.FixedEvents.SingleAsync(x => x.Name == "Hẹn với thầy", Ct))).Start);
        await UseAgent(h, service => service.UndoAgentTurnAsync(user.Id, result.Id, Ct));
        Assert.False(await h.DbAsync(db => db.FixedEvents.AnyAsync(x => x.EventType == "MEETING", Ct)));
        if (task is not null) Assert.Equal(At(900), (await h.DbAsync(db => db.WorkSessions.SingleAsync(x => x.TaskId == task.Id && x.Status == WorkSessionStatus.Scheduled, Ct))).Start);
        else Assert.Equal(At(900), (await h.DbAsync(db => db.FixedEvents.SingleAsync(x => x.Name == "Đi mua sắm", Ct))).Start);
    }

    [Theory] [InlineData("ordinary")] [InlineData("appointment")] [InlineData("locked")] [InlineData("negated")]
    [InlineData("refuseMove")] [InlineData("running")] [InlineData("unknown")] [InlineData("lookalike")]
    public async Task Urgent_event_displacement_does_not_override_existing_commitments_or_invent_urgency(string mode)
    {
        var ai = UrgentMeetingAi(); await using var h = await Harness(ai); var user = await h.RegisterAsync();
        await SetupShopping(h, user.Id, mode is "locked" or "running", mode == "locked");
        if (mode == "appointment") await h.DbAsync(async db => {
            (await db.FixedEvents.SingleAsync(x => x.Name == "Đi mua sắm", Ct)).EventType = "APPOINTMENT";
            await db.SaveChangesAsync(Ct);
        });
        if (mode == "running") await h.DbAsync(async db => {
            (await db.WorkSessions.SingleAsync(Ct)).Status = WorkSessionStatus.Running;
            await db.SaveChangesAsync(Ct);
        });
        if (mode == "unknown") await h.DbAsync(async db => {
            (await db.FixedEvents.SingleAsync(x => x.Name == "Đi mua sắm", Ct)).Name = "Hẹn khám bệnh";
            await db.SaveChangesAsync(Ct);
        });
        var text = mode == "ordinary" ? "Ngày mai tôi có cuộc họp từ 15:00 đến 17:00."
            : mode == "lookalike" ? "Ngày mai tôi gặp khách từ 15:00 đến 17:00."
            : mode == "negated" ? "Ngày mai tôi có cuộc họp không khẩn từ 15:00 đến 17:00, không dời lịch mua sắm."
            : mode == "refuseMove" ? UrgentMeetingRequest + " Không dời lịch mua sắm."
            : UrgentMeetingRequest;
        var error = await Assert.ThrowsAsync<AuthException>(() => UseAgent(h, service => service.SendAgentTurnAsync(user.Id,
            new(Guid.NewGuid(), Guid.NewGuid(), text, true), Ct)));
        Assert.Equal(mode is "locked" or "running" ? "LOCKED_SESSION_CONFLICT" : mode == "urgentTask" ? "SESSION_OVERLAP" : "FIXED_EVENT_OVERLAP", error.Code);
        Assert.False(await h.DbAsync(db => db.FixedEvents.AnyAsync(x => x.EventType == "MEETING", Ct)));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Shopping_moves_to_the_next_free_gap_or_nothing_is_saved_if_the_same_day_is_full(bool full)
    {
        var ai = UrgentMeetingAi(); await using var h = await Harness(ai); var user = await h.RegisterAsync();
        await SetupShopping(h, user.Id);
        await h.DbAsync(async db => {
            db.AvailabilityExceptions.Add(new() { UserId = user.Id, Date = Day, StartMinute = 1020, EndMinute = full ? 1440 : 1200, IsAvailable = false });
            await db.SaveChangesAsync(Ct);
        });
        var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), UrgentMeetingRequest, true);
        if (full) {
            var error = await Assert.ThrowsAsync<AuthException>(() => UseAgent(h, service => service.SendAgentTurnAsync(user.Id, input, Ct)));
            Assert.Equal("URGENT_EVENT_REPLAN_INFEASIBLE", error.Code);
        } else {
            var preview = await UseAgent(h, service => service.SendAgentTurnAsync(user.Id, input, Ct));
            Assert.Equal("PREVIEW", preview.Status);
            var moved = Assert.Single(preview.Changes, change => change.Name == "Đi mua sắm");
            Assert.Contains("20:00", moved.After); Assert.Contains("21:00", moved.After);
        }
        Assert.False(await h.DbAsync(db => db.FixedEvents.AnyAsync(x => x.EventType == "MEETING", Ct)));
        Assert.Equal(At(900), (await h.DbAsync(db => db.FixedEvents.SingleAsync(x => x.Name == "Đi mua sắm", Ct))).Start);
    }
}
