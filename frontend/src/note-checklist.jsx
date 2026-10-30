import { useEffect, useId, useRef, useState } from "react";
import { dateFmt, icon, minutes } from "./ui.jsx";

export function NoteChecklist({
  note,
  workspace,
  busy,
  onComplete,
  onRefresh,
}) {
  const [pending, setPending] = useState(null);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const sending = useRef(false);
  const alive = useRef(true);
  const completedHeading = useRef(null);
  const inputs = useRef(new Map());
  const headingId = useId();
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  const ids = new Set(note.createdTaskIds || []);
  if (!ids.size) return null;
  const tasks = (workspace?.tasks || []).filter((task) => ids.has(task.id));
  const active = tasks.filter(
    (task) => !["Completed", "Cancelled"].includes(task.status),
  );
  const completed = tasks.filter((task) => task.status === "Completed");
  const cancelled = tasks.filter((task) => task.status === "Cancelled");
  const total = active.length + completed.length;
  const disabled = !!busy || !!pending;

  async function toggle(task) {
    if (
      disabled ||
      sending.current ||
      !task.version ||
      task.status === "Cancelled" ||
      (workspace?.sessions || []).some(
        (session) =>
          session.taskId === task.id &&
          ["Running", "Paused"].includes(session.status),
      )
    )
      return;
    sending.current = true;
    setPending(task.id);
    setError("");
    setNotice("");
    const completing = task.status !== "Completed";
    try {
      await onComplete(task, completing);
      if (!alive.current) return;
      setNotice(
        completing
          ? `Đã hoàn thành “${task.name}”.`
          : `Đã mở lại “${task.name}”. Chọn Xếp lịch để dành thời gian làm tiếp.`,
      );
      requestAnimationFrame(() => {
        if (alive.current)
          (completing
            ? completedHeading.current
            : inputs.current.get(task.id)
          )?.focus();
      });
    } catch (failure) {
      if (alive.current) setError(failure.message);
    } finally {
      sending.current = false;
      if (alive.current) setPending(null);
    }
  }
  async function refresh() {
    if (disabled || sending.current) return;
    sending.current = true;
    setPending("refresh");
    try {
      await onRefresh();
      if (alive.current) {
        setError("");
        setNotice("Đã cập nhật danh sách công việc.");
      }
    } catch (failure) {
      if (alive.current) setError(failure.message);
    } finally {
      sending.current = false;
      if (alive.current) setPending(null);
    }
  }
  function row(task) {
    const done = task.status === "Completed";
    const cancelled = task.status === "Cancelled";
    const sessions = (workspace?.sessions || []).filter(
      (session) => session.taskId === task.id,
    );
    const running = sessions.find((session) =>
      ["Running", "Paused"].includes(session.status),
    );
    const planned = sessions
      .filter((session) =>
        ["Scheduled", "Running", "Paused"].includes(session.status),
      )
      .sort((a, b) => new Date(a.start) - new Date(b.start));
    const overdue = !done && !cancelled && new Date(task.deadline) < new Date();
    return (
      <li
        key={task.id}
        className={`note-check-row${done ? " is-done" : ""}${cancelled ? " is-cancelled" : ""}`}
        aria-busy={pending === task.id}
      >
        <label className="note-check-hit">
          <input
            type="checkbox"
            checked={done}
            ref={(element) => {
              if (element) inputs.current.set(task.id, element);
              else inputs.current.delete(task.id);
            }}
            aria-label={`${done ? "Mở lại" : "Hoàn thành"} ${task.name}`}
            aria-describedby={running ? `note-active-${task.id}` : undefined}
            disabled={disabled || !!running || cancelled || !task.version}
            onChange={() => toggle(task)}
          />
        </label>
        <div className="note-check-content">
          <details>
            <summary>
              <span className="note-check-name">{task.name}</span>
              <span
                className={`note-check-meta${overdue ? " is-overdue" : ""}`}
              >
                {cancelled
                  ? "Đã hủy"
                  : done
                    ? "Đã hoàn thành"
                    : `${overdue ? "Quá hạn · " : "Hạn "}${dateFmt(task.deadline, { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" })}`}
              </span>
            </summary>
            <div className="note-check-details">
              {task.description && (
                <p className="note-check-description">{task.description}</p>
              )}
              <p>
                Dự kiến {minutes(task.estimatedMinutes)} · Đã làm{" "}
                {minutes(task.actualMinutes || 0)} · Tiến độ{" "}
                {task.progress || 0}%
              </p>
              {planned.length > 0 ? (
                <ul aria-label={`Lịch của ${task.name}`}>
                  {planned.map((session) => (
                    <li key={session.id}>
                      {dateFmt(session.start, {
                        day: "2-digit",
                        month: "2-digit",
                        hour: "2-digit",
                        minute: "2-digit",
                      })}
                      {" – "}
                      {dateFmt(session.end, {
                        hour: "2-digit",
                        minute: "2-digit",
                      })}
                    </li>
                  ))}
                </ul>
              ) : (
                !done && !cancelled && <p>Chưa có phiên sắp tới.</p>
              )}
              <div className="actions">
                <button
                  type="button"
                  className="btn ghost small"
                  data-action="edit-task"
                  data-id={task.id}
                  disabled={disabled}
                >
                  {icon("edit")} Chỉnh sửa công việc
                </button>
                {!done && !cancelled && !running && (
                  <button
                    type="button"
                    className="btn secondary small"
                    data-action="schedule-task"
                    data-id={task.id}
                    disabled={disabled}
                  >
                    {icon("calendar")} Xếp lịch
                  </button>
                )}
              </div>
            </div>
          </details>
          {running && (
            <p id={`note-active-${task.id}`} className="note-check-running">
              Kết thúc phiên đang thực hiện trước khi đánh dấu hoàn thành.{" "}
              <button
                type="button"
                className="note-check-link"
                data-action="session-detail"
                data-id={running.id}
                disabled={disabled}
              >
                Mở phiên
              </button>
            </p>
          )}
          {!done &&
            !cancelled &&
            ((task.progress || 0) > 0 || (task.actualMinutes || 0) > 0) && (
              <p className="note-check-progress">
                Đã làm {minutes(task.actualMinutes || 0)} · {task.progress || 0}
                % hoàn thành
              </p>
            )}
          {pending === task.id && (
            <span className="note-check-progress">Đang lưu…</span>
          )}
        </div>
      </li>
    );
  }
  return (
    <section className="note-checklist" aria-labelledby={headingId}>
      <div className="note-checklist-heading">
        <h2 id={headingId}>Việc trong ghi chú</h2>
        <span>
          {completed.length}/{total} đã xong
        </span>
      </div>
      {!!total && (
        <progress
          value={completed.length}
          max={total}
          aria-label="Tiến độ checklist"
        />
      )}
      {active.length > 0 ? (
        <ul className="note-check-items" aria-label="Việc chưa hoàn thành">
          {active.map(row)}
        </ul>
      ) : (
        total > 0 && (
          <p className="note-check-empty">
            Bạn đã hoàn thành tất cả công việc trong ghi chú này.
          </p>
        )
      )}
      {completed.length > 0 && (
        <details className="note-check-completed">
          <summary ref={completedHeading}>
            Đã hoàn thành · {completed.length}
          </summary>
          <ul className="note-check-items" aria-label="Việc đã hoàn thành">
            {completed.map(row)}
          </ul>
        </details>
      )}
      {cancelled.length > 0 && (
        <details className="note-check-completed">
          <summary>Đã hủy · {cancelled.length}</summary>
          <ul className="note-check-items" aria-label="Việc đã hủy">
            {cancelled.map(row)}
          </ul>
        </details>
      )}
      {tasks.length < ids.size && (
        <p className="note-check-empty">
          Một số việc liên kết đã được xóa hoặc chưa tải được. Bạn có thể tải
          lại danh sách để kiểm tra.
        </p>
      )}
      <p className="note-check-notice" role="status">
        {notice}
      </p>
      {error && (
        <p className="note-check-error" role="alert">
          {error}
        </p>
      )}
      <div className="note-checklist-footer">
        <span>
          Tick khi đã xong cả việc. Trạng thái đồng bộ với Công việc và Lịch.
        </span>
        <button
          type="button"
          className="btn ghost small"
          onClick={refresh}
          disabled={disabled}
        >
          {icon("refresh")} Tải lại danh sách
        </button>
      </div>
    </section>
  );
}
