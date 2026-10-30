using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class WorkspaceReminderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task Call(ServiceHarness h, Func<IWorkspaceService, Task> action)
    { await using var scope = h.Provider.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    [Theory] [InlineData(5, true)] [InlineData(-1, true)] [InlineData(60, false)] [InlineData(-30, false)]
    public async Task Fixed_events_get_due_reminders_once_and_respect_the_configured_window(int minutes, bool expected)
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        await h.DbAsync(async db => {
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "PRN212", Start = now.AddMinutes(minutes), End = now.AddMinutes(minutes + 60) });
            await db.SaveChangesAsync(Ct);
        });
        await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct));
        await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct));
        var rows = await h.DbAsync(db => db.InAppNotifications.Where(x => x.Kind == "EVENT_REMINDER").ToArrayAsync(Ct));
        Assert.Equal(expected ? 1 : 0, rows.Length);
        if (expected) { Assert.Contains("PRN212", rows[0].Message); Assert.True(rows[0].IsVisible); Assert.False(rows[0].EmailPending); }
    }
    [Fact] public async Task Opt_out_suppresses_fixed_event_notifications()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        await Call(h, s => s.SetNotificationSettingsAsync(user.Id, new(InAppEnabled: false), Ct));
        await h.DbAsync(async db => { db.FixedEvents.Add(new() { UserId = user.Id, Name = "Private", Start = now.AddMinutes(5), End = now.AddHours(1) }); await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct));
        Assert.Equal(0, await h.DbAsync(db => db.InAppNotifications.CountAsync(x => x.Kind == "EVENT_REMINDER", Ct)));
    }
    [Fact] public async Task Existing_needs_review_sessions_receive_one_visible_catch_up_reminder()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        await h.DbAsync(async db => {
            var task = new WorkTask { UserId = user.Id, Name = "Report", Deadline = now.AddDays(2) }; db.WorkTasks.Add(task);
            db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task.Id, Start = now.AddHours(-2), End = now.AddHours(-1), Status = WorkSessionStatus.NeedsReview });
            await db.SaveChangesAsync(Ct);
        });
        await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct)); await Call(h, s => s.EnsureNotificationsAsync(user.Id, Ct));
        Assert.Equal(1, await h.DbAsync(db => db.InAppNotifications.CountAsync(x => x.Kind == "SESSION_REVIEW", Ct)));
    }
}
