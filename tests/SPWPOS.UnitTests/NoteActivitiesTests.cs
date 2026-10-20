using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class NoteActivitiesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Interpreter : ITaskInterpreter
    {
        public NoteAgentCapabilities Capabilities => new(true, Provider: "Gemini");
        public Task<NoteAnalysis> AnalyzeNoteAsync(string text, bool useAi, string zone, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(new NoteAnalysis([], "Gemini", [], false, Activities(now)));
        public Task<TaskSuggestion> CaptureAsync(CaptureInput input, string zone, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<BreakdownView> BreakdownAsync(TaskInput task, bool useAi, CancellationToken ct) => throw new NotSupportedException();
    }
    private static NoteActivity[] Activities(DateTimeOffset now)
    {
        var day = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        return [new("EVENT", "Đi học", Type: "CLASS", Start: day.AddHours(0), End: day.AddHours(5)),
            new("EVENT", "Đi làm", Type: "WORK", Start: day.AddHours(6), End: day.AddHours(10)),
            new("EVENT", "Họp đồng nghiệp", Type: "MEETING", Start: day.AddHours(10), End: day.AddHours(10).AddMinutes(15)),
            new("TASK", "Báo cáo RDS", Type: "WRITING", Start: day.AddHours(13).AddMinutes(30), EstimatedMinutes: 60)];
    }
    private static async Task<T> Call<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    private static async Task<(ServiceHarness Harness, Guid User, NoteView Note)> Setup()
    {
        var h = await ServiceHarness.CreateAsync(services => services.AddSingleton<ITaskInterpreter>(new Interpreter()));
        var user = await h.RegisterAsync();
        await Call(h, async s => { await s.SetAvailabilityAsync(user.Id, new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 0, 1440)).ToArray()), Ct); return true; });
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Thời gian biểu", "Đi học, đi làm, họp và làm báo cáo RDS"), Ct));
        note = await Call(h, s => s.AnalyzeNoteAsync(user.Id, note.Id, new(note.Version), Ct));
        return (h, user.Id, note);
    }
    [Fact] public async Task Mixed_note_preview_is_read_only_and_confirm_saves_all_four_activities_once()
    {
        var (h, uid, note) = await Setup(); await using var lifetime = h;
        var activities = Activities(h.Clock.GetUtcNow());
        var plan = await Call(h, s => s.PreviewNoteActivitiesAsync(uid, note.Id, new(note.Version, activities), Ct));
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        var session = Assert.Single(plan.Sessions, x => !x.Preserved);
        Assert.Equal(activities[3].Start, session.Start);
        Assert.Equal(activities[3].Start!.Value.AddHours(1), session.End);
        var saved = await Call(h, s => s.ApplyNoteActivitiesAsync(uid, note.Id, plan.Id, new(note.Version), Ct));
        Assert.True(saved.Plan.IsApplied);
        Assert.Equal(3, (await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct))).Length);
        Assert.Single(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Single(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        await Call(h, s => s.ApplyNoteActivitiesAsync(uid, note.Id, plan.Id, new(note.Version), Ct));
        Assert.Equal(3, (await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct))).Length);
        Assert.Single(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }
    [Fact] public async Task Missing_report_duration_never_becomes_an_invented_hour_or_partial_calendar()
    {
        var (h, uid, note) = await Setup(); await using var lifetime = h;
        var activities = Activities(h.Clock.GetUtcNow()); activities[3] = activities[3] with { EstimatedMinutes = null };
        Assert.Equal("NOTE_ACTIVITY_INCOMPLETE", (await Assert.ThrowsAsync<AuthException>(() => Call(h,
            s => s.PreviewNoteActivitiesAsync(uid, note.Id, new(note.Version, activities), Ct)))).Code);
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }
    [Fact] public async Task Stale_calendar_and_edited_notes_cannot_apply_an_old_proposal()
    {
        var (h, uid, note) = await Setup(); await using var lifetime = h;
        var plan = await Call(h, s => s.PreviewNoteActivitiesAsync(uid, note.Id, new(note.Version, Activities(h.Clock.GetUtcNow())), Ct));
        await Call(h, s => s.SaveEventAsync(uid, null, new("New commitment", h.Clock.GetUtcNow().AddDays(3), h.Clock.GetUtcNow().AddDays(3).AddMinutes(15)), Ct));
        Assert.Equal("PLAN_STALE", (await Assert.ThrowsAsync<AuthException>(() => Call(h,
            s => s.ApplyNoteActivitiesAsync(uid, note.Id, plan.Id, new(note.Version), Ct)))).Code);
        await Call(h, s => s.SaveNoteAsync(uid, note.Id, new("Changed", "Different request", note.Version), Ct));
        Assert.Equal("NOTE_STALE", (await Assert.ThrowsAsync<AuthException>(() => Call(h,
            s => s.ApplyNoteActivitiesAsync(uid, note.Id, plan.Id, new(note.Version), Ct)))).Code);
        Assert.Single(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }
    [Fact] public async Task Events_only_note_reuses_an_existing_event_and_requires_note_specific_confirmation()
    {
        var (h, uid, note) = await Setup(); await using var lifetime = h;
        await Call(h, async s => { await s.SetAvailabilityAsync(uid, new([]), Ct); return true; });
        var events = Activities(h.Clock.GetUtcNow()).Take(3).ToArray();
        await Call(h, s => s.SaveEventAsync(uid, null, new(events[0].Name, events[0].Start!.Value, events[0].End!.Value, EventType: "CLASS"), Ct));
        var plan = await Call(h, s => s.PreviewNoteActivitiesAsync(uid, note.Id, new(note.Version, events), Ct));
        Assert.Empty(plan.Sessions);
        Assert.Equal("NOTE_PLAN_CONFIRMATION_REQUIRED", (await Assert.ThrowsAsync<AuthException>(() => Call(h, async s => {
            await s.ApplyPlanAsync(uid, plan.Id, Ct); return true;
        }))).Code);
        var result = await Call(h, s => s.ApplyNoteActivitiesAsync(uid, note.Id, plan.Id, new(note.Version), Ct));
        Assert.True(result.Plan.IsApplied);
        Assert.Equal(3, (await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct))).Length);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }
    [Fact] public async Task An_infeasible_task_prevents_partial_saving_of_the_three_fixed_events()
    {
        var (h, uid, note) = await Setup(); await using var lifetime = h;
        var activities = Activities(h.Clock.GetUtcNow());
        activities[3] = activities[3] with { Start = activities[0].Start };
        var plan = await Call(h, s => s.PreviewNoteActivitiesAsync(uid, note.Id, new(note.Version, activities), Ct));
        Assert.Contains(plan.Warnings, x => x.UnscheduledMinutes > 0);
        Assert.Equal("NOTE_PLAN_INCOMPLETE", (await Assert.ThrowsAsync<AuthException>(() => Call(h,
            s => s.ApplyNoteActivitiesAsync(uid, note.Id, plan.Id, new(note.Version), Ct)))).Code);
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }
    [Fact] public async Task Overlapping_fixed_events_are_rejected_without_creating_any_activity()
    {
        var (h, uid, note) = await Setup(); await using var lifetime = h;
        var activities = Activities(h.Clock.GetUtcNow());
        activities[1] = activities[1] with { Start = activities[0].Start };
        Assert.Equal("FIXED_EVENT_OVERLAP", (await Assert.ThrowsAsync<AuthException>(() => Call(h,
            s => s.PreviewNoteActivitiesAsync(uid, note.Id, new(note.Version, activities), Ct)))).Code);
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }
}
