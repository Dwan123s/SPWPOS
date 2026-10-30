using System.Text.Json.Nodes;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport.NoraSandbox;

namespace SPWPOS.UnitTests;

public sealed class ConversationActionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static string Root()
    {
        for(var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if(File.Exists(Path.Combine(d.FullName, "SPWPOS.sln"))) return d.FullName;
        throw new InvalidOperationException("Repository not found.");
    }
    private static ConversationTurn Action(string name) => new("harness", null, name,
        new JsonObject { ["target"] = "user_turn:1" }, new("PROBE", ["Verify persisted state."]));

    [Fact]
    public async Task Lifecycle_adapter_applies_a_real_preview_idempotently_then_restores_the_calendar()
    {
        await using var env = await SandboxEnvironment.CreateAsync("B", SandboxEnvironment.LoadConfiguration(Root(), false), false, new(5), Ct);
        env.Recorder.Script = now => new("Preview", "", false, [], [], [],
            [new(null, "Demo", now.AddDays(1), 30, 2, "OTHER", true)], [], false, 14);
        var input = new AgentTurnInput(env.ConversationId, Guid.NewGuid(), "Xem trước Demo 30 phút, chưa lưu.", false);
        var preview = await env.Agent(s => s.SendAgentTurnAsync(env.UserId, input, Ct));
        Assert.Equal("PREVIEW", preview.Status);
        var actions = new ConversationActions(); actions.Capture(input, preview);
        var applied = await actions.ExecuteAsync(env, Action("apply_turn"), Ct);
        Assert.Equal("APPLIED", applied.Status);
        await actions.ExecuteAsync(env, Action("apply_turn"), Ct);
        var replayAction = new ConversationTurn("harness", null, "resend_request", new JsonObject {
            ["source"] = "user_turn:1", ["same_request_id"] = true }, new("APPLIED", ["Same persisted result."]));
        var replay = await actions.ExecuteAsync(env, replayAction, Ct);
        Assert.Equal(applied.Id, replay.Id);
        replayAction.Params!["replace_text"] = "Thay đổi nội dung request cũ.";
        var reused = await Assert.ThrowsAsync<SPWPOS.Application.Authentication.AuthException>(() => actions.ExecuteAsync(env, replayAction, Ct));
        Assert.Equal("AGENT_REQUEST_REUSED", reused.Code);
        var saved = await env.Snapshot(Ct);
        Assert.Single(saved.Tasks, t => !t.IsDeleted); Assert.Single(saved.Sessions, s => s.Status == SPWPOS.Domain.Entities.WorkSessionStatus.Scheduled);
        await actions.ExecuteAsync(env, Action("undo_turn"), Ct);
        await actions.ExecuteAsync(env, Action("undo_turn"), Ct);
        var restored = await env.Snapshot(Ct);
        Assert.DoesNotContain(restored.Tasks, t => !t.IsDeleted);
        Assert.DoesNotContain(restored.Sessions, s => s.Status == SPWPOS.Domain.Entities.WorkSessionStatus.Scheduled);
    }

    [Theory]
    [InlineData("archive_current_conversation", "CONVERSATION_ARCHIVED")]
    [InlineData("delete_current_conversation", "CONVERSATION_DELETED")]
    public async Task Lifecycle_adapter_blocks_new_turns_in_archived_or_deleted_conversations(string action, string expectedCode)
    {
        await using var env = await SandboxEnvironment.CreateAsync("B", SandboxEnvironment.LoadConfiguration(Root(), false), false, new(5), Ct);
        var before = await env.Snapshot(Ct);
        var actions = new ConversationActions(); await actions.ExecuteAsync(env, Action(action), Ct);
        env.Recorder.Script = _ => throw new InvalidOperationException("Lifecycle rejection must happen before interpretation.");
        var error = await Assert.ThrowsAsync<SPWPOS.Application.Authentication.AuthException>(() => env.Agent(s => s.SendAgentTurnAsync(env.UserId,
            new(env.ConversationId, Guid.NewGuid(), "Xếp Demo 30 phút."), Ct)));
        Assert.Equal(expectedCode, error.Code);
        Assert.Empty(SandboxOracle.Diff(before, await env.Snapshot(Ct)));
    }

    [Theory] [InlineData(false, "AGENT_STALE")] [InlineData(true, "AGENT_PREVIEW_EXPIRED")]
    public async Task Lifecycle_adapter_rejects_stale_or_expired_previews_without_saving_the_proposed_task(bool expired, string expectedCode)
    {
        await using var env = await SandboxEnvironment.CreateAsync("B", SandboxEnvironment.LoadConfiguration(Root(), false), false, new(5), Ct);
        env.Recorder.Script = now => new("Preview", "", false, [], [], [],
            [new(null, "Demo", now.AddDays(1), 30, 2, "OTHER", true)], [], false, 14);
        var input = new AgentTurnInput(env.ConversationId, Guid.NewGuid(), "Preview Demo 30 phút, chưa lưu.", false);
        var preview = await env.Agent(s => s.SendAgentTurnAsync(env.UserId, input, Ct));
        var actions = new ConversationActions(); actions.Capture(input, preview);
        var action = expired ? new ConversationTurn("harness", null, "advance_clock", new JsonObject { ["minutes"] = 31 }, new("NOOP", ["Clock only."]))
            : new ConversationTurn("harness", null, "mutate_calendar_outside_conversation", new JsonObject { ["add_event"] = new JsonObject {
                ["ref"] = "external", ["name"] = "External event", ["start"] = "2026-10-27T20:00:00+07:00", ["end"] = "2026-10-27T21:00:00+07:00", ["category"] = "MEETING"
            } }, new("APPLIED", ["Real service revision change."]));
        await actions.ExecuteAsync(env, action, Ct);
        var before = await env.Snapshot(Ct);
        var error = await Assert.ThrowsAsync<SPWPOS.Application.Authentication.AuthException>(() => actions.ExecuteAsync(env, Action("apply_turn"), Ct));
        Assert.Equal(expectedCode, error.Code);
        Assert.Empty(SandboxOracle.Diff(before, await env.Snapshot(Ct)));
    }
}
