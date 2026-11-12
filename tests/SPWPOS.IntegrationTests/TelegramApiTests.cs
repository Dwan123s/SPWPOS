using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.Infrastructure.Telegram;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;
namespace SPWPOS.IntegrationTests;

public sealed class TelegramApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class InvalidTelegramFactory(string token, string poll) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Telegram:Enabled"] = "true", ["Telegram:BotToken"] = token, ["Telegram:PollSeconds"] = poll,
            }));
        }
    }
    [Theory]
    [InlineData("", "3", "MISSING_BOT_TOKEN")]
    [InlineData("incorrect-token", "3", "INVALID_BOT_TOKEN")]
    [InlineData("123456:abcdefghijklmnopqrstuvwxyz0123456789", "invalid", "INVALID_POLL_INTERVAL")]
    [InlineData("123456:abcdefghijklmnopqrstuvwxyz0123456789", "1", "INVALID_POLL_INTERVAL")]
    public async Task Invalid_optional_telegram_configuration_keeps_app_and_login_available(string token, string poll, string error)
    {
        await using var f = new InvalidTelegramFactory(token, poll); using var c = await f.ClientAsync();
        await Send(c, HttpMethod.Get, "/api/integrations/telegram/status", expected: HttpStatusCode.Unauthorized);
        await Authorize(f, c);
        var status = (await Send(c, HttpMethod.Get, "/api/integrations/telegram/status")).GetProperty("data");
        Assert.False(status.GetProperty("configured").GetBoolean());
        Assert.Equal(error, status.GetProperty("configurationError").GetString());
        Assert.DoesNotContain("abcdefghijklmnopqrstuvwxyz", status.ToString());
        await Send(c, HttpMethod.Get, "/api/workspace");
        await Send(c, HttpMethod.Post, "/api/integrations/telegram/connect", expected: HttpStatusCode.ServiceUnavailable);
    }
    [Fact]
    public async Task Telegram_endpoints_require_login_and_do_not_expose_bot_credentials()
    {
        await using var f = new ApiFactory(); using var c = await f.ClientAsync();
        foreach (var (method, path) in new[] { (HttpMethod.Get, "/status"), (HttpMethod.Post, "/connect"), (HttpMethod.Delete, "") })
            await Send(c, method, "/api/integrations/telegram" + path, expected: HttpStatusCode.Unauthorized);
        await Authorize(f, c);
        var status = (await Send(c, HttpMethod.Get, "/api/integrations/telegram/status")).GetProperty("data");
        Assert.False(status.GetProperty("configured").GetBoolean()); Assert.False(status.GetProperty("connected").GetBoolean());
        Assert.DoesNotContain("botToken", status.ToString()); Assert.DoesNotContain("chatId", status.ToString());
        await Send(c, HttpMethod.Post, "/api/integrations/telegram/connect", expected: HttpStatusCode.ServiceUnavailable);
        await Send(c, HttpMethod.Delete, "/api/integrations/telegram");
    }

    public static bool SqlServerAvailable => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SPWPOS_TEST_SQLSERVER"));
    private sealed class Sender : ITelegramGateway
    {
        private int sent;
        public int Sent => sent;
        public Task<string> BotUsernameAsync(CancellationToken ct) => Task.FromResult("test_bot");
        public Task<TelegramUpdate[]> UpdatesAsync(long offset, CancellationToken ct) => Task.FromResult(Array.Empty<TelegramUpdate>());
        public Task SendAsync(long chatId, string text, CancellationToken ct) { Interlocked.Increment(ref sent); return Task.CompletedTask; }
    }
    private sealed class StartNoticeFactory : ApiFactory
    {
        public Sender Gateway { get; } = new();
        public StartNoticeFactory() : base(true) { }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => {
                services.Configure<TelegramOptions>(o => { o.Enabled = true; o.BotToken = "123456:abcdefghijklmnopqrstuvwxyz0123456789"; });
                services.AddSingleton<ITelegramGateway>(Gateway);
            });
        }
    }
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires SPWPOS_TEST_SQLSERVER."), Trait("Category", "SqlServer")]
    public async Task Background_scan_sends_at_start_without_any_browser_refresh_and_keeps_timer_stopped()
    {
        await using var f = new StartNoticeFactory(); using var c = await f.ClientAsync(); await Authorize(f, c);
        Guid sessionId;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uid = await db.Users.Select(x => x.Id).SingleAsync(Ct); var now = f.Clock.GetUtcNow();
            var task = new WorkTask { UserId = uid, Name = "Fixture work", Deadline = now.AddDays(3) }; db.WorkTasks.Add(task);
            var session = new WorkSession { UserId = uid, TaskId = task.Id, Start = now, End = now.AddHours(1) };
            sessionId = session.Id; db.WorkSessions.Add(session);
            db.FixedEvents.Add(new() { UserId = uid, Name = "Fixture timetable", Start = now, End = now.AddHours(1) });
            db.TelegramConnections.Add(new() { UserId = uid, Enabled = true, ChatId = 123, ConnectedAt = now });
            await db.SaveChangesAsync(Ct);
        }
        var deadline = DateTimeOffset.UtcNow.AddSeconds(25);
        while (f.Gateway.Sent < 2 && DateTimeOffset.UtcNow < deadline) await Task.Delay(250, Ct);
        Assert.Equal(2, f.Gateway.Sent);
        await using var verify = f.Services.CreateAsyncScope(); var final = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await final.InAppNotifications.Where(x => x.Kind == "EVENT_STARTED" || x.Kind == "SESSION_STARTED").ToArrayAsync(Ct);
        Assert.Equal(2, rows.Length); Assert.All(rows, n => Assert.NotNull(n.TelegramSentAt));
        Assert.Equal(WorkSessionStatus.Scheduled, (await final.WorkSessions.SingleAsync(x => x.Id == sessionId, Ct)).Status);
    }
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires SPWPOS_TEST_SQLSERVER."), Trait("Category", "SqlServer")]
    public async Task Additive_upgrade_preserves_existing_data_and_concurrent_delivery_sends_once()
    {
        await using var f = new ApiFactory(true); using var c = await f.ClientAsync(); await Authorize(f, c);
        Guid uid; var taskId = await Create(c, f.Clock.GetUtcNow());
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            uid = await db.Users.Select(x => x.Id).SingleAsync(Ct);
            var migrations = (await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray();
            var index = Array.FindIndex(migrations, x => x.EndsWith("_TelegramNotifications", StringComparison.Ordinal)); Assert.True(index > 0);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(migrations[index - 1], Ct);
            db.PlanningNotes.Add(new() { UserId = uid, Title = "Keep Notes", Content = "Existing private data" }); await db.SaveChangesAsync(Ct);
            await migrator.MigrateAsync(null, Ct);
            Assert.True(await db.WorkTasks.AnyAsync(x => x.Id == taskId, Ct)); Assert.Equal("Keep Notes", (await db.PlanningNotes.SingleAsync(Ct)).Title);
            var connection = new TelegramConnection { UserId = uid, Enabled = true, ChatId = 123, ConnectedAt = f.Clock.GetUtcNow() };
            db.TelegramConnections.Add(connection);
            var task = await db.WorkTasks.SingleAsync(x => x.Id == taskId, Ct);
            db.InAppNotifications.Add(new() { UserId = uid, Kind = "DEADLINE", TaskId = taskId, DedupeKey = $"deadline:{task.Id}:{task.Deadline!.Value.UtcTicks}",
                Title = "Deadline", Message = "Private fixture", CreatedAt = f.Clock.GetUtcNow(), TelegramPending = true,
                TelegramBindingId = connection.BindingId, NextTelegramAttemptAt = f.Clock.GetUtcNow() });
            await db.SaveChangesAsync(Ct);
        }
        var sender = new Sender(); var options = Options.Create(new TelegramOptions { Enabled = true, BotToken = "123456:abcdefghijklmnopqrstuvwxyz0123456789" });
        async Task Deliver()
        {
            await using var scope = f.Services.CreateAsyncScope();
            await new TelegramNotificationService(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), sender, options, f.Clock).SendPendingAsync(uid, Ct);
        }
        await Task.WhenAll(Deliver(), Deliver()); Assert.Equal(1, sender.Sent);
    }
}
