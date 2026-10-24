# NORA automated sandbox

The runner executes the actual NORA service, interpreter contract and planning engine against a **fresh in-memory SQLite database per scenario**. Each conversation sequence shares its own database; independent cases do not share calendar state. The clock and fixtures use the week beginning 26 October 2026, regardless of the computer's current date.

The integrated catalog contains 87 scenarios, including S07, the morning-class/afternoon-work-shift screenshot regression. S07 requires a CLASS event followed by a WORK event at the exact requested times. WORK is a built-in task and event category; unsupported agent categories fall back to active OTHER without bypassing scheduling validation. E08 and S02 are updated for main's implemented work windows. E08 now requires a task scheduled on the requested evening; S02 clarifies duration, saves the bounded task, then checks it without further writes. Independent checks enforce saved execution windows and date-specific exclusive availability overrides. Earlier evaluation reports describe the catalog at their recorded revisions, where these work windows were unsupported.

It does not start the web server, connect to the configured SQL Server database, start hosted workers, send email, or use your signed-in calendar. Live mode loads only `AiSettings` from API appsettings, the existing API User Secrets assembly and environment variables. Authentication/SQL/SMTP settings are synthetic test configuration; email transport is fake.

## Run

### Grouped conversation pack

`tests/nora-sandbox/planning-conversations.vi.json` adds 180 cases in 30 groups, including regional Vietnamese, teencode, Gen Alpha slang, multi-turn clarification, calendar boundaries and failure scenarios. The original flat catalog remains the default (`-Catalog Legacy`). Select the new pack explicitly:

```powershell
# No provider calls: validate catalog and load isolated database fixtures.
.\scripts\Test-NoraSandbox.ps1 -Catalog Conversations -Mode Offline -GroupId G01,G03
.\scripts\Test-NoraSandbox.ps1 -Catalog Conversations -Mode Offline -Suite All

# Opt-in configured model; group and case filters intersect when both are supplied.
.\scripts\Test-NoraSandbox.ps1 -Catalog Conversations -Mode Live -GroupId G10 -CaseId G10-01 -MaxCalls 5
```

Comma-separated IDs work in Windows PowerShell and pwsh. Unknown IDs and empty intersections are rejected before provider calls. Without IDs, `Smoke` selects G01-01, G03-01, G10-01, G21-01 and G23-01. Each case gets a fresh synthetic account, conversation and SQLite database. Its turns share state and use actual prior replies. Inherited fixture objects merge recursively; arrays replace rather than append. The runner resolves fixture IDs, clocks/timezones, active categories, tasks, events, sessions, exceptions, dependencies, generated large fixtures and seeded history.

**Offline `VALIDATED` is fixture validation, not a planning test or model pass.** It consumes zero interpreter calls. Live user-only `execution=chat` cases run through the real interpreter/service. They check outcome, read-only/clarification atomicity and generic scheduling invariants. A successful automatic check sequence ends in **`REVIEW_REQUIRED`**, because the pack's natural-language `expected.checks` still need a reviewer to assess exact intent, fields, scope and response quality. These checks are not translated into invented scripted answers. No expected answers or voice tags enter model context.

The 29 cases requiring API, harness, extended, fault, streaming or concurrency adapters remain **`NOT_RUN`**. Their actions and expectations are retained in the exported `catalog.json`; the runner does not execute a partial sequence and call it a pass. The 151 user-only chat cases are selectable for live execution. A failed prerequisite stops dependent turns. Provider failures are errors; the call cap and three-consecutive-error circuit breaker remain in effect. Full results, before/after snapshots, public responses and interpreted/stored commands are exported using the normal report files. No live run is triggered by ordinary unit tests.

The selected `catalog.json` preserves all manual review criteria and a `selection` section; original suite counts/hash are under `source_validation`. The actual input pack SHA-256 and runtime assembly hashes are recorded in `results.json`. Offline and live reports use schema 1.2 for this catalog, while legacy reports retain schema 1.1.

### Legacy sandbox catalog

From the repository root in PowerShell:

