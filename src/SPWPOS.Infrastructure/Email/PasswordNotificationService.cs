using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Email;

public sealed class PasswordNotificationService(ApplicationDbContext db, IEmailService email, TimeProvider clock, ILogger<PasswordNotificationService> logger)
{
    public async Task SendPendingAsync(Guid userId, CancellationToken ct)
    {
        var address = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.NormalizedEmail).SingleOrDefaultAsync(ct);
        if (address is null) return;
        await using var tx = await AccountTransaction.BeginAsync(db, address, ct);
        var recipient = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.Email).SingleAsync(ct);
        var now = clock.GetUtcNow();
        var pending = await db.PasswordChangeNotifications.Where(x => x.UserId == userId && x.SentAt == null && x.NextAttemptAt <= now).ToListAsync(ct);
        foreach (var notification in pending)
        {
            notification.Attempts++;
            try
            {
                await email.SendPasswordChangedAsync(recipient!, ct);
                notification.SentAt = clock.GetUtcNow();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                notification.NextAttemptAt = now.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(notification.Attempts, 7))));
                logger.LogWarning("Security notification delivery failed ({FailureType}); attempt {Attempt}", ex.GetType().Name, notification.Attempts);
            }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
public sealed class PasswordNotificationWorker(IServiceScopeFactory scopes, ILogger<PasswordNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
                    var ids = await db.PasswordChangeNotifications.AsNoTracking().Where(x => x.SentAt == null && x.NextAttemptAt <= now)
                        .Select(x => x.UserId).Distinct().OrderBy(x => x).Take(20).ToListAsync(stoppingToken);
                    foreach (var id in ids)
                    {
                        // Fresh context prevents stale notification tracking across account locks.
                        await using var deliveryScope = scopes.CreateAsyncScope();
                        await deliveryScope.ServiceProvider.GetRequiredService<PasswordNotificationService>().SendPendingAsync(id, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogWarning("Security notification worker failed ({FailureType})", ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Cancellation during timer waits or retry delays is a normal shutdown.
        }
    }
}
