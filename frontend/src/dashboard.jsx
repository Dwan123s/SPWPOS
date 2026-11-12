import { useState, Fragment } from "react";
import {
  state,
  button,
  icon,
  dateFmt,
  dayKey,
  timeFmt,
  minutes,
  riskLabels,
} from "./ui.jsx";
import { dashboardModel, nextDay } from "./dashboard-model.js";
import { TodayWorkload } from "./workload.jsx";
import { CapacityRibbon } from "./capacity-ribbon.jsx";
import { useWorkloadRequest } from "./workload-data.js";
import { openTeamTask } from "./team-task-link.js";
import { ScheduledFocus } from "./scheduled-focus.jsx";
import { UpcomingFocus } from "./focus-timer.jsx";
import { EisenhowerFocus } from "./eisenhower.jsx";
import { PopupDetails } from "./popup-dismissal.jsx";
import availabilityArt from "./assets/home-setup/availability.webp";
import calendarArt from "./assets/home-setup/google-calendar.webp";
import tasksArt from "./assets/home-setup/tasks.webp";
import planArt from "./assets/home-setup/plan.webp";
import teamsArt from "./assets/home-setup/teams.webp";
import "./styles/dashboard-insights.css";
import "./styles/today-home.css";
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
function DaySummary({ summary, hasAvailability, now, progress }) {
  const { work, elapsed, capacity, completed, remaining } = summary;
  const planned = completed + remaining;
  const difference = capacity - remaining;
  return (
    <PopupDetails className="home-day-summary">
      <summary
        className="home-day-date"
        aria-label="Tóm tắt lịch hôm nay"
        title="Tóm tắt lịch hôm nay"
      >
        <span>
          <strong>Hôm nay</strong>, {dateFmt(now)}
        </span>
        <progress
          className="home-day-progress"
          aria-label="Tỷ lệ thời gian đã trôi qua trong lịch hôm nay"
          max={100}
          value={progress}
        />
      </summary>
      <div className="home-day-summary-panel">
        <section aria-label="Giờ có thể làm">
          <header>
            <strong>Giờ có thể làm</strong>
            <strong>{minutes(work)}</strong>
          </header>
          <progress
            aria-label="Giờ làm đã qua"
            value={elapsed}
            max={work || 1}
          />
          <div className="home-day-summary-legend">
            <span>{minutes(elapsed)} đã qua</span>
            <span>{minutes(work - elapsed)} còn lại</span>
          </div>
        </section>
        <section aria-label="Phiên đã xếp">
          <header>
            <strong>Phiên đã xếp</strong>
            <strong>{minutes(planned)}</strong>
          </header>
          <progress
            aria-label="Thời lượng phiên đã hoàn thành"
            value={completed}
            max={planned || 1}
          />
          <div className="home-day-summary-legend">
            <span>{minutes(completed)} đã hoàn thành</span>
            <span>{minutes(remaining)} còn lại</span>
          </div>
        </section>
        <div className="home-day-summary-note">
          {hasAvailability ? (
            <>
              <p>Còn {minutes(capacity)} để làm việc sau khi trừ sự kiện.</p>
              <strong data-shortfall={difference < 0 || undefined}>
                {difference < 0
                  ? `Thiếu ${minutes(-difference)} cho các phiên chưa xong.`
                  : `Còn trống ${minutes(difference)} sau khi tính các phiên chưa xong.`}
              </strong>
            </>
          ) : (
            button("Thiết lập giờ làm", "nav", "secondary small", "time")
          )}
        </div>
        {button("Mở lịch làm việc", "nav", "ghost small", "calendar")}
      </div>
    </PopupDetails>
  );
}
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
  const planned = timeline.filter((item) => item.status !== "Completed");
  const completed = timeline.filter((item) => item.status === "Completed");
  const deadlines = new Map(
    state.workspace.tasks.map((task) => [task.id, task.deadline]),
  );
  return (
    <section className="runway-card daily-schedule" aria-label="Lịch hôm nay">
      <header className="home-agenda-heading">
        <ToolButton label="Thêm Lịch Trình" action="new-event" symbol="plus" />
        <DaySummary
          summary={model.daySummary}
          hasAvailability={model.hasAvailability}
          now={now}
          progress={model.calendarStats.percent}
        />
        <button
          className="console-icon-button home-agenda-expand"
          type="button"
          aria-label="Dòng thời gian"
          title={
            view === "timeline" ? "Về danh sách lịch" : "Mở dòng thời gian"
          }
          aria-pressed={view === "timeline"}
          onClick={() => setView(view === "timeline" ? "agenda" : "timeline")}
        >
          {icon(view === "timeline" ? "collapse" : "expand")}
        </button>
      </header>
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
            {[...planned, ...completed].map((item, index) => {
              if (item.status === "Completed") {
                const deadline = deadlines.get(item.taskId);
                return (
                  <Fragment key={`${item.kind}-${item.id}`}>
                    {index === planned.length && (
                      <h3 className="home-agenda-divider">Đã hoàn thành</h3>
                    )}
                    <button
                      type="button"
                      className="home-completed-session"
                      data-action="session-detail"
                      data-id={item.id}
                    >
                      <span className="home-completed-check">
                        {icon("check")}
                      </span>
                      <span className="home-completed-info">
                        <span>{item.taskName || item.name}</span>
                        {Number.isFinite(Date.parse(deadline)) && (
                          <time
                            className="home-completed-due"
                            dateTime={deadline}
                          >
                            Hạn {dateFmt(deadline)}
                          </time>
                        )}
                      </span>
                    </button>
                  </Fragment>
                );
              }
              const rest = model.breaks.find(
                (rest) => rest.span[0] === item.span[1],
              );
              const phase = calendarPhase(item.start, item.end, now);
              return (
                <Fragment key={`${item.kind}-${item.id}`}>
                  <div
                    className={`daily-agenda-row calendar-entry-${item.kind === "event" ? "event" : "task"} calendar-time-${phase} ${phase === "current" ? "is-active" : ""}`}
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
                          {icon(item.kind === "event" ? "calendar" : "clock")}
                          {calendarPhases[phase]}
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
                              ? "Phiên đã xếp"
                              : "Lịch Trình"}
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
                      className={`runway-block calendar-entry-${item.kind === "event" ? "event" : "task"} calendar-time-${calendarPhase(item.start, item.end, now)} ${item.span[1] - item.span[0] < 45 ? "is-short" : ""} ${item.kind === "event" ? "runway-block--event" : cyan ? "runway-block--cyan" : "runway-block--blue"} ${item.status === "Completed" ? "is-completed" : ""}`}
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
                Phiên đã xếp
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
            button("Đặt giờ làm việc", "nav", "small ghost", "time", "clock")}
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
                      ? `${item.ongoing ? "Đang diễn ra · Kết thúc" : item.kind === "event" ? "Lịch Trình" : "Phiên đã xếp"} ${timeFmt(at)}`
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
const setupSteps = [
  {
    title: "Giờ làm việc",
    artwork: availabilityArt,
    label: "Thiết lập",
    action: "nav",
    id: "time",
  },
  {
    title: "Lịch Google",
    artwork: calendarArt,
    label: "Kết nối",
    action: "calendar-connections",
  },
  {
    title: "Công việc",
    artwork: tasksArt,
    label: "Thêm việc",
    action: "new-task",
  },
  {
    title: "Kế hoạch",
    artwork: planArt,
    label: "Lập lịch",
    action: "generate-plan",
  },
  {
    title: "Nhóm",
    artwork: teamsArt,
    label: "Mở nhóm",
    action: "nav",
    id: "teams",
  },
];

function HomeSetup({ workspace, hasAvailability, onClose }) {
  const readySteps = {
    time: hasAvailability,
    "new-task": workspace.tasks.length > 0,
    "generate-plan": workspace.sessions.some(
      (session) =>
        session.status === "Scheduled" || session.status === "Completed",
    ),
  };
  return (
    <section className="card home-panel" aria-labelledby="home-setup-heading">
      <header className="home-panel-heading">
        <h2 id="home-setup-heading">Bắt đầu</h2>
        <button
          className="btn ghost icon-only"
          type="button"
          onClick={onClose}
          aria-label="Ẩn hướng dẫn"
          title="Ẩn hướng dẫn"
        >
          {icon("close")}
        </button>
      </header>
      <ul className="home-steps">
        {setupSteps.map((step) => {
          const ready = readySteps[step.id || step.action];
          return (
            <li key={step.title}>
              <img
                className="home-step-art"
                src={step.artwork}
                width={60}
                height={60}
                alt=""
                decoding="async"
              />
              <strong>{step.title}</strong>
              {ready ? (
                <span
                  className="home-step-ready"
                  role="img"
                  aria-label="Đã hoàn thành"
                >
                  {icon("check")}
                </span>
              ) : (
                button(step.label, step.action, "secondary small", step.id)
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}

function HomeDeadlines({ model, sessions }) {
  const [range, setRange] = useState("week");
  const end = nextDay(model.today, range === "day" ? 1 : 7);
  const tasks = model.due.filter(
    (task) =>
      task.deadline &&
      Number.isFinite(Date.parse(task.deadline)) &&
      dayKey(task.deadline) < end,
  );
  const nextSession = new Map();
  for (const session of sessions) {
    if (!["Scheduled", "Running", "Paused"].includes(session.status)) continue;
    if (!(Date.parse(session.end) > model.now)) continue;
    const previous = nextSession.get(session.taskId);
    if (!previous || Date.parse(session.start) < Date.parse(previous.start)) {
      nextSession.set(session.taskId, session);
    }
  }
  return (
    <section
      className="card home-panel home-deadlines"
      aria-labelledby="home-deadlines-heading"
    >
      <header className="home-panel-heading">
        <div className="home-deadlines-filter">
          <h2 id="home-deadlines-heading">
            {range === "day" ? "Đến hạn hôm nay hoặc quá hạn" : "Công việc sắp đến hạn"}
          </h2>
          {icon("chevron")}
          <select
            aria-label="Khoảng thời gian đến hạn"
            value={range}
            onChange={(event) => setRange(event.target.value)}
          >
            <option value="week">7 ngày tới</option>
            <option value="day">Hôm nay và quá hạn</option>
          </select>
        </div>
      </header>
      {tasks.length ? (
        <div
          className="home-table-wrap"
          role="region"
          aria-label="Lịch và hạn công việc"
          tabIndex={0}
        >
          <table
            className="home-table"
            aria-labelledby="home-deadlines-heading"
          >
            <thead>
              <tr>
                <th scope="col">Công việc</th>
                <th scope="col">Dự kiến</th>
                <th scope="col">Thời lượng</th>
                <th scope="col">Hạn chót</th>
              </tr>
            </thead>
            <tbody>
              {tasks.slice(0, 5).map((task) => {
                const overdue = Date.parse(task.deadline) < model.now;
                const dueToday = dayKey(task.deadline) === model.today;
                const scheduled = nextSession.get(task.id);
                return (
                  <tr key={task.id}>
                    <td>
                      <button
                        type="button"
                        className="home-table-link"
                        data-action="task-detail"
                        data-id={task.id}
                      >
                        {icon("tasks")} {task.name}
                      </button>
                    </td>
                    <td>
                      {scheduled ? (
                        <button
                          type="button"
                          className="planned-date-link"
                          data-action="calendar-date"
                          data-id={dayKey(scheduled.start)}
                          aria-label={`Mở lịch dự kiến của ${task.name}`}
                        >
                          {dayKey(scheduled.start) === model.today
                            ? "Hôm nay"
                            : dateFmt(scheduled.start)}
                        </button>
                      ) : (
                        "Chưa xếp"
                      )}
                    </td>
                    <td>
                      {task.estimatedMinutes
                        ? minutes(task.estimatedMinutes)
                        : "—"}
                    </td>
                    <td
                      title={`${dateFmt(task.deadline)} · ${timeFmt(task.deadline)}`}
                    >
                      {overdue || dueToday ? (
                        <span className="home-due-label" data-overdue={overdue}>
                          {overdue ? "Quá hạn" : "Hôm nay"}
                        </span>
                      ) : (
                        dateFmt(task.deadline)
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="home-empty home-deadlines-empty">
          <p>{range === "day" ? "Không có việc đến hạn hôm nay hoặc quá hạn." : "Không có việc nào sắp đến hạn."}</p>
          {button("Thêm công việc", "new-task", "secondary small")}
        </div>
      )}
      {tasks.length > 0 && (
        <div className="home-panel-footer">
          {button("Thêm công việc", "new-task", "ghost small", "", "plus")}
          {button("Xem tất cả công việc", "open-task-list", "ghost small", "all")}
        </div>
      )}
    </section>
  );
}

function HomeTeams() {
  const { value, error, retry } = useWorkloadRequest(
    "/api/teams",
    Array.isArray,
  );
  return (
    <section className="card home-panel" aria-labelledby="home-teams-heading">
      <header className="home-panel-heading">
        <h2 id="home-teams-heading">Nhóm của bạn</h2>
      </header>
      {error && (
        <div className="home-empty" role="status">
          <p>Chưa tải được danh sách nhóm.</p>
          <button type="button" className="btn secondary small" onClick={retry}>
            Thử lại
          </button>
        </div>
      )}
      {!error &&
        (value?.length ? (
          <>
            <div className="home-table-wrap">
              <table
                className="home-table"
                aria-labelledby="home-teams-heading"
              >
                <thead>
                  <tr>
                    <th scope="col">Nhóm</th>
                    <th scope="col">Vai trò</th>
                  </tr>
                </thead>
                <tbody>
                  {value.slice(0, 4).map((team) => (
                    <tr key={team.id}>
                      <td>
                        <button
                          type="button"
                          className="home-table-link"
                          onClick={() => openTeamTask(team.id, null)}
                        >
                          {icon("users")} {team.name}
                        </button>
                      </td>
                      <td>
                        {team.role === "Leader" ? "Trưởng nhóm" : "Thành viên"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="home-panel-footer">
              {button("Quản lý nhóm", "nav", "secondary small", "teams")}
            </div>
          </>
        ) : (
          <div className="home-empty">
            {value ? (
              <>
                <p>Bạn chưa tham gia nhóm nào.</p>
                {button("Mở quản lý nhóm", "nav", "secondary small", "teams")}
              </>
            ) : (
              <p role="status">Đang tải nhóm…</p>
            )}
          </div>
        ))}
    </section>
  );
}

function TodayHome({ onWorkspaceChanged }) {
  const [showSetup, setShowSetup] = useState(true);
  const [showAgenda, setShowAgenda] = useState(true);
  const workspace = state.workspace;
  const boundaries = [
    ...[...workspace.sessions, ...workspace.events].flatMap((item) => [
      Date.parse(item.start),
      Date.parse(item.end),
    ]),
    ...workspace.tasks.map((task) => Date.parse(task.deadline)),
  ];
  return (
    <CalendarClock boundaries={boundaries}>
      {(now) => {
        const model = dashboardModel(workspace, state.insights, now);
        return (
          <div className="today-home" data-agenda-open={showAgenda}>
            <header className="home-main-heading">
              <div className="home-main-title">
                <button
                  type="button"
                  className="console-icon-button home-agenda-toggle"
                  aria-expanded={showAgenda}
                  aria-controls="today-agenda"
                  aria-label={
                    showAgenda ? "Ẩn lịch trong ngày" : "Hiện lịch trong ngày"
                  }
                  title={
                    showAgenda ? "Ẩn lịch trong ngày" : "Hiện lịch trong ngày"
                  }
                  onClick={() => setShowAgenda((open) => !open)}
                >
                  {icon("panelToggle")}
                </button>
                <h1 tabIndex="-1">Hôm nay</h1>
              </div>
              <div className="actions">
                {!showSetup && (
                  <button
                    type="button"
                    className="btn ghost small"
                    onClick={() => setShowSetup(true)}
                  >
                    Hiện hướng dẫn
                  </button>
                )}
                <ToolButton label="Thêm việc" action="new-task" symbol="plus" />
              </div>
            </header>
            <div className="home-agenda-track" inert={!showAgenda}>
              <aside
                id="today-agenda"
                className="home-agenda"
                aria-label="Lịch trong ngày"
              >
                <DayRunway model={model} now={now} />
                <div className="home-agenda-footer">
                  {button(
                    "Kết nối lịch Google",
                    "calendar-connections",
                    "secondary small",
                    "",
                    "clock",
                  )}
                </div>
              </aside>
            </div>
            <div className="home-main">
              {showSetup && (
                <HomeSetup
                  workspace={workspace}
                  hasAvailability={model.hasAvailability}
                  onClose={() => setShowSetup(false)}
                />
              )}
              <HomeDeadlines model={model} sessions={workspace.sessions} />
              <HomeTeams />
              <TodayWorkload onWorkspaceChanged={onWorkspaceChanged} />
              <details className="card home-panel home-analysis">
                <summary>Tập trung và phân tích</summary>
                <div className="home-analysis-content">
                  <CapacityRibbon />
                  <ScheduledFocus
                    workspace={workspace}
                    onWorkspaceChanged={onWorkspaceChanged}
                  />
                  <UpcomingFocus workspace={workspace} />
                  <EisenhowerFocus />
                  <div className="home-analysis-grid">
                    <LiveIntelligence />
                  </div>
                </div>
              </details>
            </div>
          </div>
        );
      }}
    </CalendarClock>
  );
}

export function todayPage({ onWorkspaceChanged } = {}) {
  return <TodayHome onWorkspaceChanged={onWorkspaceChanged} />;
}
