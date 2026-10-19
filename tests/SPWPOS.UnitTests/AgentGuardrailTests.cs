using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PlanningAgentTests
{
    // NORA quality report T03: a question must never apply any accompanying actions.
    [Fact] public async Task Mixed_clarification_becomes_a_question_without_saving_any_actions()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) with {
            Reply = "Đã lưu công việc.", Question = "Bạn muốn học lúc nào?", Options = ["Buổi sáng", "Buổi tối"],
            ReplaceWeeklyAvailability = true, WeeklySlots = [new(1, 1140, 1320)],
            Exceptions = [new(DateOnly.FromDateTime(now.DateTime).AddDays(1), 600, 660, false)],
            Events = [new("Họp", now.AddHours(2), now.AddHours(3))],
            ScheduleTaskIds = [Guid.NewGuid()], Reschedule = true
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var revision = await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct));

        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct));

        Assert.Equal("NEEDS_INPUT", result.Status); Assert.Equal("Bạn muốn học lúc nào?", result.Question);
        Assert.Equal(new[] { "Buổi sáng", "Buổi tối" }, result.Options);
        Assert.DoesNotContain("Đã lưu", result.Reply); Assert.Empty(result.Changes); Assert.Null(result.Plan);
        Assert.False(result.CanUndo);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.AvailabilityExceptions.CountAsync(Ct)));
        Assert.Equal(7, await h.DbAsync(db => db.WeeklyAvailability.CountAsync(Ct)));
        Assert.Equal(revision, await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct)));
        var stored = await h.DbAsync(db => db.AgentTurns.SingleAsync(x => x.Id == result.Id, Ct));
        var intent = JsonSerializer.Deserialize<AgentIntent>(stored.CommandJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Empty(intent.Tasks); Assert.Empty(intent.Events); Assert.Empty(intent.Exceptions);
        Assert.Empty(intent.WeeklySlots); Assert.Empty(intent.ScheduleTaskIds);
        Assert.False(intent.ReplaceWeeklyAvailability); Assert.False(intent.Reschedule);
    }

    // T09: incomplete new tasks must ask for details, even alongside a valid event.
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Missing_new_task_details_require_clarification_without_partial_writes(bool missingDuration)
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) with {
            Tasks = [new(null, "Ôn tiếng Anh", missingDuration ? now.AddDays(1) : null,
                missingDuration ? null : 45, 2, "STUDY", true)],
            Events = [new("Họp", now.AddHours(2), now.AddHours(3))]
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);

        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct));

        Assert.Equal("NEEDS_INPUT", result.Status); Assert.Contains("Ôn tiếng Anh", result.Question);
        Assert.Contains(missingDuration ? "thời lượng" : "hạn hoàn thành", result.Question);
        Assert.Empty(result.Changes); Assert.Null(result.Plan);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
    }

    // T12: repeating existing values must not change revisions or replace sessions.
    [Theory] [InlineData("weekly")] [InlineData("exception")] [InlineData("event")] [InlineData("task")]
    public async Task Unchanged_edits_do_not_save_or_offer_undo(string kind)
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow(); var date = DateOnly.FromDateTime(now.DateTime).AddDays(1);
        var exception = await Work(h, s => s.SaveExceptionAsync(user.Id, null, new(date, 1140, 1200, false), Ct));
        var meeting = await Work(h, s => s.SaveEventAsync(user.Id, null, new("Họp", now.AddDays(1).AddHours(2), now.AddDays(1).AddHours(3)), Ct));
        var task = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Viết", "", now.AddDays(2), 60, IsHardDeadline: true), Ct));
        var session = new WorkSession { UserId = user.Id, TaskId = task, Start = now.AddHours(1), End = now.AddHours(2) };
        await h.DbAsync(async db => { db.WorkSessions.Add(session); await db.SaveChangesAsync(Ct); });
        var original = await h.DbAsync(db => db.WorkTasks.AsNoTracking().SingleAsync(Ct));
        var revision = await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct));
        ai.Response = _ => kind switch {
            "weekly" => AgentAiTestDouble.Empty() with { ReplaceWeeklyAvailability = true,
                WeeklySlots = Enumerable.Range(0, 7).Reverse().Select(day => new AvailabilityInput(day, 480, 1320)).ToArray() },
            "exception" => AgentAiTestDouble.Empty() with { Exceptions = [new(date, 1140, 1200, false, ExistingId: exception)] },
            "event" => AgentAiTestDouble.Empty() with { Events = [new("Họp", now.AddDays(1).AddHours(2), now.AddDays(1).AddHours(3), ExistingId: meeting)] },
            _ => AgentAiTestDouble.Empty() with { Tasks = [new(task, "Viết", now.AddDays(2), 60, null, "", null)] }
        };

        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct));

        Assert.Equal("ANSWERED", result.Status); Assert.Empty(result.Changes); Assert.Null(result.Plan); Assert.False(result.CanUndo);
        Assert.Equal(revision, await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.PlanProposals.CountAsync(Ct)));
        var kept = await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct));
        Assert.Equal(session.Id, kept.Id); Assert.Equal(WorkSessionStatus.Scheduled, kept.Status);
        Assert.Equal(original.UpdatedAt, await h.DbAsync(db => db.WorkTasks.Select(x => x.UpdatedAt).SingleAsync(Ct)));
    }

    [Fact] public async Task Read_only_question_does_not_attempt_to_fix_an_existing_calendar_conflict()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        var task = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Khóa", "", now.AddDays(2), 60), Ct));
        await h.DbAsync(async db => {
            db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task, Start = now.AddHours(2), End = now.AddHours(3), IsLocked = true });
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Trùng giờ", Start = now.AddHours(2), End = now.AddHours(3) });
            await db.SaveChangesAsync(Ct);
        });

        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Lịch hiện tại thế nào?"), Ct));

        Assert.Equal("ANSWERED", result.Status); Assert.Null(result.Plan); Assert.Empty(result.Changes);
        Assert.Equal(1, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.PlanProposals.CountAsync(Ct)));
    }

    [Theory] [InlineData("exception")] [InlineData("event")] [InlineData("task")]
    public async Task Repeated_edits_of_one_entity_are_rejected_even_when_the_first_edit_changes_nothing(string kind)
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow(); var date = DateOnly.FromDateTime(now.DateTime).AddDays(1);
        var exception = await Work(h, s => s.SaveExceptionAsync(user.Id, null, new(date, 1140, 1200, false), Ct));
        var meeting = await Work(h, s => s.SaveEventAsync(user.Id, null, new("Họp", now.AddDays(1).AddHours(2), now.AddDays(1).AddHours(3)), Ct));
        var task = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Viết", "", now.AddDays(2), 60, IsHardDeadline: true), Ct));
        ai.Response = _ => kind switch {
            "exception" => AgentAiTestDouble.Empty() with { Exceptions = [new(date, 1140, 1200, false, ExistingId: exception), new(date, 1200, 1260, false, ExistingId: exception)] },
            "event" => AgentAiTestDouble.Empty() with { Events = [new("Họp", now.AddDays(1).AddHours(2), now.AddDays(1).AddHours(3), ExistingId: meeting), new("Họp", now.AddDays(1).AddHours(4), now.AddDays(1).AddHours(5), ExistingId: meeting)] },
            _ => AgentAiTestDouble.Empty() with { Tasks = [new(task, "Viết", null, 60, null, "", null), new(task, "Viết", null, 90, null, "", null)] }
        };
        var revision = await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct));

        var error = await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct)));

        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.Equal(revision, await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct)));
        Assert.Equal(60, await h.DbAsync(db => db.WorkTasks.Select(x => x.EstimatedMinutes).SingleAsync(Ct)));
        Assert.Equal(1140, await h.DbAsync(db => db.AvailabilityExceptions.Select(x => x.StartMinute).SingleAsync(Ct)));
        Assert.Equal(now.AddDays(1).AddHours(2), await h.DbAsync(db => db.FixedEvents.Select(x => x.Start).SingleAsync(Ct)));
    }

    // T06: later answers must know a prior task request failed, without raw error leakage.
    [Fact] public async Task Failed_requests_remain_in_followup_context_as_unsaved_work()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) with { Tasks = [new(null, "Slide", now.AddDays(-1), 90, 2, "WRITING", true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Tạo slide 90 phút"), Ct)));
        ai.Response = _ => AgentAiTestDouble.Empty();

        await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Slide đã được xếp chưa?"), Ct));

        var failure = Assert.Single(ai.History);
        Assert.Equal("FAILED", failure.Status); Assert.Equal("Tạo slide 90 phút", failure.Text);
        Assert.Contains("chưa lưu", failure.Reply);
        Assert.DoesNotContain("Loại công việc", failure.Reply);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Context_exposes_active_database_task_types_including_custom_codes()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync();
        await h.DbAsync(async db => {
            (await db.TaskTypes.SingleAsync(x => x.Code == "ADMIN", Ct)).IsActive = false;
            db.TaskTypes.Add(new() { Code = "CUSTOM", DisplayName = "Custom work" });
            await db.SaveChangesAsync(Ct);
        });

        await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), Ct));

        using var context = JsonDocument.Parse(ai.Context);
        var codes = context.RootElement.GetProperty("activeTaskTypes").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("CUSTOM", codes); Assert.Contains("STUDY", codes); Assert.DoesNotContain("ADMIN", codes); Assert.DoesNotContain("DESIGN", codes);
    }

    [Fact] public async Task Task_category_disabled_after_preview_prevents_apply_without_partial_writes()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var preview = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(automatic: false), Ct));
        await h.DbAsync(async db => { (await db.TaskTypes.SingleAsync(x => x.Code == "WRITING", Ct)).IsActive = false; await db.SaveChangesAsync(Ct); });

        var error = await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct)));

        Assert.Equal("INVALID_TASK_TYPE", error.Code);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
    }
}
