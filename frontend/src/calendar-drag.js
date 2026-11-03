import { localInput, toUtc } from "./ui.jsx";
export const hourHeight = 64;
export const minuteHeight = hourHeight / 60;
export const snapMinutes = 15;
export function wallMinute(value) {
  const [, time] = localInput(value).split("T");
  const [hour, minute] = time.split(":").map(Number);
  return hour * 60 + minute;
}
export function dateShift(date, days) {
  const value = new Date(`${date}T12:00:00Z`);
  value.setUTCDate(value.getUTCDate() + days);
  return value.toISOString().slice(0, 10);
}
export function atMinute(date, minute) {
  const day = dateShift(date, Math.floor(minute / 1440));
  const value = ((minute % 1440) + 1440) % 1440;
  return toUtc(
    `${day}T${String(Math.floor(value / 60)).padStart(2, "0")}:${String(value % 60).padStart(2, "0")}`,
  );
}
export function moveRange(item, date, minute, resize = false) {
  const snapped = resize
    ? Math.max(
        0,
        Math.min(1440, Math.round(minute / snapMinutes) * snapMinutes),
      )
    : Math.round(minute / snapMinutes) * snapMinutes;
  const time = atMinute(date, snapped);
  return resize
    ? { start: item.start, end: time }
    : {
        start: time,
        end: new Date(
          Date.parse(time) + Date.parse(item.end) - Date.parse(item.start),
        ).toISOString(),
      };
}
export function shiftRange(item, days = 0, minutes = 0, resize = false) {
  const current = localInput(resize ? item.end : item.start);
  const [date, time] = current.split("T");
  const [h, m] = time.split(":").map(Number);
  const next = atMinute(dateShift(date, days), h * 60 + m + minutes);
  return resize
    ? { start: item.start, end: next }
    : {
        start: next,
        end: new Date(
          Date.parse(next) + Date.parse(item.end) - Date.parse(item.start),
        ).toISOString(),
      };
}
export function canDragCalendarItem(entry, now = Date.now()) {
  const item = entry.event || entry.session;
  return (
    Date.parse(item.start) > now &&
    (entry.event ||
      (item.status === "Scheduled" &&
        !item.isLocked &&
        !item.actualSeconds &&
        !item.segmentStartedAt))
  );
}
export function daySegments(entries, date) {
  const start = Date.parse(atMinute(date, 0)),
    end = Date.parse(atMinute(date, 1440));
  const segments = entries
    .map((entry) => {
      const item = entry.event || entry.session;
      return {
        ...entry,
        item,
        key: `${entry.event ? "Event" : "Session"}-${item.id}`,
        from: Math.max(start, Date.parse(item.start)),
        to: Math.min(end, Date.parse(item.end)),
      };
    })
    .filter((x) => x.to > x.from)
    .sort((a, b) => a.from - b.from || b.to - a.to);
  const clusters = [];
  let cluster = [];
  let until = 0;
  for (const entry of segments) {
    if (cluster.length && entry.from >= until) {
      clusters.push(cluster);
      cluster = [];
      until = 0;
    }
    cluster.push(entry);
    until = Math.max(until, entry.to);
  }
  if (cluster.length) clusters.push(cluster);
  return clusters.flatMap((group) => {
    const lanes = [];
    for (const entry of group) {
      let lane = lanes.findIndex((end) => end <= entry.from);
      if (lane < 0) lane = lanes.length;
      lanes[lane] = entry.to;
      entry.lane = lane;
    }
    return group.map((entry) => ({
      ...entry,
      lanes: lanes.length,
      top: (entry.from === start ? 0 : wallMinute(entry.from)) * minuteHeight,
      height: Math.max(
        24,
        ((entry.to === end ? 1440 : wallMinute(entry.to)) -
          (entry.from === start ? 0 : wallMinute(entry.from))) *
          minuteHeight,
      ),
    }));
  });
}
