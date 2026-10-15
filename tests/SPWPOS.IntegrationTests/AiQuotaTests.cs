using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class AiQuotaTests
{
    public static bool SqlServerAvailable=>SqlServerConcurrencyTests.SqlServerAvailable;
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private sealed class Factory:ApiFactory
    {
        public Factory():base(true){}
        public AgentAiTestDouble Ai{get;}=new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {base.ConfigureWebHost(builder);builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?> { ["AiLimits:DailyRequests"]="1",["AiLimits:ConcurrentRequests"]="1" }));builder.ConfigureServices(s=>s.AddSingleton<ITaskInterpreter>(Ai));}
    }
    [Fact(SkipUnless=nameof(SqlServerAvailable),Skip="Requires isolated SQL Server."),Trait("Category","SqlServer")]
    public async Task Concurrent_calls_cannot_overspend_a_single_daily_reservation_and_retry_does_not_charge_again()
    {
        await using var factory=new Factory();using var client=await factory.ClientAsync();await WorkspaceFlowTests.Authorize(factory,client);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Ai.BeforeReply=async()=>{entered.TrySetResult();await finish.Task.WaitAsync(TimeSpan.FromSeconds(15),Ct);};
        var input=new AgentTurnInput(Guid.NewGuid(),Guid.NewGuid(),"Xem lịch hôm nay",false);
        var first=client.PostAsJsonAsync("/api/agent/turns",input,Ct);await entered.Task.WaitAsync(TimeSpan.FromSeconds(15),Ct);
        try
        {
            using var second=await client.PostAsJsonAsync("/api/agent/turns",new AgentTurnInput(Guid.NewGuid(),Guid.NewGuid(),"Xem lịch ngày mai",false),Ct);
            Assert.Equal(HttpStatusCode.TooManyRequests,second.StatusCode);
        }
        finally {finish.TrySetResult();}
        using var firstResponse=await first;Assert.Equal(HttpStatusCode.OK,firstResponse.StatusCode);
        using var replay=await client.PostAsJsonAsync("/api/agent/turns",input,Ct);Assert.Equal(HttpStatusCode.OK,replay.StatusCode);Assert.Equal(1,factory.Ai.Calls);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var record=await db.AiUsageRecords.SingleAsync(Ct);Assert.Equal("COMPLETED",record.Status);Assert.Null(record.InputTokens);Assert.Null(record.OutputTokens);
        var usage=await scope.ServiceProvider.GetRequiredService<IAiUsageService>().SummaryAsync(record.UserId,Ct);Assert.Equal(1,usage.Used);Assert.Equal(0,usage.Active);Assert.Equal(1,usage.UnknownUsageCalls);
    }
}
