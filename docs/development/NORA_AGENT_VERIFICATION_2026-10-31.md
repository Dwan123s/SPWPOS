# NORA Agent verification — 31 October 2026

The bounded native function-calling runtime is implemented and deployed to the local API at `https://localhost:7043`. Vertex AI Express with `gemini-3.8-flash` is ready. Chat uses Agent mode for the supported configured Google provider; the previous Workflow mode remains selectable.

## Evidence

| Check | Result |
| --- | --- |
| Backend regression unit suite | 704 passed, 2 opt-in tests skipped; no failures |
| Final Agent, provider-protocol and development-log checks on the merged source | 31 passed |
| Final real Gemini tool-loop probe | 1 passed, 5 model turns, 7 tool calls, verified preview |
| HTTP integration suite | 136 passed; 20 SQL-only tests were run separately |
| Real SQL Server integration/migration checks | All 20 passed on disposable `SPWPOS_Tests_*` databases |
| Full frontend suite | 394 passed across 46 files |
| Final chat/stream regression subset | 43 passed, including forwarding tool events through SSE |
| Browser refresh concurrency | 4 passed |
| EF model versus migration snapshot | No pending model changes |
| Final Release API build | Succeeded with zero warnings and zero errors |
| Deployed API, frontend and NORA bundle | HTTP 200; action-trace UI present |

The full unit regression suite preceded the development-logging merge; the final 31-test run covers the merged Agent, logging and protocol changes. Reports and command logs are in the ignored `artifacts` directory, including `nora-agent-all-unit.xml`, `nora-agent-tests.xml`, `nora-agent-live.xml`, `nora-agent-sql.xml` and `nora-agent-frontend.xml`.

The real model selected:

```text
get_preferences, get_calendar, get_tasks,
prepare_changes, simulate_plan, evaluate_candidate, finish_proposal
```

The probe used an in-memory database with a fictional class tomorrow from 08:00–10:00 and a continuous 90-minute slide task due by noon. The result was a preview fitting after the class and before noon, with zero saved tasks or sessions. No actual account calendar was accessed by this probe. Its final execution took approximately 25 seconds, including setup; this is a sample, not a latency guarantee. An earlier live attempt reached the 45-second provider timeout; later native protocol and complete-loop checks passed.

## Behaviors checked

- The same model goal branches to a proposal when feasible and a clarification when infeasible, using actual tool observations.
- Simulations save no business data; selected candidate task/session IDs survive preview, apply, retry and undo.
- Fake candidates and unsupported tools cannot be applied. Identical tool calls reuse observations while consuming the bounded budget.
- Clarifications retain cumulative model/tool counters. Active conversation leases prevent concurrent runs.
- A changed calendar revision stops a stale execution. Cancellation releases the lease and leaves business data untouched.
- Optimization preserves fixed/locked commitments, stays within the selected day and requires confirmation.
- Explicitly continuous work cannot be split to manufacture feasibility. Saved deadlines and urgency cannot be relaxed without an explicit user instruction.
- Read-only requests cannot become mutating drafts. Calendar conclusions require a calendar observation.
- Conversation deletion removes run goals and tool metadata. Pruned diagnostic records do not block an otherwise valid undo.
- Provider call IDs and opaque signatures are preserved appropriately; raw signatures/reasoning are absent from the browser trace.
- Development file logging records allowlisted metadata without raw prompts, tool arguments, provider responses or credentials.

## Deployment

Migration `20261030191749_AddNoraAgentRuntime` adds `AgentRuns` and `AgentToolCalls`; the migration test verified existing tasks survive and deleting a run cascades to its tool metadata. The local database was backed up before deployment. Task, session, fixed-event and turn counts were checked unchanged during the upgrade. No real calendar event was added or moved for deployment verification.

The final API process is recorded in `artifacts/nora-agent-deployment.json`. Runtime and chat source hashes were captured in `artifacts/nora-agent-verified-source.json`, then checked again before and after deployment. A database backup and previous API binaries are retained locally; their paths are recorded in the deployment JSON.

For configuration, tool contracts, limits and a repeatable demo, see [NORA Agent runtime](../features/NORA_AGENT_RUNTIME.md). This is a bounded tool-using agent with controlled application of changes, not a background assistant or a guarantee of a globally optimal schedule.
