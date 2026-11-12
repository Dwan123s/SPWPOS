using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static TaskChecklistItem[] TeamRequirements(Guid taskId, string description)
    {
        var requirements = new List<string>();
        var plain = new List<string>();
        var hasRequirementHeading = false;
        var inRequirements = false;
        var inOtherSection = false;
        foreach (var raw in description.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var listItem = Regex.Match(raw, @"^(?:(?:[-*•●▪]|\d+[.)])\s+)(?:\[[ xX]\]\s*)?(?<text>.+)$");
            if (listItem.Success)
            {
                var item = listItem.Groups["text"].Value.Trim();
                if (inRequirements) requirements.Add(item);
                else if (!inOtherSection) plain.Add(item);
                continue;
            }
            var line = raw.TrimStart('#').Trim().Replace("**", "");
            var colon = line.IndexOfAny([':', '：']);
            var heading = MatchText(colon < 0 ? line : line[..colon]).Trim();
            if (heading is "yeu cau can thuc hien" or "yeu cau" or "viec can thuc hien" or "cong viec can lam" or "requirements")
            {
                hasRequirementHeading = inRequirements = true;
                if (colon >= 0 && line[(colon + 1)..].Trim() is { Length: > 0 } inline) requirements.Add(inline);
                continue;
            }
            if (raw.StartsWith('#') || heading is "tom tat" or "tong quan" or "summary" or "dau ra can ban giao" or "dau ra"
                or "deliverables" or "tieu chi hoan thanh" or "acceptance criteria" or "ghi chu & phoi hop" or "ghi chu va phoi hop"
                or "ghi chu" or "notes" or "tham chieu tai lieu" or "source references" or "can cu deadline" or "tai lieu nguon")
            {
                inRequirements = false;
                inOtherSection = true;
                continue;
            }
            if (line.Length == 0) continue;
            if (inRequirements) requirements.Add(line);
            else if (!inOtherSection) plain.Add(line);
        }
        return (hasRequirementHeading ? requirements : plain).Distinct(StringComparer.OrdinalIgnoreCase).Take(100)
            .Select(text => new TaskChecklistItem(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{taskId:N}:{text}")).AsSpan(0, 16)),
                text.Length <= 300 ? text : text[..299] + "…")).ToArray();
    }

    private static TaskChecklistItem[] CheckedTeamRequirements(TaskChecklistItem[] requirements, TaskChecklistItem[] stored)
    {
        var checkedIds = stored.Where(item => item.Completed).Select(item => item.Id).ToHashSet();
        return requirements.Select(item => item with { Completed = checkedIds.Contains(item.Id) }).ToArray();
    }

    public Task<Guid[]> SplitTeamTaskAsync(Guid uid, Guid taskId, TeamTaskSplitInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var parent = await SharedTask(uid, taskId, ct);
        var leader = await TeamMember(uid, parent.TeamId, ct, true);
        Require(parent.Version == input.Version, "TASK_STALE", "Công việc vừa thay đổi. Mở lại trước khi chia việc.", 409);
        Require(parent.Status == "Open" && parent.AssigneeId is null && parent.ParentTaskId is null,
            "TEAM_SPLIT_UNAVAILABLE", "Chỉ chia công việc lớn đang chờ nhận.", 409);
        ValidateTeamDeadline(parent.Deadline);
        Require(input.Children is { Length: 2 } && input.Children.All(child => child is not null),
            "INVALID_TEAM_SUBTASKS", "Nhập đúng hai công việc con.");
        Require(await db.TeamTasks.CountAsync(x => x.TeamId == parent.TeamId && x.Status != "Completed" && x.Status != "Split", ct) < 500,
            "TEAM_TASK_LIMIT", "Nhóm đã có quá nhiều công việc đang mở.");

        var children = new List<TeamTask>(2);
        foreach (var inputTask in input.Children)
        {
            Require(inputTask.Requirements is { Length: <= 100 } && inputTask.Requirements.All(text =>
                !string.IsNullOrWhiteSpace(text) && text.Trim().Length <= 300 && !text.Contains('\n') && !text.Contains('\r')),
                "INVALID_CHECKLIST", "Checklist tối đa 100 dòng, mỗi dòng từ 1 đến 300 ký tự.");
            var requirements = string.Join("\n", inputTask.Requirements.Select(text => "- " + text.Trim()));
            var description = $"Yêu cầu cần thực hiện\n{requirements}\n\nGhi chú & phối hợp\n{inputTask.Notes?.Trim()}".Trim();
            ValidateManualTeamTask(new(inputTask.Name, description, inputTask.EstimatedMinutes, parent.Deadline, parent.Priority));
            await ValidateDesignatedMember(parent.TeamId, inputTask.DesignatedUserId, ct);
            var child = new TeamTask
            {
                TeamId = parent.TeamId, ParentTaskId = parent.Id, Name = inputTask.Name.Trim(), Description = description,
                EstimatedMinutes = inputTask.EstimatedMinutes, Deadline = parent.Deadline, Priority = parent.Priority,
                Splittable = parent.Splittable, SourceDocumentId = parent.SourceDocumentId, DesignatedUserId = inputTask.DesignatedUserId
            };
            WriteImportance(child, ImportanceDto(parent), Now, "Leader");
            children.Add(child);
            db.TeamTasks.Add(child);
            await NotifyDesignatedMember(child, ct);
            TeamLog(uid, parent.TeamId, child.Id, "TASK_CREATED");
        }
        parent.Status = "Split";
        parent.DesignatedUserId = null;
        parent.Version = Guid.NewGuid();
        leader.MutationVersion = Guid.NewGuid();
        TeamLog(uid, parent.TeamId, parent.Id, "TASK_SPLIT");
        return children.Select(task => task.Id).ToArray();
    }, ct, false, System.Data.IsolationLevel.Serializable);

    public Task SetTeamChecklistItemAsync(Guid uid, Guid taskId, Guid itemId, TeamChecklistToggleInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var task = await SharedTask(uid, taskId, ct);
        Require(task.AssigneeId == uid, "TEAM_FORBIDDEN", "Chỉ người nhận mới có thể tick checklist.", 403);
        Require(task.Status == "Assigned" && task.Version == input.Version,
            "TASK_STALE", "Công việc đã thay đổi hoặc đang chờ duyệt. Hãy tải lại.", 409);
        var requirements = TeamRequirements(task.Id, task.Description);
        Require(requirements.Any(item => item.Id == itemId), "NOT_FOUND", "Không tìm thấy mục checklist.", 404);
        var assignment = await db.TeamTaskAssignments.SingleAsync(x => x.TeamTaskId == task.Id && x.UserId == uid && x.Active, ct);
        var personal = await Find<WorkTask>(uid, assignment.WorkTaskId, ct);
        var current = CheckedTeamRequirements(requirements, ReadTaskChecklist(personal));
        var updated = current.Select(item => item.Id == itemId ? item with { Completed = input.Completed } : item).ToArray();
        await SaveTeamRequirementProgress(uid, personal, requirements, updated, ct);
    }, ct, false);

    private async Task SaveTeamRequirementProgress(Guid uid, WorkTask personal, TaskChecklistItem[] requirements, TaskChecklistItem[] input, CancellationToken ct)
    {
        var stored = ReadTaskChecklist(personal);
        var requiredIds = requirements.Select(item => item.Id).ToHashSet();
        Require(requiredIds.SetEquals(input.Select(item => item.Id)), "TASK_STALE", "Danh sách yêu cầu đã thay đổi. Mở lại công việc.", 409);
        var updated = CheckedTeamRequirements(requirements, input).Concat(stored.Where(item => !requiredIds.Contains(item.Id))).ToArray();
        if (updated.SequenceEqual(stored)) return;
        personal.ChecklistJson = JsonSerializer.Serialize(updated, AgentJson);
        personal.Version = Guid.NewGuid();
        personal.UpdatedAt = Now;
        (await Preference(uid, ct)).Revision = Guid.NewGuid();
    }
}
