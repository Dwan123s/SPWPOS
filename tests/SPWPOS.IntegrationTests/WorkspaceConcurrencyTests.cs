using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;
namespace SPWPOS.IntegrationTests;

public sealed class WorkspaceConcurrencyTests
{
    public static bool SqlServerAvailable => SqlServerConcurrencyTests.SqlServerAvailable;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires SPWPOS_TEST_SQLSERVER."), Trait("Category", "SqlServer")]
    public async Task Concurrent_proposal_application_commits_exactly_once()
    {
        await using var factory = new ApiFactory(true); using var client = await factory.ClientAsync(); await Authorize(factory, client); await SetupTime(client);
        await Create(client, factory.Clock.GetUtcNow());
        var proposal = (await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(factory.Clock.GetUtcNow(), 7))).GetProperty("data");
        var route = $"/api/plans/{proposal.GetProperty("id").GetGuid()}/apply";
        var responses = await Task.WhenAll(client.PostAsync(route, null, Ct), client.PostAsync(route, null, Ct));
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in responses) response.Dispose(); }
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(proposal.GetProperty("sessions").GetArrayLength(), await db.WorkSessions.CountAsync(Ct));
    }
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires SPWPOS_TEST_SQLSERVER."), Trait("Category", "SqlServer")]
    public async Task Concurrent_session_start_has_one_running_session()
    {
        await using var factory = new ApiFactory(true); using var client = await factory.ClientAsync(); await Authorize(factory, client); await SetupTime(client);
        await Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 90, 0, 0, 1440));
        await Create(client, factory.Clock.GetUtcNow());
        var proposal = (await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(factory.Clock.GetUtcNow(), 1))).GetProperty("data");
        await Send(client, HttpMethod.Post, $"/api/plans/{proposal.GetProperty("id").GetGuid()}/apply");
        var route = $"/api/sessions/{proposal.GetProperty("sessions")[0].GetProperty("id").GetGuid()}/start";
        var responses = await Task.WhenAll(client.PostAsync(route, null, Ct), client.PostAsync(route, null, Ct));
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest); }
        finally { foreach (var response in responses) response.Dispose(); }
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().WorkSessions.CountAsync(x => x.Status == WorkSessionStatus.Running, Ct));
    }
    [Fact] public async Task Admin_can_lock_other_user_and_revokes_existing_session()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client);
        Guid admin;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var user = (await users.FindByEmailAsync(ServiceHarness.Address))!; admin = user.Id;
            Assert.True((await roles.CreateAsync(new IdentityRole<Guid>("Admin"))).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        }
        var login = (await Send(client, HttpMethod.Post, "/api/auth/login", new { userName = ServiceHarness.UserName, email = ServiceHarness.Address, password = ServiceHarness.Password })).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        using var other = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") }); const string email = "locked@example.com";
        await Send(other, HttpMethod.Post, "/api/auth/register", new { userName = "other.member", email, password = ServiceHarness.Password, confirmPassword = ServiceHarness.Password, firstName = "Lock", lastName = "Test" });
        await Send(other, HttpMethod.Post, "/api/auth/verify-email", new { email, otp = factory.Email.Inbox[(email, SPWPOS.Domain.Enums.OtpPurpose.EmailVerification)] });
        var otherLogin = (await Send(other, HttpMethod.Post, "/api/auth/login", new { email, password = ServiceHarness.Password })).GetProperty("data");
        other.DefaultRequestHeaders.Authorization = new("Bearer", otherLogin.GetProperty("accessToken").GetString());
        var me = (await Send(other, HttpMethod.Get, "/api/auth/me")).GetProperty("data"); var target = me.GetProperty("id").GetGuid();
        await Send(client, HttpMethod.Post, $"/api/admin/users/{admin}/lock", expected: HttpStatusCode.BadRequest);
        await Send(client, HttpMethod.Post, $"/api/admin/users/{target}/lock");
        await Send(other, HttpMethod.Get, "/api/auth/me", expected: HttpStatusCode.Unauthorized);
        await Send(client, HttpMethod.Post, $"/api/admin/users/{target}/unlock");
        await Send(other, HttpMethod.Post, "/api/auth/login", new { email, password = ServiceHarness.Password });
        await Send(other, HttpMethod.Get, "/api/auth/me", expected: HttpStatusCode.Unauthorized);
    }
}
