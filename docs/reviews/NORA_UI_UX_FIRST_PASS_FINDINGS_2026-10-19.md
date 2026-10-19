# Nora UI/UX — consolidated first-pass findings

Compiled: 19 October 2026.

Source: `C:/Users/Admin/Downloads/nora-ui-ux-evaluation.md`, titled **NORA (Trợ lý lập lịch): UI/UX Evaluation**. This compilation preserves the evaluator's observations and recommendations without treating the document as implementation instructions. No application changes were made for this compilation.

## Scope and confidence

The evaluation covers **one static screenshot** of the three-panel Nora screen. It combines an expert heuristic review with a **simulated** first-time-user walkthrough. It does not include interviews, observed usability sessions, live interactions, accessibility measurements, or source inspection.

The reported **approximately 6.5/10** is a subjective first-impression assessment of that screenshot, not a measured score for the current implementation. Statements about response speed, correct request interpretation, persistence, synchronization, or absent functionality cannot be established from this evidence alone.

The strongest overall finding is a **trust and comprehension gap**: users need to understand what was saved, where it appears, and how to correct it. The visual foundation was viewed positively.

## Positive findings to preserve

- Friendly visual identity, calm blue palette and approachable mascot.
- Clear separation between conversation history, chat and plan information.
- Natural-language input that makes the starting action obvious.
- Saved-status badges use both text and color.
- Sidebar reassurance about account/data ownership.

These are visual impressions; they do not establish measured contrast, accessibility compliance or successful task completion.

## Consolidated finding register

Priority reflects the impact described by the evaluator, not a confirmed engineering defect. **High** items affect confidence or control; **Medium** items affect comprehension, discoverability or usability; **Low** items are optional polish pending testing.

