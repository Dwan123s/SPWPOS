import { useState } from "react";
import { dateFmt, dayKey, icon, timeFmt } from "./ui.jsx";

export function NotePlanPreview({
  result,
  workspace,
  horizonDays,
  chosenDate,
  onChooseDate,
  confirmation = false,
  headingRef,
}) {
  const [chosen, setChosen] = useState(null);
  const sessions = result.proposedPlan.sessions || [];
  const first = dayKey(result.proposedPlan.start || Date.now());
  const dates = Array.from(
    { length: Math.min(30, Math.max(1, horizonDays)) },
    (_, index) => {
      const date = new Date(`${first}T12:00:00Z`);
      date.setUTCDate(date.getUTCDate() + index);
      return date.toISOString().slice(0, 10);
    },
  );
  const requestedDate = chosenDate ?? chosen;
  const date = dates.includes(requestedDate)
    ? requestedDate
    : dates.find(
        (value) =>
          sessions.some((session) => dayKey(session.start) === value) ||
          (confirmation &&
            (workspace?.events || []).some(
              (event) => dayKey(event.start) === value,
            )),
      ) || dates[0];
  const names = new Map([
    ...(workspace?.tasks || []).map((task) => [task.id, task.name]),
    ...(result.after.tasks || []).map((task) => [task.taskId, task.name]),
  ]);
  const entries = [
    ...sessions
      .filter((session) => dayKey(session.start) === date)
      .map((session) => ({
        ...session,
        name:
          session.taskName || names.get(session.taskId) || "Phiên công việc",
        kind: session.preserved ? "existing" : "new",
      })),
    ...(workspace?.events || [])
      .filter(
        (event) =>
          dayKey(event.start) <= date &&
          dayKey(new Date(new Date(event.end).getTime() - 1)) >= date,
      )
      .map((event) => ({ ...event, kind: "fixed" })),
  ].sort((a, b) => new Date(a.start) - new Date(b.start));
  return (
    <section className="note-calendar-preview" aria-label="Lịch thử từ ghi chú">
      <div className="note-preview-heading">
        <div>
          <span className="note-section-kicker">VIỆC CẠNH LỊCH</span>
          <h3 ref={headingRef} tabIndex={headingRef ? -1 : undefined}>
            {icon("calendar")} Lịch dự kiến
          </h3>
        </div>
        <span className="note-preview-badge">Chưa lưu</span>
      </div>
      <label className="note-preview-date">
        Xem ngày
        <select
          aria-label="Ngày xem lịch dự kiến"
          value={date}
          onChange={(event) => (onChooseDate || setChosen)(event.target.value)}
        >
          {dates.map((value) => (
            <option key={value} value={value}>
              {dateFmt(`${value}T12:00:00Z`, {
                weekday: "long",
                timeZone: "UTC",
              })}
            </option>
          ))}
        </select>
      </label>
      <div className="note-preview-agenda">
        {entries.map((entry, index) => (
          <article
            className={`note-preview-entry note-entry-${entry.kind}`}
            key={entry.id || `${entry.taskId}-${index}`}
          >
            <div className="note-preview-hours">
              <strong>
                {dayKey(entry.start) < date ? "00:00" : timeFmt(entry.start)}
              </strong>
              <span>
                {dayKey(entry.end) > date ? "24:00" : timeFmt(entry.end)}
              </span>
            </div>
            <div className="note-preview-event">
              <strong>{entry.name}</strong>
              <span>
                {entry.kind === "fixed"
                  ? "Sự kiện cố định"
                  : entry.kind === "existing"
                    ? "Lịch đang có · giữ nguyên"
                    : "Việc mới từ ghi chú"}
              </span>
            </div>
          </article>
        ))}
        {!entries.length && (
          <p className="note-preview-empty">
            Chưa có phiên hoặc sự kiện trong ngày này. Xem cảnh báo và chọn một
            ngày khác.
          </p>
        )}
      </div>
      <p className="note-preview-footnote">
        {confirmation ? (
          "Đây là lịch đề xuất, chưa lưu. Chọn Lưu sự kiện & công việc để xác nhận."
        ) : (
          <>
            Đây là lịch thử. Chọn <strong>Tạo công việc & xem lịch</strong> để
            lấy bản đề xuất và xác nhận lưu. Lịch có thể thay đổi theo thời điểm
            tạo.
          </>
        )}
      </p>
    </section>
  );
}
