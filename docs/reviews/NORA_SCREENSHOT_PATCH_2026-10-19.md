# Nora screenshot regressions, 19 October 2026

The patch was developed in `codex/nora-guardrails-integration` and has now also been applied to the main workspace, `C:\Users\Admin\source\repos\SPWPOS`, on its existing `codex/partial-agent-guardrails` branch. It has not been deployed or applied to the running application's database.

| Finding | Change |
|---|---|
| The explicit range “từ 7h đến 12h sáng” prompted another AM/PM question | The instructions recognize this same-morning range. If the model still asks, the service provides server-resolved minutes in one bounded, metered recheck. Reversed, unqualified or multi-clock requests remain questions. An unresolved second response is not applied. |
| WORK was unavailable or silently saved as OTHER | WORK is supported in task catalogs, fixed events, provider contracts and UI selections. A single task's explicit category directive is resolved against the active catalog even if the model selects a different category. The additive catalog migration is required for existing databases. |
| An unsupported category caused an unnecessary selection question | Unsupported agent categories fall back to active OTHER. A category-only question can receive one recheck with the resolved category. Explicit no-substitution requirements still clarify; this flow cannot create categories. |
| Editing one event moved a valid, unrelated session | An event operation cannot trigger a general replan unless the request explicitly asks for one. Existing task IDs are restricted to tasks actually named in the request. The normal backend still reschedules genuine conflicts. |
| Fixed-event overlap appeared as a connection interruption | The error identifies the conflicting event and its local time window. The frontend removes rejected provisional text, displays a schedule-conflict status, and reconciles saved history. A connection failure recovered as APPLIED clears the failure banner. |
| The useful response was replaced after saving | Useful public explanation stays in `reply`; authoritative applied details are stored separately in `outcome` and displayed beneath it. Premature model save claims are filtered. History, clipboard and sandbox exports retain both fields. |

The verified outcome lists actual category codes, changes and newly scheduled or moved sessions. It does not treat the model's proposed prose as proof of a successful save. Preview and undo outcomes remain distinct from applied outcomes. No extra schema migration is needed for the optional outcome field in stored turn JSON.

## Validation

The full backend run passed 363 unit tests (one opt-in sandbox skip) and 103 integration tests (16 SQL Server environment skips). Regression coverage includes ambiguous and reversed ranges, an additional clock such as `8h30`, and a morning range extending past noon. SQL Server migration execution remains unverified locally; the migration SQL and model consistency were checked separately.

All 236 frontend tests and four browser session-refresh tests passed, and the production frontend build succeeded. The initial unrestricted frontend run timed out in login/note tests during concurrent backend work. The complete rerun passed with two frontend workers without changing assertions or test timeouts.

The final offline sandbox passed 81 scenarios; six outside this runner's coverage were NOT_RUN. Evidence is retained under `artifacts/nora-sandbox/screenshot-patch-final-offline-2026-10-19/`, including `report.md`, `results.json` and `turns.csv`. A subsequent reply-filter adjustment preserves acknowledgements beginning “Mình đã hiểu”; its regression is included in the final unit run. Offline runs control the external interpreter and verify application behavior; they do not establish live model reliability. This patch has not received a fresh live-provider evaluation. Historical live S07 results are documented in the separate WORK incident review.

## Deployment and retest

Deploy the integration build and its frontend assets together. Apply the pending reviewed database migrations, including `20261019015527_AddWorkCategory`; if WORK was previously configured, its label and disabled status are preserved. Existing OTHER records are not automatically reclassified. A catalog with disabled OTHER cannot accept a fallback to it.

The authenticated capabilities endpoint reports `policyVersion: 2026-10-19.4` for the latest response presentation changes in the main workspace. Confirm that version and an active WORK catalog entry after deployment, then repeat the screenshot sequence in a test account. The expected event edit preserves a valid 17:00–17:30 checklist session, and a 14:00–15:00 meeting overlapping the work event is rejected without writes.

## Follow-up: numeric clarification after rebuilding

The follow-up screenshot offers “07:00 đến 12:00 hay từ 19:00 đến 24:00” without the words sáng/tối in its question. The original recovery trigger missed this form. A service regression reproduced NEEDS_INPUT instead of APPLIED; the correction recognizes an explicit either/or choice between the request's already-qualified start time and its twelve-hour alternative. It still requires a safely parsed, explicit range in the user's request. The bounded, metered recheck and all later scheduling validation remain in place. All 47 focused Nora quality regressions passed after this correction; this follow-up was not tested against the live provider.

