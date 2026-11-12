const activeStatuses = new Set([
  "Scheduled",
  "Running",
  "Paused",
  "NeedsReview",
]);
export const activeTask = (task) =>
  !["Completed", "Cancelled"].includes(task.status);
export const isInboxNote = (note) =>
  note.analysisImported === false ||
  (note.analysisImported == null &&
    !note.createdTaskIds?.length &&
    !note.planId);
export function addCalendarDays(day, amount) {
  const date = new Date(`${day}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() + amount);
  return date.toISOString().slice(0, 10);
}
export function smartTaskLists(workspace, now = Date.now()) {
  const zone = workspace.preference?.timeZoneId || "Asia/Ho_Chi_Minh";
  const formatter = new Intl.DateTimeFormat("en-CA", {
    timeZone: zone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  });
  const dayOf = (value) =>
    value != null && Number.isFinite(new Date(value).getTime())
      ? formatter.format(new Date(value))
      : null;
  const today = dayOf(now),
    lastDay = addCalendarDays(today, 6);
  const sessions = new Map();
  const plannedDays = new Map();
  const todayStarts = new Map();
  for (const session of workspace.sessions || []) {
    if (!activeStatuses.has(session.status)) continue;
    const start = dayOf(session.start),
      end = dayOf(new Date(Date.parse(session.end) - 1));
    if (!start || !end) continue;
    if (!sessions.has(session.taskId)) sessions.set(session.taskId, []);
    sessions.get(session.taskId).push({ start, end });
    if (start <= today && end >= today) {
      const time = Date.parse(session.start);
      todayStarts.set(session.taskId, Math.min(time, todayStarts.get(session.taskId) ?? time));
    }
    if (Date.parse(session.end) > now && session.status !== "NeedsReview") {
      const date = start < today ? today : start;
      const previous = plannedDays.get(session.taskId);
      if (!previous || date < previous) plannedDays.set(session.taskId, date);
    }
  }
  function taskDay(task, endDay) {
    const deadline = dayOf(task.deadline);
    if (deadline && deadline < today) return "overdue";
    const candidates = [];
    if (deadline && deadline >= today && deadline <= endDay)
      candidates.push(deadline);
    for (const session of sessions.get(task.id) || []) {
      if (session.start <= endDay && session.end >= today)
        candidates.push(session.start < today ? today : session.start);
    }
    return candidates.sort()[0] || null;
  }
  const tasks = workspace.tasks || [];
  const projectIds = new Set(tasks.map((task) => task.parentId).filter(Boolean));
  const todayTasks = tasks.filter((task) =>
    activeTask(task) && !projectIds.has(task.id) &&
    (todayStarts.has(task.id) || dayOf(task.deadline) === today),
  ).sort((a, b) =>
    (todayStarts.get(a.id) ?? Number.MAX_SAFE_INTEGER) -
    (todayStarts.get(b.id) ?? Number.MAX_SAFE_INTEGER) ||
    (Date.parse(a.deadline) || 0) - (Date.parse(b.deadline) || 0) ||
    b.priority - a.priority,
  );
  function groups(endDay) {
    const grouped = new Map();
    for (const task of tasks.filter(activeTask)) {
      const key = taskDay(task, endDay);
      if (!key) continue;
      if (!grouped.has(key)) grouped.set(key, []);
      grouped.get(key).push(task);
    }
    return [...grouped]
      .sort(([a], [b]) =>
        a === "overdue" ? -1 : b === "overdue" ? 1 : a.localeCompare(b),
      )
      .map(([key, items]) => ({
        key,
        tasks: items.sort(
          (a, b) =>
            (Date.parse(a.deadline) || 0) - (Date.parse(b.deadline) || 0) ||
            b.priority - a.priority,
        ),
      }));
  }
  return {
    today,
    todayTasks,
    lastDay,
    todayGroups: groups(today),
    weekGroups: groups(lastDay),
    all: tasks,
    dayOf,
    plannedDays,
  };
}
