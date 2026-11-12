using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public Task<Guid> SaveTaskAsync(Guid uid, Guid? id, TaskInput input, CancellationToken ct) => Mutate(uid, async () =>
        await SaveTaskCoreAsync(uid, id, input, ct), ct);
    private async Task<Guid> SaveTaskCoreAsync(Guid uid, Guid? id, TaskInput input, CancellationToken ct)
    {
        ValidTask(input, requirePlanningDetails: false);
        WorkTask? existingTask = null;
        if (id is { } existingId)
        {
            existingTask = await Find<WorkTask>(uid, existingId, ct);
            Require(!existingTask.IsDeleted, "NOT_FOUND", "Công việc đã xóa.", 404);
            Require(input.Version is { } version && version != Guid.Empty && version == existingTask.Version,
                "TASK_STALE", "Công việc đã thay đổi ở nơi khác. Tải lại bản mới trước khi chỉnh sửa.", 409);
        }
        var type = NormalizeTaskType(input.TaskType);
        Require(await db.TaskTypes.AnyAsync(x => x.Code == type && x.IsActive, ct), "INVALID_TASK_TYPE", "Loại công việc không hợp lệ.");
        if (input.CategoryId is { } categoryId) await Find<TaskCategory>(uid, categoryId, ct);
        var inheritsDeadline = false;
        if (input.ParentId is { } parentId)
        {
            var parent = await Find<WorkTask>(uid, parentId, ct);
            inheritsDeadline = input.InheritsProjectDeadline ??
                (existingTask?.InheritsProjectDeadline == true && existingTask.ParentId == parentId &&
                    (input.Deadline == existingTask.Deadline || input.Deadline is null));
            if (inheritsDeadline) input = input with { Deadline = parent.Deadline, IsHardDeadline = parent.IsHardDeadline };
            Require(parent.Deadline is null || input.Deadline is null || input.Deadline <= parent.Deadline,
                "SUBTASK_DEADLINE", "Deadline công việc con không được sau công việc cha.");
            Require(!parent.IsDeleted && parentId != id && parent.ParentId is null, "INVALID_PARENT", "Chỉ hỗ trợ một cấp công việc con, không tự tham chiếu.");
            Require(parent.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled, "INVALID_PARENT", "Công việc cha đã kết thúc.");
            Require(!await Mine<WorkSession>(uid).AnyAsync(x => x.TaskId == parentId && (x.Status == WorkSessionStatus.Running || x.Status == WorkSessionStatus.Paused || x.Status == WorkSessionStatus.Scheduled && x.IsLocked), ct),
                "LOCKED_SESSION_CONFLICT", "Kết thúc hoặc bỏ khóa phiên của công việc cha trước khi thêm việc con.", 409);
            foreach (var session in await Mine<WorkSession>(uid).Where(x => x.TaskId == parentId && x.Status == WorkSessionStatus.Scheduled).ToArrayAsync(ct)) session.Status = WorkSessionStatus.Cancelled;
            if (id is { } tid) Require(!await Mine<WorkTask>(uid).AnyAsync(x => x.ParentId == tid && !x.IsDeleted, ct), "INVALID_PARENT", "Công việc có việc con không thể trở thành việc con.");
        }
        WorkTask task;
        if (id is { } taskId)
        {
            task = existingTask!;
            if (task.Deadline != input.Deadline || task.IsHardDeadline != input.IsHardDeadline)
                await UpdateProjectDeadlineAsync(uid, taskId, input.Deadline, input.IsHardDeadline, ct);
            Require(!await Mine<WorkSession>(uid).AnyAsync(x => x.TaskId == taskId && x.IsLocked && x.Status == WorkSessionStatus.Scheduled && x.End > input.Deadline, ct),
                "LOCKED_SESSION_CONFLICT", "Deadline mới xung đột với phiên đã khóa.", 409);
            if (input.Deadline is null || input.EstimatedMinutes is null)
            {
                Require(!await db.TeamTaskAssignments.AnyAsync(x => x.UserId == uid && x.WorkTaskId == taskId && x.Active, ct),
                    "TASK_PLANNING_DETAILS_REQUIRED", "Công việc nhóm cần giữ hạn chót và thời gian dự kiến.", 409);
                var sessions = await Mine<WorkSession>(uid).Where(x => x.TaskId == taskId &&
                    (x.Status == WorkSessionStatus.Scheduled || x.Status == WorkSessionStatus.Running || x.Status == WorkSessionStatus.Paused)).ToArrayAsync(ct);
                Require(sessions.All(x => x.Status == WorkSessionStatus.Scheduled && !x.IsLocked),
                    "LOCKED_SESSION_CONFLICT", "Kết thúc phiên đang làm hoặc bỏ khóa trước khi bỏ hạn hay thời lượng.", 409);
                foreach (var session in sessions) session.Status = WorkSessionStatus.Cancelled;
            }
            task.Name = input.Name.Trim(); task.Description = input.Description ?? ""; task.Deadline = input.Deadline;
            task.InheritsProjectDeadline = inheritsDeadline;
            task.EstimatedMinutes = input.EstimatedMinutes ?? 0; task.Priority = input.Priority; task.TaskType = type; task.IsHardDeadline = input.IsHardDeadline;
            task.Splittable = ActivityContinuity.CanSplit(input.Name, input.TaskType, input.Splittable); task.MinimumSessionMinutes = input.MinimumSessionMinutes; task.CategoryId = input.CategoryId; task.ParentId = input.ParentId; task.UpdatedAt = Now;
            Require(task.WorkWindowStart is null || task.Deadline is null || task.Deadline > task.WorkWindowStart, "TASK_WORK_WINDOW", "Deadline mới phải sau thời điểm bắt đầu khung thực hiện đã lưu.", 409);
        }
        else
        {
            Require(await Mine<WorkTask>(uid).CountAsync(x => !x.IsDeleted && x.Status != WorkTaskStatus.Completed && x.Status != WorkTaskStatus.Cancelled, ct) < 500,
                "TASK_LIMIT", "Tối đa 500 công việc đang hoạt động.");
            task = NewTask(uid, input with { InheritsProjectDeadline = inheritsDeadline }, Now); db.WorkTasks.Add(task);
            if (input.StartDate is { } startDate)
            {
                var zone = PlanningEngine.Zone((await Preference(uid, ct)).TimeZoneId);
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Now, zone).DateTime);
                Require(startDate >= today && startDate <= today.AddDays(31),
                    "TASK_WORK_WINDOW", "Ngày bắt đầu phải nằm trong 31 ngày tới.");
                var startOfDay = PlanningEngine.At(startDate, 0, zone);
                Require(input.Deadline is null || input.Deadline > startOfDay,
                    "TASK_WORK_WINDOW", "Ngày bắt đầu phải trước hạn chót.");
                task.WorkWindowStart = startOfDay;
            }
            else if (input.WorkWindowStart is { } start)
            {
                Require(start >= Now && start <= Now.AddDays(31) && (input.Deadline is null || input.Deadline > start),
                    "TASK_WORK_WINDOW", "Giờ bắt đầu phải nằm trong 31 ngày tới và trước hạn chót.");
                task.WorkWindowStart = start;
                if (input.EstimatedMinutes is { } duration)
                    task.WorkWindowEnd = start.AddMinutes(duration);
            }
        }
        var tasks = await Mine<WorkTask>(uid).Where(x => !x.IsDeleted).ToArrayAsync(ct);
        var edges = await Mine<TaskDependency>(uid).ToArrayAsync(ct);
        Require(!PlanningEngine.HasCycle(tasks.Where(x => x.Id != task.Id).Append(task), edges),
            "DEPENDENCY_CYCLE", "Quan hệ cha/con và phụ thuộc tạo thành vòng lặp.", 409);
        return task.Id;
    }

    private async Task UpdateProjectDeadlineAsync(Guid uid, Guid projectId, DateTimeOffset? deadline, bool hardDeadline, CancellationToken ct)
    {
        var children = await Mine<WorkTask>(uid).Where(x => x.ParentId == projectId && !x.IsDeleted).ToArrayAsync(ct);
        Require(deadline is null || !children.Any(x => !x.InheritsProjectDeadline && x.Deadline > deadline),
            "SUBTASK_DEADLINE", "Có việc con đặt hạn riêng sau hạn project mới. Hãy sửa hạn của việc đó trước.");
        var inherited = children.Where(x => x.InheritsProjectDeadline && ActiveWork(x)).ToArray();
        if (inherited.Length == 0) return;
        var ids = inherited.Select(x => x.Id).ToArray();
        var conflicts = await Mine<WorkSession>(uid).Where(x => ids.Contains(x.TaskId) &&
            (x.Status == WorkSessionStatus.Scheduled || x.Status == WorkSessionStatus.Running || x.Status == WorkSessionStatus.Paused) &&
            (deadline == null || hardDeadline && x.End > deadline)).ToArrayAsync(ct);
        Require(conflicts.All(x => x.Status == WorkSessionStatus.Scheduled && !x.IsLocked && x.Start >= Now),
            "LOCKED_SESSION_CONFLICT", "Hạn project mới xung đột với phiên đã khóa hoặc đang thực hiện.", 409);
        foreach (var child in inherited)
        {
            Require(child.WorkWindowStart is null || deadline is null || deadline > child.WorkWindowStart,
                "TASK_WORK_WINDOW", $"Hạn project mới phải sau thời điểm bắt đầu của “{child.Name}”.", 409);
            child.Deadline = deadline;
            child.IsHardDeadline = deadline.HasValue && hardDeadline;
            child.UpdatedAt = Now;
        }
        foreach (var session in conflicts) session.Status = WorkSessionStatus.Cancelled;
    }
    public Task DeleteTaskAsync(Guid uid, Guid id, CancellationToken ct) => Mutate(uid, async () =>
    {
        var task = await Find<WorkTask>(uid, id, ct); var ids = await Mine<WorkTask>(uid).Where(x => x.ParentId == id && !x.IsDeleted).Select(x => x.Id).ToListAsync(ct); ids.Add(id);
        Require(!await Mine<WorkSession>(uid).AnyAsync(x => ids.Contains(x.TaskId) && (x.Status == WorkSessionStatus.Running || x.Status == WorkSessionStatus.Paused), ct),
            "SESSION_ACTIVE", "Hoàn thành hoặc bỏ qua phiên đang chạy trước khi xóa công việc.", 409);
        foreach (var item in await Mine<WorkTask>(uid).Where(x => ids.Contains(x.Id)).ToArrayAsync(ct)) { item.IsDeleted = true; item.DeletedAt = Now; item.UpdatedAt = Now; }
        foreach (var session in await Mine<WorkSession>(uid).Where(x => ids.Contains(x.TaskId) && x.Status == WorkSessionStatus.Scheduled).ToArrayAsync(ct)) session.Status = WorkSessionStatus.Cancelled;
        db.TaskDependencies.RemoveRange(await Mine<TaskDependency>(uid).Where(x => ids.Contains(x.TaskId) || ids.Contains(x.PredecessorId)).ToArrayAsync(ct));
        task.IsDeleted = true;
    }, ct);
    public Task SetStatusAsync(Guid uid, Guid id, StatusInput input, CancellationToken ct) =>
        Mutate(uid, () => SetTaskStatusCore(uid, id, input, ct), ct);
    private async Task SetTaskStatusCore(Guid uid, Guid id, StatusInput input, CancellationToken ct, TeamCompletionSubmitInput? submission = null)
    {
        Require(Enum.TryParse<WorkTaskStatus>(input.Status, true, out var status) && Enum.IsDefined(status), "INVALID_STATUS", "Trạng thái không hợp lệ.");
        var task = await Find<WorkTask>(uid, id, ct); Require(!task.IsDeleted, "NOT_FOUND", "Công việc đã xóa.", 404);
        var assignment = await db.TeamTaskAssignments.SingleOrDefaultAsync(x => x.UserId == uid && x.WorkTaskId == id && x.Active, ct);
        TeamTask? shared = null;
        if (assignment is not null)
        {
            shared = await SharedTask(uid, assignment.TeamTaskId, ct);
            Require(status != WorkTaskStatus.Cancelled, "TEAM_RETURN_REQUIRED", "Dùng Trả việc trong nhóm để ngừng nhận task này.", 409);
            if (input.Progress == 100) status = WorkTaskStatus.Completed;
            if (shared.Status is "PendingReview" or "Completed" && status == WorkTaskStatus.Completed) return;
            Require(shared.AssigneeId == uid && shared.Status == "Assigned", "TEAM_REVIEW_PENDING", "Task đang chờ trưởng nhóm duyệt hoặc đã được duyệt hoàn thành.", 409);
            Require(status != WorkTaskStatus.Completed || shared.PendingChangeJson == null, "TEAM_CHANGE_PENDING", "Xác nhận hoặc từ chối yêu cầu thay đổi trước khi báo hoàn thành.", 409);
        }
        Require(!await Mine<WorkSession>(uid).AnyAsync(x => x.TaskId == id && (x.Status == WorkSessionStatus.Running || x.Status == WorkSessionStatus.Paused), ct),
            "SESSION_ACTIVE", "Kết thúc phiên đang thực hiện trước khi thay đổi trạng thái.", 409);
        if (status == WorkTaskStatus.Completed)
        {
            Require(!await Mine<WorkTask>(uid).AnyAsync(x => x.ParentId == id && !x.IsDeleted && x.Status != WorkTaskStatus.Completed && x.Status != WorkTaskStatus.Cancelled, ct), "SUBTASK_INCOMPLETE", "Hoàn thành công việc con trước.");
            var state = await State(uid, ct);
            var completed = engine.CompletionTimes(state, [], engine.Biases(state), Now);
            Require(state.Dependencies.Where(x => x.TaskId == id).All(x => completed.ContainsKey(x.PredecessorId)),
                "DEPENDENCY_INCOMPLETE", "Hoàn thành công việc tiền nhiệm trước.");
        }
        task.Status = status; task.Progress = status == WorkTaskStatus.Completed ? 100 : input.Progress; task.UpdatedAt = Now; task.CompletedAt = status == WorkTaskStatus.Completed ? Now : null;
        if (shared is not null)
        {
            if (status == WorkTaskStatus.Completed) await SubmitTeamCompletion(uid, shared, ct, submission);
            else { shared.Progress = task.Progress; shared.Version = Guid.NewGuid(); TeamLog(uid, shared.TeamId, shared.Id, "PROGRESS_CONFIRMED"); }
            (await TeamMember(uid, shared.TeamId, ct)).MutationVersion = Guid.NewGuid();
        }
        else if (status == WorkTaskStatus.Completed) await AwardTaskExperienceAsync(uid, task, Now, ct);
        if (status is WorkTaskStatus.Completed or WorkTaskStatus.Cancelled)
            await ReleaseCompletedTaskSessions(uid,id,ct);
    }
    public Task<Guid> SaveCategoryAsync(Guid uid, Guid? id, CategoryInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var name = input.Name.Trim(); Require(name.Length > 0, "VALIDATION_ERROR", "Tên nhóm không được để trống.");
        Require(!await Mine<TaskCategory>(uid).AnyAsync(x => x.Id != id && x.Name.ToUpper() == name.ToUpper(), ct), "CATEGORY_EXISTS", "Tên nhóm đã tồn tại.", 409);
        var category = id is { } categoryId ? await Find<TaskCategory>(uid, categoryId, ct) : new TaskCategory { UserId = uid };
        category.Name = name; category.Color = input.Color; if (id is null) db.TaskCategories.Add(category); return category.Id;
    }, ct);
    public Task DeleteCategoryAsync(Guid uid, Guid id, CancellationToken ct) => Mutate(uid, async () =>
    {
        var category = await Find<TaskCategory>(uid, id, ct);
        foreach (var task in await Mine<WorkTask>(uid).Where(x => x.CategoryId == id).ToArrayAsync(ct)) task.CategoryId = null;
        db.TaskCategories.Remove(category);
    }, ct);
    public Task SetDependencyAsync(Guid uid, Guid id, Guid predecessor, bool remove, CancellationToken ct) => Mutate(uid, async () =>
    {
        var task = await Find<WorkTask>(uid, id, ct); var parent = await Find<WorkTask>(uid, predecessor, ct);
        Require(!task.IsDeleted && !parent.IsDeleted && id != predecessor, "INVALID_DEPENDENCY", "Phụ thuộc không hợp lệ.");
        var existing = await Mine<TaskDependency>(uid).SingleOrDefaultAsync(x => x.TaskId == id && x.PredecessorId == predecessor, ct);
        if (remove) { if (existing is not null) db.TaskDependencies.Remove(existing); return; }
        if (existing is not null) return;
        var edge = new TaskDependency { UserId = uid, TaskId = id, PredecessorId = predecessor, CreatedAt = Now };
        var tasks = await Mine<WorkTask>(uid).Where(x => !x.IsDeleted).ToArrayAsync(ct);
        var edges = await Mine<TaskDependency>(uid).ToArrayAsync(ct);
        Require(!PlanningEngine.HasCycle(tasks, edges.Append(edge)), "DEPENDENCY_CYCLE", "Phụ thuộc tạo thành vòng lặp.", 409);
        Require(!await Mine<WorkSession>(uid).AnyAsync(x => x.TaskId == id && (x.IsLocked || x.Status == WorkSessionStatus.Running || x.Status == WorkSessionStatus.Paused), ct),
            "LOCKED_SESSION_CONFLICT", "Bỏ khóa/kết thúc phiên trước khi thêm phụ thuộc.", 409);
        db.TaskDependencies.Add(edge);
    }, ct);
}
