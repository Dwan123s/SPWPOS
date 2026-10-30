using System.Text.Json;

namespace SPWPOS.Application.Workspace;

public sealed class NoraAgentOptions
{
    public string Mode { get; set; } = "Workflow";
    public int MaxModelTurns { get; set; } = 8;
    public int MaxToolCalls { get; set; } = 12;
    public int MaxSimulations { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 120;
}
public sealed record NoraToolDefinition(string Name, string Description, JsonElement Parameters, string Label);
public sealed record NoraToolCall(string Id, string Name, JsonElement Arguments, string? ProviderId = null);
public sealed record NoraModelRequest(string Instructions, JsonElement[] Contents, NoraToolDefinition[] Tools);
public sealed record NoraModelReply(JsonElement Content, NoraToolCall[] Calls);
public interface INoraAgentModel
{
    bool AgentAvailable { get; }
    Task<NoraModelReply> NextAgentStepAsync(NoraModelRequest request, CancellationToken ct);
}
public sealed record NoraToolResult(string Status, object? Data = null, string? ErrorCode = null, string? Message = null);
public sealed record NoraToolTrace(int Sequence, string Tool, string Label, string Status, long DurationMs);
public sealed record NoraRunView(Guid Id, string Mode, string Status, int ModelTurns, int ToolCalls, NoraToolTrace[] Steps);
public sealed record NoraTerminal(string Kind, string Reply = "", string Question = "", string[]? Options = null, string? CandidateId = null);
public sealed record NoraRunnerResult(NoraTerminal Terminal, NoraToolTrace[] Trace, int ModelTurns, int ToolCalls);
