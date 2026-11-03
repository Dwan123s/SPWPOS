using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class WorkspaceUpgradeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-28T12:00:00Z");
    private static async Task<T> Call<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    private static Task Call(ServiceHarness h, Func<IWorkspaceService, Task> action) => Call(h, async s => { await action(s); return true; });
    private static async Task<(Guid User, WorkTask Task, WorkSession Session)> Setup(ServiceHarness h)
    {
        h.Clock.Advance(Now - h.Clock.GetUtcNow()); var user = await h.RegisterAsync();
        var task = new WorkTask { UserId = user.Id, Name = "Báo cáo", EstimatedMinutes = 60, MinimumSessionMinutes = 25, Deadline = Now.AddDays(2), IsHardDeadline = true };
        var session = new WorkSession { UserId = user.Id, TaskId = task.Id, Start = Now.AddHours(-2), End = Now.AddHours(-1) };
        await h.DbAsync(async db => {
            db.WorkPreferences.Add(new() { UserId = user.Id, TimeZoneId = "UTC", MaximumSessionMinutes = 120, MaximumDailyMinutes = 480, BreakMinutes = 10 });
            db.WeeklyAvailability.AddRange(Enumerable.Range(0, 7).Select(day => new WeeklyAvailability { UserId = user.Id, DayOfWeek = day, StartMinute = 480, EndMinute = 1080 }));
            db.WorkTasks.Add(task); db.WorkSessions.Add(session); await db.SaveChangesAsync(Ct);
        });
        return (user.Id, task, session);
    }

    [Fact] public async Task Catch_up_completes_without_timer_and_awards_experience_only_once()
    {
        await using var h = await ServiceHarness.CreateAsync(); var data = await Setup(h);
        Assert.Equal(data.Session.Id, Assert.Single(await Call(h, s => s.GetCatchUpAsync(data.User, Ct))).Id);
        await Call(h, s => s.CatchUpAsync(data.User, data.Session.Id, new("COMPLETED"), Ct));
        var revision = (await Call(h, s => s.GetAsync(data.User, Ct))).Preference.Revision;
        await Call(h, s => s.CatchUpAsync(data.User, data.Session.Id, new("COMPLETED"), Ct));
        Assert.Equal(revision, (await Call(h, s => s.GetAsync(data.User, Ct))).Preference.Revision);
        Assert.Empty(await Call(h, s => s.GetCatchUpAsync(data.User, Ct)));
        await h.DbAsync(async db => {
            var session = await db.WorkSessions.SingleAsync(Ct); var execution = await db.SessionExecutions.SingleAsync(Ct);
            Assert.Equal(WorkSessionStatus.Completed, session.Status); Assert.Equal(3600, session.ActualSeconds); Assert.True(session.ExperienceAwarded);
            Assert.True(execution.IsUserAdjusted); Assert.Equal(0, execution.TimerSeconds); Assert.Empty(await db.ExecutionSegments.ToArrayAsync(Ct));
            Assert.Equal(100, (await db.WorkTasks.SingleAsync(Ct)).Progress); Assert.Equal(39, (await db.Users.SingleAsync(Ct)).ExperiencePoints);
        });
    }

    [Theory] [InlineData("MISSED", 60)] [InlineData("PARTIAL", 30)]
    public async Task Repair_preserves_commitments_and_only_schedules_remaining_work(string outcome, int duration)
    {
        await using var h = await ServiceHarness.CreateAsync(); var data = await Setup(h);
        var other = new WorkTask { UserId = data.User, Name = "Giữ nguyên", EstimatedMinutes = 60, Deadline = Now.AddDays(1) };
        var kept = new WorkSession { UserId = data.User, TaskId = other.Id, Start = Now.AddHours(3).AddMinutes(20), End = Now.AddHours(4).AddMinutes(20) };
        await h.DbAsync(async db => {
            db.WorkTasks.Add(other); db.WorkSessions.Add(kept);
            db.FixedEvents.Add(new() { UserId = data.User, Name = "Họp", Start = Now, End = Now.AddHours(2) }); await db.SaveChangesAsync(Ct);
        });
        var input = new CatchUpInput(outcome, outcome == "PARTIAL" ? 50 : null, outcome == "PARTIAL" ? 20 : null);
        var result = await Call(h, s => s.CatchUpAsync(data.User, data.Session.Id, input, Ct));
        var replay = await Call(h, s => s.CatchUpAsync(data.User, data.Session.Id, input, Ct));
        Assert.NotNull(result.ReplacementSessionId); Assert.Equal(result.ReplacementSessionId, replay.ReplacementSessionId);
        await h.DbAsync(async db => {
            var replacement = await db.WorkSessions.SingleAsync(x => x.RepairSourceSessionId == data.Session.Id, Ct);
            Assert.Equal(duration, (replacement.End - replacement.Start).TotalMinutes); Assert.Equal("AUTO_HEAL", replacement.ReasonCode);
            Assert.True(replacement.Start >= Now.AddHours(2)); Assert.True(replacement.End <= kept.Start.AddMinutes(-10));
            var retained = await db.WorkSessions.SingleAsync(x => x.Id == kept.Id, Ct); Assert.Equal(kept.Start, retained.Start); Assert.Equal(kept.End, retained.End);
            Assert.Equal(data.Task.Deadline, (await db.WorkTasks.SingleAsync(x => x.Id == data.Task.Id, Ct)).Deadline);
            Assert.Single(await db.InAppNotifications.Where(x => x.Kind == "AUTO_HEAL").ToArrayAsync(Ct));
            Assert.Equal(0, (await db.Users.SingleAsync(Ct)).ExperiencePoints);
        });
    }

    [Fact] public async Task Expired_hard_deadline_is_never_extended_by_repair()
    {
        await using var h = await ServiceHarness.CreateAsync(); var data = await Setup(h);
        await h.DbAsync(async db => { (await db.WorkTasks.SingleAsync(Ct)).Deadline = Now.AddMinutes(-1); await db.SaveChangesAsync(Ct); });
        var result = await Call(h, s => s.CatchUpAsync(data.User, data.Session.Id, new("MISSED"), Ct));
        Assert.Null(result.ReplacementSessionId); Assert.Equal(1, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
    }

    [Fact] public async Task Inventory_keeps_only_default_nora_and_paid_fox_and_rejects_removed_skins()
    {
        await using var h = await ServiceHarness.CreateAsync(); var data = await Setup(h);
        await h.DbAsync(async db => { (await db.Users.SingleAsync(Ct)).ExperiencePoints = 98; await db.SaveChangesAsync(Ct); });
        var locked = await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.SelectSkinAsync(data.User, new("forest"), Ct)));
        Assert.Equal("SKIN_NOT_FOUND", locked.Code);
        await Call(h, s => s.CatchUpAsync(data.User, data.Session.Id, new("COMPLETED"), Ct));
        var profile = await Call(h, s => s.GetGamificationAsync(data.User, Ct));
        Assert.Equal(137, profile.ExperiencePoints); Assert.Equal(2, profile.UserLevel); Assert.Equal(200, profile.NextLevelXp);
        Assert.Equal(new[] { "classic", "fox" }, profile.Inventory.Select(x => x.Code));
        Assert.False(profile.Inventory.Single(x => x.Code == "fox").Unlocked);
        await h.DbAsync(async db => { (await db.Users.SingleAsync(Ct)).NoraSkin = "forest"; await db.SaveChangesAsync(Ct); });
        Assert.Equal("classic", (await Call(h, s => s.GetGamificationAsync(data.User, Ct))).SelectedSkin);
        await Call(h, s => s.SelectSkinAsync(data.User, new("classic"), Ct));
    }

    [Fact] public async Task Calendar_escapes_text_folds_utf8_and_isolates_ownership()
    {
        await using var h = await ServiceHarness.CreateAsync(); var data = await Setup(h);
        await h.DbAsync(async db => {
            (await db.WorkTasks.SingleAsync(Ct)).Name = string.Concat(Enumerable.Repeat("Lịch học 🌿", 16)) + ",;\\\r\nBEGIN:VEVENT";
            var other = new ApplicationUser { Id = Guid.NewGuid(), UserName = "other", NormalizedUserName = "OTHER", Email = "other@example.com", NormalizedEmail = "OTHER@EXAMPLE.COM" }; db.Users.Add(other);
            db.FixedEvents.AddRange(new FixedEvent { UserId = data.User, Name = "Sự kiện riêng", Start = Now, End = Now.AddHours(1) },
                new FixedEvent { UserId = other.Id, Name = "SECRET_OTHER_USER", Start = Now, End = Now.AddHours(1) });
            await db.SaveChangesAsync(Ct);
        });
        var text = await Call(h, s => s.ExportCalendarAsync(data.User, Ct));
        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", text); Assert.EndsWith("END:VCALENDAR\r\n", text);
        Assert.DoesNotContain("SECRET_OTHER_USER", text);
        var physical = text.Split("\r\n"); Assert.All(physical, line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75));
        Assert.Equal(2, physical.Count(line => line == "BEGIN:VEVENT"));
        var unfolded = text.Replace("\r\n ", ""); Assert.Contains("\\,\\;\\\\\\nBEGIN:VEVENT", unfolded);
        Assert.Contains("DTSTART:20261028T100000Z", text);
    }

    [Fact] public async Task Stale_workspace_version_rejects_mutation_and_rolls_back_related_changes()
    {
        await using var h = await ServiceHarness.CreateAsync(); var data = await Setup(h);
        var notification = new InAppNotification { UserId = data.User, Kind = "TEST", Title = "Test", DedupeKey = "test" };
        await h.DbAsync(async db => { db.InAppNotifications.Add(notification); await db.SaveChangesAsync(Ct); });
        await using var stale = h.Provider.CreateAsyncScope(); var staleDb = stale.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await staleDb.WorkPreferences.SingleAsync(Ct);
        await h.DbAsync(async db => { (await db.WorkPreferences.SingleAsync(Ct)).MutationVersion = Guid.NewGuid(); await db.SaveChangesAsync(Ct); });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.ServiceProvider.GetRequiredService<IWorkspaceService>().MarkNotificationAsync(data.User, notification.Id, Ct));
        Assert.Null(await h.DbAsync(db => db.InAppNotifications.Select(x => x.ReadAt).SingleAsync(Ct)));
    }
}
