# Nora page UI and UX audit — 19 October 2026

Applied to the main workspace at `C:\Users\Admin\source\repos\SPWPOS`.

This pass improves the existing Nora page rather than introducing a new visual design. It covers the header, conversation library, message actions, composer, plan panel, loading/error states, keyboard navigation, and responsive layout. Earlier scheduling and response policy patches remain in place.

## Findings and changes

| Area | Finding | Implemented behavior |
| --- | --- | --- |
| Conversation three-dot menu | The fixed-height conversation row clipped the entire menu. Hover-only access also made it hard to discover on touch screens. | The trigger stays visible. Opening it now shows a floating popup above the scroll container, with Rename, Archive/Restore, Export JSON, and Delete. It does not change row positions. |
| Conversation menu keyboard access | Menu actions lacked a complete keyboard interaction. | Arrow keys, Home/End, Escape, initial menu focus, and return focus are supported. Escape in the menu does not also dismiss the mobile drawer. |
| Recent/Archived tabs | Tab selection and keyboard focus were inconsistent. | Tabs expose their selected state and shared panel; Left/Right and Home/End switch tabs. |
| Search feedback | Old rows stayed actionable during the search debounce. | Changing the search starts the loading state immediately and closes the old menu. Old rows are disabled while the request is pending. |
| Rename | A failed request could dismiss the edit and lose the draft. The active chat header could still show its old name. | Failed edits retain the draft for retry. Successful edits refresh the selected chat's title and version. |
| Delete | Failed deletion could dismiss the current chat; a confirmation could survive switching chats. The toolbar only searched the first page of conversations. | Server failures retain the chat and confirmation. Changing chats clears the confirmation. Toolbar lookup can traverse pages when selected metadata is unavailable. |
| Archive/restore | Archived chats had an unclear return path and a stale instruction referring to a nonexistent management tab. | Archived chats are visibly read-only with a direct Restore conversation button. Restoring updates metadata and enables the composer. |
| Async actions | Busy feedback always implied that the list was loading. | Rename, archive, restore, delete, and export show the relevant action while it is pending. |
| Mobile drawers | History and plan could compete for space, focus could escape behind them, and grid placement added a second header-sized gap. | One drawer opens at a time; each starts immediately below the header. Background content is inert, focus stays inside, Escape dismisses, and focus returns to the opener. |
| Mobile plan disclosure | A plan could open automatically and obscure the conversation on small screens. | Small screens open a plan on request. Desktop retains automatic plan visibility. Selecting an older plan still opens that specific plan. |
| Message edit action | The pencil could overlap text in the blue message bubble; its label implied editing an existing request. | The bubble reserves space for the control. Its accessible label explains that it copies the text to edit and resend as a new request. |
| Composer | Long drafts, length limits, unavailable voice input, and send options lacked clear feedback. | The draft field grows within its limit, a character counter shows remaining capacity, unavailable voice input is explained in options, and the options popover stays within the composer/viewport. |
| Initial connection failure | Users lacked an immediate recovery action. | Retry reconnects without navigating away from Nora. |
| Copy response | Clipboard failure silently left the button looking unresponsive. | Failure displays an explanation; success feedback remains available. |
| Small-screen controls | Several actions had cramped touch targets or consumed too much title space. | Mobile controls have larger targets, the chat delete control uses its labeled icon, clarification inputs are easier to use, and long titles truncate with the full title available. |
| Plan status | A historical or unsaved plan could look like a currently saved plan. | The plan panel includes contextual guidance for preview and undone results. |

## Validation

