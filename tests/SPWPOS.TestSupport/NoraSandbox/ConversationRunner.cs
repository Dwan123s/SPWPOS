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
    private static readonly HashSet<string> PreparedFixtureCases = [
        "G07-05", "G07-06", "G16-06", "G20-03", "G20-06", "G21-06", "G22-04", "G22-05"
    ];
    private static readonly HashSet<string> LifecycleCases = [
        "G23-05", "G23-06", "G24-01", "G24-02", "G24-03", "G24-04", "G24-05", "G24-06",
        "G25-01", "G25-02", "G25-05", "G25-06"
    ];
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
                "Chat/user-only cases run by default. IncludePreparedFixtures enables eight extra fixture-only scenarios; IncludeLifecycleActions enables twelve lifecycle scenarios through real public service calls. These are not HTTP/browser transport tests. Other adapters remain NOT_RUN with their required actions exported.",
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
            var supported = (scenario.Execution == "chat" || options.IncludePreparedFixtures && PreparedFixtureCases.Contains(scenario.Id)) &&
                scenario.Turns.All(t => t.Type == "user") || options.IncludeLifecycleActions && LifecycleCases.Contains(scenario.Id);
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
                        var lifecycle = new ConversationActions();
                        foreach (var turn in scenario.Turns)
                        {
                            if (ct.IsCancellationRequested || budget.Used >= options.MaxCalls)
                            { status = "NOT_RUN"; reason = "Sequence incomplete: cancellation or interpreter cap; dependent steps were not executed."; break; }
                            var prompt = turn.Text ?? "[harness] " + turn.Action;
                            foreach (var (alias, id) in env.Ids) prompt = prompt.Replace("{{" + alias + "}}", id.ToString(), StringComparison.Ordinal);
                            var before = await env.Snapshot(ct); var watch = Stopwatch.StartNew();
                            var foreignBefore = scenario.Id == "G20-06" ? await env.Db(db => db.WorkTasks.AsNoTracking()
                                .Where(t => t.UserId != env.UserId).OrderBy(t => t.Id).ToArrayAsync(ct), ct) : [];
                            var traceStart = env.Recorder.Traces.Count; var transportStart = env.Transport.Events.Count;
                            var usageIds = await env.Db(db => db.AiUsageRecords.Where(x => x.UserId == env.UserId).Select(x => x.Id).ToArrayAsync(ct), ct);
                            var input = new AgentTurnInput(env.ConversationId, Guid.NewGuid(), prompt, fixture["auto_apply"]?.GetValue<bool>() ?? true,
                                fixture["request_options"]?["use_capacity_factor"]?.GetValue<bool>() ?? false,
                                fixture["request_options"]?["use_estimate_bias"]?.GetValue<bool>() ?? false);
                            AgentTurnView? view = null; string? code = null; string? exceptionType = null; string? message = null;
                            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
                            try {
                                if(turn.Type == "user")
                                {
                                    view = await env.Agent(s => s.SendAgentTurnAsync(env.UserId, input, timeout.Token));
                                    lifecycle.Capture(input, view);
                                }
                                else view = await lifecycle.ExecuteAsync(env, turn, timeout.Token);
                            }
                            catch (AuthException e) { code = e.Code; exceptionType = nameof(AuthException); message = e.Message; }
                            catch (OperationCanceledException) { code = ct.IsCancellationRequested ? "SANDBOX_CANCELLED" : "SANDBOX_TIMEOUT"; exceptionType = nameof(OperationCanceledException); }
                            catch (Exception e) when (e is not OutOfMemoryException) { code = "SANDBOX_UNEXPECTED_ERROR"; exceptionType = e.GetType().Name; }
                            var after = await env.Snapshot(CancellationToken.None);
                            var lookupByRequest = turn.Type == "user"; var lookupId = view?.Id ?? Guid.Empty;
                            var stored = await env.Db(db => db.AgentTurns.AsNoTracking().SingleOrDefaultAsync(t => t.UserId == env.UserId &&
                                (lookupByRequest ? t.RequestId == input.RequestId : t.Id == lookupId), CancellationToken.None), CancellationToken.None);
                            var command = stored?.CommandJson is { Length: > 0 } ? JsonSerializer.Deserialize<JsonElement>(stored.CommandJson) : (JsonElement?)null;
                            var expected = Expectation(pack, turn.Expected.Outcome, input.AutoApply, view?.Status, turn.Expected.Assertions);
                            var expectedJson = JsonSerializer.Serialize(expected, SandboxJson.Options);
                            foreach (var (alias, id) in env.Ids) expectedJson = expectedJson.Replace("{{" + alias + "}}", id.ToString(), StringComparison.Ordinal);
                            expected = JsonSerializer.Deserialize<SandboxExpectation>(expectedJson, SandboxJson.Options)!;
                            if(turn.Expected.Outcome == "API_REJECT" && options.IncludeLifecycleActions && LifecycleCases.Contains(scenario.Id))
                                expected = expected with { Statuses = [scenario.Id switch {
                                    "G23-05" => "AGENT_STALE", "G23-06" => "AGENT_PREVIEW_EXPIRED", "G24-03" => "AGENT_UNDO_STALE",
                                    "G24-05" => "CONVERSATION_ARCHIVED", "G24-06" => "CONVERSATION_DELETED", "G25-02" => "AGENT_REQUEST_REUSED",
                                    _ => throw new InvalidOperationException("Missing exact rejection contract.")
                                }] };
                            var checks = SandboxOracle.Evaluate(expected, view, code, before, after, env.Clock.GetUtcNow()).ToList();
                            if(scenario.Id == "G20-06")
                            {
                                var foreignAfter = await env.Db(db => db.WorkTasks.AsNoTracking().Where(t => t.UserId != env.UserId)
                                    .OrderBy(t => t.Id).ToArrayAsync(CancellationToken.None), CancellationToken.None);
                                checks.Add(new("foreign-tasks-unchanged", JsonSerializer.Serialize(foreignBefore, SandboxJson.Options) ==
                                    JsonSerializer.Serialize(foreignAfter, SandboxJson.Options), "Foreign account tasks must remain unchanged.", true));
                                checks.Add(new("foreign-task-names-not-disclosed", foreignBefore.All(t =>
                                    !((view?.Reply ?? "") + " " + (view?.Question ?? "")).Contains(t.Name, StringComparison.OrdinalIgnoreCase) &&
                                    env.Recorder.Traces.Skip(traceStart).All(trace => !trace.ContextJson.Contains(t.Name, StringComparison.OrdinalIgnoreCase))),
                                    "Private foreign task names must not enter model context or replies.", true));
                            }
                            if (view?.Status is "NEEDS_INPUT" or "ANSWERED" && command is { } intent)
                                checks.Add(new("stored-nonmutation-is-write-free", new[] { "tasks", "events", "exceptions", "weeklySlots", "scheduleTaskIds" }.All(k => intent.GetProperty(k).GetArrayLength() == 0)
                                    && !intent.GetProperty("reschedule").GetBoolean() && !intent.GetProperty("replaceWeeklyAvailability").GetBoolean(), "Persisted command must have no mutation commands.", true));
                            var usage = await env.Db(db => db.AiUsageRecords.AsNoTracking().Where(x => x.UserId == env.UserId && !usageIds.Contains(x.Id))
                                .Select(x => new { x.InputTokens, x.OutputTokens, x.ModelVersion }).ToArrayAsync(CancellationToken.None), CancellationToken.None);
                            turns.Add(new(prompt, turn.Type == "user" ? "Send" : turn.Action!, view, code, exceptionType, before, after, SandboxOracle.Diff(before, after),
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

    public static SandboxExpectation Expectation(ConversationPack pack, string outcome, bool autoApply, string? status,
        SandboxExpectation? assertions = null)
    {
        var rule = pack.Source["outcome_rules"]![outcome]!;
        var statuses = rule["allowed_statuses"]!.AsArray().Select(x => x!.GetValue<string>()).Select(x => x is "DOMAIN_REJECTED" or "API_REJECTED" ? "REJECTED" : x).ToArray();
        if (outcome == "MUTATE") statuses = autoApply ? ["APPLIED"] : ["PREVIEW"];
        // PROBE allows recognized outcomes, but explicit saved-state assertions
        // still apply. Natural-language criteria always retain REVIEW_REQUIRED.
        if (outcome == "PROBE") statuses = ["APPLIED", "PREVIEW", "ANSWERED", "NEEDS_INPUT", "BLOCKED", "REJECTED"];
        return (assertions ?? new()) with { Statuses = statuses, Unchanged = assertions?.Unchanged == true ||
            rule["calendar_write"]!.GetValue<string>() == "none" || status is "NEEDS_INPUT" or "ANSWERED" or "PREVIEW" or "BLOCKED" };
    }
}
