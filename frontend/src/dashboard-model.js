const excludedSessions = new Set(["Skipped", "Missed", "Cancelled"]);
function dayKey(value, zone) {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: zone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date(value));
}
export function nextDay(day, offset = 1) {
  const date = new Date(`${day}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() + offset);
  return date.toISOString().slice(0, 10);
}
function interval(item, day, calendar) {
  const start = Math.max(0, calendar.minute(item.start, day));
  const end = Math.min(1440, calendar.minute(item.end, day));
  return end > start ? [start, end] : null;
}
function merge(intervals) {
  const result = [];
  for (const [start, end] of intervals
    .filter(([start, end]) => end > start)
    .sort((a, b) => a[0] - b[0])) {
    const previous = result.at(-1);
    if (previous && start <= previous[1])
      previous[1] = Math.max(previous[1], end);
    else result.push([start, end]);
  }
  return result;
}
function subtract(intervals, blocked) {
  return intervals.flatMap(([start, end]) => {
    const [from, to] = blocked;
    if (to <= start || from >= end) return [[start, end]];
    return [
      [start, Math.min(from, end)],
      [Math.max(to, start), end],
    ].filter(([a, b]) => b > a);
  });
}
const duration = (intervals) =>
  intervals.reduce((sum, [start, end]) => sum + end - start, 0);
export function dashboardModel(workspace, insights, now = Date.now()) {
  const zone = workspace.preference.timeZoneId || "Asia/Ho_Chi_Minh";
  const today = dayKey(now, zone);
  const dateFormatter = new Intl.DateTimeFormat("en-CA", {
    timeZone: zone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  });
  const timeFormatter = new Intl.DateTimeFormat("en-GB", {
    timeZone: zone,
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  });
  const localTimes = new Map();
  const calendar = {
    minute(value, day) {
      if (!localTimes.has(value)) {
        const date = new Date(value);
        const parts = timeFormatter.formatToParts(date);
        localTimes.set(
          value,
          Date.parse(`${dateFormatter.format(date)}T00:00:00Z`) / 60000 +
            Number(parts.find((part) => part.type === "hour").value) * 60 +
            Number(parts.find((part) => part.type === "minute").value),
        );
      }
      return localTimes.get(value) - Date.parse(`${day}T00:00:00Z`) / 60000;
    },
  };
  const sessions = workspace.sessions.filter(
    (session) => !excludedSessions.has(session.status),
  );
  const calendarEntries = [
    ...sessions.map((item) => ({
      ...item,
      kind: "session",
      name: item.taskName || "Phiên làm việc",
    })),
    ...(workspace.events || []).map((item) => ({ ...item, kind: "event" })),
  ];
  const hasAvailability =
    !!workspace.availability?.length ||
    !!workspace.exceptions?.some((item) => item.isAvailable);
  const daySessions = sessions.filter((session) =>
    interval(session, today, calendar),
  );
  const active = sessions.find((session) =>
    ["Running", "Paused"].includes(session.status),
  );
  const focus =
    active ||
    daySessions
      .filter(
        (session) =>
          session.status === "Scheduled" &&
          new Date(session.end).getTime() > now,
      )
      .sort((a, b) => new Date(a.start) - new Date(b.start))[0];
  const days = Array.from({ length: 7 }, (_, index) => {
    const key = nextDay(today, index);
    const weekday = new Date(`${key}T12:00:00Z`).getUTCDay();
    let available = merge(
      (workspace.availability || [])
        .filter((slot) => slot.dayOfWeek === weekday)
        .map((slot) => [slot.startMinute, slot.endMinute]),
    );
    const exceptions = (workspace.exceptions || []).filter(
      (exception) => exception.date === key,
    );
    available = merge([
      ...available,
      ...exceptions
        .filter((exception) => exception.isAvailable)
        .map((exception) => [exception.startMinute, exception.endMinute]),
    ]);
    for (const exception of exceptions.filter(
      (exception) => !exception.isAvailable,
    ))
      available = subtract(available, [
        exception.startMinute,
        exception.endMinute,
      ]);
    for (const event of workspace.events || []) {
      const span = interval(event, key, calendar);
      if (span) available = subtract(available, span);
    }
    const planned = duration(
      merge(
        sessions
          .map((session) => interval(session, key, calendar))
          .filter(Boolean),
      ),
    );
    const free = duration(available);
    const calendarBusy = duration(
      merge(
        calendarEntries
          .map((item) => interval(item, key, calendar))
          .filter(Boolean),
      ),
    );
    const minuteNow = calendar.minute(now, key);
    const calendarRemaining = duration(
      merge(
        calendarEntries
          .filter(
            (item) =>
              item.kind === "event" ||
              ["Scheduled", "Running", "Paused"].includes(item.status),
          )
          .map((item) => interval(item, key, calendar))
          .filter(Boolean)
          .map(([start, end]) => [Math.max(start, minuteNow), end]),
      ),
    );
    const capacityDay = insights?.days?.find((day) => day.date === key);
    const dailyLimit = workspace.preference.maximumDailyMinutes ?? 480;
    const consumed = duration(
      merge(
        sessions
          .filter((session) => session.status === "Completed")
          .map((session) => interval(session, key, calendar))
          .filter(Boolean),
      ),
    );
    const reserved = duration(
      merge(
        sessions
          .filter((session) =>
            ["Scheduled", "Running", "Paused"].includes(session.status),
          )
          .map((session) => interval(session, key, calendar))
          .filter(Boolean),
      ),
    );
    return {
      key,
      free: capacityDay?.calendarFreeMinutes ?? free,
      planned,
      calendarBusy,
      calendarRemaining,
      reserved: capacityDay?.reservedMinutes ?? reserved,
      schedulable: capacityDay?.schedulableMinutes ?? null,
      capacity: Number.isFinite(insights?.capacityFactor)
        ? (capacityDay?.effectiveCapacityMinutes ??
          Math.max(
            0,
            Math.min(dailyLimit, Math.floor(free * insights.capacityFactor)) -
              consumed,
          ))
        : null,
    };
  });
  const timeline = [
    ...daySessions.map((session) => ({
      ...session,
      kind: "session",
      span: interval(session, today, calendar),
    })),
    ...(workspace.events || []).map((event) => ({
      ...event,
      kind: "event",
      span: interval(event, today, calendar),
    })),
  ]
    .filter((item) => item.span)
    .sort((a, b) => a.span[0] - b.span[0]);
  const occupied = merge(timeline.map((item) => item.span));
  const minuteNow = calendar.minute(now, today);
  const calendarMinutes = duration(occupied);
  const elapsedCalendarMinutes = duration(
    merge(occupied.map(([start, end]) => [start, Math.min(end, minuteNow)])),
  );
  const calendarStats = {
    total: timeline.length,
    past: timeline.filter((item) => Date.parse(item.end) <= now).length,
    current: timeline.filter(
      (item) => Date.parse(item.start) <= now && Date.parse(item.end) > now,
    ).length,
    upcoming: timeline.filter((item) => Date.parse(item.start) > now).length,
    percent: calendarMinutes
      ? Math.round((100 * elapsedCalendarMinutes) / calendarMinutes)
      : 0,
    remainingMinutes: days[0].calendarRemaining,
    horizonCount: calendarEntries.filter(
      (item) =>
        calendar.minute(item.start, today) < 7 * 1440 &&
        calendar.minute(item.end, today) > 0,
    ).length,
    horizonRemaining: days.reduce((sum, day) => sum + day.calendarRemaining, 0),
  };
  const calendarMilestones = calendarEntries
    .filter(
      (item) =>
        Date.parse(item.end) > now &&
        (item.kind === "event" ||
          ["Scheduled", "Running", "Paused"].includes(item.status)),
    )
    .map((item) => ({
      ...item,
      ongoing: Date.parse(item.start) <= now,
      at: Date.parse(item.start) <= now ? item.end : item.start,
    }))
    .sort((a, b) => Date.parse(a.at) - Date.parse(b.at));
  const breaks = [];
  const breakMinutes = workspace.preference.breakMinutes || 0;
  for (let index = 0; index < occupied.length - 1; index++) {
    const end = occupied[index][1];
    if (breakMinutes > 0 && occupied[index + 1][0] - end >= breakMinutes)
      breaks.push({ span: [end, end + breakMinutes] });
  }
  const focusSpan = focus && interval(focus, today, calendar);
  const focusMinutes = focusSpan ? focusSpan[1] - focusSpan[0] : 0;
  const tasks = workspace.tasks.filter(
    (task) => !["Completed", "Cancelled"].includes(task.status),
  );
  const due = [...tasks].sort(
    (a, b) => new Date(a.deadline) - new Date(b.deadline),
  );
  const queue = [...tasks].sort(
    (a, b) =>
      b.priority - a.priority || new Date(a.deadline) - new Date(b.deadline),
  );
  const ratio = insights?.biases?.find((bias) => bias.taskType === "All");
  const accuracy =
    insights?.plannedMinutes > 0 && insights?.actualMinutes > 0
      ? Math.round(
          (Math.min(insights.plannedMinutes, insights.actualMinutes) /
            Math.max(insights.plannedMinutes, insights.actualMinutes)) *
            100,
        )
      : null;
  return {
    today,
    days,
    daySessions,
    focus,
    focusMinutes,
    scheduledMinutes: Math.max(0, days[0].planned - focusMinutes),
    restMinutes: breaks.length * breakMinutes,
    breaks,
    timeline,
    queue,
    due,
    ratio,
    accuracy,
    now,
    hasAvailability,
    calendarStats,
    calendarMilestones,
    availableMinutes: days[0].free,
    capacityMinutes: days[0].capacity,
  };
}
