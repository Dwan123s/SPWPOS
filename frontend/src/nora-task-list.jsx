import { useState } from "react";
import { icon, minutes } from "./ui.jsx";
import { calendarLocal, displayDate } from "./nora-schedule.js";

export function NoraTaskList({
  workspace,
  now,
  busy,
  loading,
  error,
  draftBlocked,
  onRefresh,
  onOpenTask,
  onSuggest,
}) {
  const [query, setQuery] = useState("");
  const [unscheduled, setUnscheduled] = useState(true);
  const tasks = workspace?.tasks ?? [];
  const parentIds = new Set(tasks.map((task) => task.parentId).filter(Boolean));
  const byId = new Map(tasks.map((task) => [task.id, task]));
  const scheduled = new Set(
    (workspace?.sessions ?? [])
      .filter(
        (session) =>
          ["Scheduled", "Running", "Paused"].includes(session.status) &&
          Date.parse(session.end) > now,
      )
      .map((session) => session.taskId),
  );
  const zone = workspace?.preference?.timeZoneId || "Asia/Ho_Chi_Minh";
  const search = query.trim().toLocaleLowerCase("vi");
  const visible = tasks
    .filter(
      (task) =>
        !task.isTeamTask &&
        !parentIds.has(task.id) &&
        !["Completed", "Cancelled"].includes(task.status) &&
        (!unscheduled || !scheduled.has(task.id)) &&
        [task.name, byId.get(task.parentId)?.name].some((name) =>
          name?.toLocaleLowerCase("vi").includes(search),
        ),
    )
    .sort(
      (a, b) =>
        (Date.parse(a.deadline) || Infinity) -
          (Date.parse(b.deadline) || Infinity) ||
        b.priority - a.priority ||
        a.name.localeCompare(b.name, "vi"),
    );

  return (
    <section
      className="nora-task-list"
      aria-label="Công việc cá nhân"
      aria-busy={loading}
    >
      <div className="nora-task-controls">
        <input
          type="search"
          aria-label="Tìm công việc"
          placeholder="Tìm công việc…"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
        />
        <button
          type="button"
          aria-label="Cập nhật công việc"
          title="Cập nhật công việc"
          disabled={busy || loading}
          onClick={onRefresh}
        >
          {icon("refresh")}
        </button>
      </div>
      <label className="nora-task-filter">
        <input
          type="checkbox"
          checked={unscheduled}
          onChange={(event) => setUnscheduled(event.target.checked)}
        />
        Chưa có phiên sắp tới
      </label>
      {error && (
        <p className="nora-day-error" role="alert">
          Chưa cập nhật được công việc. {error}
        </p>
      )}
      {draftBlocked && (
        <p className="nora-result-hint">
          Gửi hoặc xóa nội dung và ảnh đang soạn trước khi tạo yêu cầu xếp lịch.
        </p>
      )}
      {!visible.length && (
        <p className="nora-day-empty">
          {loading
            ? "Đang tải công việc…"
            : query.trim()
              ? "Không tìm thấy công việc phù hợp."
              : "Không có công việc trong danh sách này."}
        </p>
      )}
      <ul>
        {visible.map((task) => {
          const parent = byId.get(task.parentId);
          const deadline =
            task.deadline &&
            displayDate(calendarLocal(task.deadline, zone).slice(0, 10));
          const hasSession = scheduled.has(task.id);
          return (
            <li key={task.id}>
              <div className="nora-task-summary">
                <button
                  className="nora-task-name"
                  type="button"
                  onClick={() => onOpenTask(task.id)}
                  disabled={loading || busy || !!error}
                >
                  {task.name}
                </button>
                {parent && <small>{parent.name}</small>}
                <small>
                  {deadline ? `Hạn ${deadline}` : "Chưa có hạn"} ·{" "}
                  {task.estimatedMinutes > 0
                    ? `${minutes(task.remainingMinutes ?? task.estimatedMinutes)} còn lại`
                    : "Chưa ước lượng"}
                </small>
                {hasSession && <small>Đã có phiên sắp tới</small>}
              </div>
              {!hasSession && (
                <button
                  className="nora-task-plan"
                  type="button"
                  aria-label={`Soạn yêu cầu xếp lịch cho ${task.name}`}
                  disabled={busy || loading || !!error || draftBlocked}
                  onClick={() =>
                    onSuggest(
                      `Tìm giờ làm cho công việc đã lưu ${JSON.stringify(task.name)} (ID: ${task.id}). Chỉ xem trước lịch cho việc này, giữ nguyên các mục lịch khác. Không tạo công việc mới.`,
                    )
                  }
                >
                  Xếp lịch
                </button>
              )}
            </li>
          );
        })}
      </ul>
      <a className="nora-open-calendar" href="#tasks">
        Mở trang công việc {icon("arrow")}
      </a>
    </section>
  );
}
