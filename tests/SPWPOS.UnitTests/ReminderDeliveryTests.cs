using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Email;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed class ReminderDeliveryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task Call(ServiceHarness h, Func<IWorkspaceService, Task> action)
    {
        await using var scope = h.Provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());
    }
    private static async Task Deliver(ServiceHarness h, Guid uid)
    {
        await using var scope = h.Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ReminderNotificationService>().SendPendingAsync(uid, Ct);
    }

    [Fact]
    public async Task Email_only_reminders_are_persistent_retried_and_not_duplicated()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        var now = h.Clock.GetUtcNow();
        await Call(h, s => s.SetNotificationSettingsAsync(user.Id, new(InAppEnabled: false, EmailEnabled: true), Ct));
        await Call(h, async s => { await s.SaveTaskAsync(user.Id, null, new("Report", "", now.AddHours(2), 60), Ct); });
        await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct));
        var pending = await h.DbAsync(db => db.InAppNotifications.SingleAsync(x => x.Kind == "DEADLINE", Ct));
        Assert.False(pending.IsVisible); Assert.True(pending.EmailPending);
        await using (var scope = h.Provider.CreateAsyncScope())
            Assert.Empty((await scope.ServiceProvider.GetRequiredService<IWorkspaceService>().GetAsync(user.Id, Ct)).Notifications);
        h.Email.FailDelivery = true;
        await Deliver(h, user.Id);
        pending = await h.DbAsync(db => db.InAppNotifications.SingleAsync(x => x.Kind == "DEADLINE", Ct));
        Assert.Equal(1, pending.EmailAttempts); Assert.Null(pending.EmailSentAt); Assert.True(pending.NextEmailAttemptAt > now);
        h.Email.FailDelivery = false;
        await Deliver(h, user.Id); Assert.Empty(h.Email.Reminders);
        h.Clock.Advance(TimeSpan.FromMinutes(2));
        await Deliver(h, user.Id);
        await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct));
        await Deliver(h, user.Id);
        var sent = Assert.Single(h.Email.Reminders);
        Assert.Equal(user.Email, sent.Email); Assert.Contains("Report", sent.Message); Assert.Contains("Asia/Ho_Chi_Minh", sent.Message);
        pending = await h.DbAsync(db => db.InAppNotifications.SingleAsync(x => x.Kind == "DEADLINE", Ct));
        Assert.NotNull(pending.EmailSentAt); Assert.False(pending.EmailPending); Assert.Equal(2, pending.EmailAttempts);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("disable")]
    [InlineData("deadline")]
    [InlineData("delete")]
    public async Task Pending_deadline_mail_is_cancelled_when_it_is_no_longer_relevant(string change)
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        await Call(h, s => s.SetNotificationSettingsAsync(user.Id, new(EmailEnabled: true), Ct));
        await Call(h, async s => { await s.SaveTaskAsync(user.Id, null, new("Report", "", h.Clock.GetUtcNow().AddHours(2), 60), Ct); });
        await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct));
        await h.DbAsync(async db => {
            var task = await db.WorkTasks.SingleAsync(Ct);
            if (change == "complete") task.Status = WorkTaskStatus.Completed;
            if (change == "delete") task.IsDeleted = true;
            if (change == "deadline") task.Deadline = task.Deadline.AddDays(2);
            if (change == "disable") (await db.NotificationSettings.SingleAsync(Ct)).EmailEnabled = false;
            await db.SaveChangesAsync(Ct);
        });
        await Deliver(h, user.Id);
        Assert.Empty(h.Email.Reminders);
        Assert.False(await h.DbAsync(db => db.InAppNotifications.AnyAsync(x => x.EmailPending, Ct)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Session_reminder_is_sent_once_and_rescheduled_sessions_are_suppressed(bool rescheduled)
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        await Call(h, s => s.SetNotificationSettingsAsync(user.Id, new(EmailEnabled: true), Ct));
        var session = new WorkSession { UserId = user.Id, Start = now.AddMinutes(5), End = now.AddMinutes(35) };
        await h.DbAsync(async db => {
            var task = new WorkTask { UserId = user.Id, Name = "Focus", Deadline = now.AddDays(3) };
            session.TaskId = task.Id; db.WorkTasks.Add(task); db.WorkSessions.Add(session); await db.SaveChangesAsync(Ct);
        });
        await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct));
        if (rescheduled) await h.DbAsync(async db => {
            var saved = await db.WorkSessions.SingleAsync(Ct); saved.Start = saved.Start.AddHours(1); saved.End = saved.End.AddHours(1); await db.SaveChangesAsync(Ct);
        });
        await Deliver(h, user.Id); await Deliver(h, user.Id);
        if (rescheduled) Assert.Empty(h.Email.Reminders);
        else { Assert.Single(h.Email.Reminders); Assert.Contains("Focus", h.Email.Reminders[0].Message); }
    }

    [Fact]
    public async Task Email_is_opt_in_and_admin_omits_personal_work_counts()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); await h.LoginAsync();
        await Call(h, async s => { await s.SaveTaskAsync(user.Id, null, new("Report", "", h.Clock.GetUtcNow().AddHours(2), 60), Ct); });
        await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct)); await Deliver(h, user.Id);
        Assert.Empty(h.Email.Reminders);
        await h.DbAsync(async db => {
            db.WorkTasks.AddRange(new WorkTask { UserId = user.Id, Name = "Done", Status = WorkTaskStatus.Completed },
                new WorkTask { UserId = user.Id, Name = "Deleted", IsDeleted = true });
            db.RefreshTokens.Add(new() { UserId = user.Id, TokenHash = "expired-test-token", ExpiresAt = h.Clock.GetUtcNow().AddDays(-1) });
            await db.SaveChangesAsync(Ct);
        });
        await using var scope = h.Provider.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        var account = Assert.Single(await service.SearchUsersAsync(ServiceHarness.UserName, Ct));
        Assert.Equal(user.Id, account.Id); Assert.NotNull(account.LastLoginAt); Assert.Equal(0, account.TotalTasks); Assert.Equal(0, account.CompletedTasks); Assert.Equal(1, account.ActiveLoginSessions);
        var json = System.Text.Json.JsonSerializer.Serialize(account, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.DoesNotContain("totalTasks", json); Assert.DoesNotContain("completedTasks", json);
    }

    [Fact]
    public async Task Enabling_email_can_deliver_a_still_current_in_app_deadline_reminder()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        await Call(h, async s => { await s.SaveTaskAsync(user.Id, null, new("Report", "", h.Clock.GetUtcNow().AddHours(2), 60), Ct); });
        await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct));
        await Call(h, s => s.SetNotificationSettingsAsync(user.Id, new(EmailEnabled: true), Ct));
        await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct)); await Deliver(h, user.Id);
        Assert.Single(h.Email.Reminders);
        Assert.Equal(1, await h.DbAsync(db => db.InAppNotifications.CountAsync(x => x.Kind == "DEADLINE", Ct)));
    }
}