```powershell
# Default: no AI/network calls; scripted interpreter outputs through the real backend.
.\scripts\Test-NoraSandbox.ps1

# All catalog cases, with a cap high enough for the multi-turn sequences.
.\scripts\Test-NoraSandbox.ps1 -Mode Offline -Suite All -MaxCalls 150

# Actual configured model. Sends fictional fixtures and prompts, consuming provider quota.
.\scripts\Test-NoraSandbox.ps1 -Mode Live -Suite Smoke -MaxCalls 25

# Focused live evaluation; comma-separated IDs work in Windows PowerShell and pwsh.
.\scripts\Test-NoraSandbox.ps1 -Mode Live -CaseId A01,B04,D02,S03 -Repeats 3 -MaxCalls 30

# Full service-level live set. Stop at 120 interpreter attempts; no automatic unlimited retries.
.\scripts\Test-NoraSandbox.ps1 -Mode Live -Suite All -MaxCalls 120
```

`-Mode` defaults to `Offline`, `-Suite` to `Smoke`, `-Repeats` to 1 and `-MaxCalls` to 30. Repeats are bounded to 1–5; attempts to 1–500. `-TimeoutSeconds` defaults to 60. All provider calls are sequential. The runner stops attempting new scenarios after three consecutive infrastructure/provider errors. A call limit is **not** a token or monetary limit: different prompts and models can cost different amounts. Scripted/injected interpreter attempts also count toward the cap, so the number is an upper bound on live interpreter calls.

`-OutputDirectory artifacts/nora-sandbox/my-run` selects a new folder inside this repository. The script refuses to overwrite an existing `results.json`. By default it creates a unique run folder. Its isolated build output is inside that folder, so it does not overwrite DLLs used by the running API. Dependencies must already be restored; it uses `--no-restore` and skips the unrelated frontend build. If restore is needed, follow the repository's normal setup instructions first.

Normal `dotnet test` does **not** call the live model. The explicit script sets a process-local options-file variable only for its run and restores the previous value afterward. The opt-in test is skipped otherwise. No solution/project/package wiring was added.

## Read the export

Every run writes these UTF-8 files:

| File | Contents |
|---|---|
| `report.md` | Human-readable per-case prompts, replies, questions, automatic checks, actual database actions and local-time saved sessions/events. |
| `results.json` | Authoritative structured evidence: metadata, each step's result/error code, original interpreted intent, sanitized stored command, bounded model context/history, transport status/error type, synthetic snapshots before/after, actual field values for additions/edits/removals, latency and provider-supplied token counts when available. |
| `turns.csv` | One row per executed step for filtering/comparison. Full values remain in JSON. |
| `catalog.json` | Exact scenario catalog used by the run, including expectations, fixture names, operations and limitations. |
| `options.json` | Non-secret execution options, including case selection, mode and cap. |

Schema 1.1 separates a rejected operation's client-safe `errorMessage` and retained `persistedTurn` from a newly returned `result`. For earlier schema 1.0 files, `errorCode` takes precedence over a retained turn's APPLIED/PREVIEW status. Runtime assembly and catalog SHA-256 values identify the code/data actually used. CSV text is escaped to avoid interpreting model output as a spreadsheet formula; JSON preserves the original strings.

New results retain the useful public explanation in `reply` and the backend's verified save summary in `outcome`. Markdown labels the latter `Verified outcome`; CSV exposes it as `verifiedOutcome`. Review both against the actual database actions. A draft model reply is not evidence that a request was saved. A clarification about a category or explicitly qualified clock range can trigger one additional interpreter attempt with server-resolved facts; both attempts count toward the sandbox cap and normal application AI usage limits.

Reports checkpoint after each completed step/case. An interrupted process can leave a `RUNNING` case and partial evidence; those entries are not passes. A failed prerequisite stops the rest of that sequence, which is reported in the case reason. The CLI exits nonzero for FAIL/ERROR, while keeping all exports available. NOT_RUN entries require reading the reason: they can mean unsupported automation, cancellation, call cap or infrastructure circuit breaker.

Credentials, provider request URLs, HTTP headers, raw provider error bodies and exception messages are not exported. Structured model **public** output and fictional calendar context are exported for evaluation. Private model reasoning is neither requested nor recorded. Keep real personal data out of fixtures if you extend the catalog.

## What PASS means

