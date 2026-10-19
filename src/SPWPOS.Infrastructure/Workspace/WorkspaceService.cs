using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService(ApplicationDbContext db, PlanningEngine engine, TimeProvider clock,
    ITaskInterpreter interpreter, IAiUsageService aiUsage, IEmailService? email = null) : IWorkspaceService, IPlanningAgentService
{
    private DateTimeOffset Now => clock.GetUtcNow();
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string code, string message, int status = 400)
    { if (!condition) throw new AuthException(code, message, status); }
    private IQueryable<T> Mine<T>(Guid userId) where T : OwnedRecord => db.Set<T>().Where(x => x.UserId == userId);
    private async Task<T> Find<T>(Guid userId, Guid id, CancellationToken ct) where T : OwnedRecord =>
        await Mine<T>(userId).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AuthException("NOT_FOUND", "Không tìm thấy dữ liệu.", 404);
    private async Task<WorkPreference> Preference(Guid uid, CancellationToken ct)
    {
        var preference = db.WorkPreferences.Local.SingleOrDefault(x => x.UserId == uid) ?? await Mine<WorkPreference>(uid).SingleOrDefaultAsync(ct);
        if (preference is not null) return preference;
        preference = new() { UserId = uid }; db.WorkPreferences.Add(preference); return preference;
    }
    private async Task<T> Mutate<T>(Guid uid, Func<Task<T>> action, CancellationToken ct, bool revision = true)
    {
        var email = await db.Users.AsNoTracking().Where(x => x.Id == uid).Select(x => x.NormalizedEmail).SingleOrDefaultAsync(ct);
        Require(email is not null, "UNAUTHORIZED", "Tài khoản không khả dụng.", 401);
        await using var tx = await AccountTransaction.BeginAsync(db, email!, ct);
        Require(await db.Users.AnyAsync(x => x.Id == uid && x.IsActive && x.EmailConfirmed, ct), "FORBIDDEN", "Tài khoản không khả dụng.", 403);
        var preference = await Preference(uid, ct);
        var result = await action();
        db.ChangeTracker.DetectChanges();
        revision = revision && db.ChangeTracker.Entries().Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        if (revision) preference.Revision = Guid.NewGuid();
        db.ChangeTracker.DetectChanges();
        var tasksChanged = db.ChangeTracker.Entries<WorkTask>().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        var timeChanged = db.ChangeTracker.Entries().Any(e =>
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
            (e.Entity is WeeklyAvailability or AvailabilityException or FixedEvent ||
             e.Entity is WorkPreference && e.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(WorkPreference.Revision))));
        foreach (var entry in db.ChangeTracker.Entries<WorkSession>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        { if (entry.State == EntityState.Added) entry.Entity.CreatedAt = Now; entry.Entity.UpdatedAt = Now; }
        await db.SaveChangesAsync(ct);
        if (revision && tasksChanged) await UpdateContainerProgressAsync(uid, ct);
        if (revision && timeChanged)
            await RescheduleTimeConflictsAsync(uid, ct);
        if (revision) { await UpdateLearningAsync(uid, ct); await PersistRiskAsync(uid, new(Now, preference.PlanningHorizonDays), ct); await db.SaveChangesAsync(ct); }
        await tx.CommitAsync(ct); return result;
    }
    private Task Mutate(Guid uid, Func<Task> action, CancellationToken ct, bool revision = true) =>
        Mutate(uid, async () => { await action(); return true; }, ct, revision);
    private async Task<PlanningState> State(Guid uid, CancellationToken ct)
    {
        // Default preference is immutable until the first mutation persists it.
        var preference = db.WorkPreferences.Local.SingleOrDefault(x => x.UserId == uid) ?? await Mine<WorkPreference>(uid).SingleOrDefaultAsync(ct) ?? new WorkPreference { UserId = uid, Revision = Guid.Empty };
        var dependencies = await Mine<TaskDependency>(uid).ToArrayAsync(ct);
        var taskQuery = Mine<WorkTask>(uid).Where(x => !x.IsDeleted);
        var tasks = await taskQuery.OrderBy(x => x.Status == WorkTaskStatus.Completed || x.Status == WorkTaskStatus.Cancelled)
            .ThenByDescending(x => x.CreatedAt).Take(5000).ToArrayAsync(ct);
        var activeIds = tasks.Where(x => x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled).Select(x => x.Id).ToArray();
        var requiredIds = tasks.Where(x => activeIds.Contains(x.Id) && x.ParentId != null).Select(x => x.ParentId!.Value)
            .Concat(dependencies.Where(x => activeIds.Contains(x.TaskId)).Select(x => x.PredecessorId)).Distinct().ToArray();
        // A bounded history must not hide a completed predecessor or a child of
        // an active container; both affect whether active work can be scheduled.
        var related = await taskQuery.Where(x => requiredIds.Contains(x.Id) || x.ParentId != null && activeIds.Contains(x.ParentId.Value)).ToArrayAsync(ct);
        tasks = tasks.Concat(related).DistinctBy(x => x.Id).ToArray();
        return new(tasks, dependencies, await Mine<WeeklyAvailability>(uid).ToArrayAsync(ct),
            await Mine<AvailabilityException>(uid).ToArrayAsync(ct), await Mine<FixedEvent>(uid).ToArrayAsync(ct),
            await Mine<WorkSession>(uid).Where(x => x.Status != WorkSessionStatus.Cancelled)
                .OrderBy(x => x.Status == WorkSessionStatus.Running || x.Status == WorkSessionStatus.Paused ? 0 : x.Status == WorkSessionStatus.Scheduled ? 1 : 2)
                .ThenByDescending(x => x.Start).Take(10000).ToArrayAsync(ct), preference,
            await Mine<EstimationObservation>(uid).OrderByDescending(x => x.ObservedAt).Take(500).ToArrayAsync(ct));
    }
    public async Task<WorkspaceView> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId, ct); var state = await State(userId, ct); var bias = engine.Biases(state);
        var categories = await Mine<TaskCategory>(userId).OrderBy(x => x.Name).Select(x => new CategoryView(x.Id, x.Name, x.Color)).ToArrayAsync(ct);
        var notifications = await Mine<InAppNotification>(userId).Where(x => x.IsVisible).OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new NotificationView(x.Id, x.Kind, x.Title, x.Message, x.TaskId, x.CreatedAt, x.ReadAt != null)).ToArrayAsync(ct);
        var preference = state.Preference;
        var sessionsByTask = state.Sessions.ToLookup(x => x.TaskId);
        var dependenciesByTask = state.Dependencies.ToLookup(x => x.TaskId);
        var tasksById = state.Tasks.ToDictionary(x => x.Id);
        var profile = await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        var ns = await db.NotificationSettings.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct) ?? new();
        return new(new(user.Id, user.Email!, user.FirstName, user.LastName, profile?.DisplayName ?? (user.FirstName + " " + user.LastName), profile?.Bio ?? "", profile?.AvatarUrl ?? ""),
            new(preference.TimeZoneId, preference.MinimumSessionMinutes, preference.MaximumSessionMinutes, preference.BreakMinutes, preference.PreferredStartMinute, preference.PreferredEndMinute, preference.Revision, preference.PlanningHorizonDays, preference.PreferredPeriod, preference.MaximumDailyMinutes, preference.AutoReschedule),
            categories, state.Tasks.Select(t => new TaskView(t.Id, t.Name, t.Description, t.Deadline, t.EstimatedMinutes, engine.Adjusted(t, bias), engine.Remaining(t, bias),
                (int)(sessionsByTask[t.Id].Sum(s => s.ActualSeconds) / 60),
                (int)sessionsByTask[t.Id].Where(s => s.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused).Sum(s => (s.End - s.Start).TotalMinutes),
                t.Priority, t.TaskType, t.Splittable, t.MinimumSessionMinutes, t.CategoryId, t.ParentId, t.Status.ToString(), t.Progress, t.CreatedAt, t.CompletedAt,
                dependenciesByTask[t.Id].Select(d => d.PredecessorId).ToArray(), t.IsHardDeadline, t.CreationSource, t.WorkWindowStart, t.WorkWindowEnd)).ToArray(),
            state.Availability.Select(x => new AvailabilityInput(x.DayOfWeek, x.StartMinute, x.EndMinute)).ToArray(),
            state.Exceptions.Select(x => new ExceptionView(x.Id, x.Date, x.StartMinute, x.EndMinute, x.IsAvailable, x.Note, x.OverridesWeeklyAvailability)).ToArray(),
            state.Events.Select(x => new EventView(x.Id, x.Name, x.Start, x.End, x.Description, x.EventType)).ToArray(),
            state.Sessions.Where(s => tasksById.ContainsKey(s.TaskId)).Select(s => new SessionView(s.Id, s.TaskId, tasksById[s.TaskId].Name,
                s.Start, s.End, s.Status.ToString(), s.IsLocked, (int)((s.ActualSeconds + (s.SegmentStartedAt is { } at ? Math.Max(0, (Now - at).TotalSeconds) : 0)) / 60),
                s.ActualSeconds + (long)(s.SegmentStartedAt is { } since ? Math.Max(0, (Now - since).TotalSeconds) : 0),
                s.SegmentStartedAt, s.ReasonCode)).ToArray(), notifications, new(ns.InAppEnabled, ns.DeadlineLeadHours, ns.SessionReminderLeadMinutes, ns.WorkloadWarningLevel, ns.EmailEnabled), ["history-v2", "recovery"]);
    }
    public Task UpdateProfileAsync(Guid userId, ProfileInput input, CancellationToken ct) => Mutate(userId, async () =>
    {
        Require(!string.IsNullOrWhiteSpace(input.FirstName) && !string.IsNullOrWhiteSpace(input.LastName), "VALIDATION_ERROR", "Tên không được để trống.");
        var user = await db.Users.SingleAsync(x => x.Id == userId, ct); user.FirstName = input.FirstName.Trim(); user.LastName = input.LastName.Trim(); user.UpdatedAt = Now;
        var profile = await EnsureProfileAsync(userId, ct);
        var displayName = (input.DisplayName ?? (user.FirstName + " " + user.LastName)).Trim();
        Require(displayName.Length is > 0 and <= 100, "VALIDATION_ERROR", "Tên hiển thị tối đa 100 ký tự.");
        profile.DisplayName = displayName;
        profile.Bio = (input.Bio ?? "").Trim();
        profile.AvatarUrl = (input.AvatarUrl ?? "").Trim();
        profile.UpdatedAt = Now;
    }, ct, false);
    private static void ValidTask(TaskInput input)
    {
        Require(!string.IsNullOrWhiteSpace(input.Name) && !string.IsNullOrWhiteSpace(input.TaskType), "VALIDATION_ERROR", "Nhập tên và loại công việc.");
        Require(input.EstimatedMinutes is >= 1 and <= 43200 && input.Priority is >= 1 and <= 4 && input.MinimumSessionMinutes is >= 5 and <= 480,
            "VALIDATION_ERROR", "Thời lượng hoặc mức ưu tiên không hợp lệ.");
        Require(input.Name.Length <= 200 && (input.Description?.Length ?? 0) <= 4000 && input.TaskType.Length <= 60, "VALIDATION_ERROR", "Nội dung quá dài.");
        Require(input.Deadline != default, "VALIDATION_ERROR", "Deadline là bắt buộc.");
        Require(input.CreationSource is "MANUAL" or "NATURAL_LANGUAGE" or "AI_BREAKDOWN", "VALIDATION_ERROR", "Nguồn công việc không hợp lệ.");
    }
    private static WorkTask NewTask(Guid uid, TaskInput input, DateTimeOffset now) => new()
    {
        UserId = uid, Name = input.Name.Trim(), Description = input.Description ?? "", Deadline = input.Deadline,
        EstimatedMinutes = input.EstimatedMinutes, Priority = input.Priority, TaskType = NormalizeTaskType(input.TaskType), Splittable = input.Splittable,
        IsHardDeadline = input.IsHardDeadline, CreationSource = input.CreationSource,
        MinimumSessionMinutes = input.MinimumSessionMinutes, CategoryId = input.CategoryId, ParentId = input.ParentId, CreatedAt = now, UpdatedAt = now
    };
    private static string NormalizeTaskType(string type) => type.Trim().ToUpperInvariant() switch
    { "GENERAL" => "OTHER", "MEETING" => "MEETING_PREP", "RESEARCH" or "LEARNING" => "STUDY", "PERSONAL" => "ADMIN", var code => code };
    private async Task<UserProfile> EnsureProfileAsync(Guid uid, CancellationToken ct)
    {
        var profile = db.UserProfiles.Local.SingleOrDefault(x => x.UserId == uid) ?? await db.UserProfiles.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        if (profile is not null) return profile;
        var user = await db.Users.SingleAsync(x => x.Id == uid, ct);
        var name = (user.FirstName + " " + user.LastName).Trim();
        profile = new() { UserId = uid, DisplayName = name[..Math.Min(name.Length, 100)], TimeZoneId = (await Preference(uid, ct)).TimeZoneId, UpdatedAt = Now };
        db.UserProfiles.Add(profile); return profile;
    }
}
