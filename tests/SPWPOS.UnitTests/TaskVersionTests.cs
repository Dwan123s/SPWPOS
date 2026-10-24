using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class TaskVersionTests
{
    [Fact]
    public async Task Db_concurrency_token_protects_writers_outside_the_workspace_command()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await ServiceHarness.CreateAsync(); var user = await harness.RegisterAsync();
        Guid id;
        await using (var scope = harness.Provider.CreateAsyncScope())
            id = await scope.ServiceProvider.GetRequiredService<IWorkspaceService>().SaveTaskAsync(user.Id, null,
                new("Original", "", harness.Clock.GetUtcNow().AddDays(1), 60), ct);
        await using var first = harness.Provider.CreateAsyncScope(); await using var second = harness.Provider.CreateAsyncScope();
        var firstDb = first.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondDb = second.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var a = await firstDb.WorkTasks.SingleAsync(x => x.Id == id, ct);
        var b = await secondDb.WorkTasks.SingleAsync(x => x.Id == id, ct);
        var oldVersion = a.Version;
        a.Name = "First writer"; await firstDb.SaveChangesAsync(ct);
        Assert.NotEqual(oldVersion, a.Version);
        b.Priority = 4;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync(ct));
        var saved = await harness.DbAsync(db => db.WorkTasks.AsNoTracking().SingleAsync(x => x.Id == id, ct));
        Assert.Equal("First writer", saved.Name); Assert.Equal(2, saved.Priority);
    }
}
