import { SessionTimer } from "./react-ui.jsx";
import { nodeList } from "./react-ui.jsx";
import {
  state,
  button,
  badge,
  icon,
  riskLabels,
  dateFmt,
  timeFmt,
  dayKey,
  minutes,
  stat,
  empty,
  loadLabel,
} from "./ui.jsx";
import { sessionCard } from "./calendar.jsx";
export function todayPage() {
  const w = state.workspace,
    i = state.insights || {
      remainingMinutes: null,
      effectiveCapacityMinutes: null,
      capacityFactor: null,
      riskLevel: "Unknown",
      loadRatio: null,
      plannedMinutes: null,
      actualMinutes: null,
    };
  const day = dayKey(Date.now()),
    sessions = w.sessions
      .filter(
        (s) =>
          ["Running", "Paused"].includes(s.status) || dayKey(s.start) === day,
      )
      .sort((a, b) => new Date(a.start) - new Date(b.start));
  const active = w.tasks.filter(
      (t) => !["Completed", "Cancelled"].includes(t.status),
    ),
    due = [...active]
      .sort((a, b) => new Date(a.deadline) - new Date(b.deadline))
      .slice(0, 5);
  const completed = w.tasks.filter((t) => t.status === "Completed").length,
    done = sessions.filter((s) => s.status === "Completed").length,
    scheduled = sessions
      .filter((s) => !["Skipped", "Missed"].includes(s.status))
      .reduce(
        (sum, s) =>
          sum + Math.round((new Date(s.end) - new Date(s.start)) / 60000),
        0,
      );
  const next =
    sessions.find((s) => ["Running", "Paused"].includes(s.status)) ||
    sessions.find(
      (s) => s.status === "Scheduled" && new Date(s.end) > Date.now(),
    ) ||
    sessions.find((s) => s.status === "Scheduled");
  const running = next && ["Running", "Paused"].includes(next.status);
  const focus = next ? (
    <>
      <div className="focus-status">
        {badge(running ? "PHIÊN TẬP TRUNG" : "TIẾP THEO TRONG NGÀY", "purple")}
        <span>
          {timeFmt(next.start)}
          {" — "}
          {timeFmt(next.end)}
        </span>
      </div>
      <h2>{next.taskName}</h2>
      <p>
        {running
          ? "Giữ nhịp tập trung. Thời gian thực tế được ghi nhận trong phiên."
          : "Dành một khoảng thời gian trọn vẹn cho công việc này."}
      </p>
      <div className="focus-controls">
        {running ? (
          <SessionTimer id={next.id} className="focus-timer" />
        ) : (
          <span className="focus-duration">
            {icon("clock")}
            {minutes(
              Math.round((new Date(next.end) - new Date(next.start)) / 60000),
            )}
            {" tập trung"}
          </span>
        )}
        <div className="actions">
          {button(
            next.status === "Running"
              ? "Tạm dừng"
              : next.status === "Paused"
                ? "Tiếp tục"
                : "Bắt đầu phiên",
            next.status === "Running"
              ? "session-pause"
              : next.status === "Paused"
                ? "session-resume"
                : "session-start",
            "primary",
            next.id,
            next.status === "Running" ? "pause" : "play",
          )}
          {running
            ? button(
                "Xong phiên",
                "session-complete",
                "secondary",
                next.id,
                "check",
              )
            : button("Chi tiết", "session-detail", "ghost", next.id, "arrow")}
        </div>
      </div>
    </>
  ) : (
    <>
      <div className="focus-status">
        {badge("BẮT ĐẦU TỪ MỘT KẾ HOẠCH", "purple")}
        {icon("spark")}
      </div>
      <h2>Dành chỗ cho điều quan trọng.</h2>
      <p>
        {w.availability.length
          ? "Biến danh sách công việc thành những khoảng thời gian cụ thể. Bạn xem lại đề xuất trước khi áp dụng."
          : "Cho PLANORA biết khi nào bạn rảnh để tạo một kế hoạch vừa sức."}
      </p>
      <div className="focus-controls">
        <span className="focus-duration">
          {icon("calendar")}Theo nhịp làm việc của bạn
        </span>
        {button(
          w.availability.length ? "Lập kế hoạch" : "Thiết lập giờ rảnh",
          w.availability.length ? "generate-plan" : "nav",
          "primary",
          w.availability.length ? "" : "time",
          "arrow",
        )}
      </div>
    </>
  );
  return (
    <>
      <div className="daily-overview">
        <section className="focus-card">{focus}</section>
        <section className="day-summary card">
          <div className="summary-heading">
            <span className="summary-icon">{icon("today")}</span>
            <span>Nhịp làm việc hôm nay</span>
          </div>
          <div className="day-count">
            {done}
            <span>
              {"/ "}
              {sessions.length}
              {" phiên hoàn thành"}
            </span>
          </div>
          <progress
            className="progress"
            value={done}
            max={Math.max(1, sessions.length)}
            aria-label="Số phiên đã hoàn thành hôm nay"
          />
          <div className="summary-bottom">
            <span>
              {icon("clock")}
              {minutes(scheduled)}
              {" đã lên lịch"}
            </span>
            {button("Lịch tuần", "nav", "small ghost", "calendar", "arrow")}
          </div>
        </section>
      </div>
      <div className="stats-grid">
        {stat(
          "Công việc đang mở",
          active.length,
          "Công việc chưa hoàn thành",
          "tasks",
        )}
        {stat(
          "Workload còn lại",
          minutes(i.remainingMinutes),
          "Tổng thời gian ước lượng đã điều chỉnh",
          "clock",
          "blue",
        )}
        {stat(
          "Khả năng trong 7 ngày",
          minutes(i.effectiveCapacityMinutes),
          i.capacityFactor === null
            ? "Chưa lấy được dữ liệu phân tích"
            : `${Math.round(i.capacityFactor * 100)}% thời gian rảnh có thể thực hiện`,
          "calendar",
          "green",
        )}
        {stat(
          "Mức workload",
          riskLabels[i.riskLevel] || i.riskLevel,
          loadLabel(i),
          "insights",
          "amber",
        )}
      </div>
      <div className="two-columns dashboard-columns">
        <div className="stack">
          <section className="card priority-card">
            <div className="card-header">
              <div>
                <div className="section-kicker">BƯỚC TIẾP THEO</div>
                <h2>
                  {"Công việc cần chú ý "}
                  <span className="count-pill">{due.length}</span>
                </h2>
                <p>Những deadline gần nhất trong danh sách của bạn.</p>
              </div>
              {button("Tất cả", "nav", "small ghost", "tasks", "arrow")}
            </div>
            {nodeList(
              due.map((t) => (
                <div className="priority-task">
                  <span
                    className={`task-status-marker ${t.status === "InProgress" ? "in-progress" : ""}`}
                    aria-hidden="true"
                  />
                  <div className="priority-task-main">
                    <button
                      type="button"
                      className="title-button"
                      data-action="task-detail"
                      data-id={t.id}
                    >
                      {t.name}
                    </button>
                    <div className="task-meta">
                      <span>
                        {w.categories?.find((c) => c.id === t.categoryId)
                          ?.name || "Chưa phân nhóm"}
                      </span>
                      <span>·</span>
                      <span>
                        {minutes(t.remainingMinutes)}
                        {" còn lại"}
                      </span>
                    </div>
                  </div>
                  <div
                    className={`priority-task-deadline ${new Date(t.deadline) < Date.now() ? "overdue" : ""}`}
                  >
                    {dateFmt(t.deadline)}
                    <small>
                      {new Date(t.deadline) < Date.now()
                        ? "Quá hạn"
                        : timeFmt(t.deadline)}
                    </small>
                  </div>
                  {badge(
                    ["", "Thấp", "Vừa", "Cao", "Khẩn"][t.priority],
                    t.priority >= 3 ? "amber" : "",
                  )}
                </div>
              )),
            ) ||
              empty(
                "Một khoảng trống để bắt đầu",
                "Thêm công việc đầu tiên và dành thời gian cho nó.",
              )}
            <div className="card-bottom">
              {button(
                "Ghi nhanh một ý tưởng",
                "capture",
                "ghost small",
                "",
                "plus",
              )}
            </div>
          </section>
          <section className="card reflection-card">
            <div className="card-header">
              <div>
                <div className="section-kicker">HIỂU NHỊP CỦA BẠN</div>
                <h2>Từ kế hoạch đến thực tế</h2>
              </div>
              {button("Phân tích", "nav", "small ghost", "insights", "arrow")}
            </div>
            <div className="reflection-metrics">
              <div>
                <span>Dự kiến / thực tế</span>
                <strong>
                  {minutes(i.plannedMinutes)}{" "}
                  <small>
                    {"/ "}
                    {minutes(i.actualMinutes)}
                  </small>
                </strong>
              </div>
              <div>
                <span>Công việc hoàn thành</span>
                <strong>
                  {completed}
                  <small>{" công việc"}</small>
                </strong>
              </div>
            </div>
            <p className="reason">
              Mỗi phiên được ghi nhận giúp kế hoạch tiếp theo phù hợp hơn với
              bạn.
            </p>
          </section>
        </div>
        <section className="card agenda-card">
          <div className="card-header">
            <div>
              <div className="section-kicker">DÒNG THỜI GIAN</div>
              <h2>
                {"Lịch hôm nay "}
                <span className="count-pill">{sessions.length}</span>
              </h2>
            </div>
            {button("Mở lịch", "nav", "small ghost", "calendar", "calendar")}
          </div>
          <div className="today-agenda">
            {nodeList(
              sessions.map((s) => (
                <div className="agenda-entry">
                  <div className="agenda-time">
                    {timeFmt(s.start)}
                    <span />
                  </div>
                  <div>{sessionCard(s)}</div>
                </div>
              )),
            ) ||
              empty(
                "Hôm nay còn trống",
                "Tạo kế hoạch để dành thời gian cho công việc.",
                "generate-plan",
                "Lập kế hoạch",
              )}
          </div>
        </section>
      </div>
    </>
  );
}
