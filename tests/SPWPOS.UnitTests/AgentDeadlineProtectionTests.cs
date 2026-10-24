using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class AgentFreeTimeTests
{
    [Theory] [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public async Task Nora_checks_existing_urgent_deadlines_before_automatically_saving_a_new_task(bool streaming, bool insufficient)
    {
        var end = insufficient ? 1200 : 1260;
        var evidence = $"từ 19:00 đến {end / 60}:00 ngày 22/10/2026";
        var deadlineEvidence = $"hạn chót {end / 60}:00 ngày 22/10/2026";
        var ai = Ai(); ai.Response = _ => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Báo cáo mới", At(end), 60, 2, "WRITING", false, At(1140), At(end),
                "60 phút", deadlineEvidence, evidence)] };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var old = new WorkTask { UserId = user.Id, Name = "Việc khẩn cũ", Deadline = At(1200),
            EstimatedMinutes = 60, Priority = 4, Splittable = false, IsHardDeadline = true };
        await h.DbAsync(async db => { db.WorkTasks.Add(old); await db.SaveChangesAsync(Ct); });
        var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), $"Báo cáo mới 60 phút, làm {evidence}, {deadlineEvidence}.", true);
        var result = await UseAgent(h, service => streaming
            ? service.SendAgentTurnStreamingAsync(user.Id, input, (_, _) => Task.CompletedTask, Ct)
            : service.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal(insufficient ? "BLOCKED" : "PREVIEW", result.Status);
        Assert.NotNull(result.Plan); Assert.True(result.Plan.HasDeadlineProtection);
        Assert.Equal(At(1140), Assert.Single(result.Plan.Sessions, session => session.TaskId == old.Id).Start);
        Assert.Equal(1, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
        var retried = await UseAgent(h, service => service.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal(result.Status, retried.Status);
        if (insufficient) { Assert.NotEmpty(result.Plan.Warnings); return; }
        Assert.Empty(result.Plan.Warnings); Assert.Contains("Xác nhận kế hoạch", result.Reply);
        var saved = await UseAgent(h, service => service.ApplyAgentTurnAsync(user.Id, result.Id, Ct));
        Assert.Equal("APPLIED", saved.Status);
        var sessions = await h.DbAsync(db => db.WorkSessions.Where(x => x.Status == WorkSessionStatus.Scheduled).OrderBy(x => x.Start).ToArrayAsync(Ct));
        Assert.Equal(2, sessions.Length); Assert.Equal(old.Id, sessions[0].TaskId);
        Assert.Equal(At(1200), sessions[1].Start);
        await UseAgent(h, service => service.UndoAgentTurnAsync(user.Id, saved.Id, Ct));
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(x => x.Status == WorkSessionStatus.Scheduled, Ct)));
        Assert.False((await h.DbAsync(db => db.WorkTasks.SingleAsync(x => x.Id == old.Id, Ct))).IsDeleted);
    }
}
