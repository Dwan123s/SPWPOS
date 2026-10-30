import { useEffect, useState } from "react";
import "./styles/calendar-time.css";

export const calendarPhases = {
  past: "Đã qua",
  current: "Đang diễn ra",
  future: "Sắp tới",
};

export function calendarPhase(start, end, now) {
  if (new Date(end).getTime() <= now) return "past";
  if (new Date(start).getTime() <= now) return "current";
  return "future";
}

export function CalendarTimeTag({ phase }) {
  return <span className="calendar-time-tag">{calendarPhases[phase]}</span>;
}

export function CalendarClock({ boundaries, children }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const refresh = () => setNow(Date.now());
    // Update at a visible event boundary, and at least once each minute.
    // Refocusing a suspended tab immediately restores the correct labels.
    const delay = boundaries.reduce(
      (next, at) => (at > now ? Math.min(next, at - now) : next),
      60000,
    );
    const timer = setTimeout(refresh, Math.max(1, delay));
    const visible = () => {
      if (!document.hidden) refresh();
    };
    window.addEventListener("focus", refresh);
    document.addEventListener("visibilitychange", visible);
    return () => {
      clearTimeout(timer);
      window.removeEventListener("focus", refresh);
      document.removeEventListener("visibilitychange", visible);
    };
  }, [now, boundaries]);
  return children(now);
}
