using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private async Task<(Guid Version, ImportanceContext Context)> ImportanceSubject(Guid uid, Guid id, bool team, CancellationToken ct)
    {
        Require(await db.Users.AsNoTracking().AnyAsync(x => x.Id == uid && x.IsActive && x.EmailConfirmed, ct), "FORBIDDEN", "Tài khoản không khả dụng.", 403);
        if (team) {
            var task = await SharedTask(uid, id, ct); await TeamMember(uid, task.TeamId, ct, true);
            return (task.Version, new(task.Name, task.Description, []));
        }
        var personal = await Find<WorkTask>(uid, id, ct); Require(!personal.IsDeleted, "NOT_FOUND", "Công việc không còn khả dụng.", 404);
        var names = await (from d in db.TaskDependencies.AsNoTracking() join p in db.WorkTasks.AsNoTracking() on d.PredecessorId equals p.Id
            where d.UserId == uid && d.TaskId == id && p.UserId == uid && !p.IsDeleted select p.Name).Take(20).ToArrayAsync(ct);
        return (personal.Version, new(personal.Name, personal.Description, names));
    }
    public async Task<ImportanceProposalView> ProposeImportanceAsync(Guid uid, Guid id, bool team, ImportanceProposalInput input, CancellationToken ct)
    {
        Require(eisenhowerOptions?.Value.Enabled == true, "FEATURE_DISABLED", "Tính năng đang tạm tắt.", 409);
        var subject = await ImportanceSubject(uid, id, team, ct);
        Require(input.Version != Guid.Empty && subject.Version == input.Version, "TASK_STALE", "Task vừa thay đổi. Hãy tải lại để nhận đề xuất mới.", 409);
        var key = $"importance-proposal:{uid}:{team}:{id}:{input.Version}";
        if (eisenhowerCache?.TryGetValue<ImportanceProposalView>(key, out var cached) == true && cached != null) return cached;
        Require(interpreter is IImportanceInterpreter && GetAgentCapabilities().AiAvailable, "AI_NOT_CONFIGURED", "NORA chưa khả dụng. Bạn vẫn có thể tự chọn mức quan trọng.", 503);
        var proposed = await RunJob(uid, "AI_REQUEST", () => ((IImportanceInterpreter)interpreter).SuggestImportanceAsync(subject.Context, ct), ct);
        // A model response is advisory: lower-confidence suggestions remain visible for human review, never auto-saved.
        var source = subject.Context.Name + "\n" + subject.Context.Description + "\n" + string.Join("\n", subject.Context.Dependencies);
        Require(proposed != null && proposed.Level is "Important" or "Lower" or "Unknown" &&
            !string.IsNullOrWhiteSpace(proposed.Reason) && proposed.Reason.Length <= 1000 && double.IsFinite(proposed.Confidence) && proposed.Confidence is >= 0 and <= 1 &&
            proposed.Evidence != null && proposed.Evidence.Length <= 1000 && (proposed.Level == "Unknown" ||
                !string.IsNullOrWhiteSpace(proposed.Evidence) && source.Contains(proposed.Evidence, StringComparison.OrdinalIgnoreCase)),
            "IMPORTANCE_AI_INVALID", "Đề xuất chưa đủ căn cứ từ nội dung task. Bạn có thể thử lại hoặc tự chọn mức quan trọng.", 503);
        db.ChangeTracker.Clear(); var current = await ImportanceSubject(uid, id, team, ct);
        Require(current.Version == input.Version, "TASK_STALE", "Task vừa thay đổi trong lúc phân tích. Hãy tải lại để nhận đề xuất mới.", 409);
        var view = new ImportanceProposalView(input.Version, proposed);
        eisenhowerCache?.Set(key, view, TimeSpan.FromMinutes(20));
        return view;
    }
}
