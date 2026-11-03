import { useState } from "react";
import { dateFmt, dayKey, icon, timeFmt, zone } from "./ui.jsx";
import "./team-schedule-compare.css";

const sameTime = (a, b) =>
  +new Date(a.start) === +new Date(b.start) &&
  +new Date(a.end) === +new Date(b.end);
const chronological = (items) =>
  [...items].sort(
    (a, b) =>
      +new Date(a.start) - +new Date(b.start) ||
      +new Date(a.end) - +new Date(b.end),
  );

function ScheduleSessions({ items, side, beforeById, afterById }) {
  if (!items.length)
    return (
      <p className="team-compare-empty">
        {icon("calendar")} Chưa có phiên làm việc trong ngày này.
      </p>
    );
  return (
    <ul className="team-compare-sessions">
      {items.map((session) => {
        const previous = beforeById.get(session.id);
        const next = afterById.get(session.id);
        const kept =
          side === "after"
            ? session.preserved
            : next?.preserved && sameTime(session, next);
        const moved =
          side === "after" && !kept && previous && !sameTime(previous, session);
        const kind = kept
          ? "kept"
          : side === "before" || moved
            ? "moved"
            : "proposed";
        const label = kept
          ? "Giữ nguyên"
          : side === "before"
            ? "Sẽ xếp lại"
            : moved
              ? "Đổi giờ"
              : "Thêm / xếp lại";
        const minutes = Math.round(
          (new Date(session.end) - new Date(session.start)) / 60000,
        );
        return (
          <li key={session.id} className={`team-compare-session is-${kind}`}>
            <div className="team-compare-session-top">
              <strong className="team-compare-time">
                {icon("clock")}{" "}
                <time dateTime={session.start}>{timeFmt(session.start)}</time>
                <span>–</span>
                <time dateTime={session.end}>{timeFmt(session.end)}</time>
              </strong>
              <span className={`team-compare-badge is-${kind}`}>
                {kept ? icon("check") : icon("arrow")}
                {label}
              </span>
            </div>
            <p className="team-compare-task">{session.taskName}</p>
            <span className="team-compare-duration">
              {minutes} phút
              {dayKey(session.start) !== dayKey(session.end)
                ? ` · kết thúc ${dateFmt(session.end)}`
                : ""}
            </span>
            {moved && (
              <span className="team-compare-previous">
                Trước: {dateFmt(previous.start)} · {timeFmt(previous.start)}–
                {timeFmt(previous.end)}
              </span>
            )}
          </li>
        );
      })}
    </ul>
  );
}

export function TeamScheduleCompare({ before = [], after = [] }) {
  const [changesOnly, setChangesOnly] = useState(false);
  const beforeById = new Map(before.map((s) => [s.id, s]));
  const afterById = new Map(after.map((s) => [s.id, s]));
  const days = new Map();
  const add = (session, side) => {
    const key = dayKey(session.start);
    if (!days.has(key))
      days.set(key, {
        key,
        date: session.start,
        before: [],
        after: [],
        changed: false,
      });
    const day = days.get(key);
    day[side].push(session);
    day.changed ||=
      side === "after"
        ? !session.preserved
        : !afterById.get(session.id)?.preserved ||
          !sameTime(session, afterById.get(session.id));
  };
  chronological(before).forEach((s) => add(s, "before"));
  chronological(after).forEach((s) => add(s, "after"));
  const orderedDays = [...days.values()].sort((a, b) =>
    a.key.localeCompare(b.key),
  );
  const changedDays = orderedDays.filter((day) => day.changed).length;
  const proposed = after.filter((s) => !s.preserved).length;
  const kept = after.length - proposed;
  const visibleDays = changesOnly
    ? orderedDays.filter((day) => day.changed)
    : orderedDays;
  return (
    <section
      className="team-schedule-comparison"
      aria-label="So sánh lịch trước và sau khi nhận"
    >
      <div className="team-compare-toolbar">
        <div className="team-compare-stats" aria-label="Tóm tắt thay đổi lịch">
          <span className="is-proposed">
            <strong>{proposed}</strong> phiên đề xuất
          </span>
          <span>
            <strong>{kept}</strong> phiên giữ nguyên
          </span>
          <span>
            <strong>{changedDays}</strong> ngày thay đổi
          </span>
        </div>
        <button
          type="button"
          className="team-compare-filter"
          aria-pressed={changesOnly}
          disabled={!changedDays}
          onClick={() => setChangesOnly((value) => !value)}
        >
          {icon(changesOnly ? "check" : "calendar")} Chỉ xem ngày thay đổi
        </button>
      </div>
      <p className="team-compare-guide">
        Đọc hai cột cùng một ngày để so sánh. Phiên đề xuất là phiên được thêm
        hoặc xếp lại.
      </p>
      <div
        className="team-compare-scroll"
        role="region"
        aria-label="Lịch so sánh theo ngày"
        tabIndex={0}
      >
        <div className="team-compare-columns" aria-hidden="true">
          <div>
            {icon("calendar")}
            <div>
              <h4>Lịch trước khi nhận</h4>
              <p>Lịch hiện tại của bạn · {before.length} phiên</p>
            </div>
          </div>
          <div>
            {icon("spark")}
            <div>
              <h4>Lịch sau khi nhận</h4>
              <p>Phương án đang xem · {after.length} phiên</p>
            </div>
          </div>
        </div>
        {visibleDays.map((day) => (
          <section
            className="team-compare-day"
            key={day.key}
            aria-label={dateFmt(day.date, { year: "numeric" })}
          >
            <header>
              <strong>
                {dateFmt(day.date, { weekday: "long", year: "numeric" })}
              </strong>
              <span>
                {day.before.length} phiên trước · {day.after.length} phiên sau
              </span>
            </header>
            <div className="team-compare-day-columns">
              <section
                className="team-compare-before"
                aria-label={`Lịch trước khi nhận ngày ${dateFmt(day.date)}`}
              >
                <h5>Lịch trước khi nhận</h5>
                <ScheduleSessions
                  items={day.before}
                  side="before"
                  beforeById={beforeById}
                  afterById={afterById}
                />
              </section>
              <section
                className="team-compare-after"
                aria-label={`Lịch sau khi nhận ngày ${dateFmt(day.date)}`}
              >
                <h5>Lịch sau khi nhận</h5>
                <ScheduleSessions
                  items={day.after}
                  side="after"
                  beforeById={beforeById}
                  afterById={afterById}
                />
              </section>
            </div>
          </section>
        ))}
        {!visibleDays.length && (
          <p className="team-compare-empty">
            Chưa có phiên làm việc để so sánh.
          </p>
        )}
      </div>
      <small className="team-compare-zone">
        Giờ hiển thị theo múi giờ của bạn: {zone()}.
      </small>
    </section>
  );
}
