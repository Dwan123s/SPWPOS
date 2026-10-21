using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class AgentFreeTimeTests
{
    private static async Task<T> UseAgent<T>(ServiceHarness h, Func<IPlanningAgentService, Task<T>> action)
    {
        await using var scope = h.Provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IPlanningAgentService>());
    }

    [Theory] [InlineData(true, true)] [InlineData(false, true)] [InlineData(true, false)]
    public async Task Nora_previews_urgent_displacement_then_applies_and_undoes_it_without_changing_the_old_deadline(bool nearDeadline, bool autoApply)
    {
        var ai = Ai(); ai.Response = _ => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Báo cáo", At(1200), 60, 4, "WRITING", false, At(1140), At(1200),
                "60 phút", "hạn chót 20:00 ngày 22/10/2026", "từ 19:00 đến 20:00 ngày 22/10/2026")] };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var due = nearDeadline ? At(1320) : At(1320).AddDays(7);
        var old = new WorkTask { UserId = user.Id, Name = "Việc cũ", Deadline = due, EstimatedMinutes = 60, IsHardDeadline = true };
        var unrelated = new WorkTask { UserId = user.Id, Name = "Không liên quan", Deadline = At(1320).AddDays(7), EstimatedMinutes = 60, IsHardDeadline = true };
        var original = new WorkSession { UserId = user.Id, TaskId = old.Id, Start = At(1140), End = At(1200) };
        var kept = new WorkSession { UserId = user.Id, TaskId = unrelated.Id, Start = At(1260), End = At(1320) };
        await h.DbAsync(async db => {
            db.WeeklyAvailability.RemoveRange(await db.WeeklyAvailability.ToArrayAsync(Ct));
            db.WeeklyAvailability.AddRange(Enumerable.Range(0, 7).Select(day => new WeeklyAvailability { UserId = user.Id, DayOfWeek = day, StartMinute = 1140, EndMinute = 1320 }));
            db.WorkTasks.AddRange(old, unrelated); db.WorkSessions.AddRange(original, kept); await db.SaveChangesAsync(Ct);
        });
        var request = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(),
            "Báo cáo gấp 60 phút, làm từ 19:00 đến 20:00 ngày 22/10/2026, hạn chót 20:00 ngày 22/10/2026. Xếp lại giúp tôi.", autoApply);
        var preview = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id, request, Ct));
        Assert.Equal("PREVIEW", preview.Status); Assert.NotNull(preview.Plan); Assert.Empty(preview.Plan.Warnings);
        Assert.Contains("Xác nhận dời lịch", preview.Reply);
        var retried = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id, request, Ct));
        Assert.Equal("PREVIEW", retried.Status); Assert.Equal(preview.Id, retried.Id);
        Assert.Contains(preview.Changes, x => x.Kind == "SESSION" && x.TaskId == old.Id && x.Before.Length > 0 && x.After.Length > 0);
        Assert.Equal(2, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(WorkSessionStatus.Scheduled, (await h.DbAsync(db => db.WorkSessions.SingleAsync(x => x.Id == original.Id, Ct))).Status);
        var applied = await UseAgent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct));
        Assert.Equal("APPLIED", applied.Status);
        var savedUrgent = await h.DbAsync(db => db.WorkTasks.SingleAsync(x => x.Name == "Báo cáo", Ct));
        Assert.Equal(4, savedUrgent.Priority);
        var moved = await h.DbAsync(db => db.WorkSessions.SingleAsync(x => x.TaskId == old.Id && x.Status == WorkSessionStatus.Scheduled, Ct));
        Assert.Equal(nearDeadline ? At(1200) : At(1140).AddDays(1), moved.Start);
        Assert.Equal(due, (await h.DbAsync(db => db.WorkTasks.SingleAsync(x => x.Id == old.Id, Ct))).Deadline);
        Assert.Equal(WorkSessionStatus.Scheduled, (await h.DbAsync(db => db.WorkSessions.SingleAsync(x => x.Id == kept.Id, Ct))).Status);
        await UseAgent(h, s => s.UndoAgentTurnAsync(user.Id, applied.Id, Ct));
        Assert.Equal(WorkSessionStatus.Scheduled, (await h.DbAsync(db => db.WorkSessions.SingleAsync(x => x.Id == original.Id, Ct))).Status);
        Assert.Equal(due, (await h.DbAsync(db => db.WorkTasks.SingleAsync(x => x.Id == old.Id, Ct))).Deadline);
    }

    [Fact]
    public async Task The_word_gap_with_a_different_accent_does_not_make_a_customer_meeting_task_urgent()
    {
        var ai = Ai(); ai.Response = _ => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Chuẩn bị gặp khách", At(1200), 60, 2, "WORK", false)] };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var preview = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(),
            "Chuẩn bị gặp khách 60 phút, hạn chót 20:00 ngày 22/10/2026.", false), Ct));
        Assert.Equal("PREVIEW", preview.Status);
        await UseAgent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct));
        Assert.Equal(2, await h.DbAsync(db => db.WorkTasks.Select(x => x.Priority).SingleAsync(Ct)));
    }
}
