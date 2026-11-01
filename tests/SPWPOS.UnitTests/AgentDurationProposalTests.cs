using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class AgentFreeTimeTests
{
    private static AgentIntent DurationSuggestion => AgentAiTestDouble.Empty() with {
        Reply = "Mình đề xuất ước lượng 60 phút cho Bài tập, dựa trên mô tả hiện có; đây là ước lượng cần bạn xác nhận.",
        Question = "Bạn đồng ý với ước lượng này không?", Options = ["Đồng ý", "Đổi thời lượng"] };
    private static AgentIntent EstimatedTask(int minutes = 60, string name = "Bài tập") => AgentAiTestDouble.Empty() with {
        Tasks = [new(null, name, At(1320), minutes, 2, "STUDY", true,
            DurationEvidence: $"{minutes} phút", DeadlineEvidence: "hạn chót 22:00 ngày mai")] };

    [Theory] [InlineData("Đồng ý")] [InlineData("Xác nhận 60 phút")]
    public async Task An_AI_estimate_is_saved_only_after_explicit_chat_acceptance(string confirmation)
    {
        var ai = new AgentAiTestDouble { Response = _ => DurationSuggestion };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var conversation = Guid.NewGuid();
        var question = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Xếp Bài tập, hạn chót 22:00 ngày mai. Tôi chưa biết cần bao lâu."), Ct));
        Assert.Equal("NEEDS_INPUT", question.Status); Assert.Empty(question.Changes);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        var command = await h.DbAsync(db => db.AgentTurns.Where(x => x.Id == question.Id).Select(x => x.CommandJson).SingleAsync(Ct));
        using var json = JsonDocument.Parse(command);
        Assert.Equal(60, json.RootElement.GetProperty("pending").GetProperty("proposedMinutes").GetInt32());
        Assert.False(json.RootElement.GetProperty("pending").GetProperty("durationProposalAccepted").GetBoolean());
        ai.Response = _ => EstimatedTask();
        var applied = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), confirmation), Ct));
        Assert.Equal("APPLIED", applied.Status);
        Assert.Equal(60, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).EstimatedMinutes);
        using var context = JsonDocument.Parse(ai.Context);
        Assert.True(context.RootElement.GetProperty("pendingRequest").GetProperty("durationProposalAccepted").GetBoolean());
    }

    [Theory] [InlineData("Không đồng ý", 60, "Bài tập")]
    [InlineData("Đổi thời lượng", 60, "Bài tập")] [InlineData("Xem lịch thôi", 60, "Bài tập")]
    [InlineData("Đồng ý", 90, "Bài tập")] [InlineData("Đồng ý", 60, "Việc khác")]
    public async Task Unaccepted_or_changed_estimates_cannot_be_used_to_create_work(string reply, int minutes, string name)
    {
        var ai = new AgentAiTestDouble { Response = _ => DurationSuggestion };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var conversation = Guid.NewGuid();
        await UseAgent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Xếp Bài tập, hạn chót 22:00 ngày mai."), Ct));
        ai.Response = _ => EstimatedTask(minutes, name);
        var result = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), reply), Ct));
        Assert.Equal("NEEDS_INPUT", result.Status);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Fact]
    public async Task A_revised_AI_estimate_replaces_the_previous_unaccepted_proposal()
    {
        var ai = new AgentAiTestDouble { Response = _ => DurationSuggestion };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var conversation = Guid.NewGuid();
        await UseAgent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Xếp Bài tập, hạn chót 22:00 ngày mai."), Ct));
        ai.Response = _ => DurationSuggestion with { Reply = "Mình đề xuất ước lượng 30 phút cho Bài tập nếu chỉ làm phần cơ bản." };
        await UseAgent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Không đồng ý, hãy đề xuất cho phần cơ bản thôi."), Ct));
        ai.Response = _ => EstimatedTask(30);
        var result = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Đồng ý"), Ct));
        Assert.Equal("APPLIED", result.Status);
        Assert.Equal(30, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).EstimatedMinutes);
    }
}
