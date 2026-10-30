using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class NoteChecklistTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Call<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    private static async Task<(ServiceHarness Harness, Guid UserId, NoteView Note, TaskView Task)> Setup()
    {
        var h = await ServiceHarness.CreateAsync(services => services.AddSingleton<ITaskInterpreter>(new NoteAiTestDouble()));
        var user = await h.RegisterAsync();
        var note = await Call(h, s => s.SaveNoteAsync(user.Id, null, new("Kế hoạch", "Viết báo cáo 60 phút"), Ct));
        note = await Call(h, s => s.AnalyzeNoteAsync(user.Id, note.Id, new(note.Version), Ct));
        note = await Call(h, s => s.ImportNoteAsync(user.Id, note.Id, new(note.Version, note.Analysis!.Tasks), Ct));
        var workspace = await Call(h, s => s.GetAsync(user.Id, Ct));
        return (h, user.Id, note, Assert.Single(workspace.Tasks));
    }

    [Fact] public async Task Checking_and_reopening_a_note_task_syncs_calendar_without_rewriting_the_note_or_actual_history()
    {
        var (h, uid, note, task) = await Setup(); await using var lifetime = h;
        var now = h.Clock.GetUtcNow();
        var past = new WorkSession { UserId = uid, TaskId = task.Id, Start = now.AddHours(-1), End = now.AddMinutes(-30),
            Status = WorkSessionStatus.Completed, ActualSeconds = 1800 };
        var future = new WorkSession { UserId = uid, TaskId = task.Id, Start = now.AddDays(1), End = now.AddDays(1).AddHours(1), IsLocked = true };
        await h.DbAsync(async db => { db.WorkSessions.AddRange(past, future); await db.SaveChangesAsync(Ct); });
        var draft = await Call(h, s => s.SaveNoteAsync(uid, note.Id, new(note.Title, note.Content + "\nÝ đang viết thêm", note.Version), Ct));
        var checkedWorkspace = await Call(h, s => s.SetNoteTaskCompletionAsync(uid, note.Id, task.Id, new(task.Version, true), Ct));
        var done = Assert.Single(checkedWorkspace.Tasks);
        Assert.Equal("Completed", done.Status); Assert.Equal(100, done.Progress);
        Assert.NotNull(done.CompletedAt); Assert.NotEqual(task.Version, done.Version);
        Assert.Equal(task.EstimatedMinutes, done.EstimatedMinutes); Assert.Equal(task.Deadline, done.Deadline);
        Assert.Equal(30, done.ActualMinutes);
        Assert.Equal(past.Id, Assert.Single(checkedWorkspace.Sessions).Id);
        Assert.Equal(WorkSessionStatus.Cancelled, await h.DbAsync(db => db.WorkSessions.Where(x => x.Id == future.Id).Select(x => x.Status).SingleAsync(Ct)));
        h.Clock.Advance(TimeSpan.FromMinutes(5));
        var repeated = await Call(h, s => s.SetNoteTaskCompletionAsync(uid, note.Id, task.Id, new(done.Version, true), Ct));
        Assert.Equal(done.CompletedAt, Assert.Single(repeated.Tasks).CompletedAt);
        Assert.Equal(done.Version, Assert.Single(repeated.Tasks).Version);
        var reopened = await Call(h, s => s.SetNoteTaskCompletionAsync(uid, note.Id, task.Id, new(done.Version, false), Ct));
        var todo = Assert.Single(reopened.Tasks);
        Assert.Equal("Todo", todo.Status); Assert.Equal(0, todo.Progress); Assert.Null(todo.CompletedAt);
        Assert.Equal(30, todo.ActualMinutes); Assert.Equal(past.Id, Assert.Single(reopened.Sessions).Id);
        var afterNote = Assert.Single(await Call(h, s => s.GetNotesAsync(uid, Ct)));
        Assert.Equal(draft.Version, afterNote.Version); Assert.Equal(draft.Content, afterNote.Content);
        Assert.Equal(note.CreatedTaskIds, afterNote.CreatedTaskIds);
        Assert.Equal(2, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
    }

    [Fact] public async Task Stale_checkbox_cannot_override_a_task_changed_elsewhere()
    {
        var (h, uid, note, task) = await Setup(); await using var lifetime = h;
        await Call(h, async s => { await s.SetStatusAsync(uid, task.Id, new("InProgress", 45), Ct); return true; });
        var error = await Assert.ThrowsAsync<AuthException>(() => Call(h,
            s => s.SetNoteTaskCompletionAsync(uid, note.Id, task.Id, new(task.Version, true), Ct)));
        Assert.Equal("TASK_STALE", error.Code);
        var current = Assert.Single((await Call(h, s => s.GetAsync(uid, Ct))).Tasks);
        Assert.Equal("InProgress", current.Status); Assert.Equal(45, current.Progress);
    }

    [Fact] public async Task A_task_from_another_note_cannot_be_ticked_through_this_note()
    {
        var (h, uid, note, task) = await Setup(); await using var lifetime = h;
        var other = await Call(h, s => s.SaveNoteAsync(uid, null, new("Ghi chú khác", "Nội dung khác"), Ct));
        var error = await Assert.ThrowsAsync<AuthException>(() => Call(h,
            s => s.SetNoteTaskCompletionAsync(uid, other.Id, task.Id, new(task.Version, true), Ct)));
        Assert.Equal("NOT_FOUND", error.Code);
        Assert.Equal("Todo", Assert.Single((await Call(h, s => s.GetAsync(uid, Ct))).Tasks).Status);
    }

    [Theory] [InlineData(WorkSessionStatus.Running)] [InlineData(WorkSessionStatus.Paused)]
    public async Task A_running_or_paused_session_is_not_silently_finished_by_a_checkbox(WorkSessionStatus status)
    {
        var (h, uid, note, task) = await Setup(); await using var lifetime = h;
        await h.DbAsync(async db => { db.WorkSessions.Add(new() { UserId = uid, TaskId = task.Id,
            Start = h.Clock.GetUtcNow(), End = h.Clock.GetUtcNow().AddHours(1), Status = status, ActualSeconds = 600 }); await db.SaveChangesAsync(Ct); });
        var error = await Assert.ThrowsAsync<AuthException>(() => Call(h,
            s => s.SetNoteTaskCompletionAsync(uid, note.Id, task.Id, new(task.Version, true), Ct)));
        Assert.Equal("SESSION_ACTIVE", error.Code);
        var session = Assert.Single(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        Assert.Equal(status, session.Status); Assert.Equal(600, session.ActualSeconds);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Completion_still_requires_children_and_predecessors_to_be_finished(bool child)
    {
        var (h, uid, note, task) = await Setup(); await using var lifetime = h;
        var other = await Call(h, s => s.SaveTaskAsync(uid, null, new("Công việc cần xong trước", "", task.Deadline, 30, ParentId: child ? task.Id : null), Ct));
        if (!child) await Call(h, async s => { await s.SetDependencyAsync(uid, task.Id, other, false, Ct); return true; });
        var current = (await Call(h, s => s.GetAsync(uid, Ct))).Tasks.Single(x => x.Id == task.Id);
        var error = await Assert.ThrowsAsync<AuthException>(() => Call(h,
            s => s.SetNoteTaskCompletionAsync(uid, note.Id, task.Id, new(current.Version, true), Ct)));
        Assert.Equal(child ? "SUBTASK_INCOMPLETE" : "DEPENDENCY_INCOMPLETE", error.Code);
    }

    [Fact] public async Task Completing_one_session_does_not_complete_the_whole_note_task()
    {
        var (h, uid, note, task) = await Setup(); await using var lifetime = h;
        await h.DbAsync(async db => { db.WorkSessions.Add(new() { UserId = uid, TaskId = task.Id,
            Start = h.Clock.GetUtcNow().AddHours(-1), End = h.Clock.GetUtcNow().AddMinutes(-40), Status = WorkSessionStatus.Completed,
            ActualSeconds = 1200 }); await db.SaveChangesAsync(Ct); });
        var current = Assert.Single((await Call(h, s => s.GetAsync(uid, Ct))).Tasks);
        Assert.Equal("Todo", current.Status); Assert.Equal(20, current.ActualMinutes);
    }
}
