using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Calendar;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Calendar;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed class GoogleCalendarSyncTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Call<T>(ServiceHarness h, Func<GoogleCalendarService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<GoogleCalendarService>()); }
    private static Task Call(ServiceHarness h, Func<GoogleCalendarService, Task> action) => Call(h, async service => { await action(service); return true; });
    private static async Task<(ServiceHarness Harness, FakeCalendar Google, Guid User)> Setup(bool connect = true, bool webhook = false)
    {
        var google = new FakeCalendar();
        var harness = await ServiceHarness.CreateAsync(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.Configure<GoogleCalendarOptions>(o => { o.Enabled = true; o.ClientId = "test-client"; o.ClientSecret = "test-secret"; o.KeyRingPath = "test-unused"; if (webhook) o.WebhookUrl = "https://planora.example/api/integrations/google-calendar/webhook"; });
            services.AddSingleton<IGoogleCalendarGateway>(google);
        });
        var user = await harness.RegisterAsync();
        google.Clock = harness.Clock;
        if (connect)
        {
            var result = await Call(harness, s => s.ConnectAsync(user.Id, new(), Ct));
            await Call(harness, s => s.CompleteOAuthAsync(QueryHelpers.ParseQuery(new Uri(result.AuthorizationUrl).Query)["state"].ToString(), "code", null, Ct));
        }
        return (harness, google, user.Id);
    }
    private static async Task<FixedEvent> Event(ServiceHarness h, Guid uid)
    {
        var item = new FixedEvent { UserId = uid, Name = "PRN212", Start = h.Clock.GetUtcNow().AddHours(2), End = h.Clock.GetUtcNow().AddHours(3) };
        await h.DbAsync(async db => { db.FixedEvents.Add(item); await db.SaveChangesAsync(Ct); }); return item;
    }

    [Fact] public async Task OAuth_is_one_use_and_keeps_refresh_tokens_out_of_plaintext_storage()
    {
        var (h, google, uid) = await Setup(false); await using var owned = h;
        var result = await Call(h, s => s.ConnectAsync(uid, new(10, true), Ct));
        var state = QueryHelpers.ParseQuery(new Uri(result.AuthorizationUrl).Query)["state"].ToString();
        await Call(h, s => s.CompleteOAuthAsync(state, "code", null, Ct));
        var error = await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.CompleteOAuthAsync(state, "code", null, Ct)));
        Assert.Equal("CALENDAR_OAUTH_STATE", error.Code); Assert.Equal(1, google.Exchanges);
        var c = await h.DbAsync(db => db.GoogleCalendarConnections.SingleAsync(Ct));
        Assert.DoesNotContain("refresh-secret", c.ProtectedRefreshToken);
        var status = await Call(h, s => s.StatusAsync(uid, Ct)); Assert.Equal(10, status.ReminderMinutes); Assert.True(status.RemindAtStart);
    }
    [Fact] public async Task Outbound_create_edit_delete_is_idempotent_and_keeps_google_reminders_on_edits()
    {
        var (h, google, uid) = await Setup(); await using var owned = h; var item = await Event(h, uid);
        await Call(h, s => s.SyncUserAsync(uid, Ct));
        var remote = Assert.Single(google.Events.Values); Assert.Equal("PRN212", remote.Snapshot!.Title);
        Assert.Equal(15, Assert.Single(google.ReminderWrites)!.ReminderMinutes);
        await h.DbAsync(async db => { (await db.FixedEvents.SingleAsync(Ct)).Name = "PRN212 mới"; await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.SyncUserAsync(uid, Ct)); await Call(h, s => s.SyncUserAsync(uid, Ct));
        Assert.Single(google.Events); Assert.Equal("PRN212 mới", google.Events[remote.Id].Snapshot!.Title);
        Assert.Null(google.ReminderWrites.Last());
        await h.DbAsync(async db => { db.FixedEvents.Remove(await db.FixedEvents.SingleAsync(Ct)); await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.SyncUserAsync(uid, Ct)); Assert.True(google.Events[remote.Id].Deleted);
        Assert.Equal(0, await h.DbAsync(db => db.CalendarSyncOutbox.CountAsync(Ct)));
    }
    [Fact] public async Task Google_add_move_delete_updates_busy_slots_without_echo_or_recreation()
    {
        var (h, google, uid) = await Setup(); await using var owned = h;
        var snapshot = new CalendarSnapshot("Họp nhóm", h.Clock.GetUtcNow().AddHours(4), h.Clock.GetUtcNow().AddHours(5));
        google.Events["google-only"] = new("google-only", "v1", snapshot, false);
        await Call(h, s => s.SyncUserAsync(uid, Ct));
        Assert.Equal(snapshot.Start, (await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct))).Start);
        Assert.Empty(google.ReminderWrites);
        google.Events["google-only"] = new("google-only", "v2", snapshot with { Start = snapshot.Start.AddHours(1), End = snapshot.End.AddHours(1) }, false);
        await Call(h, s => s.SyncUserAsync(uid, Ct)); Assert.Equal(snapshot.Start.AddHours(1), (await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct))).Start);
        google.Events["google-only"] = new("google-only", "v3", null, true);
        await Call(h, s => s.SyncUserAsync(uid, Ct)); await Call(h, s => s.SyncUserAsync(uid, Ct));
        Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct))); Assert.Empty(google.ReminderWrites);
    }
    [Fact] public async Task Concurrent_time_edits_require_a_decision_then_keep_the_selected_version()
    {
        var (h, google, uid) = await Setup(); await using var owned = h; var item = await Event(h, uid);
        await Call(h, s => s.SyncUserAsync(uid, Ct)); var original = Assert.Single(google.Events.Values);
        await h.DbAsync(async db => { var e = await db.FixedEvents.SingleAsync(Ct); e.Start = e.Start.AddHours(1); e.End = e.End.AddHours(1); await db.SaveChangesAsync(Ct); });
        google.Events[original.Id] = original with { Etag = "google-new", Snapshot = original.Snapshot! with { Start = original.Snapshot.Start.AddHours(2), End = original.Snapshot.End.AddHours(2) } };
        await Call(h, s => s.SyncUserAsync(uid, Ct));
        var conflict = Assert.Single(await Call(h, s => s.ConflictsAsync(uid, Ct)));
        Assert.Equal(item.Start.AddHours(1), conflict.Local!.Start); Assert.Equal(item.Start.AddHours(2), conflict.Google!.Start);
        await Call(h, s => s.ResolveAsync(uid, conflict.Id, "local", Ct)); await Call(h, s => s.SyncUserAsync(uid, Ct));
        Assert.Empty(await Call(h, s => s.ConflictsAsync(uid, Ct)));
        Assert.Equal(item.Start.AddHours(1), google.Events[original.Id].Snapshot!.Start);
    }
    [Fact] public async Task Google_session_deletion_keeps_the_task_execution_history_and_xp_unchanged()
    {
        var (h, google, uid) = await Setup(); await using var owned = h;
        var task = new WorkTask { UserId = uid, Name = "Báo cáo", EstimatedMinutes = 60, Deadline = h.Clock.GetUtcNow().AddDays(1) };
        var session = new WorkSession { UserId = uid, TaskId = task.Id, Start = h.Clock.GetUtcNow().AddHours(1), End = h.Clock.GetUtcNow().AddHours(2) };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); db.WorkSessions.Add(session); await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.SyncUserAsync(uid, Ct)); var remote = Assert.Single(google.Events.Values);
        google.Events[remote.Id] = remote with { Deleted = true, Snapshot = null, Etag = "deleted" };
        await Call(h, s => s.SyncUserAsync(uid, Ct));
        Assert.Equal(WorkSessionStatus.Cancelled, (await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct))).Status);
        Assert.Equal(task.Id, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).Id);
        Assert.Equal(0, (await h.DbAsync(db => db.Users.SingleAsync(Ct))).ExperiencePoints);
    }
    [Fact] public async Task Invalid_sync_token_recovers_with_a_full_scan_and_preserves_pending_local_changes()
    {
        var (h, google, uid) = await Setup(); await using var owned = h; await Event(h, uid);
        await Call(h, s => s.SyncUserAsync(uid, Ct));
        await h.DbAsync(async db => { (await db.FixedEvents.SingleAsync(Ct)).Name = "Tên chưa gửi"; await db.SaveChangesAsync(Ct); });
        google.ExpireTokenOnce = true; await Call(h, s => s.SyncUserAsync(uid, Ct));
        Assert.True(google.FullScans >= 2); Assert.Equal("Tên chưa gửi", Assert.Single(google.Events.Values).Snapshot!.Title);
        Assert.Equal("Tên chưa gửi", (await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct))).Name);
    }
    [Fact] public async Task Ambiguous_network_failure_after_creation_does_not_duplicate_on_retry()
    {
        var (h, google, uid) = await Setup(); await using var owned = h; await Event(h, uid); google.FailAfterWriteOnce = true;
        await Call(h, s => s.SyncUserAsync(uid, Ct));
        Assert.Single(google.Events); Assert.True(await h.DbAsync(db => db.CalendarSyncOutbox.AnyAsync(Ct)));
        await Call(h, s => s.SyncUserAsync(uid, Ct)); Assert.Single(google.Events);
        Assert.Equal(0, await h.DbAsync(db => db.CalendarSyncOutbox.CountAsync(Ct)));
    }
    [Fact] public async Task Concurrent_disjoint_fields_merge_and_manual_google_moves_lock_the_session()
    {
        var now = DateTimeOffset.UtcNow;
        var baseline = new CalendarSnapshot("A", now, now.AddHours(1));
        Assert.True(GoogleCalendarService.TryMerge(baseline, baseline with { Title = "B" }, baseline with { Start = now.AddHours(2), End = now.AddHours(3) }, out var merged));
        Assert.Equal("B", merged!.Title); Assert.Equal(now.AddHours(2), merged.Start);
        var (h, google, uid) = await Setup(); await using var owned = h;
        var task = new WorkTask { UserId = uid, Name = "A", Deadline = h.Clock.GetUtcNow().AddDays(2), EstimatedMinutes = 60 };
        var session = new WorkSession { UserId = uid, TaskId = task.Id, Start = h.Clock.GetUtcNow().AddHours(1), End = h.Clock.GetUtcNow().AddHours(2) };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); db.WorkSessions.Add(session); await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.SyncUserAsync(uid, Ct)); var original = Assert.Single(google.Events.Values);
        google.Events[original.Id] = original with { Etag = "moved", Snapshot = original.Snapshot! with { Start = session.Start.AddHours(2), End = session.End.AddHours(2) } };
        await Call(h, s => s.SyncUserAsync(uid, Ct)); var moved = await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct));
        Assert.True(moved.IsLocked); Assert.Equal(session.Start.AddHours(2), moved.Start); Assert.Equal(0, moved.ActualSeconds);
    }
    [Fact] public async Task Disconnect_leaves_google_events_and_reconnect_uses_the_same_calendar()
    {
        var (h, google, uid) = await Setup(); await using var owned = h; await Event(h, uid);
        await Call(h, s => s.SyncUserAsync(uid, Ct)); await Call(h, s => s.DisconnectAsync(uid, Ct));
        Assert.Single(google.Events); Assert.False((await Call(h, s => s.StatusAsync(uid, Ct))).Connected);
        Assert.Empty((await h.DbAsync(db => db.GoogleCalendarConnections.SingleAsync(Ct))).ProtectedRefreshToken);
        var result = await Call(h, s => s.ConnectAsync(uid, new(), Ct));
        await Call(h, s => s.CompleteOAuthAsync(QueryHelpers.ParseQuery(new Uri(result.AuthorizationUrl).Query)["state"].ToString(), "code", null, Ct));
        await Call(h, s => s.SyncUserAsync(uid, Ct)); Assert.Equal(1, google.CalendarsCreated); Assert.Single(google.Events);
    }
    [Fact] public async Task Unauthorized_webhooks_and_other_users_cannot_access_or_resolve_conflicts()
    {
        var (h, google, uid) = await Setup(); await using var owned = h;
        var channel = new CalendarWatchChannel { UserId = uid, ConnectionId = await h.DbAsync(db => db.GoogleCalendarConnections.Select(x => x.Id).SingleAsync(Ct)),
            ResourceId = "resource", TokenHash = new string('A', 64), ExpiresAt = h.Clock.GetUtcNow().AddDays(1) };
        await h.DbAsync(async db => { db.CalendarWatchChannels.Add(channel); var c = await db.GoogleCalendarConnections.SingleAsync(Ct); c.NextSyncAt = h.Clock.GetUtcNow().AddDays(1); await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.ReceiveWebhookAsync(channel.Id.ToString("N"), "resource", new string('B', 64), Ct));
        Assert.Equal(h.Clock.GetUtcNow().AddDays(1), (await h.DbAsync(db => db.GoogleCalendarConnections.SingleAsync(Ct))).NextSyncAt);
        Assert.Empty(await Call(h, s => s.ConflictsAsync(Guid.NewGuid(), Ct)));
        var exception = await Assert.ThrowsAsync<AuthException>(() => Call(h, s => s.ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), "google", Ct)));
        Assert.Equal(404, exception.StatusCode);
    }
    [Fact] public async Task Rolling_back_a_local_mutation_also_rolls_back_its_outbox_and_duplicate_leases_are_skipped()
    {
        var (h, google, uid) = await Setup(); await using var owned = h;
        await h.DbAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(Ct);
            db.FixedEvents.Add(new() { UserId = uid, Name = "Không lưu", Start = h.Clock.GetUtcNow().AddHours(1), End = h.Clock.GetUtcNow().AddHours(2) });
            await db.SaveChangesAsync(Ct); Assert.True(await db.CalendarSyncOutbox.AnyAsync(Ct));
            await tx.RollbackAsync(Ct);
        });
        Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.CalendarSyncOutbox.CountAsync(Ct)));
        await Event(h, uid);
        await h.DbAsync(async db => { var c = await db.GoogleCalendarConnections.SingleAsync(Ct); c.LeaseId = Guid.NewGuid(); c.LeaseUntil = h.Clock.GetUtcNow().AddMinutes(5); await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.SyncUserAsync(uid, Ct)); Assert.Empty(google.Events);
    }
    [Fact] public async Task Recurrence_instances_are_busy_slots_and_series_deletion_removes_them_without_exporting_copies()
    {
        var (h, google, uid) = await Setup(); await using var owned = h;
        google.Events["series"] = new("series", "v1", null, false, true, IsSeries: true);
        google.Instances.Add(new("series-instance", "v1", new("Lịch lặp", h.Clock.GetUtcNow().AddHours(1), h.Clock.GetUtcNow().AddHours(2)), false, true, "series"));
        await Call(h, s => s.SyncUserAsync(uid, Ct));
        Assert.Equal("Lịch lặp", (await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct))).Name); Assert.Empty(google.ReminderWrites);
        google.Events["series"] = new("series", "deleted", null, true);
        await Call(h, s => s.SyncUserAsync(uid, Ct)); Assert.Equal(0, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
    }
    [Fact] public async Task Pagination_commits_only_the_final_token_and_active_execution_is_not_deleted_by_google()
    {
        var (h, google, uid) = await Setup(); await using var owned = h; google.PaginateOnce = true;
        var task = new WorkTask { UserId = uid, Name = "Đang làm", EstimatedMinutes = 60, Deadline = h.Clock.GetUtcNow().AddDays(1) };
        var session = new WorkSession { UserId = uid, TaskId = task.Id, Start = h.Clock.GetUtcNow().AddHours(1), End = h.Clock.GetUtcNow().AddHours(2) };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); db.WorkSessions.Add(session); await db.SaveChangesAsync(Ct); });
        await Call(h, s => s.SyncUserAsync(uid, Ct)); Assert.True(google.PagesRead >= 2);
        await h.DbAsync(async db => { var s = await db.WorkSessions.SingleAsync(Ct); s.ActualSeconds = 600; s.Status = WorkSessionStatus.Completed; await db.SaveChangesAsync(Ct); });
        var remote = Assert.Single(google.Events.Values); google.Events[remote.Id] = remote with { Deleted = true, Snapshot = null, Etag = "deleted" };
        await Call(h, s => s.SyncUserAsync(uid, Ct));
        var conflict = Assert.Single(await Call(h, s => s.ConflictsAsync(uid, Ct))); Assert.False(conflict.CanKeepGoogle);
        Assert.Equal(WorkSessionStatus.Completed, (await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct))).Status);
        await Call(h, s => s.ResolveAsync(uid, conflict.Id, "local", Ct)); await Call(h, s => s.SyncUserAsync(uid, Ct));
        Assert.Empty(await Call(h, s => s.ConflictsAsync(uid, Ct)));
        Assert.Equal(600, (await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct))).ActualSeconds);
    }

    [Fact] public async Task Watch_is_renewed_and_verified_callbacks_queue_sync_without_invalidating_unchanged_workspace()
    {
        var (h, google, uid) = await Setup(webhook: true); await using var owned = h; await Event(h, uid);
        await Call(h, s => s.SyncUserAsync(uid, Ct)); Assert.Equal(1, google.WatchCalls);
        var revision = await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct));
        await Call(h, s => s.SyncUserAsync(uid, Ct));
        Assert.Equal(revision, await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct)));
        var channel = await h.DbAsync(db => db.CalendarWatchChannels.SingleAsync(Ct));
        await Call(h, s => s.ReceiveWebhookAsync(channel.Id.ToString("N"), "wrong-resource", google.WatchToken!, Ct));
        Assert.True((await h.DbAsync(db => db.GoogleCalendarConnections.SingleAsync(Ct))).NextSyncAt > h.Clock.GetUtcNow());
        await Call(h, s => s.ReceiveWebhookAsync(channel.Id.ToString("N"), "resource", google.WatchToken!, Ct));
        Assert.Equal(h.Clock.GetUtcNow(), (await h.DbAsync(db => db.GoogleCalendarConnections.SingleAsync(Ct))).NextSyncAt);
        h.Clock.Advance(TimeSpan.FromDays(7)); await Call(h, s => s.SyncUserAsync(uid, Ct));
        Assert.Equal(2, google.WatchCalls); Assert.Equal(1, await h.DbAsync(db => db.CalendarWatchChannels.CountAsync(Ct)));
    }

    public sealed class FakeCalendar : IGoogleCalendarGateway
    {
        public Dictionary<string, CalendarRemoteEvent> Events { get; } = [];
        public List<CalendarReminderInput?> ReminderWrites { get; } = [];
        public int Exchanges { get; private set; }
        public int CalendarsCreated { get; private set; }
        public int FullScans { get; private set; }
        public bool ExpireTokenOnce { get; set; }
        public bool FailAfterWriteOnce { get; set; }
        public bool PaginateOnce { get; set; }
        public int PagesRead { get; private set; }
        public List<CalendarRemoteEvent> Instances { get; } = [];
        public TimeProvider Clock { get; set; } = TimeProvider.System;
        public int WatchCalls { get; private set; }
        public string? WatchToken { get; private set; }
        public string AuthorizationUrl(string state) => "https://accounts.google.com/o/oauth2/v2/auth?state=" + state;
        public Task<CalendarGrant> ExchangeAsync(string code, CancellationToken ct) { Exchanges++; return Task.FromResult(new CalendarGrant("access", "refresh-secret", "subject", "calendar@example.com")); }
        public Task<string> RefreshAsync(string refreshToken, CancellationToken ct) => Task.FromResult("access");
        public Task<string> CreateCalendarAsync(string accessToken, CancellationToken ct) { CalendarsCreated++; return Task.FromResult("planora-calendar"); }
        public Task<CalendarPage> ListAsync(string accessToken, string calendarId, string? syncToken, string? pageToken, CancellationToken ct)
        {
            PagesRead++;
            if (PaginateOnce && pageToken is null) { PaginateOnce = false; return Task.FromResult(new CalendarPage([], "page-two", null)); }
            if (ExpireTokenOnce && syncToken is not null) { ExpireTokenOnce = false; throw new CalendarProviderException(410, "fullSyncRequired"); }
            if (syncToken is null) FullScans++;
            return Task.FromResult(new CalendarPage(Events.Values.ToArray(), null, "next-token"));
        }
        public Task<CalendarRemoteEvent?> GetAsync(string accessToken, string calendarId, string eventId, CancellationToken ct) =>
            Task.FromResult(Events.TryGetValue(eventId, out var item) && !item.Deleted ? item : null);
        public Task<IReadOnlyList<CalendarRemoteEvent>> InstancesAsync(string accessToken, string calendarId, string eventId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CalendarRemoteEvent>>(Instances.Where(x => x.SeriesId == eventId).ToArray());
        public Task<CalendarRemoteEvent> WriteAsync(string accessToken, string calendarId, string eventId, CalendarSnapshot snapshot, string? etag, CalendarReminderInput? reminders, CancellationToken ct)
        {
            if (Events.TryGetValue(eventId, out var old) && etag is null) throw new CalendarProviderException(409, "duplicate");
            if (old is not null && old.Etag != etag) throw new CalendarProviderException(412, "conditionNotMet");
            var result = new CalendarRemoteEvent(eventId, Guid.NewGuid().ToString("N"), snapshot, false); Events[eventId] = result; ReminderWrites.Add(reminders);
            if (FailAfterWriteOnce) { FailAfterWriteOnce = false; throw new HttpRequestException("Test ambiguous timeout"); }
            return Task.FromResult(result);
        }
        public Task DeleteAsync(string accessToken, string calendarId, string eventId, string etag, CancellationToken ct)
        { Events[eventId] = Events[eventId] with { Deleted = true, Snapshot = null, Etag = "deleted" }; return Task.CompletedTask; }
        public Task<CalendarWatchResult> WatchAsync(string accessToken, string calendarId, string channelId, string token, CancellationToken ct)
        { WatchCalls++; WatchToken = token; return Task.FromResult(new CalendarWatchResult("resource", Clock.GetUtcNow().AddDays(7))); }
        public Task StopWatchAsync(string accessToken, string channelId, string resourceId, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(string refreshToken, CancellationToken ct) => Task.CompletedTask;
    }
}
