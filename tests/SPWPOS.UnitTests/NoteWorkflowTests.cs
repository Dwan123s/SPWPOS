using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
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
    [Fact] public async Task Draft_saves_preserve_line_breaks_and_reject_stale_versions_without_overwriting_content()
    {
        await using var h = await CreateHarnessAsync(); var user = await h.RegisterAsync();
        var content = "Báo cáo 60 phút\n\n";
        var first = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Nháp", content), Ct));
        Assert.Equal(content, first.Content);
        var second = await Call(h, s => s.SaveNoteAsync(user.Id, first.Id, new(first.Title, content + "Ý mới\n", first.Version), Ct));
        var conflict = await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.SaveNoteAsync(user.Id, first.Id,
            new(first.Title, "Dữ liệu từ tab cũ", first.Version), Ct)));
        Assert.Equal("NOTE_STALE", conflict.Code);
        Assert.Equal(second.Content, Assert.Single(await Call(h, s => s.GetNotesAsync(user.Id, Ct))).Content);
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

    [Fact] public async Task Note_automatic_scheduling_returns_an_urgent_displacement_proposal_without_applying_it()
    {
        await using var h = await CreateHarnessAsync(); var user = await h.RegisterAsync();
        var now = h.Clock.GetUtcNow(); var start = now.AddDays(1);
        await Call(h, async s => {
            await s.SetPreferenceAsync(user.Id, new("UTC", 25, 240, 0, 0, 1440), Ct);
            await s.SetAvailabilityAsync(user.Id, new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 0, 1440)).ToArray()), Ct);
            return true;
        });
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Khẩn", "Báo cáo khẩn 60 phút"), Ct));
        note = await Call(h, s => s.AnalyzeNoteAsync(user.Id, note.Id, new(note.Version), Ct));
        note = await Call(h, s => s.ImportNoteAsync(user.Id, note.Id, new(note.Version,
            [note.Analysis!.Tasks[0] with { Priority = 4, EstimatedMinutes = 60, Deadline = start.AddHours(1), IsHardDeadline = true }]), Ct));
        var old = new WorkTask { UserId = user.Id, Name = "Đã lên lịch", Deadline = now.AddDays(7), EstimatedMinutes = 60 };
        var original = new WorkSession { UserId = user.Id, TaskId = old.Id, Start = start, End = start.AddHours(1) };
        await h.DbAsync(async db => {
            var urgent = await db.WorkTasks.SingleAsync(Ct); urgent.WorkWindowStart = start; urgent.WorkWindowEnd = start.AddHours(1);
            db.WorkTasks.Add(old); db.WorkSessions.Add(original); await db.SaveChangesAsync(Ct);
        });
        var result = await Call(h, s => s.ScheduleNoteAsync(user.Id, note.Id, new(note.Version, true, 7), Ct));
        Assert.True(result.Plan.HasUrgentReplan); Assert.False(result.Plan.IsApplied);
        Assert.Empty(result.Plan.Warnings);
        Assert.Equal(WorkSessionStatus.Scheduled, Assert.Single(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct))).Status);
        await Call(h, async s => { await s.ApplyPlanAsync(user.Id, result.Plan.Id, Ct); return true; });
        Assert.True((await Call(h, s => s.GetPlanAsync(user.Id, result.Plan.Id, Ct))).IsApplied);
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

    [Fact]
    public async Task Delete_note_works()
    {
        await using var h = await CreateHarnessAsync(); var user = await h.RegisterAsync();
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Delete me", "Nội dung"), Ct));
        await Call(h, async s => { await s.DeleteNoteAsync(user.Id, note.Id, note.Version, Ct); return true; });
        Assert.Empty(await h.DbAsync(db => db.PlanningNotes.ToArrayAsync(Ct)));
    }

    [Fact]
    public async Task Delete_scheduled_note_works()
    {
        await using var h = await CreateHarnessAsync(); var user = await h.RegisterAsync();
        await Call(h, async s => { await s.SetAvailabilityAsync(user.Id,
            new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 0, 1440)).ToArray()), Ct); return true; });
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Scheduled", "Viết báo cáo 60 phút"), Ct));
        var result = await Call(h, s => s.ScheduleNoteAsync(user.Id, note.Id, new(note.Version, false, 7), Ct));
        await Call(h, async s => { await s.DeleteNoteAsync(user.Id, note.Id, result.Note.Version, Ct); return true; });
        Assert.Empty(await h.DbAsync(db => db.PlanningNotes.ToArrayAsync(Ct)));
    }

    private sealed class DynamicInterpreter : ITaskInterpreter
    {
        public int Count { get; set; } = 1;
        public int Calls { get; private set; }
        public NoteAnalysisContext? LastContext { get; private set; }
        public NoteAgentCapabilities Capabilities => new(true, Provider: "Gemini");
        public Task<NoteAnalysis> AnalyzeNoteChangesAsync(string text, NoteAnalysisContext context, string timeZoneId, DateTimeOffset now, CancellationToken ct)
        { LastContext = context; return AnalyzeNoteAsync(text, true, timeZoneId, now, ct); }
        public Task<NoteAnalysis> AnalyzeNoteAsync(string text, bool useAi, string timeZoneId, DateTimeOffset now, CancellationToken ct)
        {
            Calls++;
            TaskInput[] tasks = Count >= 2
                ? [new("Báo cáo", "", now.AddDays(2), 120, TaskType: "WRITING"), new("Ôn bài", "", now.AddDays(2), 90, TaskType: "STUDY")]
                : [new("Báo cáo", "", now.AddDays(2), 60, TaskType: "WRITING")];
            if (Count == 3) tasks = [..tasks, new("Chuẩn bị slide", "", now.AddDays(3), 30, TaskType: "WRITING")];
            return Task.FromResult(new NoteAnalysis(tasks, "Gemini", [], false));
        }
        public Task<TaskSuggestion> CaptureAsync(CaptureInput input, string timeZoneId, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<BreakdownView> BreakdownAsync(TaskInput input, bool useAi, CancellationToken ct) => throw new NotSupportedException();
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Repeated_note_edits_only_add_new_tasks_and_preserve_original_task_and_sessions(bool legacy)
    {
        var interpreter = new DynamicInterpreter { Count = 1 };
        await using var h = await ServiceHarness.CreateAsync(services => services.AddSingleton<ITaskInterpreter>(interpreter));
        var user = await h.RegisterAsync();
        await Call(h, async s => { await s.SetAvailabilityAsync(user.Id,
            new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 0, 1440)).ToArray()), Ct); return true; });

        // 1. Initial note creation and auto-schedule
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Lên kế hoạch", "Viết báo cáo 60 phút"), Ct));
        var firstResult = await Call(h, s => s.ScheduleNoteAsync(user.Id, note.Id, new(note.Version, false, 7), Ct));
        Assert.True(firstResult.Plan.IsApplied);
        Assert.Single(firstResult.Note.CreatedTaskIds);
        Assert.Single(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        var originalTask = Assert.Single(await h.DbAsync(db => db.WorkTasks.AsNoTracking().ToArrayAsync(Ct)));
        var originalSessions = await h.DbAsync(db => db.WorkSessions.AsNoTracking().ToArrayAsync(Ct));
        if (legacy) await h.DbAsync(async db => { (await db.PlanningNotes.SingleAsync(Ct)).ProcessingStateJson = ""; await db.SaveChangesAsync(Ct); });

        // 2. User edits the note and saves it
        interpreter.Count = 2;
        var edited = await Call(h, s => s.SaveNoteAsync(user.Id, note.Id, new("Kế hoạch cập nhật", "Viết báo cáo và ôn bài", firstResult.Note.Version), Ct));
        Assert.Null(edited.PlanId);
        Assert.Equal("Kế hoạch cập nhật", edited.Title);

        // 3. User clicks "Cập nhật công việc & lịch trình"
        var secondResult = await Call(h, s => s.ScheduleNoteAsync(user.Id, note.Id, new(edited.Version, false, 7), Ct));
        Assert.True(secondResult.Plan.IsApplied);
        Assert.Equal(2, secondResult.Note.CreatedTaskIds.Length);
        Assert.Equal(2, (await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct))).Length);
        Assert.Equal("Viết báo cáo 60 phút", interpreter.LastContext!.ProcessedContent);
        Assert.Contains("BÁO CÁO", interpreter.LastContext.ExistingTaskNames);
        var unchanged = await h.DbAsync(db => db.WorkTasks.AsNoTracking().SingleAsync(x => x.Id == originalTask.Id, Ct));
        Assert.Equal(originalTask.Version, unchanged.Version);
        Assert.Equal(originalTask.EstimatedMinutes, unchanged.EstimatedMinutes);
        Assert.Equal(originalTask.Deadline, unchanged.Deadline);
        var sessions = await h.DbAsync(db => db.WorkSessions.AsNoTracking().ToArrayAsync(Ct));
        Assert.Contains(sessions, s => s.TaskId == secondResult.Note.CreatedTaskIds[0]);
        Assert.Contains(sessions, s => s.TaskId == secondResult.Note.CreatedTaskIds[1]);
        foreach (var old in originalSessions)
        {
            var kept = Assert.Single(sessions, x => x.Id == old.Id);
            Assert.Equal(old.Start, kept.Start); Assert.Equal(old.End, kept.End); Assert.Equal(old.Status, kept.Status);
        }
        interpreter.Count = 3;
        var thirdDraft = await Call(h, s => s.SaveNoteAsync(user.Id, note.Id,
            new("Lần ba", "Viết báo cáo và ôn bài, thêm chuẩn bị slide", secondResult.Note.Version), Ct));
        var third = await Call(h, s => s.ScheduleNoteAsync(user.Id, note.Id, new(thirdDraft.Version, true, 7), Ct));
        Assert.Equal(3, third.Note.CreatedTaskIds.Length);
        Assert.Equal(3, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Single(third.Note.PendingTaskIds!);
        Assert.Equal("Chuẩn bị slide", Assert.Single(third.Note.Analysis!.Tasks).Name);
        var retry = await Call(h, s => s.ScheduleNoteAsync(user.Id, note.Id, new(thirdDraft.Version, true, 7), Ct));
        Assert.Equal(third.Plan.Id, retry.Plan.Id);
        Assert.Equal(3, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Renamed_or_deleted_tasks_are_not_recreated_and_forged_old_task_inputs_cannot_overwrite_them()
    {
        var interpreter = new DynamicInterpreter();
        await using var h = await ServiceHarness.CreateAsync(services => services.AddSingleton<ITaskInterpreter>(interpreter));
        var user = await h.RegisterAsync();
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Nháp", "Báo cáo 60 phút"), Ct));
        note = await Call(h, s => s.AnalyzeNoteAsync(user.Id, note.Id, new(note.Version), Ct));
        var oldInput = note.Analysis!.Tasks[0];
        note = await Call(h, s => s.ImportNoteAsync(user.Id, note.Id, new(note.Version, [oldInput]), Ct));
        await h.DbAsync(async db => { var task = await db.WorkTasks.SingleAsync(Ct); task.Name = "Báo cáo đã đổi tên"; task.IsDeleted = true; await db.SaveChangesAsync(Ct); });
        interpreter.Count = 2;
        var draft = await Call(h, s => s.SaveNoteAsync(user.Id, note.Id, new(note.Title, "Báo cáo 60 phút\nÔn bài 90 phút", note.Version), Ct));
        var analyzed = await Call(h, s => s.AnalyzeNoteAsync(user.Id, note.Id, new(draft.Version), Ct));
        Assert.Equal("Ôn bài", Assert.Single(analyzed.Analysis!.Tasks).Name);
        var imported = await Call(h, s => s.ImportNoteAsync(user.Id, note.Id, new(analyzed.Version,
            [oldInput with { EstimatedMinutes = 180 }, analyzed.Analysis.Tasks[0]]), Ct));
        Assert.Equal(2, imported.CreatedTaskIds.Length);
        var old = await h.DbAsync(db => db.WorkTasks.SingleAsync(x => x.IsDeleted, Ct));
        Assert.Equal("Báo cáo đã đổi tên", old.Name); Assert.Equal(60, old.EstimatedMinutes);
        Assert.Equal(2, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Saving_title_or_identical_content_preserves_analysis_and_unchanged_processed_content_has_no_new_work()
    {
        var interpreter = new DynamicInterpreter();
        await using var h = await ServiceHarness.CreateAsync(services => services.AddSingleton<ITaskInterpreter>(interpreter));
        var user = await h.RegisterAsync();
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Nháp", "Báo cáo 60 phút"), Ct));
        note = await Call(h, s => s.AnalyzeNoteAsync(user.Id, note.Id, new(note.Version), Ct));
        note = await Call(h, s => s.ImportNoteAsync(user.Id, note.Id, new(note.Version, note.Analysis!.Tasks), Ct));
        var renamed = await Call(h, s => s.SaveNoteAsync(user.Id, note.Id, new("Tiêu đề mới", note.Content, note.Version), Ct));
        Assert.NotNull(renamed.Analysis); Assert.True(renamed.AnalysisImported);
        var same = await Call(h, s => s.SaveNoteAsync(user.Id, note.Id, new(renamed.Title, renamed.Content, renamed.Version), Ct));
        Assert.Equal(renamed.Version, same.Version);
        h.Clock.Advance(TimeSpan.FromDays(1));
        var analyzed = await Call(h, s => s.AnalyzeNoteAsync(user.Id, note.Id, new(same.Version), Ct));
        Assert.Empty(analyzed.Analysis!.Tasks); Assert.Empty(analyzed.Analysis.Activities!);
        Assert.Equal(1, interpreter.Calls);
        Assert.Single(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }
}
