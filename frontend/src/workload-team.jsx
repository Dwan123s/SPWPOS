import { useState } from "react";
import { dateFmt, icon, minutes } from "./ui.jsx";
import { useWorkloadRequest } from "./workload-data.js";
import { openTeamTask } from "./team-task-link.js";
import "./workload-team.css";

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
const stages = [
  { value: "Open", label: "Chờ nhận", tone: "open" },
  { value: "Assigned", label: "Đang thực hiện", tone: "active" },
  { value: "PendingReview", label: "Chờ duyệt", tone: "review" },
  { value: "Completed", label: "Hoàn thành", tone: "done" },
];
const recipient = (task) => task.assigneeId ?? task.designatedUserId;

function MemberMark({ name }) {
  const words = name.trim().split(/\s+/);
  const initials = (words.length > 1 ? words.slice(-2) : words)
    .map((word) => Array.from(word)[0])
    .join("")
    .toLocaleUpperCase("vi");
  return (
    <span aria-hidden="true" className="team-report-avatar">
      {initials || icon("users")}
    </span>
  );
}

function TeamReportOverview({
  teamName,
  members,
  tasks,
  status,
  selectedPerson,
  onStatus,
  onPerson,
}) {
  const counts = new Map();
  for (const task of tasks)
    counts.set(task.status, (counts.get(task.status) || 0) + 1);
  const completed = counts.get("Completed") || 0;
  const overdue = tasks.filter((task) => task.risk === "OVERDUE").length;
  const totalMinutes = members.reduce(
    (total, member) => total + member.estimatedMinutes,
    0,
  );
  const maxMinutes = Math.max(
    1,
    ...members.map((member) => member.estimatedMinutes),
  );
  const metrics = [
    ["tasks", "Công việc", tasks.length, "active"],
    [
      "clock",
      "Ước lượng đã giao trong tuần",
      minutes(totalMinutes, "long"),
      "open",
    ],
    ["check", "Hoàn thành", `${completed}/${tasks.length}`, "done"],
    ["alertCircle", "Quá hạn", overdue, overdue ? "late" : "open"],
  ];

  return (
    <>
      <section
        className="team-report-overview"
        aria-label="Tổng quan nhóm trong tuần"
      >
        <div className="team-report-title">
          <span className="team-report-title-icon">{icon("users")}</span>
          <h3>
            {teamName
              ? `Tổng quan tuần · ${teamName}`
              : "Tổng quan tuần của nhóm"}
          </h3>
        </div>
        <dl className="team-report-metrics">
          {metrics.map(([symbol, label, value, tone]) => (
            <div key={label} data-tone={tone}>
              <span className="team-report-metric-icon">{icon(symbol)}</span>
              <dt>{label}</dt>
              <dd>{value}</dd>
            </div>
          ))}
        </dl>
      </section>

      <div className="team-report-breakdown">
        <section className="team-report-card">
          <header className="team-report-heading">
            <h3>Khối lượng theo thành viên</h3>
            <p>Việc đã nhận hoặc được chỉ định có hạn trong tuần.</p>
          </header>
          {members.length ? (
            <table
              className="team-report-members"
              aria-label="Tổng hợp theo thành viên"
            >
              <colgroup>
                <col className="team-report-member-name" />
                <col className="team-report-member-count" />
                <col />
              </colgroup>
              <thead className="workload-visually-hidden">
                <tr>
                  <th scope="col">Thành viên</th>
                  <th scope="col">Số việc</th>
                  <th scope="col">Tổng thời lượng ước tính</th>
                </tr>
              </thead>
              <tbody>
                {members.map((member) => (
                  <tr key={member.userId}>
                    <th scope="row">
                      <button
                        type="button"
                        className="team-report-member-button"
                        aria-pressed={selectedPerson === member.userId}
                        onClick={() => onPerson(member.userId)}
                      >
                        <MemberMark name={member.name} />
                        <span>{member.name}</span>
                      </button>
                    </th>
                    <td>{member.taskCount} việc</td>
                    <td>
                      <strong>
                        {minutes(member.estimatedMinutes, "long")}
                      </strong>
                      <span
                        className="team-report-member-bar"
                        aria-hidden="true"
                      >
                        <span
                          style={{
                            width: `${(100 * member.estimatedMinutes) / maxMinutes}%`,
                          }}
                        />
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p className="team-report-empty">Chưa có thành viên để hiển thị.</p>
          )}
        </section>

        <section className="team-report-card">
          <header className="team-report-heading">
            <h3>Phân bố công việc</h3>
          </header>
          <div className="team-report-distribution" aria-hidden="true">
            {stages
              .filter((stage) => counts.has(stage.value))
              .map((stage) => (
                <span
                  key={stage.value}
                  data-tone={stage.tone}
                  style={{ flexGrow: counts.get(stage.value) }}
                />
              ))}
          </div>
          <div
            className="team-report-stages"
            role="group"
            aria-label="Lọc nhanh theo tình trạng"
          >
            {stages.map((stage) => (
              <button
                type="button"
                key={stage.value}
                data-tone={stage.tone}
                aria-pressed={status === stage.value}
                onClick={() =>
                  onStatus(status === stage.value ? "" : stage.value)
                }
              >
                <span className="team-report-stage-dot" aria-hidden="true" />
                <span>{stage.label}</span>
                <strong>{counts.get(stage.value) || 0}</strong>
              </button>
            ))}
          </div>
        </section>
      </div>
    </>
  );
}

export function TeamWorkload({ teamId, teamName, week, renderToolbar }) {
  const { value, error, retry } = useWorkloadRequest(
    `/api/teams/${teamId}/workload?week=${week}`,
    isTeamWorkload,
  );
  const [status, setStatus] = useState("");
  const [person, setPerson] = useState("");
  const names = new Map(
    value?.members.map((member) => [member.userId, member.name]),
  );
  const selectedPerson = names.has(person) ? person : "";
  const members =
    value?.members.filter(
      (member) => !selectedPerson || member.userId === selectedPerson,
    ) ?? [];
  const personTasks =
    value?.tasks.filter(
      (task) => !selectedPerson || recipient(task) === selectedPerson,
    ) ?? [];
  const tasks = personTasks.filter((task) =>
    status === "overdue"
      ? task.risk === "OVERDUE"
      : !status || task.status === status,
  );

  return (
    <>
      {renderToolbar(
        value?.start,
        value?.end,
        value && (
          <select
            aria-label="Lọc theo thành viên"
            value={selectedPerson}
            onChange={(event) => setPerson(event.target.value)}
          >
            <option value="">Tất cả thành viên</option>
            {value.members.map((member) => (
              <option key={member.userId} value={member.userId}>
                {member.name}
              </option>
            ))}
          </select>
        ),
      )}
      {error && (
        <p className="warning" role="status">
          {error}{" "}
          <button type="button" className="btn ghost small" onClick={retry}>
            Thử lại
          </button>
        </p>
      )}
      {!value && !error && (
        <p className="workload-message" role="status">
          Đang tải công việc nhóm…
        </p>
      )}
      {value && (
        <section className="team-report" aria-label="Công việc nhóm">
          <TeamReportOverview
            teamName={teamName}
            members={members}
            tasks={personTasks}
            status={status}
            selectedPerson={selectedPerson}
            onStatus={setStatus}
            onPerson={(id) => setPerson(selectedPerson === id ? "" : id)}
          />
          <section className="team-report-card team-report-tasks">
            <header className="team-report-heading team-report-list-heading">
              <div>
                <h3>
                  Tiến độ công việc nhóm{" "}
                  <span className="team-report-result-count">
                    {tasks.length}
                  </span>
                </h3>
                <p>
                  Lịch và Thời Gian Làm Việc do từng thành viên quản lý riêng.
                </p>
              </div>
              <select
                aria-label="Lọc tình trạng công việc nhóm"
                value={status}
                onChange={(event) => setStatus(event.target.value)}
              >
                <option value="">Tất cả tình trạng</option>
                {stages.map((stage) => (
                  <option key={stage.value} value={stage.value}>
                    {stage.label}
                  </option>
                ))}
                <option value="overdue">Quá hạn</option>
              </select>
            </header>
            {tasks.length > 0 ? (
              <div
                className="workload-scroll"
                tabIndex={0}
                aria-label="Danh sách công việc nhóm"
              >
                <table
                  className="workload-table team-report-task-table"
                  role="table"
                >
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
                    {tasks.map((task) => {
                      const progress = Math.max(
                        0,
                        Math.min(100, task.progress || 0),
                      );
                      const tone =
                        task.risk === "OVERDUE"
                          ? "late"
                          : stages.find((stage) => stage.value === task.status)
                              ?.tone;
                      const name = names.get(recipient(task));
                      return (
                        <tr key={task.id} data-tone={tone}>
                          <th scope="row">
                            <button
                              type="button"
                              className="team-report-task-link"
                              onClick={() => openTeamTask(teamId, task.id)}
                            >
                              <span>{task.name}</span>
                              {icon("arrow")}
                            </button>
                          </th>
                          <td className="team-report-task-recipient">
                            <span className="team-report-recipient">
                              {name && <MemberMark name={name} />}
                              {name ||
                                (recipient(task)
                                  ? "Thành viên"
                                  : "Chưa có người nhận")}
                            </span>
                          </td>
                          <td className="team-report-task-deadline">
                            <span
                              className="team-report-mobile-label"
                              aria-hidden="true"
                            >
                              Hạn chót
                            </span>
                            <time dateTime={task.deadline}>
                              {dateFmt(task.deadline)}
                            </time>
                          </td>
                          <td className="team-report-task-estimate">
                            <span
                              className="team-report-mobile-label"
                              aria-hidden="true"
                            >
                              Ước lượng
                            </span>
                            {minutes(task.estimatedMinutes, "long")}
                          </td>
                          <td className="team-report-task-progress">
                            <span
                              className="team-report-mobile-label"
                              aria-hidden="true"
                            >
                              Tiến độ
                            </span>
                            <div className="team-report-progress">
                              <progress
                                max="100"
                                value={progress}
                                aria-label={`Tiến độ ${task.name}`}
                              />
                              <span>{progress}%</span>
                            </div>
                          </td>
                          <td className="team-report-task-status">
                            <span
                              className="team-report-status"
                              title={statuses[task.risk]}
                            >
                              <span
                                className="team-report-stage-dot"
                                aria-hidden="true"
                              />
                              {statuses[task.risk] || "Chưa rõ"}
                            </span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="team-report-empty">
                <p>
                  {personTasks.length
                    ? "Không có việc phù hợp với tình trạng đã chọn."
                    : "Không có công việc cho phạm vi đã chọn trong tuần này."}
                </p>
                {(status || selectedPerson) && (
                  <button
                    type="button"
                    className="btn ghost small"
                    onClick={() => {
                      setStatus("");
                      setPerson("");
                    }}
                  >
                    Xóa bộ lọc
                  </button>
                )}
              </div>
            )}
          </section>
        </section>
      )}
    </>
  );
}
