import { useEffect, useState } from "react";
import { api } from "./api.js";
import {
  button,
  closeModal,
  dateFmt,
  minutes,
  modal,
  state,
  timeFmt,
} from "./ui.jsx";
import { planModal } from "./calendar.jsx";
import "./workload.css";

function useWorkload() {
  const [value, setValue] = useState(null);
  const [error, setError] = useState("");
  const account = state.workspace?.profile?.id;
  useEffect(() => {
    let alive = true,
      request = 0,
      timer;
    async function refresh() {
      const current = ++request;
      clearTimeout(timer);
      try {
        const data = await api("/api/intelligence/workload");
        if (!alive || current !== request) return;
        if (
          !data?.next ||
          !Array.isArray(data.days) ||
          !Array.isArray(data.tasks)
        )
          throw new Error("Chưa tải được dữ liệu khối lượng công việc.");
        setValue(data);
        setError("");
        const boundary = Date.parse(data.refreshAt);
        if (Number.isFinite(boundary))
          timer = setTimeout(
            refresh,
            Math.max(1000, boundary - Date.now() + 100),
          );
      } catch (failure) {
        if (alive && current === request) setError(failure.message);
      }
    }
    const onVisible = () => {
      if (!document.hidden) refresh();
    };
    refresh();
    window.addEventListener("workspace-updated", refresh);
    document.addEventListener("visibilitychange", onVisible);
    return () => {
      alive = false;
      clearTimeout(timer);
      window.removeEventListener("workspace-updated", refresh);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [account]);
  return { value, error };
}

const statusText = {
  PAST: "Đã qua",
  UNCONFIGURED: "Chưa thiết lập",
  SHORTFALL: "Thiếu giờ trước hạn",
  NO_HOURS: "Không có giờ làm",
  AVAILABLE: "Còn chỗ",
  FULL: "Đã đầy",
};
const timeText = (value) => (value == null ? "—" : minutes(value));
const windowText = (row) =>
  `${dateFmt(row.start)} · ${timeFmt(row.start)}–${timeFmt(row.end)}`;
async function previewRecovery(taskId) {
  const plan = await api("/api/plans/reschedule", "POST", {
    start: new Date().toISOString(),
    horizonDays: 30,
    affectedTaskIds: [taskId],
    triggerEvent: "SESSION_MISSED",
    useCapacityFactor: false,
    useEstimateBias: false,
    strict: true,
    preserveExisting: true,
  });
  planModal(plan);
}

function OutcomeForm({ session, onChanged }) {
  const [outcome, setOutcome] = useState("PARTIAL");
  const timerSeconds = session.timerSeconds ?? session.actualSeconds ?? 0;
  const [useTimer, setUseTimer] = useState(timerSeconds > 0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState(null);
  async function submit(event) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setBusy(true);
    setError("");
    try {
      const saved = await api(`/api/sessions/${session.id}/outcome`, "POST", {
        outcome,
        actualMinutes:
          outcome === "MISSED" || useTimer ? null : Number(form.get("actual")),
        remainingMinutes:
          outcome === "PARTIAL" ? Number(form.get("remaining")) : null,
      });
      setResult(saved);
      window.dispatchEvent(new Event("workspace-updated"));
      await onChanged?.();
    } catch (failure) {
      setError(failure.message);
      if (failure.status === 409)
        window.dispatchEvent(new Event("workspace-updated"));
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      {error && (
        <p className="warning" role="alert">
          {error}
        </p>
      )}
      {result ? (
        <>
          <p role="status">
            Đã ghi nhận {minutes(result.actualSeconds / 60)}. Lịch chưa thay
            đổi.
          </p>
          {result.remainingMinutes != null && (
            <p>Còn {minutes(result.remainingMinutes)} công việc.</p>
          )}
          <button
            className="btn primary"
            type="button"
            onClick={async () => {
              try {
                await previewRecovery(result.taskId);
              } catch (failure) {
                setError(failure.message);
              }
            }}
          >
            Xem phương án
          </button>
          {result.outcome === "COMPLETED" &&
            !(session.teamTask || session.isTeamTask) && (
              <button
                className="btn secondary"
                disabled={busy}
                onClick={async () => {
                  setBusy(true);
                  try {
                    await api(
                      `/api/sessions/${session.id}/finish-task`,
                      "POST",
                    );
                    closeModal();
                    await onChanged?.();
                  } catch (failure) {
                    setError(failure.message);
                  } finally {
                    setBusy(false);
                  }
                }}
              >
                Hoàn thành công việc
              </button>
            )}
          <button className="btn ghost" type="button" onClick={closeModal}>
            Đóng
          </button>
        </>
      ) : (
        <form className="form-grid" onSubmit={submit}>
          <label>
            Kết quả phiên{" "}
            <select
              value={outcome}
              onChange={(e) => setOutcome(e.target.value)}
            >
              <option value="COMPLETED">Xong phiên</option>
              <option value="PARTIAL">Làm một phần</option>
              <option value="MISSED" disabled={timerSeconds > 0}>
                Chưa làm
              </option>
            </select>
          </label>
          {outcome !== "MISSED" && timerSeconds > 0 && (
            <label>
              <input
                type="checkbox"
                checked={useTimer}
                onChange={(e) => setUseTimer(e.target.checked)}
              />{" "}
              Dùng thời gian bộ đếm: {minutes(timerSeconds / 60)}
            </label>
          )}
          {outcome !== "MISSED" && !useTimer && (
            <label>
              Phút thực làm{" "}
              <input required name="actual" type="number" min="0" max="10080" />
            </label>
          )}
          {outcome === "PARTIAL" && (
            <label>
              Tổng số phút còn cần, gồm cả các phiên đã đặt
              <input
                required
                name="remaining"
                type="number"
                min="1"
                max="43200"
              />
            </label>
          )}
          <div className="form-actions">
            <button className="btn primary" disabled={busy}>
              Lưu kết quả
            </button>
          </div>
        </form>
      )}
    </>
  );
}

export function openSessionOutcome(session, onChanged) {
  modal(
    "Ghi kết quả phiên",
    session.name || session.taskName,
    <OutcomeForm key={session.id} session={session} onChanged={onChanged} />,
  );
}

export function TodayWorkload({ onWorkspaceChanged }) {
  const { value, error } = useWorkload();
  const [dismissed, setDismissed] = useState(false);
  if (!value)
    return error ? <p role="status">Chưa tải được gợi ý hôm nay.</p> : null;
  const next = value.next;
  if (next.kind === "SETUP" && dismissed) return null;
  const session =
    value.days
      .flatMap((day) => day.sessions)
      .find((row) => row.id === next.sessionId) ||
    state.workspace.sessions.find((row) => row.id === next.sessionId);
  return (
    <section className="card workload-next" aria-label="Bước tiếp theo">
      <h2>
        {next.kind === "SETUP"
          ? "Bắt đầu với PLANORA"
          : next.kind === "RISK"
            ? "Kế hoạch cần xử lý"
            : "Việc tiếp theo"}
      </h2>
      <p>{next.reason}</p>
      <div className="form-actions">
        {next.kind === "SETUP" && (
          <>
            {button("Thiết lập giờ làm", "nav", "primary", "time")}
            {button("Ghi việc trước", "capture", "secondary")}
            <button className="btn ghost" onClick={() => setDismissed(true)}>
              Bỏ qua
            </button>
          </>
        )}
        {next.kind === "CAPTURE" &&
          button("Ghi việc trước", "capture", "primary")}
        {next.kind === "PLAN" &&
          button("Xem lịch đề xuất", "generate-plan", "primary")}
        {next.kind === "OPEN_CALENDAR" &&
          button("Mở lịch", "nav", "primary", "calendar")}
        {next.kind === "RISK" &&
          button("Xem công việc", "task-detail", "primary", next.taskId)}
        {next.kind === "SESSION" &&
          (session?.status === "Scheduled"
            ? button(
                "Bắt đầu phiên",
                "session-start",
                "primary",
                next.sessionId,
              )
            : session?.status === "Paused"
              ? button(
                  "Tiếp tục phiên",
                  "session-resume",
                  "primary",
                  next.sessionId,
                )
              : button(
                  "Mở phiên làm việc",
                  "session-detail",
                  "primary",
                  next.sessionId,
                ))}
        {next.kind === "REVIEW" && session && (
          <button
            className="btn primary"
            onClick={() => openSessionOutcome(session, onWorkspaceChanged)}
          >
            Ghi kết quả
          </button>
        )}
      </div>
    </section>
  );
}

export function WorkloadWeek({ onWorkspaceChanged }) {
  const { value, error } = useWorkload();
  if (!value)
    return (
      <section className="card" aria-label="Tải công việc">
        <p role="status">{error || "Đang tải công việc…"}</p>
      </section>
    );
  return (
    <section className="card workload-week" aria-label="Tải công việc">
      <h2>Tải công việc trong tuần</h2>
      <p>
        {value.start} – {value.end} · Hoàn thành {value.completedTasks}/
        {value.tasks.length} công việc
      </p>
      {error && (
        <p className="warning" role="status">
          Số liệu chưa được làm mới: {error}
        </p>
      )}
      <p>{value.next.reason}</p>
      <div className="workload-grid">
        {value.days.map((day) => (
          <article key={day.date} className="workload-day">
            <div className="workload-capacity">
              <header>
                <strong>
                  {day.date.slice(5).split("-").reverse().join("/")}
                </strong>
                <br />
                {statusText[day.status]}
              </header>
              {day.status !== "PAST" &&
                day.workingMinutes > 0 &&
                day.reservedMinutes != null && (
                  <progress
                    aria-label="Giờ đã giữ chỗ"
                    max={day.workingMinutes}
                    value={day.reservedMinutes}
                  />
                )}
              {day.advice && <p>{day.advice}</p>}
              {day.status === "PAST" ? (
                <dl>
                  <dt>Dự kiến</dt>
                  <dd>{timeText(day.plannedSeconds / 60)}</dd>
                  <dt>Thực làm</dt>
                  <dd>
                    {timeText(
                      day.recordedActualSeconds == null
                        ? null
                        : day.recordedActualSeconds / 60,
                    )}
                  </dd>
                </dl>
              ) : (
                <dl>
                  <dt>Giờ làm</dt>
                  <dd>{timeText(day.workingMinutes)}</dd>
                  <dt>Đã giữ chỗ</dt>
                  <dd>{timeText(day.reservedMinutes)}</dd>
                  <dt>Còn xếp thêm</dt>
                  <dd>{timeText(day.schedulableMinutes)}</dd>
                </dl>
              )}
            </div>
            <details>
              <summary>Phiên làm việc ({day.sessions.length})</summary>
              <p>
                Dự kiến {timeText(day.plannedSeconds / 60)} · Thực làm{" "}
                {timeText(
                  day.recordedActualSeconds == null
                    ? null
                    : day.recordedActualSeconds / 60,
                )}
              </p>
              {day.sessions.map((session) => (
                <div key={session.id} className="workload-entry">
                  <strong>{session.name}</strong>
                  <p>
                    {windowText(session)} · Thực làm{" "}
                    {timeText(
                      session.actualSeconds == null
                        ? null
                        : session.actualSeconds / 60,
                    )}
                  </p>
                  {session.outcome && (
                    <p>
                      {
                        {
                          COMPLETED: "Xong phiên",
                          PARTIAL: "Làm một phần",
                          MISSED: "Chưa làm",
                        }[session.outcome]
                      }
                    </p>
                  )}
                  {session.needsOutcome ? (
                    <button
                      className="btn secondary small"
                      onClick={() =>
                        openSessionOutcome(session, onWorkspaceChanged)
                      }
                    >
                      Ghi kết quả
                    </button>
                  ) : (
                    button(
                      "Mở phiên",
                      "session-detail",
                      "ghost small",
                      session.id,
                    )
                  )}
                </div>
              ))}
            </details>
          </article>
        ))}
      </div>
      <details>
        <summary>Khối lượng theo công việc</summary>
        <ul>
          {value.tasks.map((task) => (
            <li key={task.id}>
              <strong>{task.name}</strong> · Dự kiến {minutes(task.estimate)} ·
              Còn {minutes(task.remainingMinutes)} · Chưa xếp{" "}
              {minutes(task.unscheduledMinutes)}
              {task.shortfallMinutes > 0 && (
                <> · Thiếu trước hạn {minutes(task.shortfallMinutes)}</>
              )}
              {task.shortfallMinutes == null && task.remainingMinutes > 0 && (
                <>
                  {" "}
                  ·{" "}
                  {task.deadlineAssessment === "OUTSIDE_HORIZON"
                    ? "Ngoài phạm vi tính"
                    : "Chưa đủ dữ liệu để đánh giá trước hạn"}
                </>
              )}
              {task.checklistTotal > 0 && (
                <>
                  {" "}
                  · Checklist {task.checklistDone}/{task.checklistTotal}
                </>
              )}
              {task.overdue && <> · Quá hạn</>}
              {task.teamStatus === "PendingReview" && (
                <> · Chờ trưởng nhóm duyệt</>
              )}
              {task.status === "Completed" &&
                task.teamStatus !== "PendingReview" && (
                  <> · Hoàn thành công việc</>
                )}
            </li>
          ))}
        </ul>
      </details>
    </section>
  );
}

export function EventConflictReview({ input, onSaved, onBack }) {
  const [preview, setPreview] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function run(confirm) {
    setBusy(true);
    setError("");
    try {
      if (!confirm)
        setPreview(await api("/api/time/events/replan/preview", "POST", input));
      else {
        await api(`/api/time/events/replan/${preview.id}/confirm`, "POST");
        await onSaved();
      }
    } catch (failure) {
      if (confirm && failure.code === "PLAN_STALE") {
        setPreview(null);
        window.dispatchEvent(new Event("workspace-updated"));
      }
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }
  const name = (id) =>
    state.workspace.tasks.find((t) => t.id === id)?.name || "Công việc";
  return (
    <>
      {error && (
        <p role="alert" className="warning">
          {error}
        </p>
      )}
      {preview && (
        <>
          <p>
            <strong>{input.name}</strong> · {windowText(preview.event)}
          </p>
          <p>
            Giờ cũ:{" "}
            {preview.before
              .map((row) => `${name(row.taskId)} · ${windowText(row)}`)
              .join("; ") || "Không có phiên bị dời"}
          </p>
          <p>
            Giờ đề xuất:{" "}
            {preview.after
              .map((row) => `${name(row.taskId)} · ${windowText(row)}`)
              .join("; ") || "—"}
          </p>
          {preview.warnings.map((warning, index) => (
            <p className="warning" key={index}>
              {warning.taskName}: {warning.message}
            </p>
          ))}
          {!preview.id && (
            <p>Chưa có phương án khả thi. Lịch hiện tại chưa thay đổi.</p>
          )}
        </>
      )}
      <div className="form-actions">
        {!preview && (
          <button
            className="btn primary"
            disabled={busy}
            onClick={() => run(false)}
          >
            Giữ giờ sự kiện và xem lịch đề xuất
          </button>
        )}
        {preview?.id && (
          <button
            className="btn primary"
            disabled={busy}
            onClick={() => run(true)}
          >
            Xác nhận sự kiện và lịch mới
          </button>
        )}
        <button className="btn ghost" disabled={busy} onClick={onBack}>
          Sửa sự kiện
        </button>
      </div>
    </>
  );
}
