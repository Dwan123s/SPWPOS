using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;
public sealed partial class PrivateTeamPlanningTests
{
    private static TeamDraftTaskEdit[] BriefEdits(TeamBriefDraft draft) => draft.PendingTasks is {Length:>0} pending
        ? pending.Select(t=>new TeamDraftTaskEdit(t.Id,t.Name,t.Description,t.EstimatedMinutes,t.Deadline)).ToArray()
        : draft.Tasks.Select(t=>new TeamDraftTaskEdit(t.Id,t.Name,t.Description,t.EstimatedMinutes,t.Deadline)).ToArray();
    [Fact] public async Task Leader_can_edit_pending_work_packages_and_common_deadline_without_another_ai_call_or_publishing()
    {
        var ai=new AgentAiTestDouble {BriefResponse=c=>CompleteBrief(c) with {Question="Giờ nộp chung là mấy giờ?",Tasks=CompleteBrief(c).Tasks.Select(t=>t with {Deadline=null}).ToArray()}};
        await using var h=await Setup(ai);var draft=await Use(h,s=>s.DraftTeamBriefAsync(Leader,Team,BriefInput,Ct));
        var input=new TeamDraftEditInput(draft.Revision,BriefEdits(draft).Select((t,i)=>t with {EstimatedMinutes=90+i*30,Deadline=Tomorrow,Description=t.Description+"\nLeader xác nhận nội dung và yêu cầu nghiệm thu.",Name="Leader sửa gói "+i}).ToArray());
        var saved=await Use(h,s=>s.EditTeamBriefDraftAsync(Leader,Team,draft.ProposalId,input,Ct));
        Assert.Empty(saved.Question);Assert.Equal(2,saved.Tasks.Length);Assert.Null(saved.PendingTasks);Assert.NotEqual(draft.Revision,saved.Revision);Assert.Equal(1,ai.Calls);
        Assert.Equal(1,await h.DbAsync(db=>db.TeamTasks.CountAsync(Ct)));Assert.Equal(0,await h.DbAsync(db=>db.WorkSessions.CountAsync(Ct)));
        await Assert.ThrowsAsync<AuthException>(()=>Use(h,s=>s.ConfirmTeamBriefAsync(Leader,Team,saved.ProposalId,Ct)));
        await Assert.ThrowsAsync<AuthException>(()=>Use(h,s=>s.EditTeamBriefDraftAsync(Leader,Team,draft.ProposalId,input,Ct)));
        var published=await Use(h,s=>s.ConfirmTeamBriefAsync(Leader,Team,saved.ProposalId,Ct,new(Revision:saved.Revision)));
        var tasks=await h.DbAsync(db=>db.TeamTasks.Where(t=>published.TaskIds.Contains(t.Id)).ToArrayAsync(Ct));
        Assert.All(tasks,t=>{Assert.StartsWith("Leader sửa gói",t.Name);Assert.Equal(Tomorrow,t.Deadline);Assert.Contains("Leader xác nhận",t.Description);Assert.NotNull(t.SourceDocumentId);});
        Assert.Equal(1,await h.DbAsync(db=>db.TeamTaskDocuments.CountAsync(Ct)));Assert.Equal(0,await h.DbAsync(db=>db.WorkTasks.CountAsync(Ct)));
        var retry=await Use(h,s=>s.ConfirmTeamBriefAsync(Leader,Team,saved.ProposalId,Ct,new(Revision:saved.Revision)));Assert.Equal(published.TaskIds,retry.TaskIds);
    }
    [Fact] public async Task Partial_draft_edits_keep_other_missing_deadlines_blocked_and_preserve_ids()
    {
        var ai=new AgentAiTestDouble {BriefResponse=c=>CompleteBrief(c) with {Tasks=CompleteBrief(c).Tasks.Select(t=>t with {Deadline=null}).ToArray()}};
        await using var h=await Setup(ai);var draft=await Use(h,s=>s.DraftTeamBriefAsync(Leader,Team,BriefInput,Ct));
        var edits=BriefEdits(draft);edits[0]=edits[0] with {Deadline=Tomorrow,EstimatedMinutes=75};
        var saved=await Use(h,s=>s.EditTeamBriefDraftAsync(Leader,Team,draft.ProposalId,new(draft.Revision,edits),Ct));
        Assert.Empty(saved.Tasks);Assert.Equal(2,saved.PendingTasks!.Length);Assert.Equal(edits[0].Id,saved.PendingTasks[0].Id);Assert.NotEmpty(saved.Question);
        await Assert.ThrowsAsync<AuthException>(()=>Use(h,s=>s.ConfirmTeamBriefAsync(Leader,Team,saved.ProposalId,Ct,new(Revision:saved.Revision))));
    }
    [Theory][InlineData("member")][InlineData("other")][InlineData("ids")][InlineData("duration")][InlineData("past")][InlineData("membership")][InlineData("expired")]
    public async Task Draft_edits_require_owner_leader_current_membership_and_valid_fields(string change)
    {
        await using var h=await Setup(new(){BriefResponse=CompleteBrief});var draft=await Use(h,s=>s.DraftTeamBriefAsync(Leader,Team,BriefInput,Ct));
        var edits=BriefEdits(draft);var who=Leader;
        if(change=="member")who=Member;if(change=="other")who=Other;
        if(change=="ids")edits[0]=edits[0] with {Id=Guid.NewGuid()};
        if(change=="duration")edits[0]=edits[0] with {EstimatedMinutes=0};
        if(change=="past")edits[0]=edits[0] with {Deadline=h.Clock.GetUtcNow().AddDays(-1)};
        if(change=="membership")await h.DbAsync(async db=>{db.TeamMemberships.Add(new(){TeamId=Team,UserId=Other});await db.SaveChangesAsync(Ct);});
        if(change=="expired")h.Clock.Advance(TimeSpan.FromMinutes(31));
        await Assert.ThrowsAsync<AuthException>(()=>Use(h,s=>s.EditTeamBriefDraftAsync(who,Team,draft.ProposalId,new(draft.Revision,edits),Ct)));
        Assert.Equal(1,await h.DbAsync(db=>db.TeamTasks.CountAsync(Ct)));
    }
}
