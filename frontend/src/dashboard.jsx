import { useState, Fragment } from "react";
import {
  state,
  button,
  icon,
  dateFmt,
  timeFmt,
  minutes,
  riskLabels,
} from "./ui.jsx";
import { dashboardModel } from "./dashboard-model.js";
import { TodayWorkload } from "./workload.jsx";
import { ScheduledFocus } from "./scheduled-focus.jsx";
import { UpcomingFocus } from "./focus-timer.jsx";
import { EisenhowerFocus } from "./eisenhower.jsx";
import "./styles/dashboard-insights.css";
import {
  CalendarClock,
  calendarPhase,
  calendarPhases,
} from "./calendar-time.jsx";

const clock = (value) =>
  `${String(Math.floor(value / 60)).padStart(2, "0")}:${String(value % 60).padStart(2, "0")}`;
function ToolButton({
  label,
  action = "nav",
  id = "",
  symbol = "arrow",
  text,
}) {
  return (
    <button
      type="button"
      className={`console-icon-button${text ? " console-text-button" : ""}`}
      data-action={action}
      data-id={id}
      aria-label={label}
      title={label}
    >
      {icon(symbol)}
      {text && <span>{text}</span>}
    </button>
  );
}
export { CapacityRibbon } from "./capacity-ribbon.jsx";
function DayRunway({ model, now }) {
  const [view, setView] = useState("agenda");
  const preference = state.workspace.preference;
  const first = Math.min(
    preference.preferredStartMinute ?? 480,
    ...model.timeline.map((item) => item.span[0]),
  );
  const last = Math.max(
    preference.preferredEndMinute ?? 1080,
    ...model.timeline.map((item) => item.span[1]),
  );
  const start = Math.floor(first / 60) * 60;
  const end = Math.min(1440, Math.ceil(last / 60) * 60);
  const height = Math.max(360, ((end - start) / 60) * 48);
  const scale = height / Math.max(60, end - start);
  const ticks = Array.from(
    { length: (end - start) / 60 + 1 },
    (_, index) => start + index * 60,
  );
  const lanes = [];
  const timeline = model.timeline.map((item) => {
    let lane = lanes.findIndex((end) => end <= item.span[0]);
    if (lane === -1) lane = lanes.length;
    lanes[lane] = item.span[1];
    return { ...item, lane };
  });
  const laneCount = Math.max(1, lanes.length);
  return (
    <section className="runway-card daily-schedule" aria-label="Lịch hôm nay">
      <div className="runway-toolbar">
        <span>
          {icon("calendar")} {dateFmt(now, { weekday: "long" })}
        </span>
        <span>{model.timeline.length} lịch</span>
        <ToolButton
          label="Mở lịch làm việc"
          id="calendar"
          symbol="calendar"
          text="Mở lịch"
        />
      </div>
      <div
        className="daily-schedule-views"
        role="group"
        aria-label="Cách xem lịch hôm nay"
      >
        <button
          type="button"
          aria-pressed={view === "agenda"}
          onClick={() => setView("agenda")}
        >
          Danh sách lịch
        </button>
        <button
          type="button"
          aria-pressed={view === "timeline"}
          onClick={() => setView("timeline")}
        >
          Dòng thời gian
        </button>
      </div>
      <div
        className="daily-schedule-scroll"
        role="region"
        aria-label={
          view === "agenda"
            ? "Danh sách lịch hôm nay, cuộn để xem thêm"
            : "Dòng thời gian hôm nay, cuộn để xem thêm"
        }
        tabIndex={0}
      >
        {view === "agenda" && (
          <div className="daily-agenda">
            {timeline.map((item) => {
              const rest = model.breaks.find(
                (rest) => rest.span[0] === item.span[1],
              );
              const phase = calendarPhase(item.start, item.end, now);
              const status =
                item.status === "Completed" ? "Đã xong" : calendarPhases[phase];
              return (
                <Fragment key={`${item.kind}-${item.id}`}>
                  <div
                    className={`daily-agenda-row calendar-time-${phase} ${phase === "current" ? "is-active" : ""} ${item.status === "Completed" ? "is-completed" : ""}`}
                  >
                    <div className="daily-agenda-time">
                      <strong>{timeFmt(item.start)}</strong>
                      <span>{timeFmt(item.end)}</span>
                    </div>
                    <button
                      type="button"
                      className="daily-agenda-session"
                      data-action={
                        item.kind === "session" ? "session-detail" : "nav"
                      }
                      data-id={item.kind === "session" ? item.id : "calendar"}
                    >
                      <div className="daily-agenda-session-top">
                        <span
                          className={`daily-agenda-status status-${item.status || "event"}`}
                        >
                          {icon(
                            item.status === "Completed"
                              ? "check"
                              : item.kind === "event"
                                ? "calendar"
                                : "clock",
                          )}
                          {status}
                        </span>
                        <span className="daily-agenda-duration">
                          {minutes(item.span[1] - item.span[0])}
                        </span>
                      </div>
                      <strong className="daily-agenda-title">
                        {item.taskName || item.name}
                      </strong>
                      <div className="daily-agenda-session-bottom">
                        <span>
                          {item.isLocked
                            ? "Đã khóa lịch"
                            : item.kind === "session"
                              ? "Tự động theo lịch · Mở để xem chi tiết"
                              : "Sự kiện cố định"}
                        </span>
                        {icon("arrow")}
                      </div>
                    </button>
                  </div>
                  {rest && (
                    <div className="daily-agenda-rest">
                      {icon("pause")}
                      <span>Nghỉ {rest.span[1] - rest.span[0]} phút</span>
                      <small>
                        {clock(rest.span[0])}–{clock(rest.span[1])}
                      </small>
                    </div>
                  )}
                </Fragment>
              );
            })}
            {!timeline.length && (
              <div className="daily-agenda-empty">
                <span>{icon("calendar")}</span>
                <h3>Hôm nay còn trống</h3>
                <p>Chưa có sự kiện hoặc phiên làm việc hôm nay.</p>
              </div>
            )}
          </div>
        )}
        {view === "timeline" && (
          <>
            <div className="runway-grid" style={{ height }}>
              <div className="runway-hours">
                {ticks.map((minute) => (
                  <span key={minute} style={{ top: (minute - start) * scale }}>
                    {clock(minute)}
                  </span>
                ))}
              </div>
              <div className="runway-lanes">
                {ticks.map((minute) => (
                  <div
                    key={minute}
                    className="runway-line"
                    style={{ top: (minute - start) * scale }}
                  />
                ))}
                {model.breaks.map((rest) => (
                  <div
                    key={rest.span[0]}
                    className="runway-break"
                    style={{
                      top: (rest.span[0] - start) * scale,
                      height: Math.max(
                        2,
                        (rest.span[1] - rest.span[0]) * scale,
                      ),
                    }}
                  >
                    <span title={`Nghỉ ${rest.span[1] - rest.span[0]} phút`}>
                      {rest.span[1] - rest.span[0]}p
                    </span>
                  </div>
                ))}
                {timeline.map((item) => {
                  const task = state.workspace.tasks.find(
                    (task) => task.id === item.taskId,
                  );
                  const cyan =
                    task?.taskType === "PROGRAMMING" ||
                    ["Running", "Paused"].includes(item.status);
                  return (
                    <button
                      key={`${item.kind}-${item.id}`}
                      type="button"
                      className={`runway-block calendar-time-${calendarPhase(item.start, item.end, now)} ${item.span[1] - item.span[0] < 45 ? "is-short" : ""} ${item.kind === "event" ? "runway-block--event" : cyan ? "runway-block--cyan" : "runway-block--blue"} ${item.status === "Completed" ? "is-completed" : ""}`}
                      data-action={
                        item.kind === "session" ? "session-detail" : "nav"
                      }
                      data-id={item.kind === "session" ? item.id : "calendar"}
                      aria-label={`${item.taskName || item.name}, ${timeFmt(item.start)} đến ${timeFmt(item.end)}, ${minutes(item.span[1] - item.span[0])}`}
                      style={{
                        top: (item.span[0] - start) * scale + 2,
                        height: Math.max(
                          3,
                          (item.span[1] - item.span[0]) * scale - 4,
                        ),
                        left: `${(item.lane / laneCount) * 100}%`,
                        width: `${100 / laneCount}%`,
                      }}
                      title={`${item.taskName || item.name} · ${timeFmt(item.start)} — ${timeFmt(item.end)}`}
                    >
                      <span className="runway-block-title">
                        {item.status === "Completed" && icon("check")}
                        {item.taskName || item.name}
                      </span>
                      {item.span[1] - item.span[0] >= 45 && (
                        <small>
                          {timeFmt(item.start)} — {timeFmt(item.end)} ·{" "}
                          {minutes(item.span[1] - item.span[0])}
                          {item.isLocked ? " · Đã khóa" : ""}
                        </small>
                      )}
                    </button>
                  );
                })}
                {!timeline.length && (
                  <div className="runway-empty">
                    <span>{icon("calendar")}</span>
                    <h3>Hôm nay còn trống</h3>
                    <p>Biến công việc thành những khoảng thời gian cụ thể.</p>
                    {button(
                      "Lập kế hoạch",
                      "generate-plan",
                      "small secondary",
                      "",
                      "spark",
                    )}
                  </div>
                )}
              </div>
            </div>
            <div className="runway-legend">
              <span>
                <i className="legend-cyan" />
                Tập trung
              </span>
              <span>
                <i className="legend-blue" />
                Phiên làm việc
              </span>
              <span>
                <i className="legend-rest" />
                Đệm nghỉ
              </span>
            </div>
          </>
        )}
      </div>
    </section>
  );
}
function EstimationGauge({ model }) {
  const calendarMode = model.accuracy === null;
  const stats = model.calendarStats;
  const percent = calendarMode ? stats.percent : model.accuracy;
  return (
    <section
      className="intelligence-card estimation-card"
      aria-label={calendarMode ? "Tổng quan lịch hôm nay" : "Độ khớp ước lượng"}
    >
      <div className="console-card-heading">
        <h3>{calendarMode ? "Lịch hôm nay" : "Độ khớp ước lượng"}</h3>
        <ToolButton
          label={calendarMode ? "Xem lịch" : "Xem phân tích ước lượng"}
          id={calendarMode ? "calendar" : "insights"}
          symbol={calendarMode ? "calendar" : "insights"}
        />
      </div>
      <div className="estimation-gauge">
        <svg viewBox="0 0 220 115" aria-hidden="true">
          <path className="gauge-track" d="M25 95 A85 85 0 0 1 195 95" />
          <path
            className="gauge-value"
            d="M25 95 A85 85 0 0 1 195 95"
            pathLength="100"
            strokeDasharray={`${percent} 100`}
          />
        </svg>
        <div>
          <strong>
            {calendarMode && !stats.total ? "0 lịch" : `${percent}%`}
          </strong>
          <span>
            {calendarMode
              ? stats.total
                ? "Thời lượng lịch đã trôi"
                : "Hôm nay chưa có lịch"
              : "Dự kiến so với thực tế"}
          </span>
        </div>
      </div>
      <div className="gauge-caption">
        {calendarMode ? (
          <>
            <span>
              {stats.total} lịch · {stats.past} đã qua · {stats.current} đang
              diễn ra
            </span>
            <span>{minutes(stats.remainingMinutes)} còn lại</span>
          </>
        ) : (
          <>
            <span>
              {model.ratio
                ? `${model.ratio.ratio.toFixed(2)}× hệ số`
                : "Chưa đủ dữ liệu"}
            </span>
            <span>{model.ratio?.samples || 0} mẫu</span>
          </>
        )}
      </div>
    </section>
  );
}
function CapacityHorizon({ model }) {
  const additional = (day) =>
    model.hasAvailability && day.capacity !== null
      ? (day.schedulable ?? Math.max(0, day.capacity - day.reserved))
      : null;
  const knownCapacity = model.days.some((day) => additional(day) !== null);
  const maximum = Math.max(
    60,
    ...model.days.flatMap((day) => [additional(day) || 0, day.calendarBusy]),
  );
  return (
    <section
      className="intelligence-card horizon-card"
      aria-label="Khả năng trong 7 ngày"
    >
      <div className="console-card-heading">
        <h3>Khả năng trong 7 ngày</h3>
        <ToolButton label="Thiết lập thời gian rảnh" id="time" symbol="clock" />
      </div>
      <p>Tổng thời lượng lịch học, lịch hẹn và phiên công việc</p>
      <div
        className={`horizon-chart ${knownCapacity ? "" : "is-calendar-only"}`}
        role="img"
        aria-label="Thời lượng đã có lịch và giờ có thể xếp thêm trong bảy ngày tới"
      >
        {model.days.map((day, index) => (
          <div
            className="horizon-day"
            key={day.key}
            title={`${dateFmt(`${day.key}T12:00:00Z`)}: ${minutes(day.calendarBusy)} đã có lịch; ${additional(day) === null ? "chưa xác định giờ có thể xếp thêm" : `${minutes(additional(day))} có thể xếp thêm`}`}
          >
            <strong className="horizon-total">
              {minutes(day.calendarBusy)}
            </strong>
            <div className="horizon-bar-space">
              {additional(day) !== null && (
                <div
                  className="horizon-capacity"
                  style={{ height: `${(additional(day) / maximum) * 100}%` }}
                />
              )}
              <div
                className={`horizon-planned ${index % 2 ? "is-blue" : ""}`}
                style={{ height: `${(day.calendarBusy / maximum) * 100}%` }}
              />
            </div>
            <span>
              {index === 0
                ? "H.nay"
                : new Intl.DateTimeFormat("vi-VN", {
                    weekday: "short",
                    timeZone: "UTC",
                  }).format(new Date(`${day.key}T12:00:00Z`))}
            </span>
          </div>
        ))}
      </div>
      <div className="horizon-legend">
        <span>
          <i className="legend-cyan" />
          Đã có lịch
        </span>
        {knownCapacity && (
          <span>
            <i className="legend-rest" />
            Có thể xếp thêm
          </span>
        )}
      </div>
      {!knownCapacity && (
        <div className="horizon-missing">
          <span>
            {model.hasAvailability
              ? "Chưa có dữ liệu khả năng thực hiện."
              : "Chưa đặt giờ rảnh để tính khả năng xếp thêm."}
          </span>
          {!model.hasAvailability &&
            button("Đặt giờ rảnh", "nav", "small ghost", "time", "clock")}
        </div>
      )}
    </section>
  );
}
function Deadlines({ model }) {
  const deadlines = model.due.filter((task) => task.isHardDeadline).slice(0, 4);
  const calendarMode = !deadlines.length;
  const entries = calendarMode
    ? model.calendarMilestones.slice(0, 4)
    : deadlines;
  return (
    <section
      className="intelligence-card deadlines-card"
      aria-label={calendarMode ? "Lịch sắp tới" : "Deadline cần giữ"}
    >
      <div className="console-card-heading">
        <h3>{calendarMode ? "Lịch sắp tới" : "Deadline cần giữ"}</h3>
        <ToolButton
          label={calendarMode ? "Mở lịch sắp tới" : "Xem tất cả công việc"}
          id={calendarMode ? "calendar" : "tasks"}
          symbol={calendarMode ? "calendar" : "tasks"}
        />
      </div>
      <p>
        {calendarMode
          ? "Những giờ bắt đầu và kết thúc cần giữ"
          : "Những mốc không thể bỏ lỡ"}
      </p>
      {entries.length ? (
        <div className="deadline-list">
          {entries.map((item) => {
            const at = calendarMode ? item.at : item.deadline;
            return (
              <button
                type="button"
                key={`${item.kind || "task"}-${item.id}`}
                data-action={
                  calendarMode
                    ? item.kind === "event"
                      ? "edit-event"
                      : "session-detail"
                    : "task-detail"
                }
                data-id={item.id}
                className={`deadline-line ${!calendarMode && Date.parse(at) < model.now ? "is-overdue" : ""}`}
              >
                <span className="deadline-day">
                  <strong>{dateFmt(at, { month: undefined })}</strong>
                  <small>
                    {dateFmt(at, { day: undefined, month: "short" })}
                  </small>
                </span>
                <span className="deadline-info">
                  <strong>{item.name}</strong>
                  <small>
                    {calendarMode
                      ? `${item.ongoing ? "Đang diễn ra · Kết thúc" : item.kind === "event" ? "Lịch hẹn" : "Phiên làm việc"} ${timeFmt(at)}`
                      : `${timeFmt(at)} · ${minutes(item.remainingMinutes)} còn lại`}
                  </small>
                </span>
                {icon("arrow")}
              </button>
            );
          })}
        </div>
      ) : (
        <div className="console-empty">Chưa có deadline hoặc lịch sắp tới.</div>
      )}
    </section>
  );
}
function LiveIntelligence() {
  const workspace = state.workspace;
  return (
    <CalendarClock
      boundaries={[...workspace.sessions, ...workspace.events].flatMap(
        (item) => [Date.parse(item.start), Date.parse(item.end)],
      )}
    >
      {(now) => {
        const model = dashboardModel(workspace, state.insights, now);
        const insights = state.insights;
        const calendarOnly = model.due.length === 0;
        return (
          <>
            <EstimationGauge model={model} />
            <CapacityHorizon model={model} />
            <Deadlines model={model} />
            <div className="console-workload">
              <span
                className={`workload-dot ${!calendarOnly && (insights?.riskLevel === "Overload" || insights?.riskLevel === "High") ? "is-high" : ""}`}
              />
              <span>
                {calendarOnly
                  ? `${model.calendarStats.horizonCount} lịch trong 7 ngày`
                  : insights
                    ? `Khối lượng việc ${riskLabels[insights.riskLevel] || insights.riskLevel}`
                    : "Chưa xác định"}
              </span>
              <strong>
                {minutes(
                  calendarOnly
                    ? model.calendarStats.horizonRemaining
                    : insights?.remainingMinutes,
                )}{" "}
                còn lại
              </strong>
            </div>
          </>
        );
      }}
    </CalendarClock>
  );
}
export function todayPage({ onWorkspaceChanged } = {}) {
  const model = dashboardModel(state.workspace, state.insights);
  const insights = state.insights;
  return (
    <>
      <TodayWorkload onWorkspaceChanged={onWorkspaceChanged} />
      <details className="today-overview" open>
        <summary>Công việc, lịch và phân tích</summary>
        <EisenhowerFocus />
        <div className="planning-dashboard">
        <section
          className="console-column backlog-column"
          aria-label="Danh sách công việc"
        >
          <div className="console-column-heading">
            <div>
              <h2>Công việc & ý tưởng</h2>
            </div>
            <ToolButton
              label="Thêm công việc"
              action="new-task"
              symbol="plus"
              text="Thêm việc"
            />
          </div>
          <div className="backlog-panel">
            <div className="backlog-toolbar">
              <span>
                <strong>{model.queue.length}</strong> công việc đang mở
              </span>
              <ToolButton
                label="Mở danh sách công việc"
                id="tasks"
                symbol="tasks"
                text="Xem tất cả"
              />
            </div>
            <div className="backlog-queue">
              {model.queue.length ? (
                model.queue.slice(0, 12).map((task) => (
                  <button
                    type="button"
                    className="queue-card"
                    key={task.id}
                    data-action="task-detail"
                    data-id={task.id}
                  >
                    <div className="queue-card-top">
                      <span
                        className={`queue-status ${task.status === "InProgress" ? "is-in-progress" : ""}`}
                      />
                      <strong>{task.name}</strong>
                      {icon("arrow")}
                    </div>
                    <div className="queue-chips">
                      <span>{minutes(task.remainingMinutes)}</span>
                      <span
                        className={`queue-priority priority-${task.priority}`}
                      >
                        {["", "Thấp", "Vừa", "Cao", "Khẩn"][task.priority]}
                      </span>
                      {task.dependencies?.length > 0 && <span>Phụ thuộc</span>}
                    </div>
                    <div className="queue-card-bottom">
                      <span>
                        {state.workspace.categories.find(
                          (category) => category.id === task.categoryId,
                        )?.name || "Chưa phân loại"}
                      </span>
                      <span>{dateFmt(task.deadline)}</span>
                    </div>
                    <div
                      className="queue-progress"
                      aria-label={`${task.progress}% hoàn thành`}
                    >
                      <span
                        style={{
                          width: `${Math.max(0, Math.min(100, task.progress))}%`,
                        }}
                      />
                    </div>
                  </button>
                ))
              ) : (
                <div className="console-empty">
                  <h3>Bắt đầu từ việc bạn cần làm</h3>
                  <p>
                    Viết vài dòng để AI giúp nhận diện công việc, hoặc tự thêm
                    từng việc.
                  </p>
                  {button(
                    "Nhập việc cần làm",
                    "nav",
                    "small primary",
                    "notes",
                    "edit",
                  )}
                  {button(
                    "Thêm việc thủ công",
                    "new-task",
                    "small secondary",
                    "",
                    "plus",
                  )}
                </div>
              )}
            </div>
            <button
              type="button"
              className="queue-capture"
              data-action="capture"
            >
              {icon("spark")}Ghi nhanh một ý tưởng<span>+</span>
            </button>
          </div>
          <button
            type="button"
            className="console-note-link"
            data-action="nav"
            data-id="notes"
          >
            {icon("edit")}
            <span>
              <strong>Nhập việc</strong>
              <small>Nhập việc cần làm, để AI giúp tạo lịch</small>
            </span>
            {icon("arrow")}
          </button>
        </section>
        <section
          className="console-column runway-column"
          aria-label="Lịch và phiên tập trung"
        >
          <div className="console-column-heading">
            <div>
              <h2>Lịch hôm nay & tập trung</h2>
            </div>
            <ToolButton label="Mở lịch làm việc" id="calendar" symbol="calendar" text="Mở lịch" />
          </div>
          <ScheduledFocus
            workspace={state.workspace}
            onWorkspaceChanged={onWorkspaceChanged}
          />
          <UpcomingFocus workspace={state.workspace} />
          <CalendarClock
            boundaries={[
              ...state.workspace.sessions,
              ...state.workspace.events,
            ].flatMap((item) => [Date.parse(item.start), Date.parse(item.end)])}
          >
            {(now) => (
              <DayRunway
                model={dashboardModel(state.workspace, state.insights, now)}
                now={now}
              />
            )}
          </CalendarClock>
        </section>
        <aside
          className="console-column intelligence-column"
          aria-label="Phân tích khối lượng công việc"
        >
          <div className="console-column-heading">
            <div>
              <h2>Phân tích & khả năng</h2>
            </div>
          </div>
          <LiveIntelligence />
        </aside>
        </div>
      </details>
    </>
  );
}
