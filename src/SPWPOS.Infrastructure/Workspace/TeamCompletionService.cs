using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    // Called by both personal-task and shared-task progress APIs inside their transaction.
    private async Task SubmitTeamCompletion(Guid uid, TeamTask task, CancellationToken ct, TeamCompletionSubmitInput? submission = null)
    {
        task.ProgressBeforeReview = task.Progress;
        task.Progress = 100;
        task.Status = "PendingReview";
        task.CompletionRequestId = submission?.RequestId ?? Guid.NewGuid();
        task.CompletionSubmittedAt = Now;
        task.CompletionReviewedAt = null;
        task.CompletionReviewedBy = null;
        task.CompletionApproved = null;
        task.CompletionReviewNote = null;
        task.Version = Guid.NewGuid();
        db.TeamCompletionSubmissions.Add(new() { Id = task.CompletionRequestId.Value, TeamTaskId = task.Id, UserId = uid,
            Note = submission?.Note?.Trim() ?? "", CreatedAt = Now });
        TeamLog(uid, task.TeamId, task.Id, "TASK_COMPLETION_SUBMITTED");
        var name = await TeamUserName(uid, ct);
        foreach (var leader in await db.TeamMemberships.Where(x => x.TeamId == task.TeamId && x.Active && x.Role == "Leader").Select(x => x.UserId).ToArrayAsync(ct))
            await AddNotification(leader, "TEAM_COMPLETION_REQUEST", "Task nhóm đang chờ duyệt",
                $"{name} đã báo hoàn thành “{task.Name}”. Trưởng nhóm cần kiểm tra và xác nhận.",
                $"team-review:{task.CompletionRequestId}", null, ct);
    }

    private async Task<string> TeamUserName(Guid uid, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == uid, ct);
        return (user.FirstName + " " + user.LastName).Trim();
    }

    public Task ReviewTeamCompletionAsync(Guid uid, Guid taskId, TeamCompletionReviewInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var task = await SharedTask(uid, taskId, ct);
        var leader = await TeamMember(uid, task.TeamId, ct, true);
        Require(input.RequestId != Guid.Empty && input.Note?.Length is not > 500, "VALIDATION_ERROR", "Nhận xét tối đa 500 ký tự.");
        Require(task.CompletionRequestId == input.RequestId, "TASK_STALE", "Yêu cầu hoàn thành đã thay đổi. Tải lại task trước khi duyệt.", 409);
        // Retrying the same decision is harmless, even with the original version.
        if (task.CompletionReviewedAt != null && task.CompletionApproved == input.Approved) return;
        Require(task.Status == "PendingReview" && task.Version == input.Version && task.AssigneeId != null,
            "TASK_STALE", "Task đã thay đổi hoặc đã được duyệt. Hãy tải lại.", 409);
        var member = await TeamMember(task.AssigneeId!.Value, task.TeamId, ct);
        var assignment = await db.TeamTaskAssignments.SingleAsync(x => x.TeamTaskId == task.Id && x.UserId == member.UserId && x.Active, ct);
        if (!input.Approved)
        {
            // Invalidate the assignee's stale AI plans before reopening their personal task.
            var preference = await Preference(member.UserId, ct);
            preference.MutationVersion = Guid.NewGuid();
            await db.SaveChangesAsync(ct);
            var personal = await Find<WorkTask>(member.UserId, assignment.WorkTaskId, ct);
            personal.Status = WorkTaskStatus.InProgress;
            personal.Progress = task.ProgressBeforeReview;
            personal.CompletedAt = null;
            personal.UpdatedAt = Now;
            preference.Revision = Guid.NewGuid();
        }
        task.Status = input.Approved ? "Completed" : "Assigned";
        task.Progress = input.Approved ? 100 : task.ProgressBeforeReview;
        task.CompletionApproved = input.Approved;
        task.CompletionReviewedAt = Now;
        task.CompletionReviewedBy = uid;
        task.CompletionReviewNote = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        task.Version = Guid.NewGuid();
        leader.MutationVersion = Guid.NewGuid();
        member.MutationVersion = Guid.NewGuid();
        // Claim the version before creating any visible side effects; everything commits together.
        await db.SaveChangesAsync(ct);
        TeamLog(uid, task.TeamId, task.Id, input.Approved ? "TASK_COMPLETION_APPROVED" : "TASK_COMPLETION_REJECTED");
        var assigneeName = await TeamUserName(member.UserId, ct);
        var leaderName = await TeamUserName(uid, ct);
        if (input.Approved)
        {
            var preference = await Preference(member.UserId, ct);
            preference.MutationVersion = Guid.NewGuid();
            await db.SaveChangesAsync(ct);
            var personal = await Find<WorkTask>(member.UserId, assignment.WorkTaskId, ct);
            await AwardTaskExperienceAsync(member.UserId, personal, task.CompletionSubmittedAt ?? Now, ct);
            var message = $"{assigneeName} đã hoàn thành task “{task.Name}”. Đã được trưởng nhóm {leaderName} xác nhận.";
            db.TeamChatMessages.Add(new() { TeamId = task.TeamId, UserId = uid, RequestId = input.RequestId,
                Text = message, IsSystem = true, CreatedAt = Now });
            foreach (var recipient in await db.TeamMemberships.Where(x => x.TeamId == task.TeamId && x.Active).Select(x => x.UserId).ToArrayAsync(ct))
                await AddNotification(recipient, "TEAM_TASK_COMPLETED", "Task nhóm đã hoàn thành", message,
                    $"team-completed:{input.RequestId}", null, ct);
        }
        else
            await AddNotification(member.UserId, "TEAM_COMPLETION_REJECTED", "Task nhóm cần bổ sung",
                $"Trưởng nhóm {leaderName} yêu cầu bổ sung “{task.Name}”." + (task.CompletionReviewNote is { } note ? " " + note : " Hãy trao đổi trong chat nhóm và gửi duyệt lại khi xong."),
                $"team-rejected:{input.RequestId}", assignment.WorkTaskId, ct);
    }, ct, false);
}
