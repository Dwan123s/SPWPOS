using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed class TaskBatchTests
{
    [Fact]
    public async Task Changing_project_due_date_updates_inherited_children_and_keeps_explicit_equal_dates()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var h = await ServiceHarness.CreateAsync();
        var user = await h.RegisterAsync();
        var due = h.Clock.GetUtcNow().AddDays(4);
        async Task<T> WithWorkspace<T>(Func<IWorkspaceService, Task<T>> action)
        {
            await using var scope = h.Provider.CreateAsyncScope();
            return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());
        }
        var batch = await WithWorkspace(s => s.SaveTaskBatchAsync(user.Id, new(Guid.NewGuid(), "Demo IoT",
            [new("Chuẩn bị demo", EstimatedMinutes: 60), new("Nộp tài liệu", Deadline: due)], ProjectDeadline: due), ct));
        async Task<TaskView[]> Read() => (await WithWorkspace(s => s.GetAsync(user.Id, ct))).Tasks;
        async Task Edit(DateTimeOffset deadline)
        {
            var project = (await Read()).Single(x => x.Id == batch.ProjectId);
            await WithWorkspace(s => s.SaveTaskAsync(user.Id, project.Id, new(project.Name, Deadline: deadline,
                IsHardDeadline: true, Version: project.Version), ct));
        }
        await Edit(due.AddDays(2));
        var tasks = await Read();
        Assert.Equal(due.AddDays(2), tasks.Single(x => x.Id == batch.TaskIds[0]).Deadline);
        Assert.True(tasks.Single(x => x.Id == batch.TaskIds[0]).InheritsProjectDeadline);
        Assert.Equal(due, tasks.Single(x => x.Id == batch.TaskIds[1]).Deadline);
        Assert.False(tasks.Single(x => x.Id == batch.TaskIds[1]).InheritsProjectDeadline);
        var rejected = await Assert.ThrowsAsync<AuthException>(() => Edit(due.AddDays(-1)));
        Assert.Equal("SUBTASK_DEADLINE", rejected.Code);
        await Edit(due);
        Assert.Equal(due, (await Read()).Single(x => x.Id == batch.TaskIds[0]).Deadline);
    }

    [Fact]
    public async Task Workload_counts_title_only_inbox_items_without_charging_estimated_time()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var h = await ServiceHarness.CreateAsync();
        var user = await h.RegisterAsync();
        await using var scope = h.Provider.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        var id = await workspace.SaveTaskAsync(user.Id, null, new("Check mail"), ct);
        var workload = await workspace.GetWorkloadAsync(user.Id, ct);
        Assert.Empty(workload.Tasks);
        Assert.Equal(1, workload.TasksMissingPlanningDetails);
        Assert.Equal("INBOX", workload.Next.Kind);
        await workspace.SetStatusAsync(user.Id, id, new("Completed", 100), ct);
        Assert.Equal(0, (await workspace.GetWorkloadAsync(user.Id, ct)).TasksMissingPlanningDetails);
    }

    [Fact]
    public async Task Saves_project_children_and_reviewed_order_once()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await ServiceHarness.CreateAsync();
        var user = await harness.RegisterAsync();
        await using var scope = harness.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        var deadline = harness.Clock.GetUtcNow().AddDays(2);
        var step = new TaskChecklistItem(Guid.NewGuid(), "Đối chiếu số liệu với tài liệu gốc");
        var input = new TaskBatchInput(Guid.NewGuid(), "Báo cáo IoT",
            [new("Thu thập số liệu", EstimatedMinutes: 30, Checklist: [step]),
             new("Viết báo cáo", Deadline: deadline.AddHours(-1), EstimatedMinutes: 60)], ProjectDeadline: deadline);

        var result = await service.SaveTaskBatchAsync(user.Id, input, ct);
        var repeat = await service.SaveTaskBatchAsync(user.Id, input, ct);
        Assert.Equal(result.ProjectId, repeat.ProjectId);
        Assert.True(result.TaskIds.ToHashSet().SetEquals(repeat.TaskIds));
        var tasks = (await service.GetAsync(user.Id, ct)).Tasks;
        Assert.Equal(deadline, Assert.Single(tasks, task => task.Name == "Báo cáo IoT").Deadline);
        var children = tasks.Where(task => task.ParentId == result.ProjectId).ToArray();
        Assert.Equal(2, children.Length);
        var firstChild = children.Single(task => task.Id == result.TaskIds[0]);
        var secondChild = children.Single(task => task.Id == result.TaskIds[1]);
        Assert.Equal(deadline, firstChild.Deadline);
        Assert.Equal(deadline.AddHours(-1), secondChild.Deadline);
        Assert.Equal(firstChild.Id, Assert.Single(secondChild.Dependencies));
        Assert.Equal(step, Assert.Single(firstChild.Checklist!));
        await service.UpdateTaskChecklistAsync(user.Id, firstChild.Id, new(firstChild.Version, [step with { Completed = true }]), ct);
        var updated = (await service.GetAsync(user.Id, ct)).Tasks.Single(task => task.Id == firstChild.Id);
        Assert.True(Assert.Single(updated.Checklist!).Completed);
        Assert.Equal("Todo", updated.Status);
        Assert.Equal(0, updated.Progress);

        await service.SetAvailabilityAsync(user.Id, new(Enumerable.Range(0, 7)
            .Select(day => new AvailabilityInput(day, 540, 1080)).ToArray()), ct);
        var plan = await service.GeneratePlanAsync(user.Id, new(harness.Clock.GetUtcNow(), 7, result.TaskIds,
            Strict: true, PreserveExisting: true), ct);
        var first = plan.Sessions.Where(session => session.TaskId == result.TaskIds[0]).ToArray();
        var second = plan.Sessions.Where(session => session.TaskId == result.TaskIds[1]).ToArray();
        Assert.NotEmpty(first);
        Assert.NotEmpty(second);
        Assert.True(first.Max(session => session.End) <= second.Min(session => session.Start));
        await service.ApplyPlanAsync(user.Id, plan.Id, ct);
        var workload = await service.GetWorkloadAsync(user.Id, ct);
        var nextWeek = await service.GetWorkloadAsync(user.Id, ct, 1);
        var scheduledIds = workload.Days.Concat(nextWeek.Days)
            .SelectMany(day => day.Sessions).Select(session => session.TaskId).ToHashSet();
        Assert.All(result.TaskIds, id => Assert.Contains(id, scheduledIds));

        var invalid = input with { RequestId = Guid.NewGuid(), ProjectName = "Không hợp lệ",
            Tasks = [input.Tasks[0], input.Tasks[1] with { Deadline = deadline.AddDays(1) }] };
        await Assert.ThrowsAsync<AuthException>(() => service.SaveTaskBatchAsync(user.Id, invalid, ct));
        Assert.DoesNotContain((await service.GetAsync(user.Id, ct)).Tasks, task => task.Id == invalid.RequestId);
    }
}
