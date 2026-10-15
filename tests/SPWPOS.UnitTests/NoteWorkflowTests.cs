using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class NoteWorkflowTests
{
    private static Task<ServiceHarness> CreateHarnessAsync(int count = 1, bool needsReview = false, string source = "Gemini") =>
        ServiceHarness.CreateAsync(services => services.AddSingleton<ITaskInterpreter>(new NoteAiTestDouble(count, needsReview, source)));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Call<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }

    [Fact] public async Task Note_is_persisted_and_analysis_does_not_create_tasks_or_sessions()
    {
        await using var h = await CreateHarnessAsync(count: 2); var user = await h.RegisterAsync();
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Tuần này", "Viết báo cáo ngày mai, 2 tiếng\nÔn bài thứ Sáu, 90 phút"), Ct));
        var saved = Assert.Single(await Call(h, s => s.GetNotesAsync(user.Id, Ct)));
        Assert.Equal(note.Content, saved.Content);
        var analyzed = await Call(h, s => s.AnalyzeNoteAsync(user.Id, note.Id, new(note.Version), Ct));
        Assert.NotNull(analyzed.Analysis); Assert.Equal("Gemini", analyzed.Analysis.Source);
        Assert.Equal(2, analyzed.Analysis.Tasks.Length);
        Assert.Equal(120, analyzed.Analysis.Tasks[0].EstimatedMinutes);
        Assert.Equal(90, analyzed.Analysis.Tasks[1].EstimatedMinutes);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.PlanProposals.ToArrayAsync(Ct)));
    }

    [Theory] [InlineData("Gemini")] [InlineData("VertexAI")]
    public async Task Import_is_atomic_and_retry_does_not_duplicate_tasks(string source)
    {
        await using var h = await CreateHarnessAsync(count: 2, source: source); var user = await h.RegisterAsync();
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Công việc", "Báo cáo 60 phút\nÔn bài 30 phút"), Ct));
        note = await Call(h, s => s.AnalyzeNoteAsync(user.Id, note.Id, new(note.Version), Ct));
        var tasks = note.Analysis!.Tasks;
        var error = await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.ImportNoteAsync(user.Id, note.Id,
            new(note.Version, [tasks[0], tasks[1] with { Priority = 10 }]), Ct)));
        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        var input = new NoteImportInput(note.Version, tasks);
        var imported = await Call(h, s => s.ImportNoteAsync(user.Id, note.Id, input, Ct));
        var retry = await Call(h, s => s.ImportNoteAsync(user.Id, note.Id, input, Ct));
        Assert.Equal(imported.CreatedTaskIds, retry.CreatedTaskIds);
        Assert.Equal(2, (await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct))).Length);
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Editing_invalidates_analysis_and_old_requests_are_rejected()
    {
        await using var h = await CreateHarnessAsync(); var user = await h.RegisterAsync();
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Bản nháp", "Viết báo cáo 60 phút"), Ct));
        var analyzed = await Call(h, s => s.AnalyzeNoteAsync(user.Id, note.Id, new(note.Version), Ct));
        var edited = await Call(h, s => s.SaveNoteAsync(user.Id, note.Id, new("Bản mới", "Ôn bài 30 phút", analyzed.Version), Ct));
        Assert.Null(edited.Analysis);
        Assert.Equal("NOTE_STALE", (await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.ImportNoteAsync(user.Id, note.Id,
            new(analyzed.Version, analyzed.Analysis!.Tasks), Ct)))).Code);
        Assert.Equal("NOTE_STALE", (await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.AnalyzeNoteAsync(user.Id, note.Id,
            new(analyzed.Version), Ct)))).Code);
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.AnalyzeNoteAsync(Guid.NewGuid(), note.Id,
            new(edited.Version), Ct)))).Code);
    }

    [Fact] public async Task Note_tasks_can_be_scheduled_and_only_apply_changes_the_calendar()
    {
        await using var h = await CreateHarnessAsync(); var user = await h.RegisterAsync();
        var slots = Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 0, 1440)).ToArray();
        await Call(h, async s => { await s.SetAvailabilityAsync(user.Id, new(slots), Ct); return true; });
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Kế hoạch", "Viết báo cáo ngày mai, 60 phút"), Ct));
        note = await Call(h, s => s.AnalyzeNoteAsync(user.Id, note.Id, new(note.Version), Ct));
        note = await Call(h, s => s.ImportNoteAsync(user.Id, note.Id, new(note.Version, note.Analysis!.Tasks), Ct));
        var plan = await Call(h, s => s.GeneratePlanAsync(user.Id,
            new(h.Clock.GetUtcNow(), 7, note.CreatedTaskIds, true, "NEW_TASK"), Ct));
        Assert.NotEmpty(plan.Sessions); Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        await Call(h, async s => { await s.ApplyPlanAsync(user.Id, plan.Id, Ct); return true; });
        Assert.NotEmpty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Automatic_scheduling_applies_once_and_keeps_existing_sessions()
    {
        await using var h = await CreateHarnessAsync(); var user = await h.RegisterAsync();
        await Call(h, async s => { await s.SetAvailabilityAsync(user.Id,
            new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 0, 1440)).ToArray()), Ct); return true; });
        await Call(h, s => s.SaveTaskAsync(user.Id, null, new("Existing", "", h.Clock.GetUtcNow().AddDays(2), 60), Ct));
        var original = await Call(h, s => s.GeneratePlanAsync(user.Id, new(h.Clock.GetUtcNow(), 7), Ct));
        await Call(h, async s => { await s.ApplyPlanAsync(user.Id, original.Id, Ct); return true; });
        var oldSessions = await h.DbAsync(db => db.WorkSessions.AsNoTracking().ToArrayAsync(Ct));
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Tự xếp lịch", "Ôn bài ngày mai 60 phút"), Ct));
        var input = new NoteScheduleInput(note.Version, false, 7);
        var result = await Call(h, s => s.ScheduleNoteAsync(user.Id, note.Id, input, Ct));
        Assert.True(result.Plan.IsApplied); Assert.Single(result.Note.CreatedTaskIds);
        Assert.Equal(new[] { "READ_NOTE", "ANALYZE", "CREATE_TASKS", "PLAN", "APPLY" }, result.Steps!.Select(x => x.Code));
        var sessions = await h.DbAsync(db => db.WorkSessions.AsNoTracking().ToArrayAsync(Ct));
        Assert.Contains(sessions, x => x.TaskId == result.Note.CreatedTaskIds[0]);
        foreach (var previous in oldSessions)
        {
            var kept = Assert.Single(sessions, x => x.Id == previous.Id);
            Assert.Equal(previous.Start, kept.Start); Assert.Equal(previous.Status, kept.Status);
        }
        var retry = await Call(h, s => s.ScheduleNoteAsync(user.Id, note.Id, input, Ct));
        Assert.Equal(result.Plan.Id, retry.Plan.Id);
        Assert.Equal(sessions.Length, (await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct))).Length);
        Assert.Equal(2, (await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct))).Length);
    }

    [Fact] public async Task Automatic_schedule_without_availability_does_not_create_work()
    {
        await using var h = await CreateHarnessAsync(); var user = await h.RegisterAsync();
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Không có giờ rảnh", "Viết báo cáo 60 phút"), Ct));
        Assert.Equal("NO_AVAILABILITY", (await Assert.ThrowsAsync<AuthException>(() => Call(h,
            s => s.ScheduleNoteAsync(user.Id, note.Id, new(note.Version), Ct)))).Code);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Automatic_agent_does_not_ignore_unsupported_note_conditions_or_create_partial_work()
    {
        await using var h = await CreateHarnessAsync(needsReview: true); var user = await h.RegisterAsync();
        await Call(h, async s => { await s.SetAvailabilityAsync(user.Id,
            new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 0, 1440)).ToArray()), Ct); return true; });
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Có điều kiện", "Tôi rảnh 19h đến 21h\nViết báo cáo 60 phút"), Ct));
        Assert.Equal("NOTE_NEEDS_REVIEW", (await Assert.ThrowsAsync<AuthException>(() => Call(h,
            s => s.ScheduleNoteAsync(user.Id, note.Id, new(note.Version), Ct)))).Code);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        var saved = Assert.Single(await Call(h, s => s.GetNotesAsync(user.Id, Ct)));
        Assert.True(saved.Analysis!.NeedsReview); Assert.Single(saved.Analysis.Tasks);
    }
    [Fact] public async Task Ai_failure_keeps_note_and_does_not_create_tasks_or_calendar()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        await Call(h, async service => { await service.SetAvailabilityAsync(user.Id, new([new AvailabilityInput(1, 0, 1440)]), Ct); return true; });
        var note = await Call(h, service => service.SaveNoteAsync(user.Id, null, new("Saved", "Viết báo cáo 60 phút"), Ct));
        var error = await Assert.ThrowsAsync<AuthException>(() => Call(h, service => service.ScheduleNoteAsync(user.Id, note.Id, new(note.Version, false, 7), Ct)));
        Assert.Equal("NOTE_AI_NOT_CONFIGURED", error.Code);
        var saved = Assert.Single(await Call(h, service => service.GetNotesAsync(user.Id, Ct)));
        Assert.Equal(note.Content, saved.Content); Assert.Equal(note.Version, saved.Version); Assert.Null(saved.Analysis);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.PlanProposals.ToArrayAsync(Ct)));
        var job = Assert.Single(await h.DbAsync(db => db.SystemJobRuns.ToArrayAsync(Ct)));
        Assert.Equal("FAILED", job.Status); Assert.Equal(error.Code, job.ErrorMessage);
    }
    [Fact] public async Task Old_rule_analysis_cannot_be_imported_without_new_ai_analysis()
    {
        await using var h = await CreateHarnessAsync(); var user = await h.RegisterAsync();
        var note = await Call(h, service => service.SaveNoteAsync(user.Id, null, new("Old", "Viết báo cáo 60 phút"), Ct));
        var old = new NoteAnalysis([new("Báo cáo", "", h.Clock.GetUtcNow().AddDays(2), 60)], "Rules", []);
        await h.DbAsync(async db => { var saved = await db.PlanningNotes.SingleAsync(Ct); saved.AnalysisJson = System.Text.Json.JsonSerializer.Serialize(old, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)); await db.SaveChangesAsync(Ct); });
        var error = await Assert.ThrowsAsync<AuthException>(() => Call(h, service => service.ImportNoteAsync(user.Id, note.Id, new(note.Version, old.Tasks), Ct)));
        Assert.Equal("NOTE_AI_REQUIRED", error.Code); Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }
}
