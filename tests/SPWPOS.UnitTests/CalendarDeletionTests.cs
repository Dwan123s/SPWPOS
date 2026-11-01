using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class CalendarDeletionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task Call(ServiceHarness h, Func<IWorkspaceService, Task> action)
    { await using var scope = h.Provider.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    [Fact]
    public async Task Deleting_a_session_removes_only_that_calendar_slot_and_cancels_its_reminders()
    {
        await using var h = await ServiceHarness.CreateAsync(); var u = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        var task = new WorkTask { UserId = u.Id, Name = "Report", Deadline = now.AddDays(3) };
        var session = new WorkSession { UserId = u.Id, TaskId = task.Id, Start = now.AddHours(1), End = now.AddHours(2) };
        var other = new WorkSession { UserId = u.Id, TaskId = task.Id, Start = now.AddHours(3), End = now.AddHours(4) };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); db.WorkSessions.AddRange(session, other); db.InAppNotifications.Add(new() {
            UserId = u.Id, SessionId = session.Id, Kind = "SESSION_REMINDER", DedupeKey = "session-reminder", IsVisible = true, EmailPending = true, TelegramPending = true }); await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.DeleteSessionAsync(u.Id, session.Id, Ct)); await Call(h, s => s.DeleteSessionAsync(u.Id, session.Id, Ct));
        await h.DbAsync(async db => {
            Assert.Equal(WorkSessionStatus.Cancelled, (await db.WorkSessions.SingleAsync(x => x.Id == session.Id, Ct)).Status);
            Assert.Equal(WorkSessionStatus.Scheduled, (await db.WorkSessions.SingleAsync(x => x.Id == other.Id, Ct)).Status);
            var saved = await db.WorkTasks.SingleAsync(Ct); Assert.Equal(task.Deadline, saved.Deadline); Assert.Equal(WorkTaskStatus.Todo, saved.Status);
            var notice = await db.InAppNotifications.SingleAsync(Ct); Assert.False(notice.IsVisible); Assert.False(notice.TelegramPending); Assert.False(notice.EmailPending);
        });
        await Call(h, async s => { var w = await s.GetAsync(u.Id, Ct); Assert.DoesNotContain(w.Sessions, x => x.Id == session.Id); Assert.Contains(w.Sessions, x => x.Id == other.Id); });
    }
    [Theory]
    [InlineData(WorkSessionStatus.Running)] [InlineData(WorkSessionStatus.Paused)] [InlineData(WorkSessionStatus.Completed)] [InlineData(WorkSessionStatus.Skipped)]
    public async Task Cannot_delete_a_session_that_has_already_been_used(WorkSessionStatus status)
    {
        await using var h = await ServiceHarness.CreateAsync(); var u = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        var task = new WorkTask { UserId = u.Id, Name = "Report", Deadline = now.AddDays(3) };
        var session = new WorkSession { UserId = u.Id, TaskId = task.Id, Start = now, End = now.AddHours(1), Status = status };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); db.WorkSessions.Add(session); await db.SaveChangesAsync(Ct); });
        var error = await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.DeleteSessionAsync(u.Id, session.Id, Ct)));
        Assert.Equal("SESSION_NOT_DELETABLE", error.Code);
        Assert.Equal(status, await h.DbAsync(db => db.WorkSessions.Select(x => x.Status).SingleAsync(Ct)));
    }
    [Fact]
    public async Task Locked_session_requires_explicit_unlock_before_deleting()
    {
        await using var h = await ServiceHarness.CreateAsync(); var u = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        var task = new WorkTask { UserId = u.Id, Name = "Report", Deadline = now.AddDays(3) };
        var session = new WorkSession { UserId = u.Id, TaskId = task.Id, Start = now.AddHours(1), End = now.AddHours(2), IsLocked = true };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); db.WorkSessions.Add(session); await db.SaveChangesAsync(Ct); });
        var error = await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.DeleteSessionAsync(u.Id, session.Id, Ct))); Assert.Equal("SESSION_LOCKED", error.Code);
        await Call(h, s => s.LockSessionAsync(u.Id, session.Id, false, Ct)); await Call(h, s => s.DeleteSessionAsync(u.Id, session.Id, Ct));
        Assert.Equal(WorkSessionStatus.Cancelled, await h.DbAsync(db => db.WorkSessions.Select(x => x.Status).SingleAsync(Ct)));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Recorded_time_or_execution_history_blocks_deletion_even_if_status_looks_scheduled(bool history)
    {
        await using var h = await ServiceHarness.CreateAsync(); var u = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        var task = new WorkTask { UserId = u.Id, Name = "Report", Deadline = now.AddDays(3) };
        var session = new WorkSession { UserId = u.Id, TaskId = task.Id, Start = now.AddHours(1), End = now.AddHours(2), ActualSeconds = history ? 0 : 1 };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); db.WorkSessions.Add(session);
            if (history) db.SessionExecutions.Add(new() { UserId = u.Id, SessionId = session.Id, StartedAt = now });
            await db.SaveChangesAsync(Ct); });
        Assert.Equal("SESSION_NOT_DELETABLE", (await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.DeleteSessionAsync(u.Id, session.Id, Ct)))).Code);
        Assert.Equal(WorkSessionStatus.Scheduled, await h.DbAsync(db => db.WorkSessions.Select(x => x.Status).SingleAsync(Ct)));
    }
    [Fact]
    public async Task Deleting_one_fixed_occurrence_preserves_other_occurrences_and_stops_its_reminders()
    {
        await using var h = await ServiceHarness.CreateAsync(); var u = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        var e = new FixedEvent { UserId = u.Id, Name = "Lớp học", Start = now.AddHours(1), End = now.AddHours(2) };
        var next = new FixedEvent { UserId = u.Id, Name = e.Name, Start = e.Start.AddDays(7), End = e.End.AddDays(7) };
        await h.DbAsync(async db => { db.FixedEvents.AddRange(e, next); db.InAppNotifications.Add(new() { UserId = u.Id, Kind = "EVENT_REMINDER", DedupeKey = $"event:{e.Id}:{e.Start.UtcTicks}", TelegramPending = true }); await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.DeleteEventAsync(u.Id, e.Id, Ct));
        await h.DbAsync(async db => { Assert.Equal(next.Id, (await db.FixedEvents.SingleAsync(Ct)).Id); var n = await db.InAppNotifications.SingleAsync(Ct); Assert.False(n.TelegramPending); Assert.False(n.IsVisible); });
    }
    [Fact]
    public async Task Cannot_delete_another_accounts_calendar_data()
    {
        await using var h = await ServiceHarness.CreateAsync(); var u = await h.RegisterAsync(); var now = h.Clock.GetUtcNow();
        var other = new ApplicationUser { Id = Guid.NewGuid(), UserName = "other", Email = "other@test.local", NormalizedEmail = "OTHER@TEST.LOCAL", EmailConfirmed = true, IsActive = true };
        var task = new WorkTask { UserId = other.Id, Name = "Private", Deadline = now.AddDays(3) };
        var session = new WorkSession { UserId = other.Id, TaskId = task.Id, Start = now.AddHours(1), End = now.AddHours(2) };
        var e = new FixedEvent { UserId = other.Id, Name = "Private", Start = now.AddHours(1), End = now.AddHours(2) };
        await h.DbAsync(async db => { db.Users.Add(other); db.WorkTasks.Add(task); db.WorkSessions.Add(session); db.FixedEvents.Add(e); await db.SaveChangesAsync(Ct); });
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.DeleteSessionAsync(u.Id, session.Id, Ct)))).Code);
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.DeleteEventAsync(u.Id, e.Id, Ct)))).Code);
        Assert.Equal(WorkSessionStatus.Scheduled, await h.DbAsync(db => db.WorkSessions.Select(x => x.Status).SingleAsync(Ct))); Assert.Equal(1, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
    }
}
