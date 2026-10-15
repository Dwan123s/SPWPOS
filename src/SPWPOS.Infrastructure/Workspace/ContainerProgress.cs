using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private async Task UpdateContainerProgressAsync(Guid uid, CancellationToken ct)
    {
        var tasks = await Mine<WorkTask>(uid).Where(x => !x.IsDeleted).ToArrayAsync(ct);
        var children = tasks.Where(x => x.ParentId != null).ToLookup(x => x.ParentId!.Value);
        var containers = tasks.Where(x => x.Status != WorkTaskStatus.Cancelled && children[x.Id].Any()).ToArray();
        if (containers.Length == 0) return;
        var original = containers.ToDictionary(x => x.Id, x => (x.Status, x.Progress, x.CompletedAt));
        foreach (var parent in containers)
        {
            var required = children[parent.Id].Where(x => x.Status != WorkTaskStatus.Cancelled).ToArray();
            if (required.Length == 0) continue;
            var weight = required.Sum(x => Math.Max(1, x.EstimatedMinutes));
            parent.Progress = (int)Math.Floor(required.Sum(x => Math.Max(1, x.EstimatedMinutes) * (double)x.Progress) / weight);
            // Reopened children reopen the container; an old Completed status
            // must not seed completion before the current graph is evaluated.
            parent.Status = parent.Progress > 0 ? WorkTaskStatus.InProgress : WorkTaskStatus.Todo;
            parent.CompletedAt = null;
        }
        var state = new PlanningState(tasks, await Mine<TaskDependency>(uid).ToArrayAsync(ct), [], [], [], [], await Preference(uid, ct));
        var completed = engine.CompletionTimes(state, [], [new("All", 0, 1, false)], Now);
        foreach (var parent in containers)
        {
            var before = original[parent.Id];
            if (completed.ContainsKey(parent.Id))
            { parent.Status = WorkTaskStatus.Completed; parent.Progress = 100; parent.CompletedAt = before.CompletedAt ?? Now; }
            else if (parent.Progress == 100) parent.Progress = 99;
            if (before != (parent.Status, parent.Progress, parent.CompletedAt)) parent.UpdatedAt = Now;
        }
        await db.SaveChangesAsync(ct);
    }
}
