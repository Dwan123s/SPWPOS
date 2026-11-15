using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class TeamRecipientApiTests
{
    private sealed class FixedClockFactory : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // JWT validation must use the same clock as token creation in these
            // fixed-date HTTP scenarios, including when CI runs on another day.
            builder.ConfigureTestServices(services => services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme, options =>
                    options.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, _) =>
                        expires.HasValue && expires.Value > Clock.GetUtcNow().UtcDateTime &&
                        (!notBefore.HasValue || notBefore.Value <= Clock.GetUtcNow().UtcDateTime)));
        }
    }
    [Theory]
    [InlineData("2026-11-11T08:00:00Z", 0)]
    [InlineData("2026-11-15T08:00:00Z", 1)]
    public async Task Confirm_accepts_named_recipient_and_legacy_empty_body_and_recipient_changes_are_versioned(string now, int reportWeek)
    {
        var ct = TestContext.Current.CancellationToken; await using var factory = new FixedClockFactory(); using var client = await factory.ClientAsync();
        factory.Clock.Advance(DateTimeOffset.Parse(now) - factory.Clock.GetUtcNow());
        var tokens = await AuthFlowTests.RegisterLoginAsync(factory, client); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var teamId = Guid.NewGuid(); var recipient = Guid.NewGuid(); var singleId = Guid.NewGuid(); var openId = Guid.NewGuid();
        var singleDraft = Guid.NewGuid(); var openDraft = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var uid = await db.Users.Select(x => x.Id).SingleAsync(ct);
            db.Users.Add(new() { Id = recipient, UserName = "Recipient", NormalizedUserName = "RECIPIENT", Email = "recipient@example.com", NormalizedEmail = "RECIPIENT@EXAMPLE.COM", FirstName = "Bình", IsActive = true, EmailConfirmed = true });
            db.TeamProjects.Add(new() { Id = teamId, CreatorId = uid, Name = "Recipient API" });
            var leader = new TeamMembership { TeamId = teamId, UserId = uid, Role = "Leader" };
            db.TeamMemberships.AddRange(leader, new() { TeamId = teamId, UserId = recipient });
            foreach (var (draftId, taskId) in new[] { (singleDraft, singleId), (openDraft, openId) })
            {
                var task = new SharedTaskView(taskId, "Task API", "Yêu cầu", 60, factory.Clock.GetUtcNow().AddDays(2), 2, true, null, 0, "Open", Guid.NewGuid(), false);
                db.PrivatePlanningProposals.Add(new() { Id = draftId, UserId = uid, Kind = "TEAM_DRAFT", ExpiresAt = factory.Clock.GetUtcNow().AddMinutes(20),
                    PayloadJson = JsonSerializer.Serialize(new { TeamId = teamId, MembershipVersion = leader.Version, Task = task }) });
            }
            await db.SaveChangesAsync(ct);
        }
        var named = $"/api/teams/{teamId}/drafts/{singleDraft}/confirm";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(named, new TeamTaskRecipientInput(recipient), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(named, new TeamTaskRecipientInput(recipient), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(named, new TeamTaskRecipientInput(), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/teams/{teamId}/drafts/{openDraft}/confirm", null, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/teams/tasks/{singleId}/preview", null, ct)).StatusCode);
        using var report = await client.GetAsync($"/api/teams/{teamId}/workload?week={reportWeek}", ct);
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        var json = await report.Content.ReadAsStringAsync(ct);
        using var payload = JsonDocument.Parse(json);
        var reportedTasks = payload.RootElement.GetProperty("data").GetProperty("tasks").EnumerateArray().ToArray();
        Assert.Equal(2, reportedTasks.Length);
        var designated = Assert.Single(reportedTasks, task => task.GetProperty("id").GetGuid() == singleId);
        Assert.Equal(recipient, designated.GetProperty("designatedUserId").GetGuid());
        var open = Assert.Single(reportedTasks, task => task.GetProperty("id").GetGuid() == openId);
        Assert.Equal(JsonValueKind.Null, open.GetProperty("designatedUserId").ValueKind);
        Assert.DoesNotContain("schedulableMinutes", json);
        Assert.DoesNotContain("workingMinutes", json);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/teams/{teamId}/workload/{singleId}/candidates", ct)).StatusCode);
        Guid version;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var task = await db.TeamTasks.SingleAsync(t => t.Id == singleId, ct);
            Assert.Equal(recipient, task.DesignatedUserId); Assert.Null(task.AssigneeId); version = task.Version;
            Assert.Null((await db.TeamTasks.SingleAsync(t => t.Id == openId, ct)).DesignatedUserId);
            Assert.Equal(0, await db.WorkTasks.CountAsync(ct)); Assert.Equal(1, await db.InAppNotifications.CountAsync(n => n.Kind == "TEAM_TASK_DESIGNATED", ct));
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/teams/tasks/{singleId}/recipient", new TeamTaskRecipientChangeInput(version, null), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/teams/tasks/{singleId}/recipient", new TeamTaskRecipientChangeInput(version, recipient), ct)).StatusCode);
        // Replaying the original creation cannot overwrite a later recipient change.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(named, new TeamTaskRecipientInput(recipient), ct)).StatusCode);
        await using var verify = factory.Services.CreateAsyncScope(); Assert.Null((await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().TeamTasks.SingleAsync(t => t.Id == singleId, ct)).DesignatedUserId);
    }
}
