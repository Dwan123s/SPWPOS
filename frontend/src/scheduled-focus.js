const eligible = new Set(["Scheduled", "Running", "Paused"]);
export function scheduledFocus(workspace, now = Date.now()) {
  const tasks = new Map((workspace.tasks || []).map((task) => [task.id, task]));
  const entries = [
    ...(workspace.events || []).map((event) => ({
      ...event,
      kind: "event",
      title: event.name,
      urgent: event.isUrgent === true,
    })),
    ...(workspace.sessions || [])
      .filter((session) => {
        const task = tasks.get(session.taskId);
        return (
          eligible.has(session.status) &&
          !task?.isDeleted &&
          !["Completed", "Cancelled"].includes(task?.status)
        );
      })
      .map((session) => ({
        ...session,
        kind: "session",
        title:
          session.taskName ||
          tasks.get(session.taskId)?.name ||
          "Phiên làm việc",
        urgent: tasks.get(session.taskId)?.priority === 4,
      })),
  ].filter(
    (item) =>
      Number.isFinite(Date.parse(item.start)) &&
      Date.parse(item.end) > Date.parse(item.start) &&
      Date.parse(item.end) > now,
  );
  const priority = (a, b) =>
    Number(b.urgent) - Number(a.urgent) ||
    Number(b.status === "Running") - Number(a.status === "Running") ||
    String(a.id).localeCompare(String(b.id));
  const current = entries
    .filter((item) => Date.parse(item.start) <= now)
    .sort(
      (a, b) =>
        Number(b.urgent) - Number(a.urgent) ||
        Number(b.status === "Running") - Number(a.status === "Running") ||
        Date.parse(b.start) - Date.parse(a.start) ||
        priority(a, b),
    )[0];
  const next = entries
    .filter((item) => Date.parse(item.start) > now)
    .sort(
      (a, b) => Date.parse(a.start) - Date.parse(b.start) || priority(a, b),
    )[0];
  return { current, next };
}

export function scheduleClock(seconds) {
  const total = Math.max(0, Math.ceil(seconds));
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor(total / 60) % 60;
  const rest = total % 60;
  const two = (value) => String(value).padStart(2, "0");
  return `${hours ? `${two(hours)}:` : ""}${two(minutes)}:${two(rest)}`;
}
