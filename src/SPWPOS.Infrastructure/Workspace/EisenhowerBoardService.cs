using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static TaskChecklistItem[] ReadTaskChecklist(WorkTask task) =>
        JsonSerializer.Deserialize<TaskChecklistItem[]>(task.ChecklistJson, AgentJson) ?? [];
    private static TaskChecklistItem[] ValidateTaskChecklist(TaskChecklistItem[]? items)
    {
        Require(items is { Length: <= 100 } && items.All(i => i is not null && i.Id != Guid.Empty &&
            !string.IsNullOrWhiteSpace(i.Text) && i.Text.Trim().Length <= 300) && items.Select(i => i.Id).Distinct().Count() == items.Length,
            "INVALID_CHECKLIST", "Checklist tối đa 100 mục, mỗi mục 1–300 ký tự và có mã riêng.");
        return items.Select(i => i with { Text = i.Text.Trim() }).ToArray();
    }
    private static void SetMatrixQuadrant(WorkTask task, string quadrant, DateTimeOffset now)
    {
        Require(quadrant is "Auto" or "Q1" or "Q2" or "Q3" or "Q4", "INVALID_QUADRANT", "Chọn một trong bốn ô hoặc tự đánh giá.");
        task.UrgencyOverride = quadrant == "Auto" ? "Auto" : quadrant is "Q1" or "Q3" ? "Urgent" : "NotUrgent";
        if (quadrant != "Auto") WriteImportance(task, new(quadrant is "Q1" or "Q2" ? "Important" : "Lower", "User",
            "Bạn chọn mức quan trọng trực tiếp trên ma trận.", null, true), now, "User");
    }
    private async Task<WorkTask> MatrixTask(Guid uid, Guid id, Guid version, CancellationToken ct)
    {
        Require(eisenhowerOptions?.Value.Enabled == true, "FEATURE_DISABLED", "Ma trận chưa được bật.", 409);
        var task = await Find<WorkTask>(uid, id, ct);
        Require(!task.IsDeleted && task.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled,
            "NOT_FOUND", "Công việc đã kết thúc hoặc đã xóa.", 404);
        Require(version != Guid.Empty && task.Version == version, "TASK_STALE", "Task vừa thay đổi. Tải lại trước khi chỉnh sửa.", 409);
        return task;
    }
    public Task<Guid> SaveMatrixTaskAsync(Guid uid, Guid? id, MatrixTaskInput input, CancellationToken ct) => Mutate(uid, async () => {
        Require(eisenhowerOptions?.Value.Enabled == true, "FEATURE_DISABLED", "Ma trận chưa được bật.", 409);
        var checklist = ValidateTaskChecklist(input.Checklist ?? []);
        WorkTask? original = id is { } tid ? await MatrixTask(uid, tid, input.Version, ct) : null;
        if (original != null)
            Require(!await db.TeamTaskAssignments.AnyAsync(a => a.WorkTaskId == original.Id && a.Active, ct),
                "TEAM_CONTRACT_PROTECTED", "Nội dung task nhóm do trưởng nhóm quản lý. Bạn vẫn có thể dùng checklist cá nhân.", 409);
        var taskId = await SaveTaskCoreAsync(uid, id, new(input.Name, input.Description, input.Deadline, input.EstimatedMinutes,
            original?.Priority ?? 2, original?.TaskType ?? "OTHER", input.Splittable,
            original?.MinimumSessionMinutes ?? Math.Min(25, Math.Max(5, input.EstimatedMinutes)),
            original?.CategoryId, original?.ParentId, original?.IsHardDeadline ?? true, original?.CreationSource ?? "MANUAL", original?.Version), ct);
        var task = db.WorkTasks.Local.Single(t => t.Id == taskId);
        SetMatrixQuadrant(task, input.Quadrant, Now);
        task.ChecklistJson = JsonSerializer.Serialize(checklist, AgentJson);
        if (original is null) task.MatrixOrder = (await Mine<WorkTask>(uid).Select(t => (long?)t.MatrixOrder).MaxAsync(ct) ?? 0) + 1024;
        task.Version = Guid.NewGuid(); task.UpdatedAt = Now;
        (await Preference(uid, ct)).Revision = Guid.NewGuid();
        return taskId;
    }, ct, false);

    private async Task SaveTaskChecklist(Guid uid, WorkTask task, MatrixChecklistInput input, CancellationToken ct)
    {
        task.ChecklistJson = JsonSerializer.Serialize(ValidateTaskChecklist(input.Items), AgentJson);
        task.Version = Guid.NewGuid(); task.UpdatedAt = Now;
        (await Preference(uid, ct)).Revision = Guid.NewGuid();
    }

    public Task UpdateMatrixChecklistAsync(Guid uid, Guid id, MatrixChecklistInput input, CancellationToken ct) => Mutate(uid, async () => {
        var task = await MatrixTask(uid, id, input.Version, ct);
        await SaveTaskChecklist(uid, task, input, ct);
    }, ct, false);

    public Task UpdateTaskChecklistAsync(Guid uid, Guid id, MatrixChecklistInput input, CancellationToken ct) => Mutate(uid, async () => {
        var task = await Find<WorkTask>(uid, id, ct);
        Require(!task.IsDeleted, "NOT_FOUND", "Không tìm thấy công việc.", 404);
        Require(input.Version != Guid.Empty && task.Version == input.Version,
            "TASK_STALE", "Công việc vừa thay đổi. Mở lại trước khi chỉnh sửa.", 409);
        var shared = await (from assignment in db.TeamTaskAssignments
            join teamTask in db.TeamTasks on assignment.TeamTaskId equals teamTask.Id
            where assignment.WorkTaskId == id && assignment.Active && assignment.UserId == uid
            select teamTask).SingleOrDefaultAsync(ct);
        if (shared is not null)
        {
            Require(shared.AssigneeId == uid && shared.Status == "Assigned",
                "TASK_STALE", "Công việc đã thay đổi hoặc đang chờ duyệt. Hãy tải lại.", 409);
            var requirements = TeamRequirements(shared.Id, shared.Description);
            var items = ValidateTaskChecklist(input.Items);
            var requiredIds = requirements.Select(item => item.Id).ToHashSet();
            await SaveTeamRequirementProgress(uid, task, requirements, items.Where(item => requiredIds.Contains(item.Id)).ToArray(), ct);
            return;
        }
        await SaveTaskChecklist(uid, task, input, ct);
    }, ct, false);

    public Task MoveMatrixTaskAsync(Guid uid, Guid id, MatrixMoveInput input, CancellationToken ct) => Mutate(uid, async () => {
        var task = await MatrixTask(uid, id, input.Version, ct);
        var preference = await Preference(uid, ct);
        Require(preference.Revision == input.Revision, "MATRIX_STALE", "Ma trận hoặc lịch vừa thay đổi. Tải lại để kéo tiếp.", 409);
        Require(input.Quadrant is "Auto" or "Q1" or "Q2" or "Q3" or "Q4" or "Unknown", "INVALID_QUADRANT", "Ô nhận task không hợp lệ.");
        var (planning, map, _) = await EisenhowerState(uid, ct);
        var rows = engine.AssessEisenhower(planning, Now);
        Require(rows.Any(t => t.Id == id), "INVALID_TASK", "Chỉ di chuyển task đang cần thực hiện.", 409);
        SetMatrixQuadrant(task, input.Quadrant is "Unknown" ? "Auto" : input.Quadrant, Now);
        var destination = engine.AssessEisenhower(planning, Now).Single(t => t.Id == id).Quadrant;
        var ordered = planning.Tasks.Where(t => t.Id != id && (!map.TryGetValue(t.Id, out var link) || link.Kind == "Accepted") && rows.Any(r => r.Id == t.Id && r.Quadrant == destination))
            .OrderBy(t => t.MatrixOrder).ThenBy(t => t.Deadline).ThenBy(t => t.Id).ToList();
        Require(input.BeforeId is null || ordered.Any(t => t.Id == input.BeforeId), "MATRIX_STALE", "Vị trí thả vừa thay đổi. Tải lại ma trận.", 409);
        ordered.Insert(input.BeforeId is { } before ? ordered.FindIndex(t => t.Id == before) : ordered.Count, task);
        for (var index = 0; index < ordered.Count; index++) ordered[index].MatrixOrder = (index + 1L) * 1024;
        task.Version = Guid.NewGuid(); task.UpdatedAt = Now;
        preference.Revision = Guid.NewGuid();
        // No change to duration, deadline, progress or saved calendar sessions.
    }, ct, false);

    public Task<PlanView> SuggestMatrixScheduleAsync(Guid uid, Guid id, MatrixScheduleInput input, CancellationToken ct) => Mutate(uid, async () => {
        var task = await MatrixTask(uid, id, input.Version, ct);
        var preference = await Preference(uid, ct);
        Require(preference.Revision == input.Revision, "MATRIX_STALE", "Lịch vừa thay đổi. Xem lại ma trận trước khi đề xuất.", 409);
        await ValidateExtraTimeTask(uid, task, ct);
        Require(task.HasPlanningDetails, "TASK_PLANNING_DETAILS_REQUIRED", "Bổ sung hạn chót và thời gian dự kiến để xếp lịch.", 409);
        var row = await GetTaskEisenhowerAsync(uid, id, false, ct);
        Require(row.Quadrant is "Q1" or "Q2" || input.ExplicitRequest, "SCHEDULE_ON_DEMAND",
            "Task này chỉ đề xuất lịch khi bạn chủ động yêu cầu.", 409);
        Require(task.Deadline > Now.AddMinutes(5), "DEADLINE_PASSED", "Deadline đã qua hoặc quá gần. Hãy cập nhật deadline trước khi đề xuất.", 409);
        var state = await State(uid, ct);
        state = await CalendarStateAsync(uid, state, Now, Now.AddDays(30), ct);
        var plan = engine.Generate(state, new(Now.AddMinutes(5), 30, [id], true, "MANUAL", false, false), Now);
        var proposal = new PlanProposal { UserId = uid, CreatedAt = Now, ExpiresAt = Now.AddMinutes(30), Revision = state.Preference.Revision,
            Kind = "RESCHEDULE", TriggerEvent = "MANUAL", HorizonStart = plan.Start, HorizonEnd = plan.End };
        plan = plan with { Id = proposal.Id, Kind = proposal.Kind };
        proposal.PayloadJson = JsonSerializer.Serialize(plan); db.PlanProposals.Add(proposal);
        StorePlanDecisions(uid, proposal, plan, state);
        return plan;
    }, ct, false);
}
