using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;
namespace SPWPOS.IntegrationTests;

public sealed class TaskVersionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public static bool SqlServerAvailable => SqlServerConcurrencyTests.SqlServerAvailable;

    [Fact]
    public async Task Two_tabs_cannot_overwrite_each_other_and_can_retry_after_loading_the_new_version()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client);
        var now = factory.Clock.GetUtcNow(); var id = await Create(client, now);
        var original = (await Send(client, HttpMethod.Get, $"/api/tasks/{id}")).GetProperty("data");
        var version = original.GetProperty("version").GetGuid(); Assert.NotEqual(Guid.Empty, version);
        var a = new TaskInput("Edited by tab A", "", now.AddDays(1), 60, Version: version);
        var b = a with { Name = original.GetProperty("name").GetString()!, Priority = 4 };
        await Send(client, HttpMethod.Put, $"/api/tasks/{id}", a);
        var rejected = await Send(client, HttpMethod.Put, $"/api/tasks/{id}", b, HttpStatusCode.Conflict);
        Assert.Equal("TASK_STALE", rejected.GetProperty("error").GetProperty("code").GetString());
        var current = (await Send(client, HttpMethod.Get, $"/api/tasks/{id}")).GetProperty("data");
        Assert.Equal(a.Name, current.GetProperty("name").GetString());
        var currentVersion = current.GetProperty("version").GetGuid(); Assert.NotEqual(version, currentVersion);
        await Send(client, HttpMethod.Put, $"/api/tasks/{id}", a with { Priority = 4, Version = currentVersion });
        var saved = (await Send(client, HttpMethod.Get, $"/api/tasks/{id}")).GetProperty("data");
        Assert.Equal(a.Name, saved.GetProperty("name").GetString()); Assert.Equal(4, saved.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task Updates_require_a_nonempty_version_but_creates_do_not()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client);
        var now = factory.Clock.GetUtcNow(); var id = await Create(client, now);
        foreach (var version in new Guid?[] { null, Guid.Empty, Guid.NewGuid() })
        {
            var error = await Send(client, HttpMethod.Put, $"/api/tasks/{id}",
                new TaskInput("Must not save", "", now.AddDays(1), 60, Version: version), HttpStatusCode.Conflict);
            Assert.Equal("TASK_STALE", error.GetProperty("error").GetProperty("code").GetString());
        }
        var saved = (await Send(client, HttpMethod.Get, $"/api/tasks/{id}")).GetProperty("data");
        Assert.NotEqual("Must not save", saved.GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_status_change_invalidates_an_open_edit_even_when_the_clock_has_not_advanced()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client);
        var now = factory.Clock.GetUtcNow(); var id = await Create(client, now);
        var task = (await Send(client, HttpMethod.Get, $"/api/tasks/{id}")).GetProperty("data");
        await Send(client, HttpMethod.Patch, $"/api/tasks/{id}/status", new StatusInput("InProgress", 25));
        await Send(client, HttpMethod.Put, $"/api/tasks/{id}", new TaskInput("Stale edit", "", now.AddDays(1), 60,
            Version: task.GetProperty("version").GetGuid()), HttpStatusCode.Conflict);
        var saved = (await Send(client, HttpMethod.Get, $"/api/tasks/{id}")).GetProperty("data");
        Assert.Equal(25, saved.GetProperty("progress").GetInt32());
    }

    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires SPWPOS_TEST_SQLSERVER."), Trait("Category", "SqlServer")]
    public async Task Concurrent_updates_with_the_same_version_have_exactly_one_winner()
    {
        await using var factory = new ApiFactory(true); using var client = await factory.ClientAsync(); await Authorize(factory, client);
        var now = factory.Clock.GetUtcNow(); var id = await Create(client, now);
        var task = (await Send(client, HttpMethod.Get, $"/api/tasks/{id}")).GetProperty("data");
        var input = new TaskInput("Winner", "", now.AddDays(1), 60, Version: task.GetProperty("version").GetGuid());
        using var first = System.Net.Http.Json.JsonContent.Create(input);
        using var second = System.Net.Http.Json.JsonContent.Create(input with { Name = "Other winner" });
        var responses = await Task.WhenAll(client.PutAsync($"/api/tasks/{id}", first, Ct), client.PutAsync($"/api/tasks/{id}", second, Ct));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.NotEqual(input.Version, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().WorkTasks.Where(x => x.Id == id).Select(x => x.Version).SingleAsync(Ct));
    }
}
