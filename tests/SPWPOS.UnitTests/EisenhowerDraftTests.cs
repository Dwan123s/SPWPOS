using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class EisenhowerDraftTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Board<T>(ServiceHarness h, Func<IEisenhowerService, Task<T>> action) {
        await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IEisenhowerService>());
    }
    private static Task Board(ServiceHarness h, Func<IEisenhowerService, Task> action) => Board(h, async service => { await action(service); return true; });
    private static async Task<T> Notes<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action) {
        await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());
    }
    [Fact] public async Task Undated_quick_capture_is_an_Inbox_draft_and_never_invents_scheduling_fields()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        var input = new MatrixDraftInput("Ý tưởng", "Một nhiệm vụ tạm thời", Quadrant: "Q2", RequestId: Guid.NewGuid());
        var id = await Board(h, s => s.SaveMatrixDraftAsync(user.Id, null, input, Ct));
        Assert.Equal(id, await Board(h, s => s.SaveMatrixDraftAsync(user.Id, null, input, Ct)));
        var row = Assert.Single((await Board(h, s => s.GetEisenhowerAsync(user.Id, Ct))).Tasks);
        Assert.Equal(id, row.Id); Assert.Equal("Draft", row.Kind); Assert.Equal("Q2", row.Quadrant); Assert.Null(row.Deadline);
        Assert.Equal(0, row.EstimatedMinutes); Assert.Equal(0, row.RemainingMinutes); Assert.Equal("Một nhiệm vụ tạm thời", row.Description);
        var note = Assert.Single(await Notes(h, s => s.GetNotesAsync(user.Id, Ct)));
        Assert.Equal(id, note.Id); Assert.False(note.AnalysisImported); Assert.Empty(note.CreatedTaskIds);
        Assert.False(await h.DbAsync(db => db.WorkTasks.AnyAsync(Ct))); Assert.False(await h.DbAsync(db => db.WorkSessions.AnyAsync(Ct)));
    }
    [Fact] public async Task Draft_edit_and_move_keep_optional_deadline_and_reject_stale_or_cross_account_writes()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        var id = await Board(h, s => s.SaveMatrixDraftAsync(user.Id, null, new("Cần nhớ", RequestId: Guid.NewGuid()), Ct));
        var view = await Board(h, s => s.GetEisenhowerAsync(user.Id, Ct)); var row = Assert.Single(view.Tasks);
        var deadline = h.Clock.GetUtcNow().AddDays(3);
        await Board(h, s => s.SaveMatrixDraftAsync(user.Id, id, new("Đã chỉnh", "Mô tả mới", deadline, "Q4", row.Version), Ct));
        await Assert.ThrowsAsync<AuthException>(() => Board(h, s => s.SaveMatrixDraftAsync(user.Id, id, new("Cũ", Version: row.Version), Ct)));
        view = await Board(h, s => s.GetEisenhowerAsync(user.Id, Ct)); row = Assert.Single(view.Tasks);
        Assert.Equal(deadline, row.Deadline); Assert.Equal("Đã chỉnh", row.Name);
        await Board(h, s => s.MoveMatrixDraftAsync(user.Id, id, new(row.Version, view.Revision, "Q1"), Ct));
        row = Assert.Single((await Board(h, s => s.GetEisenhowerAsync(user.Id, Ct))).Tasks);
        Assert.Equal("Q1", row.Quadrant); Assert.Equal(deadline, row.Deadline);
        var other = Guid.NewGuid(); await h.DbAsync(async db => { db.Users.Add(new() { Id = other, UserName = "another", Email = "another@example.com", NormalizedEmail = "ANOTHER@EXAMPLE.COM", IsActive = true, EmailConfirmed = true }); await db.SaveChangesAsync(Ct); });
        await Assert.ThrowsAsync<AuthException>(() => Board(h, s => s.SaveMatrixDraftAsync(other, id, new("Không được sửa", Version: row.Version), Ct)));
        Assert.Empty((await Board(h, s => s.GetEisenhowerAsync(other, Ct))).Tasks);
    }
    [Fact] public async Task Notes_edits_preserve_matrix_metadata_and_processed_drafts_leave_the_matrix()
    {
        await using var h = await ServiceHarness.CreateAsync(services => services.AddSingleton<ITaskInterpreter>(new NoteAiTestDouble())); var user = await h.RegisterAsync();
        var id = await Board(h, s => s.SaveMatrixDraftAsync(user.Id, null, new("Ý tưởng", Quadrant: "Q2", RequestId: Guid.NewGuid()), Ct));
        var note = Assert.Single(await Notes(h, s => s.GetNotesAsync(user.Id, Ct)));
        note = await Notes(h, s => s.SaveNoteAsync(user.Id, id, new("Ý tưởng", "Bổ sung điều kiện", note.Version), Ct));
        Assert.Equal("Q2", Assert.Single((await Board(h, s => s.GetEisenhowerAsync(user.Id, Ct))).Tasks).Quadrant);
        note = await Notes(h, s => s.AnalyzeNoteAsync(user.Id, id, new(note.Version), Ct));
        await Notes(h, s => s.ImportNoteAsync(user.Id, id, new(note.Version, note.Analysis!.Tasks), Ct));
        var row = Assert.Single((await Board(h, s => s.GetEisenhowerAsync(user.Id, Ct))).Tasks);
        Assert.Equal("Personal", row.Kind); Assert.Equal("Q2", row.Quadrant); Assert.NotEqual(id, row.Id);
    }
}
