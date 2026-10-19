# NORA afternoon work-shift rejection, 19 October 2026

Current implementation: WORK is supported for tasks and fixed events; unavailable agent categories fall back to OTHER unless the user explicitly forbids substitution. The patch is local to the integration worktree. The catalog migration and updated build have not been deployed.

The screenshot request is valid: tomorrow's class is 07:00–12:00 and the requested work shift is 13:00–17:00. These fixed events do not overlap and do not require task-duration or deadline clarification.

## Diagnosis

The local API process was running `src/SPWPOS.Api/bin/Debug/net8.0/SPWPOS.Api.exe`, started on 18 October. Its infrastructure assembly was built on 18 October at 19:20. Its event schema only specified `eventType` as a string, without the enum restriction present in the current guardrail source.

A read-only query restricted to the exact afternoon prompt and 19 October found repeated FAILED turns with the screenshot's message. Their `CommandJson` was empty because the failed transaction rolled it back. The historical payload therefore cannot be recovered from those rows.

With the user's explicit approval, two live synthetic reproductions used a copy of that running build, a frozen clock of 19 October 08:26 +07:00, and a synthetic morning class. Both returned:

```json
{"name":"Đi làm","start":"2026-10-20T13:00:00+07:00","end":"2026-10-20T17:00:00+07:00","description":"","eventType":"WORK","repeatWeeks":1,"existingId":null}
```

The running and diagnostic-copy infrastructure DLLs had identical SHA-256:

`CF226EE0D20892911306460248DFE8683B369119DC73091286B0EFA14B1F4116`

`EventOccurrences` permits MEETING, CLASS, APPOINTMENT and OTHER. WORK is rejected by the same combined check as invalid recurrence counts, hence the misleading “Sự kiện hoặc số tuần lặp không hợp lệ.” message. The reproduced dates and repeatWeeks were valid. This establishes a repeatable cause matching the incident; the unavailable historical payload remains a limitation.

## Initial workaround and evidence

The initial workaround restricted event categories in both the provider schema and local shape validator, and mapped fixed work shifts to OTHER. Its verification below is historical. The requested final implementation adds WORK as a built-in category and retains a narrow fallback for unsupported categories, as described below.

The initial interpreter regression rejected WORK. The final contract now preserves WORK, normalizes genuinely unsupported categories to OTHER, and still rejects malformed category values. A real SQLite/service regression saves the morning class followed by the afternoon shift, checks both exact time ranges and categories, preserves the morning event ID, and verifies that no flexible tasks or sessions are created. Only the external AI is controlled in that unit test.

Sandbox sequence S07 contains the exact two screenshot messages. Its standard frozen clock is 26 October, so tomorrow is 27 October in those runs; the old-build reproduction above uses the screenshot date. The catalog now has 87 scenarios, of which 81 have service-level automation.

## Initial workaround verification and response review

- Focused interpreter/service regressions: 6 passed.
- Full unit suite after the prompt change: 308 passed, 1 opt-in sandbox test skipped.
- Full offline sandbox: 81 PASS, 6 NOT_RUN; scripted outputs verify backend actions and do not measure model interpretation.
- Live S07: all 3 two-turn repetitions passed, 6 interpreter attempts. Each morning event was CLASS and each afternoon event OTHER. Both turns were APPLIED; the second turn preserved the first event, and neither created flexible work.
- Human review of all six public responses: the replies accurately list the saved date and time range. No clarification, unsupported completion claim, or request to reduce the four-hour fixed event was present. “Không có phiên làm việc cần chuyển.” accurately reflects the empty synthetic work-session state.

The live results are bounded evidence for this scenario, not a general model-reliability claim. They do not resolve the integration draft's separate D14/E08 findings.

Full JSON, CSV, runtime fingerprints and readable reports are retained under `artifacts/nora-sandbox/work-shift-incident-live-2026-10-19/`. The full offline rerun is retained under `artifacts/nora-sandbox/work-shift-incident-offline-2026-10-19/`. Earlier run folders are preserved.

## Running application

The running API was not rebuilt, restarted, or switched to the integration branch. No real calendar changes, database migration execution, deployment or Git push were performed for this investigation. The source patch and tests are local changes in the integration worktree. The live app will continue using the older schema until an appropriate reviewed build is deployed or restarted.

## Final requested behavior: WORK plus category fallback

WORK is now a built-in category for tasks and fixed events. Task creation, the API event contract, fixed-event validation, Nora's schema/rules, note analysis, task labels, the note review dropdown and calendar event selection support it. Fixed work shifts remain events with exact start/end times; flexible professional work remains a task with duration and deadline requirements.

| Agent category input | Result |
|---|---|
| Active supported WORK or another supported category | Preserve it |
| Unsupported/inactive category on a task | OTHER, subject to OTHER being active |
| Unsupported category on an event | OTHER |
| Missing/null/blank category on a new item | OTHER |
| Missing/null/blank category on an edit | Preserve the owned entity's saved category |
| Number, boolean or object instead of a category label | Reject the malformed AI response |
| User explicitly requires an unavailable category and forbids substitutes | Clarify without saving or creating a category |

Normalization is limited to category fields before strict AI response validation. The service repeats category resolution against its current catalog before storing the command. All ownership, required detail, date, work-window, recurrence, overlap, protected-session, stale-preview and atomicity checks remain in force. Disabling a category after preview still blocks applying that preview; a fresh request is required. Manual form/API requests retain strict category validation.

Migration `20261019015527_AddWorkCategory` adds active WORK to existing task catalogs only when absent. It preserves a pre-existing WORK definition, label and disabled status. Rollback leaves category data in place because tasks and estimation history may reference it. Existing OTHER records are not reclassified.

Verification before the final sandbox runs: **349 unit tests passed** (1 opt-in skip), **102 integration tests passed** (16 SQL Server skips), and **232 frontend tests plus 4 browser session-refresh tests passed**. The frontend build passed. EF reports no pending model changes, and the upgrade SQL was generated for inspection without applying it. SQL Server migration-execution tests are present but skipped without their separate test environment.

The sandbox oracle now checks requested event categories in addition to exact times; a regression proves that saving a WORK event as OTHER fails that check. S07 expects CLASS at 07:00–12:00, then WORK at 13:00–17:00, with no flexible tasks or sessions created. The first offline run had 80 PASS and one F07 failure because fallback overrode an explicit no-substitution request. That run is retained under `artifacts/nora-sandbox/work-category-fallback-offline-2026-10-19/`. The added source guard clarifies F07 even if the model already substituted OTHER. Fresh evidence is retained under `artifacts/nora-sandbox/work-category-fallback-offline-final-2026-10-19/` and `artifacts/nora-sandbox/work-category-fallback-live-2026-10-19/`.

Final sandbox results: **81 offline PASS, 6 NOT_RUN**; **3 live S07 repetitions PASS, 6 AI calls**. Every live sequence saved CLASS followed by WORK, preserved both requested time ranges, and created zero flexible tasks or sessions. Review of all six public replies confirmed the correct dates/times and an accurate statement that no work sessions needed moving. These results do not resolve the draft integration's separate D14/E08 findings.
