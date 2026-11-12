using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public async Task<TaskBatchPreview> PreviewTaskBatchAsync(Guid uid, TeamBriefInput input, CancellationToken ct)
    {
        Require(input.File is not null || !string.IsNullOrWhiteSpace(input.Text),
            "TASK_BATCH_EMPTY", "Nhập danh sách việc hoặc chọn một tài liệu.");
        Require(interpreter is ITeamBriefInterpreter && interpreter.Capabilities.AiAvailable,
            "AI_NOT_CONFIGURED", "NORA chưa khả dụng trên máy chủ.", 503);
        var document = input.File is null ? null : TeamBriefReader.Read(input.File);
        var zone = (await State(uid, ct)).Preference.TimeZoneId;
        var context = new TeamBriefContext(1, zone, Now, document?.Name, input.Text ?? "",
            document?.Text, document?.PdfData, Personal: true);
        var result = await RunJob(uid, "AI_REQUEST",
            () => ((ITeamBriefInterpreter)interpreter).AnalyzeTeamBriefAsync(context, ct), ct);
        Require(result.Tasks.Length <= 12, "TASK_BATCH_LIMIT", "Chia tài liệu thành các phần nhỏ hơn để phân tích.");
        var project = document is null ? "Công việc từ NORA" : Path.GetFileNameWithoutExtension(document.Name);
        var tasks = result.Tasks.Select(item =>
        {
            var steps = item.Requirements.Concat(item.AcceptanceCriteria).Select(step => step.Trim())
                .Where(step => step.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var checklist = steps.Where(step => step.Length <= 300).Take(100)
                .Select(step => new TaskChecklistItem(Guid.NewGuid(), step)).ToArray();
            var checklistTexts = checklist.Select(step => step.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var description = BriefDescription(item with
            {
                Requirements = item.Requirements.Where(step => !checklistTexts.Contains(step.Trim())).ToArray(),
                AcceptanceCriteria = item.AcceptanceCriteria.Where(step => !checklistTexts.Contains(step.Trim())).ToArray()
            }, document?.Name);
            return new TaskBatchTaskInput(Limit(item.Name.Trim(), 200), Limit(description, 4000),
                item.Deadline > Now ? item.Deadline : null,
                item.EstimatedMinutes is >= 1 and <= 43200 ? item.EstimatedMinutes : null, checklist);
        }).ToArray();
        var warnings = result.Warnings.Append(result.Question).Append(result.SplitReason)
            .Where(message => !string.IsNullOrWhiteSpace(message)).Distinct().ToArray();
        return new TaskBatchPreview(project, result.Summary, tasks, warnings, result.ProjectDeadline > Now ? result.ProjectDeadline : null);
    }

    public Task<TaskBatchResult> SaveTaskBatchAsync(Guid uid, TaskBatchInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        Require(input.RequestId != Guid.Empty && !string.IsNullOrWhiteSpace(input.ProjectName) &&
            input.ProjectName.Trim().Length <= 200 && input.Tasks.Length is >= 1 and <= 12,
            "TASK_BATCH_INVALID", "Nhập tên project và giữ từ 1 đến 12 công việc.");
        var existing = await Mine<WorkTask>(uid).SingleOrDefaultAsync(task => task.Id == input.RequestId, ct);
        if (existing is not null)
        {
            Require(!existing.IsDeleted && existing.ParentId is null && existing.CreationSource == "NATURAL_LANGUAGE",
                "TASK_BATCH_STALE", "Project đã thay đổi. Mở lại form trước khi tạo project mới.", 409);
            var ids = await Mine<WorkTask>(uid).Where(task => task.ParentId == existing.Id && !task.IsDeleted)
                .Select(task => task.Id).ToArrayAsync(ct);
            Require(ids.Length > 0, "TASK_BATCH_STALE", "Project đã thay đổi. Kiểm tra danh sách công việc.", 409);
            return new TaskBatchResult(existing.Id, ids);
        }
        var activeCount = await Mine<WorkTask>(uid).CountAsync(task => !task.IsDeleted &&
            task.Status != WorkTaskStatus.Completed && task.Status != WorkTaskStatus.Cancelled, ct);
        Require(activeCount + input.Tasks.Length + 1 <= 500, "TASK_LIMIT", "Tối đa 500 công việc đang hoạt động.");

        var now = Now;
        Require(input.ProjectDeadline is null || input.ProjectDeadline > now,
            "TASK_BATCH_DEADLINE", "Hạn chót project phải ở tương lai.");
        var project = NewTask(uid, new TaskInput(input.ProjectName.Trim(), Deadline: input.ProjectDeadline,
            CreationSource: "NATURAL_LANGUAGE", IsHardDeadline: input.ProjectDeadline.HasValue), now);
        project.Id = input.RequestId;
        var tasks = input.Tasks.Select(item =>
        {
            var deadline = item.Deadline ?? input.ProjectDeadline;
            Require(input.ProjectDeadline is null || deadline <= input.ProjectDeadline,
                "TASK_BATCH_DEADLINE", "Hạn riêng của việc phải trước hoặc cùng hạn project.");
            var task = new TaskInput(item.Name, item.Description, deadline, item.EstimatedMinutes,
                ParentId: project.Id, CreationSource: "NATURAL_LANGUAGE", IsHardDeadline: deadline.HasValue);
            ValidTask(task, requirePlanningDetails: false);
            var child = NewTask(uid, task, now);
            child.ChecklistJson = JsonSerializer.Serialize(ValidateTaskChecklist(item.Checklist ?? []), AgentJson);
            return child;
        }).ToArray();
        db.WorkTasks.Add(project);
        db.WorkTasks.AddRange(tasks);
        if (input.Sequential)
            for (var index = 1; index < tasks.Length; index++)
                db.TaskDependencies.Add(new TaskDependency { UserId = uid, TaskId = tasks[index].Id,
                    PredecessorId = tasks[index - 1].Id, CreatedAt = now });
        return new TaskBatchResult(project.Id, tasks.Select(task => task.Id).ToArray());
    }, ct, isolation: System.Data.IsolationLevel.Serializable);

    private static string Limit(string text, int length) => text.Length <= length ? text : text[..length];
}
