import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { state, dayKey, timeFmt, icon } from "./ui.jsx";
import {
  hourHeight,
  minuteHeight,
  moveRange,
  shiftRange,
  daySegments,
  canDragCalendarItem,
  atMinute,
  wallMinute,
} from "./calendar-drag.js";
import "./calendar-week.css";

export function CalendarWeek({
  dates,
  entries,
  block,
  fixed,
  now,
  onWorkspaceChanged = async () => {},
}) {
  const host = useRef(null),
    scroll = useRef(null),
    gesture = useRef(null),
    suppressClick = useRef(false),
    busyRef = useRef(false),
    mounted = useRef(false);
  const [draft, setDraft] = useState(null),
    [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [undo, setUndo] = useState(null),
    [message, setMessage] = useState("");
  const allEntries = [
    ...state.workspace.sessions.map((session) => ({ session })),
    ...state.workspace.events.map((event) => ({ event })),
  ];
  const days = dates.map((date) => ({
    date,
    segments: daySegments(allEntries, date),
  }));
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      gesture.current?.cleanup?.();
    };
  }, []);
  useLayoutEffect(() => {
    if (scroll.current) scroll.current.scrollTop = 7 * hourHeight;
  }, [dates[0]]);
  async function save(entry, range, inverse = null) {
    if (busyRef.current) return;
    const item = entry.event || entry.session;
    const userId = state.workspace.profile?.id;
    if (
      (range.start === item.start && range.end === item.end) ||
      (Date.parse(range.start) === Date.parse(item.start) &&
        Date.parse(range.end) === Date.parse(item.end))
    )
      return;
    busyRef.current = true;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const result = await api(
        "/api/calendar/move",
        "POST",
        inverse || {
          kind: entry.event ? "Event" : "Session",
          id: item.id,
          ...range,
          revision: state.workspace.preference.revision,
          expectedStart: item.start,
          expectedEnd: item.end,
        },
      );
      if (!mounted.current || state.workspace?.profile?.id !== userId) return;
      const changed = { ...state.workspace };
      const list = entry.event ? "events" : "sessions";
      changed[list] = changed[list].map((value) =>
        value.id === item.id
          ? { ...value, start: result.start, end: result.end }
          : value,
      );
      changed.preference = { ...changed.preference, revision: result.revision };
      state.workspace = changed;
      // Update card positions immediately after the server accepts; refresh the shared workspace next.
      if (mounted.current) {
        setUndo(
          inverse
            ? null
            : {
                entry: {
                  [entry.event ? "event" : "session"]: {
                    ...item,
                    start: result.start,
                    end: result.end,
                  },
                },
                body: {
                  kind: result.kind,
                  id: result.id,
                  start: result.previousStart,
                  end: result.previousEnd,
                  revision: result.revision,
                  expectedStart: result.start,
                  expectedEnd: result.end,
                },
              },
        );
        setMessage(inverse ? "Đã hoàn tác thay đổi giờ." : "Đã cập nhật lịch.");
      }
      try {
        await onWorkspaceChanged();
      } catch {
        if (mounted.current)
          setError(
            "Thay đổi đã lưu. Chưa tải lại được dữ liệu; hãy làm mới để đối chiếu.",
          );
      }
    } catch (failure) {
      if (mounted.current) {
        setError(
          failure.message || "Không thể đổi giờ. Lịch cũ được giữ nguyên.",
        );
        if (failure.code === "CALENDAR_STALE") setUndo(null);
      }
    } finally {
      busyRef.current = false;
      if (mounted.current) {
        setBusy(false);
        setDraft(null);
      }
    }
  }
  function pointerDown(event, entry, resize = false) {
    if (
      busyRef.current ||
      gesture.current ||
      event.button !== 0 ||
      !canDragCalendarItem(entry, now)
    )
      return;
    if (
      event.pointerType === "touch" &&
      !event.target.closest("[data-drag-handle]")
    )
      return;
    if (event.target.closest("[data-resize-handle]") && !resize) return;
    const node = event.currentTarget,
      item = entry.event || entry.session;
    const track = node.closest("[data-calendar-date]");
    const rect = node.getBoundingClientRect();
    const offset = resize ? 0 : (event.clientY - rect.top) / minuteHeight;
    // Offset within an overnight segment is added to the item's actual beginning.
    const clippedOffset = resize
      ? 0
      : ((Date.parse(track.dataset.calendarDate) -
          Date.parse(dayKey(item.start))) /
          86400000) *
          1440 -
        wallMinute(item.start);
    const grab = offset + Math.max(0, clippedOffset);
    const info = {
      entry,
      resize,
      x: event.clientX,
      y: event.clientY,
      pointerId: event.pointerId,
      active: false,
      valid: false,
      range: null,
    };
    gesture.current = info;
    const update = () => {
      if (!info.active) return;
      const clip = scroll.current.getBoundingClientRect();
      const lane = [
        ...host.current.querySelectorAll("[data-calendar-date]"),
      ].find((el) => {
        const r = el.getBoundingClientRect();
        return info.x >= r.left && info.x < r.right;
      });
      info.valid =
        !!lane &&
        info.x >= clip.left &&
        info.x <= clip.right &&
        info.y >= clip.top + 64 &&
        info.y <= clip.bottom;
      if (!info.valid) {
        info.range = null;
        setDraft(null);
        return;
      }
      try {
        const r = lane.getBoundingClientRect();
        const range = moveRange(
          item,
          lane.dataset.calendarDate,
          (info.y - r.top) / minuteHeight - grab,
          resize,
        );
        if (
          Date.parse(range.end) <= Date.parse(range.start) ||
          Date.parse(range.start) < Date.now()
        ) {
          info.valid = false;
          info.range = null;
          setDraft(null);
          return;
        }
        info.range = range;
        setDraft({ entry, range, resize });
      } catch {
        info.valid = false;
        info.range = null;
        setDraft(null);
      }
    };
    const move = (e) => {
      if (e.pointerId !== info.pointerId) return;
      info.x = e.clientX;
      info.y = e.clientY;
      if (
        !info.active &&
        Math.hypot(info.x - event.clientX, info.y - event.clientY) < 6
      )
        return;
      info.active = true;
      e.preventDefault();
      document.body.classList.add("calendar-is-dragging");
      update();
    };
    const finish = (e) => {
      if (e.pointerId !== info.pointerId) return;
      const active = info.active,
        range = info.range,
        valid = info.valid;
      cleanup();
      if (active) {
        suppressClick.current = true;
        setTimeout(() => {
          suppressClick.current = false;
        }, 0);
        if (valid && range) void save(entry, range);
        else setDraft(null);
      }
    };
    const cancel = () => {
      cleanup();
      setDraft(null);
    };
    const key = (e) => {
      if (e.key === "Escape") {
        e.preventDefault();
        cancel();
      }
    };
    const autoScroll = setInterval(() => {
      if (!info.active || !scroll.current) return;
      const r = scroll.current.getBoundingClientRect();
      const sc = scroll.current;
      let changed = false;
      if (info.y > r.bottom - 45) {
        sc.scrollTop += 18;
        changed = true;
      } else if (info.y < r.top + 100) {
        sc.scrollTop -= 18;
        changed = true;
      }
      if (info.x > r.right - 35) {
        sc.scrollLeft += 15;
        changed = true;
      } else if (info.x < r.left + 35) {
        sc.scrollLeft -= 15;
        changed = true;
      }
      if (changed) update();
    }, 50);
    function cleanup() {
      clearInterval(autoScroll);
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", finish);
      window.removeEventListener("pointercancel", cancel);
      window.removeEventListener("keydown", key);
      document.body.classList.remove("calendar-is-dragging");
      gesture.current = null;
    }
    info.cleanup = cleanup;
    window.addEventListener("pointermove", move, { passive: false });
    window.addEventListener("pointerup", finish);
    window.addEventListener("pointercancel", cancel);
    window.addEventListener("keydown", key);
  }
  function keyboard(event, entry, resize = false) {
    if (
      !event.altKey ||
      !["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(event.key)
    )
      return;
    event.preventDefault();
    event.stopPropagation();
    if (busyRef.current || !canDragCalendarItem(entry, now)) return;
    const days = resize
      ? 0
      : event.key === "ArrowLeft"
        ? -1
        : event.key === "ArrowRight"
          ? 1
          : 0;
    const minutes =
      event.key === "ArrowUp" ? -15 : event.key === "ArrowDown" ? 15 : 0;
    if (resize && !minutes) return;
    try {
      void save(
        entry,
        shiftRange(entry.event || entry.session, days, minutes, resize),
      );
    } catch (failure) {
      setError(failure.message);
    }
  }
  return (
    <div ref={host} className="calendar-drag-week" aria-busy={busy}>
      <div className="calendar-drag-feedback" aria-live="polite">
        {busy ? (
          <span role="status">Đang kiểm tra và lưu giờ mới…</span>
        ) : (
          message && <span role="status">{message}</span>
        )}
        {undo && !busy && (
          <button
            type="button"
            className="btn ghost small"
            onClick={() =>
              save(
                undo.entry,
                { start: undo.body.start, end: undo.body.end },
                undo.body,
              )
            }
          >
            {icon("undo")}Hoàn tác
          </button>
        )}
        {error && <p role="alert">{error}</p>}
      </div>
      <p className="sr-only" id="calendar-drag-help">
        Kéo thẻ để đổi giờ/ngày · Kéo mép dưới để đổi thời lượng · Esc để hủy.
        Trên điện thoại, dùng nút kéo. Bàn phím: giữ Alt + phím mũi tên.
      </p>
      <div
        ref={scroll}
        className="calendar-scroll calendar-time-scroll"
        role="region"
        aria-label="Lịch 7 ngày, cuộn dọc và ngang để xem thêm"
        tabIndex="0"
        onClickCapture={(event) => {
          if (suppressClick.current) {
            event.preventDefault();
            event.stopPropagation();
          }
        }}
      >
        <div className="week-grid calendar-hour-grid">
          <div className="calendar-hours" aria-hidden="true">
            <div className="calendar-hour-corner">
              {state.workspace.preference.timeZoneId === "Asia/Ho_Chi_Minh"
                ? "GMT+7"
                : "Giờ"}
            </div>
            <div className="calendar-hour-labels">
              {Array.from({ length: 24 }, (_, h) => (
                <span key={h} style={{ top: h * hourHeight }}>
                  {String(h).padStart(2, "0")}:00
                </span>
              ))}
            </div>
          </div>
          {days.map(({ date, segments }) => (
            <section
              key={date}
              className={`week-day ${date === dayKey(now) ? "today" : ""}`}
            >
              <div className="week-day-header">
                <span>
                  {new Intl.DateTimeFormat("vi-VN", {
                    weekday: "short",
                    timeZone: "UTC",
                  }).format(new Date(`${date}T12:00:00Z`))}
                </span>
                <strong>{date.slice(8)}</strong>
                <small>{segments.length} mục</small>
              </div>
              <div
                className="calendar-day-track"
                data-calendar-date={date}
                data-day-start={atMinute(date, 0)}
                style={{ height: 24 * hourHeight }}
              >
                {segments.map((segment) => {
                  const entry = segment,
                    editable = canDragCalendarItem(entry, now) && !busy;
                  const dragging =
                    draft &&
                    (draft.entry.event || draft.entry.session).id ===
                      entry.item.id;
                  const sameDay =
                    dayKey(entry.item.start) === date &&
                    dayKey(new Date(Date.parse(entry.item.end) - 1)) === date;
                  return (
                    <div
                      key={segment.key}
                      className={`calendar-positioned-item ${editable ? "is-draggable" : ""} ${dragging ? "is-dragging" : ""} ${segment.height < 80 ? "is-short" : ""}`}
                      style={{
                        top: segment.top,
                        height: segment.height,
                        left: `${(100 * segment.lane) / segment.lanes}%`,
                        width: `${100 / segment.lanes}%`,
                      }}
                      onPointerDown={(event) => pointerDown(event, entry)}
                    >
                      {entry.session
                        ? block(entry.session, now)
                        : fixed(entry.event, date, true, now)}
                      {editable && (
                        <button
                          type="button"
                          className="calendar-drag-handle"
                          data-drag-handle
                          aria-label={`Kéo ${entry.item.name || entry.item.taskName}`}
                          aria-describedby="calendar-drag-help"
                          onKeyDown={(event) => keyboard(event, entry)}
                          onClick={(event) => event.stopPropagation()}
                        >
                          {icon("menu")}
                        </button>
                      )}
                      {editable && sameDay && (
                        <button
                          type="button"
                          className="calendar-resize-handle"
                          data-drag-handle
                          data-resize-handle
                          aria-label={`Đổi thời lượng ${entry.item.name || entry.item.taskName}`}
                          aria-describedby="calendar-drag-help"
                          onPointerDown={(event) => {
                            event.stopPropagation();
                            pointerDown(event, entry, true);
                          }}
                          onKeyDown={(event) => keyboard(event, entry, true)}
                          onClick={(event) => event.stopPropagation()}
                        >
                          <span />
                        </button>
                      )}
                    </div>
                  );
                })}
                {draft &&
                  daySegments(
                    [{ event: { ...draft.range, id: "preview" } }],
                    date,
                  ).map((segment) => (
                    <div
                      key="preview"
                      className="calendar-drag-ghost"
                      style={{ top: segment.top, height: segment.height }}
                    >
                      <strong>
                        {(draft.entry.event || draft.entry.session).name ||
                          (draft.entry.event || draft.entry.session).taskName}
                      </strong>
                      <span>
                        {timeFmt(draft.range.start)}–{timeFmt(draft.range.end)}
                      </span>
                    </div>
                  ))}
              </div>
            </section>
          ))}
        </div>
      </div>
    </div>
  );
}
