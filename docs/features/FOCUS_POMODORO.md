# Stopwatch / Pomodoro

Open from the clock button labeled **Focus** in the workspace header or the home countdown card. When Pomodoro runs, this button shows a small green timer icon. The scheduled start/end countdown always continues showing the calendar and is never replaced by the personal timer.

- Expanded: choose Stopwatch or Pomodoro; start, pause/resume, finish, hide or minimize. Task titles, task selection and the next-task panel are omitted for now. Stopwatch counts up from zero without an end time. Pausing freezes elapsed time, resuming adds new elapsed time, and finishing preserves the total.
- Mini: a draggable 144px circle containing only the ring, time and pause/resume button. Stopwatch has a cyan loading arc moving slowly around a fixed track (12 seconds per circuit), which freezes on pause; Pomodoro has blue countdown progress. Reduced motion keeps the Stopwatch arc static. Click the time to expand. Drag the ring/background with mouse, pen or touch. Position is constrained to the viewport and remembered.
- Hover/focus the expanded Pomodoro clock (or tap its time) to reveal −5/+5 minute adjustments. Pomodoro settings offer 25/5, 50/10 and custom durations, a long break after four work rounds, automatic rest, end chime and volume. Stopwatch does not show duration or countdown controls.
- A rest can start automatically; the next work round always waits for an explicit start. Returning after sleep catches up to the current rest or waits at the next work round instead of inventing completed work.
- Upcoming events and eligible work sessions remain visible on the home page, with an empty state when there is no upcoming schedule. Timer extensions warn if they overlap an upcoming entry.

Timer state/settings/position are stored locally per workspace profile under `planora.focus.v1.<profileId>`. Absolute deadlines preserve the countdown across navigation, background tabs and reloads. Storage and audio are optional; the countdown still works if either is unavailable. Closing the tab does not provide a background alarm; reopening restores the deadline and catches up.

This is a personal timer without task selection. Finishing it does not complete a task, change its schedule or post backend execution time. The existing scheduled countdown, task completion and work-time review remain available. Adjusting a countdown does not move calendar entries. Stored legacy Focus countdowns are discarded rather than interpreted as Stopwatch elapsed time; Pomodoro settings and sessions are retained.

The Stopwatch revision was built without running tests, as requested.
