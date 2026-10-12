using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static readonly JsonSerializerOptions NoteJson = new(JsonSerializerDefaults.Web);
    private static NoteView NoteViewOf(PlanningNote note) => new(note.Id, note.Title, note.Content, note.Version,
        note.CreatedAt, note.UpdatedAt,
        note.AnalysisJson.Length == 0 ? null : JsonSerializer.Deserialize<NoteAnalysis>(note.AnalysisJson, NoteJson),
        JsonSerializer.Deserialize<Guid[]>(note.CreatedTaskIdsJson, NoteJson)!, note.PlanId);
    private static void CheckNoteVersion(PlanningNote note, Guid? version) =>
        Require(version == note.Version, "NOTE_STALE", "Ghi chú đã thay đổi. Tải lại ghi chú trước khi tiếp tục.", 409);
    private static void CheckNoteEditable(PlanningNote note) =>
        Require(note.CreatedTaskIdsJson == "[]", "NOTE_CONVERTED", "Ghi chú này đã tạo công việc. Tạo ghi chú mới cho các việc bổ sung.", 409);

    public async Task<NoteView[]> GetNotesAsync(Guid uid, CancellationToken ct) =>
        (await Mine<PlanningNote>(uid).AsNoTracking().OrderByDescending(x => x.UpdatedAt).Take(200).ToArrayAsync(ct)).Select(NoteViewOf).ToArray();

    public Task<NoteView> SaveNoteAsync(Guid uid, Guid? id, NoteInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        Require(!string.IsNullOrWhiteSpace(input.Title) && input.Title.Length <= 120 &&
            !string.IsNullOrWhiteSpace(input.Content) && input.Content.Length <= 8000, "VALIDATION_ERROR", "Nhập tiêu đề và nội dung ghi chú (tối đa 8000 ký tự).");
        PlanningNote note;
        if (id is { } noteId)
        {
            note = await Find<PlanningNote>(uid, noteId, ct);
            CheckNoteVersion(note, input.Version); CheckNoteEditable(note);
        }
        else
        {
            Require(await Mine<PlanningNote>(uid).CountAsync(ct) < 200, "NOTE_LIMIT", "Tối đa 200 ghi chú. Xóa ghi chú không cần dùng để thêm mới.");
            note = new() { UserId = uid, CreatedAt = Now }; db.PlanningNotes.Add(note);
        }
        note.Title = input.Title.Trim(); note.Content = input.Content.Trim(); note.AnalysisJson = "";
        note.Version = Guid.NewGuid(); note.UpdatedAt = Now;
        return NoteViewOf(note);
    }, ct, false);

    public Task DeleteNoteAsync(Guid uid, Guid id, Guid version, CancellationToken ct) => Mutate(uid, async () =>
    {
        var note = await Find<PlanningNote>(uid, id, ct); CheckNoteVersion(note, version);
        db.PlanningNotes.Remove(note);
    }, ct, false);

    public async Task<NoteView> AnalyzeNoteAsync(Guid uid, Guid id, NoteAnalysisInput input, CancellationToken ct)
    {
        var snapshot = await Mine<PlanningNote>(uid).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        Require(snapshot is not null, "NOT_FOUND", "Không tìm thấy ghi chú.", 404);
        CheckNoteVersion(snapshot, input.Version); CheckNoteEditable(snapshot);
        var zone = (await State(uid, ct)).Preference.TimeZoneId;
        // Provider calls happen outside the account transaction; verify the note again before saving.
        var analysis = input.UseAi
            ? await RunJob(uid, "AI_REQUEST", () => interpreter.AnalyzeNoteAsync(snapshot.Content, true, zone, Now, ct), ct, x => x.Source == "OpenAI")
            : await interpreter.AnalyzeNoteAsync(snapshot.Content, false, zone, Now, ct);
        var allowedTypes = await db.TaskTypes.Where(x => x.IsActive).Select(x => x.Code).ToArrayAsync(ct);
        analysis = analysis with { Tasks = analysis.Tasks.Select(x => x with
            { TaskType = allowedTypes.Contains(NormalizeTaskType(x.TaskType)) ? NormalizeTaskType(x.TaskType) : "OTHER",
              CreationSource = "NATURAL_LANGUAGE", CategoryId = null, ParentId = null }).ToArray() };
        return await Mutate(uid, async () =>
        {
            var note = await Find<PlanningNote>(uid, id, ct); CheckNoteVersion(note, input.Version); CheckNoteEditable(note);
            note.AnalysisJson = JsonSerializer.Serialize(analysis, NoteJson);
            note.Version = Guid.NewGuid(); note.UpdatedAt = Now;
            return NoteViewOf(note);
        }, ct, false);
    }

    public Task<NoteView> ImportNoteAsync(Guid uid, Guid id, NoteImportInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var note = await Find<PlanningNote>(uid, id, ct);
        // Retrying a successful request returns the same tasks, never creates duplicates.
        if (note.CreatedTaskIdsJson != "[]") return NoteViewOf(note);
        CheckNoteVersion(note, input.Version);
        Require(note.AnalysisJson.Length > 0, "NOTE_NOT_ANALYZED", "Phân tích ghi chú trước khi tạo công việc.");
        Require(input.Tasks is { Length: >= 1 and <= 12 }, "VALIDATION_ERROR", "Chọn từ 1 đến 12 công việc.");
        var activeCount = await Mine<WorkTask>(uid).CountAsync(x => !x.IsDeleted && x.Status != WorkTaskStatus.Completed && x.Status != WorkTaskStatus.Cancelled, ct);
        Require(activeCount + input.Tasks.Length <= 500, "TASK_LIMIT", "Tối đa 500 công việc đang hoạt động.");
        var ids = new List<Guid>();
        var accepted = new List<TaskInput>();
        foreach (var candidate in input.Tasks)
        {
            Require(candidate is not null, "VALIDATION_ERROR", "Công việc không hợp lệ.");
            var taskInput = candidate with { CreationSource = "NATURAL_LANGUAGE" };
            ValidTask(taskInput);
            Require(taskInput.CategoryId is null && taskInput.ParentId is null, "VALIDATION_ERROR", "Các việc từ ghi chú được tạo độc lập.");
            Require(await db.TaskTypes.AnyAsync(x => x.Code == NormalizeTaskType(taskInput.TaskType) && x.IsActive, ct), "INVALID_TASK_TYPE", "Loại công việc không hợp lệ.");
            var task = NewTask(uid, taskInput, Now); db.WorkTasks.Add(task); ids.Add(task.Id);
            accepted.Add(taskInput with { TaskType = task.TaskType });
        }
        var analysis = JsonSerializer.Deserialize<NoteAnalysis>(note.AnalysisJson, NoteJson)!;
        note.AnalysisJson = JsonSerializer.Serialize(analysis with { Tasks = accepted.ToArray() }, NoteJson);
        note.CreatedTaskIdsJson = JsonSerializer.Serialize(ids, NoteJson);
        note.Version = Guid.NewGuid(); note.UpdatedAt = Now;
        return NoteViewOf(note);
    }, ct);

    public async Task<NoteScheduleResult> ScheduleNoteAsync(Guid uid, Guid id, NoteScheduleInput input, CancellationToken ct)
    {
        Require(input.HorizonDays is >= 1 and <= 30, "VALIDATION_ERROR", "Số ngày xếp lịch phải từ 1 đến 30.");
        var note = NoteViewOf(await Find<PlanningNote>(uid, id, ct));
        if (note.PlanId is { } previousId)
        {
            var previous = await GetPlanAsync(uid, previousId, ct);
            if (previous.IsApplied) return new(note, previous);
        }
        var state = await State(uid, ct);
        Require(state.Availability.Length > 0 || state.Exceptions.Any(x => x.IsAvailable), "NO_AVAILABILITY",
            "Thiết lập giờ rảnh trước khi agent tự xếp lịch.");
        if (note.CreatedTaskIds.Length == 0)
        {
            note = await AnalyzeNoteAsync(uid, id, new(input.Version, input.UseAi), ct);
            Require(note.Analysis!.Tasks.Length > 0, "NOTE_NO_TASKS", "Chưa tìm thấy công việc. Bổ sung việc cần làm trong ghi chú.");
            note = await ImportNoteAsync(uid, id, new(note.Version, note.Analysis.Tasks), ct);
        }
        var plan = await GeneratePlanAsync(uid, new(Now, input.HorizonDays, note.CreatedTaskIds, true, "NEW_TASK"), ct);
        var noteVersion = note.Version;
        note = await Mutate(uid, async () =>
        {
            var saved = await Find<PlanningNote>(uid, id, ct); CheckNoteVersion(saved, noteVersion);
            saved.PlanId = plan.Id; saved.Version = Guid.NewGuid(); saved.UpdatedAt = Now;
            return NoteViewOf(saved);
        }, ct, false);
        // The normal apply path validates revision, conflicts, deadlines and ownership.
        await ApplyPlanAsync(uid, plan.Id, ct);
        return new(note, await GetPlanAsync(uid, plan.Id, ct));
    }
}
