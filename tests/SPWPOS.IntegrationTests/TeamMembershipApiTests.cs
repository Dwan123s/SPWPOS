using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class TeamMembershipApiTests
{
    public static bool SqlServerAvailable => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SPWPOS_TEST_SQLSERVER"));
    [Fact] public Task Pending_join_and_removed_member_cannot_access_group_HTTP_routes() => MembershipFlow(false);
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires SPWPOS_TEST_SQLSERVER."), Trait("Category", "SqlServer")]
    public Task SQL_migration_and_membership_approval_preserve_group_access_controls() => MembershipFlow(true);

    private static async Task MembershipFlow(bool sql)
    {
        var ct = TestContext.Current.CancellationToken; await using var f = new ApiFactory(sql); using var client = await f.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(f, client); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var leaderId = Guid.NewGuid(); var teamId = Guid.NewGuid(); var taskId = Guid.NewGuid(); var fileId = Guid.NewGuid(); Guid userId;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); userId = await db.Users.Select(x => x.Id).SingleAsync(ct);
            db.Users.Add(new() { Id = leaderId, UserName = "leader", Email = "leader@example.com", NormalizedEmail = "LEADER@EXAMPLE.COM", FirstName = "Leader", IsActive = true, EmailConfirmed = true });
            db.TeamProjects.Add(new() { Id = teamId, CreatorId = leaderId, Name = "Approval fixture" });
            db.TeamMemberships.Add(new() { TeamId = teamId, UserId = leaderId, Role = "Leader" });
            db.TeamTaskDocuments.Add(new() { Id = fileId, TeamId = teamId, UploadedBy = leaderId, Name = "private.txt", Content = [65], MimeType = "text/plain", Size = 1 });
            db.TeamTasks.Add(new() { Id = taskId, TeamId = teamId, Name = "Private group task", EstimatedMinutes = 60, Deadline = f.Clock.GetUtcNow().AddDays(2), SourceDocumentId = fileId });
            await db.SaveChangesAsync(ct);
        }
        async Task<T> Use<T>(Func<IPrivateTeamService, Task<T>> action) { await using var scope = f.Services.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IPrivateTeamService>()); }
        async Task Act(Func<IPrivateTeamService, Task> action) { await Use(async s => { await action(s); return true; }); }
        var invite = await Use(s => s.CreateTeamInviteAsync(leaderId, teamId, ct));
        var response = await client.PostAsJsonAsync("/api/teams/join", new { text = invite.Code }, ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); Assert.Equal("Pending", result.RootElement.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/teams/join-requests", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/teams/{teamId}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/teams/{teamId}/messages", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/teams/tasks/{taskId}/source/files/{fileId}", ct)).StatusCode);
        var request = Assert.Single((await Use(s => s.GetTeamAsync(leaderId, teamId, ct))).JoinRequests!);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/teams/{teamId}/join-requests/{userId}/review", new TeamJoinReviewInput(request.Version, true), ct)).StatusCode);
        if (sql)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<bool> Approve()
            {
                await gate.Task;
                try { await Act(s => s.ReviewTeamJoinRequestAsync(leaderId, teamId, userId, new(request.Version, true), ct)); return true; }
                catch (AuthException e) when (e.Code == "JOIN_REQUEST_STALE") { return false; }
                catch (DbUpdateConcurrencyException) { return false; }
            }
            var first = Approve(); var second = Approve(); gate.SetResult();
            Assert.Single(await Task.WhenAll(first, second), x => x);
        }
        else await Act(s => s.ReviewTeamJoinRequestAsync(leaderId, teamId, userId, new(request.Version, true), ct));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/teams/{teamId}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/teams/tasks/{taskId}/source/files/{fileId}", ct)).StatusCode);
        var member = (await Use(s => s.GetTeamAsync(leaderId, teamId, ct))).Members.Single(x => x.UserId == userId);
        if (sql)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<bool> Kick()
            {
                await gate.Task;
                try { await Act(s => s.RemoveTeamMemberAsync(leaderId, teamId, userId, member.Version, ct)); return true; }
                catch (DbUpdateConcurrencyException) { return false; }
            }
            async Task Chat()
            {
                await gate.Task;
                try { await Use(s => s.SendTeamChatAsync(userId, teamId, new(Guid.NewGuid(), "In-flight message"), ct)); }
                catch (AuthException e) when (e.Code == "TEAM_FORBIDDEN") { }
                catch (DbUpdateConcurrencyException) { }
            }
            var kicked = Kick(); var chatting = Chat(); gate.SetResult(); await chatting;
            if (!await kicked) await Act(s => s.RemoveTeamMemberAsync(leaderId, teamId, userId, member.Version, ct));
        }
        else await Act(s => s.RemoveTeamMemberAsync(leaderId, teamId, userId, member.Version, ct));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/teams/{teamId}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/teams/tasks/{taskId}/source/files/{fileId}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/teams/join", new { text = invite.Code }, ct)).StatusCode);
    }
}
