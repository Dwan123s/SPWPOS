import { useEffect, useRef, useState } from "react";
import { eisenhowerPriority } from "./eisenhower-priority.js";
import { EisenhowerPriorityBadge } from "./eisenhower-priority.jsx";
import { icon, dateFmt, timeFmt, minutes } from "./ui.jsx";
import { TeamTaskDetail } from "./team-task-detail.jsx";
import { TeamRecipientEditor } from "./team-recipient-select.jsx";
import { TaskTablePopup } from "./task-table-popup.jsx";
import { awaitsTeamDeadline, teamAttentionMetrics, teamTaskAttention } from "./team-attention.js";

const statuses = [
  { key: "active", label: "Chưa xong" },
  { key: "Open", label: "Chờ nhận", tone: "open", glyph: "clock" },
  { key: "Assigned", label: "Đang làm", tone: "active", glyph: "play" },
  { key: "PendingReview", label: "Chờ duyệt", tone: "review", glyph: "eye" },
  { key: "Completed", label: "Hoàn thành", tone: "done", glyph: "check" },
  { key: "Split", label: "Đã chia", tone: "split", glyph: "tasks" },
];
const PAGE_SIZE = 8;
const PREVIEW_SIZE = 4;
const matchesStatus = (task, status) =>
  status === "active"
    ? task.status !== "Completed" &&
      !(
        task.status === "Split" &&
        task.subtaskCount > 0 &&
        task.subtasksCompleted === task.subtaskCount
      )
    : task.status === status;
const normalize = (text = "") =>
  text
    .toLocaleLowerCase("vi")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .trim();
const deadline = (task) => Date.parse(task.deadline) || Number.MAX_SAFE_INTEGER;
const needsRevision = (task) =>
  task.status === "Assigned" && task.completionApproved === false;
const isMyTask = (task, uid) =>
  !!uid &&
  (task.assigneeId === uid ||
    (!task.assigneeId && task.designatedUserId === uid) ||
    (task.availabilityCheck?.userId === uid &&
      task.availabilityCheck.canTake !== false));
const isSharedToClaim = (task, uid, now) =>
  task.status === "Open" &&
  !task.assigneeId &&
  !task.designatedUserId &&
  !awaitsTeamDeadline(task, now) &&
  !isMyTask(task, uid);

