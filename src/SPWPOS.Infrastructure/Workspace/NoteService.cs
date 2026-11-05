using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public NoteAgentCapabilities GetNoteAgentCapabilities() => interpreter.Capabilities;
    private static NoteScheduleResult AgentResult(NoteView note, PlanView plan) => new(note, plan, [
        new("READ_NOTE", "Đã đọc ghi chú và múi giờ tài khoản."),
        new("ANALYZE", $"Đã nhận diện {note.Analysis?.Tasks.Length ?? 0} công việc bằng {(note.Analysis?.Source is "OpenAI" or "Gemini" or "VertexAI" ? note.Analysis.Source : "bộ phân tích trên server")}."),
        new("CREATE_TASKS", $"Đã lưu {note.CreatedTaskIds.Length} công việc; thử lại không tạo trùng."),
        new("PLAN", $"Đã xếp {plan.Sessions.Count(x => !x.Preserved)} phiên mới và giữ {plan.Sessions.Count(x => x.Preserved)} phiên hiện có."),
        new("APPLY", plan.IsApplied ? "Đã kiểm tra ràng buộc và áp dụng lịch." : "Lịch chưa được áp dụng.")
    ]);
    private static readonly JsonSerializerOptions NoteJson = new(JsonSerializerDefaults.Web);
    private static NoteView NoteViewOf(PlanningNote note)
    {
        var processing = ProcessingOf(note);
        return new(note.Id, note.Title, note.Content, note.Version, note.CreatedAt, note.UpdatedAt,
            note.AnalysisJson.Length == 0 ? null : JsonSerializer.Deserialize<NoteAnalysis>(note.AnalysisJson, NoteJson),
            JsonSerializer.Deserialize<Guid[]>(note.CreatedTaskIdsJson, NoteJson)!, note.PlanId,
            processing.AnalysisImported, processing.Content.Length > 0, processing.PendingTaskIds,
            processing.InboxCompletedAt is not null, processing.InboxCompletedAt,
            null);
    }
    private static void CheckNoteVersion(PlanningNote note, Guid? version) =>
        Require(version == note.Version, "NOTE_STALE", "Ghi chú đã thay đổi. Tải lại ghi chú trước khi tiếp tục.", 409);

    public async Task<NoteView[]> GetNotesAsync(Guid uid, CancellationToken ct) =>
        (await Mine<PlanningNote>(uid).AsNoTracking().OrderByDescending(x => x.UpdatedAt).Take(200).ToArrayAsync(ct)).Select(NoteViewOf).ToArray();

    public Task<NoteView> SetInboxCompletionAsync(Guid uid, Guid id, NoteInboxCompletionInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var note = await Find<PlanningNote>(uid, id, ct);
        CheckNoteVersion(note, input.Version);
        var processing = ProcessingOf(note);
        Require(!processing.AnalysisImported, "INBOX_ALREADY_PROCESSED", "Mục này đã chuyển thành công việc. Cập nhật trạng thái trong Công việc.", 409);
        if ((processing.InboxCompletedAt is not null) == input.Completed) return NoteViewOf(note);
        SetProcessing(note, processing with { InboxCompletedAt = input.Completed ? Now : null,
            InboxReminderAt = input.Completed ? null : processing.InboxReminderAt });
        note.Version = Guid.NewGuid(); note.UpdatedAt = Now;
        return NoteViewOf(note);
    }, ct, false);

    public Task<NoteView> SaveNoteAsync(Guid uid, Guid? id, NoteInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        Require(!string.IsNullOrWhiteSpace(input.Title) && input.Title.Length <= 120 &&
            !string.IsNullOrWhiteSpace(input.Content) && input.Content.Length <= 8000, "VALIDATION_ERROR", "Nhập tiêu đề và nội dung ghi chú (tối đa 8000 ký tự).");
        PlanningNote note;
        if (id is { } noteId)
        {
            note = await Find<PlanningNote>(uid, noteId, ct);
            CheckNoteVersion(note, input.Version);
        }
        else
        {
            Require(await Mine<PlanningNote>(uid).CountAsync(ct) < 200, "NOTE_LIMIT", "Tối đa 200 ghi chú. Xóa ghi chú không cần dùng để thêm mới.");
            note = new() { UserId = uid, CreatedAt = Now }; db.PlanningNotes.Add(note);
        }
        var title = input.Title.Trim(); var content = input.Content;
        if (note.Title == title && note.Content == content) return NoteViewOf(note);
        if (note.Content != content)
        {
            var processing = await ProcessingWithTasks(note, ct);
            SetProcessing(note, processing with { AnalysisImported = false, PendingTaskIds = [], InboxReminderAt = null });
            note.AnalysisJson = ""; note.PlanId = null;
        }
        note.Title = title; note.Content = content; note.Version = Guid.NewGuid(); note.UpdatedAt = Now;
        return NoteViewOf(note);
    }, ct, false);

    public Task DeleteNoteAsync(Guid uid, Guid id, Guid version, CancellationToken ct) => Mutate(uid, async () =>
    {
        var note = await Find<PlanningNote>(uid, id, ct);
        if (version != Guid.Empty) CheckNoteVersion(note, version);
        db.PlanningNotes.Remove(note);
    }, ct, false);

    public async Task<NoteView> AnalyzeNoteAsync(Guid uid, Guid id, NoteAnalysisInput input, CancellationToken ct)
    {
        var snapshot = await Mine<PlanningNote>(uid).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        Require(snapshot is not null, "NOT_FOUND", "Không tìm thấy ghi chú.", 404);
        CheckNoteVersion(snapshot, input.Version);
        var processing = await ProcessingWithTasks(snapshot, ct);
        var zone = (await State(uid, ct)).Preference.TimeZoneId;
        var analysisContent = snapshot.Content;
        if (processing.MatrixDraft?.Deadline is { } draftDeadline)
            analysisContent += "\nHạn chót đã chọn: " + TimeZoneInfo.ConvertTime(draftDeadline, PlanningEngine.Zone(zone)).ToString("dd/MM/yyyy HH:mm") + " (" + zone + ").";
        // Provider calls happen outside the account transaction; verify the note again before saving.
        var analysis = await RunJob(uid, "AI_REQUEST", async () =>
        {
            var result = processing.Content == snapshot.Content
                ? new NoteAnalysis([], interpreter.Capabilities.Provider, ["Không có hoạt động mới để thêm vào lịch."], Activities: [])
                : processing.Content.Length > 0
                    ? await interpreter.AnalyzeNoteChangesAsync(analysisContent,
                        new(processing.Content, processing.TaskNames, processing.ActivityKeys), zone, Now, ct)
                    : await interpreter.AnalyzeNoteAsync(analysisContent, true, zone, Now, ct);
            Require(result.Source is "OpenAI" or "Gemini" or "VertexAI", "NOTE_AI_UNAVAILABLE", "Ghi chú cần được phân tích bằng AI. Vui lòng thử lại.", 503);
            return result;
        }, ct);
        var allowedTypes = await db.TaskTypes.Where(x => x.IsActive).Select(x => x.Code).ToArrayAsync(ct);
        string NoteTaskType(string type)
        {
            var normalized = NormalizeTaskType(type);
            if (allowedTypes.Contains(normalized)) return normalized;
            Require(allowedTypes.Contains("OTHER"), "INVALID_TASK_TYPE", "Loại công việc AI nhận diện chưa được hỗ trợ và loại Khác đang tắt. Kiểm tra danh mục công việc rồi phân tích lại.", 409);
            return "OTHER";
        }
        analysis = OnlyNewActivities(processing, analysis);
        analysis = analysis with { Tasks = analysis.Tasks.Select(x => x with
            { TaskType = NoteTaskType(x.TaskType),
              CreationSource = "NATURAL_LANGUAGE", CategoryId = null, ParentId = null }).ToArray(),
            Activities = analysis.Activities?.Select(x => x.Kind == "TASK" ? x with { Type = NoteTaskType(x.Type) } : x).ToArray() };
        return await Mutate(uid, async () =>
        {
            var note = await Find<PlanningNote>(uid, id, ct); CheckNoteVersion(note, input.Version);
            SetProcessing(note, processing with { AnalysisImported = false, PendingTaskIds = [] });
            note.AnalysisJson = JsonSerializer.Serialize(analysis, NoteJson);
            note.PlanId = null;
            note.Version = Guid.NewGuid(); note.UpdatedAt = Now;
            return NoteViewOf(note);
        }, ct, false);
    }

    public Task<NoteView> ImportNoteAsync(Guid uid, Guid id, NoteImportInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var note = await Find<PlanningNote>(uid, id, ct);
        var processing = await ProcessingWithTasks(note, ct);
        if (processing.AnalysisImported && processing.ImportVersion == input.Version) return NoteViewOf(note);
        CheckNoteVersion(note, input.Version);
        Require(note.AnalysisJson.Length > 0, "NOTE_NOT_ANALYZED", "Phân tích ghi chú trước khi tạo công việc.");
        var analysis = JsonSerializer.Deserialize<NoteAnalysis>(note.AnalysisJson, NoteJson)!;
        if (processing.AnalysisImported) return NoteViewOf(note);
        Require(analysis.Activities is null, "NOTE_NEEDS_REVIEW", "Xem trước và xác nhận cả sự kiện và công việc từ ghi chú.", 409);
        Require(analysis.Source is "OpenAI" or "Gemini" or "VertexAI", "NOTE_AI_REQUIRED", "Phân tích lại ghi chú bằng AI trước khi tạo công việc.", 409);
        Require(input.Tasks is { Length: >= 1 and <= 12 }, "VALIDATION_ERROR", "Chọn từ 1 đến 12 công việc.");
        var existingIds = JsonSerializer.Deserialize<List<Guid>>(note.CreatedTaskIdsJson, NoteJson) ?? new();
        var ids = new List<Guid>();
        var accepted = new List<TaskInput>();
        var knownNames = processing.TaskNames.ToHashSet();
        foreach (var candidate in input.Tasks)
        {
            Require(candidate is not null, "VALIDATION_ERROR", "Công việc không hợp lệ.");
            var taskInput = candidate with { CreationSource = "NATURAL_LANGUAGE" };
            ValidTask(taskInput);
            Require(taskInput.CategoryId is null && taskInput.ParentId is null, "VALIDATION_ERROR", "Các việc từ ghi chú được tạo độc lập.");
            Require(await db.TaskTypes.AnyAsync(x => x.Code == NormalizeTaskType(taskInput.TaskType) && x.IsActive, ct), "INVALID_TASK_TYPE", "Loại công việc không hợp lệ.");

            if (knownNames.Add(NoteName(taskInput.Name)))
            {
                var task = NewTask(uid, taskInput, Now);
                ApplyMatrixDraftClassification(note, [task]);
                db.WorkTasks.Add(task);
                ids.Add(task.Id);
                existingIds.Add(task.Id);
                accepted.Add(taskInput with { TaskType = task.TaskType });
            }
        }
        Require(ids.Count > 0, "NOTE_NO_NEW_ACTIVITIES", "Không có việc mới để thêm. Các việc đã tạo được giữ nguyên.", 409);
        var activeCount = await Mine<WorkTask>(uid).CountAsync(x => !x.IsDeleted && x.Status != WorkTaskStatus.Completed && x.Status != WorkTaskStatus.Cancelled, ct);
        Require(activeCount + ids.Count <= 500, "TASK_LIMIT", "Tối đa 500 công việc đang hoạt động.");
        if (input.Tasks.Length == analysis.Tasks.Length)
            knownNames.UnionWith(analysis.Tasks.Select(x => NoteName(x.Name)));
        SetProcessing(note, processing with { Content = note.Content, TaskNames = knownNames.ToArray(), PendingTaskIds = ids.ToArray(), AnalysisImported = true, ImportVersion = input.Version, InboxReminderAt = null });
        note.AnalysisJson = JsonSerializer.Serialize(analysis with { Tasks = accepted.ToArray() }, NoteJson);
        note.CreatedTaskIdsJson = JsonSerializer.Serialize(existingIds.Distinct().ToArray(), NoteJson);
        note.Version = Guid.NewGuid(); note.UpdatedAt = Now;
        return NoteViewOf(note);
    }, ct);

    public async Task<NoteScheduleResult> ScheduleNoteAsync(Guid uid, Guid id, NoteScheduleInput input, CancellationToken ct)
    {
        Require(input.HorizonDays is >= 1 and <= 30, "VALIDATION_ERROR", "Số ngày xếp lịch phải từ 1 đến 30.");
        var entity = await Find<PlanningNote>(uid, id, ct);
        var note = NoteViewOf(entity);
        // Returning an already-applied result is read-only. The original request
        // version predates analysis/import, so comparing it to the final note
        // version would incorrectly generate a second plan on retry. Saving an
        // edited note clears PlanId, preventing replay across content changes.
        if (note.PlanId is { } previousId)
        {
            var previous = await GetPlanAsync(uid, previousId, ct);
            if (previous.IsApplied) return AgentResult(note, previous);
        }
        CheckNoteVersion(entity, input.Version);
        var state = await State(uid, ct);
        Require(state.Availability.Length > 0 || state.Exceptions.Any(x => x.IsAvailable), "NO_AVAILABILITY",
            "Thiết lập giờ rảnh trước khi agent tự xếp lịch.");
        if (note.Analysis is null)
        {
            note = await AnalyzeNoteAsync(uid, id, new(input.Version, true), ct);
        }
        if (!note.AnalysisImported)
        {
            Require((note.Analysis!.Activities?.Length ?? note.Analysis.Tasks.Length) > 0, "NOTE_NO_NEW_ACTIVITIES", "Không có việc mới để thêm. Các việc và lịch đã tạo được giữ nguyên.", 409);
            Require(note.Analysis!.Activities is null, "NOTE_NEEDS_REVIEW", "Kiểm tra sự kiện, thời lượng và lịch đề xuất trước khi lưu từ ghi chú.");
            Require(!note.Analysis!.NeedsReview, "NOTE_NEEDS_REVIEW",
                "Ghi chú có ngày không hợp lệ hoặc điều kiện giờ rảnh/phụ thuộc chưa được thiết lập. Xem kết quả phân tích, cập nhật Thời gian & tùy chọn hoặc Công việc rồi sửa ghi chú trước khi tự xếp lịch.");
            Require(note.Analysis!.Tasks.Length > 0, "NOTE_NO_TASKS", "Chưa tìm thấy công việc. Bổ sung việc cần làm trong ghi chú.");
            note = await ImportNoteAsync(uid, id, new(note.Version, note.Analysis.Tasks), ct);
        }
        var plan = await GeneratePlanAsync(uid, new(Now, input.HorizonDays, note.PendingTaskIds ?? note.CreatedTaskIds, true, "NEW_TASK"), ct);
        var noteVersion = note.Version;
        note = await Mutate(uid, async () =>
        {
            var saved = await Find<PlanningNote>(uid, id, ct); CheckNoteVersion(saved, noteVersion);
            saved.PlanId = plan.Id; saved.Version = Guid.NewGuid(); saved.UpdatedAt = Now;
            return NoteViewOf(saved);
        }, ct, false);
        // The normal apply path validates revision, conflicts, deadlines and ownership.
        if (plan.RequiresConfirmation) return AgentResult(note, plan);
        await ApplyPlanAsync(uid, plan.Id, ct);
        return AgentResult(note, await GetPlanAsync(uid, plan.Id, ct));
    }
}
