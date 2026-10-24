using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PlanningAgentTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Telex_task_evidence_is_understood_without_rewriting_the_saved_message(bool streaming)
    {
        const string raw = "Tooi muoons vieetss baos caos 45 phuts, xong truowsc 21h ngayf 14/10/2026";
        var ai = new AgentAiTestDouble { Response = now => Report(now, 45) with { Tasks = [new(null, "Viết báo cáo",
            new DateTimeOffset(now.Date.AddHours(21), TimeSpan.Zero), 45, 2, "WRITING", true,
            DurationEvidence: "45 phút", DeadlineEvidence: "xong trước 21h ngày 14/10/2026", WorkWindowEvidence: "")] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var input = Request(raw);
        var result = await Agent(h, s => streaming
            ? s.SendAgentTurnStreamingAsync(user.Id, input, (_, _) => Task.CompletedTask, Ct)
            : s.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal("APPLIED", result.Status); Assert.Equal(raw, result.Text);
        var task = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct)); Assert.Equal(45, task.EstimatedMinutes);
        Assert.Equal(21, task.Deadline.Hour);
        Assert.Equal(raw, await h.DbAsync(db => db.AgentTurns.Where(x => x.Id == result.Id).Select(x => x.Text).SingleAsync(Ct)));
        var retry = await Agent(h, s => s.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal(result.Id, retry.Id); Assert.Equal(1, ai.Calls);
    }

    [Fact]
    public async Task Telex_preview_only_and_negation_cannot_be_lost_during_interpretation()
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now, 45) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            Request("Chir xem truowsc, ddungwf tuwj ddoongj luuw. Vieetss baos caos 45 phuts"), Ct));
        Assert.Equal("PREVIEW", result.Status);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
    }

    [Theory] [InlineData(45)] [InlineData(90)]
    public async Task Telex_support_does_not_accept_absent_or_invented_duration_evidence(int minutes)
    {
        var ai = new AgentAiTestDouble { Response = now => Report(now, minutes) with { Tasks = [new(null, "Viết báo cáo",
            now.AddHours(10), minutes, 2, "WRITING", true, DurationEvidence: minutes + " phút", DeadlineEvidence: "")] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var raw = minutes == 45 ? "Vieetss baos caos ngayf mai" : "Vieetss baos caos 45 phuts ngayf mai";
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(raw), Ct));
        Assert.Equal("NEEDS_INPUT", result.Status);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact]
    public async Task Telex_follow_up_keeps_the_pending_evening_window_and_reads_the_duration()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Viết báo cáo", null, null, 2, "WRITING", true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        const string first = "Tooi muoons vieetss baos caos vaof toois ngayf mai";
        var question = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(first), Ct));
        Assert.Equal("NEEDS_INPUT", question.Status);
        ai.Response = now => Report(now, 45) with { Tasks = [new(null, "Viết báo cáo",
            new DateTimeOffset(now.Date.AddDays(1).AddHours(21), TimeSpan.Zero), 45, 2, "WRITING", true,
            DurationEvidence: "45 phút", DeadlineEvidence: "xong trước 21h", WorkWindowEvidence: "")] };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("45 phuts, xong truowsc 21h"), Ct));
        Assert.Equal("APPLIED", result.Status);
        var task = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct));
        Assert.Equal(h.Clock.GetUtcNow().Date.AddDays(1).AddHours(18), task.WorkWindowStart!.Value.UtcDateTime);
        Assert.Equal(45, task.EstimatedMinutes); Assert.All(result.Plan!.Sessions, x => Assert.True(x.End <= task.Deadline));
    }

    [Fact]
    public async Task Telex_named_task_is_found_before_the_agent_context_is_truncated()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync();
        var target = new SPWPOS.Domain.Entities.WorkTask { UserId = user.Id, Name = "Viết báo cáo", Deadline = h.Clock.GetUtcNow().AddDays(30) };
        await h.DbAsync(async db => {
            db.WorkTasks.Add(target);
            db.WorkTasks.AddRange(Enumerable.Range(0, 120).Select(i => new SPWPOS.Domain.Entities.WorkTask {
                UserId = user.Id, Name = "Work " + i, Deadline = h.Clock.GetUtcNow().AddDays(1) }));
            await db.SaveChangesAsync(Ct);
        });
        await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Tìm vieetss baos caos", false), Ct));
        using var context = JsonDocument.Parse(ai.Context);
        Assert.Contains(context.RootElement.GetProperty("tasks").EnumerateArray(), x => x.GetProperty("id").GetGuid() == target.Id);
    }
}
