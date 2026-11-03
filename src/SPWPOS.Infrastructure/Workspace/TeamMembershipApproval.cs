using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private Task<TeamJoinRequestView[]> PendingTeamJoinRequests(Guid teamId, CancellationToken ct) =>
        (from m in db.TeamMemberships.AsNoTracking() join u in db.Users.AsNoTracking() on m.UserId equals u.Id
         where m.TeamId == teamId && !m.Active && m.ApprovalStatus == "Pending"
         orderby m.RequestedAt
         select new TeamJoinRequestView(m.UserId, u.FirstName + " " + u.LastName, u.Email!, m.Version, m.RequestedAt)).ToArrayAsync(ct);

    public Task<MyTeamJoinRequestView[]> ListMyTeamJoinRequestsAsync(Guid uid, CancellationToken ct) =>
        (from m in db.TeamMemberships.AsNoTracking() join t in db.TeamProjects.AsNoTracking() on m.TeamId equals t.Id
         where m.UserId == uid && !m.Active && (m.ApprovalStatus == "Pending" || m.ApprovalStatus == "Rejected" || m.ApprovalStatus == "Removed")
         orderby m.RequestedAt descending
         select new MyTeamJoinRequestView(m.TeamId, t.Name, m.ApprovalStatus, m.RequestedAt)).ToArrayAsync(ct);

    public Task ReviewTeamJoinRequestAsync(Guid uid, Guid teamId, Guid memberId, TeamJoinReviewInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var leader = await TeamMember(uid, teamId, ct, true);
        var request = await db.TeamMemberships.SingleOrDefaultAsync(x => x.TeamId == teamId && x.UserId == memberId, ct);
        Require(request != null && !request.Active && request.ApprovalStatus == "Pending" && request.Version == input.Version,
            "JOIN_REQUEST_STALE", "Yêu cầu đã được xử lý hoặc thay đổi. Hãy tải lại nhóm.", 409);
        if (input.Approved)
        {
            Require(await db.Users.AnyAsync(x => x.Id == memberId && x.IsActive && x.EmailConfirmed, ct),
                "MEMBER_UNAVAILABLE", "Tài khoản này không còn khả dụng.", 409);
            Require(await db.TeamInvites.AnyAsync(x => x.Id == request.JoinInviteId && x.TeamId == teamId && !x.Revoked && x.ExpiresAt > Now, ct),
                "INVITE_INVALID", "Lời mời đã hết hạn hoặc bị thu hồi. Hãy từ chối yêu cầu và gửi lời mời mới.", 409);
        }
        request.Active = input.Approved; request.ApprovalStatus = input.Approved ? "Approved" : "Rejected";
        request.Version = Guid.NewGuid(); request.MutationVersion = Guid.NewGuid(); leader.MutationVersion = Guid.NewGuid();
        TeamLog(uid, teamId, null, input.Approved ? "MEMBER_JOIN_APPROVED" : "MEMBER_JOIN_REJECTED");
        var teamName = await db.TeamProjects.Where(x => x.Id == teamId).Select(x => x.Name).SingleAsync(ct);
        await AddNotification(memberId, "TEAM_JOIN_REVIEWED", input.Approved ? "Yêu cầu tham gia đã được duyệt" : "Yêu cầu tham gia đã bị từ chối",
            input.Approved ? $"Bạn đã được duyệt vào nhóm {teamName}. Mở nhóm để xem công việc và chat chung." : $"Trưởng nhóm {teamName} đã từ chối yêu cầu tham gia của bạn.",
            $"team-join-review:{request.Id}:{request.Version}", null, ct);
        return true;
    }, ct, false, System.Data.IsolationLevel.Serializable);

    private async Task KickTeamMember(TeamMembership leader, TeamMembership target, CancellationToken ct)
    {
        // Personal writers claim their preference before membership. Use the
        // same order so kicking cannot deadlock a simultaneous chat/claim.
        var preference = await Preference(target.UserId, ct);
        preference.Revision = Guid.NewGuid(); preference.MutationVersion = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        target.Active = false; target.ApprovalStatus = "Removed";
        target.Version = Guid.NewGuid(); target.MutationVersion = Guid.NewGuid(); leader.MutationVersion = Guid.NewGuid();
        // Claim both membership versions first. Every team writer uses the same
        // target mutation token, so an in-flight claim cannot survive removal.
        await db.SaveChangesAsync(ct);
        var tasks = await db.TeamTasks.Where(x => x.TeamId == target.TeamId && (x.AssigneeId == target.UserId || x.DesignatedUserId == target.UserId)).ToArrayAsync(ct);
        var ids = tasks.Select(x => x.Id).ToArray();
        var assignments = await db.TeamTaskAssignments.Where(x => ids.Contains(x.TeamTaskId) && x.UserId == target.UserId && x.Active).ToArrayAsync(ct);
        foreach (var assignment in assignments) { assignment.Active = false; assignment.Version = Guid.NewGuid(); }
        foreach (var task in tasks)
        {
            if (task.DesignatedUserId == target.UserId) task.DesignatedUserId = null;
            if (task.AssigneeId == target.UserId && task.Status != "Completed")
            {
                task.AssigneeId = null; task.Status = "Open"; task.Progress = 0; task.PendingChangeJson = null;
                task.CompletionRequestId = null; task.CompletionApproved = null; task.CompletionSubmittedAt = null;
                task.CompletionReviewedAt = null; task.CompletionReviewedBy = null; task.CompletionReviewNote = null; task.ProgressBeforeReview = 0;
            }
            task.Version = Guid.NewGuid();
        }
        var claims = await db.TeamClaimProposals.Where(x => x.UserId == target.UserId && x.Status == "Proposed" && db.TeamTasks.Any(t => t.Id == x.TeamTaskId && t.TeamId == target.TeamId)).ToArrayAsync(ct);
        foreach (var claim in claims) claim.Status = "Cancelled";
        // Detach group ownership while retaining the former member's private
        // tasks, calendar, execution history, and completed group history.
        TeamLog(leader.UserId, target.TeamId, null, "MEMBER_REMOVED");
        await AddNotification(target.UserId, "TEAM_MEMBER_REMOVED", "Bạn đã được đưa ra khỏi nhóm",
            "Bạn không còn quyền truy cập nhóm. Công việc chưa hoàn tất đã trả về nhóm; các bản sao công việc và lịch cá nhân của bạn được giữ nguyên.",
            $"team-member-removed:{target.Id}:{target.Version}", null, ct);
    }
}
