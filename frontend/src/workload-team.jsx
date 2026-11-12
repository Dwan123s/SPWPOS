import { useState } from "react";
import { dateFmt, minutes } from "./ui.jsx";
import { useWorkloadRequest } from "./workload-data.js";
import { openTeamTask } from "./team-task-link.js";

const isTeamWorkload = (value) =>
  Array.isArray(value?.members) && Array.isArray(value?.tasks);
const statuses = {
  UNASSIGNED: "Mở cho thành viên tự nhận",
  WAITING: "Chờ người được chỉ định nhận",
  ASSIGNED: "Đang thực hiện",
  REVIEW: "Chờ duyệt",
  COMPLETED: "Hoàn thành",
  OVERDUE: "Quá hạn",
};

export function TeamWorkload({ teamId, week, renderToolbar }) {
  const { value, error, retry } = useWorkloadRequest(
    `/api/teams/${teamId}/workload?week=${week}`,
    isTeamWorkload,
  );
  const [status, setStatus] = useState("");
  const [person, setPerson] = useState("");
  const names = new Map(value?.members.map((member) => [member.userId, member.name]));
  const selectedPerson = names.has(person) ? person : "";
  const recipient = (task) => task.assigneeId ?? task.designatedUserId;
  const tasks = value?.tasks.filter((task) =>
    (!status || task.status === status) && (!selectedPerson || recipient(task) === selectedPerson)
  ) ?? [];

  return (
    <>
      {renderToolbar(value?.start, value?.end, value && (
        <select aria-label="Lọc theo thành viên" value={selectedPerson} onChange={(event) => setPerson(event.target.value)}>
          <option value="">Tất cả thành viên</option>
          {value.members.map((member) => (
            <option key={member.userId} value={member.userId}>{member.name}</option>
          ))}
        </select>
      ))}
      {error && (
        <p className="warning" role="status">{error}{" "}
          <button type="button" className="btn ghost small" onClick={retry}>Thử lại</button>
        </p>
      )}
      {!value && !error && <p className="workload-message" role="status">Đang tải công việc nhóm…</p>}
      {value && (
        <section className="workload-panel" aria-label="Công việc nhóm">
          <header className="workload-panel-heading">
            <div>
              <h3>Tiến độ công việc nhóm</h3>
              <p>Lịch và giờ rảnh do từng thành viên quản lý riêng.</p>
            </div>
            <select aria-label="Lọc tình trạng công việc nhóm" value={status} onChange={(event) => setStatus(event.target.value)}>
              <option value="">Tất cả tình trạng</option>
              <option value="Open">Chờ nhận</option>
              <option value="Assigned">Đang thực hiện</option>
              <option value="PendingReview">Chờ duyệt</option>
              <option value="Completed">Hoàn thành</option>
            </select>
          </header>
          {tasks.length > 0 ? (
            <div className="workload-scroll" tabIndex={0} aria-label="Danh sách công việc nhóm">
              <table className="workload-table workload-task-table">
                <thead>
                  <tr>
                    <th scope="col">Công việc</th>
                    <th scope="col">Người nhận</th>
                    <th scope="col">Hạn chót</th>
                    <th scope="col">Ước lượng</th>
                    <th scope="col">Tiến độ</th>
                    <th scope="col">Tình trạng</th>
                  </tr>
                </thead>
                <tbody>
                  {tasks.map((task) => (
                    <tr key={task.id}>
                      <th scope="row">
                        <button type="button" className="btn ghost small" onClick={() => openTeamTask(teamId, task.id)}>
                          {task.name}
                        </button>
                      </th>
                      <td>{recipient(task) ? names.get(recipient(task)) || "Thành viên" : "Chưa có người nhận"}</td>
                      <td>{dateFmt(task.deadline)}</td>
                      <td>{minutes(task.estimatedMinutes)}</td>
                      <td>{task.progress}%</td>
                      <td><span className="workload-pill" data-risk={task.risk === "OVERDUE" || undefined}>{statuses[task.risk]}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : <p className="workload-message">Không có công việc nhóm trong tuần và bộ lọc đã chọn.</p>}
        </section>
      )}
    </>
  );
}
