using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact] public async Task Invitation_preview_is_read_only_and_shares_only_group_name_count_and_expiry()
    {
        await using var h = await Setup(); var invite = await Use(h, s => s.CreateTeamInviteAsync(Leader, Team, Ct));
        var before = await h.DbAsync(db => db.TeamInvites.Select(x => x.RemainingUses).SingleAsync(Ct));
        var preview = await Use(h, s => s.PreviewTeamInviteAsync(Other, invite.Code.ToLowerInvariant(), Ct));
        Assert.Equal("Đồ án", preview.TeamName); Assert.Equal(2, preview.MemberCount); Assert.False(preview.AlreadyMember);
        Assert.Equal(invite.ExpiresAt, preview.ExpiresAt); Assert.DoesNotContain("PRIVATE_CLASS", JsonSerializer.Serialize(preview));
        Assert.DoesNotContain("@example", JsonSerializer.Serialize(preview));
        Assert.Equal(before, await h.DbAsync(db => db.TeamInvites.Select(x => x.RemainingUses).SingleAsync(Ct)));
        Assert.Equal(2, await h.DbAsync(db => db.TeamMemberships.CountAsync(x => x.Active, Ct)));
        Assert.True((await Use(h, s => s.PreviewTeamInviteAsync(Member, invite.Code, Ct))).AlreadyMember);
        Assert.Equal(Team, await Use(h, s => s.JoinTeamAsync(Other, " " + invite.Code.ToLowerInvariant() + " ", Ct)));
        Assert.Equal(Team, await Use(h, s => s.JoinTeamAsync(Other, invite.Code, Ct)));
        Assert.Equal(before - 1, await h.DbAsync(db => db.TeamInvites.Select(x => x.RemainingUses).SingleAsync(Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.TeamActivities.CountAsync(x => x.ActorId == Other && x.Kind == "MEMBER_JOIN_REQUESTED", Ct)));
        Assert.False(await h.DbAsync(db => db.TeamMemberships.AnyAsync(x => x.UserId == Other && x.Active, Ct)));
    }
    [Theory] [InlineData("revoked")] [InlineData("expired")] [InlineData("exhausted")]
    public async Task Invalid_invites_cannot_be_previewed_or_used_to_join(string reason)
    {
        await using var h = await Setup(); var invite = await Use(h, s => s.CreateTeamInviteAsync(Leader, Team, Ct));
        if (reason == "revoked") await Use(h, s => s.RevokeTeamInviteAsync(Leader, Team, invite.Id, Ct));
        else if (reason == "expired") h.Clock.Advance(TimeSpan.FromDays(8));
        else await h.DbAsync(async db => { (await db.TeamInvites.SingleAsync(Ct)).RemainingUses = 0; await db.SaveChangesAsync(Ct); });
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.PreviewTeamInviteAsync(Other, invite.Code, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.JoinTeamAsync(Other, invite.Code, Ct)));
        Assert.Equal(2, await h.DbAsync(db => db.TeamMemberships.CountAsync(x => x.Active, Ct)));
    }
}
