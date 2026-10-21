# Urgent work and displaced sessions

Priority `4` marks urgent work. Both manual task planning and NORA use the
same deterministic planner. NORA interprets explicit urgency semantically;
an ordinary word containing a similar spelling does not force urgency.

When urgent work needs an occupied time, the planner may move overlapping,
lower-priority scheduled sessions. It preserves unrelated sessions, locked
sessions, running/paused work, fixed events, busy exceptions, saved task work
windows, breaks and daily/session limits. Dependencies must still be satisfied.

Displacement rules use the clock at planning time:

| Existing task | Placement policy |
| --- | --- |
| Deadline within 48 hours | Nearest feasible time to the original session, finishing before the deadline. Ties favor the later time. |
| Deadline farther away | Prefer the next local calendar day, then the second day, at a similar time. Same-day placement remains a fallback. Never defer beyond two local days or beyond the deadline. |

These rules move work sessions; they do not extend completion deadlines. A
deadline-first fallback is allowed when it is the way to complete both the
urgent work and displaced work on time. The planner never displaces a
prerequisite needed by the urgent task itself.

Proposals expose `hasUrgentReplan` and per-session reason codes
`URGENT_TASK_FIRST`, `URGENT_NEAR_DEADLINE_MOVE` and `URGENT_FLEXIBLE_MOVE`.
An infeasible urgent displacement cannot be applied: the API returns
`URGENT_REPLAN_INFEASIBLE` before cancelling any saved sessions. NORA previews
session changes and supports apply/undo through its existing flow. Every urgent
displacement requires an explicit confirmation, including when NORA's automatic
save switch is enabled. Retrying the same request does not accept the proposal.
Manual task scheduling also opens the proposal instead of automatically applying
it. Both interfaces show old and proposed times before confirmation. Automatic
note scheduling, recovery scans, and time-setting changes also leave urgent
displacements as proposals for explicit confirmation.

Regression coverage: `UrgentPlanningTests`, urgent cases in
`AgentFreeTimeTests`, and `UrgentPlanApiTests`. The optional API diagnostic
`--check-ai-urgency` checks the configured AI with fictional work and writes
no calendar data.
