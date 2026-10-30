using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Workspace;

public sealed class NotificationJobStatus
{
    public DateTimeOffset? LastSuccess { get; set; }
    private long failures;
    public long Failures => Interlocked.Read(ref failures);
    public void Failed() => Interlocked.Increment(ref failures);
}
public sealed partial class WorkspaceService
{
    private async Task AddNotification(Guid uid, string kind, string title, string message, string key, Guid? taskId, CancellationToken ct, Guid? sessionId = null, Guid? planId = null)
    {
        var settings = await db.NotificationSettings.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        var visible = settings?.InAppEnabled != false;
        var emailPending = settings?.EmailEnabled == true && kind is "DEADLINE" or "SESSION_REMINDER";
        if (!visible && !emailPending) return;
        var existing = db.InAppNotifications.Local.FirstOrDefault(x => x.UserId == uid && x.DedupeKey == key)
            ?? await Mine<InAppNotification>(uid).SingleOrDefaultAsync(x => x.DedupeKey == key, ct);
        if (existing is not null)
        {
            // Opting into email must also cover reminders that are already due in the inbox.
            if (emailPending && existing.EmailSentAt is null && existing.EmailAttempts == 0 && !existing.EmailPending)
            { existing.EmailPending = true; existing.NextEmailAttemptAt = Now; existing.Title = title; existing.Message = message; }
            return;
        }
        db.InAppNotifications.Add(new() { UserId = uid, Kind = kind, Title = title, Message = message, DedupeKey = key, TaskId = taskId, SessionId = sessionId, PlanId = planId, ScheduledFor = Now, CreatedAt = Now,
            IsVisible = visible, EmailPending = emailPending, NextEmailAttemptAt = emailPending ? Now : null });
    }
    public Task EnsureNotificationsAsync(Guid uid, CancellationToken ct) => Mutate(uid, async () =>
    {
        var settings = await db.NotificationSettings.SingleOrDefaultAsync(x => x.UserId == uid, ct) ?? new();
        var state = await State(uid, ct); bool missed = false;
        foreach (var session in state.Sessions.Where(x => x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.NeedsReview && x.End < Now &&
            state.Tasks.Any(t => t.Id == x.TaskId && t.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled)))
        {
            if (session.Status == WorkSessionStatus.Scheduled) { session.Status = WorkSessionStatus.NeedsReview; missed = true; }
            await AddNotification(uid, "SESSION_REVIEW", "Bạn đã hoàn thành phiên này chưa?",
                (state.Tasks.FirstOrDefault(x => x.Id == session.TaskId)?.Name ?? "Phiên làm việc") + " đã qua. Mở Hôm nay để ghi nhận kết quả, không cần bật timer.",
                $"review:{session.Id}:{session.End.UtcTicks}", session.TaskId, ct, sessionId: session.Id);
        }
        if (missed) (await Preference(uid, ct)).Revision = Guid.NewGuid();
        foreach (var task in state.Tasks.Where(x => x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled && x.Deadline < Now.AddHours(settings.DeadlineLeadHours)))
            await AddNotification(uid, "DEADLINE", "Công việc sắp đến hạn", task.Name + " · Hạn: " + TimeZoneInfo.ConvertTime(task.Deadline, PlanningEngine.Zone(state.Preference.TimeZoneId)).ToString("dd/MM/yyyy HH:mm") + " (" + state.Preference.TimeZoneId + ").", $"deadline:{task.Id}:{task.Deadline.UtcTicks}", task.Id, ct);
        foreach (var session in state.Sessions.Where(x => x.Status == WorkSessionStatus.Scheduled && x.Start >= Now && x.Start <= Now.AddMinutes(settings.SessionReminderLeadMinutes)))
            await AddNotification(uid, "SESSION_REMINDER", "Sắp đến phiên làm việc", (state.Tasks.FirstOrDefault(x => x.Id == session.TaskId)?.Name ?? "Phiên làm việc") + " · Bắt đầu: " + TimeZoneInfo.ConvertTime(session.Start, PlanningEngine.Zone(state.Preference.TimeZoneId)).ToString("dd/MM/yyyy HH:mm") + " (" + state.Preference.TimeZoneId + ").", $"session:{session.Id}:{session.Start.UtcTicks}", session.TaskId, ct, sessionId: session.Id);
        // Timetables imported from images are FixedEvents, not WorkSessions.
        // Allow a short catch-up window when a browser/worker wakes at start time.
        foreach (var entry in state.Events.Where(x => x.End > Now && x.Start >= Now.AddMinutes(-2) &&
            x.Start <= Now.AddMinutes(settings.SessionReminderLeadMinutes)))
            await AddNotification(uid, "EVENT_REMINDER", entry.Start <= Now ? "Lịch hẹn đang bắt đầu" : "Sắp đến lịch hẹn",
                entry.Name + " · Bắt đầu: " + TimeZoneInfo.ConvertTime(entry.Start, PlanningEngine.Zone(state.Preference.TimeZoneId)).ToString("dd/MM/yyyy HH:mm") + " (" + state.Preference.TimeZoneId + ").",
                $"event:{entry.Id}:{entry.Start.UtcTicks}", null, ct);
        var analysis = engine.Analyze(state, new(Now, 7), Now);
        static int Level(string value) => value.ToUpperInvariant() switch { "MODERATE" => 1, "HIGH" => 2, "OVERLOAD" => 3, _ => 0 };
        if (Level(analysis.RiskLevel) >= Level(settings.WorkloadWarningLevel))
            await AddNotification(uid, "WORKLOAD_WARNING", "Kế hoạch cần được xem lại", $"Có {analysis.HorizonDemandMinutes} phút công việc đến hạn và {analysis.EffectiveCapacityMinutes} phút khả năng thực hiện trong 7 ngày.",
                $"workload:{Now:yyyyMMdd}:{analysis.RiskLevel}", null, ct);
        foreach (var conflict in await DetectConflictsAsync(uid, new(Now, state.Preference.PlanningHorizonDays), ct))
            await AddNotification(uid, "WORKLOAD_WARNING", "Xung đột lịch cần xử lý", conflict.TaskName + ": " + conflict.Message,
                $"conflict:{conflict.TaskId}:{conflict.Code}:{Now:yyyyMMdd}", conflict.TaskId, ct);
        if (missed) { await db.SaveChangesAsync(ct); await UpdateLearningAsync(uid, ct); await PersistRiskAsync(uid, new(Now, state.Preference.PlanningHorizonDays), ct); }
        return true;
    }, ct, false);
    public Task<NotificationView[]> GetNotificationsAsync(Guid uid, CancellationToken ct) => Mine<InAppNotification>(uid).AsNoTracking()
        .Where(x => x.IsVisible).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(100)
        .Select(x => new NotificationView(x.Id, x.Kind, x.Title, x.Message, x.TaskId, x.CreatedAt, x.ReadAt != null)).ToArrayAsync(ct);
    public Task MarkNotificationAsync(Guid uid, Guid id, CancellationToken ct) => Mutate(uid, async () => (await Find<InAppNotification>(uid, id, ct)).ReadAt = Now, ct, false);
    public Task<AdminUserView[]> SearchUsersAsync(string? search, CancellationToken ct)
    {
        var term = (search ?? "").Trim(); Require(term.Length <= 100, "VALIDATION_ERROR", "Từ khóa quá dài.");
        return db.Users.AsNoTracking().Where(x => term == "" || x.Email!.Contains(term) || x.FirstName.Contains(term) || x.LastName.Contains(term) || x.UserName!.Contains(term))
            .OrderBy(x => x.Email).Take(100).Select(x => new AdminUserView(x.Id, x.Email!, x.FirstName, x.LastName, x.IsActive, x.EmailConfirmed, x.LastLoginAt,
                x.UserName, x.CreatedAt, 0, 0,
                db.RefreshTokens.Count(t => t.UserId == x.Id && t.RevokedAt == null && t.ExpiresAt > Now))).ToArrayAsync(ct);
    }
    public Task SetUserActiveAsync(Guid adminId, Guid targetId, bool active, CancellationToken ct, string? reason = null) => Mutate(adminId, async () =>
    {
        Require(adminId != targetId, "CANNOT_LOCK_SELF", "Không khóa tài khoản quản trị đang dùng.");
        var target = await db.Users.SingleOrDefaultAsync(x => x.Id == targetId, ct);
        Require(target is not null, "NOT_FOUND", "Không tìm thấy tài khoản.", 404);
        // Lock the target account too: the admin lock serializes with target login/refresh.
        if (db.Database.IsSqlServer())
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; SELECT @result;";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@key";
            parameter.Value = "SPWPOS:account:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(target!.NormalizedEmail!)));
            command.Parameters.Add(parameter); Require(Convert.ToInt32(await command.ExecuteScalarAsync(ct)) >= 0, "REQUEST_BUSY", "Hãy thử lại.", 409);
        }
        target!.IsActive = active; target.UpdatedAt = Now; target.SecurityStamp = Guid.NewGuid().ToString();
        Require((reason?.Length ?? 0) <= 500, "VALIDATION_ERROR", "Lý do tối đa 500 ký tự.");
        var effectiveReason = string.IsNullOrWhiteSpace(reason) ? (active ? "Mở khóa bởi quản trị viên" : "Vi phạm quy định sử dụng dịch vụ") : reason.Trim();
        db.AccountStatusLogs.Add(new() { UserId = targetId, AdminId = adminId, Action = active ? "UNLOCK" : "LOCK", Reason = effectiveReason, CreatedAt = Now });
        if (!active)
        {
            db.InAppNotifications.Add(new()
            {
                UserId = targetId,
                Kind = "ACCOUNT_LOCKED",
                Title = "Tài khoản bị khóa",
                Message = $"Bạn đã bị khóa tài khoản vì lí do : {effectiveReason}",
                DedupeKey = $"account-locked:{targetId}:{Now.ToUnixTimeSeconds()}",
                CreatedAt = Now,
                ScheduledFor = Now,
                IsVisible = true
            });
            if (!string.IsNullOrWhiteSpace(target.Email) && email is not null)
            {
                try
                {
                    await email.SendReminderAsync(
                        target.Email,
                        "Tài khoản của bạn đã bị khóa",
                        $"Chào bạn,\n\nTài khoản PLANORA của bạn đã bị khóa bởi quản trị viên vì lí do:\n{effectiveReason}\n\nNếu bạn cho rằng đây là sự nhầm lẫn, vui lòng liên hệ quản trị viên để được hỗ trợ.",
                        ct);
                }
                catch { }
            }
        }
        if (active) { target.LockoutEnd = null; target.AccessFailedCount = 0; }
        foreach (var token in await db.RefreshTokens.Where(x => x.UserId == targetId && x.RevokedAt == null).ToArrayAsync(ct)) token.RevokedAt = Now;
        db.SecurityAuditLogs.Add(new() { UserId = targetId, EventType = active ? SPWPOS.Domain.Enums.SecurityEventType.ADMIN_ACCOUNT_UNLOCKED : SPWPOS.Domain.Enums.SecurityEventType.ADMIN_ACCOUNT_LOCKED, CreatedAt = Now, IsSuccessful = true, Metadata = "administrator=" + adminId });
    }, ct, false);
    public async Task<SystemStatusView> SystemStatusAsync(CancellationToken ct)
    {
        var cutoff = Now.AddHours(-24);
        return new(await db.Database.CanConnectAsync(ct), await db.Users.CountAsync(ct), await db.WorkTasks.CountAsync(x => !x.IsDeleted, ct),
            await db.WorkSessions.CountAsync(x => x.Status == WorkSessionStatus.Running || x.Status == WorkSessionStatus.Paused, ct),
            await db.SecurityAuditLogs.CountAsync(x => x.EventType == SPWPOS.Domain.Enums.SecurityEventType.LOGIN_FAILED && x.CreatedAt >= cutoff, ct), Now,
            await db.SystemJobRuns.Where(x => x.JobType == "NOTIFICATION_SCAN" && x.Status == "SUCCESS").OrderByDescending(x => x.FinishedAt).Select(x => x.FinishedAt).FirstOrDefaultAsync(ct),
            await db.SystemJobRuns.LongCountAsync(x => x.JobType == "NOTIFICATION_SCAN" && x.Status == "FAILED", ct));
    }
}
public sealed class WorkspaceNotificationWorker(IServiceScopeFactory scopes, NotificationJobStatus status, ILogger<WorkspaceNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var failuresBefore = status.Failures;
                Guid[] users;
                using (var scope = scopes.CreateScope()) users = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
                    .Where(x => x.IsActive && x.EmailConfirmed).Select(x => x.Id).ToArrayAsync(stoppingToken);
                foreach (var uid in users)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        var workspace = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
                        await workspace.EnsureNotificationsAsync(uid, stoppingToken);
                        await workspace.ScanRecoveryAsync(uid, stoppingToken);
                        await scope.ServiceProvider.GetRequiredService<SPWPOS.Infrastructure.Email.ReminderNotificationService>().SendPendingAsync(uid, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception e)
                    {
                        // One unavailable account must not prevent reminders for others.
                        status.Failed(); logger.LogWarning("Notification scan for {UserId} failed with {FailureType}", uid, e.GetType().Name);
                        using var failureScope = scopes.CreateScope(); var failureDb = failureScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        failureDb.SystemJobRuns.Add(new() { UserId = uid, JobType = "NOTIFICATION_SCAN", Status = "FAILED", ErrorMessage = e.GetType().Name, StartedAt = DateTimeOffset.UtcNow, FinishedAt = DateTimeOffset.UtcNow });
                        await failureDb.SaveChangesAsync(stoppingToken);
                    }
                }
                using (var resultScope = scopes.CreateScope())
                {
                    var resultDb = resultScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var success = status.Failures == failuresBefore;
                    resultDb.SystemJobRuns.Add(new() { JobType = "NOTIFICATION_SCAN", Status = success ? "SUCCESS" : "FAILED", ErrorMessage = success ? null : "PARTIAL_SCAN_FAILURE", StartedAt = DateTimeOffset.UtcNow, FinishedAt = DateTimeOffset.UtcNow });
                    await resultDb.SaveChangesAsync(stoppingToken);
                    await resultScope.ServiceProvider.GetRequiredService<WorkspaceHistoryMaintenance>().PruneAsync(stoppingToken);
                }
                if (status.Failures == failuresBefore) status.LastSuccess = DateTimeOffset.UtcNow;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { status.Failed(); logger.LogWarning("Workspace notification scan failed with {FailureType}", e.GetType().Name); }
        }
    }
}
public sealed class AdminBootstrapWorker(IServiceScopeFactory scopes, IConfiguration configuration, IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var email = configuration["AdminBootstrap:Email"];
        if (string.IsNullOrWhiteSpace(email)) return;
        if (!environment.IsDevelopment()) throw new InvalidOperationException("AdminBootstrap is available only in Development; production role assignment requires an operator-controlled process.");
        using var scope = scopes.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(); var user = await users.FindByEmailAsync(email);
        if (user is null || !user.EmailConfirmed || !user.IsActive) throw new InvalidOperationException("AdminBootstrap requires an existing active, verified account.");
        if (!await roles.RoleExistsAsync("Admin"))
        { var result = await roles.CreateAsync(new IdentityRole<Guid>("Admin")); if (!result.Succeeded) throw new InvalidOperationException("Could not create Admin role."); }
        if (!await users.IsInRoleAsync(user, "Admin"))
        { var result = await users.AddToRoleAsync(user, "Admin"); if (!result.Succeeded) throw new InvalidOperationException("Could not assign Admin role."); }
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
