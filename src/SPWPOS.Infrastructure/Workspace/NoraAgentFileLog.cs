using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;

namespace SPWPOS.Infrastructure.Workspace;

// A development action log, not a transcript of provider requests or private reasoning.
public sealed class NoraAgentFileLog
{
    private readonly object gate = new();
    private readonly TimeProvider clock;
    private readonly ILogger<NoraAgentFileLog> logger;
    private readonly string? directory;
    private bool warned;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> Tools = NoraToolRegistry.Create([]).Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
    private const long MaximumDailyBytes = 10 * 1024 * 1024;

    public NoraAgentFileLog(IConfiguration configuration, TimeProvider clock, ILogger<NoraAgentFileLog> logger,
        IHostEnvironment? environment = null)
    {
        this.clock = clock;
        this.logger = logger;
        if (environment?.IsDevelopment() == true && configuration.GetValue("NoraAgent:FileLog:Enabled", true))
            directory = Path.Combine(environment.ContentRootPath, "logs", "nora-agent");
    }

    public void Initialize() => Write(null, null, "LOG_READY", new { environment = "Development" });

    public void RunStarted(Guid runId, Guid turnId, Guid conversationId, AgentTurnInput input, int continuation) =>
        Write(runId, turnId, "RUN_STARTED", new { conversationId, requestId = input.RequestId,
            promptCharacters = input.Text.Length, hasImage = input.Image is not null, input.AutoApply, continuation });

    public void ModelStarted(Guid runId, Guid turnId, int round) =>
        Write(runId, turnId, "MODEL_STARTED", new { round });

    public void ModelCompleted(Guid runId, Guid turnId, int round, long durationMs, NoraModelReply reply) =>
        Write(runId, turnId, "MODEL_COMPLETED", new { round, durationMs,
            selectedTools = reply.Calls.Select(x => Tools.Contains(x.Name) ? x.Name : "UNKNOWN_TOOL").ToArray() });

    public void ToolCompleted(Guid runId, Guid turnId, NoraToolTrace step, string argumentsHash, NoraToolResult result) =>
        Write(runId, turnId, "TOOL_COMPLETED", new { sequence = step.Sequence,
            tool = Tools.Contains(step.Tool) ? step.Tool : "UNKNOWN_TOOL", status = SafeCode(step.Status),
            durationMs = step.DurationMs, argumentsHash, errorCode = SafeCode(result.ErrorCode) });

    public void RunCompleted(Guid runId, Guid turnId, string status, int modelTurns, int toolCalls, int simulations) =>
        Write(runId, turnId, "RUN_COMPLETED", new { status = SafeCode(status), modelTurns, toolCalls, simulations,
            businessDataSaved = false });

    public void Failed(Guid runId, Guid turnId, string stage, Exception error, bool cancelled) =>
        Write(runId, turnId, "RUN_ERROR", new { stage = SafeCode(stage), cancelled,
            errorCode = error is AuthException failure ? SafeCode(failure.Code) : "UNEXPECTED_ERROR" });

    // Called by the controller only after the service transaction has returned successfully.
    public void TurnCompleted(AgentTurnView turn, string operation) =>
        Write(turn.Run?.Id, turn.Id, "TURN_COMPLETED", new { operation = SafeCode(operation), status = SafeCode(turn.Status),
            businessDataSaved = turn.Status is "APPLIED" or "UNDONE", changes = turn.Changes.Length,
            warnings = turn.Warnings.Length, errorCode = SafeCode(turn.ErrorCode) });

    private static string? SafeCode(string? value) => value is null ? null :
        Regex.IsMatch(value, "^[A-Z0-9_]{1,80}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) ? value : "OTHER";

    private void Write(Guid? runId, Guid? turnId, string eventName, object details)
    {
        if (directory is null) return;
        lock (gate)
        {
            try
            {
                var timestamp = clock.GetUtcNow();
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"nora-agent-{timestamp:yyyy-MM-dd}.jsonl");
                var line = JsonSerializer.Serialize(new { timestamp, eventName, runId, turnId, details }, Json) + Environment.NewLine;
                // Bound disk usage without letting a diagnostic failure break an agent request.
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                var bytes = Encoding.UTF8.GetBytes(line);
                if (stream.Length + bytes.Length > MaximumDailyBytes)
                {
                    Warn();
                    return;
                }
                stream.Write(bytes);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                Warn();
            }
        }
    }

    private void Warn()
    {
        if (warned) return;
        warned = true;
        logger.LogWarning("NORA development file logging is unavailable or reached its daily 10 MiB limit. Agent processing continues.");
    }
}