The independent oracle does not call `PlanningEngine`, `FreeSlots` or the backend validator to decide whether the result is correct. It compares actual database changes and checks expected outcomes, requested task duration/deadline/priority/continuity, event times, effective free windows, overlaps, daily budgets, rest, dependencies, protected sessions and preservation of unrelated entities. Preview/clarification/read-only/error paths must not persist scheduling changes. Lifecycle cases also exercise retry/apply/undo/stale/expiry/category changes using the actual service.

An unusable provider response is **ERROR**, even when the calendar stayed unchanged. It cannot count as a correct clarification or a successful explanation of an infeasible schedule. The snapshot still shows whether writes occurred. Synthetic fault-injection cases can explicitly expect an error and are labeled by trace source.

`PASS` means **the listed automatic checks passed**, not that the model's Vietnamese reply is semantically perfect. The run records `responseReview=REVIEW_REQUIRED` only when a real model produced an interpreted result; scripted runs and unusable responses are `NOT_EVALUATED`. A reviewer must evaluate whether the question/explanation is justified, dates were understood correctly, unsupported capabilities were described honestly, and save claims match evidence. Task-name matching uses normalized substrings; an acceptable synonym can need manual adjudication.

`Traces[].source` distinguishes `Live`, `Scripted`, and `InjectedFailure`. H10 uses a controlled mixed clarification/write payload even in Live mode because it tests backend normalization, not live-model interpretation. Offline mode replays controlled intents through the real backend; it does **not** measure prompt understanding or model reliability.

## Coverage and limits

The [original 86-scenario prompt pack](../reviews/NORA_GUARDRAIL_AND_SCHEDULING_TEST_PACK_2026-10-19.md), plus incident regression S07, is represented in [the machine-readable catalog](../../tests/nora-sandbox/cases.json). This runner automates the **primary service-level variant of 81 scenarios**: 64 independent cases, 7 sequences and 10 lifecycle scenarios. H04 (coordinated concurrent requests), H11 (HTTP streaming/transport/UI fault behavior) and V01–V04 (browser/audio) are explicitly not automated here. They still appear in catalog/coverage output and become NOT_RUN when selected in All.

Some catalog entries automate one documented variant: D10 Running but not Paused; E09 weekly no-op; G01 the Vietnamese timezone; G05 task truncation; G06 seeded history rather than nine extra paid calls; H08 stale undo; H10 mixed intent. Their `limitation` fields identify remaining variants. Do not claim the whole family passed from one variant.

SQLite uses the existing test model's UTC timestamp conversion. It exercises application persistence and transactions but does not establish SQL Server locking/migration correctness, authenticated HTTP authorization, CSP, frontend error labels, speech recognition or network-disconnect recovery. These retain their separate integration/browser tests. Session arithmetic in this version targets the fixed, minute-aligned Asia/Ho_Chi_Minh fixtures; cross-midnight/DST variants need an extended oracle before being scored.

## Extend a case

Edit `tests/nora-sandbox/cases.json`: each case has `id`, `title`, `fixture`, `smoke`, `automation`, `limitation` and `steps`. A step has a prompt, a public service operation and expected results. Cases in a sequence use the same synthetic account/conversation. Supported fixtures and operation names are implemented in `tests/SPWPOS.TestSupport/NoraSandbox`.

Expectations include allowed statuses (`REJECTED` means a known backend rejection, not any provider failure), zero-write requirements, entity counts, task/event field checks, preserved entities, weekly slots and effective availability. `{TARGET}`, `{F}`, `{A}` and similar ID tokens are substituted from the fixture in prompts and expectations. No expected answer prose is sent to the model. Unknown case IDs or invalid execution options are rejected before calls.

When extending Offline coverage, add a controlled response in `ScriptedResponses.cs`; do not use its success as evidence for the live interpreter. Run `NoraSandboxTests` to verify the runner/oracle, then do a small bounded Live selection. Record code changes and preserve earlier run folders so comparisons remain auditable.

## Review with Codex

Ask Codex to evaluate a particular run's `results.json` and `report.md`. They remain in the workspace, so no upload is needed in this chat. For review elsewhere, share the report and JSON together. Request separate judgments for response quality, scheduling correctness, safety invariants, provider errors and remaining coverage. Reviewer findings should cite case/step IDs and actual before/after evidence rather than an overall unqualified success percentage.
