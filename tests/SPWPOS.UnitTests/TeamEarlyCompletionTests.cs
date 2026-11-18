using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;
public sealed partial class PrivateTeamPlanningTests
{
    [Fact] public async Task Early_member_completion_releases_personal_time_but_still_requires_leader_review() {
        await using var h=await Setup();var proposal=await Use(h,s=>s.PreviewTeamClaimAsync(Member,TaskId,Ct));
        await Use(h,s=>s.ConfirmTeamClaimAsync(Member,proposal.Id,new(),Ct));
        var current=await h.DbAsync(db=>db.WorkSessions.OrderBy(s=>s.Start).FirstAsync(Ct));
        h.Clock.Advance(current.Start-h.Clock.GetUtcNow());await Workspace(h,async s=>{await s.ExecuteSessionAsync(Member,current.Id,"start",Ct);return true;});
        h.Clock.Advance(TimeSpan.FromMinutes(5));
        await Workspace(h,s=>s.FinishWorkTaskAsync(Member,current.Id,Ct));
        var shared=await h.DbAsync(db=>db.TeamTasks.SingleAsync(t=>t.Id==TaskId,Ct));Assert.Equal("PendingReview",shared.Status);
        Assert.Equal(0,await h.DbAsync(db=>db.WorkSessions.CountAsync(s=>s.UserId==Member && s.Status==WorkSessionStatus.Scheduled,Ct)));
        Assert.Equal(0,await h.DbAsync(db=>db.InAppNotifications.CountAsync(n=>n.Kind=="TEAM_TASK_COMPLETED",Ct)));
    }

    [Fact]
    public async Task Finishing_an_active_session_requires_the_team_report_and_rolls_back_when_missing()
    {
        await using var h = await Setup();
        await ClaimForReview(h);
        var policy = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).CompletionPolicy!;
        await Use(h, s => s.SaveTeamCompletionPolicyAsync(Leader, Team, policy with { RequireReportUrl = true }, Ct));
        var session = await h.DbAsync(db => db.WorkSessions.OrderBy(s => s.Start).FirstAsync(Ct));
        h.Clock.Advance(session.Start - h.Clock.GetUtcNow());
        await Workspace(h, async s => { await s.ExecuteSessionAsync(Member, session.Id, "start", Ct); return true; });
        h.Clock.Advance(TimeSpan.FromMinutes(5));

        var error = await Assert.ThrowsAsync<AuthException>(() => Workspace(h, s => s.FinishWorkTaskAsync(Member, session.Id, Ct)));
        Assert.Equal("TEAM_REPORT_URL_REQUIRED", error.Code);
        Assert.Equal(WorkSessionStatus.Running, await h.DbAsync(db => db.WorkSessions.Where(s => s.Id == session.Id).Select(s => s.Status).SingleAsync(Ct)));
        Assert.Equal("Assigned", (await Current(h)).Status);

        await Workspace(h, s => s.FinishWorkTaskAsync(Member, session.Id, Ct, new("https://example.com/report")));
        var submitted = await Current(h);
        Assert.Equal("PendingReview", submitted.Status);
        Assert.Equal("https://example.com/report", submitted.CompletionReportUrl);
    }

    [Fact]
    public async Task Catch_up_records_past_work_and_leaves_required_report_task_available_for_submission()
    {
        await using var h = await Setup();
        await ClaimForReview(h);
        var policy = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).CompletionPolicy!;
        await Use(h, s => s.SaveTeamCompletionPolicyAsync(Leader, Team, policy with { RequireReportUrl = true }, Ct));
        var session = await h.DbAsync(db => db.WorkSessions.OrderBy(s => s.Start).FirstAsync(Ct));
        h.Clock.Advance(session.End.AddMinutes(1) - h.Clock.GetUtcNow());
        await h.DbAsync(async db =>
        {
            (await db.WorkTasks.SingleAsync(Ct)).Progress = 99;
            await db.SaveChangesAsync(Ct);
        });

        var outcome = await Workspace(h, s => s.CatchUpAsync(Member, session.Id, new("COMPLETED"), Ct));
        Assert.Equal("Completed", outcome.Status);
        Assert.Equal(WorkTaskStatus.InProgress, await h.DbAsync(db => db.WorkTasks.Select(t => t.Status).SingleAsync(Ct)));
        Assert.Equal("Assigned", (await Current(h)).Status);

        await Workspace(h, s => s.FinishWorkTaskAsync(Member, session.Id, Ct, new("https://example.com/report")));
        Assert.Equal("PendingReview", (await Current(h)).Status);
    }
}
