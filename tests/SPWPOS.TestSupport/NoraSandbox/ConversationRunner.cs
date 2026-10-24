using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
namespace SPWPOS.TestSupport.NoraSandbox;

public static class ConversationRunner
{
    public static async Task<SandboxRun> RunAsync(SandboxOptions options, Action<string>? progress, CancellationToken ct)
    {
        var root = Path.GetFullPath(options.RepositoryRoot); var output = Path.GetFullPath(options.OutputDirectory);
        if (File.Exists(Path.Combine(output, "results.json"))) throw new InvalidOperationException("Use a fresh output directory.");
        var pack = ConversationPack.Load(root); var selected = pack.Select(options);
        var live = options.Mode == "Live"; var config = SandboxEnvironment.LoadConfiguration(root, live);
        var started = DateTimeOffset.UtcNow; var budget = new SandboxBudget(options.MaxCalls);
        var results = new List<SandboxCaseResult>(); var errors = 0;
        var coverage = pack.Cases.ToDictionary(c => c.Id, c => selected.Any(x => x.Id == c.Id) ? "PENDING" : "NOT_SELECTED");
        var fingerprints = new[] { typeof(ConversationRunner).Assembly, typeof(SPWPOS.Infrastructure.Workspace.TaskInterpreter).Assembly, typeof(PlanningEngine).Assembly }
            .ToDictionary(a => a.GetName().Name + ".dll", a => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))));
        fingerprints["planning-conversations.vi.json"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "tests", "nora-sandbox", "planning-conversations.vi.json"))));
        SandboxRun Report() => new("1.2", started, DateTimeOffset.UtcNow, options.Mode,
            live ? config["AiSettings:Provider"] ?? "Unconfigured" : "FixtureValidation", live ? config["AiSettings:Model"] : null,
            null, null, "Per-case resolved fixture clock/timezone; see catalog.json and turn traces.", budget.Limit, budget.Used,
            results.ToArray(), new(coverage), fingerprints, [
                "Grouped conversation pack: each case uses a fresh synthetic account, in-memory SQLite database and conversation; turns within a case share state.",
                "Offline VALIDATED means the catalog and database fixture loaded successfully; no prompt was interpreted and no scheduling response was tested.",
                "Live REVIEW_REQUIRED means automatic outcome/schedule invariants passed. Per-turn textual expected.checks in catalog.json still require human review; it is not PASS.",
                "Only execution=chat with user-only turns is executable here. API, harness, fault, streaming, concurrency and extended scenarios remain NOT_RUN with their required actions exported.",
                "No production database, SMTP, hosted workers or real calendar is used. Only fictional fixture data and user prompt/history enter provider context; expected answers are never sent.",
                "Live provider calls consume quota and are bounded by MaxCalls and a three-consecutive-error circuit breaker. Call counts are not monetary limits."
            ]);
        Directory.CreateDirectory(output);
        var selectedSource = (JsonObject)pack.Source.DeepClone();
        var chosen = selected.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var group in selectedSource["groups"]!.AsArray())
        {
            var cases = group!["cases"]!.AsArray();
            for (var i = cases.Count - 1; i >= 0; i--) if (!chosen.Contains(cases[i]!["id"]!.GetValue<string>())) cases.RemoveAt(i);
        }
        // Keep source metadata intact; selection is recorded separately from the original suite counts/hash.
        selectedSource["source_validation"] = selectedSource["validation"]?.DeepClone();
        selectedSource.Remove("validation");
        selectedSource["selection"] = JsonSerializer.SerializeToNode(new { caseIds = selected.Select(c => c.Id), options.GroupIds, options.Mode }, SandboxJson.Options);
        await File.WriteAllTextAsync(Path.Combine(output, "catalog.json"), selectedSource.ToJsonString(SandboxJson.Options), ct);
        await SandboxExport.WriteAsync(Report(), output, CancellationToken.None);
        foreach (var scenario in selected)
        for (var repeat = 1; repeat <= options.Repeats; repeat++)
        {
            var turns = new List<SandboxTurnResult>(); var status = "RUNNING"; string? reason = null;
            var supported = scenario.Execution == "chat" && scenario.Turns.All(t => t.Type == "user");
            if (!supported || ct.IsCancellationRequested || live && (budget.Used >= options.MaxCalls || errors >= 3))
            {
                reason = !supported ? $"Requires {scenario.Execution} adapter; no user or harness step was executed. Full criteria/actions remain in catalog.json."
                    : ct.IsCancellationRequested ? "Run cancelled." : errors >= 3 ? "Stopped after three consecutive infrastructure/provider errors." : "Interpreter attempt cap reached.";
                status = "NOT_RUN";
            }
            else
            {
                var index = results.Count;
                results.Add(new(scenario.Id, scenario.Title, repeat, scenario.Fixture, "RUNNING", "NOT_EVALUATED", null, []));
                try
                {
                    await using var env = await ConversationFixture.CreateAsync(pack, scenario, config, live, budget, ct);
                    var fixture = pack.Fixture(scenario);
                    if (!live)
                    {
                        _ = await env.Snapshot(ct);
                        status = "VALIDATED"; reason = "Catalog/fixture validated only; no interpreter or planning request executed.";
                    }
                    else
                    {
                        foreach (var turn in scenario.Turns)
                        {
                            if (ct.IsCancellationRequested || budget.Used >= options.MaxCalls)
                            { status = "NOT_RUN"; reason = "Sequence incomplete: cancellation or interpreter cap; dependent steps were not executed."; break; }
                            var prompt = turn.Text!;
                            foreach (var (alias, id) in env.Ids) prompt = prompt.Replace("{{" + alias + "}}", id.ToString(), StringComparison.Ordinal);
                            var before = await env.Snapshot(ct); var watch = Stopwatch.StartNew();
                            var traceStart = env.Recorder.Traces.Count; var transportStart = env.Transport.Events.Count;
                            var usageIds = await env.Db(db => db.AiUsageRecords.Where(x => x.UserId == env.UserId).Select(x => x.Id).ToArrayAsync(ct), ct);
                            var input = new AgentTurnInput(env.ConversationId, Guid.NewGuid(), prompt, fixture["auto_apply"]?.GetValue<bool>() ?? true,
                                fixture["request_options"]?["use_capacity_factor"]?.GetValue<bool>() ?? false,
                                fixture["request_options"]?["use_estimate_bias"]?.GetValue<bool>() ?? false);
                            AgentTurnView? view = null; string? code = null; string? exceptionType = null; string? message = null;
                            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
                            try { view = await env.Agent(s => s.SendAgentTurnAsync(env.UserId, input, timeout.Token)); }
                            catch (AuthException e) { code = e.Code; exceptionType = nameof(AuthException); message = e.Message; }
                            catch (OperationCanceledException) { code = ct.IsCancellationRequested ? "SANDBOX_CANCELLED" : "SANDBOX_TIMEOUT"; exceptionType = nameof(OperationCanceledException); }
                            catch (Exception e) when (e is not OutOfMemoryException) { code = "SANDBOX_UNEXPECTED_ERROR"; exceptionType = e.GetType().Name; }
                            var after = await env.Snapshot(CancellationToken.None);
                            var stored = await env.Db(db => db.AgentTurns.AsNoTracking().SingleOrDefaultAsync(t => t.UserId == env.UserId && t.RequestId == input.RequestId, CancellationToken.None), CancellationToken.None);
                            var command = stored?.CommandJson is { Length: > 0 } ? JsonSerializer.Deserialize<JsonElement>(stored.CommandJson) : (JsonElement?)null;
                            var checks = SandboxOracle.Evaluate(Expectation(pack, turn.Expected.Outcome, input.AutoApply, view?.Status), view, code, before, after, env.Clock.GetUtcNow()).ToList();
                            if (view?.Status is "NEEDS_INPUT" or "ANSWERED" && command is { } intent)
                                checks.Add(new("stored-nonmutation-is-write-free", new[] { "tasks", "events", "exceptions", "weeklySlots", "scheduleTaskIds" }.All(k => intent.GetProperty(k).GetArrayLength() == 0)
                                    && !intent.GetProperty("reschedule").GetBoolean() && !intent.GetProperty("replaceWeeklyAvailability").GetBoolean(), "Persisted command must have no mutation commands.", true));
                            var usage = await env.Db(db => db.AiUsageRecords.AsNoTracking().Where(x => x.UserId == env.UserId && !usageIds.Contains(x.Id))
                                .Select(x => new { x.InputTokens, x.OutputTokens, x.ModelVersion }).ToArrayAsync(CancellationToken.None), CancellationToken.None);
                            turns.Add(new(prompt, "Send", view, code, exceptionType, before, after, SandboxOracle.Diff(before, after),
                                env.Recorder.Traces.Skip(traceStart).ToArray(), command, checks.ToArray(), watch.ElapsedMilliseconds,
                                usage.Any(x => x.InputTokens.HasValue) ? usage.Sum(x => x.InputTokens ?? 0) : null,
                                usage.Any(x => x.OutputTokens.HasValue) ? usage.Sum(x => x.OutputTokens ?? 0) : null,
                                usage.Select(x => x.ModelVersion).FirstOrDefault(x => x is not null), env.Transport.Events.Skip(transportStart).ToArray(),
                                stored?.ResultJson is { Length: > 0 } ? JsonSerializer.Deserialize<AgentTurnView>(stored.ResultJson, SandboxJson.Options) : null, message));
                            results[index] = results[index] with { Turns = turns.ToArray() };
                            await SandboxExport.WriteAsync(Report(), output, CancellationToken.None);
                            if (checks.Any(x => !x.Passed))
                            {
                                status = code is "SANDBOX_CALL_LIMIT" or "SANDBOX_CANCELLED" ? "NOT_RUN"
                                    : SandboxOracle.IsProviderError(code) || code is "SANDBOX_TIMEOUT" or "SANDBOX_UNEXPECTED_ERROR" ? "ERROR" : "FAIL";
                                reason = "Stopped at user turn " + turns.Count + "; dependent turns were not executed."; break;
                            }
                        }
                        if (status == "RUNNING") { status = "REVIEW_REQUIRED"; reason = "Automatic invariants passed; review every expected.checks item in catalog.json before judging the conversation."; }
                    }
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                { status = ct.IsCancellationRequested ? "NOT_RUN" : "ERROR"; reason = "Fixture/execution failed: " + e.GetType().Name + ". Raw exception text withheld."; }
                results.RemoveAt(index);
            }
            results.Add(new(scenario.Id, scenario.Title, repeat, scenario.Fixture, status,
                turns.Any(t => t.Traces.Any(x => x.Source == "Live" && x.InterpretedIntent is not null)) ? "REVIEW_REQUIRED" : "NOT_EVALUATED", reason, turns.ToArray()));
            if (status == "ERROR") errors++;
            else if (status != "NOT_RUN") errors = 0;
            coverage[scenario.Id] = status;
            progress?.Invoke($"NORA_CONVERSATIONS {scenario.Id} repeat={repeat} status={status} calls={budget.Used}/{options.MaxCalls}");
            await SandboxExport.WriteAsync(Report(), output, CancellationToken.None);
        }
        return Report();
    }

    private static SandboxExpectation Expectation(ConversationPack pack, string outcome, bool autoApply, string? status)
    {
        var rule = pack.Source["outcome_rules"]![outcome]!;
        var statuses = rule["allowed_statuses"]!.AsArray().Select(x => x!.GetValue<string>()).Select(x => x is "DOMAIN_REJECTED" or "API_REJECTED" ? "REJECTED" : x).ToArray();
        if (outcome == "MUTATE") statuses = autoApply ? ["APPLIED"] : ["PREVIEW"];
        // PROBE has no success oracle; allow recognized outcomes for safety checks, always retain REVIEW_REQUIRED.
        if (outcome == "PROBE") statuses = ["APPLIED", "PREVIEW", "ANSWERED", "NEEDS_INPUT", "BLOCKED", "REJECTED"];
        return new() { Statuses = statuses, Unchanged = rule["calendar_write"]!.GetValue<string>() == "none" || status is "NEEDS_INPUT" or "ANSWERED" or "PREVIEW" or "BLOCKED" };
    }
}
