using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;
namespace SPWPOS.IntegrationTests;

public sealed class NotificationRefreshApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact] public async Task Refresh_creates_a_due_timetable_reminder_without_waiting_for_worker_and_never_duplicates_it()
    {
        await using var f = new ApiFactory(); using var client = await f.ClientAsync();
        await Send(client, HttpMethod.Post, "/api/notifications/refresh", expected: HttpStatusCode.Unauthorized);
        await Authorize(f, client);
        await using(var scope = f.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var uid = await db.Users.Select(x => x.Id).SingleAsync(Ct);
            db.FixedEvents.Add(new() { UserId = uid, Name = "PRN212", Start = f.Clock.GetUtcNow().AddMinutes(5), End = f.Clock.GetUtcNow().AddHours(1) });
            await db.SaveChangesAsync(Ct);
        }
        var first = (await Send(client, HttpMethod.Post, "/api/notifications/refresh")).GetProperty("data");
        var id = Assert.Single(first.EnumerateArray(), x => x.GetProperty("kind").GetString() == "EVENT_REMINDER").GetProperty("id").GetGuid();
        await Send(client, HttpMethod.Post, "/api/notifications/refresh");
        var second = (await Send(client, HttpMethod.Get, "/api/notifications")).GetProperty("data");
        Assert.Equal(id, Assert.Single(second.EnumerateArray(), x => x.GetProperty("kind").GetString() == "EVENT_REMINDER").GetProperty("id").GetGuid());
        await Send(client, HttpMethod.Post, $"/api/notifications/{id}/read");
        var read = (await Send(client, HttpMethod.Get, "/api/notifications")).GetProperty("data");
        Assert.True(Assert.Single(read.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id).GetProperty("isRead").GetBoolean());
    }
}
