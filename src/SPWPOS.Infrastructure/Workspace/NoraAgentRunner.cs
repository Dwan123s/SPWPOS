using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;

namespace SPWPOS.Infrastructure.Workspace;

// The model, not this runner, chooses the next tool. The runner enforces limits
// and returns each real observation before asking the model for another action.
public sealed class NoraAgentRunner
{
    public static string Fingerprint(string name, JsonElement arguments)
    {
        object? Canonical(JsonElement e) => e.ValueKind switch {
            JsonValueKind.Object => e.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).ToDictionary(x => x.Name, x => Canonical(x.Value)),
            JsonValueKind.Array => e.EnumerateArray().Select(Canonical).ToArray(),
            _ => e.Clone()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name + ":" + JsonSerializer.Serialize(Canonical(arguments)))));
    }

    public async Task<NoraRunnerResult> RunAsync(string instructions, JsonElement initial, NoraToolDefinition[] tools, NoraAgentOptions limits,
        int priorModelTurns, int priorToolCalls,
        Func<NoraModelRequest, CancellationToken, Task<NoraModelReply>> model,
        Func<NoraToolCall, CancellationToken, Task<NoraToolResult>> execute,
        Func<NoraToolTrace, string, NoraToolResult, CancellationToken, Task> record,
        CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(limits.TimeoutSeconds));
        var token = timeout.Token;
        var contents = new List<JsonElement> { initial };
        var trace = new List<NoraToolTrace>();
        var cache = new Dictionary<string, NoraToolResult>(StringComparer.Ordinal);
        var callIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var rounds = priorModelTurns; var calls = priorToolCalls; var consecutiveErrors = 0;
        // Audit counters span the goal; resource limits apply to this user turn.
        // A clarification answer must still have room to finish its proposal.
        var modelLimit = priorModelTurns + limits.MaxModelTurns;
        var toolLimit = priorToolCalls + limits.MaxToolCalls;
        NoraRunnerResult Stop(string code) => new(new("BLOCKED", code), trace.ToArray(), rounds, calls);
        try
        {
            while (rounds < modelLimit && calls < toolLimit)
            {
                token.ThrowIfCancellationRequested(); rounds++;
                var budget = $"\nCURRENT TURN BUDGET: {modelLimit - rounds + 1} model responses including this one and {toolLimit - calls} tool calls remain. " +
                    "A successful simulation already includes feasibility and warnings; do not re-evaluate the same candidate. " +
                    "When the requested changes are complete and feasible, finish_proposal next. Ask only for essential missing facts; never invent feasibility to finish.";
                var reply = await model(new(instructions + budget, contents.ToArray(), tools), token);
                if (reply.Content.ValueKind != JsonValueKind.Object || reply.Calls.Length is 0 or > 6)
                    throw new AuthException("AGENT_INVALID_STEP", "AI chưa chọn được hành động hợp lệ. Lịch chưa thay đổi.", 503);
                contents.Add(reply.Content);
                var observations = new List<object>();
                foreach (var call in reply.Calls)
                {
                    if (calls >= toolLimit) return Stop("TOOL_BUDGET_EXCEEDED");
                    token.ThrowIfCancellationRequested(); calls++;
                    var watch = Stopwatch.StartNew();
                    var definition = tools.SingleOrDefault(x => x.Name == call.Name);
                    var fingerprint = Fingerprint(call.Name, call.Arguments);
                    NoraToolResult result;
                    if (callIds.TryGetValue(call.Id, out var previous) && previous != fingerprint)
                        throw new AuthException("AGENT_CALL_REUSED", "AI dùng lại mã hành động cho nội dung khác. Lịch chưa thay đổi.", 503);
                    callIds[call.Id] = fingerprint;
                    if (definition is null) result = new("ERROR", ErrorCode: "UNKNOWN_TOOL", Message: "Chỉ dùng công cụ được cung cấp.");
                    else if (call.Name.StartsWith("finish_", StringComparison.Ordinal) || call.Name is "ask_user" or "propose_changes" or "answer_calendar" or "answer_free_time")
                    {
                        result = reply.Calls.Length != 1
                            ? new("ERROR", ErrorCode: "MIXED_TERMINAL", Message: "Kết thúc hoặc hỏi người dùng phải là hành động riêng.")
                            : await ValidatedExecution(call, definition, execute, token);
                    }
                    else if (cache.TryGetValue(fingerprint, out var saved)) result = saved;
                    else
                    {
                        result = await ValidatedExecution(call, definition, execute, token);
                        cache[fingerprint] = result;
                    }
                    var step = new NoraToolTrace(calls, call.Name, definition?.Label ?? "Hành động không được hỗ trợ", result.Status, watch.ElapsedMilliseconds);
                    trace.Add(step); await record(step, fingerprint, result, token);
                    if (result.Status == "TERMINAL" && result.Data is NoraTerminal terminal)
                        return new(terminal, trace.ToArray(), rounds, calls);
                    consecutiveErrors = result.Status == "ERROR" ? consecutiveErrors + 1 : 0;
                    if (consecutiveErrors >= 3) return Stop("REPEATED_TOOL_ERRORS");
                    var response = new Dictionary<string, object?> { ["name"] = call.Name,
                        ["response"] = new { status = result.Status, data = result.Data, errorCode = result.ErrorCode, message = result.Message } };
                    if (call.ProviderId is not null) response["id"] = call.ProviderId;
                    observations.Add(new { functionResponse = response });
                }
                var observation = JsonSerializer.SerializeToElement(new { role = "user", parts = observations });
                if (observation.GetRawText().Length > 100000)
                    throw new AuthException("AGENT_CONTEXT_LIMIT", "Kết quả công cụ vượt giới hạn. Hãy thu hẹp phạm vi lịch.", 503);
                contents.Add(observation);
            }
            return Stop("AGENT_BUDGET_EXCEEDED");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Stop("AGENT_TIME_LIMIT"); }
    }

    private static async Task<NoraToolResult> ValidatedExecution(NoraToolCall call, NoraToolDefinition definition,
        Func<NoraToolCall, CancellationToken, Task<NoraToolResult>> execute, CancellationToken ct)
    {
        try { TaskInterpreter.ValidateShape(call.Arguments, definition.Parameters); }
        catch (JsonException) { return new("ERROR", ErrorCode: "INVALID_TOOL_ARGUMENTS", Message: "Tham số không khớp schema công cụ."); }
        try { return await execute(call, ct); }
        catch (AuthException error) when (error.Code is not "AGENT_STALE" and not "FORBIDDEN" and not "UNAUTHORIZED")
        { return new("ERROR", ErrorCode: error.Code, Message: error.Message); }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException or ArgumentException)
        { return new("ERROR", ErrorCode: "INVALID_TOOL_ARGUMENTS", Message: "Kiểm tra lại tham số và dữ liệu công cụ đã trả về."); }
    }
}
