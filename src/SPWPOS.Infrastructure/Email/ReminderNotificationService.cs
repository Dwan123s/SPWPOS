using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;

namespace SPWPOS.Infrastructure.Email;

public sealed class ReminderNotificationService(ApplicationDbContext db, IEmailService email, TimeProvider clock,
    ILogger<ReminderNotificationService> logger)
{
    public async Task SendPendingAsync(Guid userId, CancellationToken ct)
    {
        var address = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.NormalizedEmail).SingleOrDefaultAsync(ct);
        if (address is null) return;
        await using var tx = await AccountTransaction.BeginAsync(db, address, ct);
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId, ct);
        var enabled = user.IsActive && user.EmailConfirmed && await db.NotificationSettings.AnyAsync(x => x.UserId == userId && x.EmailEnabled, ct);
        var now = clock.GetUtcNow();
        var pending = await db.InAppNotifications.Where(x => x.UserId == userId && x.EmailPending &&
            x.EmailSentAt == null && x.ScheduledFor <= now && x.NextEmailAttemptAt <= now).OrderBy(x => x.CreatedAt).Take(20).ToArrayAsync(ct);
        foreach (var notification in pending)
        {
            var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.Id == notification.TaskId, ct);
            var current = enabled && task is { IsDeleted: false, Status: not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled };
            if(notification.Kind == "NORA_REMINDER")
                current = enabled && notification.ScheduledFor <= now && notification.ScheduledFor >= now.AddDays(-1);
            else if (notification.Kind == "SESSION_REMINDER")
            {
                var session = await db.WorkSessions.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.Id == notification.SessionId, ct);
                current &= session is { Status: WorkSessionStatus.Scheduled } && session.Start > now &&
                    notification.DedupeKey == $"session:{session.Id}:{session.Start.UtcTicks}";
            }
            else current &= notification.Kind == "DEADLINE" && task?.Deadline is { } deadline && deadline > now &&
                notification.DedupeKey == $"deadline:{task.Id}:{deadline.UtcTicks}";
            if (!current) { notification.EmailPending = false; continue; }
            notification.EmailAttempts++;
            try
            {
                await email.SendReminderAsync(user.Email!, notification.Title, notification.Message, ct);
                notification.EmailSentAt = clock.GetUtcNow();
                notification.EmailPending = false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                notification.NextEmailAttemptAt = now.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(notification.EmailAttempts, 7))));
                logger.LogWarning("Reminder delivery failed ({FailureType}); attempt {Attempt}", ex.GetType().Name, notification.EmailAttempts);
            }
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
