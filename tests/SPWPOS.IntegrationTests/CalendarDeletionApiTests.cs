using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;
namespace SPWPOS.IntegrationTests;

public sealed class CalendarDeletionApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Calendar_delete_requires_login_and_keeps_task_while_removing_only_selected_session()
    {
        await using var f = new ApiFactory(); using var c = await f.ClientAsync();
        await Send(c, HttpMethod.Delete, $"/api/sessions/{Guid.NewGuid()}", expected: HttpStatusCode.Unauthorized);
        await Authorize(f, c); var taskId = await Create(c, f.Clock.GetUtcNow());
        Guid id; Guid lockedId;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var uid = await db.Users.Select(x => x.Id).SingleAsync(Ct);
            var first = new WorkSession { UserId = uid, TaskId = taskId, Start = f.Clock.GetUtcNow().AddHours(1), End = f.Clock.GetUtcNow().AddHours(2) };
            var locked = new WorkSession { UserId = uid, TaskId = taskId, Start = f.Clock.GetUtcNow().AddHours(3), End = f.Clock.GetUtcNow().AddHours(4), IsLocked = true };
            id = first.Id; lockedId = locked.Id; db.WorkSessions.AddRange(first, locked); await db.SaveChangesAsync(Ct);
        }
        await Send(c, HttpMethod.Delete, $"/api/sessions/{lockedId}", expected: HttpStatusCode.Conflict);
        await Send(c, HttpMethod.Delete, $"/api/sessions/{id}");
        var workspace = (await Send(c, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.DoesNotContain(workspace.GetProperty("sessions").EnumerateArray(), s => s.GetProperty("id").GetGuid() == id);
        Assert.Contains(workspace.GetProperty("sessions").EnumerateArray(), s => s.GetProperty("id").GetGuid() == lockedId);
        Assert.Contains(workspace.GetProperty("tasks").EnumerateArray(), t => t.GetProperty("id").GetGuid() == taskId);
        await Send(c, HttpMethod.Delete, $"/api/sessions/{id}");
    }
}
