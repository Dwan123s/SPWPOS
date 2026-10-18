using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed class NoraQualityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Agent<T>(ServiceHarness h, Func<IPlanningAgentService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IPlanningAgentService>()); }
    private static async Task<T> Work<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    private static Task Work(ServiceHarness h, Func<IWorkspaceService, Task> action) => Work(h, async s => { await action(s); return true; });
    private static async Task<ServiceHarness> Harness(AgentAiTestDouble ai)
    {
        var h = await ServiceHarness.CreateAsync(s => s.AddSingleton<ITaskInterpreter>(ai));
        h.Clock.Advance(DateTimeOffset.Parse("2026-10-19T01:00:00Z") - h.Clock.GetUtcNow());
        return h;
    }
    private static Task<AgentTurnView> Send(ServiceHarness h, Guid uid, Guid conversation, string text, bool apply = true) =>
        Agent(h, s => s.SendAgentTurnAsync(uid, new(conversation, Guid.NewGuid(), text, apply), Ct));
    private static async Task Setup(ServiceHarness h, Guid uid)
    {
        await Work(h, s => s.SetAvailabilityAsync(uid, new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 1140, 1320)).ToArray()), Ct));
        await Work(h, s => s.SetPreferenceAsync(uid, new("Asia/Ho_Chi_Minh", 25, 240, 0, 1140, 1320, MaximumDailyMinutes: 480), Ct));
    }
    private static DateTimeOffset At(int day, int hour) => DateTimeOffset.Parse($"2026-10-{day:00}T{hour:00}:00:00+07:00");

    [Fact] public async Task Mixed_clarification_recovers_to_a_question_with_no_mutations_in_either_apply_mode()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Question = "Buổi slide đã được lưu chưa?", Reschedule = true,
            Tasks = [new(null, "Bài tập", At(22, 22), 60, 2, "STUDY", true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var revision = (await Work(h, s => s.GetAsync(user.Id, Ct))).Preference.Revision;
        foreach (var automatic in new[] { false, true })
        {
            var result = await Send(h, user.Id, Guid.NewGuid(), "Xếp bài tập, đừng trùng slide", automatic);
            Assert.Equal("NEEDS_INPUT", result.Status); Assert.Empty(result.Changes); Assert.Null(result.Plan);
        }
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        Assert.Equal(revision, (await Work(h, s => s.GetAsync(user.Id, Ct))).Preference.Revision);
    }

    [Fact] public async Task Invented_duration_is_rejected_and_followup_keeps_Thursday_and_changes_deadline_to_21()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Ôn tiếng Anh", At(22, 22), 180, 2, "STUDY", true,
            DurationEvidence: "", DeadlineEvidence: "", WorkWindowEvidence: "Tối thứ Năm 22/10")] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id); var conversation = Guid.NewGuid();
        var first = await Send(h, user.Id, conversation, "Tối thứ Năm 22/10 mình muốn ôn tiếng Anh. Xếp giúp mình nhé.");
        Assert.Equal("NEEDS_INPUT", first.Status); Assert.Contains("bao nhiêu phút", first.Question);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Ôn tiếng Anh", null, 45, 2, "STUDY", true,
            DurationEvidence: "45 phút", DeadlineEvidence: "", WorkWindowEvidence: "Tối thứ Năm 22/10")] };
        var applied = await Send(h, user.Id, conversation, "Khoảng 45 phút thôi, mình muốn học xong trước 21h.");
        Assert.Equal("APPLIED", applied.Status);
        using var context = JsonDocument.Parse(ai.Context); Assert.Equal("Tối thứ Năm 22/10 mình muốn ôn tiếng Anh. Xếp giúp mình nhé.", context.RootElement.GetProperty("pendingRequest").GetProperty("originalText").GetString());
        var task = Assert.Single(await h.DbAsync(db => db.WorkTasks.AsNoTracking().ToArrayAsync(Ct)));
        Assert.Equal(45, task.EstimatedMinutes); Assert.Equal(At(22, 21), task.Deadline);
        Assert.Equal(At(22, 18), task.WorkWindowStart); Assert.Equal(At(23, 0), task.WorkWindowEnd);
        var session = Assert.Single(applied.Plan!.Sessions); Assert.Equal(At(22, 19), session.Start); Assert.Equal(session.Start.AddMinutes(45), session.End);
        Assert.Contains("22/10/2026 19:00", applied.Reply);
        var replan = await Work(h, s => s.GeneratePlanAsync(user.Id, new(h.Clock.GetUtcNow().AddMinutes(5), 7, UseCapacityFactor: false, UseEstimateBias: false), Ct));
        Assert.All(replan.Sessions, x => Assert.True(x.Start >= At(22, 18) && x.End <= At(22, 21)));
        var edit = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.EditSessionAsync(user.Id, session.Id, new(At(20, 19), At(20, 19).AddMinutes(45)), Ct)));
        Assert.Equal("TASK_WORK_WINDOW", edit.Code);
    }

    [Fact] public async Task Followup_updates_existing_task_duration_and_deadline_without_losing_work_window()
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var id = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Ôn tiếng Anh", "", At(22, 22), 180, TaskType: "STUDY"), Ct));
        await h.DbAsync(async db => { var task = await db.WorkTasks.SingleAsync(Ct); task.WorkWindowStart = At(22, 18); task.WorkWindowEnd = At(23, 0); await db.SaveChangesAsync(Ct); });
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(id, "Ôn tiếng Anh", null, 45, null, "", null, DurationEvidence: "45 phút", DeadlineEvidence: "", WorkWindowEvidence: "")] };
        var result = await Send(h, user.Id, Guid.NewGuid(), "Khoảng 45 phút thôi, mình muốn học xong trước 21h.");
        Assert.Equal("APPLIED", result.Status); var task = await h.DbAsync(db => db.WorkTasks.AsNoTracking().SingleAsync(Ct));
        Assert.Equal(At(22, 21), task.Deadline); Assert.Equal(At(22, 18), task.WorkWindowStart);
        Assert.Equal(At(22, 19), Assert.Single(result.Plan!.Sessions).Start);
    }

    [Theory] [InlineData("6", 18)] [InlineData("sáu", 18)] [InlineData("8", 20)]
    public async Task Ambiguous_event_time_never_writes_then_clarification_creates_one_event(string hour, int clarified)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Hẹn với thầy", At(21, clarified), At(21, clarified).AddMinutes(30), EventType: "APPOINTMENT")] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); var conversation = Guid.NewGuid();
        var first = await Send(h, user.Id, conversation, $"Thứ Tư 21/10 mình có hẹn với thầy lúc {hour} giờ, kéo dài nửa tiếng.");
        Assert.Equal("NEEDS_INPUT", first.Status); Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        var result = await Send(h, user.Id, conversation, $"À, {clarified}h chiều nhé.");
        Assert.Equal("APPLIED", result.Status); Assert.Single(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        Assert.False(first.CanUndo);
    }

    [Fact] public async Task No_change_event_and_task_requests_keep_revision_sessions_and_undo_unchanged()
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var eventId = await Work(h, s => s.SaveEventAsync(user.Id, null, new("Hẹn", At(21, 18), At(21, 18).AddMinutes(30), EventType: "APPOINTMENT"), Ct));
        ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Hẹn", At(21, 18), At(21, 18).AddMinutes(30), EventType: "APPOINTMENT", ExistingId: eventId)] };
        var before = await Work(h, s => s.GetAsync(user.Id, Ct));
        var result = await Send(h, user.Id, Guid.NewGuid(), "À, 6 giờ chiều nhé.");
        Assert.Equal("ANSWERED", result.Status); Assert.Empty(result.Changes); Assert.False(result.CanUndo);
        Assert.Equal(before.Preference.Revision, (await Work(h, s => s.GetAsync(user.Id, Ct))).Preference.Revision);
        Assert.Single(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        var taskId = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Slide", "", At(21, 22), 90, TaskType: "WRITING", IsHardDeadline: true), Ct));
        ai.Response = _ => AgentAiTestDouble.Empty() with { ScheduleTaskIds = [taskId] };
        var scheduled = await Send(h, user.Id, Guid.NewGuid(), "Xếp lịch Slide"); Assert.Equal("APPLIED", scheduled.Status);
        before = await Work(h, s => s.GetAsync(user.Id, Ct));
        result = await Send(h, user.Id, Guid.NewGuid(), "Xếp lại lịch Slide");
        Assert.Equal("ANSWERED", result.Status); Assert.Null(result.Plan); Assert.False(result.CanUndo);
        var after = await Work(h, s => s.GetAsync(user.Id, Ct)); Assert.Equal(before.Preference.Revision, after.Preference.Revision);
        Assert.Equal(before.Sessions.Select(x => x.Id), after.Sessions.Select(x => x.Id));
    }

    [Fact] public async Task Failed_requests_are_visible_and_missing_work_cannot_be_declared_deadline_safe()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Làm slide", At(21, 22), 90, 2, "DESIGN", true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id); var conversation = Guid.NewGuid();
        Assert.Equal("INVALID_TASK_TYPE", (await Assert.ThrowsAsync<AuthException>(() => Send(h, user.Id, conversation, "Slide 90 phút, xong trước 22h ngày 21/10"))).Code);
        ai.Response = _ => AgentAiTestDouble.Empty() with { Reply = "Tất cả deadline đều an toàn." };
        var result = await Send(h, user.Id, conversation, "Làm slide còn kịp deadline không?");
        Assert.Equal("ANSWERED", result.Status); Assert.Contains("chưa thể xác nhận", result.Reply);
        Assert.Contains(ai.History, x => x.Status == "FAILED");
        using var context = JsonDocument.Parse(ai.Context); Assert.DoesNotContain(context.RootElement.GetProperty("activeTaskTypes").EnumerateArray(), x => x.GetString() == "DESIGN");
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Exclusive_Friday_availability_overrides_recurring_hours_and_continuous_two_hours_is_blocked()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Exceptions = [new(new(2026, 10, 23), 1200, 1260, true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        Assert.Equal("APPLIED", (await Send(h, user.Id, Guid.NewGuid(), "Riêng thứ Sáu 23/10 mình chỉ rảnh từ 20h đến 21h, ngoài khoảng đó bận hết.")).Status);
        var exception = await h.DbAsync(db => db.AvailabilityExceptions.AsNoTracking().SingleAsync(Ct)); Assert.True(exception.OverridesWeeklyAvailability);
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Thi thử", At(23, 22), 120, 2, "STUDY", false, At(23, 18), At(24, 0))] };
        var result = await Send(h, user.Id, Guid.NewGuid(), "Thi thử 2 tiếng liên tục vào tối thứ Sáu 23/10, xong trước 22h.");
        Assert.Equal("BLOCKED", result.Status); Assert.False(result.CanUndo); Assert.NotEmpty(result.Warnings);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Disabling_a_task_type_after_preview_blocks_apply_atomically()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Slide", At(21, 22), 90, 2, "WRITING", true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var preview = await Send(h, user.Id, Guid.NewGuid(), "Làm slide 90 phút", false); Assert.Equal("PREVIEW", preview.Status);
        await h.DbAsync(async db => { var type = await db.TaskTypes.SingleAsync(x => x.Code == "WRITING", Ct); type.IsActive = false; await db.SaveChangesAsync(Ct); });
        Assert.Equal("INVALID_TASK_TYPE", (await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct)))).Code);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }
    [Fact] public async Task Multiple_clarification_answers_are_retained_as_bounded_structured_context()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Question = "Bạn dành bao nhiêu phút?" } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id); var conversation = Guid.NewGuid();
        await Send(h, user.Id, conversation, "Làm slide cho tôi");
        ai.Response = _ => AgentAiTestDouble.Empty() with { Question = "Deadline vào ngày giờ nào?" };
        await Send(h, user.Id, conversation, "90 phút");
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Làm slide", At(21, 22), 90, 2, "WRITING", true,
            DurationEvidence: "90 phút", DeadlineEvidence: "xong trước 22h ngày 21/10", WorkWindowEvidence: "")] };
        var result = await Send(h, user.Id, conversation, "xong trước 22h ngày 21/10");
        Assert.Equal("APPLIED", result.Status);
        using var context = JsonDocument.Parse(ai.Context); var pending = context.RootElement.GetProperty("pendingRequest");
        Assert.Contains(pending.GetProperty("answers").EnumerateArray(), x => x.GetString() == "90 phút");
        Assert.Equal(90, pending.GetProperty("estimatedMinutes").GetInt32());
        Assert.Equal(90, Assert.Single(await h.DbAsync(db => db.WorkTasks.AsNoTracking().ToArrayAsync(Ct))).EstimatedMinutes);
    }

    [Fact] public async Task Replacing_exclusive_date_hours_removes_old_windows_and_undo_restores_them()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Exceptions = [new(new(2026, 10, 23), 1200, 1260, true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id); var conversation = Guid.NewGuid();
        var first = await Send(h, user.Id, conversation, "23/10 chỉ rảnh 20h–21h"); Assert.Equal("APPLIED", first.Status);
        var old = Assert.Single(await h.DbAsync(db => db.AvailabilityExceptions.AsNoTracking().ToArrayAsync(Ct)));
        ai.Response = _ => AgentAiTestDouble.Empty() with { Exceptions = [new(new(2026, 10, 23), 1260, 1320, true)] };
        var second = await Send(h, user.Id, conversation, "23/10 chỉ rảnh 21h–22h"); Assert.Equal("APPLIED", second.Status);
        var updated = Assert.Single(await h.DbAsync(db => db.AvailabilityExceptions.AsNoTracking().ToArrayAsync(Ct)));
        Assert.Equal(1260, updated.StartMinute); Assert.True(updated.OverridesWeeklyAvailability);
        await Agent(h, s => s.UndoAgentTurnAsync(user.Id, second.Id, Ct));
        var restored = Assert.Single(await h.DbAsync(db => db.AvailabilityExceptions.AsNoTracking().ToArrayAsync(Ct)));
        Assert.Equal(old.Id, restored.Id); Assert.Equal(old.StartMinute, restored.StartMinute); Assert.True(restored.OverridesWeeklyAvailability);
    }
}
