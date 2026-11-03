using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class CalendarMoveTests
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private static async Task<T> Work<T>(ServiceHarness h,Func<IWorkspaceService,Task<T>> action){await using var scope=h.Provider.CreateAsyncScope();return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());}
    private static Task Work(ServiceHarness h,Func<IWorkspaceService,Task> action)=>Work(h,async s=>{await action(s);return true;});
    private static async Task<(Guid User,WorkTask Task,WorkSession Session,FixedEvent Event)> Setup(ServiceHarness h){
        h.Clock.Advance(DateTimeOffset.Parse("2026-11-03T06:00:00Z")-h.Clock.GetUtcNow());var user=await h.RegisterAsync();
        await Work(h,s=>s.SetPreferenceAsync(user.Id,new("UTC",25,120,0,480,1320,MaximumDailyMinutes:480),Ct));
        await Work(h,s=>s.SetAvailabilityAsync(user.Id,new(Enumerable.Range(0,7).Select(d=>new AvailabilityInput(d,480,1320)).ToArray()),Ct));
        var task=new WorkTask{UserId=user.Id,Name="Báo cáo",EstimatedMinutes=90,Deadline=h.Clock.GetUtcNow().AddDays(10)};
        var session=new WorkSession{UserId=user.Id,TaskId=task.Id,Start=h.Clock.GetUtcNow().AddDays(2).AddHours(4),End=h.Clock.GetUtcNow().AddDays(2).AddHours(5)};
        var entry=new FixedEvent{UserId=user.Id,Name="Họp",Start=session.Start.AddHours(3),End=session.End.AddHours(3),Description="Tài liệu gốc",EventType="MEETING",IsUrgent=true};
        await h.DbAsync(async db=>{db.WorkTasks.Add(task);db.WorkSessions.Add(session);db.FixedEvents.Add(entry);await db.SaveChangesAsync(Ct);});return(user.Id,task,session,entry);
    }
    private static async Task<CalendarMoveInput> Input(ServiceHarness h,Guid user,string kind,Guid id,DateTimeOffset previousStart,DateTimeOffset previousEnd,DateTimeOffset start,DateTimeOffset end)=>
        new(kind,id,start,end,(await Work(h,s=>s.GetAsync(user,Ct))).Preference.Revision,previousStart,previousEnd);
    [Fact]public async Task Move_and_resize_session_preserves_deadline_and_supports_revision_guarded_undo(){
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h);
        var input=await Input(h,x.User,"Session",x.Session.Id,x.Session.Start,x.Session.End,x.Session.Start.AddDays(1),x.Session.End.AddDays(1).AddMinutes(15));
        var moved=await Work(h,s=>s.MoveCalendarItemAsync(x.User,input,Ct));Assert.NotEqual(input.Revision,moved.Revision);
        var stored=await h.DbAsync(db=>db.WorkSessions.SingleAsync(Ct));Assert.Equal(input.Start,stored.Start);Assert.Equal(input.End,stored.End);Assert.Equal("USER_ADJUSTMENT",stored.ReasonCode);
        Assert.Equal(x.Task.Deadline,(await h.DbAsync(db=>db.WorkTasks.SingleAsync(Ct))).Deadline);
        await Work(h,s=>s.MoveCalendarItemAsync(x.User,new("Session",x.Session.Id,moved.PreviousStart,moved.PreviousEnd,moved.Revision,moved.Start,moved.End),Ct));
        Assert.Equal(x.Session.Start,(await h.DbAsync(db=>db.WorkSessions.SingleAsync(Ct))).Start);Assert.Equal(1,await h.DbAsync(db=>db.WorkSessions.CountAsync(Ct)));
    }
    [Fact]public async Task Event_move_only_changes_time_not_name_category_description_or_urgency(){
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h);
        var input=await Input(h,x.User,"Event",x.Event.Id,x.Event.Start,x.Event.End,x.Event.Start.AddDays(1),x.Event.End.AddDays(1));
        await Work(h,s=>s.MoveCalendarItemAsync(x.User,input,Ct));var entry=await h.DbAsync(db=>db.FixedEvents.SingleAsync(Ct));
        Assert.Equal(input.Start,entry.Start);Assert.Equal("Họp",entry.Name);Assert.Equal("MEETING",entry.EventType);Assert.Equal("Tài liệu gốc",entry.Description);Assert.True(entry.IsUrgent);
        Assert.Equal(x.Session.Start,(await h.DbAsync(db=>db.WorkSessions.SingleAsync(Ct))).Start);
    }
    [Theory][InlineData("Event")][InlineData("Session")]
    public async Task Conflicts_reject_the_drop_atomically_without_moving_any_other_item(string kind){
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h);var eventMove=kind=="Event";
        var input=await Input(h,x.User,kind,eventMove?x.Event.Id:x.Session.Id,eventMove?x.Event.Start:x.Session.Start,eventMove?x.Event.End:x.Session.End,eventMove?x.Session.Start:x.Event.Start,eventMove?x.Session.End:x.Event.End);
        await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.MoveCalendarItemAsync(x.User,input,Ct)));
        Assert.Equal(x.Session.Start,(await h.DbAsync(db=>db.WorkSessions.SingleAsync(Ct))).Start);Assert.Equal(x.Event.Start,(await h.DbAsync(db=>db.FixedEvents.SingleAsync(Ct))).Start);
        Assert.Equal(input.Revision,(await Work(h,s=>s.GetAsync(x.User,Ct))).Preference.Revision);
    }
    [Theory][InlineData("locked")][InlineData("running")][InlineData("completed")][InlineData("past")]
    public async Task Protected_sessions_cannot_be_dragged(string reason){
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h);
        await h.DbAsync(async db=>{var session=await db.WorkSessions.SingleAsync(Ct);if(reason=="locked")session.IsLocked=true;if(reason=="running")session.Status=WorkSessionStatus.Running;if(reason=="completed")session.Status=WorkSessionStatus.Completed;if(reason=="past"){session.Start=h.Clock.GetUtcNow().AddHours(-2);session.End=h.Clock.GetUtcNow().AddHours(-1);}await db.SaveChangesAsync(Ct);});
        var stored=await h.DbAsync(db=>db.WorkSessions.SingleAsync(Ct));var input=await Input(h,x.User,"Session",stored.Id,stored.Start,stored.End,x.Session.Start.AddDays(1),x.Session.End.AddDays(1));
        Assert.Equal("SESSION_NOT_EDITABLE",(await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.MoveCalendarItemAsync(x.User,input,Ct)))).Code);
    }
    [Fact]public async Task Stale_calendar_or_changed_target_and_foreign_item_are_rejected(){
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h);
        var input=await Input(h,x.User,"Session",x.Session.Id,x.Session.Start,x.Session.End,x.Session.Start.AddDays(1),x.Session.End.AddDays(1));
        await Work(h,s=>s.SaveEventAsync(x.User,null,new("Hẹn mới",x.Event.End.AddDays(2),x.Event.End.AddDays(2).AddHours(1)),Ct));
        Assert.Equal("CALENDAR_STALE",(await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.MoveCalendarItemAsync(x.User,input,Ct)))).Code);
        input=input with{Revision=(await Work(h,s=>s.GetAsync(x.User,Ct))).Preference.Revision,ExpectedEnd=x.Session.End.AddMinutes(1)};
        await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.MoveCalendarItemAsync(x.User,input,Ct)));
        var other=Guid.NewGuid();await h.DbAsync(async db=>{db.Users.Add(new(){Id=other,IsActive=true,EmailConfirmed=true,NormalizedEmail="OTHER@EXAMPLE.COM",Email="other@example.com"});await db.SaveChangesAsync(Ct);});
        await Work(h,s=>s.SetPreferenceAsync(other,new("UTC",25,120,0,480,1320),Ct));
        var foreign=input with{Revision=(await Work(h,s=>s.GetAsync(other,Ct))).Preference.Revision};
        Assert.Equal("NOT_FOUND",(await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.MoveCalendarItemAsync(other,foreign,Ct)))).Code);
    }
    [Theory][InlineData("deadline")][InlineData("soft-deadline")][InlineData("availability")][InlineData("length")]
    public async Task Resizing_and_moving_preserve_task_constraints(string restriction){
        await using var h=await ServiceHarness.CreateAsync();var x=await Setup(h);var start=x.Session.Start.AddDays(1);var end=start.AddHours(1);
        if(restriction=="soft-deadline")await h.DbAsync(async db=>{(await db.WorkTasks.SingleAsync(Ct)).IsHardDeadline=false;await db.SaveChangesAsync(Ct);});
        if(restriction is "deadline" or "soft-deadline"){start=x.Task.Deadline.AddDays(1);end=start.AddHours(1);}if(restriction=="availability"){start=new(start.Date.AddHours(2),TimeSpan.Zero);end=start.AddHours(1);}if(restriction=="length")end=start.AddHours(4);
        var input=await Input(h,x.User,"Session",x.Session.Id,x.Session.Start,x.Session.End,start,end);
        await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.MoveCalendarItemAsync(x.User,input,Ct)));Assert.Equal(x.Session.Start,(await h.DbAsync(db=>db.WorkSessions.SingleAsync(Ct))).Start);
    }
}