Separately, the local process was rebuilt at 18:13 from `C:\Users\Admin\source\repos\SPWPOS\src\SPWPOS.Api\bin\Debug\net8.0\SPWPOS.Api.exe`. That checkout remains on `codex/partial-agent-guardrails`, commit `56cbff7`, and does not contain the recovery implementation. The patch remains in `C:\Users\Admin\.codex\worktrees\nora-guardrails-integration\SPWPOS`. Rebuilding the original checkout cannot include these uncommitted worktree changes. No running server was stopped or restarted during this follow-up investigation.

The integration draft's previously recorded D14 multi-day clarification and E08 intermittent invalid model-window findings remain open. This patch does not merge or deploy the draft.

## Main workspace transfer

The Nora implementation, frontend handling, regression tests and required scheduling/category migrations were transferred into the normal checkout. Only the task-window and availability projections were ported into the shared workspace mapper; unrelated authentication and converted-note editing changes from the integration branch were excluded. The notes category selector receives WORK without changing its editing flow. No branch switch, commit or push was performed.

The main workspace keeps the user's existing `global.json` pin of 8.0.400. Its 261 pre-existing user files, including the presentation assets/output and untracked calendar layout test, were verified unchanged with SHA-256. Transfer backups and a manifest are retained under `artifacts/nora-workspace-transfer-20261019-182213/`.

The complete backend checks in this checkout passed 360 unit tests (one opt-in sandbox skip) and 102 integration tests (16 SQL Server environment skips). All 238 frontend tests, including the user's pre-existing calendar layout tests, and four browser session-refresh tests passed. The frontend production build passed. EF reports that the model matches the latest migration. The differences from the integration worktree's counts reflect the excluded unrelated changes and the local calendar tests.

A read-only schema check reports two pending migrations. Rebuilding source alone does not apply them: `20261018172132_NoraSchedulingConstraints` adds the task-window and dated availability columns, and `20261019015527_AddWorkCategory` adds the WORK catalog entry. An idempotent SQL script for those two migrations was generated at `artifacts/nora-main-workspace-upgrade.sql` without execution. Apply them through the normal reviewed development upgrade process before using the rebuilt application. `scripts/Start-Development.ps1` includes database update after it checks that the old API has stopped. No migration or server restart was performed by this transfer.

### Debug startup build follow-up

The next development startup failed before applying migrations. An ordinary Debug infrastructure build reproduced eight CS1061 errors: the application compiler could not find WorkWindowStart, WorkWindowEnd or OverridesWeeklyAvailability. The fields were present in source, but copying had preserved a source timestamp older than the existing Debug Domain reference assembly, allowing incremental compilation to reuse that older assembly. Release verification had not exercised this existing Debug cache.

A forced Debug API rebuild (`dotnet build src/SPWPOS.Api/SPWPOS.Api.csproj --no-restore -m:1 -t:Rebuild -p:SkipFrontendBuild=true`) succeeded with zero warnings/errors. A subsequent EF migration-script command using its normal Debug build also succeeded. The script was exported to `artifacts/nora-startup-debug-upgrade.sql`; neither database update nor API startup was performed during these checks. No source workaround or migration change was needed.

## Response presentation follow-up

The next screenshot sequence showed correct reported scheduling, WORK categorization, OTHER fallback and overlap rejection. It also showed missing conversational openings for several successful actions, verbose technical defaults, and a duplicated conflict message. The response improvements are applied directly to the main workspace.

Useful public explanations are retained. If filtering removes the entire model reply, a short preview acknowledgement is generated from the proposed changes; after a successful save it becomes a confirmation based on the applied changes. No additional AI call is needed. Model claims such as “đã lên lịch” are also filtered before save. Preview results do not claim that work has been saved.

Verified outcomes use readable Vietnamese labels such as Công việc, Lớp học and Khác. Task summaries omit the default priority number, the hard-deadline implementation marker and the default permission to split sessions. A nondefault priority and a continuous-work requirement remain visible. Actual stored category codes, priorities, durations, deadlines and scheduling commands are unchanged.

An unavailable requested category produces a short substitution notice near the response, including when the interpreter already returned OTHER. An active category that the model incorrectly selected is not falsely described as unavailable. When history contains the finalized failed request, the frontend keeps its actionable conflict message in the conversation and clears the duplicate global error banner. If history cannot be loaded, the error banner remains available.

The full backend suites passed 363 unit tests (one opt-in sandbox skip) and 102 integration tests (16 SQL Server skips). All 238 frontend tests and four browser session-refresh tests passed, and the frontend production build passed. Regression tests cover preview versus applied confirmations, retained explanations/history, category-substitution notices, preservation of stored defaults, and one visible conflict message both with and without history recovery. This response-only change adds no database migration and does not rewrite historical turn content or restart the running API.
