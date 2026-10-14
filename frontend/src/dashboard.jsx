import { useState, Fragment } from "react";
import { SessionTimer } from "./react-ui.jsx";
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
import { nextWorkflowStep, workflowSteps } from "./workflow.js";
import { StartSessionButton } from "./session-start.jsx";

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
export function CapacityRibbon() {
  const model = dashboardModel(state.workspace, state.insights);
  const used = model.scheduledMinutes + model.focusMinutes + model.restMinutes;
  const ceiling = Math.max(
    used,
    model.capacityMinutes || model.availableMinutes,
    1,
  );
  return (
    <section className="capacity-ribbon" aria-label="Phân bổ thời gian hôm nay">
      <div className="capacity-ribbon-track">
        {[
          {
            name: "Đã xếp lịch",
            value: model.scheduledMinutes,
            tone: "scheduled",
          },
          { name: "Tập trung", value: model.focusMinutes, tone: "focus" },
          { name: "Đệm nghỉ", value: model.restMinutes, tone: "rest" },
        ].map((segment) => (
          <div
            key={segment.tone}
            className={`capacity-segment capacity-segment--${segment.tone}`}
            style={{ flexGrow: Math.max(segment.value / ceiling, 0.14) }}
            title={`${minutes(segment.value)} ${segment.name.toLowerCase()}`}
          >
            <strong>{minutes(segment.value)}</strong>
            <span>{segment.name}</span>
          </div>
        ))}
        <div
          className={`capacity-ceiling ${model.capacityMinutes !== null && used > model.capacityMinutes ? "is-overloaded" : ""}`}
        >
          <strong>{minutes(model.capacityMinutes)}</strong>
          <span>Khả năng hôm nay</span>
        </div>
      </div>
    </section>
  );
}
function FocusCockpit({ model }) {
  const next = nextWorkflowStep(state.workspace);
  const focus = model.focus;
  const active = focus && ["Running", "Paused"].includes(focus.status);
  const planned = focus
    ? Math.max(
        0,
        Math.round((new Date(focus.end) - new Date(focus.start)) / 60000),
      )
    : 0;
  return (
    <section
      className={`focus-cockpit ${active ? "is-active" : ""}`}
      aria-label="Phiên tập trung"
    >
      <div className="cockpit-timer">
        <span>{active ? "THỜI GIAN ĐÃ GHI NHẬN" : "PHIÊN TẬP TRUNG"}</span>
        {active ? (
          <SessionTimer id={focus.id} className="cockpit-time" compact />
        ) : (
          <strong className="cockpit-time">{clock(planned)}</strong>
        )}
        <small>
          {focus
            ? `${timeFmt(focus.start)} — ${timeFmt(focus.end)}`
            : "Chưa có phiên trong hôm nay"}
        </small>
      </div>
      <div className="cockpit-content">
        <span className="console-kicker">
          {focus?.status === "Paused"
            ? "ĐANG TẠM DỪNG"
            : active
              ? "ĐANG TẬP TRUNG"
              : focus
                ? "TIẾP THEO TRONG NGÀY"
                : "CHUẨN BỊ ĐỂ BẮT ĐẦU"}
        </span>
        <h2>{focus?.taskName || next.title}</h2>
        {!focus && <p className="reason">{next.description}</p>}
        {focus ? (
          <div className="cockpit-actions">
            {!active ? (
              <StartSessionButton
                session={focus}
                workspace={state.workspace}
                label="Bắt đầu phiên"
              />
            ) : (
              button(
                focus.status === "Running"
                  ? "Tạm dừng"
                  : focus.status === "Paused"
                    ? "Tiếp tục"
                    : "Bắt đầu phiên",
                focus.status === "Running"
                  ? "session-pause"
                  : focus.status === "Paused"
                    ? "session-resume"
                    : "session-start",
                "small secondary",
                focus.id,
                focus.status === "Running" ? "pause" : "play",
              )
            )}
            {button("Chi tiết", "session-detail", "small ghost", focus.id)}
            {active &&
              button(
                "Xong phiên",
                "session-complete",
                "small primary",
                focus.id,
                "check",
              )}
          </div>
        ) : (
          <div className="cockpit-actions">
            {button(
              next.label,
              next.action || "nav",
              "small primary",
              next.route || "",
              next.symbol,
            )}
          </div>
        )}
      </div>
    </section>
  );
}
function DayRunway({ model }) {
  const [view, setView] = useState("agenda");
  const next = nextWorkflowStep(state.workspace);
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
          {icon("calendar")} {dateFmt(Date.now(), { weekday: "long" })}
        </span>
        <span>{model.daySessions.length} phiên</span>
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
          Danh sách phiên
        </button>
        <button
          type="button"
          aria-pressed={view === "timeline"}
          onClick={() => setView("timeline")}
        >
          Dòng thời gian
        </button>
      </div>
      {view === "agenda" && (
        <div className="daily-agenda">
          {timeline.map((item) => {
            const rest = model.breaks.find(
              (rest) => rest.span[0] === item.span[1],
            );
            const status =
              {
                Scheduled: "Chờ làm",
                Running: "Đang làm",
                Paused: "Tạm dừng",
                Completed: "Đã xong",
                Skipped: "Bỏ qua",
                Missed: "Lỡ phiên",
              }[item.status] || "Lịch bận";
            return (
              <Fragment key={`${item.kind}-${item.id}`}>
                <div
                  className={`daily-agenda-row ${["Running", "Paused"].includes(item.status) ? "is-active" : ""} ${item.status === "Completed" ? "is-completed" : ""}`}
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
                            ? "Mở để bắt đầu hoặc xem chi tiết"
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
              <p>{next.description}</p>
              {button(
                next.label,
                next.action || "nav",
                "small secondary",
                next.route || "",
                next.symbol,
              )}
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
                    height: Math.max(2, (rest.span[1] - rest.span[0]) * scale),
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
                    className={`runway-block ${item.span[1] - item.span[0] < 45 ? "is-short" : ""} ${item.kind === "event" ? "runway-block--event" : cyan ? "runway-block--cyan" : "runway-block--blue"} ${item.status === "Completed" ? "is-completed" : ""}`}
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
    </section>
  );
}
function EstimationGauge({ model }) {
  return (
    <section className="intelligence-card estimation-card">
      <div className="console-card-heading">
        <h3>Độ khớp ước lượng</h3>
        <ToolButton
          label="Xem phân tích ước lượng"
          id="insights"
          symbol="insights"
        />
      </div>
      <div className="estimation-gauge">
        <svg viewBox="0 0 220 115" aria-hidden="true">
          <path className="gauge-track" d="M25 95 A85 85 0 0 1 195 95" />
          <path
            className="gauge-value"
            d="M25 95 A85 85 0 0 1 195 95"
            pathLength="100"
            strokeDasharray={`${model.accuracy ?? 0} 100`}
          />
        </svg>
        <div>
          <strong>
            {model.accuracy === null ? "—" : `${model.accuracy}%`}
          </strong>
          <span>
            {model.accuracy === null
              ? "Chưa có phiên ghi nhận"
              : "Dự kiến so với thực tế"}
          </span>
        </div>
      </div>
      <div className="gauge-caption">
        <span>
          {model.ratio
            ? `${model.ratio.ratio.toFixed(2)}× hệ số`
            : "Chưa đủ dữ liệu"}
        </span>
        <span>{model.ratio?.samples || 0} mẫu</span>
      </div>
    </section>
  );
}
function CapacityHorizon({ model }) {
  const maximum = Math.max(
    60,
    ...model.days.flatMap((day) => [day.capacity || 0, day.planned]),
  );
  return (
    <section className="intelligence-card horizon-card">
      <div className="console-card-heading">
        <h3>Khả năng trong 7 ngày</h3>
        <ToolButton label="Thiết lập thời gian rảnh" id="time" symbol="clock" />
      </div>
      <p>Lịch đã xếp và khả năng thực hiện</p>
      <div
        className="horizon-chart"
        role="img"
        aria-label="Lịch đã xếp và khả năng thực hiện trong bảy ngày tới"
      >
        {model.days.map((day, index) => (
          <div
            className="horizon-day"
            key={day.key}
            title={`${dateFmt(`${day.key}T12:00:00Z`)}: ${minutes(day.planned)} đã xếp; ${minutes(day.capacity)} khả năng`}
          >
            <div className="horizon-bar-space">
              <div
                className="horizon-capacity"
                style={{
                  height: `${((day.capacity || 0) / maximum) * 100}%`,
                }}
              />
              <div
                className={`horizon-planned ${index % 2 ? "is-blue" : ""}`}
                style={{ height: `${(day.planned / maximum) * 100}%` }}
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
          Đã xếp
        </span>
        <span>
          <i className="legend-rest" />
          Khả năng
        </span>
      </div>
    </section>
  );
}
function Deadlines({ model }) {
  const deadlines = model.due.filter((task) => task.isHardDeadline).slice(0, 4);
  return (
    <section className="intelligence-card deadlines-card">
      <div className="console-card-heading">
        <h3>Deadline cần giữ</h3>
        <ToolButton label="Xem tất cả công việc" id="tasks" symbol="tasks" />
      </div>
      <p>Những mốc không thể bỏ lỡ</p>
      {deadlines.length ? (
        <div className="deadline-list">
          {deadlines.map((task) => (
            <button
              type="button"
              key={task.id}
              data-action="task-detail"
              data-id={task.id}
              className={`deadline-line ${new Date(task.deadline) < Date.now() ? "is-overdue" : ""}`}
            >
              <span className="deadline-day">
                <strong>{dateFmt(task.deadline, { month: undefined })}</strong>
                <small>
                  {dateFmt(task.deadline, { day: undefined, month: "short" })}
                </small>
              </span>
              <span className="deadline-info">
                <strong>{task.name}</strong>
                <small>
                  {timeFmt(task.deadline)} · {minutes(task.remainingMinutes)}{" "}
                  còn lại
                </small>
              </span>
              {icon("arrow")}
            </button>
          ))}
        </div>
      ) : (
        <div className="console-empty">Chưa có deadline cố định.</div>
      )}
    </section>
  );
}
export function todayPage() {
  const model = dashboardModel(state.workspace, state.insights);
  const insights = state.insights;
  const next = nextWorkflowStep(state.workspace);
  const readyToPlan = workflowSteps(state.workspace)
    .slice(0, 2)
    .every((step) => step.done);
  return (
    <>
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
            <ToolButton
              label={readyToPlan ? "Xem lịch đề xuất" : next.label}
              action={readyToPlan ? "generate-plan" : next.action || "nav"}
              id={readyToPlan ? "" : next.route || ""}
              symbol={readyToPlan ? "calendar" : next.symbol}
              text={readyToPlan ? "Xem lịch đề xuất" : next.label}
            />
          </div>
          <FocusCockpit model={model} />
          <DayRunway model={model} />
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
          <EstimationGauge model={model} />
          <CapacityHorizon model={model} />
          <Deadlines model={model} />
          <div className="console-workload">
            <span
              className={`workload-dot ${insights?.riskLevel === "Overload" || insights?.riskLevel === "High" ? "is-high" : ""}`}
            />
            <span>
              {insights
                ? `Khối lượng việc ${riskLabels[insights.riskLevel] || insights.riskLevel}`
                : "Chưa xác định"}
            </span>
            <strong>{minutes(insights?.remainingMinutes)} còn lại</strong>
          </div>
        </aside>
      </div>
    </>
  );
}
