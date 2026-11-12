import { useEffect, useState } from "react";
import { icon, dateFmt, timeFmt } from "./ui.jsx";
import { TeamTaskDetail } from "./team-task-detail.jsx";

const statuses = [
  { key: "active", label: "Chưa hoàn thành" },
  { key: "Open", label: "Chờ nhận", tone: "open" },
  { key: "Assigned", label: "Đang làm", tone: "active" },
  { key: "PendingReview", label: "Chờ duyệt", tone: "review" },
  { key: "Completed", label: "Hoàn thành", tone: "done" },
];
const PAGE_SIZE = 8;
const matchesStatus = (task, status) =>
  status === "active" ? task.status !== "Completed" : task.status === status;
const normalize = (text = "") =>
  text
    .toLocaleLowerCase("vi")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .trim();
const deadline = (task) => Date.parse(task.deadline) || Number.MAX_SAFE_INTEGER;

export function TaskBoard({
  detail,
  uid,
  busy,
  onCompose,
  error,
  onDialogChange,
  taskToOpen,
  onTaskOpened,
  ...actions
}) {
  const [scope, setScope] = useState("all");
  const [status, setStatus] = useState("active");
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState("deadline");
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState(null);
  useEffect(() => {
    if (!taskToOpen) return;
    setSelectedId(taskToOpen.taskId);
    onTaskOpened();
  }, [taskToOpen, onTaskOpened]);
  const selectedTask = detail.tasks.find((task) => task.id === selectedId);
  const dialogOpen = !!selectedTask;
  useEffect(() => {
    onDialogChange?.(dialogOpen);
    return () => onDialogChange?.(false);
  }, [dialogOpen, onDialogChange]);
  useEffect(() => {
    if (selectedId && !selectedTask) setSelectedId(null);
  }, [selectedId, selectedTask]);
  const members = new Map(
    detail.members.map((member) => [member.userId, member]),
  );
  const search = normalize(query);
  const scoped = detail.tasks.filter(
    (task) =>
      (scope !== "mine" ||
        task.assigneeId === uid ||
        (!task.assigneeId && task.designatedUserId === uid)) &&
      (scope !== "available" ||
        (!task.assigneeId &&
          task.status === "Open" &&
          (!task.designatedUserId || task.designatedUserId === uid))) &&
      normalize(
        `${task.name} ${task.description || ""} ${members.get(task.assigneeId)?.name || ""} ${members.get(task.designatedUserId)?.name || ""}`,
      ).includes(search),
  );
  const tasks = scoped
    .filter((task) => matchesStatus(task, status))
    .sort(
      (a, b) =>
        (sort === "priority" ? (b.priority || 2) - (a.priority || 2) : 0) ||
        deadline(a) - deadline(b) ||
        a.name.localeCompare(b.name, "vi") ||
        a.id.localeCompare(b.id),
    );
  const pages = Math.max(1, Math.ceil(tasks.length / PAGE_SIZE));
  const currentPage = Math.min(page, pages);
  const start = (currentPage - 1) * PAGE_SIZE;
  useEffect(() => {
    setPage((previous) => Math.min(previous, pages));
  }, [pages]);
  // Every filter starts at the first page; polling can also shrink the last page.
  function change(setter, value) {
    setter(value);
    setPage(1);
  }
  return (
    <section
      className="team-board team-board-compact"
      aria-label="Công việc nhóm"
    >
      <div className="team-board-heading">
        <div>
          <span className="team-eyebrow">CÔNG VIỆC CHUNG</span>
          <h3>
            Bảng công việc{" "}
            <span className="team-count">{detail.tasks.length}</span>
          </h3>
        </div>
        <label className="team-search">
          {icon("search")}
          <input
            aria-label="Tìm task trong nhóm"
            value={query}
            onChange={(e) => change(setQuery, e.target.value)}
            placeholder="Tìm việc, thành viên…"
          />
        </label>
      </div>
      <div className="team-board-toolbar">
        <div className="team-board-tabs" aria-label="Lọc công việc">
          {[
            { id: "all", name: "Tất cả" },
            { id: "available", name: "Có thể nhận" },
            { id: "mine", name: "Việc của tôi" },
          ].map((item) => (
            <button
              key={item.id}
              type="button"
              className={scope === item.id ? "active" : ""}
              aria-pressed={scope === item.id}
              onClick={() => {
                change(setScope, item.id);
                if (item.id === "available") setStatus("Open");
              }}
            >
              {item.name}
            </button>
          ))}
        </div>
        <label className="team-board-sort">
          <span>Sắp xếp</span>
          <select
            aria-label="Sắp xếp công việc"
            value={sort}
            onChange={(e) => change(setSort, e.target.value)}
          >
            <option value="deadline">Hạn gần nhất</option>
            <option value="priority">Ưu tiên cao</option>
          </select>
        </label>
      </div>
      <div className="team-status-filters" aria-label="Lọc trạng thái">
        {statuses.map((item) => (
          <button
            type="button"
            key={item.key}
            className={`${item.tone || "all"}${status === item.key ? " selected" : ""}`}
            aria-pressed={status === item.key}
            onClick={() => change(setStatus, item.key)}
          >
            <span className="team-status-dot" />
            {item.label}
            <strong>
              {scoped.filter((task) => matchesStatus(task, item.key)).length}
            </strong>
          </button>
        ))}
      </div>
      {!detail.tasks.length ? (
        <div className="team-board-empty">
          <span>{icon("tasks")}</span>
          <h3>Công việc đầu tiên đang chờ bạn</h3>
          <p>
            {detail.team.role === "Leader"
              ? "Nêu công việc và hạn chót, sau đó chọn mở cho cả nhóm hoặc chỉ định một người."
              : "Khi có việc phù hợp, bạn có thể xem phương án NORA xếp trên lịch riêng trước khi nhận."}
          </p>
          {detail.team.role === "Leader" && (
            <button className="btn primary" disabled={busy} onClick={onCompose}>
              {icon("spark")} Tạo task cùng NORA
            </button>
          )}
        </div>
      ) : !tasks.length ? (
        <div className="team-board-no-results" role="status">
          {icon("search")}
          <strong>Không có công việc phù hợp</strong>
          <p>Thử trạng thái khác hoặc thay đổi từ khóa tìm kiếm.</p>
          <button
            type="button"
            className="team-text-button"
            onClick={() => {
              setScope("all");
              setStatus("active");
              setQuery("");
              setPage(1);
            }}
          >
            Đặt lại bộ lọc
          </button>
        </div>
      ) : (
        <>
          <div className="team-task-list">
            {tasks.slice(start, start + PAGE_SIZE).map((task) => (
              <TaskRow
                key={task.id}
                task={task}
                uid={uid}
                busy={busy}
                leader={detail.team.role === "Leader"}
                assignee={members.get(task.assigneeId)}
                designated={members.get(task.designatedUserId)}
                onOpen={() => setSelectedId(task.id)}
                {...actions}
              />
            ))}
          </div>
          <nav
            className="team-board-pagination"
            aria-label="Phân trang công việc"
          >
            <span>
              {start + 1}–{Math.min(start + PAGE_SIZE, tasks.length)} /{" "}
              {tasks.length} công việc
            </span>
            <div>
              <button
                type="button"
                aria-label="Trang công việc trước"
                disabled={currentPage === 1}
                onClick={() => setPage(currentPage - 1)}
              >
                ←
              </button>
              <span aria-live="polite">
                Trang {currentPage} / {pages}
              </span>
              <button
                type="button"
                aria-label="Trang công việc tiếp"
                disabled={currentPage === pages}
                onClick={() => setPage(currentPage + 1)}
              >
                →
              </button>
            </div>
          </nav>
        </>
      )}
      {selectedTask && (
        <TeamTaskDetail
          task={selectedTask}
          uid={uid}
          busy={busy}
          leader={detail.team.role === "Leader"}
          assignee={members.get(selectedTask.assigneeId)}
          designated={members.get(selectedTask.designatedUserId)}
          members={detail.members}
          reviewer={members.get(selectedTask.completionReviewedBy)}
          error={error}
          onClose={() => setSelectedId(null)}
          {...actions}
        />
      )}
    </section>
  );
}