| ID | Priority | Consolidated finding | Evidence and uncertainty | Proposed next action / acceptance check |
| --- | --- | --- | --- | --- |
| NUX-01 | High | Chat and right-hand plan appear inconsistent. Chat confirms class 07:00–12:00 and work 13:00–17:00; the panel shows a different 17:00–17:30 session. | Visible content differs. It is unknown whether this is a stale panel, a selected older result, or a distinction between fixed events and flexible sessions. Data loss or synchronization failure is not proven. | Reproduce with known data. Define whether the panel shows the whole day, the latest request, or a selected plan. Label its scope/date and verify its entries against the persisted calendar. A user must be able to explain why any item is included or absent. |
| NUX-02 | High | Confirmation repeats the same outcome in prose, a change log and a card. | Repetition is visible in the reviewed screenshot. Some explanation may still carry useful rationale. | Consolidate repeated facts into one authoritative result card; retain only explanation that adds meaning. Show outcome/status and recovery actions once without losing failure or partial-result details. |
| NUX-03 | Medium | Internal category codes such as `[CLASS]` and `[WORK]` add technical noise. | Codes were visible in the reviewed screenshot; current response formatting needs comparison. | Use localized category labels only when useful; keep internal enum values out of normal user-facing summaries. |
| NUX-04 | Medium | Full dates repeat at both ends of a same-day range. | Visible formatting issue. Cross-day ranges still need explicit dates. | Show the date once for same-day results and a compact time range. Keep both dates for overnight/multi-day results. |
| NUX-05 | High | Undo and schedule editing are not discoverable in the reviewed state. | Absence from a screenshot does not prove absence from the product. Undo is already present in previously reviewed Nora states. Editing a submitted prompt and editing a saved event are different actions. | Test eligible and ineligible undo states. Make Undo visible when supported and explain unavailable/expired actions. Define a clear way to adjust a saved event's title/time; do not label prompt resend as direct event editing. |
| NUX-06 | High | Conversation deletion needs an understandable confirmation and clear scope. | The screenshot alone cannot reveal confirmation behavior. The recommendation to move deletion into a menu differs from the user's later explicit preference for a top bin icon plus a menu action. | Retain the user's requested icon-only top bin and menu entry. Both should show the shared centered dialog, actual chat title and permanent-delete warning. Confirm that cancelling sends no deletion request and failure retains the dialog. |
| NUX-07 | High | Event, session, plan, schedule and work-calendar terminology lacks an obvious relationship. | The screenshot uses multiple terms; they may represent legitimately different concepts. | Agree a small glossary and apply it consistently. Preserve meaningful distinctions: a fixed event, a flexible task, a scheduled work session and the complete calendar are not necessarily interchangeable. Clarify the right-panel heading using that glossary. |
| NUX-08 | Medium | The user cannot easily see the whole day and free gaps in context. | A text-focused screenshot demonstrates limited day context, not the absence of a calendar elsewhere. | Explore a day-at-a-glance timeline with fixed events and work sessions clearly distinguished, date/timezone visible, and an easy path to the full calendar. Validate whether it resolves NUX-01 before committing to a redesign. |
| NUX-09 | Medium | Opportunities for lunch/travel buffers are not surfaced. | This is an enhancement proposal, not a demonstrated scheduling failure. An open gap does not prove the user wants lunch or travel scheduled. | Consider optional, context-aware suggestions. Suggestions must not create or move entries without the user's action. Avoid repeated or irrelevant prompts. |
| NUX-10 | Medium | Other capabilities—viewing a day, finding free time, moving an event—are hard to discover. | No relevant suggestions are visible in the screenshot; capabilities or chips may appear in other states. | Check empty and populated chats. Offer a small set of supported example requests where useful. Verify that selecting a suggestion has clear behavior and does not silently change the calendar. |
| NUX-11 | Medium | Truncated conversation titles look identical. | Visible sidebar discoverability problem. Tooltip/full-title access helps, but does not make similar titles easy to scan. | Prefer short, distinct titles based on topic and date; preserve rename. Test finding an older conversation without opening several candidates. |
| NUX-12 | Medium | Pause, pencil and options icons have unclear meaning. The options symbol resembles a sun. | Static screenshots cannot establish tooltips or accessible names. The symbol may communicate a different concept than intended. | Verify tooltip, accessible name and keyboard focus for each icon. Use a recognizable settings/sliders symbol for send options. Make the distinction between new chat, edit/resend and motion pause clear. |
| NUX-13 | Low | Persistent “Sẵn sàng hỗ trợ” occupies header space with little information. | A design judgment; it may provide useful reassurance to some users. | Compare normal-state silence with a compact status. Always retain clear thinking, disconnected and failure feedback. Prioritize narrow-screen space. |
| NUX-14 | Medium | The “6 yêu cầu” badge lacks an obvious purpose. | Users may interpret it as usage quota, pending requests or chat length. Intended meaning requires confirmation. | Label it explicitly as conversation request count if useful, explain it, or remove it after validating that it adds no value. Keep usage/quota separate. |
| NUX-15 | Medium | The final card appears cut off by the composer. | A screenshot cannot distinguish content overlap from an ordinary scroll position. | Check the bottom of a long conversation at desktop, phone, landscape and browser zoom, including a growing composer. Ensure the last card and actions can scroll fully into view without being obscured. |
| NUX-16 | Low | The always-visible 0/2000 character counter adds idle-state clutter. | Optional presentation preference. The counter currently also communicates an input limit. | Consider showing it after typing or near the limit. Preserve a discoverable limit and clear feedback at the boundary; validate accessibility before hiding information. |
| NUX-17 | High | Automatic saving is insufficiently explained for a consequential scheduling action. | The small checkbox is visible; understanding and discoverability were inferred rather than observed. Default-on is not itself established as a defect. | Clarify the label/helper text and saved-versus-preview behavior. Test whether users can explain both settings. If changing the control type, retain equivalent keyboard and screen-reader operation. |
| NUX-18 | Medium | Secondary text appears small and low contrast; icon accessibility needs verification. | Estimated 10–11 px sizes and contrast were not measured. Accessible names cannot be inferred visually. | Measure actual styles and color pairs. Use readable secondary text, verify normal-text contrast of at least 4.5:1, and test focus visibility, zoom, names and keyboard operation. The evaluator's 12 px suggestion is not, by itself, an accessibility compliance rule. |

