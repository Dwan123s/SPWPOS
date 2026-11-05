using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;

namespace SPWPOS.IntegrationTests;

public sealed class NoraReminderApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Factory : ApiFactory
    {
        public AgentAiTestDouble Ai {get;}=new(){Response=_=>AgentAiTestDouble.Empty() with {Reminder=new("Create","Trả lời email khách hàng")}};
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {base.ConfigureWebHost(builder);builder.ConfigureServices(s=>s.AddSingleton<ITaskInterpreter>(Ai));}
    }
    [Fact]
    public async Task Chat_creates_a_reminder_due_feed_delivers_it_and_explicit_acknowledgement_prevents_ringing_again()
    {
        await using var factory=new Factory();using var client=await factory.ClientAsync();await Authorize(factory,client);
        var input=new AgentTurnInput(Guid.NewGuid(),Guid.NewGuid(),"1 phút nữa nhắc tôi trả lời email khách hàng");
        var response=(await Send(client,HttpMethod.Post,"/api/agent/turns",input)).GetProperty("data");
        Assert.Equal("APPLIED",response.GetProperty("status").GetString());
        var reminder=Assert.Single((await Send(client,HttpMethod.Get,"/api/agent/reminders")).GetProperty("data").EnumerateArray());
        var id=reminder.GetProperty("id").GetGuid();Assert.False(reminder.GetProperty("due").GetBoolean());
        Assert.Empty((await Send(client,HttpMethod.Get,"/api/notes")).GetProperty("data").EnumerateArray());
        Assert.Empty((await Send(client,HttpMethod.Get,"/api/notifications")).GetProperty("data").EnumerateArray());
        await Send(client,HttpMethod.Post,"/api/agent/turns",input);
        Assert.Single((await Send(client,HttpMethod.Get,"/api/agent/reminders")).GetProperty("data").EnumerateArray());
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var notice=Assert.Single((await Send(client,HttpMethod.Get,"/api/notifications")).GetProperty("data").EnumerateArray());
        Assert.Equal(id,notice.GetProperty("id").GetGuid());Assert.Equal("NORA_REMINDER",notice.GetProperty("kind").GetString());
        Assert.True(Assert.Single((await Send(client,HttpMethod.Get,"/api/agent/reminders")).GetProperty("data").EnumerateArray()).GetProperty("due").GetBoolean());
        await Send(client,HttpMethod.Post,$"/api/notifications/{id}/read");
        Assert.Empty((await Send(client,HttpMethod.Get,"/api/agent/reminders")).GetProperty("data").EnumerateArray());
        factory.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Empty((await Send(client,HttpMethod.Get,"/api/agent/reminders")).GetProperty("data").EnumerateArray());
    }
    [Fact]
    public async Task Reminder_management_requires_authentication_and_cancellation_silences_future_reminders()
    {
        await using var factory=new Factory();using var denied=await factory.ClientAsync();
        using(var response=await denied.GetAsync("/api/agent/reminders",Ct))Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
        using var client=await factory.ClientAsync();await Authorize(factory,client);
        await Send(client,HttpMethod.Post,"/api/agent/turns",new AgentTurnInput(Guid.NewGuid(),Guid.NewGuid(),"5 phút nữa nhắc tôi trả lời email khách hàng"));
        var reminder=Assert.Single((await Send(client,HttpMethod.Get,"/api/agent/reminders")).GetProperty("data").EnumerateArray());
        await Send(client,HttpMethod.Delete,$"/api/agent/reminders/{reminder.GetProperty("id").GetGuid()}");
        Assert.Empty((await Send(client,HttpMethod.Get,"/api/agent/reminders")).GetProperty("data").EnumerateArray());
        factory.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Empty((await Send(client,HttpMethod.Get,"/api/notifications")).GetProperty("data").EnumerateArray());
    }
}
