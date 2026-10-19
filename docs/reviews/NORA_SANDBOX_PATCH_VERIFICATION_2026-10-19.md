# NORA sandbox findings: patch and verification — 19 October 2026

The patch addresses the findings in the [original evaluation](NORA_SANDBOX_EVALUATION_2026-10-19.md). The full live rerun improved from **68 PASS / 12 FAIL** to **80 PASS / 0 FAIL**, with the same six scenarios outside automation. Response review then found two remaining explanation errors; these received additional fixes and targeted repeat checks. Automatic PASS is a bounded action/safety result, not a guarantee of response quality.

## Changes

| Finding | Change |
|---|---|
| B05: conflicting weekday and date | A server check compares adjacent Vietnamese weekday/date pairs containing a four-digit year. A mismatch produces a calendar-correct clarification and removes every proposed write, even when the model itself asks an incorrect question. |
| G01, D07, E03: UTC/local confusion | Context timestamps use account-local offsets, including saved deadlines, events, sessions and scope. An explicit local clock supplies today/tomorrow. A single relative-date event or exception is checked against the account calendar before writes. |
| A01, A02, S01, H02, H06: unnecessary questions | Instructions distinguish ordinary availability/preferences from exclusive work windows and classify ordinary work using active categories. Clear requests proceed to the deterministic planner without speculative feasibility confirmation. |
| D14, D05, D06, D09, F01: scheduling reasoning | Instructions delegate feasibility to the existing planner, distinguish per-day caps from total demand, account for breaks and completed/locked work, and allow multiple supported actions together. Backend limits and atomicity remain enforced. |
| C02: partial availability rejected | With `replaceWeeklyAvailability=false`, supplied slots replace the specified days and preserve every unmentioned day. Full replacement/clearing still requires the explicit replacement flag. |
| C07, S02: invalid event category | The provider schema constrains event types to MEETING, CLASS, APPOINTMENT and OTHER. Fixed study blocks use an event category rather than the STUDY task category. |
| E02, E06, E08, S05: lost rationale | Clarification retains explanatory sentences while dropping detected save/completion claims and definitive action promises. All accompanying commands are still removed. Settings changes direct the user to Settings; continuous work is distinguished from an exclusive date window. |
| G05: fabricated scope year/global guarantee | The backend supplies exact scope dates and counts. Read-only global requests over truncated context receive a server-generated limitation instead of an unverified global conclusion. |
| H12: preview-only consent | Explicit preview/no-save phrases disable automatic application for initial sends and request-ID retries, even when the auto-apply toggle is enabled. The user can still explicitly apply the verified preview. |

No database migration, dependency, planner algorithm change, or production calendar mutation was required. The running API must load the rebuilt source to use the patch; these sandbox runs do not deploy or restart it.

## Evidence and verification

| Run | Result | Evidence |
|---|---|---|
| Original full live evaluation | 68 PASS, 12 FAIL, 6 NOT_RUN | [JSON](../../artifacts/nora-sandbox/live-evaluation-2026-10-19/results.json) |
| Patched full live evaluation | 80 PASS, 0 FAIL, 6 NOT_RUN | [Readable report](../../artifacts/nora-sandbox/live-patched-2026-10-19/report.md), [JSON](../../artifacts/nora-sandbox/live-patched-2026-10-19/results.json), [CSV](../../artifacts/nora-sandbox/live-patched-2026-10-19/turns.csv) |
| Explanation/consent repeats: B05, E06, G05, H12, three each | 12 automatic PASS; B05 explanation required the final correction below | [Report](../../artifacts/nora-sandbox/live-final-targeted-2026-10-19/report.md), [JSON](../../artifacts/nora-sandbox/live-final-targeted-2026-10-19/results.json) |
| Final date correction: B05 and G01, three each | 6 PASS; correct weekday clarification and October 28 event date in every repeat | [Report](../../artifacts/nora-sandbox/live-date-final-2026-10-19/report.md), [JSON](../../artifacts/nora-sandbox/live-date-final-2026-10-19/results.json) |
| Full offline backend verification | 80 PASS, 0 FAIL, 6 NOT_RUN | [Report](../../artifacts/nora-sandbox/offline-final-2026-10-19/report.md), [JSON](../../artifacts/nora-sandbox/offline-final-2026-10-19/results.json) |

The full live rerun used VertexAI / gemini-3.5-flash-lite: 93 interpreter attempts, including one scripted mixed-intent probe and one injected provider failure. Provider-reported tokens: 357,885 input, 19,700 output. Follow-up runs are separate and retain their own runtime fingerprints; the final minor changes were verified with targeted live repeats rather than claiming the earlier full live run used the final binary.

Regression tests reproduce preview-only retry/application, contradictory weekday/date output (including an incorrect model clarification), the local-midnight boundary, partial weekly updates, local deadline serialization, safe clarification explanations, truncated global answers and invalid event categories. Each new behavior was observed failing before its production fix. Tests use the real service and SQLite database with the interpreter replaced at the external AI boundary; they do not pin prompt wording. Two HTTP workflow fixtures were corrected to derive tomorrow from the account's Vietnam calendar, rather than UTC/the machine timezone.

The final full unit suite passed **280 tests**, with the opt-in sandbox test skipped. The HTTP/integration suite passed **98 tests**, with **13 SQL Server tests skipped** because their separate environment was unavailable. The integration run preceded the final clarification-only date correction; final unit tests exercise that correction through the real service/database. `git diff --check` passed.

## Response review

- D07 now schedules both dependent tasks; D14 allocates the work over two dates. A01/A02 complete, S01 reaches its full workflow, and S02 creates the agreed fixed study event. H02 reaches retry and H06 reaches expiry validation.
- D05 and D06 now reach the planner and return verified minute shortfalls instead of an already-passed-deadline claim or category question. Their generic capacity warnings do not yet explain every limiting resource separately.
- E03 now reports the actual local overlap, 19:00–20:00. E02 and S05 retain the explanation that the referenced work was never saved.
- The full live rerun still printed a wrong scope year in G05 and misleading advice in E06. The subsequent scope override returns the verified 2026 range in all three repeats; E06's three repeats direct the user to Settings without suggesting that task flexibility bypasses the session limit.
- Two B05 repeats asked a question without saving but stated incorrect weekdays. The final server check now also corrects model clarifications, rather than running only when the intent contains writes. Earlier exports remain unchanged as evidence.
- H12 produces an actual preview with no task/session writes; the offline adversarial intent and unit tests verify that this does not depend on model compliance.

## Remaining scope

The deterministic date checks are deliberately limited to recognized adjacent weekday/date pairs and a single unambiguous relative event/exception. They are not a general natural-language date parser; complex multi-date clauses and task deadlines still depend on interpretation plus backend validation. Preview consent detection covers explicit Vietnamese/English phrases, not every possible paraphrase.

Task-specific exclusive work windows and changing preference settings through chat remain unsupported. These are explained to the user rather than silently substituted. Six catalog scenarios (H04, H11, V01–V04) remain outside this runner. Browser rendering, streaming intermediate wording, voice, SQL Server concurrency and production reliability were not established by these runs. The final saved service response and persisted actions are the reviewed outputs; raw model traces can still contain inaccurate or premature prose.
