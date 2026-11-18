import { useEffect, useId, useState, useSyncExternalStore } from "react";
import { api } from "./api.js";
import { EisenhowerPriorityBadge } from "./eisenhower-priority.jsx";
import { CalendarClock } from "./calendar-time.jsx";
import { dateFmt, icon, minutes, state, statusLabels, timeFmt } from "./ui.jsx";
import { sessionStartIssue, sessionStartOpensAt } from "./session-start.jsx";
import { OutcomeForm } from "./workload.jsx";
import {
  hasWorkHours,
  planShortfall,
  rememberTaskPreview,
  replacedSessions,
  subscribeTaskPreviews,
  taskPlanInput,
  taskPlanShortfall,
  taskPreview,
  taskScheduleStatus,
} from "./task-workflow.js";

export function TaskPriorityIndicators({
  task,
  sessions,
  now,
  project = false,
  link = true,
  onOpenPlan,
}) {
  const preview = useSyncExternalStore(
    subscribeTaskPreviews,
    () => taskPreview(task.id, state.workspace, now),
    () => null,
  );
  const deadline = taskScheduleStatus(task, sessions, now, preview);
  return (
    <div className="task-priority-indicators">
      <EisenhowerPriorityBadge task={task} />
      {deadline &&
        (!project || deadline.tone === "late") &&
        (link ? (
          <button
            type="button"
            data-tone={deadline.tone}
            data-action={onOpenPlan ? undefined : "task-planning"}
            onClick={onOpenPlan}
            data-id={task.id}
            aria-label={task.name + " · " + deadline.label}
          >
            {deadline.label}
          </button>
        ) : (
          <span data-tone={deadline.tone}>{deadline.label}</span>
        ))}
    </div>
  );
}

const sessionWindow = (session) =>
  dateFmt(session.start) +
  " · " +
  timeFmt(session.start) +
  "–" +
  timeFmt(session.end);

export function TaskPlannedSessions({ sessions }) {
  if (!sessions.length)
    return <p className="muted">Công việc chưa có phiên đã xếp.</p>;
  return sessions.map((session) => (
    <div className="list-line" key={session.id}>
      <span>{sessionWindow(session)}</span>
      <button
        type="button"
        className="btn small ghost"
        data-action="session-detail"
        data-id={session.id}
      >
        {icon("clock")} {statusLabels[session.status] || session.status}
      </button>
    </div>
  ));
}

