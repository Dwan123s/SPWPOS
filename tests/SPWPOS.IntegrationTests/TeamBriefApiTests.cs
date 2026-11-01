using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class TeamBriefApiTests
{
    private sealed class Factory : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(s => s.AddSingleton<ITaskInterpreter>(new AgentAiTestDouble { BriefResponse = context => new("Dự án API", "", "Chia theo thành viên hiện tại",
                Enumerable.Range(1, context.MemberCount).Select(i => new TeamBriefTask("Task API " + i, "Tóm tắt", ["Yêu cầu"], ["Báo cáo"], ["Đủ dữ liệu"], [], [], 60, context.Now.AddDays(2), "hạn sau hai ngày")).ToArray(), []) }));
        }
    }
    [Fact] public async Task Upload_larger_than_standard_request_limit_is_accepted_only_for_briefs_and_confirm_is_idempotent()
    {
        var ct = TestContext.Current.CancellationToken; await using var factory = new Factory(); using var client = await factory.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var create = await client.PostAsJsonAsync("/api/teams", new { name = "API brief fixture" }, ct); Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var team = JsonDocument.Parse(await create.Content.ReadAsStringAsync(ct)); var teamId = team.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var bytes = new byte[3 * 1024 * 1024]; Encoding.ASCII.GetBytes("%PDF-1.4").CopyTo(bytes, 0);
        var input = new TeamBriefInput("hạn sau hai ngày", new("fixture.pdf", "application/pdf", Convert.ToBase64String(bytes)));
        using var uploadBody = new StringContent(JsonSerializer.Serialize(input, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json");
        var upload = await client.PostAsync($"/api/teams/{teamId}/briefs", uploadBody, ct);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        using var response = JsonDocument.Parse(await upload.Content.ReadAsStringAsync(ct)); var data = response.RootElement.GetProperty("data");
        Assert.Equal(1, data.GetProperty("memberCount").GetInt32()); Assert.Single(data.GetProperty("tasks").EnumerateArray());
        var id = data.GetProperty("proposalId").GetGuid();
        using var regularBody = new StringContent(JsonSerializer.Serialize(input, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json");
        var regular = await client.PostAsync($"/api/teams/{teamId}/drafts", regularBody, ct); Assert.Equal(HttpStatusCode.RequestEntityTooLarge, regular.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope()) Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().TeamTasks.CountAsync(ct));
        var first = await client.PostAsJsonAsync($"/api/teams/{teamId}/briefs/{id}/confirm", new { }, ct); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var retry = await client.PostAsJsonAsync($"/api/teams/{teamId}/briefs/{id}/confirm", new { }, ct); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        await using var verify = factory.Services.CreateAsyncScope(); Assert.Equal(1, await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().TeamTasks.CountAsync(ct));
    }
}
