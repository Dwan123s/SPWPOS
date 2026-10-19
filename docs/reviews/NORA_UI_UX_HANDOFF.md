# Nora UI/UX team handoff

Inventory verified against the current main workspace on 19 October 2026. Paths refer to this checkout; teammates can use the same paths relative to the repository root.

Start with the page/components and two active Nora stylesheets. For a runnable handoff, give the team the entire frontend directory plus the three artwork files listed below. Shared shell modules have additional application-wide dependencies; this inventory is a review map, not a standalone extracted application.

## Nora page and components

| File | Review purpose |
| --- | --- |
| [frontend/src/agent.jsx](../../frontend/src/agent.jsx) | Main page: header, toolbar, messages, composer, clarification, history/plan drawers, schedule results, action state and keyboard handling. |
| [frontend/src/conversations.jsx](../../frontend/src/conversations.jsx) | Recent/Archived tabs, search, conversation rows, popup menu, rename, archive/restore, export and delete entry point. |
| [frontend/src/conversation-delete-dialog.jsx](../../frontend/src/conversation-delete-dialog.jsx) | Shared centered delete confirmation dialog, target title, focus, cancel and failure feedback. |
| [frontend/src/nora-chat-ui.jsx](../../frontend/src/nora-chat-ui.jsx) | Reply rendering, conversation grouping, plan labels and date/time presentation. |
| [frontend/src/nora-progress.jsx](../../frontend/src/nora-progress.jsx) | Streaming progress, processing steps and walking animation markup. |
| [frontend/src/nora-mascot.jsx](../../frontend/src/nora-mascot.jsx) | Nora avatar, animated mascot and motion preference control. |
| [frontend/src/mascot-eyes.jsx](../../frontend/src/mascot-eyes.jsx) | Mascot eye rendering and pointer tracking. |

## Active Nora-specific styles

| File | Review purpose |
| --- | --- |
| [frontend/src/styles/nora-entry.css](../../frontend/src/styles/nora-entry.css) | Imported by agent.jsx. Current UI refinements, popup and modal styles, interaction targets, composer and responsive overrides. |
| [frontend/src/styles/nora-chat.css](../../frontend/src/styles/nora-chat.css) | Imported by nora-entry.css. Base Nora layout, chat bubbles, history, plan, animations and responsive styles. |

## Behavior supporting the UI

| File | Review purpose |
| --- | --- |
| [frontend/src/voice-input.js](../../frontend/src/voice-input.js) | Microphone availability, recording/transcription, cancellation and voice feedback state. |
| [frontend/src/agent-stream.js](../../frontend/src/agent-stream.js) | Streaming request handling and progress/result events. |
| [frontend/src/agent-failure.js](../../frontend/src/agent-failure.js) | Failure mapping and user-facing recovery messages. |
| [frontend/src/api.js](../../frontend/src/api.js) | Shared HTTP requests and API error handling. |
| [frontend/src/session-refresh.js](../../frontend/src/session-refresh.js) | Session refresh and request recovery. |

## Shared visual primitives and application shell

| File | Review purpose |
| --- | --- |
| [frontend/src/ui.jsx](../../frontend/src/ui.jsx) | Shared icons, UI primitives, formatting and workspace state used by Nora. |
| [frontend/src/react-ui.jsx](../../frontend/src/react-ui.jsx) | Shared React attribute/UI helpers used by ui.jsx. |
| [frontend/src/store.js](../../frontend/src/store.js) | Shared application state, notifications and modal state. |
| [frontend/src/main.jsx](../../frontend/src/main.jsx) | Frontend entry point and global stylesheet import. |
| [frontend/src/App.jsx](../../frontend/src/App.jsx) | Application root, shared overlays and shell initialization. |
| [frontend/src/shell.jsx](../../frontend/src/shell.jsx) | Application navigation and outer workspace shell. |
| [frontend/src/controller.jsx](../../frontend/src/controller.jsx) | Route metadata and global navigation/action handling, including Nora route integration. |
| [frontend/src/route-pages.jsx](../../frontend/src/route-pages.jsx) | Lazy-loads AgentPage for the agent route. |
| [frontend/src/workspace-motion.jsx](../../frontend/src/workspace-motion.jsx) | Shared application motion/modal behavior. |

## Global stylesheet cascade

