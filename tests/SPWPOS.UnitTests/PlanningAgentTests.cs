using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PlanningAgentTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid Conversation = Guid.NewGuid();
    private static async Task<T> Agent<T>(ServiceHarness h, Func<IPlanningAgentService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IPlanningAgentService>()); }
    private static async Task<T> Work<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    private static Task Work(ServiceHarness h, Func<IWorkspaceService, Task> action) => Work(h, async s => { await action(s); return true; });
    private static async Task<ServiceHarness> Harness(AgentAiTestDouble ai)
    {
        var h = await ServiceHarness.CreateAsync(s => s.AddSingleton<ITaskInterpreter>(ai));
        h.Clock.Advance(DateTimeOffset.Parse("2026-10-14T08:00:00Z") - h.Clock.GetUtcNow());
        return h;
    }
    private static async Task Setup(ServiceHarness h, Guid uid, int maximum = 240, int daily = 480, bool automatic = true)
    {
        await Work(h, s => s.SetAvailabilityAsync(uid, new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 480, 1320)).ToArray()), Ct));
        await Work(h, s => s.SetPreferenceAsync(uid, new("UTC", 25, maximum, 0, 480, 1320, MaximumDailyMinutes: daily, AutoReschedule: automatic), Ct));
    }
    private static AgentTurnInput Request(string text = "Xếp lịch cho tôi", bool automatic = true) => new(Conversation, Guid.NewGuid(), text, automatic);
    private static AgentIntent Report(DateTimeOffset now, int minutes = 120) => AgentAiTestDouble.Empty() with
    { Tasks = [new(null, "Báo cáo SWP", now.AddHours(10), minutes, 3, "WRITING", false)] };

    [Fact] public async Task Continuous_report_is_scheduled_before_deadline_and_retry_does_not_duplicate()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var input = Request(); var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal("APPLIED", result.Status); var session = Assert.Single(result.Plan!.Sessions);
        Assert.Equal(120, (session.End - session.Start).TotalMinutes); Assert.True(session.End <= h.Clock.GetUtcNow().AddHours(10));
        var retry = await Agent(h, s => s.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal(result.Id, retry.Id); Assert.Equal(result.Plan.Id, retry.Plan!.Id); Assert.Equal(1, ai.Calls);
        Assert.Equal(1, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct))); Assert.Equal(1, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
        Assert.DoesNotContain("email", ai.Context, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain(user.Email!, ai.Context);
    }

    [Fact] public async Task Exactly_two_free_hours_are_sufficient_for_an_explicit_two_hour_request()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) with { Tasks = [new(null, "Báo cáo", now.AddHours(8), 120, 3, "WRITING", false)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await Work(h, s => s.SetAvailabilityAsync(user.Id, new([new((int)h.Clock.GetUtcNow().DayOfWeek, 840, 960)]), Ct));
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct));
        Assert.Equal("APPLIED", result.Status); Assert.Equal(120, (Assert.Single(result.Plan!.Sessions).End - result.Plan.Sessions[0].Start).TotalMinutes);
    }

    [Fact] public async Task An_explicit_two_hour_request_is_not_silently_lengthened_by_personal_estimation_bias()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            foreach (var day in Enumerable.Range(1, 3))
            {
                var at = h.Clock.GetUtcNow().AddDays(-day);
                var task = new WorkTask { UserId = user.Id, Name = "Báo cáo trước", TaskType = "WRITING", EstimatedMinutes = 60, Deadline = at.AddHours(2), Status = WorkTaskStatus.Completed, Progress = 100, CompletedAt = at.AddMinutes(90) };
                db.WorkTasks.Add(task);
                db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task.Id, Start = at, End = at.AddMinutes(90), Status = WorkSessionStatus.Completed, ActualSeconds = 5400 });
                db.EstimationObservations.Add(new() { UserId = user.Id, TaskId = task.Id, TaskType = "WRITING", EstimatedMinutes = 60, ActualMinutes = 90, ObservedAt = at.AddMinutes(90) });
            }
            await db.SaveChangesAsync(Ct);
        });
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct));
        Assert.Equal("APPLIED", result.Status); var session = Assert.Single(result.Plan!.Sessions);
        Assert.Equal(120, (session.End - session.Start).TotalMinutes); Assert.False(result.Plan.UseEstimateBias);
    }

    [Theory] [InlineData(false, 240)] [InlineData(true, 90)]
    public async Task Insufficient_hours_or_session_limit_does_not_create_partial_work(bool available, int limit)
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); if (available) await Setup(h, user.Id, limit);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct));
        Assert.Equal("BLOCKED", result.Status); Assert.NotEmpty(result.Warnings);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct))); Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
        Assert.Equal("AGENT_PREVIEW_EXPIRED", (await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.ApplyAgentTurnAsync(user.Id, result.Id, Ct)))).Code);
    }

    [Fact] public async Task Preview_apply_and_undo_are_atomic_and_preserve_history()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var preview = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(automatic: false), Ct));
        Assert.Equal("PREVIEW", preview.Status); Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.PlanProposals.CountAsync(Ct)));
        var applied = await Agent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct)); Assert.True(applied.CanUndo);
        var retry = await Agent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct)); Assert.Equal(applied.Plan!.Id, retry.Plan!.Id);
        var undone = await Agent(h, s => s.UndoAgentTurnAsync(user.Id, preview.Id, Ct)); Assert.Equal("UNDONE", undone.Status);
        Assert.Empty((await Work(h, s => s.GetAsync(user.Id, Ct))).Tasks);
        Assert.Equal(WorkSessionStatus.Cancelled, (await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct))).Status);
        Assert.Equal("UNDONE", (await h.DbAsync(db => db.PlanProposals.SingleAsync(Ct))).Status);
        Assert.Equal("UNDONE", (await Agent(h, s => s.UndoAgentTurnAsync(user.Id, preview.Id, Ct))).Status);
    }

    [Fact] public async Task Meeting_conflict_suggests_a_free_time_and_only_the_selected_alternative_is_saved_and_undoable()
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with { Events = [new("Họp", now.AddHours(6), now.AddHours(8), EventType: "MEETING")] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id); var now = h.Clock.GetUtcNow();
        var task = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Viết", "", now.AddDays(2), 60, IsHardDeadline: true), Ct));
        var other = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Giữ nguyên", "", now.AddDays(2), 60), Ct));
        var original = new WorkSession { UserId = user.Id, TaskId = task, Start = now.AddHours(6), End = now.AddHours(7) };
        var kept = new WorkSession { UserId = user.Id, TaskId = other, Start = now.AddHours(9), End = now.AddHours(10) };
        await h.DbAsync(async db => { db.WorkSessions.AddRange(original, kept); await db.SaveChangesAsync(Ct); });
        var conflict = await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct)));
        Assert.Equal("SESSION_OVERLAP", conflict.Code); Assert.Contains("Đề xuất", conflict.Message);
        ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Họp", now.AddHours(7), now.AddHours(9), EventType: "MEETING")] };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Thêm cuộc họp từ 15:00 đến 17:00 hôm nay"), Ct)); Assert.Equal("APPLIED", result.Status);
        Assert.DoesNotContain(result.Changes, x => x.Kind == "SESSION");
        var sessions = await h.DbAsync(db => db.WorkSessions.AsNoTracking().ToArrayAsync(Ct));
        Assert.Equal(WorkSessionStatus.Scheduled, sessions.Single(x => x.Id == original.Id).Status);
        Assert.Equal(kept.Start, sessions.Single(x => x.Id == kept.Id).Start);
        Assert.All(sessions.Where(x => x.Status == WorkSessionStatus.Scheduled), x => Assert.False(x.Start < now.AddHours(9) && now.AddHours(7) < x.End));
        await Agent(h, s => s.UndoAgentTurnAsync(user.Id, result.Id, Ct));
        Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
        Assert.Equal(WorkSessionStatus.Scheduled, (await h.DbAsync(db => db.WorkSessions.SingleAsync(x => x.Id == original.Id, Ct))).Status);
    }

    [Fact] public async Task Weekly_and_daily_availability_apply_together_and_undo_restores_original_week()
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with { ReplaceWeeklyAvailability = true,
            WeeklySlots = Enumerable.Range(1, 5).Select(day => new AvailabilityInput(day, 1140, 1320)).ToArray(),
            Exceptions = [new(DateOnly.FromDateTime(now.DateTime).AddDays(1), 1200, 1260, false, "Bận")]} };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct)); Assert.Equal("APPLIED", result.Status);
        var changed = await Work(h, s => s.GetAsync(user.Id, Ct)); Assert.Equal(5, changed.Availability.Length); Assert.Single(changed.Exceptions);
        await Agent(h, s => s.UndoAgentTurnAsync(user.Id, result.Id, Ct));
        var restored = await Work(h, s => s.GetAsync(user.Id, Ct)); Assert.Equal(7, restored.Availability.Length); Assert.Empty(restored.Exceptions);
    }

    [Fact] public async Task Locked_or_fixed_event_conflict_rolls_back_every_action()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) with { Events = [new("Họp", now.AddHours(6), now.AddHours(8))] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id); var now = h.Clock.GetUtcNow();
        var task = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Khóa", "", now.AddDays(1), 60), Ct));
        await h.DbAsync(async db => { db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task, Start = now.AddHours(6), End = now.AddHours(7), IsLocked = true }); await db.SaveChangesAsync(Ct); });
        var error = await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct)));
        Assert.Equal("LOCKED_SESSION_CONFLICT", error.Code); Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct))); Assert.Equal(1, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Preview_is_rejected_after_revision_changes_and_undo_after_execution()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var preview = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(automatic: false), Ct));
        await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Khác", "", h.Clock.GetUtcNow().AddDays(2), 60), Ct));
        Assert.Equal("AGENT_STALE", (await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct)))).Code);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct));
        await Work(h, s => s.ExecuteSessionAsync(user.Id, Assert.Single(result.Plan!.Sessions, x => !x.Preserved).Id, "start", Ct));
        Assert.Equal("AGENT_UNDO_STALE", (await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.UndoAgentTurnAsync(user.Id, result.Id, Ct)))).Code);
    }

    [Fact] public async Task Changes_during_ai_call_are_detected_without_holding_an_account_transaction()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        ai.BeforeReply = () => Work(h, s => s.SaveExceptionAsync(user.Id, null, new(DateOnly.FromDateTime(h.Clock.GetUtcNow().DateTime).AddDays(1), 600, 660, false), Ct));
        Assert.Equal("AGENT_STALE", (await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct)))).Code);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Clarification_is_saved_and_followup_receives_history()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Question = "6h sáng hay 18h?" } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync();
        Assert.Equal("NEEDS_INPUT", (await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Deadline 6h"), Ct))).Status);
        ai.Response = _ => AgentAiTestDouble.Empty();
        Assert.Equal("ANSWERED", (await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("18h"), Ct))).Status);
        Assert.Contains(ai.History, x => x.Text == "Deadline 6h" && x.Reply.Contains("6h sáng"));
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task A_foreign_task_or_turn_cannot_be_read_applied_or_modified()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { ScheduleTaskIds = [Guid.NewGuid()] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct)))).Code);
        ai.Response = _ => AgentAiTestDouble.Empty();
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct));
        Assert.Empty(await Agent(h, s => s.GetAgentTurnsAsync(Guid.NewGuid(), Ct)));
        // A second valid account is needed to reach the owner-scoped lookup.
        var other = new ApplicationUser { Id = Guid.NewGuid(), UserName = "other", Email = "other@example.com", NormalizedEmail = "OTHER@EXAMPLE.COM", IsActive = true, EmailConfirmed = true };
        await h.DbAsync(async db => { db.Users.Add(other); await db.SaveChangesAsync(Ct); });
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.ApplyAgentTurnAsync(other.Id, turn.Id, Ct)))).Code);
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.UndoAgentTurnAsync(other.Id, turn.Id, Ct)))).Code);
    }

    [Fact] public async Task Time_forms_reject_conflicts_and_preserve_sessions_when_the_user_selects_a_free_alternative()
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow(); var id = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Viết", "", now.AddDays(2), 60), Ct));
        var old = new WorkSession { UserId = user.Id, TaskId = id, Start = now.AddHours(6), End = now.AddHours(7) };
        await h.DbAsync(async db => { db.WorkSessions.Add(old); await db.SaveChangesAsync(Ct); });
        var conflict = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.SaveEventAsync(user.Id, null, new("Họp", now.AddHours(6), now.AddHours(8)), Ct)));
        Assert.Equal("SESSION_OVERLAP", conflict.Code); Assert.Contains("Đề xuất", conflict.Message);
        Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
        await Work(h, s => s.SaveEventAsync(user.Id, null, new("Họp", now.AddHours(7), now.AddHours(9)), Ct));
        var sessions = await h.DbAsync(db => db.WorkSessions.AsNoTracking().ToArrayAsync(Ct));
        Assert.Equal(WorkSessionStatus.Scheduled, sessions.Single(x => x.Id == old.Id).Status);
        var replacement = Assert.Single(sessions, x => x.Status == WorkSessionStatus.Scheduled);
        Assert.Equal(old.Start, replacement.Start); Assert.Equal(old.End, replacement.End);
        Assert.Equal(0, ai.Calls);
    }

    [Fact] public async Task A_time_change_proposes_urgent_displacement_without_automatically_moving_the_old_sessions()
    {
        await using var h = await Harness(new AgentAiTestDouble()); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        var urgent = new WorkTask { UserId = user.Id, Name = "Khẩn", Priority = 4, EstimatedMinutes = 60,
            Deadline = now.AddHours(4), IsHardDeadline = true, WorkWindowStart = now.AddHours(2), WorkWindowEnd = now.AddHours(4) };
        var old = new WorkTask { UserId = user.Id, Name = "Việc cũ", Deadline = now.AddDays(7), EstimatedMinutes = 60 };
        var first = new WorkSession { UserId = user.Id, TaskId = urgent.Id, Start = now.AddHours(2), End = now.AddHours(3) };
        var second = new WorkSession { UserId = user.Id, TaskId = old.Id, Start = now.AddHours(3), End = now.AddHours(4) };
        await h.DbAsync(async db => { db.WorkTasks.AddRange(urgent, old); db.WorkSessions.AddRange(first, second); await db.SaveChangesAsync(Ct); });
        await Work(h, s => s.SaveExceptionAsync(user.Id, null, new(DateOnly.FromDateTime(now.DateTime), 600, 660, false), Ct));
        var saved = await h.DbAsync(db => db.WorkSessions.Where(x => x.Status == WorkSessionStatus.Scheduled).ToArrayAsync(Ct));
        Assert.Equal(2, saved.Length); Assert.Contains(saved, x => x.Id == first.Id && x.Start == first.Start);
        Assert.Contains(saved, x => x.Id == second.Id && x.Start == second.Start);
        var proposal = Assert.Single(await h.DbAsync(db => db.PlanProposals.ToArrayAsync(Ct)));
        Assert.False(proposal.IsApplied);
        var plan = await Work(h, s => s.GetPlanAsync(user.Id, proposal.Id, Ct));
        Assert.True(plan.HasUrgentReplan); Assert.Empty(plan.Warnings);
        await Work(h, s => s.ApplyPlanAsync(user.Id, proposal.Id, Ct));
        Assert.True((await Work(h, s => s.GetPlanAsync(user.Id, proposal.Id, Ct))).IsApplied);
    }

    [Fact] public async Task Recurring_events_check_all_occurrences_before_saving_any()
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        await Work(h, s => s.SaveEventAsync(user.Id, null, new("Hẹn trước", now.AddDays(14).AddHours(6), now.AddDays(14).AddHours(7)), Ct));
        Assert.Equal("FIXED_EVENT_OVERLAP", (await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.SaveEventAsync(user.Id, null, new("Họp tuần", now.AddHours(6), now.AddHours(7), RepeatWeeks: 3), Ct)))).Code);
        Assert.Equal(1, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
        await Work(h, s => s.SaveEventAsync(user.Id, null, new("Học tuần", now.AddHours(9), now.AddHours(10), RepeatWeeks: 3), Ct));
        Assert.Equal(4, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
    }

    [Fact] public async Task Daily_budget_includes_completed_and_preserved_work()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now, 120) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id, daily: 120);
        var now = h.Clock.GetUtcNow(); var id = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Đã làm", "", now.AddDays(2), 60), Ct));
        await h.DbAsync(async db => { db.WorkSessions.Add(new() { UserId = user.Id, TaskId = id, Start = now.AddHours(-2), End = now.AddHours(-1), Status = WorkSessionStatus.Completed }); await db.SaveChangesAsync(Ct); });
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct)); Assert.Equal("BLOCKED", result.Status);
        Assert.Equal(1, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Editing_a_busy_exception_and_meeting_updates_in_place_and_undo_restores_both()
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow(); var date = DateOnly.FromDateTime(now.DateTime).AddDays(1);
        var exceptionId = await Work(h, s => s.SaveExceptionAsync(user.Id, null, new(date, 1140, 1260, false, "Bận cũ"), Ct));
        var eventId = await Work(h, s => s.SaveEventAsync(user.Id, null, new("Họp cũ", now.AddDays(1).AddHours(6), now.AddDays(1).AddHours(7)), Ct));
        ai.Response = _ => AgentAiTestDouble.Empty() with {
            Exceptions = [new(date, 1140, 1260, true, "Rảnh lại", exceptionId)],
            Events = [new("Họp mới", now.AddDays(1).AddHours(8), now.AddDays(1).AddHours(9), ExistingId: eventId)]
        };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct)); Assert.Equal("APPLIED", result.Status);
        var workspace = await Work(h, s => s.GetAsync(user.Id, Ct)); Assert.True(Assert.Single(workspace.Exceptions).IsAvailable);
        Assert.Equal("Họp mới", Assert.Single(workspace.Events).Name); Assert.Equal(eventId, workspace.Events[0].Id);
        await Agent(h, s => s.UndoAgentTurnAsync(user.Id, result.Id, Ct));
        workspace = await Work(h, s => s.GetAsync(user.Id, Ct)); Assert.False(Assert.Single(workspace.Exceptions).IsAvailable);
        Assert.Equal("Họp cũ", Assert.Single(workspace.Events).Name); Assert.Equal(exceptionId, workspace.Exceptions[0].Id);
    }

    [Fact] public async Task A_fixed_event_collision_does_not_silently_move_either_meeting()
    {
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with { Events = [new("Họp mới", now.AddHours(6), now.AddHours(8))] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id); var now = h.Clock.GetUtcNow();
        await Work(h, s => s.SaveEventAsync(user.Id, null, new("Họp cũ", now.AddHours(7), now.AddHours(9)), Ct));
        Assert.Equal("FIXED_EVENT_OVERLAP", (await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct)))).Code);
        Assert.Equal("Họp cũ", (await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct))).Name);
    }

    [Fact] public async Task Disabling_automatic_rescheduling_still_rejects_a_conflicting_event_with_alternatives()
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id, automatic: false);
        var now = h.Clock.GetUtcNow(); var task = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Viết", "", now.AddDays(2), 60), Ct));
        await h.DbAsync(async db => { db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task, Start = now.AddHours(6), End = now.AddHours(7) }); await db.SaveChangesAsync(Ct); });
        var conflict = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.SaveEventAsync(user.Id, null, new("Họp", now.AddHours(6), now.AddHours(8)), Ct)));
        Assert.Equal("SESSION_OVERLAP", conflict.Code); Assert.Contains("Đề xuất", conflict.Message);
        Assert.Equal(WorkSessionStatus.Scheduled, (await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct))).Status);
        Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
    }
}
