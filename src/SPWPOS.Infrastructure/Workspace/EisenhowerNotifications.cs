using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private async Task NotifyEisenhowerTransitions(Guid uid, CancellationToken ct)
    {
        if (eisenhowerOptions?.Value is not { Enabled: true, NotificationsEnabled: true }) return;
        var view = await GetEisenhowerAsync(uid, ct);
        var previous = await Mine<PriorityAlertState>(uid).ToDictionaryAsync(x => x.TaskId, ct);
        foreach (var task in view.Tasks.Where(t => t.Kind != "Review")) {
            var signal = task.AtRisk ? "Risk" : task.Quadrant == "Q1" ? "Urgent" : "";
            if (!previous.TryGetValue(task.Id, out var row)) {
                // Establish a baseline silently. Enabling the feature must not flood old accounts.
                db.PriorityAlertStates.Add(new() { UserId = uid, TaskId = task.Id, LastSignal = signal }); continue;
            }
            if (signal == row.LastSignal) { row.ClearSince = null; continue; }
            if (signal == "" || row.LastSignal == "Risk" && signal == "Urgent") {
                row.ClearSince ??= Now;
                if (Now - row.ClearSince >= TimeSpan.FromMinutes(10)) { row.LastSignal = signal; row.Episode = Guid.NewGuid(); row.ClearSince = null; }
                continue;
            }
            // Escalations are immediate, including overdue/capacity failure. Recovery must remain stable.
            row.LastSignal = signal; row.ClearSince = null;
            await AddNotification(uid, "EISENHOWER_ALERT", task.AtRisk ? "Công việc có nguy cơ trễ hạn" : "Công việc quan trọng cần làm trước",
                task.Name + " · " + task.Explanation + (task.Kind == "NeedsAcceptance" ? " Mở nhóm để xem và nhận task." : ""),
                $"eisenhower:{task.Id}:{row.Episode}:{signal}", task.Kind == "Personal" || task.Kind == "Accepted" ? task.Id : null, ct);
        }
        // Task removal/completion stops old episodes. No cascade to user data.
        db.PriorityAlertStates.RemoveRange(previous.Values.Where(x => !view.Tasks.Any(t => t.Id == x.TaskId)));
    }
}
