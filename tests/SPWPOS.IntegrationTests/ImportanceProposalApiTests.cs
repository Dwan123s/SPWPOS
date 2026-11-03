using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class ImportanceProposalApiTests
{
    private sealed class Factory : ApiFactory
    {
        public AgentAiTestDouble Ai { get; } = new() { ImportanceResponse = c => new("Important", "Đầu ra bắt buộc để hoàn thành môn học", .75, c.Name) };
        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            base.ConfigureWebHost(builder); builder.ConfigureTestServices(s => s.AddSingleton<ITaskInterpreter>(Ai));
        }
    }
    [Fact] public async Task Proposal_endpoint_returns_version_and_advice_but_requires_separate_confirmation_to_save()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = new Factory(); using var client = await f.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(f, client); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var task = new WorkTask { Name = "Nghiệm thu proposal SSG105", Description = "Hoàn thành bài nộp bắt buộc", Deadline = f.Clock.GetUtcNow().AddDays(3), EstimatedMinutes = 60 };
        await using (var scope = f.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); task.UserId = await db.Users.Select(u => u.Id).SingleAsync(ct);
            db.WorkTasks.Add(task); await db.SaveChangesAsync(ct);
        }
        var endpoint = $"/api/tasks/{task.Id}/importance/proposal";
        var response = await client.PostAsJsonAsync(endpoint, new ImportanceProposalInput(task.Version), ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var proposal = data.RootElement.GetProperty("data");
        Assert.Equal(task.Version, proposal.GetProperty("version").GetGuid()); Assert.Equal("Important", proposal.GetProperty("suggestion").GetProperty("level").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(endpoint, new ImportanceProposalInput(task.Version), ct)).StatusCode); Assert.Equal(1, f.Ai.Calls);
        await using (var scope = f.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var unchanged = await db.WorkTasks.SingleAsync(ct);
            Assert.Equal("Unknown", unchanged.ImportanceLevel); Assert.Equal(task.Version, unchanged.Version); Assert.Equal(0, await db.WorkSessions.CountAsync(ct));
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/tasks/{task.Id}/importance", new ImportanceInput(task.Version, "Lower", "Tôi phụ trách phần hỗ trợ"), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(endpoint, new ImportanceProposalInput(task.Version), ct)).StatusCode);
        await using (var scope = f.Services.CreateAsyncScope()) Assert.Equal("Lower", (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().WorkTasks.SingleAsync(ct)).ImportanceLevel);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(endpoint, new ImportanceProposalInput(task.Version), ct)).StatusCode);
    }
}
