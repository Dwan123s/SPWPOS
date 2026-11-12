import { useId, useState } from "react";
import { api } from "./api.js";
import {
  button,
  closeModal,
  dateFmt,
  dayKey,
  icon,
  minutes,
  modal,
  state,
  timeFmt,
} from "./ui.jsx";
import { planModal } from "./calendar.jsx";
import {
  isWorkloadWeekday,
  useWorkloadRequest,
  workloadDayLabel,
} from "./workload-data.js";
import { TeamWorkload } from "./workload-team.jsx";
import { calendarDayBounds } from "./nora-schedule.js";
import { nextDay } from "./dashboard-model.js";
import "./workload.css";

const isPersonalWorkload = (value) =>
  !!value?.next && Array.isArray(value.days) && Array.isArray(value.tasks);
const useWorkload = (week = 0) =>
  useWorkloadRequest(
    week == null
      ? null
      : week === 0
        ? "/api/intelligence/workload"
        : `/api/intelligence/workload?week=${week}`,
    isPersonalWorkload,
  );

const statusText = {
  PAST: "Đã qua",
  UNCONFIGURED: "Chưa thiết lập",
  SHORTFALL: "Thiếu giờ trước hạn",
  NO_HOURS: "Không có giờ làm",
  AVAILABLE: "Còn chỗ",
  FULL: "Đã đầy",
};
const timeText = (value) => (value == null ? "—" : minutes(value));
const workloadDate = (date) => dateFmt(`${date}T12:00:00Z`, { timeZone: "UTC" });
const loadPercent = (seconds, workingMinutes) =>
  workingMinutes > 0 ? Math.min(100, (seconds / 60 / workingMinutes) * 100) : 0;
const windowText = (row) =>
  `${dateFmt(row.start)} · ${timeFmt(row.start)}–${timeFmt(row.end)}`;