| File | Review purpose |
| --- | --- |
| [frontend/src/styles/index.css](../../frontend/src/styles/index.css) | Global stylesheet entry, theme tokens and shared primitives. |
| [frontend/src/styles/app.css](../../frontend/src/styles/app.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/layout.css](../../frontend/src/styles/layout.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/auth.css](../../frontend/src/styles/auth.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/glass.css](../../frontend/src/styles/glass.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/responsive.css](../../frontend/src/styles/responsive.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/login-scene.css](../../frontend/src/styles/login-scene.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/password-checklist.css](../../frontend/src/styles/password-checklist.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/login-feedback.css](../../frontend/src/styles/login-feedback.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/loading.css](../../frontend/src/styles/loading.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/workspace-experience.css](../../frontend/src/styles/workspace-experience.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/google-signin.css](../../frontend/src/styles/google-signin.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/sidebar.css](../../frontend/src/styles/sidebar.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/planning-console.css](../../frontend/src/styles/planning-console.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/workspace-login-theme.css](../../frontend/src/styles/workspace-login-theme.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/daily-schedule.css](../../frontend/src/styles/daily-schedule.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/getting-started.css](../../frontend/src/styles/getting-started.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/delete-actions.css](../../frontend/src/styles/delete-actions.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/notifications.css](../../frontend/src/styles/notifications.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |
| [frontend/src/styles/workspace-polish.css](../../frontend/src/styles/workspace-polish.css) | Global cascade dependency; review alongside Nora styles when checking shell, typography, controls or responsive behavior. |

## Nora visual assets

| File | Review purpose |
| --- | --- |
| [src/SPWPOS.Api/wwwroot/assets/start/planora-owl-v1.png](../../src/SPWPOS.Api/wwwroot/assets/start/planora-owl-v1.png) | Avatar and mascot artwork. |
| [src/SPWPOS.Api/wwwroot/assets/start/planora-owl-eye-base-v1.png](../../src/SPWPOS.Api/wwwroot/assets/start/planora-owl-eye-base-v1.png) | Eye tracking base artwork. |
| [src/SPWPOS.Api/wwwroot/assets/nora/nora-walk-v1.png](../../src/SPWPOS.Api/wwwroot/assets/nora/nora-walk-v1.png) | Walking animation sprite referenced by Nora CSS. |

## UI and behavior regression tests

| File | Review purpose |
| --- | --- |
| [frontend/tests/agent.test.jsx](../../frontend/tests/agent.test.jsx) | Page interactions, results, plan disclosure, deletion, restoration, retry and clipboard feedback. |
| [frontend/tests/conversations.test.jsx](../../frontend/tests/conversations.test.jsx) | Menu keyboard handling, rename/archive/restore, deletion retry, cancellation and return focus. |
| [frontend/tests/nora-chat-ui.test.jsx](../../frontend/tests/nora-chat-ui.test.jsx) | Reply and plan presentation helpers. |
| [frontend/tests/voice-input.test.jsx](../../frontend/tests/voice-input.test.jsx) | Voice recording/transcription interactions. |
| [frontend/tests/agent-stream.test.js](../../frontend/tests/agent-stream.test.js) | Streaming transport behavior. |
| [frontend/tests/agent-failure.test.jsx](../../frontend/tests/agent-failure.test.jsx) | Failure presentation/recovery behavior. |

## Review context and build configuration

| File | Review purpose |
| --- | --- |
| [docs/reviews/NORA_UI_UX_AUDIT_2026-10-19.md](../../docs/reviews/NORA_UI_UX_AUDIT_2026-10-19.md) | Implemented fixes, browser evidence and verification limits. |
| [frontend/package.json](../../frontend/package.json) | Frontend dependencies and build/test commands. |
| [frontend/vite.config.js](../../frontend/vite.config.js) | Frontend build and test configuration. |
| [frontend/index.html](../../frontend/index.html) | Frontend HTML entry. |

## Important distinctions

- `frontend/src/styles/agent.css` is a legacy stylesheet and is not imported by the current Nora page. The active chain is `agent.jsx` → `nora-entry.css` → `nora-chat.css`, plus the global cascade above.
- `src/SPWPOS.Api/wwwroot/app/` contains generated build output. Review and edit the frontend source rather than the generated bundles.
- Nora page layout and rendering live in the frontend. Backend agent services are needed only if the team also wants to evaluate generated wording, scheduling decisions or API contracts.
- Synthetic screenshots in the audit use fictional data. Real voice, authentication and AI scheduling were not certified by the visual audit.
