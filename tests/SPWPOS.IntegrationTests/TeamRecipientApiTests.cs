using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class TeamRecipientApiTests
{
    [Fact] public async Task Confirm_accepts_named_recipient_and_legacy_empty_body_and_recipient_changes_are_versioned()
    {
        var ct = TestContext.Current.CancellationToken; await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
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
