# NORA schedule review

Requests such as “phân tích lịch trình hôm nay của tôi” use a read-only
`scheduleReviewQuery`, separate from the shorter `freeTimeQuery` answer.
An older provider's free-time interpretation is promoted when the request
explicitly asks for analysis/advice, without discarding mutation commands.

The first interpretation selects an explicit account-local date range. The
server loads all calendar reservations for that range (including elapsed
appointments and completed sessions), calculates the facts, and asks the
configured AI to explain them in 2–3 useful paragraphs. Both provider requests
use the normal quota accounting. The account revision is checked again after
the explanation; retries reuse the saved answer without another provider call.

The report includes fixed appointments, work sessions, merged occupied time,
remaining calendar gaps, fragmentation, overlaps, near/due work and practical
recommendations. Free time is measured from the current instant onward; the
calendar overview includes the full requested period. Completed sessions do
not imply their whole task is completed. Task progress is a current snapshot.
Displayed lists disclose truncation while totals use all loaded reservations.

Specific task slots are independent alternatives, checked with the same
session validation used to save work: remaining duration, deadline, saved
work window, breaks, dependencies and daily/session limits. Small calendar
gaps are not presented as valid focus sessions. A 60-minute task inside a
70-minute gap keeps its exact 60-minute proposed start/end.

Neither interpretation nor advice can create, move or save calendar items.
Mutation-bearing or mismatched explanation output falls back to the verified
report. Any later scheduling request follows the normal proposal/confirmation
flow, including mandatory confirmation for urgent displacement.

Tests cover the 110-minute fragmented example, completed/elapsed calendar
items, overlaps, missing availability, invalid scopes, constraints and unsafe
explanation output. `--check-ai-schedule-review` uses fictional data to verify
the configured provider's scope classification and detailed advice.
