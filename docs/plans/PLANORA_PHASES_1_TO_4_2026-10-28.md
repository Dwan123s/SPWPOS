# PLANORA phases 1–4, 28/10/2026

Requested scope: preserve existing routes and behavior while splitting entities/controllers; add optimistic workspace concurrency; scored deterministic scheduling and EMA; privacy-first admin and internal XP/skins; catch-up, bounded auto-heal and iCalendar export.

Existing Notes edits remain in the working tree and must not be overwritten. NORA work is already committed as `7a15832`; push remains pending separate destination approval.

Implementation sequence:

1. Mechanical SRP split, compile; workspace concurrency using Revision while preserving authentication serialization.
2. Feasible slot scoring and deterministic EMA with documented tunable policy values and scientific references.
3. XP, inventory and level API/UI; freeze real payments; aggregate admin metrics/health without personal work content.
4. Review elapsed sessions without a timer; idempotent catch-up and bounded automatic repair; authenticated iCalendar export; full verification.

Images also suggest independent deployment. The detailed pasted mission prioritizes the four tasks above; deployment changes will be additive and keep current development/startup compatibility.

Completed in the working tree. The user chose a shared browser origin: independent builds and CI artifacts are implemented without changing CORS/cookie policy. See [implementation](../features/WORKSPACE_UPGRADE_PHASES_1_TO_4.md), [deployment](../development/INDEPENDENT_BUILD_SAME_ORIGIN.md) and [verification](../development/WORKSPACE_UPGRADE_VERIFICATION_2026-10-28.md). Final suites: 635 unit, 149 API/integration, 360 frontend, 4 session refresh tests passed. Real database migration/deployment/push are not part of this local verification.

Do not claim a guaranteed runtime below 100 ms, clinical validation, live provider health, or two-way calendar synchronization without evidence. iCalendar is a portable one-way export.
