import { useEffect, useState } from "react";
import { api } from "./api.js";
import { button, dayKey, icon, minutes } from "./ui.jsx";
import "./analytics-history.css";

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
                  <td>
                    {button(task.name, "task-detail", "small ghost", task.id)}
                  </td>
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
const periods = [
  ["3", "3 ngày"],
  ["7", "7 ngày"],
  ["30", "30 ngày"],
];
const longDuration = (seconds) => {
  if (seconds == null) return "—";
  if (seconds > 0 && seconds < 60) return "Dưới 1 phút";
  return minutes(Math.floor(seconds / 60), "long");
};
const dayFormat = new Intl.DateTimeFormat("vi-VN", {
  weekday: "short",
  day: "numeric",
  month: "numeric",
  timeZone: "UTC",
});
const rangeFormat = new Intl.DateTimeFormat("vi-VN", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  timeZone: "UTC",
});

function AnalyticsError({ message, onRetry }) {
  return (
    <div className="timesheet-feedback" role="alert">
      <span>{message}</span>
      <button className="btn secondary small" type="button" onClick={onRetry}>
        Thử lại
      </button>
    </div>
  );
}

function DateRangeForm({ range, onApply }) {
  const today = dayKey(Date.now());
  const [start, setStart] = useState(range?.start || today);
  const [end, setEnd] = useState(range?.end || today);
  const [message, setMessage] = useState("");
  return (
    <form
      className="timesheet-range-form"
      onSubmit={(event) => {
        event.preventDefault();
        const length = (Date.parse(end) - Date.parse(start)) / 86400000 + 1;
        if (length < 1 || length > 90 || end > today) {
          setMessage("Chọn khoảng từ 1 đến 90 ngày, không vượt hôm nay.");
          return;
        }
        setMessage("");
        onApply({ start, end });
      }}
    >
      <label className="field">
        Từ ngày
        <input
          type="date"
          value={start}
          max={today}
          required
          onChange={(event) => {
            setStart(event.target.value);
            setMessage("");
          }}
        />
      </label>
      <label className="field">
        Đến ngày
        <input
          type="date"
          value={end}
          max={today}
          required
          onChange={(event) => {
            setEnd(event.target.value);
            setMessage("");
          }}
        />
      </label>
      <button className="btn primary" type="submit">
        Áp dụng
      </button>
      {message && <p role="alert">{message}</p>}
    </form>
  );
}

function TimesheetTotals({ data }) {
  const { current, focusChangePercent } = data;
  const metrics = [
    [
      "check",
      "Đã ghi nhận",
      current.recordedActualSeconds,
      "Thời lượng đã lưu, gồm cả số nhập tay.",
      "recorded",
    ],
    [
      "calendar",
      "Lịch dự kiến",
      current.plannedSeconds,
      "Tổng thời lượng các phiên trong lịch.",
      "planned",
    ],
    [
      "clock",
      "Tập trung",
      current.focusSeconds,
      "Thời lượng từ bộ đếm khi bạn làm việc.",
      "focus",
    ],
  ];
  return (
    <div className="timesheet-overview">
      <dl className="timesheet-totals">
        {metrics.map(([symbol, label, value, hint, tone]) => (
          <div key={label} className="timesheet-metric" data-tone={tone}>
            <dt>
              <span className="timesheet-icon">{icon(symbol)}</span>
              {label}
            </dt>
            <dd>{longDuration(value)}</dd>
            <dd className="timesheet-metric-hint">{hint}</dd>
          </div>
        ))}
      </dl>
      <div className="timesheet-summary-line">
        <span>
          <strong>{current.completedTasks}</strong> việc hoàn thành ·{" "}
          {current.onTimeTasks} đúng hạn
        </span>
        {focusChangePercent != null && (
          <span>
            Tập trung{" "}
            <strong>
              {focusChangePercent > 0 ? "+" : ""}
              {focusChangePercent}%
            </strong>{" "}
            so với kỳ trước
          </span>
        )}
        {current.openFocusSeconds > 0 && (
          <span className="timesheet-running">
            Đang chạy · {longDuration(current.openFocusSeconds)} tạm tính
          </span>
        )}
      </div>
    </div>
  );
}

const comparedTimes = [
  { key: "plannedSeconds", label: "Dự kiến", tone: "planned" },
  { key: "focusSeconds", label: "Tập trung", tone: "focus" },
];
const hasDayActivity = (day) =>
  day.plannedSeconds ||
  day.focusSeconds ||
  day.openFocusSeconds ||
  day.completedTasks;

