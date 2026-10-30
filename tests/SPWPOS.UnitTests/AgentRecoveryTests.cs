using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed partial class PlanningAgentTests
{
    [Theory]
    [InlineData("AGENT_AI_TIMEOUT", true)]
    [InlineData("AGENT_AI_UNAVAILABLE", true)]
    [InlineData("AGENT_AI_QUOTA_EXCEEDED", false)]
    [InlineData("AGENT_AI_AUTH_FAILED", false)]
    [InlineData("AGENT_AI_INVALID_RESPONSE", false)]
    public async Task Recovery_transient_interpretation_failure_can_complete_without_duplicate_calendar_writes(string code, bool recoverable)
    {
        var first = true;
        var ai = new AgentAiTestDouble { Response = now => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Report", now.AddHours(10), 60, 2, "WRITING", true,
                DurationEvidence: "60 phút", DeadlineEvidence: "hạn chót 18:00 hôm nay")]
        }, BeforeReply = () => {
            if (first) { first = false; throw new AuthException(code, "Synthetic provider failure", 503); }
            return Task.CompletedTask;
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var input = Request("Report cần 60 phút, hạn chót 18:00 hôm nay.");
        if (recoverable)
        {
            var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, input, Ct));
            Assert.Equal("APPLIED", result.Status);
            var replay = await Agent(h, s => s.SendAgentTurnAsync(user.Id, input, Ct));
            Assert.Equal(result.Id, replay.Id);
            Assert.Single(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
            var session = Assert.Single(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
            Assert.Equal(60, (session.End - session.Start).TotalMinutes);
        }
        else
        {
            var error = await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, input, Ct)));
            Assert.Equal(code, error.Code);
            Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
            Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        }
    }

    [Fact]
    public async Task Recovery_persistent_timeout_finishes_as_a_failure_without_calendar_changes()
    {
        var ai = new AgentAiTestDouble { BeforeReply = () => throw new AuthException("AGENT_AI_TIMEOUT", "Synthetic timeout", 503) };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var error = await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, Request("Tạo Report 60 phút."), Ct)));
        Assert.Equal("AGENT_AI_TIMEOUT", error.Code);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Fact]
    public async Task Recovery_never_restarts_a_stream_after_public_reply_text_was_sent()
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var first = true;
        ai.StreamResponse = async (delta, token) => {
            if (first) { first = false; await delta("Đang xem yêu cầu…", token);
                throw new AuthException("AGENT_AI_TIMEOUT", "Synthetic interrupted stream", 503); }
            return Report(h.Clock.GetUtcNow(), 60);
        };
        var events = new List<SPWPOS.Application.Workspace.AgentStreamEvent>();
        var error = await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.SendAgentTurnStreamingAsync(user.Id, Request(),
            (item, _) => { events.Add(item); return Task.CompletedTask; }, Ct)));
        Assert.Equal("AGENT_AI_TIMEOUT", error.Code);
        Assert.Contains(events, item => !string.IsNullOrEmpty(item.Delta));
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Fact]
    public async Task Recovery_user_cancellation_is_preserved_and_cannot_save_a_schedule()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var ai = new AgentAiTestDouble { BeforeReply = () => {
            cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token);
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(), cancellation.Token)));
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }
}
