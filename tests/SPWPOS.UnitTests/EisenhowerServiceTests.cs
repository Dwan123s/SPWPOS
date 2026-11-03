using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    private static async Task<T> Eisenhower<T>(ServiceHarness h, Func<IEisenhowerService, Task<T>> action) {
        await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IEisenhowerService>());
    }
    [Fact] public async Task Eisenhower_groups_inherited_shared_importance_without_duplication_and_preserves_member_override()
    {
        await using var h = await Setup(); var shared = Assert.Single((await Use(h,s=>s.GetTeamAsync(Leader,Team,Ct))).Tasks);
        await Eisenhower(h, async s => { await s.SetImportanceAsync(Leader, TaskId, true, new(shared.Version,"Important","Đầu ra bắt buộc"),Ct); return true; });
        var before = await Eisenhower(h,s=>s.GetEisenhowerAsync(Member,Ct)); Assert.Empty(before.Tasks);
        var latest = (await Use(h,x=>x.GetTeamAsync(Leader,Team,Ct))).Tasks.Single();
        await Use(h,s=>s.ChangeTeamTaskRecipientAsync(Leader,TaskId,new(latest.Version,Member),Ct));
        var candidate = Assert.Single((await Eisenhower(h,s=>s.GetEisenhowerAsync(Member,Ct))).Tasks);
        Assert.Equal("NeedsAcceptance",candidate.Kind); Assert.Equal("Important",candidate.Importance);
        Assert.Equal(0,await h.DbAsync(db=>db.WorkTasks.CountAsync(Ct)));
        var preview = await Use(h,s=>s.PreviewTeamClaimAsync(Member,TaskId,Ct));
        await Use(h,s=>s.ConfirmTeamClaimAsync(Member,preview.Id,new(),Ct));
        var accepted = Assert.Single((await Eisenhower(h,s=>s.GetEisenhowerAsync(Member,Ct))).Tasks);
        Assert.Equal("Accepted",accepted.Kind); Assert.Equal(TaskId,accepted.TeamTaskId);
        var personal = await h.DbAsync(db=>db.WorkTasks.SingleAsync(Ct));
        await Eisenhower(h,async s=>{ await s.SetImportanceAsync(Member,personal.Id,false,new(personal.Version,"Lower","Tôi phụ trách phần hỗ trợ"),Ct); return true; });
        Assert.Equal("Lower",Assert.Single((await Eisenhower(h,s=>s.GetEisenhowerAsync(Member,Ct))).Tasks).Importance);
        Assert.Equal("Important",(await Use(h,s=>s.GetTeamAsync(Member,Team,Ct))).Tasks.Single().Importance!.Level);
        await Assert.ThrowsAsync<AuthException>(()=>Eisenhower(h,s=>s.GetTaskEisenhowerAsync(Leader,TaskId,true,Ct)));
    }
    [Fact] public async Task Shared_importance_requires_leader_and_invalidates_claim_previews_without_changing_calendars()
    {
        await using var h = await Setup();
        var preview = await Use(h,s=>s.PreviewTeamClaimAsync(Member,TaskId,Ct));
        var shared = Assert.Single((await Use(h,s=>s.GetTeamAsync(Leader,Team,Ct))).Tasks);
        await Assert.ThrowsAsync<AuthException>(()=>Eisenhower(h,async s=>{ await s.SetImportanceAsync(Member,TaskId,true,new(shared.Version,"Important","Bắt buộc"),Ct); return true; }));
        await Eisenhower(h,async s=>{ await s.SetImportanceAsync(Leader,TaskId,true,new(shared.Version,"Important","Bắt buộc"),Ct); return true; });
        await Assert.ThrowsAsync<AuthException>(()=>Use(h,s=>s.ConfirmTeamClaimAsync(Member,preview.Id,new(),Ct)));
        Assert.Equal(0,await h.DbAsync(db=>db.WorkTasks.CountAsync(Ct)));
        await Assert.ThrowsAsync<AuthException>(()=>Eisenhower(h,s=>s.GetTaskEisenhowerAsync(Other,TaskId,true,Ct)));
    }
    [Fact] public async Task Assessments_refresh_on_revision_and_time_but_get_never_mutates_business_data()
    {
        await using var h = await Setup(); var personal = new WorkTask { UserId=Member,Name="Personal",Deadline=Tomorrow.AddHours(20),EstimatedMinutes=25,ImportanceLevel="Important" };
        await h.DbAsync(async db=>{ db.WorkTasks.Add(personal); await db.SaveChangesAsync(Ct); });
        var first=await Eisenhower(h,s=>s.GetEisenhowerAsync(Member,Ct));
        var repeat=await Eisenhower(h,s=>s.GetEisenhowerAsync(Member,Ct)); Assert.Equal(first.AssessmentRevision,repeat.AssessmentRevision);
        h.Clock.Advance(TimeSpan.FromHours(34));
        var later=await Eisenhower(h,s=>s.GetEisenhowerAsync(Member,Ct)); Assert.True(later.EvaluatedAt>first.EvaluatedAt); Assert.Equal("Urgent",later.Tasks.Single().Urgency);
        Assert.Equal(0,await h.DbAsync(db=>db.WorkSessions.CountAsync(Ct)));
        Assert.Equal(0,await h.DbAsync(db=>db.PriorityAlertStates.CountAsync(Ct)));
    }
    [Fact] public async Task Priority_transitions_notify_once_and_keep_initial_backlog_quiet()
    {
        await using var h=await Setup();
        var task=new WorkTask { UserId=Member,Name="Transition",Deadline=Tomorrow.AddHours(20),EstimatedMinutes=25,ImportanceLevel="Important" };
        await h.DbAsync(async db=>{ db.WorkTasks.Add(task); db.TelegramConnections.Add(new(){UserId=Member,ChatId=123456,Enabled=true,ConnectedAt=h.Clock.GetUtcNow()}); await db.SaveChangesAsync(Ct); });
        await Workspace(h,async s=>{ await s.EnsureNotificationsAsync(Member,Ct); return true; });
        Assert.Equal(0,await h.DbAsync(db=>db.InAppNotifications.CountAsync(n=>n.Kind=="EISENHOWER_ALERT",Ct)));
        h.Clock.Advance(TimeSpan.FromHours(34));
        await Workspace(h,async s=>{ await s.EnsureNotificationsAsync(Member,Ct); return true; }); await Workspace(h,async s=>{ await s.EnsureNotificationsAsync(Member,Ct); return true; });
        Assert.Equal(1,await h.DbAsync(db=>db.InAppNotifications.CountAsync(n=>n.Kind=="EISENHOWER_ALERT",Ct)));
        var alert = await h.DbAsync(db=>db.InAppNotifications.SingleAsync(n=>n.Kind=="EISENHOWER_ALERT",Ct));
        Assert.True(alert.TelegramPending); Assert.NotNull(alert.TelegramBindingId);
    }
    [Fact] public async Task Brief_importance_is_optional_but_supported_evidence_survives_confirmation()
    {
        var ai=new AgentAiTestDouble { BriefResponse=c=>CompleteBrief(c) with { Tasks=CompleteBrief(c).Tasks.Select(t=>t with { Importance=new("Important","Đầu ra bắt buộc",.95,"báo cáo") }).ToArray() } };
        await using var h=await Setup(ai);
        var draft=await Use(h,s=>s.DraftTeamBriefAsync(Leader,Team,BriefInput,Ct));
        Assert.All(draft.Tasks,t=>Assert.Equal("Important",t.Importance!.Level));
        await Use(h,s=>s.ConfirmTeamBriefAsync(Leader,Team,draft.ProposalId,Ct));
        Assert.All((await Use(h,s=>s.GetTeamAsync(Member,Team,Ct))).Tasks.Where(t=>draft.Tasks.Any(d=>d.Id==t.Id)), t=>{Assert.Equal("Leader",t.Importance!.Source); Assert.True(t.Importance.Confirmed);});
    }
}
