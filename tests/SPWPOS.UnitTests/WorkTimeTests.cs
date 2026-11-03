using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class WorkTimeTests
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private static async Task<T> Work<T>(ServiceHarness h,Func<IWorkspaceService,Task<T>> action){await using var scope=h.Provider.CreateAsyncScope();return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());}
    private static Task Work(ServiceHarness h,Func<IWorkspaceService,Task> action)=>Work(h,async s=>{await action(s);return true;});
    private static async Task<(Guid User,WorkTask Task,WorkSession Source,WorkSession Future,WorkSession Other)> Setup(ServiceHarness h,bool running=false) {
        h.Clock.Advance(DateTimeOffset.Parse("2026-11-02T10:00:00Z")-h.Clock.GetUtcNow());var user=await h.RegisterAsync();var now=h.Clock.GetUtcNow();
        await Work(h,s=>s.SetPreferenceAsync(user.Id,new("UTC",25,120,0,480,1320,MaximumDailyMinutes:480),Ct));
        await Work(h,s=>s.SetAvailabilityAsync(user.Id,new(Enumerable.Range(0,7).Select(d=>new AvailabilityInput(d,480,1320)).ToArray()),Ct));
        var task=new WorkTask{UserId=user.Id,Name="Báo cáo",EstimatedMinutes=120,Deadline=now.AddHours(10),IsHardDeadline=true};
        var otherTask=new WorkTask{UserId=user.Id,Name="Việc khác",EstimatedMinutes=60,Deadline=task.Deadline};
        var source=new WorkSession{UserId=user.Id,TaskId=task.Id,Start=now,End=now.AddHours(2)};
        var future=new WorkSession{UserId=user.Id,TaskId=task.Id,Start=now.AddHours(5),End=now.AddHours(5).AddMinutes(45)};
        var other=new WorkSession{UserId=user.Id,TaskId=otherTask.Id,Start=now.AddHours(3),End=now.AddHours(4)};
        await h.DbAsync(async db=>{db.WorkTasks.AddRange(task,otherTask);db.WorkSessions.AddRange(source,future,other);await db.SaveChangesAsync(Ct);});
        if(running)await Work(h,s=>s.ExecuteSessionAsync(user.Id,source.Id,"start",Ct));
        return(user.Id,task,source,future,other);
    }
    [Fact] public async Task Expired_running_session_asks_once_stops_counting_at_end_and_never_completes_task() {
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h,true);
        await h.DbAsync(async db=>{db.TelegramConnections.Add(new(){UserId=x.User,ChatId=123,Enabled=true,ConnectedAt=h.Clock.GetUtcNow()});await db.SaveChangesAsync(Ct);});
        h.Clock.Advance(TimeSpan.FromMinutes(121));await Work(h,s=>s.EnsureStartedNotificationsAsync(x.User,Ct));await Work(h,s=>s.EnsureStartedNotificationsAsync(x.User,Ct));
        var source=await h.DbAsync(db=>db.WorkSessions.SingleAsync(s=>s.Id==x.Source.Id,Ct));Assert.Equal(WorkSessionStatus.Paused,source.Status);Assert.Equal(7200,source.ActualSeconds);Assert.Null(source.SegmentStartedAt);
        Assert.Equal(WorkTaskStatus.InProgress,(await h.DbAsync(db=>db.WorkTasks.SingleAsync(t=>t.Id==x.Task.Id,Ct))).Status);
        var notification=await h.DbAsync(db=>db.InAppNotifications.SingleAsync(n=>n.Kind=="SESSION_REVIEW",Ct));Assert.True(notification.TelegramPending);Assert.Contains("thêm thời gian",notification.Message);
        Assert.Single(await Work(h,s=>s.GetWorkTimeReviewsAsync(x.User,Ct)));
    }
    [Fact] public async Task Extra_time_is_a_separate_confirmed_budget_preserving_estimate_deadline_and_other_sessions() {
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h,true);h.Clock.Advance(TimeSpan.FromMinutes(121));
        var preview=await Work(h,s=>s.PreviewExtraTimeAsync(x.User,x.Source.Id,new(30),Ct));Assert.True(preview.Feasible);Assert.Equal(30,preview.Slots.Sum(s=>(s.End-s.Start).TotalMinutes));
        Assert.Equal(3,await h.DbAsync(db=>db.WorkSessions.CountAsync(Ct)));
        var result=await Work(h,s=>s.ConfirmExtraTimeAsync(x.User,preview.Id,new(preview.Revision),Ct));
        var retry=await Work(h,s=>s.ConfirmExtraTimeAsync(x.User,preview.Id,new(preview.Revision),Ct));Assert.Equal(result.SessionIds,retry.SessionIds);
        var task=await h.DbAsync(db=>db.WorkTasks.SingleAsync(t=>t.Id==x.Task.Id,Ct));Assert.Equal(120,task.EstimatedMinutes);Assert.Equal(x.Task.Deadline,task.Deadline);Assert.Equal(0,task.Progress);
        var sessions=await h.DbAsync(db=>db.WorkSessions.ToArrayAsync(Ct));Assert.Equal(4,sessions.Length);Assert.Contains(sessions,s=>s.Id==x.Other.Id && s.Start==x.Other.Start);Assert.Contains(sessions,s=>s.Id==x.Future.Id && s.Start==x.Future.Start);
        Assert.Empty(await Work(h,s=>s.GetWorkTimeReviewsAsync(x.User,Ct)));
        var snapshot=await Work(h,s=>s.GetAsync(x.User,Ct));Assert.Equal(75,snapshot.Tasks.Single(t=>t.Id==x.Task.Id).RemainingMinutes);
    }
    [Theory][InlineData("calendar")][InlineData("time")][InlineData("task")]
    public async Task Confirmation_rejects_stale_or_elapsed_proposals(string change) {
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h);h.Clock.Advance(TimeSpan.FromMinutes(121));
        var preview=await Work(h,s=>s.PreviewExtraTimeAsync(x.User,x.Source.Id,new(30),Ct));Assert.True(preview.Feasible);
        if(change=="calendar")await Work(h,s=>s.SaveEventAsync(x.User,null,new("Hẹn mới",x.Other.End,x.Other.End.AddMinutes(30)),Ct));
        if(change=="task")await h.DbAsync(async db=>{(await db.WorkTasks.SingleAsync(t=>t.Id==x.Task.Id,Ct)).Name="Đã sửa";await db.SaveChangesAsync(Ct);});
        if(change=="time")h.Clock.Advance(TimeSpan.FromMinutes(2));
        await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.ConfirmExtraTimeAsync(x.User,preview.Id,new(preview.Revision),Ct)));
        Assert.Equal(3,await h.DbAsync(db=>db.WorkSessions.CountAsync(Ct)));
    }
    [Fact] public async Task No_extra_time_can_cross_deadline_or_displace_fixed_commitments() {
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h);h.Clock.Advance(TimeSpan.FromMinutes(121));
        await h.DbAsync(async db=>{(await db.WorkTasks.SingleAsync(t=>t.Id==x.Task.Id,Ct)).Deadline=h.Clock.GetUtcNow().AddMinutes(20);db.FixedEvents.Add(new(){UserId=x.User,Name="Lịch học",Start=h.Clock.GetUtcNow(),End=h.Clock.GetUtcNow().AddHours(2)});await db.SaveChangesAsync(Ct);});
        var preview=await Work(h,s=>s.PreviewExtraTimeAsync(x.User,x.Source.Id,new(30),Ct));Assert.False(preview.Feasible);Assert.Empty(preview.Slots);Assert.Equal(Guid.Empty,preview.Id);Assert.Equal(3,await h.DbAsync(db=>db.WorkSessions.CountAsync(Ct)));
    }
    [Fact] public async Task Finishing_early_records_actual_time_releases_current_remainder_and_future_sessions_once() {
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h,true);h.Clock.Advance(TimeSpan.FromMinutes(30));
        await Work(h,s=>s.FinishWorkTaskAsync(x.User,x.Source.Id,Ct));await Work(h,s=>s.FinishWorkTaskAsync(x.User,x.Source.Id,Ct));
        var task=await h.DbAsync(db=>db.WorkTasks.SingleAsync(t=>t.Id==x.Task.Id,Ct));Assert.Equal(WorkTaskStatus.Completed,task.Status);Assert.Equal(120,task.EstimatedMinutes);
        var source=await h.DbAsync(db=>db.WorkSessions.SingleAsync(s=>s.Id==x.Source.Id,Ct));Assert.Equal(1800,source.ActualSeconds);Assert.Equal(h.Clock.GetUtcNow(),source.End);Assert.Equal(WorkSessionStatus.Completed,source.Status);
        Assert.Equal(WorkSessionStatus.Cancelled,(await h.DbAsync(db=>db.WorkSessions.SingleAsync(s=>s.Id==x.Future.Id,Ct))).Status);
        Assert.Equal(WorkSessionStatus.Scheduled,(await h.DbAsync(db=>db.WorkSessions.SingleAsync(s=>s.Id==x.Other.Id,Ct))).Status);
        Assert.Equal(1,await h.DbAsync(db=>db.EstimationObservations.CountAsync(o=>o.TaskId==task.Id,Ct)));
    }
    [Fact] public async Task Completing_extra_session_consumes_the_confirmed_budget_and_asks_again_if_task_is_not_done() {
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h,true);h.Clock.Advance(TimeSpan.FromMinutes(121));
        var preview=await Work(h,s=>s.PreviewExtraTimeAsync(x.User,x.Source.Id,new(30),Ct));
        await Work(h,s=>s.ConfirmExtraTimeAsync(x.User,preview.Id,new(preview.Revision),Ct));var slot=Assert.Single(preview.Slots);
        h.Clock.Advance(slot.Start-h.Clock.GetUtcNow());await Work(h,s=>s.ExecuteSessionAsync(x.User,slot.Id,"start",Ct));
        h.Clock.Advance(TimeSpan.FromMinutes(30));await Work(h,s=>s.ExecuteSessionAsync(x.User,slot.Id,"complete",Ct));
        Assert.Equal(45,(await Work(h,s=>s.GetAsync(x.User,Ct))).Tasks.Single(t=>t.Id==x.Task.Id).RemainingMinutes);
        var reviews=await Work(h,s=>s.GetWorkTimeReviewsAsync(x.User,Ct));Assert.Single(reviews);Assert.Equal(slot.Id,reviews[0].SessionId);
        Assert.DoesNotContain(reviews,r=>r.SessionId==x.Source.Id);
    }
    [Fact] public async Task Another_account_cannot_extend_or_complete_a_private_session() {
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h);h.Clock.Advance(TimeSpan.FromMinutes(121));var other=Guid.NewGuid();
        await h.DbAsync(async db=>{db.Users.Add(new(){Id=other,UserName="other.time",NormalizedUserName="OTHER.TIME",Email="other.time@example.com",NormalizedEmail="OTHER.TIME@EXAMPLE.COM",FirstName="Other",IsActive=true,EmailConfirmed=true});await db.SaveChangesAsync(Ct);});
        await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.PreviewExtraTimeAsync(other,x.Source.Id,new(30),Ct)));
        await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.FinishWorkTaskAsync(other,x.Source.Id,Ct)));
        Assert.Empty(await Work(h,s=>s.GetWorkTimeReviewsAsync(other,Ct)));
    }
}
