using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SPWPOS.Infrastructure.Persistence;

namespace SPWPOS.Infrastructure.Calendar;

public sealed class GoogleCalendarWorker(IServiceScopeFactory scopes, IOptions<GoogleCalendarOptions> options,
    TimeProvider clock, ILogger<GoogleCalendarWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!options.Value.Configured) return;
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            do
            {
                try
                {
                    for (var n = 0; n < 10 && !stoppingToken.IsCancellationRequested; n++)
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        timeout.CancelAfter(TimeSpan.FromMinutes(3));
                        if (!await scope.ServiceProvider.GetRequiredService<GoogleCalendarService>().SyncNextAsync(timeout.Token)) break;
                    }
                    await using var cleanup = scopes.CreateAsyncScope();
                    var db = cleanup.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var now = clock.GetUtcNow();
                    await db.CalendarOAuthRequests.Where(x => x.ExpiresAt < now).ExecuteDeleteAsync(stoppingToken);
                    await db.CalendarSyncOutbox.Where(x => x.CreatedAt < now.AddDays(-1) &&
                        !db.GoogleCalendarConnections.Any(c => c.UserId == x.UserId && c.Status != "Disconnected")).ExecuteDeleteAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error)
                {
                    // Never log exception bodies/URLs: OAuth/provider responses may
                    // contain credentials or calendar contents.
                    logger.LogWarning("Google Calendar worker will retry ({FailureType}).", error.GetType().Name);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Cancellation during timer waits or retry delays is a normal shutdown.
        }
    }
}
