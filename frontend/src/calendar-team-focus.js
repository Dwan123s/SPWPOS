export function teamSessionFocus(sessions, tasks) {
  const teamTasks = new Map(tasks.filter((task) => task.isTeamTask).map((task) => [task.id, task]));
  const sessionsByTask = new Map();
  const focusBySession = new Map();

  for (const session of sessions) {
    if (!teamTasks.has(session.taskId) || ["Skipped", "Missed", "Cancelled"].includes(session.status)) continue;
    const taskSessions = sessionsByTask.get(session.taskId) ?? [];
    taskSessions.push(session);
    sessionsByTask.set(session.taskId, taskSessions);
  }

  for (const [taskId, taskSessions] of sessionsByTask) {
    const task = teamTasks.get(taskId);
    const items = (task.teamChecklist ?? []).filter((item) => item.text?.trim());
    taskSessions.sort((a, b) => Date.parse(a.start) - Date.parse(b.start) || a.id.localeCompare(b.id));
    const total = taskSessions.length;

    taskSessions.forEach((session, index) => {
      // Checklist order suggests a sequence. It does not specify how long each item takes.
      const from = Math.floor(index * items.length / total);
      const to = Math.floor((index + 1) * items.length / total);
      focusBySession.set(session.id, {
        label: `Phiên ${index + 1}/${total}`,
        items: items.slice(from, Math.max(from + 1, to)),
      });
    });
  }

  return focusBySession;
}
