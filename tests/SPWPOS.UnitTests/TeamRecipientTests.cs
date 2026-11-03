using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact] public async Task Designated_task_notifies_its_recipient_but_only_their_explicit_claim_changes_their_calendar()
    {
        await using var h = await Setup(new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Báo cáo", Tomorrow, 60, 2, "OTHER", true, DeadlineEvidence: "hạn ngày mai")] } });
        var draft = await Use(h, s => s.DraftTeamTaskAsync(Leader, Team, new("Báo cáo hạn ngày mai"), Ct));
        var id = await Use(h, s => s.ConfirmTeamTaskDraftAsync(Leader, Team, draft.ProposalId, Ct, new(Member)));
        await Use(h, s => s.ConfirmTeamTaskDraftAsync(Leader, Team, draft.ProposalId, Ct, new(Member)));
        var task = (await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks.Single(t => t.Id == id);
        Assert.Equal(Member, task.DesignatedUserId); Assert.Null(task.AssigneeId); Assert.Equal("Open", task.Status);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.InAppNotifications.CountAsync(n => n.UserId == Member && n.Kind == "TEAM_TASK_DESIGNATED", Ct)));
        var denied = await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.PreviewTeamClaimAsync(Leader, id, Ct)));
        Assert.Equal("TASK_DESIGNATED", denied.Code);
        var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, id, Ct)); Assert.NotEmpty(preview.Options);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct));
        Assert.Equal(Member, (await h.DbAsync(db => db.TeamTasks.SingleAsync(t => t.Id == id, Ct))).AssigneeId);
        Assert.Equal(1, await h.DbAsync(db => db.WorkTasks.CountAsync(t => t.UserId == Member, Ct)));
    }

    [Theory] [InlineData("foreign")] [InlineData("removed")] [InlineData("inactive")]
    public async Task Invalid_designated_members_cannot_receive_new_single_or_batch_tasks(string kind)
    {
        await using var h = await Setup(new AgentAiTestDouble { BriefResponse = CompleteBrief });
        var draft = await Use(h, s => s.DraftTeamBriefAsync(Leader, Team, BriefInput, Ct));
        if (kind == "removed") await h.DbAsync(async db => { (await db.TeamMemberships.SingleAsync(m => m.UserId == Member, Ct)).Active = false; await db.SaveChangesAsync(Ct); });
        if (kind == "inactive") await h.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == Member, Ct)).IsActive = false; await db.SaveChangesAsync(Ct); });
        var recipient = kind == "foreign" ? Other : Member;
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct, new([new(draft.Tasks[0].Id, recipient)]))));
        Assert.Equal(1, await h.DbAsync(db => db.TeamTasks.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.TeamTaskDocuments.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.InAppNotifications.CountAsync(n => n.Kind == "TEAM_TASK_DESIGNATED", Ct)));
        var current = Assert.Single((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ChangeTeamTaskRecipientAsync(Leader, current.Id, new(current.Version, recipient), Ct)));
    }

    [Fact] public async Task Batch_can_mix_named_and_open_tasks_and_rejects_conflicting_confirmation_retries()
    {
        await using var h = await Setup(new AgentAiTestDouble { BriefResponse = CompleteBrief });
        var draft = await Use(h, s => s.DraftTeamBriefAsync(Leader, Team, BriefInput, Ct));
        var choices = new TeamBriefConfirmInput([new(draft.Tasks[0].Id, Member)]);
        await Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct, choices));
        await Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct, choices));
        var detail = await Use(h, s => s.GetTeamAsync(Member, Team, Ct));
        Assert.Equal(Member, detail.Tasks.Single(t => t.Id == draft.Tasks[0].Id).DesignatedUserId);
        Assert.Null(detail.Tasks.Single(t => t.Id == draft.Tasks[1].Id).DesignatedUserId);
        Assert.Equal(1, await h.DbAsync(db => db.TeamTaskDocuments.CountAsync(Ct)));
        var error = await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct)));
        Assert.Equal("REQUEST_REUSED", error.Code);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Changing_recipient_invalidates_old_claims_and_leader_can_release_an_unclaimed_task()
    {
        await using var h = await Setup();
        var memberPreview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        var task = Assert.Single((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ChangeTeamTaskRecipientAsync(Member, TaskId, new(task.Version, Member), Ct)));
        await Use(h, s => s.ChangeTeamTaskRecipientAsync(Leader, TaskId, new(task.Version, Leader), Ct));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamClaimAsync(Member, memberPreview.Id, new(), Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ChangeTeamTaskRecipientAsync(Leader, TaskId, new(task.Version, null), Ct)));
        task = Assert.Single((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks);
        await Use(h, s => s.ChangeTeamTaskRecipientAsync(Leader, TaskId, new(task.Version, null), Ct));
        Assert.NotEmpty((await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct))).Options);
    }

    [Fact] public async Task Unclaimed_designations_prevent_orphaned_tasks_when_the_member_leaves_and_return_keeps_the_designation()
    {
        await using var h = await Setup();
        var detail = await Use(h, s => s.GetTeamAsync(Leader, Team, Ct)); var task = Assert.Single(detail.Tasks);
        await Use(h, s => s.ChangeTeamTaskRecipientAsync(Leader, TaskId, new(task.Version, Member), Ct));
        var version = detail.Members.Single(m => m.UserId == Member).Version;
        var error = await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RemoveTeamMemberAsync(Member, Team, Member, version, Ct)));
        Assert.Equal("DESIGNATIONS_ACTIVE", error.Code);
        var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct));
        task = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks.Single();
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ChangeTeamTaskRecipientAsync(Leader, TaskId, new(task.Version, null), Ct)));
        await Use(h, s => s.UndoTeamClaimAsync(Member, preview.Id, Ct));
        task = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks.Single(); Assert.Equal(Member, task.DesignatedUserId);
        await Use(h, s => s.ChangeTeamTaskRecipientAsync(Leader, TaskId, new(task.Version, null), Ct));
        await Use(h, s => s.RemoveTeamMemberAsync(Member, Team, Member, version, Ct));
    }
}
