using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SPWPOS.Infrastructure.Calendar;
using SPWPOS.Infrastructure.Email;
using SPWPOS.Infrastructure.Telegram;
using SPWPOS.Infrastructure.Workspace;

namespace SPWPOS.UnitTests;

public sealed class BackgroundWorkerShutdownTests
{
    [Theory]
    [InlineData("notifications")]
    [InlineData("password")]
    [InlineData("telegram")]
    [InlineData("calendar")]
    public async Task Disposal_during_startup_completes_timer_wait_without_cancellation_failure(string name)
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();
        using BackgroundService worker = name switch
        {
            "notifications" => new WorkspaceNotificationWorker(scopes, new NotificationJobStatus(), NullLogger<WorkspaceNotificationWorker>.Instance, TimeProvider.System),
            "password" => new PasswordNotificationWorker(scopes, NullLogger<PasswordNotificationWorker>.Instance),
            "telegram" => new TelegramUpdateWorker(scopes, Options.Create(new TelegramOptions
                { Enabled = true, BotToken = "123456:offline_fixture_token_1234567890" }), NullLogger<TelegramUpdateWorker>.Instance),
            // The missing sync service is caught as a retryable error, then the worker waits.
            "calendar" => new GoogleCalendarWorker(scopes, Options.Create(new GoogleCalendarOptions
                { Enabled = true, ClientId = "offline", ClientSecret = "offline" }), TimeProvider.System, NullLogger<GoogleCalendarWorker>.Instance),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        await worker.StartAsync(TestContext.Current.CancellationToken);
        var execution = Assert.IsAssignableFrom<Task>(worker.ExecuteTask);
        Assert.False(execution.IsCompleted);
        // Host disposal after a bind failure cancels workers without calling StopAsync first.
        worker.Dispose();
        await execution.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.True(execution.IsCompletedSuccessfully);
    }
}
