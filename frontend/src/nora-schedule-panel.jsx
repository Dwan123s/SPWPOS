import { useEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { icon, state } from "./ui.jsx";
import {
  compactScheduleText,
  calendarLocal,
  displayDate,
  resultDate,
  timelineForDay,
} from "./nora-schedule.js";
import { planDay, planHours, planSessionName } from "./nora-chat-ui.jsx";

export function NoraSchedulePanel({
  turn,
  view,
  onViewChange,
  date,
  onDateChange,
  onSuggest,
  onEditEvent,
  refreshKey,
  busy,
}) {
  const [workspace, setWorkspace] = useState(state.workspace);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [updated, setUpdated] = useState(null);
  const [attempt, setAttempt] = useState(0);
  const request = useRef(0);
  useEffect(() => {
    const id = ++request.current;
    setLoading(true);
    setError("");
    setUpdated(null);
    api("/api/workspace")
      .then((value) => {
        if (!Array.isArray(value?.events) || !Array.isArray(value?.sessions))
          throw new Error("Chưa nhận được dữ liệu lịch đầy đủ.");
        if (id === request.current) {
          setWorkspace(value);
          setUpdated(new Date());
        }
      })
      .catch((failure) => {
        if (id === request.current) setError(failure.message);
      })
      .finally(() => {
        if (id === request.current) setLoading(false);
      });
    return () => {
      request.current++;
    };
  }, [attempt, refreshKey]);
  let entries = [],
    timelineError = "";
  try {
    entries = timelineForDay(workspace, date);
  } catch {
    timelineError =
      "Không thể đọc ngày này trong múi giờ tài khoản. Hãy chọn ngày khác.";
  }
  const zone = workspace?.preference?.timeZoneId || "Asia/Ho_Chi_Minh";
  const nowLocal = calendarLocal(Date.now(), zone);
  const gaps = entries.filter((entry) => entry.kind === "gap");
  const lunch = gaps.find(
    (gap) =>
      gap.startLabel <= "12:00" &&
      gap.endLabel >= "12:30" &&
      `${date}T12:00` > nowLocal,
  );
  const travel = gaps.find(
    (gap) =>
      gap.next.kind === "event" &&
      gap.to - gap.from >= 30 * 60000 &&
      calendarLocal(gap.to - 30 * 60000, zone) > nowLocal,
  );
  const draftRange = (name, start, end) =>
    onSuggest(`Thêm ${name} ngày ${displayDate(date)} từ ${start} đến ${end}.`);
  return (
    <section className="nora-schedule-content">
      <div
        className="nora-panel-tabs"
        role="group"
        aria-label="Chế độ xem lịch"
      >
        <button
          type="button"
          aria-pressed={view === "day"}
          onClick={() => onViewChange("day")}
        >
          Lịch trong ngày
        </button>
        <button
          type="button"
          aria-pressed={view === "result"}
          disabled={!turn}
          onClick={() => onViewChange("result")}
        >
          Kết quả yêu cầu
        </button>
      </div>
      {view === "day" ? (
        <>
          <div className="nora-day-controls">
            <label>
              Ngày xem
              <input
                type="date"
                aria-label="Ngày xem lịch"
                value={date}
                onChange={(event) => {
                  if (event.target.value) onDateChange(event.target.value);
                }}
              />
            </label>
            <button
              type="button"
              title="Tải lại dữ liệu lịch đã lưu"
              aria-label="Cập nhật lịch"
              disabled={loading || busy}
              onClick={() => setAttempt((value) => value + 1)}
            >
              {icon("refresh")}
            </button>
          </div>
          <p className="nora-day-source" role="status">
            {loading
              ? "Đang đối chiếu lịch đã lưu…"
              : updated
                ? `Vừa cập nhật lúc ${new Intl.DateTimeFormat("vi-VN", { timeZone: zone, hour: "2-digit", minute: "2-digit" }).format(updated)}`
                : "Dữ liệu lần tải trước · chưa xác nhận cập nhật"}
            <span>{zone}</span>
          </p>
          {error && (
            <p className="nora-day-error" role="alert">
              Chưa cập nhật được lịch. {error} Hãy thử “Cập nhật lịch”.
            </p>
          )}
          {timelineError && <p role="alert">{timelineError}</p>}
          <section
            aria-label={`Lịch đã lưu ngày ${displayDate(date)}`}
            className="nora-day-timeline"
            aria-busy={loading}
          >
            <h3>{displayDate(date)}</h3>
            <p className="nora-day-legend">
              {icon("calendar")} Sự kiện cố định <span>· Phiên công việc</span>
            </p>
            {!entries.length && !timelineError && (
              <p className="nora-day-empty">
                Chưa có mục lịch được tải cho ngày này. Điều này không có nghĩa
                là cả ngày đều rảnh.
              </p>
            )}
            <ol>
              {entries.map((entry) => (
                <li
                  key={`${entry.kind}-${entry.id}`}
                  className={`nora-timeline-item is-${entry.kind}${entry.overlap ? " has-overlap" : ""}`}
                >
                  <time>
                    {entry.startLabel}–{entry.endLabel}
                  </time>
                  {entry.kind === "gap" ? (
                    <div>
                      <strong>Khoảng trống giữa lịch</strong>
                      <small>Chưa đối chiếu giờ rảnh</small>
                    </div>
                  ) : (
                    <div>
                      <strong>{entry.name}</strong>
                      <small>
                        {entry.label}
                        {entry.status === "Completed" ? " · Đã hoàn thành" : ""}
                      </small>
                      {entry.overlap && (
                        <span className="nora-overlap-note">
                          Trùng với một mục lịch khác
                        </span>
                      )}
                      {entry.kind === "event" && onEditEvent && (
                        <button
                          type="button"
                          className="nora-timeline-edit"
                          disabled={busy || loading || !updated}
                          onClick={() => onEditEvent(entry)}
                          aria-label={`Sửa ${entry.name}`}
                        >
                          {icon("edit")}Sửa
                        </button>
                      )}
                    </div>
                  )}
                </li>
              ))}
            </ol>
          </section>
          {updated && !error && (lunch || travel) && (
            <div className="nora-day-suggestions">
              <h3>Bạn có muốn thêm?</h3>
              <p>Chỉ điền gợi ý vào ô nhập. Bạn xem lại rồi gửi.</p>
              {lunch && (
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => draftRange("nghỉ trưa", "12:00", "12:30")}
                  aria-label="Gợi ý nghỉ trưa 30 phút"
                >
                  Nghỉ trưa · 30 phút
                </button>
              )}
              {travel && (
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => {
                    const from = new Date(travel.to - 30 * 60000);
                    draftRange(
                      "thời gian di chuyển",
                      new Intl.DateTimeFormat("en-GB", {
                        timeZone: zone,
                        hour: "2-digit",
                        minute: "2-digit",
                      }).format(from),
                      travel.endLabel,
                    );
                  }}
                >
                  Di chuyển · 30 phút
                </button>
              )}
            </div>
          )}
        </>
      ) : turn ? (
        <section
          className="nora-request-result"
          aria-label="Chi tiết kết quả yêu cầu"
        >
          <h3>Kết quả của yêu cầu</h3>
          <blockquote>{turn.text}</blockquote>
          <p className="nora-result-hint">
            {turn.status === "PREVIEW"
              ? "Bản xem trước · Chưa lưu vào lịch."
              : turn.status === "UNDONE"
                ? "Yêu cầu đã hoàn tác · Chỉ xem lịch sử."
                : turn.status === "APPLIED"
                  ? "Đã lưu khi xử lý yêu cầu này. Đây không phải toàn bộ lịch hiện tại."
                  : "Chưa có thay đổi được lưu."}
          </p>
          <ul>
            {turn.changes?.map((change, index) => (
              <li key={index}>
                <strong>{change.name}</strong>
                <span>{compactScheduleText(change.after)}</span>
              </li>
            ))}
          </ul>
          <ol>
            {turn.plan?.sessions
              ?.filter((item) => !item.preserved)
              .map((item) => (
                <li key={item.id}>
                  <strong>{planSessionName(item, turn)}</strong>
                  <span>
                    {planDay(item.start)} · {planHours(item.start)}–
                    {planHours(item.end)}
                    {planDay(item.start) !== planDay(item.end)
                      ? ` (${planDay(item.end)})`
                      : ""}
                  </span>
                  <small>
                    Khoảng làm việc{" "}
                    {turn.status === "PREVIEW"
                      ? "đề xuất"
                      : "trong kết quả này"}
                  </small>
                </li>
              ))}
          </ol>
          <button
            type="button"
            className="nora-switch-day"
            onClick={() => {
              onDateChange(resultDate(turn));
              onViewChange("day");
            }}
          >
            Đối chiếu với lịch đã lưu {icon("arrow")}
          </button>
        </section>
      ) : null}
      <a
        className="nora-open-calendar"
        href="#calendar"
        onClick={() => {
          state.calendarStart = date;
        }}
      >
        Mở lịch đầy đủ {icon("arrow")}
      </a>
    </section>
  );
}
