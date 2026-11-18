const activeSession = new Set(["Scheduled", "Running", "Paused"]);
const blockingWarnings = new Set([
  "SOFT_DEADLINE_EXCEEDED",
  "DEADLINE_CONFLICT",
]);

const previews = new Map();
const previewListeners = new Set();
let previewKey;

function currentPreviews(workspace) {
  const { id } = workspace?.profile || {};
  const { revision } = workspace?.preference || {};
  const key = id && revision ? id + ":" + revision : null;
  if (key !== previewKey) {
    previews.clear();
    previewKey = key;
  }
  return key ? previews : null;
}

export function rememberTaskPreview(taskId, plan, workspace) {
  const current = currentPreviews(workspace);
  if (
    plan &&
    !plan.isApplied &&
    plan.revision === workspace.preference?.revision
  )
    current?.set(taskId, { plan, expires: Date.now() + 29 * 60000 });
  else current?.delete(taskId);
  previewListeners.forEach((listener) => listener());
}

export function subscribeTaskPreviews(listener) {
  previewListeners.add(listener);
  return () => previewListeners.delete(listener);
}

export function taskPreview(taskId, workspace, now = Date.now()) {
  const entry = currentPreviews(workspace)?.get(taskId);
  return entry?.expires > now ? entry.plan : null;
}

export function taskPlanShortfall(task, plan) {
  if (!plan || !task.deadline) return 0;
  const deadline = Date.parse(task.deadline);
  const unscheduled = Math.max(
    0,
    ...plan.warnings
      .filter((warning) => warning.taskId === task.id)
      .map((warning) => warning.unscheduledMinutes || 0),
  );
  const late = plan.sessions.reduce((total, session) => {
    if (session.taskId !== task.id) return total;
    const outside =
      Date.parse(session.end) - Math.max(deadline, Date.parse(session.start));
    return total + Math.max(0, outside) / 60000;
  }, 0);
  return Math.ceil(unscheduled + late);
}

export function taskScheduleStatus(
  task,
  sessions,
  now = Date.now(),
  preview = null,
) {
  if (!task.deadline || ["Completed", "Cancelled"].includes(task.status))
    return null;
  const deadline = Date.parse(task.deadline);
  if (!Number.isFinite(deadline)) return null;
  if (deadline <= now) return { label: "Quá hạn", tone: "late" };
  if (!(task.estimatedMinutes > 0) || !(task.remainingMinutes > 0)) return null;
  const reserved = (sessions || []).reduce((total, session) => {
    if (
      session.taskId !== task.id ||
      !activeSession.has(session.status) ||
      Date.parse(session.end) <= now ||
      Date.parse(session.end) > deadline
    )
      return total;
    return (
      total +
      Math.max(0, Date.parse(session.end) - Date.parse(session.start)) / 60000
    );
  }, 0);
  const shortfall = taskPlanShortfall(task, preview);
  if (shortfall > 0) {
    return {
      label:
        deadline > Date.parse(preview.end)
          ? "Chưa xếp đủ trong khoảng xem trước"
          : "Thiếu " + shortfall + " phút trước hạn",
      tone: "risk",
    };
  }
  if (!reserved) return { label: "Chưa xếp lịch", tone: "neutral" };
  const missing = Math.max(0, Math.ceil(task.remainingMinutes - reserved));
  return missing
    ? { label: "Thiếu " + missing + " phút trước hạn", tone: "risk" }
    : { label: "Đủ lịch trước hạn", tone: "ready" };
}

export function hasWorkHours(workspace) {
  return Boolean(
    workspace.availabilityPolicy?.confirmed ||
    workspace.availability?.length ||
    workspace.exceptions?.some((item) => item.isAvailable),
  );
}

export function taskPlanInput(task, preserveExisting, triggerEvent = "MANUAL") {
  const now = Date.now();
  const daysToDeadline = Math.ceil(
    (Date.parse(task.deadline) - now) / 86400000,
  );
  return {
    start: new Date(now).toISOString(),
    horizonDays: Math.min(30, Math.max(1, daysToDeadline || 1)),
    affectedTaskIds: [task.id],
    isReschedule: true,
    triggerEvent,
    strict: true,
    preserveExisting,
  };
}

