import { nodeList } from "./react-ui.jsx";
import {
  state,
  button,
  icon,
  statusLabels,
  dateFmt,
  timeFmt,
  dayKey,
  minutes,
} from "./ui.jsx";
import {
  CalendarClock,
  CalendarTimeTag,
  calendarPhase,
  calendarPhases,
} from "./calendar-time.jsx";
import { workflowSteps } from "./workflow.js";
import { CalendarWeek } from "./calendar-week.jsx";
export function weekStart(key) {
  const date = new Date(`${key}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() - ((date.getUTCDay() + 6) % 7));
  return date.toISOString().slice(0, 10);
}
export function calendarPage({ onWorkspaceChanged } = {}) {
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
  const sessions = state.workspace.sessions.filter(
    (s) =>
      dayKey(s.start) <= dates[6] &&
      dayKey(new Date(Date.parse(s.end) - 1)) >= dates[0],
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
  const fixed = (e, date, compact = false, now) => {
    const phase = calendarPhase(e.start, e.end, now);
    const from = dayKey(e.start) < date ? "00:00" : timeFmt(e.start),
      to = dayKey(e.end) > date ? "24:00" : timeFmt(e.end);
    return (
      <button
        key={`event-${e.id}-${date}`}
        type="button"
        className={`calendar-block fixed-event calendar-timed-card calendar-time-${phase}${compact ? "" : " calendar-agenda-entry"}`}
        data-action="event-detail"
        data-id={e.id}
      >
        <span className="block-time">
          {from}
          {" — "}
          {to}
        </span>
        <strong title={e.name}>{e.name}</strong>
        <CalendarTimeTag phase={phase} />
        <span className="block-meta">
          {icon("lock")}
          Sự kiện cố định
        </span>
      </button>
    );
  };
  const block = (s, now, agenda = false) => (
    <button
      key={`session-${s.id}`}
      type="button"
      className={`calendar-block calendar-timed-card calendar-time-${calendarPhase(s.start, s.end, now)} ${s.status === "Completed" ? "completed" : ["Running", "Paused"].includes(s.status) ? "active-session" : ["Skipped", "Missed"].includes(s.status) ? "inactive" : ""}${agenda ? " calendar-agenda-entry" : ""}`}
      data-action="session-detail"
      data-id={s.id}
    >
      <span className="block-time">
        {timeFmt(s.start)}
        {" — "}
        {timeFmt(s.end)}
        {s.isLocked ? icon("lock") : ""}
      </span>
      <strong title={s.taskName}>{s.taskName}</strong>
      <CalendarTimeTag phase={calendarPhase(s.start, s.end, now)} />
      <span className="block-meta">
        {minutes(Math.round((new Date(s.end) - new Date(s.start)) / 60000))}
        {" · "}
        {statusLabels[s.status] || s.status}
      </span>
    </button>
  );
  const tasks = (state.workspace.tasks || [])
    .filter((task) => !["Completed", "Cancelled"].includes(task.status))
    .sort((a, b) => new Date(a.deadline) - new Date(b.deadline));
  return (
    <div className="calendar-planning-layout">
      <aside
        className="card calendar-task-context"
        aria-label="Công việc cạnh lịch"
      >
        <details open={!globalThis.matchMedia?.("(max-width: 800px)").matches}>
          <summary>
            <span>Công việc cần làm</span>
            <span>{tasks.length}</span>
          </summary>
          <p className="reason">
            Xem việc cùng lịch để biết thời gian đang dành cho từng việc.
          </p>
          <div className="calendar-task-queue">
            {tasks.slice(0, 8).map((task) => (
              <button
                type="button"
                key={task.id}
                data-action="task-detail"
                data-id={task.id}
              >
                <strong>{task.name}</strong>
                <span>
                  {minutes(task.remainingMinutes)} còn lại · Hạn{" "}
                  {dateFmt(task.deadline)}
                </span>
              </button>
            ))}
          </div>
          {!tasks.length && (
            <p className="reason">
              Chưa có công việc. Viết một ghi chú để bắt đầu.
            </p>
          )}
          <div className="form-actions">
            {button("Nhập việc", "nav", "small primary", "notes", "edit")}
            {tasks.length > 0 &&
              button("Xem tất cả", "nav", "small ghost", "tasks")}
          </div>
        </details>
      </aside>
      <div className="calendar-schedule-main">
        <CalendarClock
          boundaries={[...sessions, ...state.workspace.events].flatMap(
            (entry) => [Date.parse(entry.start), Date.parse(entry.end)],
          )}
        >
          {(now) =>
            calendarMarkup(
              dates,
              view,
              sessions,
              entries,
              block,
              fixed,
              now,
              onWorkspaceChanged,
            )
          }
        </CalendarClock>
      </div>
    </div>
  );
}
function calendarMarkup(
  dates,
  view,
  sessions,
  entries,
  block,
  fixed,
  now,
  onWorkspaceChanged,
) {
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
            {workflowSteps(state.workspace)
              .slice(0, 3)
              .every((step) => step.done) &&
              button(
                "Lập kế hoạch",
                "generate-plan",
                "small primary",
                "",
                "spark",
              )}
          </div>
        </div>
        <div className="calendar-context">
          <div
            className="calendar-legend calendar-time-legend"
            aria-label="Màu lịch theo thời gian"
          >
            {Object.entries(calendarPhases).map(([phase, label]) => (
              <span key={phase} className={`calendar-time-${phase}`}>
                <i aria-hidden="true" />
                {label}
              </span>
            ))}
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
          <CalendarWeek
            dates={dates}
            entries={entries}
            block={block}
            fixed={fixed}
            now={now}
            onWorkspaceChanged={onWorkspaceChanged}
          />
        ) : (
          <div
            className="agenda-week"
            role="region"
            aria-label="Danh sách lịch 7 ngày, cuộn để xem thêm"
            tabIndex="0"
          >
            {nodeList(
              dates.map((date) => (
                <section key={date} className="agenda-day">
                  <div
                    className={`agenda-day-label ${date === dayKey(now) ? "today" : ""}`}
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
                          ? block(e.session, now, true)
                          : fixed(e.event, date, false, now),
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
        <p className="calendar-hint">
          {icon("clock")}
          Cuộn trong khung lịch để xem thêm. Bấm một mục để xem chi tiết và các
          thao tác với lịch.
          {" Múi giờ "}
          {state.workspace.preference.timeZoneId}.
        </p>
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
