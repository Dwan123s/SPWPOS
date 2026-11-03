using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static bool SameTaskContract(WorkTask a, WorkTask b) => a.Name == b.Name && a.Description == b.Description && a.Deadline == b.Deadline &&
        a.EstimatedMinutes == b.EstimatedMinutes && a.Priority == b.Priority && a.Splittable == b.Splittable && a.MinimumSessionMinutes == b.MinimumSessionMinutes &&
        a.TaskType == b.TaskType && a.IsHardDeadline == b.IsHardDeadline && a.WorkWindowStart == b.WorkWindowStart && a.WorkWindowEnd == b.WorkWindowEnd &&
        a.CategoryId == b.CategoryId && a.ParentId == b.ParentId;
    private static ImportanceView ImportanceDto(ITaskImportance task) => task is WorkTask w && w.ImportanceLevel == "Unknown" && w.InheritedImportance is { } level
        ? new(level, "Leader", w.InheritedImportanceReason ?? "", null, true)
        : new(task.ImportanceLevel, task.ImportanceSource, task.ImportanceReason, task.ImportanceConfidence, task.ImportanceConfirmedAt != null);

    private static ImportanceView SuggestedImportance(ImportanceSuggestion? suggestion, string? source = null)
    {
        if (suggestion == null || suggestion.Level is not ("Important" or "Lower") || !double.IsFinite(suggestion.Confidence) ||
            suggestion.Confidence is < .8 or > 1 || string.IsNullOrWhiteSpace(suggestion.Reason) || suggestion.Reason.Length > 1000 ||
            string.IsNullOrWhiteSpace(suggestion.Evidence) || suggestion.Evidence.Length > 1000 ||
            source != null && !source.Contains(suggestion.Evidence, StringComparison.OrdinalIgnoreCase))
            return new("Unknown", "Unknown", "Cần làm rõ ảnh hưởng của công việc.", null);
        return new(suggestion.Level, "AI", suggestion.Reason, suggestion.Confidence);
    }
    private static void WriteImportance(ITaskImportance target, ImportanceView? value, DateTimeOffset? confirmedAt = null, string? source = null)
    {
        if (value == null) return;
        target.ImportanceLevel = value.Level is "Important" or "Lower" ? value.Level : "Unknown";
        target.ImportanceReason = (value.Reason ?? "")[..Math.Min(value.Reason?.Length ?? 0, 1000)];
        target.ImportanceSource = source ?? value.Source;
        target.ImportanceConfidence = value.Confidence;
        target.ImportanceConfirmedAt = target.ImportanceLevel == "Unknown" ? null : confirmedAt;
    }
    private async Task<(PlanningState State, Dictionary<Guid, (Guid SharedId, Guid TeamId, string Kind)> Map, EisenhowerTaskView[] Reviews)> EisenhowerState(Guid uid, CancellationToken ct)
    {
        var state = await State(uid, ct);
        var owned = await (from a in db.TeamTaskAssignments.AsNoTracking() join t in db.TeamTasks.AsNoTracking() on a.TeamTaskId equals t.Id
            join m in db.TeamMemberships.AsNoTracking() on t.TeamId equals m.TeamId
            where a.UserId == uid && a.Active && m.UserId == uid && m.Active
            select new { a.WorkTaskId, Task = t }).ToArrayAsync(ct);
        var map = owned.ToDictionary(x => x.WorkTaskId, x => (x.Task.Id, x.Task.TeamId, "Accepted"));
        var candidates = await (from t in db.TeamTasks.AsNoTracking() join m in db.TeamMemberships.AsNoTracking() on t.TeamId equals m.TeamId
            where m.Active && m.UserId == uid && t.DesignatedUserId == uid && t.AssigneeId == null && t.Status == "Open" select t).ToArrayAsync(ct);
        // Submitted work is already Completed in the personal model. Keep it in the snapshot
        // so dependency completion remains known; Remaining excludes it from the matrix.
        var personal = state.Tasks.ToList();
        foreach (var task in candidates) {
            personal.Add(new() { Id = task.Id, UserId = uid, Name = task.Name, Description = task.Description, Deadline = task.Deadline,
                EstimatedMinutes = task.EstimatedMinutes, Priority = task.Priority, Splittable = task.Splittable, IsHardDeadline = true,
                ImportanceLevel = task.ImportanceLevel, ImportanceReason = task.ImportanceReason });
            map[task.Id] = (task.Id, task.TeamId, "NeedsAcceptance");
        }
        var reviewTasks = await (from t in db.TeamTasks.AsNoTracking() join m in db.TeamMemberships.AsNoTracking() on t.TeamId equals m.TeamId
            where m.UserId == uid && m.Active && m.Role == "Leader" && t.Status == "PendingReview" select t).ToArrayAsync(ct);
        var reviews = reviewTasks.Select(t => new EisenhowerTaskView(t.Id, t.Name, t.Deadline, t.ImportanceLevel, "Unknown", "Review",
            "Thành viên đã gửi kết quả. Cần trưởng nhóm kiểm tra và duyệt.", ["REVIEW_REQUIRED"], 0, null, false, false, false, false, "Review", t.Id, t.TeamId)).ToArray();
        return (state with { Tasks = personal.ToArray() }, map, reviews);
    }
    public async Task<EisenhowerView> GetEisenhowerAsync(Guid uid, CancellationToken ct)
    {
        Require(await db.Users.AsNoTracking().AnyAsync(x => x.Id == uid && x.IsActive && x.EmailConfirmed, ct), "FORBIDDEN", "Tài khoản không khả dụng.", 403);
        var now = Now;
        if (eisenhowerOptions?.Value.Enabled != true) return new([], now, now.AddMinutes(1), "disabled", Enabled: false);
        var (state, map, reviews) = await EisenhowerState(uid, ct);
        // Includes task metadata, inherited team importance, assignment and every scheduling constraint.
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { uid, state, map, reviews }))));
        var key = "eisenhower:" + uid + ":" + fingerprint;
        if (eisenhowerCache?.TryGetValue<EisenhowerView>(key, out var cached) == true && cached != null && cached.NextEvaluationAt > now) return cached;
        var tasks = engine.AssessEisenhower(state, now).Select(t => map.TryGetValue(t.Id, out var link)
            ? t with { Kind = link.Kind, TeamTaskId = link.SharedId, TeamId = link.TeamId } : t).Concat(reviews).ToArray();
        var next = tasks.Select(t => t.Deadline.AddHours(-24)).Where(t => t > now).DefaultIfEmpty(now.AddSeconds(30)).Min();
        if (next > now.AddSeconds(30)) next = now.AddSeconds(30);
        var view = new EisenhowerView(tasks, now, next, fingerprint);
        eisenhowerCache?.Set(key, view, next);
        return view;
    }
    public async Task<EisenhowerTaskView> GetTaskEisenhowerAsync(Guid uid, Guid id, bool team, CancellationToken ct)
    {
        if (team) {
            var task = await SharedTask(uid, id, ct);
            Require(task.AssigneeId == uid || task.DesignatedUserId == uid || task.Status == "Open" && task.DesignatedUserId == null,
                "TEAM_FORBIDDEN", "Chỉ xem được đánh giá theo lịch của chính bạn.", 403);
        } else await Find<WorkTask>(uid, id, ct);
        var view = await GetEisenhowerAsync(uid, ct);
        var result = view.Tasks.FirstOrDefault(t => team ? t.TeamTaskId == id : t.Id == id);
        if (result != null) return result;
        if (team) {
            var task = await SharedTask(uid, id, ct);
            Require(task.Status == "Open", "NOT_FOUND", "Công việc không cần đánh giá thực hiện.", 404);
            var (state, _, _) = await EisenhowerState(uid, ct);
            var candidate = new WorkTask { Id = task.Id, UserId = uid, Name = task.Name, Deadline = task.Deadline, EstimatedMinutes = task.EstimatedMinutes,
                Priority = task.Priority, Splittable = task.Splittable, ImportanceLevel = task.ImportanceLevel, IsHardDeadline = true };
            return engine.AssessEisenhower(state with { Tasks = state.Tasks.Append(candidate).ToArray() }, Now).Single(t => t.Id == task.Id)
                with { Kind = "Candidate", TeamTaskId = id, TeamId = task.TeamId };
        }
        throw new SPWPOS.Application.Authentication.AuthException("NOT_FOUND", "Không tìm thấy công việc đang cần thực hiện.", 404);
    }
    public Task SetImportanceAsync(Guid uid, Guid id, bool team, ImportanceInput input, CancellationToken ct) => Mutate(uid, async () => {
        Require(input.Level is "Important" or "Lower" or "Unknown" && !string.IsNullOrWhiteSpace(input.Reason) && input.Reason.Length <= 1000,
            "VALIDATION_ERROR", "Mức quan trọng hoặc lý do không hợp lệ.");
        var value = new ImportanceView(input.Level, team ? "Leader" : "User", input.Reason.Trim(), null, true);
        if (team) {
            var task = await SharedTask(uid, id, ct); var member = await TeamMember(uid, task.TeamId, ct, true);
            Require(task.Version == input.Version, "TASK_STALE", "Công việc vừa thay đổi. Hãy tải lại.", 409);
            WriteImportance(task, value, Now); task.Version = Guid.NewGuid(); member.MutationVersion = Guid.NewGuid();
            foreach (var account in await db.TeamTaskAssignments.Where(a => a.TeamTaskId == id && a.Active).Select(a => a.UserId).Distinct().ToArrayAsync(ct))
                (await Preference(account, ct)).Revision = Guid.NewGuid();
            TeamLog(uid, task.TeamId, task.Id, "TASK_IMPORTANCE_CHANGED");
        } else {
            var task = await Find<WorkTask>(uid, id, ct);
            Require(!task.IsDeleted && task.Version == input.Version, "TASK_STALE", "Công việc vừa thay đổi. Hãy tải lại.", 409);
            WriteImportance(task, value, Now); task.Version = Guid.NewGuid(); task.UpdatedAt = Now;
        }
        (await Preference(uid, ct)).Revision = Guid.NewGuid();
    }, ct, false);
}
