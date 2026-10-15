using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class RecoveryTests
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private static async Task<T> Work<T>(ServiceHarness h,Func<IWorkspaceService,Task<T>> action)
    {await using var scope=h.Provider.CreateAsyncScope();return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());}
    private static Task Work(ServiceHarness h,Func<IWorkspaceService,Task> action)=>Work(h,async s=>{await action(s);return true;});
    private static async Task<(Guid User,Guid Task)> Setup(ServiceHarness h,bool expired=false)
    {
        h.Clock.Advance(DateTimeOffset.Parse("2026-10-15T12:00:00Z")-h.Clock.GetUtcNow());var user=await h.RegisterAsync();
        await Work(h,s=>s.SetPreferenceAsync(user.Id,new("UTC",25,90,0,0,1440),Ct));
        await Work(h,s=>s.SetAvailabilityAsync(user.Id,new(Enumerable.Range(0,7).Select(d=>new AvailabilityInput(d,0,1440)).ToArray()),Ct));
        var task=new WorkTask{UserId=user.Id,Name="Report",EstimatedMinutes=60,Deadline=h.Clock.GetUtcNow().AddDays(expired?-1:2),IsHardDeadline=true,CreatedAt=h.Clock.GetUtcNow().AddDays(-4)};
        await h.DbAsync(async db=>{db.WorkTasks.Add(task);db.WorkSessions.AddRange(new[]{1,3}.Select(day=>new WorkSession{UserId=user.Id,TaskId=task.Id,Start=h.Clock.GetUtcNow().AddDays(-day),End=h.Clock.GetUtcNow().AddDays(-day).AddHours(1),Status=WorkSessionStatus.Missed}));await db.SaveChangesAsync(Ct);});
        return(user.Id,task.Id);
    }
    [Fact] public async Task Two_missed_planned_days_create_one_proposal_and_default_never_applies_it()
    {
        await using var h=await ServiceHarness.CreateAsync();var data=await Setup(h);
        await Work(h,s=>s.ScanRecoveryAsync(data.User,Ct));await Work(h,s=>s.ScanRecoveryAsync(data.User,Ct));
        var page=await Work(h,s=>s.GetRecoveryAsync(data.User,1,Ct));var row=Assert.Single(page.Items);Assert.Equal("PROPOSED",row.Status);Assert.False(page.Settings.AutoApply);
        Assert.Equal(0,await h.DbAsync(db=>db.WorkSessions.CountAsync(x=>x.Status==WorkSessionStatus.Scheduled,Ct)));
        Assert.Equal(1,await h.DbAsync(db=>db.PlanProposals.CountAsync(Ct)));Assert.Equal(1,await h.DbAsync(db=>db.InAppNotifications.CountAsync(x=>x.Kind=="RECOVERY",Ct)));
        await Work(h,s=>s.ApplyRecoveryAsync(data.User,row.Id,Ct));var revision=(await Work(h,s=>s.GetAsync(data.User,Ct))).Preference.Revision;
        await Work(h,s=>s.ApplyRecoveryAsync(data.User,row.Id,Ct));Assert.Equal(revision,(await Work(h,s=>s.GetAsync(data.User,Ct))).Preference.Revision);
        Assert.Single((await Work(h,s=>s.GetAsync(data.User,Ct))).Sessions, x=>x.Status=="Scheduled");
    }
    [Fact] public async Task Stale_proposals_require_rebuild_and_hard_deadline_is_never_extended()
    {
        await using var h=await ServiceHarness.CreateAsync();var data=await Setup(h);
        await Work(h,s=>s.ScanRecoveryAsync(data.User,Ct));var row=Assert.Single((await Work(h,s=>s.GetRecoveryAsync(data.User,1,Ct))).Items);
        await Work(h,s=>s.SaveExceptionAsync(data.User,null,new(new(2026,10,16),600,660,false),Ct));
        Assert.Equal("PLAN_STALE",(await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.ApplyRecoveryAsync(data.User,row.Id,Ct)))).Code);
        await Work(h,s=>s.RebuildRecoveryAsync(data.User,row.Id,Ct));Assert.Equal("PROPOSED",Assert.Single((await Work(h,s=>s.GetRecoveryAsync(data.User,1,Ct))).Items).Status);
        await h.DbAsync(async db=>{var task=await db.WorkTasks.SingleAsync(Ct);task.Deadline=h.Clock.GetUtcNow().AddHours(-1);await db.SaveChangesAsync(Ct);});
        await Work(h,s=>s.RebuildRecoveryAsync(data.User,row.Id,Ct));var blocked=Assert.Single((await Work(h,s=>s.GetRecoveryAsync(data.User,1,Ct))).Items);Assert.Equal("NEEDS_DECISION",blocked.Status);Assert.Null(blocked.Plan);
    }
    [Fact] public async Task Opt_out_and_quiet_hours_are_respected_then_expired_proposal_is_rebuilt_after_quiet_hours()
    {
        await using var h=await ServiceHarness.CreateAsync();var data=await Setup(h);
        await Work(h,s=>s.SetRecoverySettingsAsync(data.User,new(false),Ct));await Work(h,s=>s.ScanRecoveryAsync(data.User,Ct));Assert.Empty((await Work(h,s=>s.GetRecoveryAsync(data.User,1,Ct))).Items);
        var settings=(await Work(h,s=>s.GetRecoveryAsync(data.User,1,Ct))).Settings;
        await Work(h,s=>s.SetRecoverySettingsAsync(data.User,new(true,true,2,660,780,settings.Version),Ct));
        await Work(h,s=>s.ScanRecoveryAsync(data.User,Ct));Assert.Equal(0,await h.DbAsync(db=>db.InAppNotifications.CountAsync(x=>x.Kind=="RECOVERY",Ct)));
        Assert.Equal(0,await h.DbAsync(db=>db.WorkSessions.CountAsync(x=>x.Status==WorkSessionStatus.Scheduled,Ct)));
        h.Clock.Advance(TimeSpan.FromHours(2));await Work(h,s=>s.ScanRecoveryAsync(data.User,Ct));
        Assert.Equal("APPLIED",Assert.Single((await Work(h,s=>s.GetRecoveryAsync(data.User,1,Ct))).Items).Status);
        Assert.Equal(1,await h.DbAsync(db=>db.WorkSessions.CountAsync(x=>x.Status==WorkSessionStatus.Scheduled,Ct)));
    }
}