export function TaskBoard({
  detail,
  uid,
  busy,
  onCompose,
  error,
  onDialogChange,
  onExecutionChanged,
  taskToOpen,
  onTaskOpened,
  attentionToOpen,
  now = Date.now(),
  ...actions
}) {
  const leader = detail.team.role === "Leader";
  const [scope, setScope] = useState("split");
  const [attentionId, setAttentionId] = useState(null);
  const [status, setStatus] = useState("active");
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState("deadline");
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState(null);
  const [requestedTask, setRequestedTask] = useState(null);
  const heading = useRef(null);
  useEffect(() => {
    if (!attentionToOpen || !leader) return;
    setScope("attention");
    setAttentionId(attentionToOpen.id);
    setQuery("");
    setSort("deadline");
    setPage(1);
    heading.current?.focus();
  }, [attentionToOpen, leader]);
  useEffect(() => {
    if (!taskToOpen) return;
    setSelectedId(taskToOpen.taskId);
    setRequestedTask(taskToOpen);
    onTaskOpened();
  }, [taskToOpen, onTaskOpened]);
  const selectedTask = detail.tasks.find((task) => task.id === selectedId);
  const tasksById = new Map(detail.tasks.map((task) => [task.id, task]));
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
  const attentionById = new Map(
    detail.tasks.map((task) => [task.id, teamTaskAttention(task, now)]),
  );
  const attentionCount = [...attentionById.values()].filter(Boolean).length;
  const attentionMetric =
    scope === "attention" &&
    teamAttentionMetrics.find((metric) => metric.id === attentionId);
  const matching = detail.tasks.filter((task) =>
    normalize(
      `${task.name} ${task.description || ""} ${members.get(task.assigneeId)?.name || ""} ${members.get(task.designatedUserId)?.name || ""}`,
    ).includes(search),
  );
  const scoped = matching.filter(
    (task) =>
      (scope !== "attention" || (leader && attentionById.get(task.id))) &&
      (!attentionMetric ||
        attentionById.get(task.id)?.id === attentionMetric.id) &&
      (scope !== "mine" || (isMyTask(task, uid) && !awaitsTeamDeadline(task, now))) &&
      (scope !== "available" || isSharedToClaim(task, uid, now)) &&
      (scope !== "overdue" || awaitsTeamDeadline(task, now)),
  );
  const compare = (view) => (a, b) =>
    (view === "attention"
      ? attentionById.get(a.id).rank - attentionById.get(b.id).rank
      : 0) ||
    (view === "mine"
      ? Number(needsRevision(b)) - Number(needsRevision(a))
      : 0) ||
    (sort === "priority" ? eisenhowerPriority(a, now).rank - eisenhowerPriority(b, now).rank : 0) ||
    deadline(a) - deadline(b) ||
    a.name.localeCompare(b.name, "vi") ||
    a.id.localeCompare(b.id);
  const tasks = scoped
    .filter((task) => scope === "attention" || matchesStatus(task, status))
    .sort(compare(scope));
  const pages = Math.max(1, Math.ceil(tasks.length / PAGE_SIZE));
  const currentPage = Math.min(page, pages);
  const start = (currentPage - 1) * PAGE_SIZE;
  const emptyMine = scope === "mine" && status === "active" && !search;
  useEffect(() => {
    setPage((previous) => Math.min(previous, pages));
  }, [pages]);
  // Every filter starts at the first page; polling can also shrink the last page.
  function change(setter, value) {
    setter(value);
    setPage(1);
  }
  function chooseScope(next) {
    change(setScope, next);
    setAttentionId(null);
    setStatus(["available", "overdue"].includes(next) ? "Open" : "active");
  }
  function taskRow(task, view) {
    return (
      <TaskRow
        key={task.id}
        task={task}
        parentName={tasksById.get(task.parentTaskId)?.name}
        uid={uid}
        busy={busy}
        leader={leader}
        members={detail.members}
        preferClaim={view === "available"}
        attentionReason={
          view === "attention" ? attentionById.get(task.id)?.label : null
        }
        overdue={attentionById.get(task.id)?.id === "overdue"}
        awaitingDeadline={awaitsTeamDeadline(task, now)}
        assignee={members.get(task.assigneeId)}
        designated={members.get(task.designatedUserId)}
        onOpen={() => setSelectedId(task.id)}
        {...actions}
      />
    );
  }
  const overdueTasks = matching.filter((task) => awaitsTeamDeadline(task, now)).sort(compare("overdue"));
  const splitSections =
    scope === "split"
      ? [
          ...(overdueTasks.length ? [{
            id: "overdue",
            title: "Quá hạn · Chưa có người nhận",
            glyph: "alertCircle",
            tasks: overdueTasks,
          }] : []),
          {
            id: "mine",
            title: "Việc của tôi",
            glyph: "userPlus",
            tasks: matching
              .filter(
                (task) => isMyTask(task, uid) && matchesStatus(task, "active") && !awaitsTeamDeadline(task, now),
              )
              .sort(compare("mine")),
            empty: "Bạn chưa có việc được giao hoặc đã nhận.",
          },
          {
            id: "available",
            title: "Có thể nhận",
            glyph: "tasks",
            tasks: matching
              .filter((task) => isSharedToClaim(task, uid, now))
              .sort(compare("available")),
            empty: "Hiện không có việc mở cho cả nhóm nhận.",
          },
        ]
      : [];
  const toolbar = detail.tasks.length > 0 && (
    <div className="team-board-toolbar">
      <div
        className="team-board-views"
        role="group"
        aria-label="Phạm vi công việc"
      >
        {[
          ...(scope !== "split"
            ? [{ id: "split", name: "Bảng công việc" }]
            : []),
          { id: "mine", name: "Việc của tôi" },
          { id: "available", name: "Có thể nhận" },
          ...(scope === "overdue" ? [{ id: "overdue", name: "Quá hạn · Chưa có người nhận" }] : []),
          ...(leader && scope !== "split"
            ? [{ id: "attention", name: "Cần xử lý" }]
            : []),
          { id: "all", name: "Cả nhóm" },
        ].map((item) => (
          <button
            key={item.id}
            type="button"
            className={scope === item.id ? "active" : ""}
            aria-pressed={scope === item.id}
            onClick={() => chooseScope(item.id)}
          >
            {item.name}
            {item.id === "attention" && attentionCount > 0 && (
              <span className="team-attention-count" aria-hidden="true">
                {attentionCount}
              </span>
            )}
          </button>
        ))}
      </div>
      {attentionMetric && (
        <div className="team-board-attention" aria-live="polite">
          <span>
            {icon(attentionMetric.glyph)} {attentionMetric.label}
          </span>
          <button type="button" onClick={() => change(setAttentionId, null)}>
            Bỏ lọc {icon("close")}
          </button>
        </div>
      )}
      <div className="team-board-controls">
        <label className="team-search">
          {icon("search")}
          <input
            aria-label="Tìm task trong nhóm"
            value={query}
            onChange={(event) => change(setQuery, event.target.value)}
            placeholder="Tìm việc, thành viên…"
          />
        </label>
        {scope !== "split" &&
          scope !== "attention" &&
          scope !== "overdue" &&
          scope !== "available" && (
            <label className="team-board-field">
              <span>Trạng thái</span>
              <select
                aria-label="Lọc trạng thái"
                value={status}
                onChange={(event) => change(setStatus, event.target.value)}
              >
                {statuses.map((item) => (
                  <option key={item.key} value={item.key}>
                    {item.label} (
                    {
                      scoped.filter((task) => matchesStatus(task, item.key))
                        .length
                    }
                    )
                  </option>
                ))}
              </select>
            </label>
          )}
        <label className="team-board-field">
          <span>Sắp xếp</span>
          <select
            aria-label="Sắp xếp công việc"
            value={sort}
            onChange={(event) => change(setSort, event.target.value)}
          >
            <option value="deadline">Hạn gần nhất</option>
            <option value="priority">Ưu tiên Eisenhower</option>
          </select>
        </label>
      </div>
    </div>
  );
  return (
    <section
      className="team-board team-board-compact"
      aria-label="Công việc nhóm"
    >
      <div className="team-board-heading">
        <div>
          <h3 tabIndex={-1} ref={heading}>
            Bảng công việc{" "}
            {detail.tasks.length > 0 && (
              <span className="team-count">{detail.tasks.length}</span>
            )}
          </h3>
        </div>
        {scope === "split" && leader && detail.tasks.length > 0 && (
          <button
            type="button"
            className="btn secondary team-board-review-shortcut"
            onClick={() => {
              chooseScope("attention");
              heading.current?.focus();
            }}
          >
            {icon("eye")} Cần xử lý
            {attentionCount > 0 && (
              <span className="team-attention-count" aria-hidden="true">
                {attentionCount}
              </span>
            )}
          </button>
        )}
      </div>
      {scope !== "split" && toolbar}
      {!detail.tasks.length ? (
        <div className="team-board-empty">
          <span aria-hidden="true">{icon("tasks")}</span>
          <div>
            <h4>Nhóm chưa có công việc</h4>
            <p>
              {leader
                ? "Đăng việc đầu tiên cho cả nhóm hoặc chỉ định một thành viên."
                : "Khi trưởng nhóm đăng việc, bạn có thể xem và nhận việc tại đây."}
            </p>
          </div>
          {leader && (
            <button className="btn primary" disabled={busy} onClick={onCompose}>
              {icon("plus")} Đăng việc mới
            </button>
          )}
        </div>
      ) : scope === "split" ? null : !tasks.length ? (
        <div className="team-board-no-results" role="status">
          {icon("search")}
          <strong>
            {attentionMetric && !search
              ? "Không còn việc trong mục này"
              : emptyMine
                ? "Bạn chưa có việc đang thực hiện"
                : scope === "attention" && !search
                  ? "Không có việc cần xử lý"
                  : "Không có công việc phù hợp"}
          </strong>
          <button
            type="button"
            className="team-text-button"
            onClick={() => {
              setScope(emptyMine ? "available" : "all");
              setAttentionId(null);
              setStatus(emptyMine ? "Open" : "active");
              setQuery("");
              setPage(1);
            }}
          >
            {emptyMine
              ? "Xem việc có thể nhận"
              : scope === "attention" && !search
                ? "Xem tất cả công việc"
                : "Đặt lại bộ lọc"}
          </button>
        </div>
      ) : (
        <>
          <div className="team-task-list">
            <div className="team-task-list-head" aria-hidden="true">
              <span>Công việc</span>
              <span>Hạn chót</span>
              <span>Người nhận</span>
              <span>Thao tác</span>
            </div>
            {tasks
              .slice(start, start + PAGE_SIZE)
              .map((task) => taskRow(task, scope))}
          </div>
          <nav
            className="team-board-pagination"
            aria-label="Phân trang công việc"
          >
            <span>
              {start + 1}–{Math.min(start + PAGE_SIZE, tasks.length)} /{" "}
              {tasks.length} công việc
            </span>
            {pages > 1 && (
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
            )}
          </nav>
        </>
      )}
      {scope === "split" && (
        <div className="team-board-split">
          {splitSections.map((section) => (
            <section
              key={section.id}
              className={`team-board-split-section ${section.id}`}
              aria-label={section.title}
            >
              <header className="team-board-split-heading">
                <span className="team-board-split-icon">
                  {icon(section.glyph)}
                </span>
                <h4>{section.title}</h4>
                <span className="team-board-split-count">
                  {section.tasks.length}
                </span>
                {section.tasks.length > 1 && (
                  <button
                    type="button"
                    data-mobile-only={
                      section.tasks.length <= PREVIEW_SIZE || undefined
                    }
                    aria-label={`Xem tất cả · ${section.title}`}
                    onClick={() => {
                      chooseScope(section.id);
                      heading.current?.focus();
                    }}
                  >
                    Xem tất cả {icon("arrow")}
                  </button>
                )}
              </header>
              {section.tasks.length ? (
                <div className="team-board-split-list">
                  {section.tasks
                    .slice(0, PREVIEW_SIZE)
                    .map((task) => taskRow(task, section.id))}
                </div>
              ) : (
                <p className="team-board-split-empty">
                  {search ? "Không có việc khớp tìm kiếm." : section.empty}
                </p>
              )}
            </section>
          ))}
        </div>
      )}
      {scope === "split" && toolbar}
      {selectedTask && (
        <TeamTaskDetail
          key={selectedTask.id}
          task={selectedTask}
          teamId={detail.team.id}
          onExecutionChanged={onExecutionChanged}
          selectedSessionId={requestedTask?.taskId === selectedTask.id ? requestedTask.selectedSessionId : undefined}
          initialSection={requestedTask?.taskId === selectedTask.id ? requestedTask.initialSection : undefined}
          parent={tasksById.get(selectedTask.parentTaskId)}
          childrenTasks={detail.tasks.filter(
            (task) => task.parentTaskId === selectedTask.id,
          )}
          onOpenTask={(task) => setSelectedId(task.id)}
          uid={uid}
          busy={busy}
          leader={detail.team.role === "Leader"}
          assignee={members.get(selectedTask.assigneeId)}
          designated={members.get(selectedTask.designatedUserId)}
          members={detail.members}
          reviewer={members.get(selectedTask.completionReviewedBy)}
          awaitingDeadline={awaitsTeamDeadline(selectedTask, now)}
          error={error}
          onClose={() => {
            setSelectedId(null);
            setRequestedTask(null);
          }}
          {...actions}
        />
      )}
    </section>
  );
}

