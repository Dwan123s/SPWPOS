using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    private static Friendship Friends(Guid a, Guid b) => new() {
        UserLowId = a.CompareTo(b) < 0 ? a : b, UserHighId = a.CompareTo(b) < 0 ? b : a,
        RequestedById = a, Status = "Accepted"
    };

    [Fact]
    public async Task Friend_invitation_requires_leader_and_accepted_friendship_and_is_idempotent()
    {
        await using var h = await Setup();
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.InviteTeamFriendAsync(Leader, Team, Other, Ct)));
        await h.DbAsync(async db => { db.Friendships.Add(Friends(Leader, Other)); await db.SaveChangesAsync(Ct); });
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.InviteTeamFriendAsync(Member, Team, Other, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ListTeamInviteFriendsAsync(Member, Team, "", 0, Ct)));
        var sent = await Use(h, s => s.InviteTeamFriendAsync(Leader, Team, Other, Ct));
        var repeated = await Use(h, s => s.InviteTeamFriendAsync(Leader, Team, Other, Ct));
        Assert.Equal("Invited", sent.Status); Assert.Equal(sent.InvitationId, repeated.InvitationId);
        Assert.Equal(1, await h.DbAsync(db => db.TeamFriendInvitations.CountAsync(Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.InAppNotifications.CountAsync(x => x.Kind == "TEAM_FRIEND_INVITE", Ct)));
        Assert.Empty(await Use(h, s => s.ListTeamsAsync(Other, Ct)));
        Assert.Empty(await Use(h, s => s.ListTeamFriendInvitationsAsync(Member, Ct)));
    }

    [Fact]
    public async Task Friend_invitation_recipient_accepts_once_without_second_leader_approval()
    {
        await using var h = await Setup();
        await h.DbAsync(async db => { db.Friendships.Add(Friends(Leader, Other)); await db.SaveChangesAsync(Ct); });
        var sent = await Use(h, s => s.InviteTeamFriendAsync(Leader, Team, Other, Ct));
        var incoming = Assert.Single(await Use(h, s => s.ListTeamFriendInvitationsAsync(Other, Ct)));
        Assert.Equal("Đồ án", incoming.TeamName);
        var input = new TeamFriendInviteResponse(sent.Version!.Value, true);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RespondTeamFriendInvitationAsync(Member, incoming.Id, input, Ct)));
        Assert.Equal(Team, await Use(h, s => s.RespondTeamFriendInvitationAsync(Other, incoming.Id, input, Ct)));
        Assert.Equal(Team, await Use(h, s => s.RespondTeamFriendInvitationAsync(Other, incoming.Id, input, Ct)));
        Assert.Single(await Use(h, s => s.ListTeamsAsync(Other, Ct)));
        Assert.Empty(await Use(h, s => s.ListTeamFriendInvitationsAsync(Other, Ct)));
        Assert.Equal(3, (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Members.Length);
    }

    [Fact]
    public async Task Friend_invitation_can_be_revoked_and_old_version_cannot_join()
    {
        await using var h = await Setup();
        await h.DbAsync(async db => { db.Friendships.Add(Friends(Leader, Other)); await db.SaveChangesAsync(Ct); });
        var sent = await Use(h, s => s.InviteTeamFriendAsync(Leader, Team, Other, Ct));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RevokeTeamFriendInvitationAsync(Member, Team, sent.InvitationId!.Value, sent.Version!.Value, Ct)));
        await Use(h, s => s.RevokeTeamFriendInvitationAsync(Leader, Team, sent.InvitationId!.Value, sent.Version!.Value, Ct));
        Assert.Empty(await Use(h, s => s.ListTeamFriendInvitationsAsync(Other, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RespondTeamFriendInvitationAsync(Other, sent.InvitationId!.Value, new(sent.Version!.Value, true), Ct)));
        Assert.Empty(await Use(h, s => s.ListTeamsAsync(Other, Ct)));
    }

    [Fact]
    public async Task Friend_invitation_decline_and_expiry_do_not_add_members()
    {
        await using var h = await Setup();
        await h.DbAsync(async db => { db.Friendships.Add(Friends(Leader, Other)); await db.SaveChangesAsync(Ct); });
        var sent = await Use(h, s => s.InviteTeamFriendAsync(Leader, Team, Other, Ct));
        await Use(h, s => s.RespondTeamFriendInvitationAsync(Other, sent.InvitationId!.Value, new(sent.Version!.Value, false), Ct));
        Assert.Empty(await Use(h, s => s.ListTeamsAsync(Other, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.InviteTeamFriendAsync(Leader, Team, Other, Ct)));
        h.Clock.Advance(TimeSpan.FromDays(2));
        var renewed = await Use(h, s => s.InviteTeamFriendAsync(Leader, Team, Other, Ct));
        Assert.NotEqual(sent.Version, renewed.Version);
        h.Clock.Advance(TimeSpan.FromDays(8));
        Assert.Empty(await Use(h, s => s.ListTeamFriendInvitationsAsync(Other, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RespondTeamFriendInvitationAsync(Other, renewed.InvitationId!.Value, new(renewed.Version!.Value, true), Ct)));
    }

    [Fact]
    public async Task Friend_invitation_list_filters_and_marks_existing_members_without_leaking_nonfriends()
    {
        await using var h = await Setup();
        await h.DbAsync(async db => {
            db.Friendships.AddRange(Friends(Leader, Other), Friends(Leader, Member));
            db.UserProfiles.Add(new() { UserId = Other, DisplayName = "Minh Anh" });
            await db.SaveChangesAsync(Ct);
        });
        var list = await Use(h, s => s.ListTeamInviteFriendsAsync(Leader, Team, "", 0, Ct));
        Assert.Equal(2, list.Items.Length);
        Assert.Equal("Member", list.Items.Single(x => x.UserId == Member).Status);
        var search = await Use(h, s => s.ListTeamInviteFriendsAsync(Leader, Team, "minh", 0, Ct));
        Assert.Equal(Other, Assert.Single(search.Items).UserId);
        Assert.Null(search.NextOffset);
        Assert.Empty((await Use(h, s => s.ListTeamInviteFriendsAsync(Leader, Team, "missing", 0, Ct))).Items);
    }

    [Fact]
    public async Task Friend_invitation_preserves_pending_invite_when_group_is_full()
    {
        await using var h = await Setup();
        await h.DbAsync(async db => {
            db.Friendships.Add(Friends(Leader, Other));
            var limit = TeamPlan.Free.MemberLimit!.Value;
            for (var index = 2; index < limit; index++) {
                var id = Guid.NewGuid();
                db.Users.Add(new() { Id = id, UserName = id.ToString(), NormalizedUserName = id.ToString().ToUpperInvariant(), Email = id + "@example.com", NormalizedEmail = (id + "@example.com").ToUpperInvariant(), IsActive = true, EmailConfirmed = true });
                db.TeamMemberships.Add(new() { UserId = id, TeamId = Team });
            }
            await db.SaveChangesAsync(Ct);
        });
        var sent = await Use(h, s => s.InviteTeamFriendAsync(Leader, Team, Other, Ct));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RespondTeamFriendInvitationAsync(Other, sent.InvitationId!.Value, new(sent.Version!.Value, true), Ct)));
        Assert.Single(await Use(h, s => s.ListTeamFriendInvitationsAsync(Other, Ct)));
        Assert.Empty(await Use(h, s => s.ListTeamsAsync(Other, Ct)));
    }

    [Fact]
    public async Task Friend_invitation_cannot_restore_removed_members_or_join_after_sender_loses_leader_role()
    {
        await using var h = await Setup();
        await h.DbAsync(async db => { db.Friendships.Add(Friends(Leader, Other)); await db.SaveChangesAsync(Ct); });
        var sent = await Use(h, s => s.InviteTeamFriendAsync(Leader, Team, Other, Ct));
        await h.DbAsync(async db => {
            var leader = await db.TeamMemberships.SingleAsync(x => x.UserId == Leader, Ct);
            leader.Role = "Member"; await db.SaveChangesAsync(Ct);
        });
        Assert.Empty(await Use(h, s => s.ListTeamFriendInvitationsAsync(Other, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RespondTeamFriendInvitationAsync(Other, sent.InvitationId!.Value, new(sent.Version!.Value, true), Ct)));
        await h.DbAsync(async db => {
            (await db.TeamMemberships.SingleAsync(x => x.UserId == Leader, Ct)).Role = "Leader";
            db.TeamMemberships.Add(new() { UserId = Other, TeamId = Team, Active = false, ApprovalStatus = "Removed" });
            await db.SaveChangesAsync(Ct);
        });
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RespondTeamFriendInvitationAsync(Other, sent.InvitationId!.Value, new(sent.Version!.Value, true), Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.InviteTeamFriendAsync(Leader, Team, Other, Ct)));
    }
}
