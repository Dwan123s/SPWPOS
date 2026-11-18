using SPWPOS.Application.Workspace;

namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact]
    public async Task Team_personal_and_matrix_priorities_match_through_claim_edit_acceptance_and_deadline_crossing()
    {
        await using var h = await Setup();
        var deadline = Tomorrow.AddHours(3);
        var draft = await Use(h, s => s.DraftManualTeamTaskAsync(Leader, Team,
            new("Báo cáo ưu tiên", "Bản cuối", 60, deadline, ImportanceLevel: "Important"), Ct));
        Assert.Equal("Q2", draft.Task!.Eisenhower!.Quadrant);
        var id = await Use(h, s => s.ConfirmTeamTaskDraftAsync(Leader, Team, draft.ProposalId, Ct));
        var claim = await Use(h, s => s.PreviewTeamClaimAsync(Member, id, Ct));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, claim.Id, new(), Ct));
        var shared = (await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks.Single(task => task.Id == id);
        var personal = Assert.Single((await Workspace(h, s => s.GetAsync(Member, Ct))).Tasks);
        Assert.Equal("Q2", shared.Eisenhower!.Quadrant);
        Assert.Equal(shared.Eisenhower.Quadrant, personal.Eisenhower!.Quadrant);
        Assert.Equal("Q2", Assert.Single((await Eisenhower(h, s => s.GetEisenhowerAsync(Member, Ct))).Tasks).Quadrant);

        var change = await Use(h, s => s.ProposeManualTeamTaskChangeAsync(Leader, id,
            new(shared.Version, new(shared.Name, shared.Description, 60, deadline, ImportanceLevel: "Lower")), Ct));
        await Use(h, s => s.ConfirmTeamTaskDraftAsync(Leader, Team, change.ProposalId, Ct));
        Assert.Equal("Important", (await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks.Single(task => task.Id == id).Importance!.Level);
        var approval = await Use(h, s => s.PreviewTeamTaskChangeAsync(Member, id, Ct));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, approval.Id, new(), Ct));
        Assert.Equal("Q4", Assert.Single((await Workspace(h, s => s.GetAsync(Member, Ct))).Tasks).Eisenhower!.Quadrant);

        h.Clock.Advance(deadline.AddHours(-24) - h.Clock.GetUtcNow());
        Assert.Equal("Q3", (await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks.Single(task => task.Id == id).Eisenhower!.Quadrant);
        Assert.Equal("Q3", Assert.Single((await Workspace(h, s => s.GetAsync(Member, Ct))).Tasks).Eisenhower!.Quadrant);
        Assert.Equal("Q3", Assert.Single((await Eisenhower(h, s => s.GetEisenhowerAsync(Member, Ct))).Tasks).Quadrant);
    }
}