export function TaskPlanningPanel({
  task,
  initialPlan,
  initialError,
  onWorkspaceChanged,
}) {
  const [plan, setPlan] = useState(initialPlan);
  const [error, setError] = useState(initialError || "");
  const [busy, setBusy] = useState(false);
  const [applied, setApplied] = useState(Boolean(initialPlan?.isApplied));
  const workspace = state.workspace;
  const outdated = Boolean(
    plan &&
    !applied &&
    workspace.preference?.revision &&
    plan.revision !== workspace.preference.revision,
  );
  const missingDetails = !task.deadline || !task.estimatedMinutes;
  const hasHours = hasWorkHours(workspace);
  const changes =
    plan && !applied ? replacedSessions(plan, workspace.sessions || []) : [];
  const added = plan?.sessions.filter((session) => !session.preserved) || [];
  const showAdded =
    added.length > 0 &&
    (!applied ||
      added.some(
        (session) =>
          !(workspace.sessions || []).some((saved) => saved.id === session.id),
      ));
  const retained =
    plan?.sessions.filter(
      (session) => session.preserved && session.taskId === task.id,
    ) || [];
  const warnings = plan?.warnings || [];
  const taskNames = new Map(
    (workspace.tasks || []).map((item) => [item.id, item.name]),
  );
  const ownShortfall = taskPlanShortfall(task, plan);
  const blocked = plan && (planShortfall(plan) || ownShortfall > 0);
  const active = !["Completed", "Cancelled"].includes(task.status);

  async function preview(preserve) {
    setBusy(true);
    setError("");
    try {
      const input = taskPlanInput(task, preserve);
      // The existing proposal remains available if the new request fails.
      const next = await api("/api/plans", "POST", input);
      rememberTaskPreview(task.id, next, state.workspace);
      setPlan(next);
      setApplied(false);
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  async function apply() {
    setBusy(true);
    setError("");
    try {
      await api("/api/plans/" + plan.id + "/apply", "POST");
      rememberTaskPreview(task.id, null, state.workspace);
      setApplied(true);
      try {
        await onWorkspaceChanged?.();
      } catch {
        setError("Đã lưu lịch. Tải lại để cập nhật các màn hình đang mở.");
      }
    } catch (failure) {
      setError(failure.message);
      if (["PLAN_EXPIRED", "PLAN_STALE"].includes(failure.code)) setPlan(null);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section
      className="task-plan-in-drawer"
      aria-label="Phương án xếp lịch"
      aria-busy={busy}
    >
      {error && (
        <p className="warning" role="alert">
          {error}
        </p>
      )}
      {applied && (
        <p className="info" role="status">
          Đã lưu lịch làm việc.
        </p>
      )}
      {outdated && (
        <p className="warning" role="status">
          Lịch đã thay đổi. Tạo phương án mới trước khi áp dụng.
        </p>
      )}
      {ownShortfall > 0 && !applied && (
        <strong className="task-plan-shortfall">
          Thiếu {ownShortfall} phút{" "}
          {Date.parse(task.deadline) > Date.parse(plan.end)
            ? "trong khoảng xem trước"
            : "trước hạn"}
        </strong>
      )}
      {plan && (
        <>
          {changes.length > 0 && (
            <div className="task-plan-changes">
              <h3>Phiên hiện tại sẽ thay đổi</h3>
              <ul>
                {changes.map((session) => (
                  <li key={session.id}>
                    {session.taskName || taskNames.get(session.taskId)} · Giờ
                    cũ: {sessionWindow(session)}
                  </li>
                ))}
              </ul>
            </div>
          )}
          {showAdded && (
            <div className="task-plan-changes">
              <h3>{applied ? "Phiên đã xếp" : "Phiên đề xuất"}</h3>
              <ul>
                {added.map((session) => (
                  <li key={session.id}>
                    {session.taskId !== task.id &&
                      (taskNames.get(session.taskId) || "Công việc khác") +
                        " · "}
                    {changes.length > 0 && "Giờ mới: "}
                    {sessionWindow(session)}
                  </li>
                ))}
              </ul>
            </div>
          )}
          {!applied && retained.length > 0 && (
            <div className="task-plan-changes">
              <h3>Phiên được giữ</h3>
              <ul>
                {retained.map((session) => (
                  <li key={session.id}>{sessionWindow(session)}</li>
                ))}
              </ul>
            </div>
          )}
          {!applied &&
            warnings.map((warning, index) => (
              <p
                className="reason"
                key={warning.taskId + ":" + warning.code + ":" + index}
              >
                {warning.taskId !== task.id && warning.taskName + " · "}
                {warning.message}
              </p>
            ))}
        </>
      )}
      {active &&
        (missingDetails ? (
          <p className="reason">Thêm hạn chót và thời lượng để xếp lịch.</p>
        ) : !hasHours ? (
          <p className="reason">
            Xác nhận{" "}
            <button
              type="button"
              className="text-btn"
              data-action="task-work-hours"
            >
              giờ làm việc
            </button>{" "}
            để xếp lịch.
          </p>
        ) : (
          <div className="actions">
            {(!plan || outdated || blocked) && (
              <button
                type="button"
                className="btn secondary small"
                onClick={() => preview(true)}
                disabled={busy}
              >
                {plan ? "Tính lại với lịch hiện tại" : "Xem phương án xếp lịch"}
              </button>
            )}
            {blocked && !outdated && !applied && (
              <button
                type="button"
                className="btn secondary small"
                onClick={() => preview(false)}
                disabled={busy}
              >
                Thử sắp lại các phiên liên quan
              </button>
            )}
            {plan && !applied && !blocked && !outdated && added.length > 0 && (
              <button
                type="button"
                className="btn primary small"
                onClick={apply}
                disabled={busy}
              >
                {changes.length || plan.hasUrgentReplan
                  ? "Xác nhận dời lịch"
                  : plan.requiresConfirmation
                    ? "Xác nhận kế hoạch"
                    : "Áp dụng vào Lịch"}
              </button>
            )}
          </div>
        ))}
    </section>
  );
}

const liveStatuses = new Set(["Running", "Paused"]);

function elapsedSeconds(session, now) {
  const since = state.loadedAt || now;
  const extra =
    session.status === "Running"
      ? Math.max(0, Math.min(now, Date.parse(session.end)) - since)
      : 0;
  return (session.actualSeconds || 0) + Math.floor(extra / 1000);
}

function SessionElapsed({ session }) {
  const [now, setNow] = useState(Date.now);
  useEffect(() => {
    if (session.status !== "Running") return;
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, [session.status]);
  const seconds = elapsedSeconds(session, now);
  return (
    <span>
      {String(Math.floor(seconds / 60)).padStart(2, "0")}:
      {String(seconds % 60).padStart(2, "0")}
    </span>
  );
}

export function TaskSessionControls({
  task,
  onWorkspaceChanged,
  selectedSessionId,
}) {
  const issueId = useId();
  const [logging, setLogging] = useState(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [updated, setUpdated] = useState(null);
  const sessions = (state.workspace.sessions || []).filter(
    (session) => session.taskId === task.id,
  );
  const selected = sessions.find((session) => session.id === selectedSessionId);
  const live = sessions.find((session) => liveStatuses.has(session.status));
  const boundaries = sessions.flatMap((session) => [
    sessionStartOpensAt(session),
    Date.parse(session.end),
  ]);

  async function control(session, action) {
    setBusy(true);
    setError("");
    setUpdated("");
    try {
      await api("/api/sessions/" + session.id + "/" + action, "POST");
      await onWorkspaceChanged?.();
      setUpdated(action === "pause" ? "Đã tạm dừng phiên." : "Đang làm việc.");
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="task-execution" aria-label="Phiên làm việc">
      <h3>Làm việc và ghi giờ</h3>
      {error && (
        <p className="warning" role="alert">
          {error}
        </p>
      )}
      {updated && (
        <span className="sr-only" role="status">
          {updated}
        </span>
      )}
      {logging ? (
        <OutcomeForm
          key={logging.id}
          session={{ ...logging, isTeamTask: task.isTeamTask }}
          endNow={liveStatuses.has(logging.status)}
          onChanged={onWorkspaceChanged}
          onClose={() => setLogging(null)}
        />
      ) : (
        <CalendarClock boundaries={boundaries}>
          {(now) => {
            const next = sessions
              .filter(
                (session) =>
                  session.status === "Scheduled" &&
                  Date.parse(session.end) > now,
              )
              .sort((a, b) => Date.parse(a.start) - Date.parse(b.start))[0];
            const review = sessions.find(
              (session) => session.status === "NeedsReview",
            );
            const session = selected || live || next || review;
            if (!session)
              return (
                <p className="reason">Chưa có phiên làm việc để ghi giờ.</p>
              );
            const status = session.status;
            const issue =
              status === "Scheduled"
                ? sessionStartIssue(session, state.workspace, now)
                : null;
            const canLog =
              liveStatuses.has(status) ||
              status === "NeedsReview" ||
              (status === "Scheduled" && Date.parse(session.end) <= now);
            return (
              <>
                <p>
                  {sessionWindow(session)}
                  {liveStatuses.has(status) && (
                    <>
                      {" "}
                      · <SessionElapsed session={session} />
                    </>
                  )}
                </p>
                <dl className="task-execution-facts">
                  <div>
                    <dt>Thời lượng phiên</dt>
                    <dd>
                      {minutes(
                        Math.round(
                          (Date.parse(session.end) -
                            Date.parse(session.start)) /
                            60000,
                        ),
                      )}
                    </dd>
                  </div>
                  <div>
                    <dt>Công việc còn lại</dt>
                    <dd>{minutes(task.remainingMinutes)}</dd>
                  </div>
                </dl>
                {issue && (
                  <p className="reason" id={issueId} role="status">
                    {issue}
                  </p>
                )}
                <div className="actions">
                  {status === "Scheduled" && !canLog && (
                    <button
                      type="button"
                      className="btn primary small"
                      disabled={busy || Boolean(issue)}
                      aria-describedby={issue ? issueId : undefined}
                      onClick={() => control(session, "start")}
                    >
                      Bắt đầu
                    </button>
                  )}
                  {liveStatuses.has(status) && (
                    <button
                      type="button"
                      className="btn secondary small"
                      disabled={busy}
                      onClick={() =>
                        control(
                          session,
                          status === "Running" ? "pause" : "resume",
                        )
                      }
                    >
                      {status === "Running" ? "Tạm dừng" : "Tiếp tục"}
                    </button>
                  )}
                  {canLog && (
                    <button
                      type="button"
                      className="btn primary small"
                      disabled={busy}
                      onClick={() =>
                        setLogging({
                          ...session,
                          actualSeconds: elapsedSeconds(session, Date.now()),
                        })
                      }
                    >
                      Ghi giờ
                    </button>
                  )}
                  {selected && (
                    <button
                      type="button"
                      className="btn ghost small"
                      data-action="session-detail"
                      data-id={selected.id}
                    >
                      Tùy chọn phiên
                    </button>
                  )}
                </div>
              </>
            );
          }}
        </CalendarClock>
      )}
    </section>
  );
}
