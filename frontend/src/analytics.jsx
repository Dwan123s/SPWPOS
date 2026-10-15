import { useEffect, useState } from "react";
import { api } from "./api.js";
import { minutes } from "./ui.jsx";

const notices = {
  INSUFFICIENT_COMPLETION_SAMPLES:
    "Chưa đủ 3 công việc hoàn thành để kết luận xu hướng.",
  OPEN_TIMER_PROVISIONAL:
    "Thời gian phiên đang chạy là tạm tính và được hiển thị riêng.",
  MANUAL_ACTUAL_SEPARATE_FROM_FOCUS:
    "Có thời lượng chỉnh tay. Thời gian tập trung vẫn lấy từ các đoạn ghi nhận của bộ đếm.",
  NO_COMPARISON_BASELINE:
    "Kỳ trước chưa có thời gian tập trung để tính phần trăm thay đổi.",
};
const duration = (seconds) => minutes(Math.floor(seconds / 60));
// Compatibility for an older server during a staged API/schema upgrade.
// No date-based focus is inferred from scheduled session dates.
export function WorkspacePerformance({ tasks }) {
  const [page, setPage] = useState(1);
  const rows = tasks.slice((page - 1) * 20, page * 20);
  return (
    <section className="card section-gap analytics-panel">
      <h2>Dự kiến và thực tế</h2>
      <p>Thời lượng đã ghi nhận theo công việc.</p>
      {rows.length ? (
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Công việc</th>
                <th>Dự kiến</th>
                <th>Đã ghi nhận</th>
                <th>Tiến độ</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((task) => (
                <tr key={task.id}>
                  <td>{task.name}</td>
                  <td>{minutes(task.estimatedMinutes)}</td>
                  <td>{minutes(task.actualMinutes)}</td>
                  <td>{task.progress}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="compact-empty">
          Chưa có thời lượng công việc để hiển thị.
        </p>
      )}
      {tasks.length > 20 && (
        <nav className="pagination" aria-label="Phân trang thời lượng">
          <button
            className="btn secondary small"
            type="button"
            disabled={page === 1}
            onClick={() => setPage((x) => x - 1)}
          >
            Trước
          </button>
          <span>
            {" "}
            Trang {page} · {tasks.length} công việc{" "}
          </span>
          <button
            className="btn secondary small"
            type="button"
            disabled={page * 20 >= tasks.length}
            onClick={() => setPage((x) => x + 1)}
          >
            Sau
          </button>
        </nav>
      )}
    </section>
  );
}
export function AnalyticsHistory() {
  const [days, setDays] = useState("7");
  const [range, setRange] = useState(null);
  const [data, setData] = useState(null);
  const [error, setError] = useState("");
  const [retry, setRetry] = useState(0);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [tasks, setTasks] = useState(null);
  const [taskError, setTaskError] = useState("");
  useEffect(() => {
    let active = true;
    setLoading(true);
    setError("");
    const query = range
      ? `start=${range.start}&end=${range.end}`
      : `days=${days}`;
    api(`/api/intelligence/history?${query}`)
      .then((result) => {
        if (
          !Array.isArray(result?.days) ||
          !result.current ||
          !Array.isArray(result.notices)
        )
          throw new Error("Chưa tải được thống kê hợp lệ. Hãy thử lại.");
        if (active) setData(result);
      })
      .catch((e) => {
        if (active) setError(e.message);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [days, range, retry]);
  useEffect(() => {
    let active = true;
    setTaskError("");
    setTasks(null);
    api(
      `/api/intelligence/tasks?page=${page}&pageSize=20&search=${encodeURIComponent(search)}`,
    )
      .then((result) => {
        if (!Array.isArray(result?.items) || !Number.isFinite(result.total))
          throw new Error("Chưa tải được danh sách công việc. Hãy thử lại.");
        if (active) setTasks(result);
      })
      .catch((e) => {
        if (active) setTaskError(e.message);
      });
    return () => {
      active = false;
    };
  }, [page, search, retry]);
  return (
    <section
      className="card section-gap analytics-panel"
      aria-label="Thống kê lịch sử"
    >
      <div className="card-header">
        <div>
          <h2>Nhìn lại tiến độ</h2>
          <p>Giờ tập trung theo thời điểm thực hiện, theo múi giờ tài khoản.</p>
        </div>
        <label className="field">
          Khoảng xem{" "}
          <select
            aria-label="Khoảng thống kê"
            value={range ? "custom" : days}
            onChange={(e) => {
              if (e.target.value !== "custom") {
                setRange(null);
                setDays(e.target.value);
              }
            }}
          >
            <option value="3">3 ngày</option>
            <option value="7">7 ngày</option>
            <option value="30">30 ngày</option>
            {range && <option value="custom">Tùy chọn</option>}
          </select>
        </label>
      </div>
      <details className="analytics-date-range">
        <summary>Chọn khoảng ngày</summary>
        <form
          className="analytics-range-form"
          onInput={(e) => e.currentTarget.elements.end.setCustomValidity("")}
          onSubmit={(e) => {
            e.preventDefault();
            const values = new FormData(e.currentTarget);
            const start = values.get("start"),
              end = values.get("end");
            const field = e.currentTarget.elements.end;
            const length = (Date.parse(end) - Date.parse(start)) / 86400000 + 1;
            field.setCustomValidity(
              length < 1
                ? "Ngày kết thúc cần từ ngày bắt đầu trở đi."
                : length > 90
                  ? "Chọn tối đa 90 ngày để xem thống kê."
                  : "",
            );
            if (!e.currentTarget.reportValidity()) return;
            setRange({ start, end });
          }}
        >
          <label className="field">
            Từ ngày <input name="start" type="date" required />
          </label>
          <label className="field">
            Đến ngày <input name="end" type="date" required />
          </label>
          <button type="submit" className="btn secondary">
            Xem
          </button>
        </form>
      </details>
      {loading && <p role="status">Đang tải thống kê…</p>}
      {error && (
        <p role="alert" className="inline-feedback is-error">
          {error}{" "}
          <button
            type="button"
            className="btn secondary small"
            onClick={() => setRetry((x) => x + 1)}
          >
            Thử lại
          </button>
        </p>
      )}
      {!loading && !error && data && (
        <>
          <p className="reason">
            {data.start} – {data.end} · {data.timeZoneId}
          </p>
          <div className="stats-grid analytics-metrics">
            <div>
              <span>Tập trung đã ghi nhận</span>
              <h3>{duration(data.current.focusSeconds)}</h3>
              <p>
                {data.focusChangePercent == null
                  ? "Chưa có kỳ so sánh"
                  : `${data.focusChangePercent > 0 ? "+" : ""}${data.focusChangePercent}% so với kỳ trước`}
              </p>
            </div>
            <div>
              <span>Đang chạy · tạm tính</span>
              <h3>{duration(data.current.openFocusSeconds)}</h3>
            </div>
            <div>
              <span>Công việc hoàn thành</span>
              <h3>{data.current.completedTasks}</h3>
              <p>
                {data.current.onTimeTasks} đúng hạn · không tính trùng việc cha
              </p>
            </div>
            <div>
              <span>Lịch dự kiến</span>
              <h3>{duration(data.current.plannedSeconds)}</h3>
              <p>Gồm các phiên chưa thực hiện hoặc bị lỡ</p>
            </div>
          </div>
          {data.notices.map((code) => (
            <p key={code} className="reason">
              {notices[code] || code}
            </p>
          ))}
          <div className="table-scroll">
            <table>
              <caption>Lịch dự kiến và thời gian tập trung theo ngày</caption>
              <thead>
                <tr>
                  <th>Ngày</th>
                  <th>Dự kiến</th>
                  <th>Tập trung</th>
                  <th>Đang chạy</th>
                  <th>Hoàn thành</th>
                </tr>
              </thead>
              <tbody>
                {data.days.map((day) => (
                  <tr key={day.date}>
                    <td>{day.date}</td>
                    <td>{duration(day.plannedSeconds)}</td>
                    <td>{duration(day.focusSeconds)}</td>
                    <td>{duration(day.openFocusSeconds)}</td>
                    <td>{day.completedTasks}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
      <h3>Thời lượng theo công việc</h3>
      <p className="reason">
        Thực tế đã ghi nhận có thể gồm chỉnh tay; không thay thế giờ tập trung ở
        trên.
      </p>
      <form
        className="analytics-search"
        onSubmit={(e) => {
          e.preventDefault();
          setPage(1);
          setSearch(new FormData(e.currentTarget).get("search").trim());
        }}
      >
        <label className="field">
          Tìm công việc <input name="search" maxLength={100} />
        </label>
        <button type="submit" className="btn secondary">
          Tìm
        </button>
      </form>
      {taskError ? (
        <p role="alert" className="inline-feedback is-error">
          {taskError}{" "}
          <button
            type="button"
            className="btn secondary small"
            onClick={() => setRetry((x) => x + 1)}
          >
            Thử lại
          </button>
        </p>
      ) : !tasks ? (
        <p role="status">Đang tải công việc…</p>
      ) : (
        <>
          {tasks.items.length > 0 && (
            <div className="table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Công việc</th>
                    <th>Dự kiến</th>
                    <th>Đã ghi nhận</th>
                    <th>Tiến độ</th>
                  </tr>
                </thead>
                <tbody>
                  {tasks.items.map((task) => (
                    <tr key={task.id}>
                      <td>{task.name}</td>
                      <td>{minutes(task.estimatedMinutes)}</td>
                      <td>{duration(task.recordedActualSeconds)}</td>
                      <td>{task.progress}%</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {tasks.total === 0 && (
            <p className="compact-empty">Chưa có công việc phù hợp.</p>
          )}
          {tasks.total > tasks.pageSize && (
            <nav className="pagination" aria-label="Phân trang thống kê">
              <button
                className="btn secondary small"
                type="button"
                disabled={page === 1}
                onClick={() => setPage((x) => x - 1)}
              >
                Trước
              </button>
              <span>
                {" "}
                Trang {page} · {tasks.total} công việc{" "}
              </span>
              <button
                className="btn secondary small"
                type="button"
                disabled={page * tasks.pageSize >= tasks.total}
                onClick={() => setPage((x) => x + 1)}
              >
                Sau
              </button>
            </nav>
          )}
        </>
      )}
    </section>
  );
}