function TaskRow({
  task,
  parentName,
  uid,
  busy,
  leader,
  assignee,
  designated,
  members,
  preferClaim,
  attentionReason,
  overdue,
  onOpen,
  onClaim,
  onProgress,
  onReviewCompletion,
  onSplit,
  onRecipientChange,
  onRequestAvailability,
  onExtendDeadline,
  awaitingDeadline,
}) {
  const [editingRecipient, setEditingRecipient] = useState(false);
  const [menuAnchor, setMenuAnchor] = useState(null);
  const mine = task.assigneeId === uid;
  const open = task.status === "Open" && !task.assigneeId;
  const canClaim =
    open &&
    !awaitingDeadline &&
    (!task.designatedUserId || task.designatedUserId === uid) &&
    onClaim;
  const canAssign = leader && open && !awaitingDeadline && onRecipientChange;
  const canSplit = leader && open && !awaitingDeadline && !task.parentTaskId && onSplit;
  const check = task.availabilityCheck;
  const checkingMySchedule =
    check && check.userId === uid && check.canTake == null;
  const toggleRecipient = () => setEditingRecipient((value) => !value);
  const details = { label: "Chi tiết", glyph: "arrow", run: onOpen };
  const assign = canAssign && {
    label: editingRecipient ? "Đóng chọn người nhận" : "Chỉ định người nhận",
    glyph: "userPlus",
    run: toggleRecipient,
  };
  const claim = canClaim && {
    label: checkingMySchedule
      ? "Kiểm tra lịch & phản hồi"
      : "Xem lịch trước khi nhận",
    glyph: "calendar",
    run: () => onClaim(task),
  };
  const workActions = [];
  if (leader && awaitingDeadline && onExtendDeadline)
    workActions.push({ label: "Gia hạn", glyph: "calendar", run: () => onExtendDeadline(task) });
  const assignFirst =
    assign &&
    (!claim ||
      (!preferClaim && !checkingMySchedule && task.designatedUserId !== uid));
  if (assignFirst) workActions.push(assign);
  if (claim) workActions.push(claim);
  if (assign && !assignFirst) workActions.push(assign);
  if (mine && task.status === "Assigned" && onProgress)
    workActions.push({
      label: "Gửi duyệt",
      accessibleLabel: "Báo hoàn thành · Gửi duyệt",
      glyph: "check",
      run: () => onProgress(task, 100),
    });
  if (leader && task.status === "PendingReview" && onReviewCompletion)
    workActions.push({
      label: "Kiểm tra & duyệt",
      glyph: "eye",
      run: () => onReviewCompletion(task),
    });
  const [primary = details, ...extra] = workActions;
  if (canSplit)
    extra.push({
      label: "Chia thành 2 việc con",
      glyph: "tasks",
      run: () => onSplit(task),
    });
  if (primary !== details) extra.push(details);

  const status =
    statuses.find((item) => item.key === task.status) || statuses[1];
  const hasDeadline = Number.isFinite(Date.parse(task.deadline));
  const ownerName = task.assigneeId
    ? mine
      ? "Bạn"
      : assignee?.name || "Thành viên"
    : task.designatedUserId
      ? task.designatedUserId === uid
        ? "Chờ bạn nhận"
        : "Chờ " + (designated?.name || "thành viên được chỉ định") + " nhận"
      : "Chưa có người nhận";
  const split = task.status === "Split";
  const closeMenu = (restoreFocus = true) => {
    setMenuAnchor(null);
    if (restoreFocus) menuAnchor?.focus({ preventScroll: true });
  };

  return (
    <article
      className="team-task-row"
      aria-label={task.name}
      onClick={(event) => {
        if (
          event.currentTarget.contains(event.target) &&
          !event.target.closest("button, a, input, select, textarea")
        )
          onOpen();
      }}
    >
      <div className="team-task-row-main">
        <div className="team-task-row-title">
          <h4>
            <button type="button" aria-haspopup="dialog" onClick={onOpen}>
              {task.name}
            </button>
          </h4>
          <div className="team-task-topline">
            <span className={"team-row-status " + status.tone}>
              {icon(status.glyph)} {status.label}
            </span>
            <EisenhowerPriorityBadge task={task} />
            {needsRevision(task) && (
              <span className="team-row-tag needs-revision">
                {icon("edit")} Cần bổ sung
              </span>
            )}
            {task.hasPendingChange && (
              <span className="team-row-tag">
                {icon("refresh")} Có yêu cầu mới
              </span>
            )}
          </div>
          {parentName && (
            <p className="team-row-caption">Việc con của {parentName}</p>
          )}
          {mine && task.checklist?.length > 0 && (
            <p className="team-row-caption">
              Checklist {task.checklist.filter((item) => item.completed).length}
              /{task.checklist.length}
            </p>
          )}
          {attentionReason && !overdue && !check && (
            <p className="team-row-caption">{attentionReason}</p>
          )}
          {!leader && task.status === "PendingReview" && (
            <p className="team-row-caption">Chờ trưởng nhóm xác nhận</p>
          )}
          {awaitingDeadline && !leader && <p className="team-row-caption">Chờ trưởng nhóm gia hạn</p>}
          {check && !awaitingDeadline && (
            <p className="team-row-caption">
              {leader &&
                (members.find((member) => member.userId === check.userId)
                  ?.name || "Thành viên") + " · "}
              {check.canTake == null
                ? "Chờ phản hồi kiểm tra lịch"
                : check.canTake
                  ? "Có thể nhận"
                  : "Chưa thể nhận"}
            </p>
          )}
        </div>
        <div className="team-task-row-deadline">
          <span className="team-task-field-label">Hạn chót</span>
          {hasDeadline ? (
            <time dateTime={task.deadline}>
              {dateFmt(task.deadline)} · {timeFmt(task.deadline)}
            </time>
          ) : (
            <span>Chưa đặt hạn</span>
          )}
          <span className="team-task-estimate">
            {icon(split ? "tasks" : "clock")}
            {split
              ? task.subtasksCompleted +
                "/" +
                task.subtaskCount +
                " việc con đã xong"
              : minutes(task.estimatedMinutes, "long")}
          </span>
          {overdue && (
            <span className="team-row-tag overdue">
              {icon("alertCircle")} Quá hạn
            </span>
          )}
        </div>
        <div className="team-task-row-owner">
          <span className="team-task-field-label">Người nhận</span>
          <span className="team-task-owner-name">
            {icon("users")}{" "}
            <span>{split ? "Theo từng việc con" : ownerName}</span>
          </span>
          {task.assigneeId && (
            <div className="team-row-progress">
              <progress
                value={task.progress}
                max={100}
                aria-label="Tiến độ công việc"
              />
              <span>{task.progress}%</span>
            </div>
          )}
        </div>
        <div className="team-task-row-actions">
          <button
            type="button"
            className={"btn " + (primary === details ? "secondary" : "primary")}
            disabled={busy}
            aria-label={primary.accessibleLabel}
            aria-haspopup={primary === details ? "dialog" : undefined}
            aria-expanded={primary === assign ? editingRecipient : undefined}
            onClick={primary.run}
          >
            {icon(primary.glyph)} {primary.label}
          </button>
          {extra.length > 0 && (
            <button
              type="button"
              className="team-row-more"
              aria-haspopup="dialog"
              aria-expanded={!!menuAnchor}
              aria-label="Thêm thao tác"
              title="Thêm thao tác"
              disabled={busy}
              onClick={(event) =>
                menuAnchor ? closeMenu() : setMenuAnchor(event.currentTarget)
              }
            >
              <span aria-hidden="true">⋯</span>
            </button>
          )}
        </div>
      </div>
      {menuAnchor && extra.length > 0 && (
        <TaskTablePopup
          anchor={menuAnchor}
          kind="team-actions"
          title="Thao tác công việc"
          onClose={closeMenu}
        >
          {extra.map((action, index) => (
            <button
              type="button"
              key={action.label}
              className="task-table-menu-item"
              data-popup-autofocus={index === 0 ? "" : undefined}
              disabled={busy}
              onClick={() => {
                closeMenu();
                action.run();
              }}
            >
              {icon(action.glyph)} {action.label}
            </button>
          ))}
        </TaskTablePopup>
      )}
      {editingRecipient && canAssign && (
        <div onClick={(event) => event.stopPropagation()}>
          <TeamRecipientEditor
            key={task.version}
            task={task}
            members={members}
            busy={busy}
            onSave={onRecipientChange}
            onRequestAvailability={onRequestAvailability}
          />
        </div>
      )}
    </article>
  );
}
