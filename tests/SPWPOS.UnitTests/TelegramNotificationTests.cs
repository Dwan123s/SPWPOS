using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Telegram;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class TelegramNotificationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Gateway : ITelegramGateway
    {
        public List<(long Chat, string Text)> Sent { get; } = [];
        public TelegramDeliveryException? Failure { get; set; }
        public Task<string> BotUsernameAsync(CancellationToken ct) => Task.FromResult("planora_test_bot");
        public Task<TelegramUpdate[]> UpdatesAsync(long offset, CancellationToken ct) => Task.FromResult(Array.Empty<TelegramUpdate>());
        public Task SendAsync(long chatId, string text, CancellationToken ct)
        { if (Failure is not null) throw Failure; Sent.Add((chatId, text)); return Task.CompletedTask; }
    }
    private static Task<ServiceHarness> Harness(Gateway gateway, bool enabled = true) => ServiceHarness.CreateAsync(s =>
    {
        s.Configure<TelegramOptions>(o => { o.Enabled = enabled; o.BotToken = "123456:abcdefghijklmnopqrstuvwxyz0123456789"; });
        s.AddSingleton<ITelegramGateway>(gateway);
    });
    private static async Task<T> Call<T>(ServiceHarness h, Func<TelegramNotificationService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<TelegramNotificationService>()); }
    private static Task Call(ServiceHarness h, Func<TelegramNotificationService, Task> action) => Call(h, async s => { await action(s); return true; });
    private static async Task Work(ServiceHarness h, Func<IWorkspaceService, Task> action)
    { await using var scope = h.Provider.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    private static string Token(TelegramLink link) => link.Url.Split("start=")[1];
    private static TelegramUpdate Start(TelegramLink link, long chat = 123) => new(1, chat, chat, "private", "/start " + Token(link), "Người dùng Telegram");
    private static async Task Link(ServiceHarness h, Guid uid, long chat = 123)
    { var link = await Call(h, s => s.ConnectAsync(uid, Ct)); await Call(h, s => s.ProcessUpdateAsync(Start(link, chat), Ct)); }
    private static async Task<Guid> TaskDue(ServiceHarness h, Guid uid)
    {
        var task = new WorkTask { UserId = uid, Name = "Báo cáo riêng", Deadline = h.Clock.GetUtcNow().AddHours(2) };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); await db.SaveChangesAsync(Ct); }); return task.Id;
    }

    [Fact]
    public async Task Linking_is_private_expiring_single_use_and_status_contains_no_credentials()
    {
        await using var h = await Harness(new()); var u = await h.RegisterAsync();
        var link = await Call(h, s => s.ConnectAsync(u.Id, Ct));
        var stored = await h.DbAsync(db => db.TelegramConnections.SingleAsync(Ct));
        Assert.DoesNotContain(Token(link), stored.LinkTokenHash!); Assert.Null(stored.ChatId); Assert.False(stored.Enabled);
        await Call(h, s => s.ProcessUpdateAsync(Start(link) with { ChatType = "group" }, Ct));
        await Call(h, s => s.ProcessUpdateAsync(Start(link) with { SenderId = 999 }, Ct));
        Assert.False((await Call(h, s => s.StatusAsync(u.Id, Ct))).Connected);
        await Call(h, s => s.ProcessUpdateAsync(Start(link), Ct));
        var status = await Call(h, s => s.StatusAsync(u.Id, Ct));
        Assert.True(status.Connected); Assert.True(status.Enabled); Assert.Null(status.LinkExpiresAt);
        await Call(h, s => s.ProcessUpdateAsync(Start(link, 999), Ct));
        Assert.Equal(123, (await h.DbAsync(db => db.TelegramConnections.SingleAsync(Ct))).ChatId);
        var json = JsonSerializer.Serialize(status);
        Assert.DoesNotContain("Token", json); Assert.DoesNotContain("ChatId", json);
        await Call(h, s => s.DisconnectAsync(u.Id, Ct));
        await Call(h, s => s.ProcessUpdateAsync(Start(link), Ct));
        Assert.False((await Call(h, s => s.StatusAsync(u.Id, Ct))).Connected);
        link = await Call(h, s => s.ConnectAsync(u.Id, Ct)); h.Clock.Advance(TimeSpan.FromMinutes(11));
        await Call(h, s => s.ProcessUpdateAsync(Start(link), Ct));
        Assert.False((await Call(h, s => s.StatusAsync(u.Id, Ct))).Connected);
    }

    [Fact]
    public async Task Rotating_link_invalidates_previous_link_and_one_chat_cannot_link_two_accounts()
    {
        await using var h = await Harness(new()); var u = await h.RegisterAsync();
        var old = await Call(h, s => s.ConnectAsync(u.Id, Ct)); var current = await Call(h, s => s.ConnectAsync(u.Id, Ct));
        await Call(h, s => s.ProcessUpdateAsync(Start(old), Ct));
        Assert.False((await Call(h, s => s.StatusAsync(u.Id, Ct))).Connected);
        await Call(h, s => s.ProcessUpdateAsync(Start(current), Ct));
        var other = new ApplicationUser { Id = Guid.NewGuid(), UserName = "other", Email = "other@test.local", NormalizedEmail = "OTHER@TEST.LOCAL", EmailConfirmed = true, IsActive = true };
        await h.DbAsync(async db => { db.Users.Add(other); await db.SaveChangesAsync(Ct); });
        var otherLink = await Call(h, s => s.ConnectAsync(other.Id, Ct));
        await Call(h, s => s.ProcessUpdateAsync(Start(otherLink), Ct));
        Assert.False((await Call(h, s => s.StatusAsync(other.Id, Ct))).Connected);
        await Call(h, s => s.ProcessUpdateAsync(Start(otherLink, 456), Ct));
        Assert.True((await Call(h, s => s.StatusAsync(other.Id, Ct))).Connected);
    }

    [Fact]
    public async Task Telegram_only_deadline_is_durable_retried_and_not_sent_twice()
    {
        var gateway = new Gateway(); await using var h = await Harness(gateway); var u = await h.RegisterAsync();
        await Link(h, u.Id); await Work(h, s => s.SetNotificationSettingsAsync(u.Id, new(InAppEnabled: false), Ct));
        await TaskDue(h, u.Id); await Work(h, s => s.EnsureNotificationsAsync(u.Id, Ct));
        gateway.Failure = new(429, 120);
        await Call(h, s => s.SendPendingAsync(u.Id, Ct));
        var n = await h.DbAsync(db => db.InAppNotifications.SingleAsync(x => x.Kind == "DEADLINE", Ct));
        Assert.True(n.TelegramPending); Assert.Null(n.TelegramSentAt); Assert.Equal(1, n.TelegramAttempts); Assert.False(n.IsVisible);
        gateway.Failure = null; await Call(h, s => s.SendPendingAsync(u.Id, Ct)); Assert.Empty(gateway.Sent);
        h.Clock.Advance(TimeSpan.FromMinutes(3)); await Call(h, s => s.SendPendingAsync(u.Id, Ct));
        await Work(h, s => s.EnsureNotificationsAsync(u.Id, Ct)); await Call(h, s => s.SendPendingAsync(u.Id, Ct));
        var deadline = Assert.Single(gateway.Sent, x => x.Text.Contains("Công việc sắp đến hạn"));
        Assert.Equal(123, deadline.Chat); Assert.Contains("Báo cáo riêng", deadline.Text);
        Assert.Empty(h.Email.Reminders);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("reschedule")]
    [InlineData("delete")]
    [InlineData("disable")]
    [InlineData("disconnect")]
    [InlineData("relink")]
    [InlineData("lock")]
    public async Task Changed_work_or_connection_cancels_pending_delivery(string change)
    {
        var g = new Gateway(); await using var h = await Harness(g); var u = await h.RegisterAsync(); await Link(h, u.Id);
        var taskId = await TaskDue(h, u.Id); await Work(h, s => s.EnsureNotificationsAsync(u.Id, Ct));
        if (change == "disable") await Call(h, s => s.SetEnabledAsync(u.Id, false, Ct));
        else if (change == "disconnect") await Call(h, s => s.DisconnectAsync(u.Id, Ct));
        else if (change == "relink") await Link(h, u.Id, 456);
        else await h.DbAsync(async db => {
            var t = await db.WorkTasks.SingleAsync(x => x.Id == taskId, Ct);
            if (change == "complete") t.Status = WorkTaskStatus.Completed;
            if (change == "reschedule") t.Deadline = t.Deadline.AddDays(1);
            if (change == "delete") t.IsDeleted = true;
            if (change == "lock") (await db.Users.SingleAsync(Ct)).IsActive = false;
            await db.SaveChangesAsync(Ct);
        });
        await Call(h, s => s.SendPendingAsync(u.Id, Ct)); Assert.DoesNotContain(g.Sent, x => x.Text.Contains("Báo cáo riêng"));
        Assert.False(await h.DbAsync(db => db.InAppNotifications.AnyAsync(x => x.TelegramPending, Ct)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Timetable_events_are_sent_only_while_their_original_time_is_current(bool move)
    {
        var g = new Gateway(); await using var h = await Harness(g); var u = await h.RegisterAsync(); await Link(h, u.Id);
        await h.DbAsync(async db => { db.FixedEvents.Add(new() { UserId = u.Id, Name = "Lớp học", Start = h.Clock.GetUtcNow().AddMinutes(5), End = h.Clock.GetUtcNow().AddHours(1) }); await db.SaveChangesAsync(Ct); });
        await Work(h, s => s.EnsureNotificationsAsync(u.Id, Ct));
        if (move) await h.DbAsync(async db => { var e = await db.FixedEvents.SingleAsync(Ct); e.Start = e.Start.AddHours(1); e.End = e.End.AddHours(1); await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.SendPendingAsync(u.Id, Ct));
        if (move) Assert.Empty(g.Sent); else Assert.Contains(g.Sent, x => x.Text.Contains("Lớp học"));
    }

    [Fact]
    public async Task Unlinked_accounts_never_send_and_blocked_bot_requires_start_again()
    {
        var g = new Gateway(); await using var h = await Harness(g); var u = await h.RegisterAsync();
        await TaskDue(h, u.Id); await Work(h, s => s.EnsureNotificationsAsync(u.Id, Ct));
        await Call(h, s => s.SendPendingAsync(u.Id, Ct)); Assert.Empty(g.Sent);
        h.Clock.Advance(TimeSpan.FromSeconds(1)); await Link(h, u.Id);
        await TaskDue(h, u.Id); await Work(h, s => s.EnsureNotificationsAsync(u.Id, Ct));
        g.Failure = new(403); await Call(h, s => s.SendPendingAsync(u.Id, Ct));
        var status = await Call(h, s => s.StatusAsync(u.Id, Ct)); Assert.False(status.Enabled); Assert.Equal("BOT_BLOCKED", status.ErrorCode);
        await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.SetEnabledAsync(u.Id, true, Ct)));
        g.Failure = null; await Link(h, u.Id);
        Assert.True((await Call(h, s => s.StatusAsync(u.Id, Ct))).Enabled);
        await h.DbAsync(async db => { (await db.Users.SingleAsync(Ct)).IsActive = false; await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.ProcessUpdateAsync(new(4, 123, 123, "private", "/stop", "User"), Ct));
        Assert.False((await Call(h, s => s.StatusAsync(u.Id, Ct))).Connected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Session_reminders_are_not_sent_after_rescheduling(bool move)
    {
        var g = new Gateway(); await using var h = await Harness(g); var u = await h.RegisterAsync(); await Link(h, u.Id);
        var taskId = await TaskDue(h, u.Id);
        await h.DbAsync(async db => { db.WorkSessions.Add(new() { UserId = u.Id, TaskId = taskId,
            Start = h.Clock.GetUtcNow().AddMinutes(5), End = h.Clock.GetUtcNow().AddMinutes(35) }); await db.SaveChangesAsync(Ct); });
        await Work(h, s => s.EnsureNotificationsAsync(u.Id, Ct));
        if (move) await h.DbAsync(async db => { var s = await db.WorkSessions.SingleAsync(Ct); s.Start = s.Start.AddHours(1); s.End = s.End.AddHours(1); await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.SendPendingAsync(u.Id, Ct));
        Assert.Equal(move ? 0 : 1, g.Sent.Count(x => x.Text.Contains("Sắp đến phiên làm việc")));
    }

    [Fact]
    public async Task Delivering_one_account_cannot_read_or_send_another_accounts_notifications()
    {
        var g = new Gateway(); await using var h = await Harness(g); var u = await h.RegisterAsync(); await Link(h, u.Id);
        var other = new ApplicationUser { Id = Guid.NewGuid(), UserName = "other", Email = "other@test.local", NormalizedEmail = "OTHER@TEST.LOCAL", EmailConfirmed = true, IsActive = true };
        await h.DbAsync(async db => { db.Users.Add(other); await db.SaveChangesAsync(Ct); }); await Link(h, other.Id, 456);
        await TaskDue(h, u.Id);
        await h.DbAsync(async db => { db.WorkTasks.Add(new() { UserId = other.Id, Name = "OTHER_PRIVATE_TASK", Deadline = h.Clock.GetUtcNow().AddHours(1) }); await db.SaveChangesAsync(Ct); });
        await Work(h, s => s.EnsureNotificationsAsync(u.Id, Ct)); await Work(h, s => s.EnsureNotificationsAsync(other.Id, Ct));
        await Call(h, s => s.SendPendingAsync(u.Id, Ct));
        Assert.NotEmpty(g.Sent); Assert.All(g.Sent, x => { Assert.Equal(123, x.Chat); Assert.DoesNotContain("OTHER_PRIVATE_TASK", x.Text); });
        Assert.True(await h.DbAsync(db => db.InAppNotifications.AnyAsync(x => x.UserId == other.Id && x.TelegramPending, Ct)));
    }

    [Fact]
    public async Task Disabled_server_configuration_blocks_connect_without_secret_in_response()
    {
        await using var h = await Harness(new(), false); var u = await h.RegisterAsync();
        Assert.False((await Call(h, s => s.StatusAsync(u.Id, Ct))).Configured);
        var e = await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.ConnectAsync(u.Id, Ct)));
        Assert.DoesNotContain("123456", e.Message);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Telegram_sends_separate_advance_and_at_start_notices_once_each(bool session)
    {
        var g = new Gateway(); await using var h = await Harness(g); var u = await h.RegisterAsync(); await Link(h, u.Id);
        var now = h.Clock.GetUtcNow();
        await Work(h, s => s.SetNotificationSettingsAsync(u.Id, new(InAppEnabled: false), Ct));
        await h.DbAsync(async db => {
            if (session) {
                var task = new WorkTask { UserId = u.Id, Name = "Ôn bài", Deadline = now.AddDays(3) }; db.WorkTasks.Add(task);
                db.WorkSessions.Add(new() { UserId = u.Id, TaskId = task.Id, Start = now.AddMinutes(5), End = now.AddMinutes(35) });
            } else db.FixedEvents.Add(new() { UserId = u.Id, Name = "Đi làm", Start = now.AddMinutes(5), End = now.AddHours(1) });
            await db.SaveChangesAsync(Ct);
        });
        await Work(h, s => s.EnsureNotificationsAsync(u.Id, Ct)); await Call(h, s => s.SendPendingAsync(u.Id, Ct));
        Assert.Single(g.Sent, x => x.Text.Contains(session ? "Sắp đến phiên làm việc" : "Sắp đến lịch hẹn"));
        Assert.DoesNotContain(g.Sent, x => x.Text.Contains("đang diễn ra"));
        h.Clock.Advance(TimeSpan.FromMinutes(5));
        await Work(h, s => s.EnsureStartedNotificationsAsync(u.Id, Ct)); await Call(h, s => s.SendPendingAsync(u.Id, Ct));
        await Work(h, s => s.EnsureNotificationsAsync(u.Id, Ct)); await Call(h, s => s.SendPendingAsync(u.Id, Ct));
        h.Clock.Advance(TimeSpan.FromSeconds(10));
        await Work(h, s => s.EnsureStartedNotificationsAsync(u.Id, Ct)); await Call(h, s => s.SendPendingAsync(u.Id, Ct));
        var started = Assert.Single(g.Sent, x => x.Text.Contains("đang diễn ra"));
        Assert.StartsWith("🟢 <b>", started.Text); Assert.Contains("🟢 Đang diễn ra", started.Text); Assert.DoesNotContain("Bắt đầu sau", started.Text);
        var kinds = session ? new[] { "SESSION_REMINDER", "SESSION_STARTED" } : new[] { "EVENT_REMINDER", "EVENT_STARTED" };
        var delivered = await h.DbAsync(db => db.InAppNotifications.Where(x => kinds.Contains(x.Kind)).ToArrayAsync(Ct));
        Assert.Equal(2, delivered.Length); Assert.All(delivered, n => { Assert.NotNull(n.TelegramSentAt); Assert.False(n.IsVisible); });
    }

    [Theory]
    [InlineData(false, "reschedule")] [InlineData(false, "delete")] [InlineData(false, "expired")]
    [InlineData(true, "reschedule")] [InlineData(true, "cancel")] [InlineData(true, "complete")] [InlineData(true, "expired")]
    public async Task At_start_delivery_rechecks_the_current_schedule_and_task_before_sending(bool session, string change)
    {
        var g = new Gateway(); await using var h = await Harness(g); var u = await h.RegisterAsync(); await Link(h, u.Id);
        var now = h.Clock.GetUtcNow();
        await h.DbAsync(async db => {
            if (session) {
                var task = new WorkTask { UserId = u.Id, Name = "Ôn bài", Deadline = now.AddDays(3) }; db.WorkTasks.Add(task);
                db.WorkSessions.Add(new() { UserId = u.Id, TaskId = task.Id, Start = now, End = now.AddMinutes(30) });
            } else db.FixedEvents.Add(new() { UserId = u.Id, Name = "Đi làm", Start = now, End = now.AddHours(1) });
            await db.SaveChangesAsync(Ct);
        });
        await Work(h, s => s.EnsureStartedNotificationsAsync(u.Id, Ct));
        await h.DbAsync(async db => {
            if (session) {
                var s = await db.WorkSessions.SingleAsync(Ct);
                if (change == "reschedule") { s.Start = s.Start.AddHours(1); s.End = s.End.AddHours(1); }
                if (change == "cancel") s.Status = WorkSessionStatus.Cancelled;
                if (change == "complete") (await db.WorkTasks.SingleAsync(Ct)).Status = WorkTaskStatus.Completed;
            } else {
                var e = await db.FixedEvents.SingleAsync(Ct);
                if (change == "reschedule") { e.Start = e.Start.AddHours(1); e.End = e.End.AddHours(1); }
                if (change == "delete") db.FixedEvents.Remove(e);
            }
            await db.SaveChangesAsync(Ct);
        });
        if (change == "expired") h.Clock.Advance(TimeSpan.FromHours(2));
        await Call(h, s => s.SendPendingAsync(u.Id, Ct)); Assert.Empty(g.Sent);
        Assert.False(await h.DbAsync(db => db.InAppNotifications.AnyAsync(x => x.TelegramPending, Ct)));
    }

    private sealed class HttpStub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request));
    }
    [Fact]
    public async Task Gateway_sends_formatted_html_and_sanitizes_credential_bearing_transport_errors()
    {
        var options = Options.Create(new TelegramOptions { Enabled = true, BotToken = "123:very_secret_token" });
        string? payload = null;
        using var http = new HttpClient(new HttpStub(r => {
            payload = r.Content!.ReadAsStringAsync(Ct).GetAwaiter().GetResult();
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true,\"result\":{\"message_id\":1}}", Encoding.UTF8, "application/json") };
        }));
        var formatted = TelegramMessageFormatter.Format(new() { Title = "Reminder", Message = "<b>user supplied</b>" }, new(), "UTC", DateTimeOffset.UtcNow);
        await new TelegramGateway(http, options).SendAsync(123, formatted, Ct);
        using var request = JsonDocument.Parse(payload!);
        Assert.Equal("HTML", request.RootElement.GetProperty("parse_mode").GetString());
        Assert.Equal(formatted, request.RootElement.GetProperty("text").GetString());
        Assert.Contains("&lt;b&gt;user supplied&lt;/b&gt;", formatted);
        Assert.Equal(123, request.RootElement.GetProperty("chat_id").GetInt64());
        using var broken = new HttpClient(new HttpStub(_ => throw new HttpRequestException("https://api.telegram.org/bot123:very_secret_token/sendMessage")));
        var ex = await Assert.ThrowsAsync<TelegramDeliveryException>(() => new TelegramGateway(broken, options).SendAsync(123, "x", Ct));
        Assert.DoesNotContain("very_secret_token", ex.ToString()); Assert.Null(ex.InnerException);
    }
}
