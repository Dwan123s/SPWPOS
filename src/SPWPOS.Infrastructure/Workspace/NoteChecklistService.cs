using System.Text.Json;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public async Task<WorkspaceView> SetNoteTaskCompletionAsync(Guid uid, Guid noteId, Guid taskId,
        NoteTaskCompletionInput input, CancellationToken ct)
    {
        await Mutate(uid, async () =>
        {
            var note = await Find<PlanningNote>(uid, noteId, ct);
            var ids = JsonSerializer.Deserialize<Guid[]>(note.CreatedTaskIdsJson, NoteJson)!;
            Require(ids.Contains(taskId), "NOT_FOUND", "Công việc không thuộc ghi chú này.", 404);
            var task = await Find<WorkTask>(uid, taskId, ct);
            Require(!task.IsDeleted, "NOT_FOUND", "Công việc đã xóa.", 404);
            Require(input.Version != Guid.Empty && input.Version == task.Version, "TASK_STALE",
                "Công việc đã thay đổi ở nơi khác. Tải lại danh sách trước khi đánh dấu.", 409);
            Require(task.Status != WorkTaskStatus.Cancelled, "TASK_CANCELLED",
                "Công việc đã hủy. Mở Công việc để khôi phục trước khi đánh dấu.", 409);
            if ((task.Status == WorkTaskStatus.Completed) == input.Completed) return;
            // Share normal status rules (active sessions, children and dependencies).
            // This does not write the note: an autosave may run independently.
            await SetTaskStatusCore(uid, taskId, new(input.Completed ? "Completed" : "Todo", input.Completed ? 100 : 0), ct);
        }, ct);
        // Return committed task versions and calendar state, including cancelled
        // future sessions; the browser must not guess them from the checkbox.
        return await GetAsync(uid, ct);
    }
}
