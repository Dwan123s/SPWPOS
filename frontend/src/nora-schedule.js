import { dayKey, localInput, state } from "./ui.jsx";

const labels = {
  CLASS: "Lớp học",
  WORK: "Công việc",
  MEETING: "Cuộc họp",
  APPOINTMENT: "Cuộc hẹn",
  OTHER: "Khác",
  STUDY: "Học tập",
  PROGRAMMING: "Lập trình",
  WRITING: "Viết nội dung",
  ADMIN: "Việc cá nhân",
  MEETING_PREP: "Chuẩn bị họp",
};
export function compactScheduleText(text = "") {
  return text
    .replace(
      /\s*\[(CLASS|WORK|MEETING|APPOINTMENT|OTHER|STUDY|PROGRAMMING|WRITING|ADMIN|MEETING_PREP)\]/g,
      (_, code) => ` · ${labels[code]}`,
    )
    .replace(
      /(\d{2}\/\d{2}\/\d{4}) (\d{2}:\d{2})\s*(?:→|->|—|–)\s*\1 (\d{2}:\d{2})/g,
      "$1 · $2–$3",
    );
}
export const displayDate = (date) => date.split("-").reverse().join("/");
export function resultDate(turn) {
  const session = turn?.plan?.sessions?.find((item) => !item.preserved);
  if (session) return dayKey(session.start);
  const date = turn?.changes
    ?.map((item) => item.after || "")
    .join(" ")
    .match(/(\d{2})\/(\d{2})\/(\d{4})/);
  return date ? `${date[3]}-${date[2]}-${date[1]}` : dayKey(new Date());
}
export function conversationLabel(row, turns = []) {
  const first = turns.find((turn) => turn.conversationId === row.id);
  if (!first || row.title !== first.text?.trim().slice(0, 120))
    return row.title;
  const result = turns.find(
    (turn) => turn.conversationId === row.id && turn.changes?.length,
  );
  if (!result) return row.title;
  const names = [
    ...new Set(
      turns
        .filter((turn) => turn.conversationId === row.id)
        .flatMap((turn) => (turn.changes || []).map((change) => change.name))
        .filter(Boolean),
    ),
  ];
  const date = resultDate(result).slice(5).split("-").reverse().join("/");
  return `${date} · ${names.slice(0, 2).join(" + ")}${names.length > 2 ? ` +${names.length - 2}` : ""}`;
}
export function savedEventForChange(change, workspace = state.workspace) {
  const range = (change.after || "").match(
    /(\d{2})\/(\d{2})\/(\d{4}) (\d{2}:\d{2})\s*(?:→|->|—|–)\s*(\d{2})\/(\d{2})\/(\d{4}) (\d{2}:\d{2})/,
  );
  if (!range || change.kind !== "EVENT") return null;
  const from = `${range[3]}-${range[2]}-${range[1]}T${range[4]}`,
    to = `${range[7]}-${range[6]}-${range[5]}T${range[8]}`;
  const matches = (workspace?.events || []).filter(
    (event) =>
      event.name === change.name &&
      localInput(event.start) === from &&
      localInput(event.end) === to,
  );
  return matches.length === 1 ? matches[0] : null;
}
export function calendarLocal(value, zone) {
  return new Intl.DateTimeFormat("sv-SE", {
    timeZone: zone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  })
    .format(new Date(value))
    .replace(" ", "T");
}
function midnight(date, zone) {
  const nominal = Date.parse(`${date}T00:00:00Z`);
  let candidate = nominal;
  for (let i = 0; i < 3; i++)
    candidate += nominal - Date.parse(`${calendarLocal(candidate, zone)}:00Z`);
  if (calendarLocal(candidate, zone) !== `${date}T00:00`)
    throw new Error("Invalid local midnight");
  return candidate;
}
// Gaps are intervals without loaded calendar entries, not promises of availability.
export function timelineForDay(workspace, date) {
  const next = new Date(`${date}T12:00:00Z`);
  next.setUTCDate(next.getUTCDate() + 1);
  const zone = workspace?.preference?.timeZoneId || "Asia/Ho_Chi_Minh";
  const start = midnight(date, zone),
    end = midnight(next.toISOString().slice(0, 10), zone);
  const taskNames = new Map(
    (workspace?.tasks || []).map((task) => [task.id, task.name]),
  );
  const entries = [
    ...(workspace?.events || []).map((event) => ({
      ...event,
      kind: "event",
      label: "Sự kiện cố định",
    })),
    ...(workspace?.sessions || [])
      .filter((session) => session.status !== "Cancelled")
      .map((session) => ({
        ...session,
        kind: "session",
        label: "Phiên công việc",
        name: taskNames.get(session.taskId) || "Công việc",
      })),
  ]
    .filter(
      (entry) =>
        Date.parse(entry.start) < end &&
        Date.parse(entry.end) > start &&
        Date.parse(entry.end) > Date.parse(entry.start),
    )
    .map((entry) => ({
      ...entry,
      from: Math.max(start, Date.parse(entry.start)),
      to: Math.min(end, Date.parse(entry.end)),
      startLabel:
        Date.parse(entry.start) < start
          ? "00:00"
          : calendarLocal(entry.start, zone).slice(11),
      endLabel:
        Date.parse(entry.end) >= end
          ? "24:00"
          : calendarLocal(entry.end, zone).slice(11),
    }))
    .sort((a, b) => a.from - b.from || a.to - b.to);
  const output = [];
  let covered = start;
  for (let i = 0; i < entries.length; i++) {
    const entry = entries[i];
    if (i && entry.from > covered)
      output.push({
        id: `gap-${entry.id}`,
        kind: "gap",
        from: covered,
        to: entry.from,
        startLabel: calendarLocal(covered, zone).slice(11),
        endLabel: entry.startLabel,
        next: entry,
      });
    output.push({
      ...entry,
      overlap:
        entry.from < covered ||
        Boolean(entries[i + 1] && entries[i + 1].from < entry.to),
    });
    covered = Math.max(covered, entry.to);
  }
  return output;
}
