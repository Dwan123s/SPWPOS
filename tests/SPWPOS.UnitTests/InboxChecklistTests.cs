using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed class InboxChecklistTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Work<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action)
    {
        await using var scope = h.Provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());
    }

    [Fact]
    public async Task Completion_survives_reload_and_edit_and_can_be_reopened_without_creating_tasks()
    {
        await using var h = await ServiceHarness.CreateAsync();
        var user = await h.RegisterAsync();
        var note = await Work(h, s => s.SaveNoteAsync(user.Id, null, new("Mua sách", "Mua sách"), Ct));
        Assert.False(note.InboxCompleted);
        var completed = await Work(h, s => s.SetInboxCompletionAsync(user.Id, note.Id, new(note.Version, true), Ct));
        Assert.True(completed.InboxCompleted);
        Assert.Equal(h.Clock.GetUtcNow(), completed.InboxCompletedAt);
        Assert.NotEqual(note.Version, completed.Version);
        Assert.True(Assert.Single(await Work(h, s => s.GetNotesAsync(user.Id, Ct))).InboxCompleted);
        var same = await Work(h, s => s.SetInboxCompletionAsync(user.Id, note.Id, new(completed.Version, true), Ct));
        Assert.Equal(completed.Version, same.Version);
        var edited = await Work(h, s => s.SaveNoteAsync(user.Id, note.Id, new("Mua sách", "Mua sách cho tuần tới", completed.Version), Ct));
        Assert.True(edited.InboxCompleted);
        var reopened = await Work(h, s => s.SetInboxCompletionAsync(user.Id, note.Id, new(edited.Version, false), Ct));
        Assert.False(reopened.InboxCompleted);
        Assert.Null(reopened.InboxCompletedAt);
        Assert.Equal(edited.Content, reopened.Content);
        Assert.False(await h.DbAsync(db => db.WorkTasks.AnyAsync(Ct)));
        Assert.False(await h.DbAsync(db => db.WorkSessions.AnyAsync(Ct)));
    }

    [Fact]
    public async Task Stale_and_cross_account_completion_requests_cannot_change_the_item()
    {
        await using var h = await ServiceHarness.CreateAsync();
        var user = await h.RegisterAsync();
        var note = await Work(h, s => s.SaveNoteAsync(user.Id, null, new("Việc riêng", "Việc riêng"), Ct));
        await Work(h, s => s.SetInboxCompletionAsync(user.Id, note.Id, new(note.Version, true), Ct));
        var stale = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.SetInboxCompletionAsync(user.Id, note.Id, new(note.Version, false), Ct)));
        Assert.Equal("NOTE_STALE", stale.Code);
        var otherId = Guid.NewGuid();
        await h.DbAsync(async db =>
        {
            db.Users.Add(new() { Id = otherId, UserName = "another", Email = "another@example.test", NormalizedEmail = "ANOTHER@EXAMPLE.TEST", IsActive = true, EmailConfirmed = true });
            await db.SaveChangesAsync(Ct);
        });
        var other = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.SetInboxCompletionAsync(otherId, note.Id, new(note.Version, false), Ct)));
        Assert.Equal("NOT_FOUND", other.Code);
        Assert.True(Assert.Single(await Work(h, s => s.GetNotesAsync(user.Id, Ct))).InboxCompleted);
    }

    [Fact]
    public async Task Completed_matrix_drafts_leave_the_board_and_return_to_their_quadrant_when_reopened()
    {
        await using var h = await ServiceHarness.CreateAsync();
        var user = await h.RegisterAsync();
        await using var scope = h.Provider.CreateAsyncScope();
        var board = scope.ServiceProvider.GetRequiredService<IEisenhowerService>();
        var id = await board.SaveMatrixDraftAsync(user.Id, null, new("Ý tưởng", Quadrant: "Q2", RequestId: Guid.NewGuid()), Ct);
        var note = Assert.Single(await Work(h, s => s.GetNotesAsync(user.Id, Ct)));
        var completed = await Work(h, s => s.SetInboxCompletionAsync(user.Id, id, new(note.Version, true), Ct));
        Assert.Empty((await board.GetEisenhowerAsync(user.Id, Ct)).Tasks);
        await Work(h, s => s.SetInboxCompletionAsync(user.Id, id, new(completed.Version, false), Ct));
        Assert.Equal("Q2", Assert.Single((await board.GetEisenhowerAsync(user.Id, Ct)).Tasks).Quadrant);
    }
}