- **247 frontend tests passed**, across 30 files. Nine additional tests cover menu keyboard behavior, failed rename/delete recovery, archive/restore, active title updates, confirmation reset, initial reconnect, and clipboard failure. The existing older-plan test also verifies explicit mobile plan disclosure.
- **4 session refresh tests passed** using the project's browser/session test harness.
- **Production frontend build passed** with `npm run build`; assets were generated into the API's existing web root.
- Browser checks used the actual Nora React components and loaded styles with a synthetic, in-memory HTTP fixture. No real conversations, calendar entries, AI requests, or database records were changed.
- Checked widths **375, 667, 900, and 1280 CSS pixels**, including a **667 × 375** landscape viewport. Document width matched viewport width in these states. The landscape composer remained within the viewport.
- Manually verified visible desktop menu actions, renamed header, mobile history and plan placement, options scrolling, focus within the mobile plan, Escape dismissal, and return focus.
- `git diff --check` passed. All **261 protected user files** from the prior workspace-transfer manifest remained unchanged.

The tests use HTTP and clipboard boundary fakes while rendering the real components; they do not mock Nora's internal UI helpers. They verify visible state and recovery behavior rather than claiming model or persistence correctness.

## Evidence and limits

- [Desktop with expanded conversation actions](../evidence/nora-ui-2026-10-19/desktop-menu.jpg)
- [Phone with plan drawer](../evidence/nora-ui-2026-10-19/mobile-plan.jpg)
- [Frontend test output](../evidence/nora-ui-2026-10-19/frontend-verified.log)

These local evidence files contain synthetic data and are stored under the ignored `artifacts` directory.

Export's menu visibility and action feedback were reviewed. The in-app browser did not return its download event, so successful file delivery to the browser's download directory remains unverified. Live authentication, actual speech recognition/microphone permissions, screen-reader operation, and live AI scheduling quality were outside this frontend pass. This is not a full accessibility certification.

## Files changed in this pass

- `frontend/src/agent.jsx`
- `frontend/src/conversations.jsx`
- `frontend/src/styles/nora-entry.css`
- `frontend/tests/agent.test.jsx`
- `frontend/tests/conversations.test.jsx`

Reload the locally running app to use the newly built frontend. Deployment is still required for a separately hosted environment. No migration or API restart was performed in this pass.


## Follow-up: popup and delete confirmation refinement

The initial inline menu treatment was replaced following the user's visual feedback:

- The three-dot menu uses the browser's popover layer, escaping the list's clipping without entering document flow. It opens with a 180 ms extension/fade animation, respects reduced motion, stays within viewport bounds, and opens upward when space below is insufficient. Scrolling the underlying list or resizing dismisses it rather than leaving an orphaned popup.
- The top delete action is a red bin icon with no visible label, border, or rectangular background. Its accessible name and tooltip remain.
- Both delete entry points render the same shared `ConversationDeleteDialog`, centered over the conversation area. It names the actual target chat, warns that deletion is permanent, focuses Cancel initially, supports close/Escape, and returns focus after cancellation. Native modal behavior prevents interaction with the page behind it.
- Failed deletions keep the dialog open and display the failure inside it. Buttons are disabled while deletion is pending. The inline sidebar confirmation and toolbar banner are no longer rendered.

Browser verification used synthetic data at desktop, 375 px phone, and 667 × 375 landscape sizes. Measured row positions before and after opening the popup were identical (44 px row height; tops 294.8, 340.8, and 386.8). The landscape popup opened upward and remained between y=69.8 and y=263.8 in a 375 px-high viewport. Both entry points displayed the correct target title. Cancelling on mobile retained the open history drawer and returned focus to its three-dot trigger.

Evidence:

- [Floating desktop menu](../evidence/nora-ui-2026-10-19/floating-menu-desktop.jpg)
- [Floating phone menu](../evidence/nora-ui-2026-10-19/floating-menu-mobile.jpg)
- [Shared centered delete dialog](../evidence/nora-ui-2026-10-19/delete-dialog-desktop.jpg)
- [Follow-up test output](../evidence/nora-ui-2026-10-19/popup-full.log)

The follow-up also adds `frontend/src/conversation-delete-dialog.jsx` and a cancellation/focus regression test, and updates existing deletion tests to assert the shared dialog.

Follow-up validation completed: **248 frontend tests passed** across 30 files, **4 session refresh tests passed**, and the **production frontend build passed**. All 261 protected files remained unchanged. No real conversation was deleted during browser verification.