export function planShortfall(plan) {
  return plan.warnings.some(
    (warning) =>
      warning.unscheduledMinutes > 0 || blockingWarnings.has(warning.code),
  );
}

export function replacedSessions(plan, sessions) {
  const kept = new Set(
    plan.sessions
      .filter((session) => session.preserved)
      .map((session) => session.id),
  );
  return sessions.filter(
    (session) =>
      session.status === "Scheduled" &&
      !kept.has(session.id) &&
      Date.parse(session.start) >= Date.parse(plan.start) &&
      Date.parse(session.start) < Date.parse(plan.end),
  );
}

export function canAutoApplyTaskPlan(task, plan, workspace) {
  const added = plan.sessions.filter((session) => !session.preserved);
  return (
    workspace.preference?.autoReschedule !== false &&
    plan.strict === true &&
    Date.parse(plan.end) > Date.parse(plan.start) &&
    Boolean(
      plan.revision && plan.revision === workspace.preference?.revision,
    ) &&
    !plan.requiresConfirmation &&
    !plan.hasUrgentReplan &&
    !plan.hasDeadlineProtection &&
    !plan.isOptimization &&
    !planShortfall(plan) &&
    !replacedSessions(plan, workspace.sessions || []).length &&
    added.length > 0 &&
    added.every(
      (session) =>
        session.taskId === task.id &&
        Date.parse(session.end) <= Date.parse(task.deadline),
    )
  );
}

const weekdays = new Map([
  ["hai", 1],
  ["ba", 2],
  ["tu", 3],
  ["nam", 4],
  ["sau", 5],
  ["bay", 6],
  ["2", 1],
  ["3", 2],
  ["4", 3],
  ["5", 4],
  ["6", 5],
  ["7", 6],
]);

export function quickTaskTiming(text, today) {
  text = text.normalize("NFC");
  const normalized = text
    .toLocaleLowerCase("vi")
    .normalize("NFD")
    .replace(/\p{M}/gu, "");
  const deadline = normalized.match(
    /(?:^|[\s,;])(?:han(?:\s+chot)?|deadline)\s+(?:vao\s+)?(thu\s+(?:hai|ba|tu|nam|sau|bay|[2-7])|chu\s+nhat|hom\s+nay|(?:ngay\s+)?mai|\d{1,2}\/\d{1,2}(?:\/\d{4})?)(?:\s+(?:luc\s+)?(\d{1,2})(?:[:h](\d{2})|h))?\s*[.!]?$/,
  );
  const beforeDeadline = deadline
    ? normalized.slice(0, deadline.index)
    : normalized;
  const duration = beforeDeadline.match(
    /(?:\s+(?:trong|mat|can)\s+|\s+)(\d{1,5})\s*phut\s*[,;]?\s*$/,
  );
  const estimate = Number(duration?.[1]);
  const result = {
    name: text
      .slice(0, duration?.index ?? deadline?.index ?? text.length)
      .replace(/[\s,;]+$/, "")
      .trim(),
    estimate: estimate > 0 && estimate <= 43200 ? estimate : null,
    deadline: null,
  };
  if (!deadline) return result;
  const [, day, hour, minute] = deadline;
  if (Number(hour) > 23 || Number(minute) > 59) return result;
  const clock =
    hour == null ? "23:59" : hour.padStart(2, "0") + ":" + (minute ?? "00");
  const date = new Date(today + "T12:00:00Z");
  if (day.includes("/")) {
    const [dayOfMonth, month, year = today.slice(0, 4)] = day.split("/");
    const key =
      year + "-" + month.padStart(2, "0") + "-" + dayOfMonth.padStart(2, "0");
    const target = new Date(key + "T12:00:00Z");
    if (
      !Number.isFinite(target.getTime()) ||
      !target.toISOString().startsWith(key)
    )
      return result;
    result.deadline = key + "T" + clock;
    return result;
  }
  const weekday = day.startsWith("chu") ? 0 : weekdays.get(day.split(/\s+/)[1]);
  const offset = day.endsWith("mai")
    ? 1
    : weekday == null
      ? 0
      : (weekday - date.getUTCDay() + 7) % 7;
  date.setUTCDate(date.getUTCDate() + offset);
  result.deadline = date.toISOString().slice(0, 10) + "T" + clock;
  return result;
}
