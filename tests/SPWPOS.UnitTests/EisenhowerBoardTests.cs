using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class EisenhowerBoardTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Board<T>(ServiceHarness h, Func<IEisenhowerService, Task<T>> action) {
        await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IEisenhowerService>());
    }
    private static Task Board(ServiceHarness h, Func<IEisenhowerService, Task> action) => Board(h, async s => { await action(s); return true; });
    private static async Task<T> Work<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action) {
        await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());
    }
    private static Task<Guid> Add(ServiceHarness h, Guid uid, string name = "Report", string quadrant = "Q2", int minutes = 60) =>
        Board(h, s => s.SaveMatrixTaskAsync(uid, null, new(name, "Đọc tài liệu và viết kết quả.", h.Clock.GetUtcNow().AddDays(5), minutes, quadrant,
            Checklist: [new(Guid.NewGuid(), "Đọc tài liệu"), new(Guid.NewGuid(), "Viết bản nháp")]), Ct));
    private static Task<EisenhowerView> View(ServiceHarness h, Guid uid) => Board(h, s => s.GetEisenhowerAsync(uid, Ct));

    [Fact] public async Task Creating_task_and_checklist_is_atomic_and_does_not_schedule_or_complete_work()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        var id = await Add(h, user.Id); var row = Assert.Single((await View(h, user.Id)).Tasks);
        Assert.Equal(id, row.Id); Assert.Equal("Q2", row.Quadrant); Assert.Equal("User", row.UrgencySource);
        Assert.Equal("SuggestSchedule", row.Recommendation); Assert.Equal(2, row.Checklist!.Length);
        Assert.All(row.Checklist, i => Assert.False(i.Completed)); Assert.NotEqual(Guid.Empty, row.Version);
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.PlanProposals.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.AgentTurns.CountAsync(Ct)));
        var invalid = new TaskChecklistItem(Guid.NewGuid(), " ");
        await Assert.ThrowsAsync<AuthException>(() => Board(h, s => s.SaveMatrixTaskAsync(user.Id, null,
            new("Invalid", "", h.Clock.GetUtcNow().AddDays(2), 30, Checklist: [invalid]), Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Theory] [InlineData("Q1", "Important", "Urgent")] [InlineData("Q2", "Important", "NotUrgent")]
    [InlineData("Q3", "Lower", "Urgent")] [InlineData("Q4", "Lower", "NotUrgent")]
    public async Task Dragging_saves_user_quadrant_and_never_changes_deadline_or_calendar(string quadrant, string importance, string urgency)
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        var id = await Add(h, user.Id); var first = await View(h, user.Id); var original = first.Tasks.Single();
        var savedSession = new WorkSession { UserId = user.Id, TaskId = id, Start = h.Clock.GetUtcNow().AddHours(2), End = h.Clock.GetUtcNow().AddHours(3) };
        await h.DbAsync(async db => { db.WorkSessions.Add(savedSession); await db.SaveChangesAsync(Ct); });
        await Board(h, s => s.MoveMatrixTaskAsync(user.Id, id, new(original.Version, first.Revision, quadrant), Ct));
        var moved = (await View(h, user.Id)).Tasks.Single();
        Assert.Equal(quadrant, moved.Quadrant); Assert.Equal(importance, moved.Importance); Assert.Equal(urgency, moved.Urgency);
        Assert.Equal(original.Deadline, moved.Deadline); Assert.Equal(original.EstimatedMinutes, moved.EstimatedMinutes);
        var session = await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct));
        Assert.Equal(savedSession.Start, session.Start); Assert.Equal(savedSession.End, session.End); Assert.Equal(WorkSessionStatus.Scheduled, session.Status);
        Assert.Equal(0, await h.DbAsync(db => db.PlanProposals.CountAsync(Ct)));
    }

    [Fact] public async Task Reorder_and_stale_guards_prevent_lost_updates_and_foreign_positions()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        var first = await Add(h, user.Id, "First"); var second = await Add(h, user.Id, "Second");
        var view = await View(h, user.Id); var row = view.Tasks.Single(t => t.Id == second);
        await Board(h, s => s.MoveMatrixTaskAsync(user.Id, second, new(row.Version, view.Revision, "Q2", first), Ct));
        var next = await View(h, user.Id); Assert.Equal(new[] { second, first }, next.Tasks.Select(t => t.Id));
        Assert.Equal("TASK_STALE", (await Assert.ThrowsAsync<AuthException>(() => Board(h, s => s.MoveMatrixTaskAsync(user.Id, second,
            new(row.Version, view.Revision, "Q1"), Ct)))).Code);
        var current = next.Tasks.Single(t => t.Id == second);
        Assert.Equal("MATRIX_STALE", (await Assert.ThrowsAsync<AuthException>(() => Board(h, s => s.MoveMatrixTaskAsync(user.Id, second,
            new(current.Version, view.Revision, "Q1"), Ct)))).Code);
        Assert.Equal("MATRIX_STALE", (await Assert.ThrowsAsync<AuthException>(() => Board(h, s => s.MoveMatrixTaskAsync(user.Id, second,
            new(current.Version, next.Revision, "Q1", Guid.NewGuid()), Ct)))).Code);
        Assert.Equal("Q2", (await View(h, user.Id)).Tasks.Single(t => t.Id == second).Quadrant);
        var foreign = new ApplicationUser { Id = Guid.NewGuid(), UserName = "other", NormalizedUserName = "OTHER", Email = "other@example.com", NormalizedEmail = "OTHER@EXAMPLE.COM", EmailConfirmed = true, IsActive = true };
        await h.DbAsync(async db => { db.Users.Add(foreign); await db.SaveChangesAsync(Ct); });
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => Board(h, s => s.UpdateMatrixChecklistAsync(foreign.Id, second,
            new(current.Version, []), Ct)))).Code);
    }

    [Fact] public async Task Checking_and_reordering_all_steps_does_not_finish_task_or_change_estimates()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); var id = await Add(h, user.Id);
        var row = (await View(h, user.Id)).Tasks.Single();
        var reversed = row.Checklist!.Reverse().Select(i => i with { Completed = true }).ToArray();
        await Board(h, s => s.UpdateMatrixChecklistAsync(user.Id, id, new(row.Version, reversed), Ct));
        var next = (await View(h, user.Id)).Tasks.Single();
        Assert.Equal(reversed, next.Checklist); Assert.NotEqual(row.Version, next.Version);
        var task = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct));
        Assert.Equal(WorkTaskStatus.Todo, task.Status); Assert.Equal(0, task.Progress); Assert.Equal(60, task.EstimatedMinutes);
        Assert.Equal(0, await h.DbAsync(db => db.Users.Select(u => u.ExperiencePoints).SingleAsync(Ct)));
        Assert.Equal("TASK_STALE", (await Assert.ThrowsAsync<AuthException>(() => Board(h, s => s.UpdateMatrixChecklistAsync(user.Id, id, new(row.Version, []), Ct)))).Code);
        Assert.Equal("INVALID_CHECKLIST", (await Assert.ThrowsAsync<AuthException>(() => Board(h, s => s.UpdateMatrixChecklistAsync(user.Id, id,
            new(next.Version, [reversed[0], reversed[0]]), Ct)))).Code);
    }

    [Fact] public async Task Manual_nonurgent_does_not_hide_overdue_warning_and_can_return_to_automatic_urgency()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); var id = await Add(h, user.Id, quadrant: "Q4");
        h.Clock.Advance(TimeSpan.FromDays(6));
        var view = await View(h, user.Id); var row = view.Tasks.Single();
        Assert.Equal("Q4", row.Quadrant); Assert.True(row.Overdue); Assert.True(row.AtRisk); Assert.Contains("OVERDUE", row.ReasonCodes);
        await Board(h, s => s.MoveMatrixTaskAsync(user.Id, id, new(row.Version, view.Revision, "Auto"), Ct));
        var auto = (await View(h, user.Id)).Tasks.Single();
        Assert.Equal("Q3", auto.Quadrant); Assert.Equal("Auto", auto.UrgencySource); Assert.True(auto.Overdue);
    }

    [Theory] [InlineData("Q1", "SuggestSchedule")] [InlineData("Q2", "SuggestSchedule")]
    [InlineData("Q3", "QuickAction")] [InlineData("Q4", "Backlog")]
    public async Task Recommendations_follow_quadrants_and_calendar_requires_explicit_apply(string quadrant, string recommendation)
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        var id = await Add(h, user.Id, quadrant: quadrant, minutes: quadrant == "Q3" ? 5 : 60);
        await Work(h, async s => { await s.SetAvailabilityAsync(user.Id, new(Enumerable.Range(0,7).Select(d => new AvailabilityInput(d,480,1320)).ToArray()), Ct); return true; });
        var view = await View(h, user.Id); var row = view.Tasks.Single();
        Assert.Equal(recommendation, row.Recommendation);
        if (quadrant is "Q3" or "Q4")
            Assert.Equal("SCHEDULE_ON_DEMAND", (await Assert.ThrowsAsync<AuthException>(() => Board(h, s => s.SuggestMatrixScheduleAsync(user.Id,id,new(row.Version,view.Revision),Ct)))).Code);
        var plan = await Board(h, s => s.SuggestMatrixScheduleAsync(user.Id,id,new(row.Version,view.Revision,true),Ct));
        Assert.Empty(plan.Warnings); Assert.NotEmpty(plan.Sessions); Assert.False(plan.IsApplied);
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
        await Work(h, async s => { await s.ApplyPlanAsync(user.Id,plan.Id,Ct); return true; });
        Assert.True(await h.DbAsync(db => db.WorkSessions.AnyAsync(s=>s.TaskId==id,Ct)));
        Assert.Equal(row.Deadline, await h.DbAsync(db => db.WorkTasks.Select(t=>t.Deadline).SingleAsync(Ct)));
    }
}
