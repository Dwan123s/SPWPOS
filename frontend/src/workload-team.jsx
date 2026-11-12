import { useState } from "react";
import { api } from "./api.js";
import { closeModal, dateFmt, dayKey, minutes, modal } from "./ui.jsx";
import {
  isWorkloadWeekday,
  useWorkloadRequest,
  workloadDayLabel,
} from "./workload-data.js";
import { openTeamTask } from "./team-task-link.js";

const isTeamWorkload = (value) =>
  Array.isArray(value?.members) && Array.isArray(value?.tasks);
const time = (value) => (value == null ? "—" : minutes(value));
const riskText = {
  OVERDUE: "Quá hạn",
  BEHIND: "Dự kiến trễ",
  UNPLANNED: "Chưa xếp đủ",
  UNKNOWN: "Chưa đủ dữ liệu",
  ON_TRACK: "Đủ lịch trước hạn",
  UNASSIGNED: "Chưa có người nhận",
  WAITING: "Chờ nhận việc",
  REVIEW: "Chờ duyệt",
  COMPLETED: "Hoàn thành",
};
const candidateText = {
  FITS: "Xếp được trong lịch hiện tại",
  NEEDS_REPLAN: "Cần kiểm tra phương án xếp lại",
  UNKNOWN: "Chưa thiết lập giờ làm",
};
const isRisk = (task) => task.risk === "BEHIND" || task.risk === "OVERDUE";
const owner = (task) => task.assigneeId || task.designatedUserId;

function RecipientForm({ teamId, task, names }) {
  const { value, error, retry } = useWorkloadRequest(
    `/api/teams/${teamId}/workload/${task.id}/candidates`,
    Array.isArray,
  );
  const [recipient, setRecipient] = useState(task.designatedUserId || "");
  const [saving, setSaving] = useState(false);
  const [failure, setFailure] = useState(null);
  const selected = value?.find((row) => row.userId === recipient);
  const stale = failure?.status === 409;
  async function save(event) {
    event.preventDefault();
    setSaving(true);
    setFailure(null);
    try {
      await api(`/api/teams/tasks/${task.id}/recipient`, "POST", {
        version: task.version,
        userId: recipient || null,
      });
      closeModal();
      window.dispatchEvent(new Event("workspace-updated"));
    } catch (problem) {
      setFailure(problem);
      if (problem.status === 409)
        window.dispatchEvent(new Event("workspace-updated"));
    } finally {
      setSaving(false);
    }
  }
  return (
    <form className="workload-recipient" onSubmit={save}>
      <p>
        Cần {time(task.remainingMinutes)} trước {dateFmt(task.deadline)}. Thành
        viên xác nhận nhận việc trên lịch của họ.
      </p>
      {error && (
        <p role="alert" className="warning">
          {error}{" "}
          <button type="button" className="btn ghost small" onClick={retry}>
            Thử lại
          </button>
        </p>
      )}
      {failure && (
        <p role="alert" className="warning">
          {failure.message}
        </p>
      )}
      {!value && !error && (
        <p role="status">Đang kiểm tra công suất trước hạn…</p>
      )}
      {value && (
        <fieldset disabled={saving || stale}>
          <legend>Chọn người nhận</legend>
          {value.map((candidate) => (
            <label className="workload-recipient-option" key={candidate.userId}>
              <input
                type="radio"
                name="recipient"
                value={candidate.userId}
                checked={recipient === candidate.userId}
                onChange={() => setRecipient(candidate.userId)}
              />
              <span>
                <strong>{names.get(candidate.userId) || "Thành viên"}</strong>
                <small>
                  {candidateText[candidate.status]}
                  {candidate.schedulableMinutes != null &&
                    ` · Còn ${time(candidate.schedulableMinutes)} trước hạn`}
                </small>
              </span>
            </label>
          ))}
          <label className="workload-recipient-option">
            <input
              type="radio"
              name="recipient"
              value=""
              checked={!recipient}
              onChange={() => setRecipient("")}
            />
            Để nhóm tự nhận
          </label>
        </fieldset>
      )}
      <div className="form-actions">
        <button
          className="btn primary"
          disabled={
            saving || stale || !value || !!error || (recipient && !selected)
          }
        >
          {saving ? "Đang lưu…" : "Xác nhận người nhận"}
        </button>
        <button
          type="button"
          className="btn ghost"
          onClick={closeModal}
          disabled={saving}
        >
          Đóng
        </button>
      </div>
    </form>
  );
}

