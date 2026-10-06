import { nodeList } from "./react-ui.jsx";
import {
  state,
  button,
  badge,
  icon,
  statusLabels,
  dateFmt,
  timeFmt,
  dayKey,
  minutes,
} from "./ui.jsx";
import { sessionCard } from "./calendar.jsx";
export function weekStart(key) {
  const date = new Date(`${key}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() - ((date.getUTCDay() + 6) % 7));
  return date.toISOString().slice(0, 10);
}
export function calendarPage() {
  const base = weekStart(state.calendarStart || dayKey(Date.now()));
  state.calendarStart = base;
  const dates = Array.from(
    {
      length: 7,
    },
    (_, index) => {
      const date = new Date(`${base}T12:00:00Z`);
      date.setUTCDate(date.getUTCDate() + index);
      return date.toISOString().slice(0, 10);
    },
  );
  const view =
    state.calendarView ||
    (globalThis.matchMedia?.("(max-width: 800px)").matches ? "agenda" : "week");
  const sessions = state.workspace.sessions.filter((s) =>
    dates.includes(dayKey(s.start)),
  );
  const entries = (date) =>
    [
      ...sessions
        .filter((s) => dayKey(s.start) === date)
        .map((s) => ({
          start: s.start,
          end: s.end,
          session: s,
        })),
      ...state.workspace.events
        .filter(
          (e) =>
            dayKey(e.start) <= date &&
            dayKey(new Date(new Date(e.end).getTime() - 1)) >= date,
        )
        .map((e) => ({
          start: e.start,
          end: e.end,
          event: e,
        })),
    ].sort((a, b) => new Date(a.start) - new Date(b.start));
  const fixed = (e, date, compact = false) => {
    const from = dayKey(e.start) < date ? "00:00" : timeFmt(e.start),
      to = dayKey(e.end) > date ? "24:00" : timeFmt(e.end);
    return compact ? (
      <button
        type="button"
        className="calendar-block fixed"
        data-action="edit-event"
        data-id={e.id}
      >
        <span className="block-time">
          {from}
          {" — "}
          {to}
        </span>
        <strong>{e.name}</strong>
        <span className="block-meta">
          {icon("lock")}
          Sự kiện cố định
        </span>
      </button>
    ) : (
      <div className="session-card fixed">
        <div className="session-top">
          <h3>{e.name}</h3>
          {badge("Cố định")}
        </div>
        <p className="session-time">
          {from}
          {" — "}
          {to}
        </p>
        {button("Chi tiết sự kiện", "edit-event", "small ghost", e.id, "edit")}
      </div>
    );
  };
  const block = (s) => (
    <button
      type="button"
      className={`calendar-block ${s.status === "Completed" ? "completed" : ["Running", "Paused"].includes(s.status) ? "active-session" : ["Skipped", "Missed"].includes(s.status) ? "inactive" : ""}`}
      data-action="session-detail"
      data-id={s.id}
    >
      <span className="block-time">
        {timeFmt(s.start)}
        {" — "}
        {timeFmt(s.end)}
        {s.isLocked ? icon("lock") : ""}
      </span>
      <strong>{s.taskName}</strong>
      <span className="block-meta">
        {minutes(Math.round((new Date(s.end) - new Date(s.start)) / 60000))}
        {" · "}
        {statusLabels[s.status] || s.status}
      </span>
    </button>
  );
  return calendarMarkup(dates, view, sessions, entries, block, fixed);
}
function calendarMarkup(dates, view, sessions, entries, block, fixed) {
  return (
    <>
      {state.conflicts?.length ? (
        <div className="warning">
          <strong>
            {state.conflicts.length}
            {" xung đột cần điều chỉnh"}
          </strong>
          {nodeList(
            state.conflicts.map((c) => (
              <p>
                {c.taskName}
                {": "}
                {c.message}
              </p>
            )),
          )}
          {button(
            "Lập lại phần lịch liên quan",
            "replan-conflicts",
            "secondary",
            "",
            "refresh",
          )}
        </div>
      ) : (
        ""
      )}
      <section className="card calendar-workspace">
        <div className="calendar-toolbar">
          <div className="calendar-period">
            <div className="actions">
              {button("", "week-prev", "icon-only ghost", "", "back")}
              {button("", "week-next", "icon-only ghost", "", "arrow")}
            </div>
            <h2>
              {dateFmt(`${dates[0]}T12:00:00Z`, {
                timeZone: "UTC",
              })}
              {" — "}
              {dateFmt(`${dates[6]}T12:00:00Z`, {
                year: "numeric",
                timeZone: "UTC",
              })}
            </h2>
            {button("Hôm nay", "week-today", "small secondary")}
          </div>
          <div className="actions">
            <div
              className="view-switch"
              role="group"
              aria-label="Chế độ xem lịch"
            >
              {nodeList(
                ["week", "agenda"].map((v) => (
                  <button
                    type="button"
                    data-action="calendar-view"
                    data-id={v}
                    className={v === view ? "active" : ""}
                    aria-pressed={v === view}
                  >
                    {v === "week" ? "Tuần" : "Danh sách"}
                  </button>
                )),
              )}
            </div>
            {button("Sự kiện", "new-event", "small secondary", "", "plus")}
          </div>
        </div>
        <div className="calendar-context">
          <div className="calendar-legend">
            <span>
              <i className="legend-scheduled" />
              Phiên làm việc
            </span>
            <span>
              <i className="legend-active" />
              Đang tập trung
            </span>
            <span>
              <i className="legend-fixed" />
              Sự kiện cố định
            </span>
          </div>
          <span>
            {sessions.length}
            {" phiên · "}
            {minutes(
              sessions
                .filter((s) => !["Skipped", "Missed"].includes(s.status))
                .reduce(
                  (sum, s) =>
                    sum +
                    Math.round((new Date(s.end) - new Date(s.start)) / 60000),
                  0,
                ),
            )}
          </span>
        </div>
        {view === "week" ? (
          <>
            <div
              className="calendar-scroll"
              role="region"
              aria-label="Lịch 7 ngày, cuộn ngang để xem thêm"
              tabIndex="0"
            >
              <div className="week-grid">
                {nodeList(
                  dates.map((date) => (
                    <section
                      className={`week-day ${date === dayKey(Date.now()) ? "today" : ""}`}
                    >
                      <div className="week-day-header">
                        <span>
                          {new Intl.DateTimeFormat("vi-VN", {
                            weekday: "short",
                            timeZone: "UTC",
                          }).format(new Date(`${date}T12:00:00Z`))}
                        </span>
                        <strong>{date.slice(8)}</strong>
                        <small>
                          {entries(date).length}
                          {" mục"}
                        </small>
                      </div>
                      <div className="day-blocks">
                        {nodeList(
                          entries(date).map((e) =>
                            e.session
                              ? block(e.session)
                              : fixed(e.event, date, true),
                          ),
                        ) || (
                          <div className="calendar-empty">
                            —<span>Chưa có lịch</span>
                          </div>
                        )}
                      </div>
                    </section>
                  )),
                )}
              </div>
            </div>
            <p className="calendar-hint">
              {icon("clock")}
              {"Lịch theo múi giờ "}
              {state.workspace.preference.timeZoneId}. Bấm một phiên để xem và
              điều chỉnh.
            </p>
          </>
        ) : (
          <div className="agenda-week">
            {nodeList(
              dates.map((date) => (
                <section className="agenda-day">
                  <div
                    className={`agenda-day-label ${date === dayKey(Date.now()) ? "today" : ""}`}
                  >
                    <strong>{date.slice(8)}</strong>
                    <span>
                      {new Intl.DateTimeFormat("vi-VN", {
                        weekday: "long",
                        timeZone: "UTC",
                      }).format(new Date(`${date}T12:00:00Z`))}
                    </span>
                  </div>
                  <div className="agenda-day-entries">
                    {nodeList(
                      entries(date).map((e) =>
                        e.session
                          ? sessionCard(e.session)
                          : fixed(e.event, date),
                      ),
                    ) || (
                      <p className="reason agenda-free">
                        Chưa có phiên làm việc hoặc sự kiện.
                      </p>
                    )}
                  </div>
                </section>
              )),
            )}
          </div>
        )}
      </section>
      <div className="calendar-bottom">
        {icon("shield")}
        <span>Phiên đã khóa được bảo vệ khi lập lại kế hoạch.</span>
        {button(
          "Thiết lập thời gian rảnh",
          "nav",
          "small ghost",
          "time",
          "arrow",
        )}
      </div>
    </>
  );
}
