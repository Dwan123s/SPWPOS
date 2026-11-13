const plannedStatuses = new Set(["Scheduled", "Running", "Paused"]);
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

export function plannedTaskStarts(sessions, now = Date.now()) {
  const starts = new Map();
  for (const session of sessions ?? []) {
    if (!plannedStatuses.has(session.status)) continue;
    const start = Date.parse(session.start);
    const end = Date.parse(session.end);
    if (end > now && end > start && start < (starts.get(session.taskId) ?? Infinity)) {
      starts.set(session.taskId, start);
    }
  }
  return starts;
}

export function orderProjectChildren(children, plannedStarts) {
  const byId = new Map(children.map((task) => [task.id, task]));
  const visited = new Set();
  const ordered = [];
  function visit(task) {
    if (visited.has(task.id)) return;
    visited.add(task.id);
    for (const id of task.dependencies ?? []) {
      if (byId.has(id)) visit(byId.get(id));
    }
    ordered.push(task);
  }
  children.forEach(visit);
  // A stable sort keeps dependency order for tied or unscheduled items.
  return ordered.sort((a, b) =>
    (plannedStarts?.get(a.id) ?? Number.MAX_SAFE_INTEGER) - (plannedStarts?.get(b.id) ?? Number.MAX_SAFE_INTEGER),
  );
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
  const isOverdue = (task) => Date.parse(task.deadline) <= now;
  const sessions = new Map();
  const plannedStarts = plannedTaskStarts(workspace.sessions, now);
  const plannedDays = new Map([...plannedStarts].map(([taskId, start]) => {
    const date = dayOf(start);
    return [taskId, date < today ? today : date];
  }));
  const todayStarts = new Map();
  for (const session of workspace.sessions || []) {
    if (!plannedStatuses.has(session.status) && session.status !== "NeedsReview") continue;
    const start = dayOf(session.start),
      end = dayOf(new Date(Date.parse(session.end) - 1));
    if (!start || !end) continue;
    if (!sessions.has(session.taskId)) sessions.set(session.taskId, []);
    sessions.get(session.taskId).push({ start, end });
    if (start <= today && end >= today) {
      const time = Date.parse(session.start);
      todayStarts.set(session.taskId, Math.min(time, todayStarts.get(session.taskId) ?? time));
    }
  }
  function taskDay(task, endDay) {
    const deadline = dayOf(task.deadline);
    if (isOverdue(task)) return "overdue";
    const candidates = [];
    if (deadline && deadline >= today && deadline <= endDay)
      candidates.push(deadline);
    for (const session of sessions.get(task.id) || []) {
      if (session.start <= endDay && session.end >= today)
        candidates.push(session.start < today ? today : session.start);
    }
    return candidates.sort()[0] || null;
  }
  const tasks = (workspace.tasks || []).filter((task) => !task.isDeleted);
  const projectIds = new Set(tasks.map((task) => task.parentId).filter(Boolean));
  const projects = tasks.filter((task) => !task.parentId && !task.isTeamTask && projectIds.has(task.id));
  const personalInbox = tasks.filter((task) => !task.isTeamTask && !task.parentId && !projectIds.has(task.id));
  const todayTasks = tasks.filter((task) =>
    activeTask(task) && !projectIds.has(task.id) &&
    // Show undated work in the sidebar without creating a due date or session.
    (!dayOf(task.deadline) || isOverdue(task) || todayStarts.has(task.id) || dayOf(task.deadline) === today),
  ).sort((a, b) =>
    Number(isOverdue(b)) - Number(isOverdue(a)) ||
    (todayStarts.get(a.id) ?? Number.MAX_SAFE_INTEGER) -
    (todayStarts.get(b.id) ?? Number.MAX_SAFE_INTEGER) ||
    (Date.parse(a.deadline) || Number.MAX_SAFE_INTEGER) - (Date.parse(b.deadline) || Number.MAX_SAFE_INTEGER) ||
    b.priority - a.priority,
  );
  const todayCompleted = tasks.filter((task) =>
    task.status === "Completed" && !projectIds.has(task.id) && dayOf(task.completedAt) === today,
  ).length;
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
    todayCompleted,
    isOverdue,
    lastDay,
    todayGroups: groups(today),
    weekGroups: groups(lastDay),
    all: tasks,
    projects,
    personalInbox,
    dayOf,
    plannedDays,
    plannedStarts,
  };
}
