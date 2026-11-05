using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Email;
using SPWPOS.Infrastructure.Telegram;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed class NoraReminderDeliveryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Gateway : ITelegramGateway
    {
        public List<string> Messages {get;}=[];
        public Task<string> BotUsernameAsync(CancellationToken ct)=>Task.FromResult("test_bot");
        public Task<TelegramUpdate[]> UpdatesAsync(long offset,CancellationToken ct)=>Task.FromResult(Array.Empty<TelegramUpdate>());
        public Task SendAsync(long chatId,string text,CancellationToken ct){Messages.Add(text);return Task.CompletedTask;}
    }
    private static async Task<ServiceHarness> Harness(Gateway gateway)
    {
        return await ServiceHarness.CreateAsync(services=>{
            services.AddSingleton<ITelegramGateway>(gateway);
            services.Configure<TelegramOptions>(options=>{options.Enabled=true;options.BotToken="123456:abcdefghijklmnopqrstuvwxyz0123456789";});
            services.AddSingleton<ITaskInterpreter>(new AgentAiTestDouble {Response=_=>AgentAiTestDouble.Empty() with{Reminder=new("Create","Trả lời email khách hàng")}});
        });
    }
    private static async Task Work(ServiceHarness h,Func<IWorkspaceService,Task> action)
    {await using var scope=h.Provider.CreateAsyncScope();await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());}
    private static async Task Create(ServiceHarness h,Guid uid)
    {await using var scope=h.Provider.CreateAsyncScope();await scope.ServiceProvider.GetRequiredService<IPlanningAgentService>().SendAgentTurnAsync(uid,new(Guid.NewGuid(),Guid.NewGuid(),"1 phút nữa nhắc tôi trả lời email khách hàng"),Ct);}
    private static async Task Deliver(ServiceHarness h,Guid uid)
    {
        await using(var scope=h.Provider.CreateAsyncScope())await scope.ServiceProvider.GetRequiredService<ReminderNotificationService>().SendPendingAsync(uid,Ct);
        await using(var scope=h.Provider.CreateAsyncScope())await scope.ServiceProvider.GetRequiredService<TelegramNotificationService>().SendPendingAsync(uid,Ct);
    }
    private static Task Connect(ServiceHarness h,Guid uid)=>h.DbAsync(async db=>{
        db.TelegramConnections.Add(new(){UserId=uid,ChatId=123,Enabled=true,ConnectedAt=h.Clock.GetUtcNow()});await db.SaveChangesAsync(Ct);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Every_enabled_channel_delivers_once_only_when_due_even_without_a_web_tab(bool webEnabled)
    {
        var gateway=new Gateway();await using var h=await Harness(gateway);var user=await h.RegisterAsync();
        await Work(h,s=>s.SetNotificationSettingsAsync(user.Id,new(InAppEnabled:webEnabled,EmailEnabled:true),Ct));
        await Connect(h,user.Id);await Create(h,user.Id);
        var scheduled=await h.DbAsync(db=>db.InAppNotifications.SingleAsync(Ct));
        Assert.True(scheduled.EmailPending);Assert.True(scheduled.TelegramPending);Assert.Equal(webEnabled,scheduled.IsVisible);
        await Deliver(h,user.Id);Assert.Empty(h.Email.Reminders);Assert.Empty(gateway.Messages);
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await Deliver(h,user.Id);await Deliver(h,user.Id);
        Assert.Contains("Trả lời email",Assert.Single(h.Email.Reminders).Message);
        Assert.Contains("NORA",Assert.Single(gateway.Messages));
        await using var scope=h.Provider.CreateAsyncScope();
        var work=scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        Assert.Single(await work.GetNoraRemindersAsync(user.Id,Ct));
        Assert.Equal(webEnabled?1:0,(await work.GetNotificationsAsync(user.Id,Ct)).Length);
    }
    [Fact]
    public async Task Email_failure_does_not_block_Telegram_and_retry_does_not_duplicate_a_delivered_channel()
    {
        var gateway=new Gateway();await using var h=await Harness(gateway);var user=await h.RegisterAsync();
        await Work(h,s=>s.SetNotificationSettingsAsync(user.Id,new(EmailEnabled:true),Ct));await Connect(h,user.Id);await Create(h,user.Id);
        h.Clock.Advance(TimeSpan.FromMinutes(1));h.Email.FailDelivery=true;
        await Deliver(h,user.Id);Assert.Single(gateway.Messages);Assert.Empty(h.Email.Reminders);
        h.Email.FailDelivery=false;h.Clock.Advance(TimeSpan.FromMinutes(2));
        await Deliver(h,user.Id);Assert.Single(gateway.Messages);Assert.Single(h.Email.Reminders);
    }
    [Theory]
    [InlineData("cancel")]
    [InlineData("disable")]
    [InlineData("undo")]
    public async Task Cancelling_disabling_or_undoing_clears_delivery_before_the_alarm(string change)
    {
        var gateway=new Gateway();await using var h=await Harness(gateway);var user=await h.RegisterAsync();
        await Work(h,s=>s.SetNotificationSettingsAsync(user.Id,new(EmailEnabled:true),Ct));await Connect(h,user.Id);await Create(h,user.Id);
        if(change=="cancel")
            await Work(h,async s=>{var reminder=Assert.Single(await s.GetNoraRemindersAsync(user.Id,Ct));await s.CancelNoraReminderAsync(user.Id,reminder.Id,Ct);});
        else if(change=="undo")
        {
            await using var scope=h.Provider.CreateAsyncScope();var service=scope.ServiceProvider.GetRequiredService<IPlanningAgentService>();
            var turn=Assert.Single(await service.GetAgentTurnsAsync(user.Id,Ct));await service.UndoAgentTurnAsync(user.Id,turn.Id,Ct);
        }
        else
        {
            await Work(h,s=>s.SetNotificationSettingsAsync(user.Id,new(EmailEnabled:false),Ct));
            await h.DbAsync(async db=>{(await db.TelegramConnections.SingleAsync(Ct)).Enabled=false;await db.SaveChangesAsync(Ct);});
        }
        h.Clock.Advance(TimeSpan.FromMinutes(1));await Deliver(h,user.Id);
        Assert.Empty(h.Email.Reminders);Assert.Empty(gateway.Messages);
    }
    [Fact]
    public async Task Enabling_channels_after_creation_queues_a_future_reminder_on_the_background_tick()
    {
        var gateway=new Gateway();await using var h=await Harness(gateway);var user=await h.RegisterAsync();
        await Create(h,user.Id);await Connect(h,user.Id);
        await Work(h,s=>s.SetNotificationSettingsAsync(user.Id,new(EmailEnabled:true),Ct));
        h.Clock.Advance(TimeSpan.FromMinutes(1));await Work(h,s=>s.EnsureStartedNotificationsAsync(user.Id,Ct));
        await Deliver(h,user.Id);Assert.Single(gateway.Messages);Assert.Single(h.Email.Reminders);
    }
}
