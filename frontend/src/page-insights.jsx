import { Select, Slot, nodeList } from "./react-ui.jsx";
import {
  state,
  button,
  badge,
  icon,
  riskLabels,
  reasons,
  typeLabels,
  dateFmt,
  timeFmt,
  dayKey,
  minutes,
  stat,
  field,
  empty,
  modal,
  loadLabel,
} from "./ui.jsx";
import { AnalyticsHistory, WorkspacePerformance } from "./analytics.jsx";
import { WorkloadWeek } from "./workload.jsx";
export function insightsPage({ onWorkspaceChanged } = {}) {
  if (!state.insights)
    return (
      <>
        <WorkloadWeek onWorkspaceChanged={onWorkspaceChanged} />
        {state.workspace.features?.includes("history-v2") && (
          <AnalyticsHistory />
        )}
        <section className="card">
          <h2>Chưa có kết quả phân tích</h2>
          <p>
            Không thể lấy dữ liệu lúc này. Hãy thử tải lại để xem đánh giá
            workload và rủi ro.
          </p>
        </section>
      </>
    );
  const i = state.insights;
  return (
    <>
      <WorkloadWeek onWorkspaceChanged={onWorkspaceChanged} />
      <div className="stats-grid">
        {stat(
          "Workload còn lại",
          minutes(i.remainingMinutes),
          "Chỉ tính công việc độc lập và việc con",
          "tasks",
        )}
        {stat(
          "Thời gian rảnh",
          minutes(i.calendarFreeMinutes),
          "Sau khi trừ sự kiện và ngoại lệ",
          "calendar",
          "blue",
        )}
        {stat(
          "Khả năng hiệu quả",
          minutes(i.effectiveCapacityMinutes),
          `${i.capacitySamples} phiên quan sát`,
          "clock",
          "green",
        )}
        {stat(
          "Nguy cơ tổng thể",
          riskLabels[i.riskLevel],
          loadLabel(i),
          "insights",
          "amber",
        )}
      </div>
      <div className="equal-columns section-gap">
        <section className="card">
          <div className="card-header">
            <div>
              <h2>Hệ số ước lượng cá nhân</h2>
              <p>Thời gian thực tế ÷ dự kiến, từ công việc đã hoàn thành.</p>
            </div>
          </div>
          {nodeList(
            i.biases.map((b) => (
              <div className="bias-item">
                <div>
                  <strong>
                    {b.taskType === "All"
                      ? "Toàn bộ công việc"
                      : typeLabels[b.taskType] || b.taskType}
                  </strong>
                  <p>
                    {b.samples}
                    {" mẫu · "}
                    {b.personalized
                      ? "Đã cá nhân hóa"
                      : "Chưa đủ 3 mẫu, dùng hệ số trung tính"}
                  </p>
                </div>
                <strong>{b.ratio.toFixed(2)}×</strong>
              </div>
            )),
          ) || (
            <p className="reason">
              Hoàn thành ít nhất 3 công việc có thời gian thực tế để bắt đầu cá
              nhân hóa.
            </p>
          )}
          <p className="reason spaced">
            Ví dụ: hệ số 1.25× điều chỉnh 60 phút dự kiến thành 75 phút khi lập
            lịch.
          </p>
        </section>
        <section className="card">
          <div className="card-header">
            <div>
              <h2>Khả năng thực hiện</h2>
              <p>Tỷ lệ thời gian được hoàn thành trong các phiên đã đến hạn.</p>
            </div>
          </div>
          <p className="reason capacity-mode">
            {i.capacitySamples >= 5
              ? "Dựa trên lịch sử cá nhân"
              : "Mức dự phòng ban đầu · chưa phải kết quả cá nhân"}
          </p>
          <div className="capacity-visual">
            <svg viewBox="0 0 120 120" aria-hidden="true">
              <circle
                cx="60"
                cy="60"
                r="48"
                stroke="#edeafa"
                strokeWidth="12"
              />
              <circle
                cx="60"
                cy="60"
                r="48"
                stroke="#8780e5"
                strokeWidth="12"
                strokeDasharray={`${i.capacityFactor * 302} 302`}
                transform="rotate(-90 60 60)"
              />
            </svg>
            <strong>{Math.round(i.capacityFactor * 100)}%</strong>
          </div>
          <p className="reason">
            {i.capacitySamples >= 5
              ? "Đã sử dụng lịch sử cá nhân để tính khả năng thực hiện."
              : "Chưa đủ 5 phiên quan sát. Tạm dùng 80% để dành một phần dự phòng."}
          </p>
          {nodeList(
            i.reasons.map((r) => (
              <div className="warning">{reasons[r] || r}</div>
            )),
          )}
        </section>
      </div>
      {state.workspace.features?.includes("history-v2") ? (
        <AnalyticsHistory />
      ) : (
        <WorkspacePerformance tasks={state.workspace.tasks} />
      )}
      <section className="card">
        <div className="card-header">
          <h2>Rủi ro theo công việc</h2>
        </div>
        {i.tasks.length
          ? nodeList(
              i.tasks.map((t) => (
                <div className="list-line">
                  <div>
                    <button
                      className="text-btn"
                      data-action="task-detail"
                      data-id={t.taskId}
                    >
                      {t.name}
                    </button>
                    <p>
                      {t.reasons.map((r) => reasons[r] || r).join(" · ") ||
                        "Chưa phát hiện rủi ro đáng kể"}
                    </p>
                  </div>
                  {badge(
                    riskLabels[t.level],
                    t.level === "High"
                      ? "red"
                      : t.level === "Medium"
                        ? "amber"
                        : "green",
                  )}
                </div>
              )),
            )
          : empty("Chưa có dữ liệu", "Thêm công việc để xem phân tích.")}
      </section>
    </>
  );
}
