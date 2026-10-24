using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
namespace SPWPOS.TestSupport.NoraSandbox;

public static class SandboxRunner
{
    public static SandboxCase[] LoadCatalog(string repositoryRoot)
    {
        var rows = JsonSerializer.Deserialize<SandboxCase[]>(File.ReadAllText(Path.Combine(repositoryRoot, "tests", "nora-sandbox", "cases.json")), SandboxJson.Options)
            ?? throw new InvalidOperationException("Empty sandbox catalog.");
        if (rows.Length == 0 || rows.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != rows.Length ||
            rows.Any(x => x.Automation == "Service" && x.Steps.Length == 0)) throw new InvalidOperationException("Invalid sandbox catalog.");
        return rows;
    }

    private static string ReplaceIds(string value, SandboxEnvironment env)
    {
        foreach (var id in env.Ids) value = value.Replace("{" + id.Key + "}", id.Value.ToString(), StringComparison.Ordinal);
        return value;
    }
    private static SandboxStep Resolve(SandboxStep step, SandboxEnvironment env) => step with {
        Prompt = ReplaceIds(step.Prompt, env), Expected = JsonSerializer.Deserialize<SandboxExpectation>(
            ReplaceIds(JsonSerializer.Serialize(step.Expected, SandboxJson.Options), env), SandboxJson.Options)!
    };
    private static (string? Commit, bool? Dirty) GitState(string root)
    {
        string Read(params string[] args)
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var value = process.StandardOutput.ReadToEnd(); _ = process.StandardError.ReadToEnd();
            return process.WaitForExit(5000) && process.ExitCode == 0 ? value.Trim() : throw new InvalidOperationException("Git metadata unavailable.");
        }
        try { return (Read("rev-parse", "HEAD"), Read("status", "--porcelain").Length > 0); }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { return (null, null); }
    }

    public static async Task<SandboxRun> RunAsync(SandboxOptions options, Action<string>? progress, CancellationToken ct)
    {
        if (options.Mode is not "Offline" and not "Live" || options.Suite is not "Smoke" and not "All" || options.Repeats is < 1 or > 5 ||
            options.MaxCalls is < 1 or > 500 || options.TimeoutSeconds is < 5 or > 120 || options.Catalog is not "Legacy" and not "Conversations") throw new ArgumentException("Invalid sandbox options.");
        if (options.Catalog == "Conversations") return await ConversationRunner.RunAsync(options, progress, ct);
        if (options.GroupIds is { Length: > 0 }) throw new ArgumentException("Group IDs require the Conversations catalog.");
        var root = Path.GetFullPath(options.RepositoryRoot); var output = Path.GetFullPath(options.OutputDirectory);
        if (File.Exists(Path.Combine(output, "results.json"))) throw new InvalidOperationException("Use a fresh output directory; existing results are preserved.");
        var catalog = LoadCatalog(root); var requested = options.CaseIds ?? [];
        if (requested.Except(catalog.Select(x => x.Id), StringComparer.Ordinal).Any()) throw new ArgumentException("Unknown sandbox case IDs.");
        var selected = catalog.Where(x => requested.Length > 0 ? requested.Contains(x.Id) : options.Suite == "All" || x.Smoke).ToArray();
        var live = options.Mode == "Live"; var config = SandboxEnvironment.LoadConfiguration(root, live);
        var git = GitState(root); var budget = new SandboxBudget(options.MaxCalls); var started = DateTimeOffset.UtcNow;
        var fingerprints = new[] { typeof(SandboxRunner).Assembly, typeof(SPWPOS.Infrastructure.Workspace.TaskInterpreter).Assembly, typeof(PlanningEngine).Assembly }
            .ToDictionary(x => x.GetName().Name + ".dll", x => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(x.Location))));
        fingerprints["catalog.json"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(catalog, SandboxJson.Options))));
        var results = new List<SandboxCaseResult>(); var consecutiveErrors = 0;
        var coverage = catalog.ToDictionary(x => x.Id, x => selected.Any(s => s.Id == x.Id) ? x.Automation == "Service" ? "PENDING" : "NOT_AUTOMATED" : "NOT_SELECTED");
        SandboxRun Current() => new("1.1", started, DateTimeOffset.UtcNow, options.Mode,
            live ? config["AiSettings:Provider"] ?? "Unconfigured" : "Scripted", live ? config["AiSettings:Model"] : null,
            git.Commit, git.Dirty, SandboxEnvironment.ReferenceTime.ToString("O"), options.MaxCalls, budget.Used,
            results.ToArray(), new(coverage), fingerprints, [
                "SQLite in-memory; no production database, authenticated HTTP API, background workers or real calendar.",
                "Only synthetic fixture data is sent to the configured provider. No configuration values, credentials or raw provider bodies are exported.",
                "Offline results test scripted interpreter fixtures and real backend actions; they do not measure live prompt comprehension.",
                "PASS means the listed automatic checks passed. Response semantics require human review; lexical task-name matching can need adjudication.",
                "Service-level runs do not verify browser rendering, microphone input, transport disconnects, SQL Server locks or unimplemented scenario variants.",
                "Live calls consume provider quota. Call count is a cap on interpreter attempts, including scripted and injected attempts, not a currency/token spending guarantee."
            ]);
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "catalog.json"), JsonSerializer.Serialize(catalog, SandboxJson.Options), ct);
        await SandboxExport.WriteAsync(Current(), output, CancellationToken.None);
        foreach (var scenario in selected)
        for (var repeat = 1; repeat <= options.Repeats; repeat++)
        {
            var turns = new List<SandboxTurnResult>(); string? reason = scenario.Limitation; var status = "RUNNING";
            if (scenario.Automation != "Service" || budget.Used >= options.MaxCalls || ct.IsCancellationRequested || consecutiveErrors >= 3)
            {
                status = "NOT_RUN";
                reason = scenario.Automation != "Service" ? scenario.Limitation : ct.IsCancellationRequested ? "Run cancelled."
                    : consecutiveErrors >= 3 ? "Stopped after three consecutive infrastructure/provider errors." : "Interpreter call cap reached.";
                results.Add(new(scenario.Id, scenario.Title, repeat, scenario.Fixture, status, "NOT_EVALUATED", reason, []));
                coverage[scenario.Id] = status; await SandboxExport.WriteAsync(Current(), output, CancellationToken.None); continue;
            }
            results.Add(new(scenario.Id, scenario.Title, repeat, scenario.Fixture, status, live ? "REVIEW_REQUIRED" : "NOT_EVALUATED", reason, []));
            var resultIndex = results.Count - 1;
            try
            {
                await using var env = await SandboxEnvironment.CreateAsync(scenario.Fixture, config, live, budget, ct);
                AgentTurnInput? previousInput = null; AgentTurnView? previousResult = null;
                for (var stepIndex = 0; stepIndex < scenario.Steps.Length; stepIndex++)
                {
                    if (ct.IsCancellationRequested || budget.Used >= options.MaxCalls && scenario.Steps[stepIndex].Operation is "Send" or "SendPreview")
                    { status = "NOT_RUN"; reason = "Remaining sequence not run: cancellation or call cap."; break; }
                    var step = Resolve(scenario.Steps[stepIndex], env);
                    if (step.Operation is "StaleApply" or "StaleUndo")
                        await env.Db(async db => { (await db.WorkPreferences.SingleAsync(x => x.UserId == env.UserId, ct)).Revision = Guid.NewGuid(); await db.SaveChangesAsync(ct); }, ct);
                    if (step.Operation == "ExpiredApply") env.Clock.Advance(TimeSpan.FromMinutes(31));
                    if (step.Operation == "DisableTypeApply")
                        await env.Db(async db => { (await db.TaskTypes.SingleAsync(x => x.Code == "WRITING", ct)).IsActive = false; await db.SaveChangesAsync(ct); }, ct);
                    var before = await env.Snapshot(ct); var watch = Stopwatch.StartNew(); var traceStart = env.Recorder.Traces.Count;
                    var transportStart = env.Transport.Events.Count;
                    var previousUsageIds = await env.Db(db => db.AiUsageRecords.Where(x => x.UserId == env.UserId).Select(x => x.Id).ToArrayAsync(ct), ct);
                    AgentTurnView? result = null; string? code = null; string? exceptionType = null; string? errorMessage = null; JsonElement? command = null;
                    var scripted = !live || step.Operation == "MixedClarification";
                    env.Recorder.Script = scripted ? _ => SandboxScriptedResponses.Build(scenario, stepIndex, step, before) : null;
                    env.Recorder.FailNext = step.Operation == "InjectedFailure";
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
                    var token = timeout.Token;
                    try
                    {
                        if (step.Operation is "Apply" or "StaleApply" or "ExpiredApply" or "DisableTypeApply")
                            result = await env.Agent(s => s.ApplyAgentTurnAsync(env.UserId, previousResult!.Id, token));
                        else if (step.Operation is "Undo" or "StaleUndo")
                            result = await env.Agent(s => s.UndoAgentTurnAsync(env.UserId, previousResult!.Id, token));
                        else
                        {
                            var input = step.Operation is "Retry" or "RetryChanged" ? previousInput! : new AgentTurnInput(env.ConversationId, Guid.NewGuid(), step.Prompt, step.Operation != "SendPreview");
                            if (step.Operation == "RetryChanged") input = input with { Text = input.Text.Replace("60", "90", StringComparison.Ordinal) };
                            previousInput = input;
                            result = await env.Agent(s => s.SendAgentTurnAsync(env.UserId, input, token));
                        }
                    }
                    catch (AuthException error) { code = error.Code; exceptionType = nameof(AuthException); errorMessage = error.Message; }
                    catch (OperationCanceledException) { code = ct.IsCancellationRequested ? "SANDBOX_CANCELLED" : "SANDBOX_TIMEOUT"; exceptionType = nameof(OperationCanceledException); }
                    catch (Exception error) { code = "SANDBOX_UNEXPECTED_ERROR"; exceptionType = error.GetType().Name; }
                    var after = await env.Snapshot(CancellationToken.None);
                    var persisted = await env.Db(db => db.AgentTurns.AsNoTracking().Where(x => x.UserId == env.UserId)
                        .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToArrayAsync(CancellationToken.None), CancellationToken.None);
                    var saved = result is not null ? persisted.SingleOrDefault(x => x.Id == result.Id) : previousInput is not null ? persisted.SingleOrDefault(x => x.RequestId == previousInput.RequestId) : null;
                    if (saved?.CommandJson is { Length: > 0 }) command = JsonSerializer.Deserialize<JsonElement>(saved.CommandJson);
                    // A retained APPLIED/PREVIEW turn is not the result of a rejected retry/apply.
                    var storedView = saved?.ResultJson is { Length: > 0 } ? JsonSerializer.Deserialize<AgentTurnView>(saved.ResultJson, SandboxJson.Options) : null;
                    var outcomeResult = code is null ? result : null;
                    var checks = SandboxOracle.Evaluate(step.Expected, outcomeResult, code, before, after, env.Clock.GetUtcNow()).ToList();
                    if (outcomeResult?.Status == "NEEDS_INPUT" && command is { } intent)
                        checks.Add(new("stored-clarification-is-write-free", new[] { "tasks", "events", "exceptions", "weeklySlots", "scheduleTaskIds" }.All(x => intent.GetProperty(x).GetArrayLength() == 0) &&
                            !intent.GetProperty("reschedule").GetBoolean() && !intent.GetProperty("replaceWeeklyAvailability").GetBoolean(), "Check sanitized persisted command, not the untrusted model intent.", true));
                    if (step.Operation == "Retry" && previousResult is not null)
                        checks.Add(new("idempotent-result-id", result?.Id == previousResult.Id && result?.Plan?.Id == previousResult.Plan?.Id, "Retry returns the original turn and plan IDs.", true));
                    if (scenario.Id == "F03")
                        checks.Add(new("foreign-owner-unchanged", await env.Db(async db => {
                            var foreign = await db.WorkTasks.AsNoTracking().SingleAsync(x => x.Id == env.Ids["FOREIGN"], CancellationToken.None);
                            return foreign.UserId != env.UserId && foreign.EstimatedMinutes == 60 && !await db.WorkSessions.AnyAsync(x => x.TaskId == foreign.Id, CancellationToken.None);
                        }, CancellationToken.None), "Foreign synthetic task remains owned and unscheduled.", true));
                    var usage = await env.Db(db => db.AiUsageRecords.AsNoTracking().Where(x => x.UserId == env.UserId && !previousUsageIds.Contains(x.Id))
                        .Select(x => new { x.InputTokens, x.OutputTokens, x.ModelVersion }).ToArrayAsync(CancellationToken.None), CancellationToken.None);
                    var traces = env.Recorder.Traces.Skip(traceStart).ToArray();
                    turns.Add(new(step.Prompt, step.Operation, result, code, exceptionType, before, after, SandboxOracle.Diff(before, after), traces, command,
                        checks.ToArray(), watch.ElapsedMilliseconds, traces.Length > 0 ? usage.SingleOrDefault()?.InputTokens : null,
                        traces.Length > 0 ? usage.SingleOrDefault()?.OutputTokens : null, traces.Length > 0 ? usage.SingleOrDefault()?.ModelVersion : null,
                        env.Transport.Events.Skip(transportStart).ToArray(), storedView, errorMessage));
                    results[resultIndex] = results[resultIndex] with { Turns = turns.ToArray() };
                    await SandboxExport.WriteAsync(Current(), output, CancellationToken.None);
                    if (checks.Any(x => !x.Passed))
                    {
                        status = code == "SANDBOX_CALL_LIMIT" || code == "SANDBOX_CANCELLED" ? "NOT_RUN"
                            : SandboxOracle.IsProviderError(code) || code is "SANDBOX_TIMEOUT" or "SANDBOX_UNEXPECTED_ERROR" ? "ERROR" : "FAIL";
                        reason = "Stopped after step " + (stepIndex + 1) + "; dependent steps were not executed. " + scenario.Limitation; break;
                    }
                    previousResult = result;
                }
                if (status == "RUNNING") status = "PASS";
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                status = "ERROR"; reason = "Sandbox setup/execution failed: " + error.GetType().Name + ". Raw exception text is withheld.";
            }
            results[resultIndex] = new(scenario.Id, scenario.Title, repeat, scenario.Fixture, status,
                live && turns.Any(x => x.Traces.Any(t => t.Source == "Live" && t.InterpretedIntent is not null)) ? "REVIEW_REQUIRED" : "NOT_EVALUATED", reason, turns.ToArray());
            consecutiveErrors = status == "ERROR" ? consecutiveErrors + 1 : 0;
            coverage[scenario.Id] = status; progress?.Invoke($"NORA_SANDBOX {scenario.Id} repeat={repeat} status={status} calls={budget.Used}/{options.MaxCalls}");
            await SandboxExport.WriteAsync(Current(), output, CancellationToken.None);
        }
        return Current();
    }
}