async function previewRecovery(taskId, triggerEvent = "SESSION_MISSED") {
  const plan = await api("/api/plans/reschedule", "POST", {
    start: new Date().toISOString(),
    horizonDays: 30,
    affectedTaskIds: [taskId],
    triggerEvent,
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
        {next.kind === "INBOX" &&
          button("Mở công việc", "open-task-list", "primary", "all")}
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

function TeamWorkloadPicker({ week, renderToolbar }) {
  const { value, error, retry } = useWorkloadRequest(
    "/api/teams",
    Array.isArray,
  );
  const [teamId, setTeamId] = useState("");
  const teams = value?.filter((team) => team.role === "Leader") ?? [];
  const selected = teams.find((team) => team.id === teamId) || teams[0];
  return (
    <>
      {!selected && renderToolbar()}
      {error && (
        <p className="warning" role="alert">
          {error}{" "}
          <button type="button" className="btn ghost small" onClick={retry}>
            Thử lại
          </button>
        </p>
      )}
      {!value && !error && (
        <p className="workload-message" role="status">
          Đang tải danh sách nhóm…
        </p>
      )}
      {value && !selected && (
        <p className="workload-message">
          Báo cáo công việc nhóm dành cho trưởng nhóm.{" "}
          {button("Mở Nhóm", "nav", "ghost small", "teams")}
        </p>
      )}
      {selected && (
        <TeamWorkload
          key={selected.id}
          teamId={selected.id}
          week={week}
          renderToolbar={(start, end, personFilter) =>
            renderToolbar(
              start,
              end,
              <>
                <label className="workload-team-picker">
                  <select
                    aria-label="Chọn nhóm"
                    value={selected.id}
                    onChange={(event) => setTeamId(event.target.value)}
                  >
                    {teams.map((team) => (
                      <option key={team.id} value={team.id}>
                        {team.name}
                      </option>
                    ))}
                  </select>
                </label>
                {personFilter}
              </>,
            )
          }
        />
      )}
    </>
  );
}

export function WorkloadWeek({ onWorkspaceChanged }) {
  const [scope, setScope] = useState("personal");
  const [week, setWeek] = useState(0);
  const [showHours, setShowHours] = useState(true);
  const [showWeekends, setShowWeekends] = useState(true);
  const renderToolbar = (start, end, filters) => (
    <header className="workload-toolbar">
      <h2 className="workload-visually-hidden">Tải công việc trong tuần</h2>
      <div
        className="workload-scope"
        role="group"
        aria-label="Phạm vi công việc"
      >
        <button
          type="button"
          aria-pressed={scope === "personal"}
          onClick={() => setScope("personal")}
        >
          Cá nhân
        </button>
        <button
          type="button"
          aria-pressed={scope === "team"}
          onClick={() => setScope("team")}
        >
          Nhóm
        </button>
      </div>
      {filters}
      <div className="workload-weeks" role="group" aria-label="Chọn tuần">
        <button
          className="btn ghost small"
          type="button"
          onClick={() => setWeek(0)}
        >
          Hôm nay
        </button>
        <button
          className="btn ghost small"
          type="button"
          aria-label="Tuần trước"
          disabled={week <= -4}
          onClick={() => setWeek((current) => Math.max(-4, current - 1))}
        >
          ‹
        </button>
        <button
          className="btn ghost small"
          type="button"
          aria-label="Tuần sau"
          disabled={week >= 3}
          onClick={() => setWeek((current) => Math.min(3, current + 1))}
        >
          ›
        </button>
        {start && end && (
          <label className="workload-range" title={`${start} – ${end}`}>
            <span>{workloadDate(start)} – {workloadDate(end)}</span>{icon("chevron")}
            <select aria-label="Chuyển đến tuần" value={week} onChange={(event) => setWeek(Number(event.target.value))}>
              {Array.from({ length: 8 }, (_, index) => index - 4).map((offset) => {
                const monday = nextDay(start, (offset - week) * 7);
                return <option key={offset} value={offset}>{workloadDate(monday)} – {workloadDate(nextDay(monday, 6))}{offset === 0 ? " · Tuần này" : ""}</option>;
              })}
            </select>
          </label>
        )}
      </div>
      {scope === "personal" && <div className="workload-capacity-filters">
        <label>
          Hiện số giờ
          <input
            type="checkbox"
            checked={showHours}
            onChange={(event) => setShowHours(event.target.checked)}
          />
        </label>
        <label>
          Hiện cuối tuần
          <input
            type="checkbox"
            checked={showWeekends}
            onChange={(event) => setShowWeekends(event.target.checked)}
          />
        </label>
      </div>}
    </header>
  );
  const viewOptions = { week, showHours, showWeekends, renderToolbar };
  return (
    <section className="workload-week" aria-label="Tải công việc">
      {scope === "personal" ? (
        <PersonalWorkload
          {...viewOptions}
          onWorkspaceChanged={onWorkspaceChanged}
        />
      ) : (
        <TeamWorkloadPicker {...viewOptions} />
      )}
    </section>
  );
}

function WorkloadGroupCell({ group, day, cell, showHours, showName, today }) {
  const popupId = useId();
  const date = workloadDayLabel.format(new Date(day.date));
  const duration = cell && timeText(Math.round(cell.seconds / 60));
  const fill = loadPercent(cell?.seconds ?? 0, day.workingMinutes);
  const close = () => document.getElementById(popupId)?.hidePopover?.();
  const canAdd = group.inbox && day.date >= today;
  return (
    <>
      <button
        type="button"
        className="workload-group-slot"
        data-inbox={group.inbox || undefined}
        popoverTarget={popupId}
        aria-label={`${group.name}, ${date}${showHours && duration ? `, ${duration} dự kiến` : ""}`}
        onClick={(event) => {
          const rect = event.currentTarget.getBoundingClientRect();
          const popup = document.getElementById(popupId);
          const width = Math.min(320, window.innerWidth - 24);
          popup.style.left = `${Math.max(12, Math.min(rect.left, window.innerWidth - width - 12))}px`;
          popup.style.top = `${Math.max(12, Math.min(rect.bottom + 6, window.innerHeight - 296))}px`;
        }}
      >
        <span className="workload-group-load">
          {fill > 0 && <span className="workload-group-bar" style={{ width: `${fill}%` }} aria-hidden="true" />}
          <span className="workload-group-hours">{showHours && duration}</span>
        </span>
        {showName && <span className="workload-group-name">{group.name}</span>}
      </button>
      <div id={popupId} popover="auto" className="workload-group-popup"
        role="group" aria-label={`${group.name} · ${date}`}>
        <header><strong>{group.name}</strong><span>{date}</span></header>
        <ul>
          {Array.from(cell?.tasks ?? [], ([id, task]) => {
            const complete = task.status === "Completed";
            return (
              <li key={id} className="workload-popup-task">
                {task.canComplete ? (
                  <button type="button" className="task-complete-toggle workload-popup-check" role="checkbox"
                    aria-checked={complete} aria-label={`${complete ? "Mở lại" : "Hoàn thành"} ${task.name}`}
                    data-action="toggle-task-completion" data-id={id}>{complete && icon("check")}</button>
                ) : <span className="workload-popup-task-icon" aria-hidden="true">{icon(complete ? "check" : "tasks")}</span>}
                <button type="button" className="workload-popup-title" data-action="task-detail" data-id={id} onClick={close}>
                  <span>{task.name}<small className="workload-group-times">{task.times.join(" · ")}</small></span>
                  <small>{timeText(Math.round(task.seconds / 60))}</small>
                </button>
              </li>
            );
          })}
        </ul>
        {!cell && <p>Chưa có công việc được xếp trong ngày này.</p>}
        {canAdd && (
          <button type="button" className="btn secondary small" data-action="new-task"
            data-start-date={day.date} onClick={close}>
            {icon("plus")} Thêm công việc vào Inbox
          </button>
        )}
        {group.projectId && (
          <button type="button" className="btn secondary small" data-action="task-detail" data-id={group.projectId}
            onClick={close}>
            Xem project
          </button>
        )}
      </div>
    </>
  );
}

function PersonalWorkload({
  week,
  onWorkspaceChanged,
  showHours,
  showWeekends,
  renderToolbar,
}) {
  const { value, error, retry } = useWorkload(week);
  const following = useWorkload(value && week < 3 ? week + 1 : null);
  const [selection, setSelection] = useState(null);
  const [showBands, setShowBands] = useState(true);
  const bandsId = useId();
  function toggleSelection(date) {
    setSelection((current) => current === date ? null : date);
  }
  if (!value)
    return (
      <>
        {renderToolbar()}
        <div className="workload-message" role="status">
          {error || "Đang tải công việc…"}
          {error && (
            <button type="button" className="btn ghost small" onClick={retry}>
              Thử lại
            </button>
          )}
        </div>
      </>
    );
  const nextWeek = following.value?.start > value.end ? following.value : null;
  const days = [...value.days, ...(nextWeek?.days ?? [])].filter(
    (day) => showWeekends || isWorkloadWeekday(day),
  );
  const today = dayKey(value.asOf || Date.now());
  const name = state.workspace.profile?.displayName?.trim() || "Tôi";
  const groups = new Map();
  const tasksById = new Map(state.workspace.tasks.map((task) => [task.id, task]));
  const projectIds = new Set(state.workspace.tasks.map((task) => task.parentId).filter(Boolean));
  for (const day of days) {
    if (!day.sessions.length) continue;
    const zone = value.timeZoneId || state.workspace?.preference?.timeZoneId || "Asia/Ho_Chi_Minh";
    let bounds;
    try {
      bounds = calendarDayBounds(day.date, zone);
    } catch {
      // Some time zones skip local midnight on clock changes.
    }
    for (const session of day.sessions) {
      const id = session.taskId || session.id;
      const task = tasksById.get(id);
      const project = tasksById.get(task?.parentId) || (projectIds.has(id) ? task : null);
      const isTeam = session.teamTask || task?.isTeamTask;
      const key = project ? project.id : isTeam ? "team" : "inbox";
      const group = groups.get(key) ?? {
        name: project?.name || (isTeam ? "Công việc nhóm" : "Inbox"),
        projectId: project?.id,
        inbox: key === "inbox",
        days: new Map(),
      };
      groups.set(key, group);
      const cell = group.days.get(day.date) ?? { seconds: 0, tasks: new Map() };
      const start = Date.parse(session.start);
      const end = Date.parse(session.end);
      const seconds = bounds
        ? (Math.min(end, bounds[1]) - Math.max(start, bounds[0])) / 1000
        : Math.min((end - start) / 1000, day.plannedSeconds);
      const duration = Math.max(0, seconds);
      const entry = cell.tasks.get(id) ?? {
        name: session.name, seconds: 0, status: task?.status, times: [],
        canComplete: Boolean(task && !isTeam && !projectIds.has(id) && task.status !== "Cancelled"),
      };
      entry.seconds += duration;
      const dayEnd = bounds && end >= bounds[1] ? "24:00" : timeFmt(end);
      entry.times.push(`${timeFmt(Math.max(start, bounds?.[0] ?? start))}–${dayEnd}`);
      cell.tasks.set(id, entry);
      cell.seconds += duration;
      group.days.set(day.date, cell);
    }
  }
  const personLabel = (
    <>
      <span className="workload-person-avatar" aria-hidden="true">{Array.from(name)[0].toLocaleUpperCase("vi")}</span>
      <span>Cá nhân</span>
    </>
  );
  return (
    <>
      {renderToolbar(value.start, nextWeek?.end ?? value.end)}
      {error && (
        <p className="warning" role="status">
          Số liệu chưa được làm mới: {error}{" "}
          <button type="button" className="btn ghost small" onClick={retry}>
            Thử lại
          </button>
        </p>
      )}
      {following.error && (
        <p className="warning" role="status">
          Chưa tải được tuần tiếp theo.{" "}
          <button
            type="button"
            className="btn ghost small"
            onClick={following.retry}
          >
            Thử lại
          </button>
        </p>
      )}
      <div
        className="workload-scroll workload-matrix-scroll"
        role="region"
        tabIndex={0}
        aria-label="Lịch công việc theo ngày"
      >
        <table className="workload-table workload-matrix workload-personal-matrix" style={{ "--workload-days": days.length }}>
          <thead>
            <tr>
              <th scope="col">Tên</th>
              {days.map((day) => (
                <th
                  key={day.date}
                  scope="col"
                  aria-current={day.date === today ? "date" : undefined}
                >
                  {workloadDayLabel.format(new Date(day.date))}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            <tr className="workload-total-row">
              <th scope="row">
                {groups.size > 0 ? (
                  <button type="button" className="workload-person-label workload-person-toggle" aria-expanded={showBands}
                    aria-controls={bandsId} onClick={() => setShowBands((shown) => !shown)}>
                    {personLabel}
                    {icon("chevron")}
                  </button>
                ) : <span className="workload-person-label">{personLabel}</span>}
              </th>
              {days.map((day) => {
                const plannedMinutes = day.plannedSeconds / 60;
                const room = day.schedulableMinutes;
                const capacity = room > 0 ? `Trống ${timeText(room)}` : statusText[day.status];
                const fill = loadPercent(day.plannedSeconds, day.workingMinutes);
                const visibleCapacity = showHours || !(room > 0) ? capacity : "Còn chỗ";
                return (
                  <td
                    key={day.date}
                    data-today={day.date === today || undefined}
                  >
                    <button
                      type="button"
                      className="workload-matrix-day"
                      data-load={day.status === "SHORTFALL" ? "shortfall" : undefined}
                      aria-label={`${workloadDayLabel.format(new Date(day.date))}${showHours ? `, ${timeText(plannedMinutes)} dự kiến` : ""}, ${visibleCapacity}`}
                      aria-expanded={
                        selection === day.date
                      }
                      onClick={() => toggleSelection(day.date)}
                    >
                      <span className="workload-matrix-track" aria-hidden="true">
                        {fill > 0 && <span className="workload-matrix-bar" style={{ width: `${fill}%` }} />}
                      </span>
                      {showHours &&
                        plannedMinutes > 0 &&
                        <strong>{timeText(Math.round(plannedMinutes))}</strong>}
                    </button>
                  </td>
                );
              })}
            </tr>
          </tbody>
          <tbody id={bandsId} hidden={groups.size > 0 && !showBands}>
            {groups.size === 0 && (
              <tr className="workload-empty-row" aria-hidden="true">
                <td />
                {days.map(({ date }) => (
                  <td key={date} data-today={date === today || undefined} />
                ))}
              </tr>
            )}
            {Array.from(groups, ([id, group]) => {
              const first = group.days.keys().next().value;
              const last = Array.from(group.days.keys()).at(-1);
              return (
              <tr key={id} className="workload-group-row">
                <th scope="row"><span className="workload-visually-hidden">{group.name}</span></th>
                {days.map((day) => {
                  const cell = group.days.get(day.date);
                  const inRange = day.date >= first && day.date <= last;
                  return (
                    <td
                      key={day.date}
                      data-today={day.date === today || undefined}
                      data-band={inRange ? group.inbox ? "inbox" : "project" : undefined}
                    >
                      {inRange && (
                        <WorkloadGroupCell group={group} day={day} cell={cell} showHours={showHours} showName={day.date === first} today={today} />
                      )}
                    </td>
                  );
                })}
              </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      {week === 0 && value.next.kind === "SETUP" && (
        <p className="workload-message">
          {value.next.reason}{" "}
          {button("Thiết lập giờ làm", "nav", "secondary small", "time")}
        </p>
      )}
      {week === 0 && value.tasksMissingPlanningDetails > 0 && (
        <p className="workload-message">
          <strong>Thiếu thông tin xếp lịch</strong> · {value.tasksMissingPlanningDetails} việc cần hạn chót hoặc thời lượng.{" "}
          {button("Mở công việc", "open-task-list", "secondary small", "all")}
        </p>
      )}
      {week === 0 && value.tasksAwaitingSchedule > 0 && (
        <p className="workload-message">
          <strong>Đủ thông tin, chưa xếp đủ thời gian</strong> · {value.tasksAwaitingSchedule} việc.{" "}
          {button("Xem lịch đề xuất", "generate-plan", "secondary small")}
        </p>
      )}
      <div
        className="workload-grid"
        hidden={!days.some((day) => day.date === selection)}
      >
        {days.map((day) => {
          const active = day.date === selection;
          const sessions = day.sessions;
          return (
            <article
              key={day.date}
              className="workload-day"
              hidden={!active}
            >
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
                    <dt>Giờ có thể làm</dt>
                    <dd>{timeText(day.workingMinutes)}</dd>
                    <dt>Đã giữ chỗ</dt>
                    <dd>{timeText(day.reservedMinutes)}</dd>
                    <dt>Còn xếp thêm</dt>
                    <dd>{timeText(day.schedulableMinutes)}</dd>
                  </dl>
                )}
              </div>
              <details open={active}>
                <summary>
                  Phiên đã xếp ({sessions.length})
                </summary>
                  <p>
                    Dự kiến {timeText(day.plannedSeconds / 60)} · Thực làm{" "}
                    {timeText(
                      day.recordedActualSeconds == null
                        ? null
                        : day.recordedActualSeconds / 60,
                    )}
                  </p>
                {sessions.map((session) => (
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
          );
        })}
      </div>
    </>
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
            Giữ giờ này và xem lịch đề xuất
          </button>
        )}
        {preview?.id && (
          <button
            className="btn primary"
            disabled={busy}
            onClick={() => run(true)}
          >
            Lưu Lịch Trình và áp dụng đề xuất
          </button>
        )}
        <button className="btn ghost" disabled={busy} onClick={onBack}>
          Chỉnh sửa Lịch Trình
        </button>
      </div>
    </>
  );
}
