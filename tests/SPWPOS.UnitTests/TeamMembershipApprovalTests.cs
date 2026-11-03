using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact] public async Task Pending_applicants_have_no_group_access_until_a_leader_approves()
    {
        await using var h = await Setup(); var invite = await Use(h, s => s.CreateTeamInviteAsync(Leader, Team, Ct));
        await Use(h, s => s.JoinTeamAsync(Other, invite.Code, Ct));
        await Use(h, s => s.JoinTeamAsync(Other, invite.Code, Ct));
        var notice = Assert.Single(await Workspace(h, s => s.GetNotificationsAsync(Leader, Ct)), x => x.Kind == "TEAM_JOIN_REQUEST");
        Assert.Equal(Team, notice.TeamId);
        Assert.True(notice.JoinRequestPending);
        Assert.False(notice.IsRead);
        Assert.DoesNotContain(await Workspace(h, s => s.GetNotificationsAsync(Member, Ct)), x => x.Id == notice.Id);
        Assert.Empty(await Use(h, s => s.ListTeamsAsync(Other, Ct)));
        Assert.Equal("Pending", Assert.Single(await Use(h, s => s.ListMyTeamJoinRequestsAsync(Other, Ct))).Status);
        Assert.Equal("Pending", (await Use(h, s => s.PreviewTeamInviteAsync(Other, invite.Code, Ct))).RequestStatus);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.GetTeamAsync(Other, Team, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.GetTeamChatAsync(Other, Team, null, null, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.SendTeamChatAsync(Other, Team, new(Guid.NewGuid(), "Không được gửi"), Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.PreviewTeamClaimAsync(Other, TaskId, Ct)));
        var detail = await Use(h, s => s.GetTeamAsync(Leader, Team, Ct)); var request = Assert.Single(detail.JoinRequests!);
        Assert.Equal(Other, request.UserId); Assert.Contains("@example.com", request.Email);
        Assert.Empty((await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).JoinRequests!);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ReviewTeamJoinRequestAsync(Member, Team, Other, new(request.Version, true), Ct)));
        await Use(h, s => s.ReviewTeamJoinRequestAsync(Leader, Team, Other, new(request.Version, true), Ct));
        Assert.False(Assert.Single(await Workspace(h, s => s.GetNotificationsAsync(Leader, Ct)), x => x.Id == notice.Id).JoinRequestPending);
        Assert.Single(await Use(h, s => s.ListTeamsAsync(Other, Ct)));
        Assert.Equal(3, (await Use(h, s => s.GetTeamAsync(Other, Team, Ct))).Members.Length);
        Assert.Empty(await Use(h, s => s.ListMyTeamJoinRequestsAsync(Other, Ct)));
        Assert.Equal(19, await h.DbAsync(db => db.TeamInvites.Select(x => x.RemainingUses).SingleAsync(Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ReviewTeamJoinRequestAsync(Leader, Team, Other, new(request.Version, false), Ct)));
    }

    [Fact] public async Task Rejected_request_cannot_repeat_the_same_link_and_revocation_prevents_approval()
    {
        await using var h = await Setup(); var invite = await Use(h, s => s.CreateTeamInviteAsync(Leader, Team, Ct));
        await Use(h, s => s.JoinTeamAsync(Other, invite.Code, Ct));
        var request = Assert.Single((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).JoinRequests!);
        var originalNotice = Assert.Single(await Workspace(h, s => s.GetNotificationsAsync(Leader, Ct)), x => x.Kind == "TEAM_JOIN_REQUEST");
        await Use(h, s => s.ReviewTeamJoinRequestAsync(Leader, Team, Other, new(request.Version, false), Ct));
        Assert.False(Assert.Single(await Workspace(h, s => s.GetNotificationsAsync(Leader, Ct)), x => x.Id == originalNotice.Id).JoinRequestPending);
        Assert.Equal("Rejected", Assert.Single(await Use(h, s => s.ListMyTeamJoinRequestsAsync(Other, Ct))).Status);
        Assert.Equal("JOIN_REJECTED", (await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.JoinTeamAsync(Other, invite.Code, Ct)))).Code);
        var next = await Use(h, s => s.CreateTeamInviteAsync(Leader, Team, Ct));
        await Use(h, s => s.JoinTeamAsync(Other, next.Code, Ct));
        var notices = await Workspace(h, s => s.GetNotificationsAsync(Leader, Ct));
        Assert.False(Assert.Single(notices, x => x.Id == originalNotice.Id).JoinRequestPending);
        Assert.True(Assert.Single(notices, x => x.Kind == "TEAM_JOIN_REQUEST" && x.Id != originalNotice.Id).JoinRequestPending);
        request = Assert.Single((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).JoinRequests!);
        await Use(h, s => s.RevokeTeamInviteAsync(Leader, Team, next.Id, Ct));
        Assert.Equal("INVITE_INVALID", (await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ReviewTeamJoinRequestAsync(Leader, Team, Other, new(request.Version, true), Ct)))).Code);
        Assert.Empty(await Use(h, s => s.ListTeamsAsync(Other, Ct)));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Kick_releases_unfinished_work_and_preserves_private_calendar_even_during_review(bool pendingReview)
    {
        await using var h = await Setup(); var claim = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, claim.Id, new(), Ct));
        var task = (await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks.Single();
        if (pendingReview) await Use(h, s => s.UpdateTeamProgressAsync(Member, TaskId, new(task.Version, 100), Ct));
        var sessions = await h.DbAsync(db => db.WorkSessions.AsNoTracking().Where(x => x.UserId == Member).Select(x => new { x.Id, x.Start, x.End, x.Status }).ToArrayAsync(Ct));
        var detail = await Use(h, s => s.GetTeamAsync(Leader, Team, Ct)); var member = detail.Members.Single(x => x.UserId == Member);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RemoveTeamMemberAsync(Other, Team, Member, member.Version, Ct)));
        await Use(h, s => s.RemoveTeamMemberAsync(Leader, Team, Member, member.Version, Ct));
        task = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks.Single();
        Assert.Equal("Open", task.Status); Assert.Null(task.AssigneeId); Assert.Null(task.CompletionRequestId); Assert.Equal(0, task.Progress);
        Assert.Equal(sessions, await h.DbAsync(db => db.WorkSessions.AsNoTracking().Where(x => x.UserId == Member).Select(x => new { x.Id, x.Start, x.End, x.Status }).ToArrayAsync(Ct)));
        Assert.False(await h.DbAsync(db => db.TeamTaskAssignments.AnyAsync(x => x.UserId == Member && x.Active, Ct)));
        Assert.Equal("Removed", Assert.Single(await Use(h, s => s.ListMyTeamJoinRequestsAsync(Member, Ct))).Status);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.GetTeamAsync(Member, Team, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.UndoTeamClaimAsync(Member, claim.Id, Ct)));
        var invite = await Use(h, s => s.CreateTeamInviteAsync(Leader, Team, Ct));
        Assert.Equal("MEMBER_REMOVED", (await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.JoinTeamAsync(Member, invite.Code, Ct)))).Code);
    }

    [Fact] public async Task Kick_clears_designation_and_invalidates_old_claim_but_cannot_remove_leader_or_stale_version()
    {
        await using var h = await Setup(); var detail = await Use(h, s => s.GetTeamAsync(Leader, Team, Ct));
        var target = detail.Members.Single(x => x.UserId == Member); var leader = detail.Members.Single(x => x.UserId == Leader);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RemoveTeamMemberAsync(Leader, Team, Leader, leader.Version, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RemoveTeamMemberAsync(Leader, Team, Member, Guid.NewGuid(), Ct)));
        await Use(h, s => s.ChangeTeamTaskRecipientAsync(Leader, TaskId, new(detail.Tasks.Single().Version, Member), Ct));
        var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        await Use(h, s => s.RemoveTeamMemberAsync(Leader, Team, Member, target.Version, Ct));
        Assert.Null((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks.Single().DesignatedUserId);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct)));
        Assert.False(await h.DbAsync(db => db.WorkTasks.AnyAsync(Ct)));
    }
}
