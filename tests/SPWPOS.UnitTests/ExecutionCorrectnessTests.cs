using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class ExecutionCorrectnessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Work<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    private static Task Work(ServiceHarness h, Func<IWorkspaceService, Task> action) => Work(h, async s => { await action(s); return true; });
    private static async Task<(Guid User, Guid Session, DateTimeOffset Start)> Setup(ServiceHarness h)
    {
        h.Clock.Advance(DateTimeOffset.Parse("2026-10-15T14:00:00Z") - h.Clock.GetUtcNow());
        var user = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        await Work(h, s => s.SetPreferenceAsync(user.Id, new("UTC", 25, 90, 0, 0, 1440), Ct));
        await Work(h, s => s.SetAvailabilityAsync(user.Id, new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 0, 1440)).ToArray()), Ct));
        var task = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Report", "", now.AddDays(1), 60), Ct));
        var session = new WorkSession { UserId = user.Id, TaskId = task, Start = now, End = now.AddHours(1) };
        await h.DbAsync(async db => { db.WorkSessions.Add(session); db.FixedEvents.Add(new() { UserId = user.Id, Name = "Meeting", Start = now.AddHours(2), End = now.AddHours(3) }); await db.SaveChangesAsync(Ct); });
        return (user.Id, session.Id, now);
    }
    [Fact] public async Task Resume_during_a_fixed_event_is_rejected_without_opening_a_segment()
    {
        await using var h = await ServiceHarness.CreateAsync(); var data = await Setup(h);
        await Work(h, s => s.ExecuteSessionAsync(data.User, data.Session, "start", Ct));
        h.Clock.Advance(TimeSpan.FromMinutes(20));
        await Work(h, s => s.ExecuteSessionAsync(data.User, data.Session, "pause", Ct));
        h.Clock.Advance(TimeSpan.FromMinutes(130));
        var error = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.ExecuteSessionAsync(data.User, data.Session, "resume", Ct)));
        Assert.Equal("FIXED_EVENT_CONFLICT", error.Code);
        Assert.Equal(WorkSessionStatus.Paused, await h.DbAsync(db => db.WorkSessions.Select(x => x.Status).SingleAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.ExecutionSegments.CountAsync(x => x.End == null, Ct)));
    }
    [Fact] public async Task Resume_after_the_planned_window_requires_replanning()
    {
        await using var h = await ServiceHarness.CreateAsync(); var data = await Setup(h);
        await Work(h, s => s.ExecuteSessionAsync(data.User, data.Session, "start", Ct));
        h.Clock.Advance(TimeSpan.FromMinutes(20)); await Work(h, s => s.ExecuteSessionAsync(data.User, data.Session, "pause", Ct));
        h.Clock.Advance(TimeSpan.FromMinutes(50));
        Assert.Equal("SESSION_WINDOW_EXPIRED", (await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.ExecuteSessionAsync(data.User, data.Session, "resume", Ct)))).Code);
        Assert.Equal(1200, await h.DbAsync(db => db.WorkSessions.Select(x => x.ActualSeconds).SingleAsync(Ct)));
    }
    [Fact] public async Task Replayed_execution_actions_do_not_duplicate_segments_or_time()
    {
        await using var h = await ServiceHarness.CreateAsync(); var data = await Setup(h);
        await Work(h, s => s.ExecuteSessionAsync(data.User, data.Session, "start", Ct));
        await Work(h, s => s.ExecuteSessionAsync(data.User, data.Session, "start", Ct));
        h.Clock.Advance(TimeSpan.FromMinutes(10));
        await Work(h, s => s.ExecuteSessionAsync(data.User, data.Session, "complete", Ct));
        h.Clock.Advance(TimeSpan.FromMinutes(5));
        await Work(h, s => s.ExecuteSessionAsync(data.User, data.Session, "complete", Ct));
        Assert.Equal(1, await h.DbAsync(db => db.ExecutionSegments.CountAsync(Ct)));
        Assert.Equal(600, await h.DbAsync(db => db.WorkSessions.Select(x => x.ActualSeconds).SingleAsync(Ct)));
    }
    [Fact] public async Task Container_dependency_plan_can_be_applied_and_started_after_children_complete()
    {
        await using var h = await ServiceHarness.CreateAsync(); var data = await Setup(h);
        await h.DbAsync(async db => { db.WorkSessions.RemoveRange(db.WorkSessions); db.FixedEvents.RemoveRange(db.FixedEvents); await db.SaveChangesAsync(Ct); });
        var due = data.Start.AddHours(8);
        var parent = await Work(h, s => s.SaveTaskAsync(data.User, null, new("Project", "", due, 60), Ct));
        var child = await Work(h, s => s.SaveTaskAsync(data.User, null, new("Write", "", due, 60, ParentId: parent), Ct));
        var next = await Work(h, s => s.SaveTaskAsync(data.User, null, new("Submit", "", due, 60), Ct));
        await Work(h, s => s.SetDependencyAsync(data.User, next, parent, false, Ct));
        var plan = await Work(h, s => s.GeneratePlanAsync(data.User, new(data.Start, 1, [child, next], UseCapacityFactor: false), Ct));
        Assert.Empty(plan.Warnings);
        await Work(h, s => s.ApplyPlanAsync(data.User, plan.Id, Ct));
        Assert.Empty(await Work(h, s => s.DetectConflictsAsync(data.User, new(data.Start, 1), Ct)));
        await Work(h, s => s.SetStatusAsync(data.User, child, new("Completed", 100), Ct));
        Assert.Equal(WorkTaskStatus.Completed, await h.DbAsync(db => db.WorkTasks.Where(x => x.Id == parent).Select(x => x.Status).SingleAsync(Ct)));
        var nextSession = plan.Sessions.Single(x => x.TaskId == next);
        h.Clock.Advance(nextSession.Start - h.Clock.GetUtcNow());
        await Work(h, s => s.ExecuteSessionAsync(data.User, nextSession.Id, "start", Ct));
        Assert.Equal(WorkSessionStatus.Running, await h.DbAsync(db => db.WorkSessions.Where(x => x.Id == nextSession.Id).Select(x => x.Status).SingleAsync(Ct)));
    }
}