function TaskRow({
  task,
  uid,
  busy,
  leader,
  assignee,
  designated,
  onOpen,
  onClaim,
  onProgress,
  onReviewCompletion,
}) {
  const mine = task.assigneeId === uid;
  const status =
    statuses.find((item) => item.key === task.status) || statuses[1];
  return (
    <article
      className={`team-task-row ${status.tone}`}
      aria-label={task.name}
      onClick={(event) => {
        if (!event.target.closest("button, a, input, select, textarea"))
          onOpen();
      }}
    >
      <div className="team-task-row-main">
        <div className="team-task-row-title">
          <div className="team-task-topline">
            <span className={`team-row-status ${status.tone}`}>
              <span className="team-status-dot" />
              {status.label}
            </span>
            {task.priority >= 3 && (
              <span className={`team-priority priority-${task.priority}`}>
                {task.priority === 4 ? "Khẩn cấp" : "Ưu tiên cao"}
              </span>
            )}
            {task.hasPendingChange && (
              <span className="team-change-badge">Có yêu cầu mới</span>
            )}
            {task.designatedUserId && !task.assigneeId && (
              <span className="team-designated-badge">
                {task.designatedUserId === uid ? "Giao cho bạn" : "Chỉ định"}
              </span>
            )}
          </div>
          <h4>
            <button type="button" aria-haspopup="dialog" onClick={onOpen}>
              {task.name}
            </button>
          </h4>
          <div className="team-task-row-facts">
            <span>
              {icon("calendar")}
              {dateFmt(task.deadline)} · {timeFmt(task.deadline)}
            </span>
            <span>
              {icon("clock")}
              {task.estimatedMinutes} phút
            </span>
          </div>
        </div>
        <div className="team-task-row-owner">
          <span>
            {task.assigneeId
              ? mine
                ? "Bạn"
                : assignee?.name || "Thành viên"
              : task.designatedUserId
                ? task.designatedUserId === uid
                  ? "Chờ bạn nhận"
                  : `Chờ ${designated?.name || "thành viên được chỉ định"} nhận`
                : "Chưa có người nhận"}
          </span>
          {task.assigneeId && (
            <div className="team-row-progress">
              <div className="team-progress-track">
                <i style={{ width: `${task.progress}%` }} />
              </div>
              <strong>{task.progress}%</strong>
            </div>
          )}
        </div>
        <div className="team-task-row-actions">
          {!task.assigneeId &&
            task.status === "Open" &&
            (!task.designatedUserId || task.designatedUserId === uid) && (
              <button
                className="btn primary"
                disabled={busy}
                onClick={() => onClaim(task)}
                aria-label="Xem lịch trước khi nhận"
              >
                {icon("spark")} Xem lịch trước khi nhận
              </button>
            )}
          {mine && task.status === "Assigned" && (
            <button
              className="btn primary"
              disabled={busy}
              onClick={() => onProgress(task, 100)}
              aria-label="Báo hoàn thành · Gửi duyệt"
            >
              {icon("check")} Gửi duyệt
            </button>
          )}
          {leader && task.status === "PendingReview" && (
            <button
              className="btn primary"
              disabled={busy}
              onClick={() => onReviewCompletion(task)}
            >
              Kiểm tra & duyệt
            </button>
          )}
          <button
            type="button"
            className="team-row-expand"
            aria-haspopup="dialog"
            onClick={onOpen}
          >
            Chi tiết
            <span aria-hidden="true">↗</span>
          </button>
        </div>
      </div>
      {task.status === "PendingReview" && (
        <p className="team-row-caption">
          {icon("clock")} Chờ trưởng nhóm xác nhận
        </p>
      )}
      {task.status === "Assigned" && task.completionApproved === false && (
        <p className="team-row-caption changes-requested">
          Cần bổ sung trước khi hoàn thành
        </p>
      )}
    </article>
  );
}
