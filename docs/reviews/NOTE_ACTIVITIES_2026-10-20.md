# Notes: mixed events and tasks — 20 October 2026

The screenshot regression came from the Notes extraction contract, which explicitly excluded appointments and returned only `TaskInput` records. The missing start/end fields also converted the report's 20:30 start into a flexible task with suggested duration/deadline. Nora chat uses a separate interpreter.

## Changes

- New Notes analyses include every recognized activity as an event or task, preserving explicit times. Missing details remain nullable and visible for review. Legacy task-only analyses remain readable.
- School, work shifts and meetings are fixed events. Tasks retain optional start/window constraints. A task without a stated duration requires user input rather than an invented hour.
- Preview persists a proposal containing drafts; it creates no events, tasks or sessions. Note-specific confirmation validates ownership, note version, calendar revision, expiry and feasibility, then saves all selected activities atomically.
- Confirmation is idempotent. The generic plan apply endpoint rejects these drafts so it cannot save sessions without creating their tasks/events. Matching existing events are reused.
- Review supports selection, inline editing, missing-detail prompts, availability setup, warning display and a final confirmation. Failed confirmation preserves edits and permits another preview.
- Calendar preview variants are namespaced to avoid the existing Tailwind `.fixed` collision that stacked fixed-event rows on top of one another (N01 in the 18 October Notes review).

No database migration or dependency installation is required. Changes are in the main workspace; the running API must be rebuilt/restarted to use them. Old analysis results must be analyzed again.

## Validation

- Full unit suite: 376 passed; one existing opt-in Nora sandbox skipped.
- Notes/API and impact integration tests: 11 passed, including real HTTP preview/confirmation/retry with SQLite persistence.
- Full frontend suite: 279 passed. Notes/impact tests were repeated after the calendar-preview CSS correction.
- Provider adapter tests use controlled OpenAI, Gemini and VertexAI responses to verify the mixed activity contract. They do not establish live model extraction quality.
- Browser checks use the actual Notes components/CSS with synthetic data; no live AI requests or account-calendar writes. Desktop and 390 px layouts checked for readable event rows and horizontal overflow.
- Frontend production bundle built into `artifacts/notes-build/app`; the running development app's files were not replaced during verification.

Evidence: [review](../evidence/notes-activities-2026-10-20/review-desktop.png), [proposal](../evidence/notes-activities-2026-10-20/proposal-desktop.png), [phone proposal](../evidence/notes-activities-2026-10-20/proposal-mobile.png).

## Limits

AI still interprets natural language; users should verify the reviewed date/time before saving. Availability and task dependencies written in prose require separate setup and acknowledgment. Infeasible windows or missing details block confirmation. This adds initial conversion of a note, not synchronization of subsequent edits to already-created calendar activities.

## Follow-up: analysis failure at 19:36

The user reported `NOTE_AI_UNAVAILABLE` before reaching review and requested offline diagnosis. The exact live response was not inspected; no successful diagnostic provider request was made.

Offline regressions confirmed that the original parser rejected the entire note for an unused event estimate of zero, a local timestamp without an offset, zero representing no weekly repetition, or null optional description/question text. These variations now retain the activities. Event estimates are ignored; missing/invalid task duration remains unset and must be supplied. Local ISO timestamps use the account timezone, never the host timezone; invalid or ambiguous event times require review. Invalid nonempty task time constraints remain rejected rather than being silently removed.

Schema guidance now specifies timestamp offsets, null event estimates and valid repeat/priority ranges. Remaining validation failures log only a contract field name, such as `activities.start` or `activities.repeatWeeks`; provider bodies, note text and credentials are not logged.

A workflow regression exercises the real interpreter with a controlled provider response containing three events and one incomplete task, then verifies SQLite preview/confirmation creates all four at the stated times after supplying duration. Live extraction remains unverified at the user's request.

Follow-up validation: full unit suite 386 passed and one opt-in sandbox skipped; Notes/API integration suite 11 passed. Whitespace checks passed. No frontend source change was required for this parser correction.

## Follow-up: activity editor collapses while entering duration

The editor's `open` attribute depended on validation. Typing `3` kept a missing-duration task open; typing the next digit to make `30` cleared the validation error and removed `open`, collapsing the focused editor. Activity expansion now has its own state, initialized open for incomplete activities and updated by the user's disclosure toggle. Validation updates no longer close the editor.

Regression coverage types the two digits, checks the editor stays open and focus remains on duration, edits the deadline, and checks manual collapse/reopening. All 32 Notes tests and the isolated frontend production build passed. A browser check with synthetic data confirmed the editor remains expanded with duration `30` focused and the optional deadline visible. [Screenshot](../evidence/notes-activities-2026-10-20/editor-stays-open.png).

## Follow-up: current validation replaces stale AI questions

The review previously displayed the original AI question whenever any validation issue remained. This could ask for duration in English even after 30 minutes had been entered, while the actual issue was a 10:45 AM deadline before the 20:30 start.

Validation now produces field-specific Vietnamese messages from the current values, with an error under the affected input, a visible border/background and an `aria-describedby` association. The deadline warning updates when the deadline or duration changes and disappears once corrected. A valid duration is not marked invalid for a deadline conflict. Deadline equality with the execution end is accepted, and it remains optional when a fixed start plus duration defines the execution window. Editor expansion stays independent of validation.

All 33 Notes tests passed. Browser verification with synthetic data confirmed the deadline field is highlighted for 10:45 AM, the obsolete question is absent, and changing to 10:45 PM clears the warning and enables preview. [Screenshot](../evidence/notes-activities-2026-10-20/deadline-field-error.png). No account calendar changes or live AI requests were made.

Pre-push verification: all 282 frontend tests across 34 files passed, plus four browser session-refresh tests. The latest production frontend build passed using the isolated artifact output. Backend validation remains 386 unit tests passed (one opt-in sandbox skipped) and 11 Notes/API integration tests passed; no backend source changed after those checks.
