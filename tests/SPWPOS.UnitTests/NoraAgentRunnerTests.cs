using System.Text.Json;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Workspace;

namespace SPWPOS.UnitTests;

public sealed class NoraAgentRunnerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);
    private static readonly JsonElement EmptySchema = Json(new { type = "object", additionalProperties = false, required = Array.Empty<string>(), properties = new { } });
    private static NoraToolDefinition Tool(string name) => new(name, name, EmptySchema, name);
    private static NoraModelReply Call(string name, int n = 0, string? providerId = null) => new(Json(new {
        role = "model", parts = new[] { new { functionCall = new { name, args = new { } }, thoughtSignature = "opaque-test-signature" } }
    }), [new(n.ToString(), name, Json(new { }), providerId)]);
    private static Task<NoraRunnerResult> Run(Func<NoraModelRequest, CancellationToken, Task<NoraModelReply>> model,
        Func<NoraToolCall, CancellationToken, Task<NoraToolResult>> execute, NoraAgentOptions? options = null,
        int priorModel = 0, int priorTools = 0) => new NoraAgentRunner().RunAsync("instructions",
            Json(new { role = "user", parts = new[] { new { text = "goal" } } }),
            [Tool("simulate"), Tool("inspect_constraints"), Tool("finish_proposal"), Tool("ask_user")], options ?? new(), priorModel, priorTools,
            model, execute, (_, _, _, _) => Task.CompletedTask, Ct);

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Next_tool_changes_when_real_observation_changes(bool feasible)
    {
        var rounds = 0; var executed = new List<string>();
        var result = await Run((request, _) =>
        {
            if (rounds++ == 0) return Task.FromResult(Call("simulate"));
            Assert.Equal("opaque-test-signature", request.Contents[1].GetProperty("parts")[0].GetProperty("thoughtSignature").GetString());
            var observation = request.Contents[^1].GetProperty("parts")[0].GetProperty("functionResponse");
            Assert.False(observation.TryGetProperty("id", out var ignored));
            var fits = observation.GetProperty("response").GetProperty("data").GetProperty("feasible").GetBoolean();
            return Task.FromResult(Call(fits ? "finish_proposal" : "ask_user", rounds));
        }, (call, _) =>
        {
            executed.Add(call.Name);
            return Task.FromResult(call.Name == "simulate" ? new NoraToolResult("OK", new { feasible }) :
                new("TERMINAL", new NoraTerminal(call.Name == "ask_user" ? "QUESTION" : "PROPOSAL")));
        });
        Assert.Equal(new[] { "simulate", feasible ? "finish_proposal" : "ask_user" }, executed);
        Assert.Equal(feasible ? "PROPOSAL" : "QUESTION", result.Terminal.Kind);
        Assert.Equal(2, result.ModelTurns);
    }

    [Fact] public async Task Repeated_identical_reads_are_cached_but_consume_budget()
    {
        var n = 0; var executions = 0;
        var result = await Run((_, _) => Task.FromResult(Call("inspect_constraints", n++)), (_, _) =>
        { executions++; return Task.FromResult(new NoraToolResult("OK", new { busy = true })); }, new() { MaxModelTurns = 4 });
        Assert.Equal(1, executions); Assert.Equal(4, result.ToolCalls); Assert.Equal("BLOCKED", result.Terminal.Kind);
    }

    [Fact] public async Task Unknown_tools_never_execute_and_stop_after_three_errors()
    {
        var n = 0;
        var result = await Run((_, _) => Task.FromResult(Call("delete_all_events", n++)), (_, _) => throw new InvalidOperationException("Must not execute"));
        Assert.Equal("REPEATED_TOOL_ERRORS", result.Terminal.Reply); Assert.Equal(3, result.ToolCalls);
    }

    [Fact] public async Task A_user_continuation_gets_a_bounded_turn_budget_and_keeps_cumulative_counters()
    {
        var n = 0;
        var result = await Run((_, _) => Task.FromResult(Call(n++ == 0 ? "inspect_constraints" : "ask_user", n)),
            (call, _) => Task.FromResult(call.Name == "ask_user" ? new NoraToolResult("TERMINAL", new NoraTerminal("QUESTION")) : new("OK")),
            new() { MaxModelTurns = 2, MaxToolCalls = 2 }, priorModel: 8, priorTools: 12);
        Assert.Equal("QUESTION", result.Terminal.Kind); Assert.Equal(10, result.ModelTurns); Assert.Equal(14, result.ToolCalls);
        Assert.Equal(new[] { 13, 14 }, result.Trace.Select(x => x.Sequence));
    }

    [Fact] public async Task Provider_call_id_is_echoed_only_when_supplied_and_invalid_args_are_not_executed()
    {
        var n = 0; var executions = 0;
        var result = await Run((request, _) =>
        {
            if (n++ == 0) return Task.FromResult(Call("inspect_constraints", providerId: "google-id"));
            if (n == 2) Assert.Equal("google-id", request.Contents[^1].GetProperty("parts")[0].GetProperty("functionResponse").GetProperty("id").GetString());
            var invalid = Call("inspect_constraints", n);
            return Task.FromResult(invalid with { Calls = [invalid.Calls[0] with { Arguments = Json(new { arbitrary = "data" }) }] });
        }, (_, _) => { executions++; return Task.FromResult(new NoraToolResult("OK")); });
        Assert.Equal(1, executions); Assert.Equal("REPEATED_TOOL_ERRORS", result.Terminal.Reply);
    }

    [Fact] public async Task Terminal_mixed_with_other_calls_cannot_finish()
    {
        var n = 0;
        var result = await Run((_, _) =>
        {
            var call = Call("finish_proposal", n++);
            return Task.FromResult(call with { Calls = [call.Calls[0], Call("finish_proposal", n++).Calls[0]] });
        }, (_, _) => throw new InvalidOperationException("Must not execute terminal"));
        Assert.Equal("BLOCKED", result.Terminal.Kind); Assert.All(result.Trace, x => Assert.Equal("ERROR", x.Status));
    }
}