function CapacityCell({ day, member, selected, onSelect, showHours }) {
  const hasHours = day.workingMinutes > 0;
  const past = hasHours && day.schedulableMinutes == null;
  const busy = hasHours
    ? Math.min(100, (day.busyMinutes / day.workingMinutes) * 100)
    : 0;
  const reserved = hasHours
    ? Math.min(100 - busy, (day.reservedMinutes / day.workingMinutes) * 100)
    : 0;
  const label =
    day.workingMinutes == null
      ? "Chưa thiết lập"
      : !hasHours
        ? "Không có giờ"
        : past
          ? day.recordedActualSeconds == null
            ? "—"
            : time(day.recordedActualSeconds / 60)
          : day.schedulableMinutes === 0
            ? "Đã đầy"
            : `Trống ${time(day.schedulableMinutes)}`;
  return (
    <button
      className="workload-cell"
      type="button"
      aria-pressed={selected}
      aria-label={`${member.name}, ${workloadDayLabel.format(new Date(day.date))}, ${label}`}
      data-load={day.schedulableMinutes === 0 && hasHours ? "full" : "normal"}
      onClick={onSelect}
    >
      {hasHours && (
        <span className="workload-track" aria-hidden="true">
          <span
            className="workload-segment busy"
            style={{ width: `${busy}%` }}
          />
          <span
            className="workload-segment planned"
            style={{ width: `${reserved}%` }}
          />
        </span>
      )}
      <span>
        {showHours || !hasHours || day.schedulableMinutes === 0
          ? label
          : past
            ? day.recordedActualSeconds == null
              ? "—"
              : "Đã ghi nhận"
            : "Còn chỗ"}
      </span>
    </button>
  );
}

