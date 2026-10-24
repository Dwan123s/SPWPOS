using System.Text.Encodings.Web;
using System.Text.Json;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.TestSupport.NoraSandbox;

public static class SandboxJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}

public sealed record SandboxOptions(string RepositoryRoot, string OutputDirectory, string Mode = "Offline",
    string Suite = "Smoke", string[]? CaseIds = null, int Repeats = 1, int MaxCalls = 30, int TimeoutSeconds = 60,
    string Catalog = "Legacy", string[]? GroupIds = null);
public sealed record SandboxTaskExpectation(string Match, int Minutes, string Deadline, bool? Splittable = null,
    int? Priority = null, bool Scheduled = true, string? Id = null, string? WorkWindowStart = null, string? WorkWindowEnd = null);
public sealed record SandboxEventExpectation(string Match, string Start, string End, string? EventType = null);
public sealed record SandboxFreeExpectation(string Date, int StartMinute, int EndMinute);
public sealed record SandboxExpectation {
    public string[] Statuses { get; init; } = ["APPLIED"];
    public bool Unchanged { get; init; }
    public int? TaskCount { get; init; }
    public int? EventCount { get; init; }
    public int? ExceptionCount { get; init; }
    public SandboxTaskExpectation[] Tasks { get; init; } = [];
    public SandboxEventExpectation[] Events { get; init; } = [];
    public string[] Preserve { get; init; } = [];
    public int[][]? Weekly { get; init; }
    public SandboxFreeExpectation[] Free { get; init; } = [];
}
public sealed record SandboxStep(string Prompt, SandboxExpectation Expected, string Operation = "Send");
public sealed record SandboxCase(string Id, string Title, string Fixture, bool Smoke, string Automation,
    string? Limitation, SandboxStep[] Steps);
public sealed record SandboxSnapshot(WorkPreference Preference, WorkTask[] Tasks, WorkSession[] Sessions,
    FixedEvent[] Events, WeeklyAvailability[] Weekly, AvailabilityException[] Exceptions,
    TaskDependency[] Dependencies, string[] ActiveTaskTypes, PlanProposal[] Proposals);
public sealed record SandboxChange(string Entity, string Id, string Operation, JsonElement? Before, JsonElement? After);
public sealed record SandboxCheck(string Name, bool Passed, string Detail, bool Critical = false);
public sealed record SandboxTrace(string Prompt, string ContextJson, AgentMessage[] History, AgentIntent? InterpretedIntent,
    DateTimeOffset CurrentUtc, string TimeZoneId, string Source);
public sealed record SandboxTransportEvent(int? HttpStatus, string? ExceptionType, string? SocketError);
public sealed record SandboxTurnResult(string Prompt, string Operation, AgentTurnView? Result, string? ErrorCode,
    string? ExceptionType, SandboxSnapshot Before, SandboxSnapshot After, SandboxChange[] Actions,
    SandboxTrace[] Traces, JsonElement? StoredCommand, SandboxCheck[] Checks, long ElapsedMilliseconds,
    long? InputTokens, long? OutputTokens, string? ModelVersion, SandboxTransportEvent[] Transport,
    AgentTurnView? PersistedTurn = null, string? ErrorMessage = null);
public sealed record SandboxCaseResult(string Id, string Title, int Repeat, string Fixture, string Status,
    string ResponseReview, string? Reason, SandboxTurnResult[] Turns);
public sealed record SandboxRun(string SchemaVersion, DateTimeOffset StartedAt, DateTimeOffset FinishedAt,
    string Mode, string Provider, string? Model, string? GitCommit, bool? DirtyWorkingTree,
    string ControlledClock, int CallLimit, int CallsUsed, SandboxCaseResult[] Cases,
    Dictionary<string, string> Coverage, Dictionary<string, string> RuntimeSha256, string[] Limitations);
