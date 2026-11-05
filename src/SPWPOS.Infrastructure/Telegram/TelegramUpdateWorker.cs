using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Telegram;

public sealed class TelegramUpdateWorker(IServiceScopeFactory scopes, IOptions<TelegramOptions> options,
    ILogger<TelegramUpdateWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            if (!options.Value.Configured)
            {
                if (options.Value.Enabled) logger.LogWarning("Telegram integration is unavailable ({ConfigurationError}); the application remains available.", options.Value.ConfigurationError);
                return;
            }
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.BotToken)));
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollSeconds));
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    // Serialize polling across app instances; commit cursor only after every update is handled.
                    await using var tx = await AccountTransaction.BeginAsync(db, "PLANORA:TELEGRAM:UPDATES", ct);
                    var cursor = await db.TelegramUpdateCursors.SingleOrDefaultAsync(x => x.Id == key, ct);
                    if (cursor is null) { cursor = new() { Id = key }; db.TelegramUpdateCursors.Add(cursor); }
                    var updates = await scope.ServiceProvider.GetRequiredService<ITelegramGateway>().UpdatesAsync(cursor.NextOffset, ct);
                    foreach (var update in updates.OrderBy(x => x.Id))
                    {
                        using var updateScope = scopes.CreateScope();
                        await updateScope.ServiceProvider.GetRequiredService<TelegramNotificationService>().ProcessUpdateAsync(update, ct);
                        cursor.NextOffset = Math.Max(cursor.NextOffset, update.Id + 1);
                    }
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (TelegramDeliveryException ex)
                {
                    logger.LogWarning("Telegram update polling failed (code {Code}).", ex.Code);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(ex.RetryAfterSeconds, 30, 60)), ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Telegram update polling failed ({FailureType}).", ex.GetType().Name);
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cancellation during timer waits or retry delays is a normal shutdown.
        }
    }
}