export function TeamWorkload({
  teamId,
  week,
  showHours,
  showWeekends,
  renderToolbar,
}) {
  const { value, error, retry } = useWorkloadRequest(
    `/api/teams/${teamId}/workload?week=${week}`,
    isTeamWorkload,
  );
  const following = useWorkloadRequest(
    value && week < 3 ? `/api/teams/${teamId}/workload?week=${week + 1}` : null,
    isTeamWorkload,
  );
  const [filter, setFilter] = useState("all");
  const [person, setPerson] = useState("");
  const [selection, setSelection] = useState(null);
  if (!value)
    return (
      <>
        {renderToolbar()}
        <div className="workload-message" role="status">
          {error || "Đang tải công suất nhóm…"}
          {error && (
            <button className="btn ghost small" onClick={retry}>
              Thử lại
            </button>
          )}
        </div>
      </>
    );
  const names = new Map(value.members.map((row) => [row.userId, row.name]));
  const visiblePerson = names.has(person) ? person : "";
  const members = visiblePerson
    ? value.members.filter((row) => row.userId === visiblePerson)
    : value.members;
  const nextWeek = following.value?.start > value.end ? following.value : null;
  const nextDays = new Map(
    nextWeek?.members.map((row) => [row.userId, row.days]),
  );
  const memberDays = new Map(
    members.map((row) => [
      row.userId,
      new Map(
        [...row.days, ...(nextDays.get(row.userId) ?? [])]
          .filter((day) => showWeekends || isWorkloadWeekday(day))
          .map((day) => [day.date, day]),
      ),
    ]),
  );
  const columns = [
    ...new Set(
      members.flatMap((row) => [...memberDays.get(row.userId).keys()]),
    ),
  ].sort();
  const today = value.asOf ? dayKey(value.asOf) : "";
  const selectedMember = members.find(
    (row) => row.userId === selection?.userId,
  );
  const selectedDay = memberDays
    .get(selectedMember?.userId)
    ?.get(selection?.date);
  const available = value.members
    .flatMap((row) => row.days)
    .map((day) => day.schedulableMinutes)
    .filter((count) => count != null);
  const tasks = value.tasks.filter(
    (task) =>
      (!visiblePerson || owner(task) === visiblePerson) &&
      (filter === "all" ||
        (filter === "risk" && isRisk(task)) ||
        task.risk === filter ||
        (filter === "open" && task.status === "Open")),
  );
  return (
    <>
      {renderToolbar(
        `${value.start} – ${nextWeek?.end ?? value.end}`,
        <select
          aria-label="Lọc theo thành viên"
          value={visiblePerson}
          onChange={(event) => setPerson(event.target.value)}
        >
          <option value="">Tất cả thành viên</option>
          {value.members.map((row) => (
            <option key={row.userId} value={row.userId}>
              {row.name}
            </option>
          ))}
        </select>,
      )}
      {error && (
        <p className="warning" role="status">
          Số liệu chưa được làm mới: {error}{" "}
          <button className="btn ghost small" onClick={retry}>
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
      <section
        className="workload-panel workload-capacity-panel"
        aria-label="Công suất thành viên"
      >
        <header className="workload-panel-heading">
          <div>
            <h3>Công suất thành viên</h3>
            <p>Ngày tính theo múi giờ của từng thành viên.</p>
          </div>
          <div className="workload-legend">
            <span>
              <i className="workload-segment planned" /> Phiên đã xếp
            </span>
            <span>
              <i className="workload-segment busy" /> Lịch bận
            </span>
          </div>
        </header>
        <div
          className="workload-scroll workload-matrix-scroll"
          tabIndex={0}
          aria-label="Bảng công suất theo ngày"
        >
          <table className="workload-table workload-matrix">
            <thead>
              <tr>
                <th scope="col">Thành viên</th>
                {columns.map((date) => (
                  <th
                    scope="col"
                    key={date}
                    aria-current={date === today ? "date" : undefined}
                  >
                    {workloadDayLabel.format(new Date(date))}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {members.map((member) => (
                <tr key={member.userId}>
                  <th scope="row">
                    {member.name}
                    {value.tasks.some(
                      (task) => owner(task) === member.userId && isRisk(task),
                    ) && (
                      <small className="workload-risk-text">
                        {
                          value.tasks.filter(
                            (task) =>
                              owner(task) === member.userId && isRisk(task),
                          ).length
                        }{" "}
                        việc có rủi ro
                      </small>
                    )}
                  </th>
                  {columns.map((date) => {
                    const day = memberDays.get(member.userId).get(date);
                    return (
                      <td key={date} data-today={date === today || undefined}>
                        {day && (
                          <CapacityCell
                            day={day}
                            member={member}
                            showHours={showHours}
                            selected={
                              selectedDay?.date === day.date &&
                              selectedMember?.userId === member.userId
                            }
                            onSelect={() =>
                              setSelection({
                                userId: member.userId,
                                date: day.date,
                              })
                            }
                          />
                        )}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!value.members.length && (
          <p className="workload-message">Chưa có thành viên đang hoạt động.</p>
        )}
        {selectedDay && (
          <aside className="workload-day-inspect" aria-live="polite">
            <div>
              <strong>{selectedMember.name}</strong>
              <span>
                {workloadDayLabel.format(new Date(selectedDay.date))} ·{" "}
                {selectedMember.timeZoneId}
              </span>
            </div>
            <div>
              <span>Giờ làm</span>
              <strong>{time(selectedDay.workingMinutes)}</strong>
            </div>
            <div>
              <span>Lịch bận</span>
              <strong>{time(selectedDay.busyMinutes)}</strong>
            </div>
            <div>
              <span>Phiên đã xếp</span>
              <strong>{time(selectedDay.reservedMinutes)}</strong>
            </div>
            <div>
              <span>Còn xếp thêm</span>
              <strong>{time(selectedDay.schedulableMinutes)}</strong>
            </div>
            <div>
              <span>Thực làm cho nhóm</span>
              <strong>
                {time(
                  selectedDay.recordedActualSeconds == null
                    ? null
                    : selectedDay.recordedActualSeconds / 60,
                )}
              </strong>
            </div>
          </aside>
        )}
      </section>
      <details className="workload-summary">
        <summary>Tổng quan tuần đang chọn</summary>
        <div className="workload-metrics">
          <div>
            <span>Thành viên</span>
            <strong>{value.members.length}</strong>
          </div>
          <div>
            <span>Còn xếp thêm</span>
            <strong>
              {available.length
                ? time(available.reduce((a, b) => a + b, 0))
                : "—"}
            </strong>
          </div>
          <div>
            <span>Chờ nhận việc</span>
            <strong>
              {value.tasks.filter((task) => task.status === "Open").length}
            </strong>
          </div>
          <div data-risk>
            <span>Dự kiến trễ / quá hạn</span>
            <strong>{value.tasks.filter(isRisk).length}</strong>
          </div>
        </div>
      </details>
      <details className="workload-summary workload-more">
        <summary>Điều phối công việc nhóm</summary>
        <section className="workload-panel" aria-label="Công việc nhóm">
          <header className="workload-panel-heading">
            <div>
              <h3>Công việc nhóm</h3>
              <p>
                {tasks.length}/{value.tasks.length} công việc
              </p>
            </div>
            <div className="workload-filters">
              <select
                aria-label="Lọc tình trạng công việc nhóm"
                value={filter}
                onChange={(event) => setFilter(event.target.value)}
              >
                <option value="all">Tất cả tình trạng</option>
                <option value="risk">Dự kiến trễ / quá hạn</option>
                <option value="UNPLANNED">Chưa xếp đủ</option>
                <option value="open">Chờ nhận việc</option>
                <option value="REVIEW">Chờ duyệt</option>
              </select>
            </div>
          </header>
          {tasks.length ? (
            <div
              className="workload-scroll"
              tabIndex={0}
              aria-label="Danh sách công việc nhóm"
            >
              <table className="workload-table workload-task-table">
                <thead>
                  <tr>
                    <th scope="col">Công việc</th>
                    <th scope="col">Người nhận</th>
                    <th scope="col">Hạn chót</th>
                    <th scope="col">Còn lại</th>
                    <th scope="col">Tình trạng</th>
                    <th scope="col">Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {tasks.map((task) => (
                    <tr key={task.id}>
                      <th scope="row">{task.name}</th>
                      <td>
                        {names.get(owner(task)) ||
                          (owner(task) ? "Thành viên" : "Chưa chỉ định")}
                      </td>
                      <td>{dateFmt(task.deadline)}</td>
                      <td>{time(task.remainingMinutes)}</td>
                      <td>
                        <span
                          className="workload-pill"
                          data-risk={isRisk(task) || undefined}
                        >
                          {riskText[task.risk]}
                        </span>
                        {task.shortfallMinutes > 0 ? (
                          <small>
                            Thiếu {time(task.shortfallMinutes)} trước hạn
                          </small>
                        ) : (
                          task.unscheduledMinutes > 0 && (
                            <small>
                              Chưa xếp {time(task.unscheduledMinutes)}
                            </small>
                          )
                        )}
                      </td>
                      <td>
                        {task.status === "Open" && !isRisk(task) ? (
                          <button
                            className="btn secondary small"
                            onClick={() =>
                              modal(
                                "Chọn người nhận",
                                task.name,
                                <RecipientForm
                                  teamId={teamId}
                                  task={task}
                                  names={names}
                                />,
                              )
                            }
                          >
                            {task.designatedUserId
                              ? "Đổi người nhận"
                              : "Chọn người nhận"}
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="btn ghost small"
                            onClick={() => openTeamTask(teamId, task.id)}
                          >
                            Mở công việc
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="workload-message">
              {value.tasks.length
                ? "Không có công việc khớp bộ lọc."
                : "Chưa có công việc trong tuần này."}
            </p>
          )}
        </section>
      </details>
    </>
  );
}
