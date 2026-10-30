using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class RecoveryConcurrencyTests
{
    public static bool SqlServerAvailable=>SqlServerConcurrencyTests.SqlServerAvailable;
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    [Fact(SkipUnless=nameof(SqlServerAvailable),Skip="Requires isolated SQL Server."),Trait("Category","SqlServer")]
    public async Task Concurrent_scans_and_applies_create_one_case_and_one_schedule()
    {
        await using var factory=new ApiFactory(true);using var client=await factory.ClientAsync();
        await WorkspaceFlowTests.Authorize(factory,client);await WorkspaceFlowTests.SetupTime(client);
        Guid caseId;
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var uid=await db.Users.Select(x=>x.Id).SingleAsync(Ct);var now=factory.Clock.GetUtcNow();
            var task=new WorkTask{UserId=uid,Name="Recovery SQL",Deadline=now.AddDays(2),EstimatedMinutes=60,IsHardDeadline=true,CreatedAt=now.AddDays(-4)};
            db.WorkTasks.Add(task);db.WorkSessions.AddRange(new[]{1,3}.Select(day=>new WorkSession{UserId=uid,TaskId=task.Id,Start=now.AddDays(-day),End=now.AddDays(-day).AddHours(1),Status=WorkSessionStatus.Missed}));await db.SaveChangesAsync(Ct);
        }
        var scans=await Task.WhenAll(client.PostAsJsonAsync("/api/recovery/scan",new{},Ct),client.PostAsJsonAsync("/api/recovery/scan",new{},Ct));
        await AssertOptimisticResults(scans);
        using(var retry=await client.PostAsJsonAsync("/api/recovery/scan",new{},Ct)) Assert.Equal(HttpStatusCode.OK,retry.StatusCode);
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();caseId=(await db.RecoveryCases.SingleAsync(Ct)).Id;
            Assert.Equal(1,await db.PlanProposals.CountAsync(Ct));Assert.Equal(0,await db.WorkSessions.CountAsync(x=>x.Status==WorkSessionStatus.Scheduled,Ct));
        }
        var applies=await Task.WhenAll(client.PostAsJsonAsync($"/api/recovery/{caseId}/apply",new{},Ct),client.PostAsJsonAsync($"/api/recovery/{caseId}/apply",new{},Ct));
        await AssertOptimisticResults(applies);
        using(var retry=await client.PostAsJsonAsync($"/api/recovery/{caseId}/apply",new{},Ct)) Assert.Equal(HttpStatusCode.OK,retry.StatusCode);
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();Assert.Equal(1,await db.WorkSessions.CountAsync(x=>x.Status==WorkSessionStatus.Scheduled,Ct));
            Assert.Equal("APPLIED",(await db.RecoveryCases.SingleAsync(Ct)).Status);
        }
    }
    private static async Task AssertOptimisticResults(HttpResponseMessage[] responses)
    {
        try {
            Assert.Contains(responses,r=>r.StatusCode==HttpStatusCode.OK);
            foreach(var response in responses) {
                Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict);
                if(response.StatusCode==HttpStatusCode.Conflict) {
                    var error=await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
                    Assert.Equal("CONCURRENCY_CONFLICT",error.GetProperty("error").GetProperty("code").GetString());
                }
            }
        } finally {foreach(var response in responses)response.Dispose();}
    }
}
