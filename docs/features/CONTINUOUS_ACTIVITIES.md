# Activities that require one continuous interval

Fixed calendar events already have one exact start and end. A meeting, class,
appointment or shift is never split by the planner.

An actual meeting accidentally saved as a task previously inherited
`Splittable=true`. A 120-minute meeting could therefore be scheduled at 08:00–09:00
and 10:00–11:00. `ContinuousActivityTests` reproduces this case before the fix.

The domain now computes effective splitting from the saved choice and a mandatory
continuity policy. Explicit activity types and recognizable Vietnamese/English
titles protect meetings, classes, appointments, interviews, exams and work shifts.
The policy also applies when an older task's stored splitting flag is incorrect.
Unknown/custom work can be marked non-splittable using the existing task setting.
Meeting preparation, meeting minutes, exam revision and ordinary writing/study
remain eligible for splitting unless the user requires uninterrupted work.

The scheduler, scoring, repair, risk analysis and session split/edit validation all
honor effective continuity. Mandatory continuous activities cannot enable splitting
through task edits or NORA. No matching continuous slot produces
`NO_CONTIGUOUS_SLOT`; the warning explains the required duration. A partial manual
session or shortening an assigned continuous task fails with
`CONTINUOUS_ACTIVITY_REQUIRED`. Existing fixed event times remain unchanged.

The task and Eisenhower views expose `requiresContinuousSession` so the editor
disables the splitting option and explains why. This is a computed rule, requiring
no database migration or bulk rewrite of existing tasks.

NORA instructions distinguish attendance from preparation. Note activity analysis
now preserves `splittable=false` for explicitly continuous work through structured
AI output, review, preview and import. Older note payloads remain compatible.

Verification uses synthetic tasks and isolated test databases, without adding
meetings or changing availability in the user's real calendar.