function TimesheetDays({ data }) {
  const [visible, setVisible] = useState(7);
  const days = [...data.days].sort((a, b) => b.date.localeCompare(a.date));
  const scale = Math.max(
    1,
    ...days.flatMap((day) => comparedTimes.map(({ key }) => day[key])),
  );
  return (
    <section
      className="timesheet-card timesheet-days"
      aria-label="Giờ làm theo ngày"
    >
      <header className="timesheet-detail-heading">
        <h3>Theo ngày</h3>
        <div className="timesheet-legend">
          {comparedTimes.map(({ key, label, tone }) => (
            <span key={key} data-tone={tone}>
              <i aria-hidden="true" />
              {label}
            </span>
          ))}
        </div>
      </header>
      {days.some(hasDayActivity) ? (
        <>
          <ul className="timesheet-day-list">
            {days.slice(0, visible).map((day) => (
              <li key={day.date}>
                <div className="timesheet-day-heading">
                  <button
                    type="button"
                    className="timesheet-text-link"
                    data-action="calendar-date"
                    data-id={day.date}
                    aria-label={`Mở lịch ngày ${day.date}`}
                  >
                    <time dateTime={day.date}>
                      {dayFormat.format(new Date(day.date))}
                    </time>
                    {icon("arrow")}
                  </button>
                  {day.completedTasks > 0 && (
                    <small>{day.completedTasks} việc hoàn thành</small>
                  )}
                </div>
                <div className="timesheet-day-comparison">
                  {comparedTimes.map(({ key, label, tone }) => (
                    <div
                      key={key}
                      className="timesheet-comparison-row"
                      data-tone={tone}
                    >
                      <span>{label}</span>
                      <div className="timesheet-track" aria-hidden="true">
                        <span
                          style={{ width: `${(100 * day[key]) / scale}%` }}
                        />
                      </div>
                      <strong>{longDuration(day[key])}</strong>
                    </div>
                  ))}
                  {day.openFocusSeconds > 0 && (
                    <small className="timesheet-running">
                      Đang chạy · {longDuration(day.openFocusSeconds)} tạm tính
                    </small>
                  )}
                </div>
              </li>
            ))}
          </ul>
          {visible < days.length && (
            <div className="timesheet-more">
              <span>
                Đang xem {visible}/{days.length} ngày
              </span>
              <button
                type="button"
                className="btn secondary small"
                onClick={() => setVisible((current) => current + 7)}
              >
                Xem thêm ngày
              </button>
            </div>
          )}
        </>
      ) : (
        <div className="timesheet-empty">
          {icon("calendar")}
          <strong>Chưa có giờ làm trong khoảng này</strong>
          <p>
            Chọn khoảng ngày khác để xem lịch dự kiến và thời gian tập trung.
          </p>
        </div>
      )}
    </section>
  );
}

