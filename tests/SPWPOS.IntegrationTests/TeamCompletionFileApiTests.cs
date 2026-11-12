using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class TeamCompletionFileApiTests
{
    [Fact] public async Task Large_report_submission_is_saved_once_and_download_has_attachment_headers_and_requires_login()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = new ApiFactory(); using var client = await f.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(f, client); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var taskId = Guid.NewGuid(); var version = Guid.NewGuid();
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var uid = await db.Users.Select(x => x.Id).SingleAsync(ct);
            var team = new TeamProject { Name = "Report API fixture", CreatorId = uid }; db.TeamProjects.Add(team);
            db.TeamMemberships.Add(new() { TeamId = team.Id, UserId = uid, Role = "Leader" });
            db.WorkPreferences.Add(new() { UserId = uid });
            var personal = new WorkTask { UserId = uid, Name = "Report", Deadline = f.Clock.GetUtcNow().AddDays(2), EstimatedMinutes = 60 };
            db.WorkTasks.Add(personal); db.TeamTasks.Add(new() { Id = taskId, TeamId = team.Id, AssigneeId = uid, Status = "Assigned", Version = version, Name = "Report", Deadline = personal.Deadline!.Value, EstimatedMinutes = 60 });
            db.TeamTaskAssignments.Add(new() { TeamTaskId = taskId, WorkTaskId = personal.Id, UserId = uid, ProposalId = Guid.NewGuid() }); await db.SaveChangesAsync(ct);
        }
        var content = new byte[3 * 1024 * 1024]; Encoding.ASCII.GetBytes("%PDF-1.4").CopyTo(content, 0);
        var input = new TeamCompletionSubmitInput(version, Guid.NewGuid(), "Kết quả để duyệt", [new("Report.pdf", Convert.ToBase64String(content))]);
        async Task<HttpResponseMessage> Send()
        {
            using var body = new StringContent(JsonSerializer.Serialize(input, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json");
            return await client.PostAsync($"/api/teams/tasks/{taskId}/completion/submit", body, ct);
        }
        Assert.Equal(HttpStatusCode.OK, (await Send()).StatusCode); Assert.Equal(HttpStatusCode.OK, (await Send()).StatusCode);
        Guid fileId;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            fileId = (await db.TeamCompletionFiles.SingleAsync(ct)).Id;
            Assert.Equal(1, await db.TeamCompletionSubmissions.CountAsync(ct)); Assert.Equal("PendingReview", (await db.TeamTasks.SingleAsync(ct)).Status);
        }
        var download = await client.GetAsync($"/api/teams/tasks/{taskId}/completion/files/{fileId}", ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode); Assert.Equal(content, await download.Content.ReadAsByteArrayAsync(ct));
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
        Assert.Contains("sandbox", download.Headers.GetValues("Content-Security-Policy").Single());
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/teams/tasks/{taskId}/completion/files/{fileId}", ct)).StatusCode);
    }
}
