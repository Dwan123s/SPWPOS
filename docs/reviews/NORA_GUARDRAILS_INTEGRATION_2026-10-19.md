# NORA guardrails integration — 19 October 2026

This branch integrates the safeguards from `56cbff7` into main's scheduling implementation at `bbb93ef`, then rebases cleanly onto the authentication update in `bbe595f`. The backend and offline checks pass. Live evaluation still identifies response-quality failures, so this change is prepared as a **draft for review, not ready to merge**.

## Integration decisions

- Preserve main's persisted task work windows, pending clarification state, evidence fields, result kinds, exclusive date availability, migration, planner/execution checks and server-grounded deadline answers. No additional migration or frontend behavior change is introduced.
- Port preview-only consent enforcement for send/retry, weekday/date and account-local relative-date guards, local context timestamps, event-type enums, partial weekly updates, sanitized failed history, safe clarification explanations and truncated-context notices.
- Keep one validation flow: shape validation, normalization with pending evidence, date consistency checks, final result-kind/action validation, deterministic planning, then revision/constraint checks at apply time.
- Retain main's reuse of equivalent sessions. Correct a discovered interaction: a plan blocked by an unscheduled dependency must remain BLOCKED even when it proposes no new sessions. It must not be called an already-correct schedule.
- Recover source evidence only within bounded, explicit forms. A duration can be taken directly after the exact task name when the model's quoted spelling is invalid; a verbatim deadline time can use its preceding deadline label in the same clause. Available hours and work dates do not become task duration/deadline evidence. Missing or ungrounded details still ask a question.
- Supply the actual account-local week boundary and per-date calendar capacity. These are context facts, not permission to bypass the planner or change limits.

## Sandbox changes

The catalog still contains 86 scenarios and 80 automated primary service variants. E08 now requires successful scheduling within Thursday evening. S02 now asks for the missing duration, creates the task inside Thursday evening after clarification, then verifies saved sessions without further writes. Earlier reports retain their historical unsupported-work-window expectations.

The independent oracle checks task-window boundaries and date-specific availability overrides in addition to the existing scheduling invariants. Regression tests prove that out-of-window work and additive hours wrongly retained under an exclusive date override are detected. The oracle does not call the planner to decide whether a produced schedule passes.

## Verification

| Check | Result |
|---|---|
| Imported guardrail regressions against main before porting fixes | 18 failed, 51 passed in the selected test run |
| New work-window/availability oracle regressions before fixes | 2 failed, 4 passed |
| Final rebased unit suite | **306 passed**, 1 opt-in sandbox test skipped |
| Final HTTP/integration suite | **100 passed**, 14 SQL Server tests skipped |
| Frontend | Build passed; **231 tests + 4 browser session-refresh tests passed** |
| Final full offline sandbox | **80 PASS**, 6 NOT_RUN |
| Initial combined live run | 61 PASS, 19 FAIL, 6 NOT_RUN; most failures were valid evidence quotes rejected before source-grounding correction |
| Full live rerun after source-grounding correction | **77 PASS, 2 FAIL, 1 ERROR**, 6 NOT_RUN |
| Final focused live repeats: D14, E08, G02, three each | **5 PASS, 3 FAIL, 1 ERROR**; breakdown below |

The final focused run uses the week/capacity facts added after the full live rerun. These runs are separate samples with separate runtime fingerprints; their counts are not combined into a new 80-case success claim. The unit, HTTP, frontend and offline checks were repeated after rebasing onto `bbe595f`; the Nora source did not change during that rebase, and live calls were not repeated for the unrelated authentication update.

HTTP fixtures now derive tomorrow from the account's local calendar and advance their frozen clock to the actual planned session before testing timer operations. The isolated worktree's frontend assets were built for static-asset HTTP tests; missing assets were not treated as a production defect.

Local verification used the available .NET SDK 8.0.401 with a temporary worktree-only SDK override. The committed `global.json` requirement remains main's 8.0.425. Frontend checks passed on installed Node 24.13.0, although npm warned that the declared minimum is 24.15.0. No toolchain requirements or lockfiles were changed.

## Remaining live findings

| Case | Latest evidence | Required follow-up |
|---|---|---|
| D14: 180 minutes over two days, 120 minutes/day, 60-minute sessions and 15-minute breaks | All three focused repeats incorrectly ask to reduce time or extend limits. The model emits no task, so nothing is saved. The offline backend fits 60 minutes Monday 19:00–20:00, 60 minutes Monday 20:15–21:15 and 60 minutes Tuesday 19:00–20:00. | The model still decides whether to ask about feasibility before handing an intent to the planner. This needs a bounded planner-feedback/intent-extraction design; further prompt changes alone have not established reliability. |
| E08: explicit Thursday-evening task window | Two focused repeats save the correct bounded task; one returns AGENT_AI_INVALID_RESPONSE after an HTTP 200 provider response. No changes are saved on that error. The specific malformed field is not captured by the sanitized export. | Diagnose the invalid structured output and add a contract regression before claiming reliable live support. |
| G02: Monday next week | Failed in the full rerun, then **passed all three** focused repeats after the server supplied nextWeekStart. | Retain the date anchor and regression coverage. Three samples are bounded evidence, not a production reliability estimate. |

The draft should remain unmerged until D14 and E08 have an agreed resolution and fresh live evidence. Six catalog scenarios remain outside this runner (H04, H11, V01–V04). SQL Server concurrency/migration checks need their separate environment. Streaming intermediate wording, voice and production behavior are not certified by these runs.

## Evidence

[Committed run summaries](../evidence/nora-integration-2026-10-19/summary.json) and [focused readable report](../evidence/nora-integration-2026-10-19/live-targeted-report.md) make the remaining failures reviewable without provider access. The committed CSVs contain public replies, questions and actual action counts for the full rerun and focused repeats.

Full synthetic snapshots, commands, contexts, hashes and reports remain in this worktree under `artifacts/nora-sandbox/`: `integrated-offline-final-2026-10-19`, `integrated-live-grounded-2026-10-19` and `integrated-context-targeted-2026-10-19`. Earlier failed runs are retained. No real calendar, credentials, provider raw bodies or private model reasoning are included in committed evidence.
