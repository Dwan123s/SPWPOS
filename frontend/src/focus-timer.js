// Absolute timestamps keep Pomodoro and Stopwatch accurate in background tabs.
export const defaultFocusSettings = {
  focusMinutes: 25,
  breakMinutes: 5,
  longBreakMinutes: 15,
  autoBreak: true,
  alarm: true,
  volume: 35,
};
export const focusStorageKey = (uid) => `planora.focus.v1.${uid}`;
export function freshFocus() {
  return {
    mode: "pomodoro",
    settings: { ...defaultFocusSettings },
    timer: null,
    view: "hidden",
    position: null,
  };
}
export function readFocus(uid) {
  const fallback = freshFocus();
  if (!uid) return fallback;
  try {
    const value = JSON.parse(localStorage.getItem(focusStorageKey(uid)));
    if (!value || !["focus", "stopwatch", "pomodoro"].includes(value.mode))
      return fallback;
    const settings = { ...defaultFocusSettings };
    for (const name of ["focusMinutes", "breakMinutes", "longBreakMinutes"])
      if (Number.isFinite(value.settings?.[name]))
        settings[name] = Math.max(
          1,
          Math.min(180, Math.round(value.settings[name])),
        );
    for (const name of ["autoBreak", "alarm"])
      if (typeof value.settings?.[name] === "boolean")
        settings[name] = value.settings[name];
    if (Number.isFinite(value.settings?.volume))
      settings.volume = Math.max(0, Math.min(100, value.settings.volume));
    const t = value.timer;
    const commonTimer =
      t &&
      ["running", "paused", "completed"].includes(t.status) &&
      typeof t.title === "string";
    const validStopwatch =
      commonTimer &&
      t.mode === "stopwatch" &&
      Number.isFinite(t.elapsedMs) &&
      t.elapsedMs >= 0 &&
      Number.isFinite(t.segmentStartedAt) &&
      t.segmentStartedAt >= 0;
    const validPomodoro =
      commonTimer &&
      t.mode === "pomodoro" &&
      ["work", "break", "longBreak"].includes(t.phase) &&
      Number.isFinite(t.remainingMs) &&
      t.remainingMs >= 0 &&
      t.remainingMs <= 10800000 &&
      Number.isFinite(t.durationMs) &&
      t.durationMs > 0 &&
      t.durationMs <= 10800000 &&
      Number.isFinite(t.deadline) &&
      Number.isInteger(t.round) &&
      t.round >= 1 &&
      t.round <= 4;
    // A previous Focus countdown cannot be interpreted as stopwatch elapsed time.
    const timer = validStopwatch || validPomodoro ? t : null;
    return {
      mode: value.mode === "focus" ? "stopwatch" : value.mode,
      settings,
      timer,
      view: ["hidden", "mini", "expanded"].includes(value.view)
        ? value.view
        : "hidden",
      position:
        Number.isFinite(value.position?.x) && Number.isFinite(value.position?.y)
          ? value.position
          : null,
    };
  } catch {
    return fallback;
  }
}
export function remainingFocus(timer, now = Date.now()) {
  return timer && timer.mode !== "stopwatch"
    ? Math.max(
        0,
        timer.status === "running" ? timer.deadline - now : timer.remainingMs,
      )
    : 0;
}
export function elapsedFocus(timer, now = Date.now()) {
  if (!timer || timer.mode !== "stopwatch") return 0;
  return Math.max(
    0,
    timer.elapsedMs +
      (timer.status === "running"
        ? Math.max(0, now - timer.segmentStartedAt)
        : 0),
  );
}
export function timerDisplayMs(timer, now = Date.now()) {
  return timer?.mode === "stopwatch"
    ? elapsedFocus(timer, now)
    : remainingFocus(timer, now);
}
export function startFocus(model, task, now = Date.now()) {
  const mode = model.mode === "focus" ? "stopwatch" : model.mode;
  const durationMs = model.settings.focusMinutes * 60000;
  return {
    ...model,
    view: "expanded",
    timer: {
      mode,
      phase: "work",
      status: "running",
      round: 1,
      title: task?.title || "Tập trung tự do",
      taskId: task?.taskId || null,
      durationMs,
      remainingMs: durationMs,
      deadline: now + durationMs,
      ...(mode === "stopwatch"
        ? { elapsedMs: 0, segmentStartedAt: now, remainingMs: 0, deadline: 0 }
        : {}),
    },
  };
}
export function tickFocus(model, now = Date.now()) {
  const t = model.timer;
  if (
    !t ||
    t.mode === "stopwatch" ||
    t.status !== "running" ||
    t.deadline > now
  )
    return model;
  const wasWork = t.phase === "work";
  const phase = wasWork ? (t.round === 4 ? "longBreak" : "break") : "work";
  const durationMs =
    model.settings[
      phase === "work"
        ? "focusMinutes"
        : phase === "break"
          ? "breakMinutes"
          : "longBreakMinutes"
    ] * 60000;
  const deadline = t.deadline + durationMs;
  // Auto-start only the rest. Work always waits for an explicit start.
  if (wasWork && model.settings.autoBreak && deadline <= now) {
    const workMs = model.settings.focusMinutes * 60000;
    return {
      ...model,
      timer: {
        ...t,
        phase: "work",
        round: (t.round % 4) + 1,
        status: "paused",
        durationMs: workMs,
        remainingMs: workMs,
        deadline: now + workMs,
      },
    };
  }
  return {
    ...model,
    timer: {
      ...t,
      phase,
      round: wasWork ? t.round : (t.round % 4) + 1,
      durationMs,
      remainingMs: durationMs,
      deadline,
      status: wasWork && model.settings.autoBreak ? "running" : "paused",
    },
  };
}
export function toggleFocus(model, now = Date.now()) {
  const advanced = tickFocus(model, now),
    t = advanced.timer;
  if (!t || t.status === "completed") return advanced;
  if (t.mode === "stopwatch")
    return {
      ...advanced,
      timer: {
        ...t,
        elapsedMs: elapsedFocus(t, now),
        segmentStartedAt: now,
        status: t.status === "running" ? "paused" : "running",
      },
    };
  const remainingMs = remainingFocus(t, now);
  return {
    ...advanced,
    timer: {
      ...t,
      remainingMs,
      status: t.status === "running" ? "paused" : "running",
      deadline: now + remainingMs,
    },
  };
}
export function completeFocus(model, now = Date.now()) {
  const t = model.timer;
  if (!t) return model;
  return {
    ...model,
    timer: {
      ...t,
      status: "completed",
      remainingMs: 0,
      ...(t.mode === "stopwatch"
        ? { elapsedMs: elapsedFocus(t, now), segmentStartedAt: now }
        : {}),
    },
  };
}
export function adjustFocus(model, minutes, now = Date.now()) {
  const advanced = tickFocus(model, now),
    t = advanced.timer;
  if (!t || t.mode === "stopwatch" || t.status === "completed") return advanced;
  const current = remainingFocus(t, now);
  const remainingMs = Math.max(
    1000,
    Math.min(180 * 60000, current + minutes * 60000),
  );
  return {
    ...advanced,
    timer: {
      ...t,
      remainingMs,
      durationMs: Math.max(
        remainingMs,
        Math.min(180 * 60000, t.durationMs + remainingMs - current),
      ),
      deadline: now + remainingMs,
    },
  };
}
export function clampFocusPosition(
  position,
  width = window.innerWidth,
  height = window.innerHeight,
) {
  const size = 144,
    gap = 12;
  return {
    x: Math.max(
      gap,
      Math.min(
        position?.x ?? width - size - 24,
        Math.max(gap, width - size - gap),
      ),
    ),
    y: Math.max(
      gap,
      Math.min(
        position?.y ?? height - size - 24,
        Math.max(gap, height - size - gap),
      ),
    ),
  };
}
