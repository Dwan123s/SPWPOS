using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;
namespace SPWPOS.IntegrationTests;

public sealed class WorkspaceUpgradeApiTests
{
    public static bool SqlServerAvailable => SqlServerConcurrencyTests.SqlServerAvailable;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires isolated SQL Server."), Trait("Category", "SqlServer")]
    public async Task First_parallel_workspace_writes_return_conflict_instead_of_unique_constraint_error()
    {
        await using var f = new ApiFactory(true); using var client = await f.ClientAsync(); await Authorize(f, client);
        var results = await Task.WhenAll(System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(client, "/api/categories", new { name = "A", color = "#336699" }, Ct),
            System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(client, "/api/categories", new { name = "B", color = "#336699" }, Ct));
        try {
            Assert.Contains(results, x => x.IsSuccessStatusCode);
            Assert.All(results, x => Assert.True(x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
            await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await db.WorkPreferences.CountAsync(Ct));
            Assert.Equal(results.Count(x => x.IsSuccessStatusCode), await db.TaskCategories.CountAsync(Ct));
        } finally { foreach (var response in results) response.Dispose(); }
    }

    [Fact] public async Task Ambient_tracking_and_calendar_require_authentication_and_never_require_timer()
    {
        await using var f = new ApiFactory(); using var client = await f.ClientAsync();
        using var anonymous = await client.GetAsync("/api/workspace/calendar/export.ics", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await Authorize(f, client); Guid id;
        await using (var scope = f.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var uid = await db.Users.Select(x => x.Id).SingleAsync(Ct);
            var task = new WorkTask { UserId = uid, Name = "Viết báo cáo riêng", EstimatedMinutes = 60, Deadline = f.Clock.GetUtcNow().AddDays(1) };
            var session = new WorkSession { UserId = uid, TaskId = task.Id, Start = f.Clock.GetUtcNow().AddHours(-2), End = f.Clock.GetUtcNow().AddHours(-1) };
            id = session.Id; db.WorkTasks.Add(task); db.WorkSessions.Add(session); await db.SaveChangesAsync(Ct);
        }
        var reviews = (await Send(client, HttpMethod.Get, "/api/sessions/needs-review")).GetProperty("data");
        Assert.Equal(id, reviews[0].GetProperty("id").GetGuid());
        await Send(client, HttpMethod.Post, $"/api/sessions/{id}/catch-up", new CatchUpInput("COMPLETED"));
        var profile = (await Send(client, HttpMethod.Get, "/api/workspace/gamification/profile")).GetProperty("data");
        Assert.Equal(39, profile.GetProperty("experiencePoints").GetInt64());
        using var calendar = await client.GetAsync("/api/workspace/calendar/export.ics", Ct);
        Assert.Equal(HttpStatusCode.OK, calendar.StatusCode); Assert.Equal("text/calendar", calendar.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Viết báo cáo riêng", (await calendar.Content.ReadAsStringAsync(Ct)).Replace("\r\n ", ""));
        await Send(client, HttpMethod.Post, "/api/commerce/payments", new SkinCheckoutInput("fox", Guid.NewGuid()), HttpStatusCode.ServiceUnavailable);
    }

    [Fact] public async Task Admin_only_metrics_are_aggregate_and_user_listing_omits_personal_work()
    {
        await using var f = new ApiFactory(); using var client = await f.ClientAsync(); await Authorize(f, client);
        await Send(client, HttpMethod.Get, "/api/admin/metrics", expected: HttpStatusCode.Forbidden);
        await Send(client, HttpMethod.Get, "/api/admin/health", expected: HttpStatusCode.Forbidden);
        await using (var scope = f.Services.CreateAsyncScope()) {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            Assert.True((await roles.CreateAsync(new("Admin"))).Succeeded);
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(ServiceHarness.Address))!, "Admin")).Succeeded);
        }
        var login = (await Send(client, HttpMethod.Post, "/api/auth/login", new { email = ServiceHarness.Address, password = ServiceHarness.Password })).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        var metrics = (await Send(client, HttpMethod.Get, "/api/admin/metrics")).GetProperty("data");
        Assert.Equal(1, metrics.GetProperty("users").GetInt32()); Assert.Equal(1, metrics.GetProperty("activeUsersToday").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, metrics.GetProperty("inputTokens").ValueKind);
        var user = (await Send(client, HttpMethod.Get, "/api/admin/users")).GetProperty("data")[0];
        Assert.False(user.TryGetProperty("totalTasks", out _)); Assert.False(user.TryGetProperty("completedTasks", out _));
        var health = (await Send(client, HttpMethod.Get, "/api/admin/health")).GetProperty("data");
        Assert.True(health.GetProperty("databaseHealthy").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, health.GetProperty("ai").GetProperty("lastObservedLatencyMilliseconds").ValueKind);
    }
}
