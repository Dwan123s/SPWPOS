using Microsoft.EntityFrameworkCore;
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
}
