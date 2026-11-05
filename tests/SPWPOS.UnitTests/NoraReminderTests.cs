using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed class NoraReminderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Work<T>(ServiceHarness h,Func<IWorkspaceService,Task<T>> call)
    {await using var scope=h.Provider.CreateAsyncScope();return await call(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());}
    private static async Task<T> Agent<T>(ServiceHarness h,Func<IPlanningAgentService,Task<T>> call)
    {await using var scope=h.Provider.CreateAsyncScope();return await call(scope.ServiceProvider.GetRequiredService<IPlanningAgentService>());}
    private static AgentTurnInput Input(string text,bool auto=true,Guid? conversation=null)=>new(conversation??Guid.NewGuid(),Guid.NewGuid(),text,AutoApply:auto);
    private static AgentAiTestDouble Model(NoraReminderCommand command)=>new(){Response=_=>AgentAiTestDouble.Empty() with{Reminder=command}};

    [Theory]
    [InlineData("1 phút nữa nhắc tôi rep mail khách hàng",60)]
    [InlineData("Nhắc tôi sau 30 giây kiểm tra lò",30)]
    [InlineData("Nhắc mình 1,5 giờ nữa gọi khách",5400)]
    [InlineData("2 gio nua nhac toi goi dien",7200)]
    public async Task Reminders_are_durable_account_scoped_idempotent_and_do_not_create_tasks_or_Inbox_notes(string text,int seconds)
    {
        var ai=Model(new("Create","Rep mail khách hàng"));
        await using var h=await ServiceHarness.CreateAsync(s=>s.AddSingleton<ITaskInterpreter>(ai));var user=await h.RegisterAsync();var now=h.Clock.GetUtcNow();
        var input=Input(text);
        var result=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,input,Ct));
        Assert.Equal("APPLIED",result.Status);Assert.Single(result.Changes,x=>x.Kind=="REMINDER");
        var reminder=Assert.Single(await Work(h,s=>s.GetNoraRemindersAsync(user.Id,Ct)));
        Assert.Equal(now.AddSeconds(seconds),reminder.At);
        Assert.Empty(await Work(h,s=>s.GetNotificationsAsync(user.Id,Ct)));
        await Agent(h,s=>s.SendAgentTurnAsync(user.Id,input,Ct));
        Assert.Single(await Work(h,s=>s.GetNoraRemindersAsync(user.Id,Ct)));Assert.Equal(1,ai.Calls);
        h.Clock.Advance(TimeSpan.FromSeconds(seconds));
        Assert.Equal("NORA_REMINDER",Assert.Single(await Work(h,s=>s.GetNotificationsAsync(user.Id,Ct))).Kind);
        Assert.Equal(0,await h.DbAsync(db=>db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(0,await h.DbAsync(db=>db.FixedEvents.CountAsync(Ct)));
        Assert.Equal(0,await h.DbAsync(db=>db.PlanningNotes.CountAsync(Ct)));
    }

    [Fact]
    public async Task Preview_respects_no_save_then_apply_and_undo_restore_reminder_state()
    {
        var ai=Model(new("Create","Gọi khách"));
        await using var h=await ServiceHarness.CreateAsync(s=>s.AddSingleton<ITaskInterpreter>(ai));var user=await h.RegisterAsync();
        var preview=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,Input("10 phút nữa nhắc tôi gọi khách",false),Ct));
        Assert.Equal("PREVIEW",preview.Status);Assert.Empty(await Work(h,s=>s.GetNoraRemindersAsync(user.Id,Ct)));
        var applied=await Agent(h,s=>s.ApplyAgentTurnAsync(user.Id,preview.Id,Ct));
        var reminder=Assert.Single(await Work(h,s=>s.GetNoraRemindersAsync(user.Id,Ct)));
        Assert.True(applied.CanUndo);
        await Agent(h,s=>s.UndoAgentTurnAsync(user.Id,applied.Id,Ct));
        Assert.Empty(await Work(h,s=>s.GetNoraRemindersAsync(user.Id,Ct)));
        await Agent(h,s=>s.UndoAgentTurnAsync(user.Id,applied.Id,Ct));
        Assert.Equal(1,await h.DbAsync(db=>db.InAppNotifications.CountAsync(Ct)));
    }

    [Fact]
    public async Task NORA_can_list_and_cancel_by_name_and_undo_a_cancellation()
    {
        var ai=Model(new("Create","Gọi khách hàng"));
        await using var h=await ServiceHarness.CreateAsync(s=>s.AddSingleton<ITaskInterpreter>(ai));var user=await h.RegisterAsync();
        await Agent(h,s=>s.SendAgentTurnAsync(user.Id,Input("10 phút nữa nhắc gọi khách hàng"),Ct));
        ai.Response=_=>AgentAiTestDouble.Empty() with{Reminder=new("List")};
        var list=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,Input("Tôi có lời nhắc nào?"),Ct));
        Assert.Equal("ANSWERED",list.Status);Assert.Contains("Gọi khách hàng",list.Reply);
        ai.Response=_=>AgentAiTestDouble.Empty() with{Reminder=new("Cancel","Gọi khách hàng")};
        var cancelled=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,Input("Hủy nhắc gọi khách hàng"),Ct));
        Assert.Equal("APPLIED",cancelled.Status);Assert.Empty(await Work(h,s=>s.GetNoraRemindersAsync(user.Id,Ct)));
        await Agent(h,s=>s.UndoAgentTurnAsync(user.Id,cancelled.Id,Ct));
        Assert.Single(await Work(h,s=>s.GetNoraRemindersAsync(user.Id,Ct)));
    }

    [Theory]
    [InlineData("Nhắc tôi gọi khách")]
    [InlineData("1 phút nữa nhắc tôi")]
    public async Task Missing_time_or_message_asks_a_question_without_saving(string text)
    {
        var ai=Model(new("Create",text.StartsWith("1")?"":"Gọi khách"));
        await using var h=await ServiceHarness.CreateAsync(s=>s.AddSingleton<ITaskInterpreter>(ai));var user=await h.RegisterAsync();
        var result=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,Input(text),Ct));
        Assert.Equal("NEEDS_INPUT",result.Status);Assert.NotEmpty(result.Question);
        Assert.Empty(await Work(h,s=>s.GetNoraRemindersAsync(user.Id,Ct)));
    }

    [Theory]
    [InlineData("0 phút nữa nhắc gọi khách")]
    [InlineData("31 ngày nữa nhắc gọi khách")]
    public async Task Invalid_delays_do_not_save(string text)
    {
        var ai=Model(new("Create","Gọi khách"));
        await using var h=await ServiceHarness.CreateAsync(s=>s.AddSingleton<ITaskInterpreter>(ai));var user=await h.RegisterAsync();
        await Assert.ThrowsAsync<AuthException>(()=>Agent(h,s=>s.SendAgentTurnAsync(user.Id,Input(text),Ct)));
        Assert.Empty(await Work(h,s=>s.GetNoraRemindersAsync(user.Id,Ct)));
    }

    [Fact]
    public async Task Legacy_Inbox_alarms_move_to_NORA_without_changing_their_deadline_and_new_notes_do_not_create_alarms()
    {
        await using var h=await ServiceHarness.CreateAsync();var user=await h.RegisterAsync();var at=h.Clock.GetUtcNow().AddMinutes(5);
        await h.DbAsync(async db=>{db.InAppNotifications.Add(new(){UserId=user.Id,Kind="INBOX_REMINDER",Title="Inbox",Message="Lời nhắc cũ",ScheduledFor=at,CreatedAt=at,DedupeKey="inbox-reminder:legacy"});await db.SaveChangesAsync(Ct);});
        var old=Assert.Single(await Work(h,s=>s.GetNoraRemindersAsync(user.Id,Ct)));Assert.Equal(at,old.At);
        Assert.Equal("NORA_REMINDER",await h.DbAsync(db=>db.InAppNotifications.Select(x=>x.Kind).SingleAsync(Ct)));
        var note=await Work(h,s=>s.SaveNoteAsync(user.Id,null,new("Ghi chú","1 phút nữa rep mail"),Ct));
        Assert.Null(note.InboxReminderAt);Assert.Single(await Work(h,s=>s.GetNoraRemindersAsync(user.Id,Ct)));
        await Work(h,async s=>{await s.DeleteNoteAsync(user.Id,note.Id,note.Version,Ct);return true;});
        Assert.Single(await Work(h,s=>s.GetNoraRemindersAsync(user.Id,Ct)));
    }
}