function TimesheetTasks({ page, search, setPage, onSearch }) {
  const [draft, setDraft] = useState(search);
  const [result, setResult] = useState(null);
  const [retry, setRetry] = useState(0);
  const path = `/api/intelligence/tasks?page=${page}&pageSize=20&search=${encodeURIComponent(search)}`;
  useEffect(() => {
    let active = true;
    api(path)
      .then((value) => {
        if (
          !Array.isArray(value?.items) ||
          !Number.isFinite(value.total) ||
          !(value.pageSize > 0)
        )
          throw new Error("Chưa tải được danh sách công việc. Hãy thử lại.");
        if (active) setResult({ path, retry, value });
      })
      .catch((error) => {
        if (active) setResult({ path, retry, error: error.message });
      });
    return () => {
      active = false;
    };
  }, [path, retry]);
  const current =
    result?.path === path && result.retry === retry ? result : null;
  const tasks = current?.value;
  return (
    <section
      className="timesheet-card timesheet-tasks"
      aria-label="Thời lượng theo công việc"
    >
      <header className="timesheet-detail-heading">
        <div>
          <h3>Thời lượng theo công việc</h3>
          <p>
            Tổng thời lượng từ trước đến nay của từng việc, gồm cả số nhập tay.
          </p>
        </div>
        <form
          className="timesheet-search"
          onSubmit={(event) => {
            event.preventDefault();
            onSearch(draft.trim());
          }}
        >
          <label className="timesheet-search-field">
            {icon("search")}
            <input
              type="search"
              aria-label="Tìm công việc"
              placeholder="Tìm theo tên công việc…"
              value={draft}
              maxLength={100}
              onChange={(event) => setDraft(event.target.value)}
            />
          </label>
          <button type="submit" className="btn secondary">
            Tìm
          </button>
          {search && (
            <button
              type="button"
              className="btn ghost"
              onClick={() => {
                setDraft("");
                onSearch("");
              }}
            >
              Xóa tìm kiếm
            </button>
          )}
        </form>
      </header>
      {current?.error ? (
        <AnalyticsError
          message={current.error}
          onRetry={() => setRetry((count) => count + 1)}
        />
      ) : !tasks ? (
        <p className="timesheet-feedback" role="status">
          Đang tải công việc…
        </p>
      ) : tasks.items.length === 0 ? (
        <div className="timesheet-empty">
          {icon("search")}
          <strong>Chưa có công việc phù hợp.</strong>
        </div>
      ) : (
        <>
          <ul className="timesheet-task-list">
            {tasks.items.map((task) => (
              <li key={task.id}>
                <button
                  className="timesheet-text-link"
                  type="button"
                  data-action="task-detail"
                  data-id={task.id}
                >
                  <span>{task.name}</span>
                  {icon("arrow")}
                </button>
                <dl className="timesheet-task-meta">
                  <div>
                    <dt>Ước lượng</dt>
                    <dd>{minutes(task.estimatedMinutes, "long")}</dd>
                  </div>
                  <div>
                    <dt>Đã ghi nhận</dt>
                    <dd>{longDuration(task.recordedActualSeconds)}</dd>
                  </div>
                  <div>
                    <dt>Tiến độ</dt>
                    <dd className="timesheet-progress">
                      <progress
                        max="100"
                        value={task.progress}
                        aria-label={`Tiến độ ${task.name}`}
                      />
                      <span>{task.progress}%</span>
                    </dd>
                  </div>
                </dl>
              </li>
            ))}
          </ul>
          {tasks.total > tasks.pageSize && (
            <nav className="timesheet-more" aria-label="Phân trang thống kê">
              <button
                className="btn secondary small"
                type="button"
                disabled={page === 1}
                onClick={() => setPage(page - 1)}
              >
                Trước
              </button>
              <span>
                Trang {page}/{Math.ceil(tasks.total / tasks.pageSize)} ·{" "}
                {tasks.total} việc
              </span>
              <button
                className="btn secondary small"
                type="button"
                disabled={page * tasks.pageSize >= tasks.total}
                onClick={() => setPage(page + 1)}
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

export function AnalyticsHistory() {
  const [view, setView] = useState("days");
  const [days, setDays] = useState("7");
  const [custom, setCustom] = useState(false);
  const [range, setRange] = useState(null);
  const [result, setResult] = useState(null);
  const [retry, setRetry] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const query = range
    ? `start=${range.start}&end=${range.end}`
    : `days=${days}`;
  useEffect(() => {
    let active = true;
    api(`/api/intelligence/history?${query}`)
      .then((value) => {
        if (
          !Array.isArray(value?.days) ||
          !value.current ||
          !Number.isFinite(Date.parse(value.start)) ||
          !Number.isFinite(Date.parse(value.end)) ||
          value.start > value.end
        )
          throw new Error("Chưa tải được thống kê hợp lệ. Hãy thử lại.");
        if (active) setResult({ query, retry, value });
      })
      .catch((error) => {
        if (active) setResult({ query, retry, error: error.message });
      });
    return () => {
      active = false;
    };
  }, [query, retry]);
  const current =
    result?.query === query && result.retry === retry ? result : null;
  const data = current?.value;

  return (
    <section className="timesheet" aria-label="Thống kê lịch sử">
      <header className="timesheet-card timesheet-header">
        <div className="timesheet-heading">
          <span className="timesheet-icon">{icon("clock")}</span>
          <div>
            <h2>Giờ làm việc cá nhân</h2>
            <p>So sánh thời lượng dự kiến với thời gian đã làm.</p>
          </div>
        </div>
        <div
          className="timesheet-segment"
          role="group"
          aria-label="Xem giờ làm theo"
        >
          <button
            type="button"
            aria-pressed={view === "days"}
            onClick={() => setView("days")}
          >
            Theo ngày
          </button>
          <button
            type="button"
            aria-pressed={view === "tasks"}
            onClick={() => setView("tasks")}
          >
            Theo công việc
          </button>
        </div>
      </header>

      {view === "days" ? (
        <>
          <section
            className="timesheet-card timesheet-period-card"
            aria-label="Khoảng ngày báo cáo"
          >
            <div className="timesheet-period-row">
              <div
                className="timesheet-segment"
                role="group"
                aria-label="Khoảng thống kê"
              >
                {periods.map(([key, label]) => (
                  <button
                    key={key}
                    type="button"
                    aria-pressed={!range && !custom && days === key}
                    onClick={() => {
                      setDays(key);
                      setRange(null);
                      setCustom(false);
                    }}
                  >
                    {label}
                  </button>
                ))}
                <button
                  type="button"
                  aria-expanded={custom}
                  aria-controls="timesheet-custom-range"
                  onClick={() => setCustom((open) => !open)}
                >
                  {icon("calendar")} Chọn ngày
                </button>
              </div>
              {data && (
                <span className="timesheet-date-label">
                  {rangeFormat.formatRange(
                    new Date(data.start),
                    new Date(data.end),
                  )}
                </span>
              )}
            </div>
            <div id="timesheet-custom-range" hidden={!custom}>
              {custom && (
                <DateRangeForm
                  range={range ?? data}
                  onApply={(value) => {
                    setRange(value);
                    setCustom(false);
                  }}
                />
              )}
            </div>
          </section>
          {current?.error ? (
            <AnalyticsError
              message={current.error}
              onRetry={() => setRetry((count) => count + 1)}
            />
          ) : !data ? (
            <p className="timesheet-feedback" role="status">
              Đang tải giờ làm…
            </p>
          ) : (
            <>
              <TimesheetTotals data={data} />
              <TimesheetDays key={query} data={data} />
            </>
          )}
        </>
      ) : (
        <TimesheetTasks
          page={page}
          search={search}
          setPage={setPage}
          onSearch={(value) => {
            setSearch(value);
            setPage(1);
          }}
        />
      )}
    </section>
  );
}
