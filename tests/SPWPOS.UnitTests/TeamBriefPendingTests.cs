using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;
public sealed partial class PrivateTeamPlanningTests
{
    [Fact] public async Task Date_only_brief_keeps_full_packages_for_review_until_leader_confirms_common_hour()
    {
        var ai = new AgentAiTestDouble { BriefResponse=c=>CompleteBrief(c) with { Question="Ngày nộp là 04/11/2026. Bạn xác nhận giờ nộp chung nhé?",
            Tasks=CompleteBrief(c).Tasks.Select(t=>t with {Deadline=null,DeadlineEvidence="04/11/2026"}).ToArray() } };
        await using var h=await Setup(ai);
        var input=new TeamBriefInput("",TextBrief("Xây dựng proposal, hạn nộp 04/11/2026. Giờ nộp cần trưởng nhóm xác nhận."));
        var pending=await Use(h,s=>s.DraftTeamBriefAsync(Leader,Team,input,Ct));
        Assert.NotEqual(Guid.Empty,pending.ProposalId); Assert.Equal(2,pending.PendingTasks!.Length);
        Assert.All(pending.PendingTasks,t=>{Assert.Null(t.Deadline);Assert.True(t.DeadlineNeedsConfirmation);Assert.Contains("ĐẦU RA CẦN BÀN GIAO",t.Description);Assert.Contains("TIÊU CHÍ HOÀN THÀNH",t.Description);});
        Assert.Equal(1,await h.DbAsync(db=>db.PrivatePlanningProposals.CountAsync(Ct)));
        ai.BriefResponse=c=>CompleteBrief(c) with {Tasks=CompleteBrief(c).Tasks.Select(t=>t with {Deadline=Tomorrow,DeadlineEvidence="hạn ngày mai"}).ToArray()};
        var ready=await Use(h,s=>s.DraftTeamBriefAsync(Leader,Team,input with {Text="Xác nhận chung hạn ngày mai"},Ct));
        Assert.Empty(ready.Question);Assert.Equal(2,ready.Tasks.Length);Assert.NotEqual(Guid.Empty,ready.ProposalId);
    }
    [Fact] public async Task Deadline_quotes_accept_document_whitespace_without_accepting_fabricated_evidence()
    {
        var ai=new AgentAiTestDouble {BriefResponse=c=>CompleteBrief(c) with {Tasks=CompleteBrief(c).Tasks.Select(t=>t with {DeadlineEvidence="hạn   ngày\nmai"}).ToArray()}};
        await using var h=await Setup(ai);
        var ready=await Use(h,s=>s.DraftTeamBriefAsync(Leader,Team,BriefInput,Ct));
        Assert.Equal(2,ready.Tasks.Length);Assert.Empty(ready.Question);
    }
}
