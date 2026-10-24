using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class WorkspaceRegressionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Call<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    private static Task Call(ServiceHarness h, Func<IWorkspaceService, Task> action) => Call(h, async s => { await action(s); return true; });
    [Fact] public async Task Child_dependency_on_parent_is_rejected_and_removal_still_works()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); var due = h.Clock.GetUtcNow().AddDays(1);
        var parent = await Call(h, s => s.SaveTaskAsync(user.Id, null, new("Parent", "", due, 60), Ct));
        var child = await Call(h, s => s.SaveTaskAsync(user.Id, null, new("Child", "", due, 30, ParentId: parent), Ct));
        Assert.Equal("DEPENDENCY_CYCLE", (await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.SetDependencyAsync(user.Id, child, parent, false, Ct)))).Code);
        Assert.Empty(await h.DbAsync(db => db.TaskDependencies.ToArrayAsync(Ct)));
        await Call(h, s => s.SetDependencyAsync(user.Id, child, parent, true, Ct));
    }
    [Fact] public async Task Reparenting_cannot_create_an_implicit_dependency_cycle_and_rolls_back()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); var due = h.Clock.GetUtcNow().AddDays(1);
        var parent = await Call(h, s => s.SaveTaskAsync(user.Id, null, new("Parent", "", due, 60), Ct));
        var child = await Call(h, s => s.SaveTaskAsync(user.Id, null, new("Child", "", due, 30), Ct));
        await Call(h, s => s.SetDependencyAsync(user.Id, child, parent, false, Ct));
        var version = await h.DbAsync(db => db.WorkTasks.Where(x => x.Id == child).Select(x => x.Version).SingleAsync(Ct));
        Assert.Equal("DEPENDENCY_CYCLE", (await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.SaveTaskAsync(user.Id, child, new("Child", "", due, 30, ParentId: parent, Version: version), Ct)))).Code);
        Assert.Null(await h.DbAsync(db => db.WorkTasks.Where(x => x.Id == child).Select(x => x.ParentId).SingleAsync(Ct)));
    }
    [Fact] public async Task Repeated_reads_and_analysis_do_not_write_jobs_learning_or_risk_history()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        await Call(h, s => s.SaveTaskAsync(user.Id, null, new("Due soon", "", h.Clock.GetUtcNow().AddHours(1), 60), Ct));
        var before = await h.DbAsync(async db => (await db.SystemJobRuns.CountAsync(Ct), await db.RiskAssessments.CountAsync(Ct), await db.RiskReasons.CountAsync(Ct), await db.InAppNotifications.CountAsync(Ct)));
        for (var i = 0; i < 3; i++)
        {
            await Call(h, s => s.GetAsync(user.Id, Ct));
            await Call(h, s => s.AnalyzeAsync(user.Id, new(h.Clock.GetUtcNow(), 7), Ct));
        }
        var after = await h.DbAsync(async db => (await db.SystemJobRuns.CountAsync(Ct), await db.RiskAssessments.CountAsync(Ct), await db.RiskReasons.CountAsync(Ct), await db.InAppNotifications.CountAsync(Ct)));
        Assert.Equal(before, after);
        await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct));
        Assert.Contains(await h.DbAsync(db => db.InAppNotifications.ToArrayAsync(Ct)), x => x.Kind == "DEADLINE");
        Assert.Equal(before.Item1, await h.DbAsync(db => db.SystemJobRuns.CountAsync(Ct)));
    }
    [Fact] public async Task Old_active_task_is_not_hidden_by_thousands_of_completed_tasks()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        var active = new WorkTask { UserId = user.Id, Name = "Still active", Deadline = now.AddDays(1), CreatedAt = now.AddYears(-1) };
        var predecessor = new WorkTask { UserId = user.Id, Name = "Old completed prerequisite", Deadline = now, Status = WorkTaskStatus.Completed, Progress = 100, CreatedAt = now.AddYears(-2) };
        await h.DbAsync(async db => {
            db.WorkTasks.AddRange(active, predecessor);
            db.TaskDependencies.Add(new() { UserId = user.Id, TaskId = active.Id, PredecessorId = predecessor.Id });
            db.WorkTasks.AddRange(Enumerable.Range(0, 5000).Select(i => new WorkTask { UserId = user.Id, Name = "Completed", Deadline = now, Status = WorkTaskStatus.Completed, Progress = 100, CreatedAt = now }));
            await db.SaveChangesAsync(Ct);
        });
        var view = await Call(h, s => s.GetAsync(user.Id, Ct)); Assert.Contains(view.Tasks, x => x.Id == active.Id);
        Assert.Contains(view.Tasks, x => x.Id == predecessor.Id && x.Status == "Completed");
    }
    [Fact] public async Task Retention_removes_only_old_completed_jobs_and_superseded_risks_with_their_reasons()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        var old = new RiskAssessment { UserId = user.Id, Scope = "OVERLOAD", IsCurrent = false, ComputedAt = now.AddDays(-31) };
        var current = new RiskAssessment { UserId = user.Id, Scope = "OVERLOAD", IsCurrent = true, ComputedAt = now.AddDays(-31) };
        await h.DbAsync(async db => {
            db.RiskAssessments.AddRange(old, current);
            db.RiskReasons.AddRange(new RiskReason { UserId = user.Id, AssessmentId = old.Id, Code = "OLD" }, new RiskReason { UserId = user.Id, AssessmentId = current.Id, Code = "KEEP" });
            db.SystemJobRuns.AddRange(new SystemJobRun { JobType = "NOTIFICATION_SCAN", Status = "SUCCESS", StartedAt = now.AddDays(-31), FinishedAt = now.AddDays(-31) },
                new SystemJobRun { JobType = "PLAN_GENERATION", StartedAt = now.AddDays(-31) }, new SystemJobRun { JobType = "AI_REQUEST", Status = "FAILED", StartedAt = now, FinishedAt = now });
            await db.SaveChangesAsync(Ct);
        });
        await using var scope = h.Provider.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<WorkspaceHistoryMaintenance>().PruneAsync(Ct);
        Assert.Equal(current.Id, Assert.Single(await h.DbAsync(db => db.RiskAssessments.ToArrayAsync(Ct))).Id);
        Assert.Equal("KEEP", Assert.Single(await h.DbAsync(db => db.RiskReasons.ToArrayAsync(Ct))).Code);
        Assert.Equal(2, await h.DbAsync(db => db.SystemJobRuns.CountAsync(Ct)));
        Assert.NotEmpty(await h.DbAsync(db => db.SecurityAuditLogs.ToArrayAsync(Ct)));
    }
}
