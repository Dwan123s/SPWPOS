using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Commerce;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private IQueryable<Guid> TeamInviteFriendIds(Guid uid) => db.Friendships.AsNoTracking()
        .Where(f => f.Status == "Accepted" && (f.UserLowId == uid || f.UserHighId == uid))
        .Select(f => f.UserLowId == uid ? f.UserHighId : f.UserLowId);

    private static string FriendInviteStatus(TeamMembership? member, TeamFriendInvitation? invitation, DateTimeOffset now) =>
        member?.Active == true ? "Member" : member?.ApprovalStatus == "Removed" ? "Removed" :
        member?.ApprovalStatus == "Pending" ? "AwaitingApproval" :
        invitation?.Status == "Pending" && invitation.ExpiresAt > now ? "Invited" :
        invitation?.Status == "Declined" && invitation.CreatedAt > now.AddDays(-1) ? "Declined" : "Available";

    public async Task<TeamInviteFriendsPage> ListTeamInviteFriendsAsync(Guid uid, Guid teamId, string? search, int offset, CancellationToken ct)
    {
        await TeamMember(uid, teamId, ct, true);
        var term = (search ?? "").Trim();
        Require(term.Length <= 100 && offset is >= 0 and <= 10000, "VALIDATION_ERROR", "Tên tìm kiếm hoặc trang không hợp lệ.");
        var friends = TeamInviteFriendIds(uid);
        var query = from u in db.Users.AsNoTracking()
                    join p in db.UserProfiles.AsNoTracking() on u.Id equals p.UserId into profiles
                    from p in profiles.DefaultIfEmpty()
                    where friends.Contains(u.Id) && u.IsActive && u.EmailConfirmed
                    select new { u.Id, Name = p != null && p.DisplayName != "" ? p.DisplayName : (u.FirstName + " " + u.LastName).Trim(), AvatarUrl = p != null ? p.AvatarUrl : "" };
        if (term.Length > 0) query = query.Where(x => x.Name.ToUpper().Contains(term.ToUpper()));
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(offset).Take(25).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var members = await db.TeamMemberships.AsNoTracking().Where(x => x.TeamId == teamId && ids.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, ct);
        var invitations = await db.TeamFriendInvitations.AsNoTracking().Where(x => x.TeamId == teamId && ids.Contains(x.RecipientId)).ToDictionaryAsync(x => x.RecipientId, ct);
        return new(rows.Take(24).Select(x => {
            var invitation = invitations.GetValueOrDefault(x.Id);
            return new TeamInviteFriendView(x.Id, x.Name, x.AvatarUrl, FriendInviteStatus(members.GetValueOrDefault(x.Id), invitation, Now), invitation?.Id, invitation?.Version);
        }).ToArray(), rows.Length > 24 ? offset + 24 : null);
    }

    public Task<TeamInviteFriendView> InviteTeamFriendAsync(Guid uid, Guid teamId, Guid friendId, CancellationToken ct) => Mutate(uid, async () =>
    {
        await AccountTransaction.LockTeamAsync(db, teamId, ct);
        await TeamMember(uid, teamId, ct, true);
        Require(friendId != uid && await TeamInviteFriendIds(uid).ContainsAsync(friendId, ct), "NOT_FRIEND", "Chỉ có thể mời người đã kết bạn với bạn.", 403);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == friendId && x.IsActive && x.EmailConfirmed, ct);
        Require(user != null, "MEMBER_UNAVAILABLE", "Tài khoản này không còn khả dụng.", 409);
        var member = await db.TeamMemberships.SingleOrDefaultAsync(x => x.TeamId == teamId && x.UserId == friendId, ct);
        Require(member?.ApprovalStatus != "Removed", "MEMBER_REMOVED", "Người này đã bị loại khỏi nhóm và không thể nhận lời mời mới.", 409);
        var invitation = await db.TeamFriendInvitations.SingleOrDefaultAsync(x => x.TeamId == teamId && x.RecipientId == friendId, ct);
        var status = FriendInviteStatus(member, invitation, Now);
        if (status is "Member" or "AwaitingApproval" or "Invited")
            return new TeamInviteFriendView(friendId, (user.FirstName + " " + user.LastName).Trim(), "", status, invitation?.Id, invitation?.Version);
        // Declining an invitation must not immediately expose the recipient to another notification.
        Require(invitation?.Status != "Declined" || invitation.CreatedAt <= Now.AddDays(-1), "INVITE_COOLDOWN", "Hãy chờ 24 giờ từ lần mời trước trước khi gửi lại.", 409);
        invitation ??= new() { TeamId = teamId, RecipientId = friendId };
        if (db.Entry(invitation).State == EntityState.Detached) db.TeamFriendInvitations.Add(invitation);
        invitation.SenderId = uid; invitation.Status = "Pending"; invitation.CreatedAt = Now; invitation.ExpiresAt = Now.AddDays(7); invitation.Version = Guid.NewGuid();
        var teamName = await db.TeamProjects.Where(x => x.Id == teamId).Select(x => x.Name).SingleAsync(ct);
        await AddNotification(friendId, "TEAM_FRIEND_INVITE", "Bạn được mời tham gia nhóm", $"Trưởng nhóm {teamName} đã mời bạn. Mở lời mời để tham gia hoặc từ chối.",
            $"team-friend-invite:{invitation.Id}:{invitation.Version}", null, ct);
        return new TeamInviteFriendView(friendId, (user.FirstName + " " + user.LastName).Trim(), "", "Invited", invitation.Id, invitation.Version);
    }, ct, false);

    public async Task<TeamFriendInvitationView[]> ListTeamFriendInvitationsAsync(Guid uid, CancellationToken ct) => await
        (from i in db.TeamFriendInvitations.AsNoTracking()
         join t in db.TeamProjects.AsNoTracking() on i.TeamId equals t.Id
         join u in db.Users.AsNoTracking() on i.SenderId equals u.Id
         join leader in db.TeamMemberships.AsNoTracking() on new { i.TeamId, UserId = i.SenderId } equals new { leader.TeamId, leader.UserId }
         where i.RecipientId == uid && i.Status == "Pending" && i.ExpiresAt > Now && u.IsActive && u.EmailConfirmed && leader.Active && leader.Role == "Leader"
             && !db.TeamMemberships.Any(m => m.TeamId == i.TeamId && m.UserId == uid && (m.Active || m.ApprovalStatus == "Removed"))
         orderby i.CreatedAt descending
         select new TeamFriendInvitationView(i.Id, i.TeamId, t.Name, (u.FirstName + " " + u.LastName).Trim(), i.ExpiresAt, i.Version)).ToArrayAsync(ct);

    public Task<Guid> RespondTeamFriendInvitationAsync(Guid uid, Guid invitationId, TeamFriendInviteResponse input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var teamId = await db.TeamFriendInvitations.Where(x => x.Id == invitationId && x.RecipientId == uid).Select(x => (Guid?)x.TeamId).SingleOrDefaultAsync(ct);
        Require(teamId != null, "NOT_FOUND", "Không tìm thấy lời mời dành cho bạn.", 404);
        await AccountTransaction.LockTeamAsync(db, teamId.Value, ct);
        var invitation = await db.TeamFriendInvitations.SingleAsync(x => x.Id == invitationId, ct);
        if (invitation.Status == (input.Accepted ? "Accepted" : "Declined") && invitation.Version == input.Version) return invitation.TeamId;
        Require(invitation.Status == "Pending" && invitation.ExpiresAt > Now && invitation.Version == input.Version,
            "INVITE_STALE", "Lời mời đã được xử lý, hết hạn hoặc bị thu hồi. Hãy tải lại danh sách.", 409);
        if (input.Accepted)
        {
            await TeamMember(invitation.SenderId, invitation.TeamId, ct, true);
            Require(await db.Users.AnyAsync(x => x.Id == invitation.SenderId && x.IsActive && x.EmailConfirmed, ct), "INVITE_STALE", "Người mời không còn khả dụng.", 409);
            var member = await db.TeamMemberships.SingleOrDefaultAsync(x => x.TeamId == invitation.TeamId && x.UserId == uid, ct);
            Require(member?.ApprovalStatus != "Removed", "MEMBER_REMOVED", "Bạn đã bị loại khỏi nhóm này.", 403);
            if (member?.Active != true)
            {
                var plan = await TeamPlans.CurrentAsync(db, invitation.TeamId, Now, ct);
                Require(plan.MemberLimit is not int limit || await db.TeamMemberships.CountAsync(x => x.TeamId == invitation.TeamId && x.Active, ct) < limit,
                    "TEAM_MEMBER_LIMIT", "Nhóm đã đầy. Hãy liên hệ trưởng nhóm để có thêm chỗ; lời mời của bạn vẫn được giữ lại.", 409);
                member ??= new() { TeamId = invitation.TeamId, UserId = uid };
                if (db.Entry(member).State == EntityState.Detached) db.TeamMemberships.Add(member);
                member.Active = true; member.ApprovalStatus = "Approved"; member.Role = "Member"; member.JoinInviteId = null;
                member.Version = Guid.NewGuid(); member.MutationVersion = Guid.NewGuid();
                TeamLog(uid, invitation.TeamId, null, "MEMBER_JOINED");
            }
        }
        invitation.Status = input.Accepted ? "Accepted" : "Declined";
        // Keep the version for idempotent double-clicks; resend/revoke rotates it.
        return invitation.TeamId;
    }, ct, false);

    public Task RevokeTeamFriendInvitationAsync(Guid uid, Guid teamId, Guid invitationId, Guid version, CancellationToken ct) => Mutate(uid, async () =>
    {
        await AccountTransaction.LockTeamAsync(db, teamId, ct);
        await TeamMember(uid, teamId, ct, true);
        var invitation = await db.TeamFriendInvitations.SingleOrDefaultAsync(x => x.TeamId == teamId && x.Id == invitationId, ct);
        Require(invitation != null && invitation.Status == "Pending" && invitation.Version == version, "INVITE_STALE", "Lời mời đã thay đổi. Hãy tải lại danh sách.", 409);
        invitation.Status = "Revoked"; invitation.Version = Guid.NewGuid();
    }, ct, false);
}
