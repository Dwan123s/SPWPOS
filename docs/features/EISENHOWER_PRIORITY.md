# Unified task priority

All task surfaces use the same two axes. Importance is explicitly `Important`,
`Lower`, or `Unknown`. Urgency is calculated from the absolute deadline: overdue
work and deadlines at most 24 hours away are `Urgent`; later deadlines are
`NotUrgent`. A missing deadline leaves urgency unknown. Calendar capacity and
completion risk remain separate warnings and never change this classification.

The four quadrants are Q1 (important, urgent), Q2 (important, not urgent), Q3
(lower importance, urgent), and Q4 (lower importance, not urgent). Missing
importance or urgency produces `Unknown`. Existing numeric priority does not
establish importance, and stored manual urgency overrides are no longer used.

Personal task inputs, task batches, group manual drafts/changes, editable group
briefs, matrix inputs, and note task activities accept optional `importanceLevel`.
Omitting it on an existing task preserves the previous assessment; explicitly
passing `Unknown` clears the assessment. Shared changes still require the
recipient's normal acceptance before changing the effective task contract.

Workspace and shared task responses expose an `eisenhower` summary. The matrix,
task lists, group board, filters, focus selection, and schedule ordering use the
same rule. Matrix moves may change importance or order; moving across the
deadline-derived urgency boundary is rejected. Undated drafts remain visible in
the clarification area. No deadline or duration is invented for them.

With Eisenhower scheduling enabled, ordering follows Q1, Q2, Q3, Q4, then unknown,
while retaining dependency precedence and protected calendar sessions. Changes
to classification do not apply a calendar proposal automatically.

No database migration is required for this change.