## Reconciliation with work already performed

The screenshot's exact build/version was not supplied. Some concerns overlap with changes made earlier in this workspace. These should be **retested**, rather than logged as newly confirmed missing features:

| Concern | Known work from this conversation | First-pass disposition |
| --- | --- | --- |
| No delete confirmation | Both menu and top bin now use a shared centered confirmation dialog. | Revalidate on the latest deployed build. Do not revert the user's requested top bin placement based on the evaluation alone. |
| Delete label / rectangular button | Top action was simplified to an icon-only red bin. | Compare latest screenshots. |
| Menu behavior | Menu was changed to a floating animated popup that does not move rows. | Preserve this behavior while checking clipping, keyboard and mobile states. |
| No Undo | Undo has been visible in earlier successful scheduling screenshots and tested UI states. | Investigate discoverability, eligibility and placement; do not assume the action is absent. |
| Unlabeled icons | Accessible names, tooltips and keyboard improvements were included in prior UI work. | Visual recognition can still be weak even when accessibility labels exist. Evaluate both. |
| Plan mismatch | Older-plan selection behavior and historical/preview guidance were improved earlier. | Those changes do not prove the screenshot's scope confusion is resolved. Reproduce the exact class/work example. |
| Internal codes | Earlier response presentation work introduced user-facing labels in some summaries. | Check all response paths and stored historical messages before closing the finding. |

No item is marked resolved solely on the basis of this reconciliation.

## Proposed implementation sequence

### 1. Establish the current baseline

QA/product should capture the latest build identifier, full conversation, selected plan source/date, auto-save setting and persisted calendar entries for the reviewed scenario. Retest the concerns already addressed above. This separates stale evidence, presentation ambiguity and actual defects.

### 2. Resolve trust and recovery

Prioritize NUX-01, NUX-02, NUX-05, NUX-07 and NUX-17. Product/UX should agree the panel's purpose, result-card hierarchy, terminology and saving model. Frontend should implement the presentation; backend involvement is conditional on evidence of incorrect persistence or result data. QA should verify saved, preview, blocked/conflict, failed, undone and historical-plan states.

NUX-06 remains a validation requirement for the already requested delete design. Deleting a conversation must be distinguished from deleting its scheduled items; verify and state the product's actual behavior rather than assuming a relationship.

### 3. Improve navigation and readability

Address titles, icon meaning, count labels, composer clearance and measured accessibility (NUX-11/12/14/15/18). Accessibility belongs in this implementation phase, not only in later cosmetic polish. Include compact dates and localized category presentation (NUX-03/04).

### 4. Validate enhancements before expanding scope

Prototype the day timeline and capability discovery (NUX-08/10), then evaluate optional proactive suggestions (NUX-09). Treat header status and character-counter changes (NUX-13/16) as lower-priority experiments, not prerequisites for correctness.

## Next evaluation pass

Run an interactive walkthrough on the latest build, followed by think-aloud sessions with 3–5 nontechnical participants. Suggested tasks:

1. Schedule class tomorrow 07:00–12:00 and work 13:00–17:00.
2. Verify what was saved using Nora and the application's calendar.
3. Move work to 14:00 and explain what changed.
4. Undo the eligible change and verify the restored state.
5. Switch auto-save off, submit another request and explain whether anything was saved.
6. Find a free hour tomorrow and identify how to discover this capability.
7. Find an older conversation among similar titles.
8. Open deletion from each entry point, verify the named target and cancel.

Record task completion without assistance, time to locate recovery actions, misunderstandings, calendar verification behavior and confidence before/after. Asking users to verify in the application's calendar is a test task; needing an unrelated external calendar to trust Nora is a separate negative signal.

A useful success condition is that participants can accurately explain **what was saved, what the right panel represents, how to reverse a change, and what auto-save does**. A single revised screenshot cannot establish these outcomes.
