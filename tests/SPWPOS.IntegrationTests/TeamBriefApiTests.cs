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
    [Fact]
    public async Task Manual_team_draft_accepts_thirty_days_without_calling_ai()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory();
        using var client = await factory.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var response = await client.PostAsJsonAsync("/api/teams", new { name = "Manual drafts" }, ct);
        response.EnsureSuccessStatusCode();
        using var team = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var teamId = team.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var now = factory.Clock.GetUtcNow();
        var input = new { name = "Report", description = "Review notes", estimatedMinutes = 90,
            deadline = now.AddDays(30), priority = 2, splittable = true };
        var path = $"/api/teams/{teamId}/drafts/manual";
        var preview = await client.PostAsJsonAsync(path, input, ct);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, input with { deadline = now.AddDays(30).AddSeconds(1) }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, input with { deadline = now.AddMinutes(5) }, ct)).StatusCode);
    }

    [Fact] public async Task Upload_larger_than_standard_request_limit_is_accepted_only_for_briefs_and_confirm_is_idempotent()
    {
        var ct = TestContext.Current.CancellationToken; await using var factory = new Factory(); using var client = await factory.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var create = await client.PostAsJsonAsync("/api/teams", new { name = "API brief fixture" }, ct); Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var team = JsonDocument.Parse(await create.Content.ReadAsStringAsync(ct)); var teamId = team.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var bytes = new byte[3 * 1024 * 1024]; Encoding.ASCII.GetBytes("%PDF-1.4").CopyTo(bytes, 0);
        using var invalidCount = await client.PostAsJsonAsync($"/api/teams/{teamId}/briefs", new TeamBriefInput("hạn sau hai ngày", TaskCount: 2), ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCount.StatusCode);
        var input = new TeamBriefInput("hạn sau hai ngày", new("fixture.pdf", "application/pdf", Convert.ToBase64String(bytes)), TaskCount: 1);
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
        await using var verify = factory.Services.CreateAsyncScope(); var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.TeamTasks.CountAsync(ct)); Assert.Equal(1, await db.TeamTaskDocuments.CountAsync(ct));
        var taskId = await db.TeamTasks.Select(x => x.Id).SingleAsync(ct);
        var detail = await client.GetStringAsync($"/api/teams/{teamId}", ct); Assert.DoesNotContain(input.File!.Data, detail);
        using var group = JsonDocument.Parse(detail);
        var metadata = Assert.Single(group.RootElement.GetProperty("data").GetProperty("tasks").EnumerateArray()).GetProperty("sourceFiles");
        Assert.Equal(id, Assert.Single(metadata.EnumerateArray()).GetProperty("id").GetGuid());
        var file = await client.GetAsync($"/api/teams/tasks/{taskId}/source/files/{id}", ct);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode); Assert.Equal(bytes, await file.Content.ReadAsByteArrayAsync(ct));
        Assert.Equal("attachment", file.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("application/pdf", file.Content.Headers.ContentType!.MediaType);
        Assert.Contains("sandbox", file.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(file.Headers.CacheControl!.NoStore); Assert.Equal("nosniff", file.Headers.GetValues("X-Content-Type-Options").Single());
        var legacy = new SPWPOS.Domain.Entities.TeamTask { TeamId = teamId, Name = "Task chưa có tài liệu", Deadline = factory.Clock.GetUtcNow().AddDays(2), EstimatedMinutes = 60 };
        db.TeamTasks.Add(legacy); await db.SaveChangesAsync(ct);
        var attachment = new TeamSourceFileInput(legacy.Version, Guid.NewGuid(), input.File);
        async Task<HttpResponseMessage> Attach()
        {
            using var body = new StringContent(JsonSerializer.Serialize(attachment, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json");
            return await client.PostAsync($"/api/teams/tasks/{legacy.Id}/source", body, ct);
        }
        Assert.Equal(HttpStatusCode.OK, (await Attach()).StatusCode); Assert.Equal(HttpStatusCode.OK, (await Attach()).StatusCode);
        Assert.Equal(2, await db.TeamTaskDocuments.CountAsync(ct));
        var uploaded = await client.GetAsync($"/api/teams/tasks/{legacy.Id}/source/files/{attachment.RequestId}", ct);
        Assert.Equal(bytes, await uploaded.Content.ReadAsByteArrayAsync(ct));
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/teams/tasks/{taskId}/source/files/{id}", ct)).StatusCode);
    }
}
