using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static string MatrixDraftDescription(PlanningNote note) => note.Content == note.Title ? "" :
        note.Content.StartsWith(note.Title + "\n", StringComparison.Ordinal) ? note.Content[(note.Title.Length + 1)..] : note.Content;
    private async Task<EisenhowerTaskView[]> MatrixDrafts(Guid uid, CancellationToken ct)
    {
        var notes = await Mine<PlanningNote>(uid).AsNoTracking().ToArrayAsync(ct);
        return notes.Select(note => (Note: note, Processing: ProcessingOf(note)))
            .Where(x => x.Processing.MatrixDraft != null && !x.Processing.AnalysisImported && x.Processing.InboxCompletedAt is null)
            .Select(x => {
                var draft = x.Processing.MatrixDraft!;
                var important = draft.Quadrant is "Q1" or "Q2" ? "Important" : draft.Quadrant is "Q3" or "Q4" ? "Lower" : "Unknown";
                var urgency = draft.Quadrant is "Q1" or "Q3" ? "Urgent" : draft.Quadrant is "Q2" or "Q4" ? "NotUrgent" : "Unknown";
                return new EisenhowerTaskView(x.Note.Id, x.Note.Title, draft.Deadline, important, urgency, draft.Quadrant,
                    "Task nháp trong Inbox. Hoàn thiện nội dung và thời lượng trước khi lên lịch.", ["INBOX_DRAFT"], 0, null,
                    false, draft.Deadline is { } due && due < Now, false, false, Kind: "Draft", Version: x.Note.Version,
                    Order: draft.Order, Checklist: [], UrgencySource: "User", Recommendation: "Clarify", Description: MatrixDraftDescription(x.Note), CanEdit: true);
            }).ToArray();
    }
    public Task<Guid> SaveMatrixDraftAsync(Guid uid, Guid? id, MatrixDraftInput input, CancellationToken ct) => Mutate(uid, async () => {
        Require(eisenhowerOptions?.Value.Enabled == true, "FEATURE_DISABLED", "Ma trận chưa được bật.", 409);
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Trim().Length <= 120 && input.Description != null && input.Description.Length <= 4000,
            "VALIDATION_ERROR", "Nhập tên công việc (tối đa 120 ký tự) và mô tả tối đa 4000 ký tự.");
        Require(input.Quadrant is "Q1" or "Q2" or "Q3" or "Q4" or "Unknown", "INVALID_QUADRANT", "Vị trí task không hợp lệ.");
        var name = input.Name.Trim(); var description = input.Description.Trim();
        var content = name + (description.Length > 0 ? "\n" + description : "");
        PlanningNote note;
        if (id is { } existingId) {
            note = await Find<PlanningNote>(uid, existingId, ct);
            CheckNoteVersion(note, input.Version);
            Require(ProcessingOf(note) is { MatrixDraft: not null, AnalysisImported: false }, "DRAFT_ALREADY_PROCESSED", "Bản nháp đã được xử lý. Mở Nhập việc để xem tiếp.", 409);
        } else {
            Require(input.RequestId != Guid.Empty, "VALIDATION_ERROR", "Thiếu mã yêu cầu tạo bản nháp.");
            var existing = await db.PlanningNotes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.RequestId, ct);
            if (existing != null) {
                Require(existing.UserId == uid && existing.Title == name && existing.Content == content &&
                    ProcessingOf(existing).MatrixDraft is { } original && original.Quadrant == input.Quadrant && original.Deadline == input.Deadline,
                    "DRAFT_REQUEST_CONFLICT", "Yêu cầu này đã thay đổi. Hãy mở lại popup.", 409);
                return existing.Id;
            }
            Require(await Mine<PlanningNote>(uid).CountAsync(ct) < 200, "NOTE_LIMIT", "Tối đa 200 ghi chú. Xóa ghi chú không cần dùng để thêm mới.");
            note = new() { Id = input.RequestId, UserId = uid, CreatedAt = Now };
            db.PlanningNotes.Add(note);
        }
        var processing = ProcessingOf(note);
        note.Title = name; note.Content = content; note.AnalysisJson = ""; note.PlanId = null;
        note.Version = Guid.NewGuid(); note.UpdatedAt = Now;
        SetProcessing(note, processing with { MatrixDraft = new(input.Quadrant, input.Deadline, processing.MatrixDraft?.Order ?? 0), PendingTaskIds = [] });
        (await Preference(uid, ct)).Revision = Guid.NewGuid();
        return note.Id;
    }, ct, false);

    public Task MoveMatrixDraftAsync(Guid uid, Guid id, MatrixMoveInput input, CancellationToken ct) => Mutate(uid, async () => {
        Require(eisenhowerOptions?.Value.Enabled == true, "FEATURE_DISABLED", "Ma trận chưa được bật.", 409);
        var note = await Find<PlanningNote>(uid, id, ct); CheckNoteVersion(note, input.Version);
        var processing = ProcessingOf(note);
        Require(processing is { MatrixDraft: not null, AnalysisImported: false }, "DRAFT_ALREADY_PROCESSED", "Bản nháp đã được xử lý.", 409);
        Require(input.Quadrant is "Q1" or "Q2" or "Q3" or "Q4" or "Unknown", "INVALID_QUADRANT", "Vị trí task không hợp lệ.");
        var preference = await Preference(uid, ct);
        Require(input.Revision == preference.Revision, "MATRIX_STALE", "Ma trận vừa thay đổi. Hãy tải lại.", 409);
        Require(input.BeforeId is null, "DRAFT_ORDER_UNSUPPORTED", "Bản nháp chỉ chuyển giữa các ô. Hoàn thiện task để sắp thứ tự.");
        SetProcessing(note, processing with { MatrixDraft = processing.MatrixDraft with { Quadrant = input.Quadrant } });
        note.Version = Guid.NewGuid(); note.UpdatedAt = Now; preference.Revision = Guid.NewGuid();
    }, ct, false);
    private void ApplyMatrixDraftClassification(PlanningNote note, IEnumerable<WorkTask> tasks)
    {
        var quadrant = ProcessingOf(note).MatrixDraft?.Quadrant;
        if (quadrant is not ("Q1" or "Q2" or "Q3" or "Q4")) return;
        foreach (var task in tasks) SetMatrixQuadrant(task, quadrant, Now);
    }
}
