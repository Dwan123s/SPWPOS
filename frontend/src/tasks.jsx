import { TaskEisenhowerDetail } from "./eisenhower.jsx";
import { Fragment, useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Select, Slot, nodeList } from "./react-ui.jsx";
import { setSlot } from "./store.js";
import {
  state,
  icon,
  button,
  badge,
  statusLabels,
  typeLabels,
  dateFmt,
  timeFmt,
  minutes,
  localInput,
  toUtc,
  field,
  modal,
  empty,
  dayKey,
  toast,
} from "./ui.jsx";
import { api } from "./api.js";
import { askNora, secretaryMode } from "./agent-handoff.js";
import { TaskBatchForm } from "./task-batch.jsx";
import { TaskChecklist } from "./task-checklist.jsx";
import { parseTaskNotes, TaskNoteContent } from "./team-task-notes.jsx";
import { readTeamBriefFile, TEAM_BRIEF_ACCEPT } from "./team-brief-file.js";
import { orderProjectChildren, plannedTaskStarts } from "./task-smart-lists.js";
import { TaskDeadlinePicker, TaskEstimateEditor, TaskTablePopup } from "./task-table-popup.jsx";
import { useOutsideDismiss } from "./popup-dismissal.jsx";
import { sessionStartIssue } from "./session-start.jsx";
import "./task-create.css";
import "./task-detail-drawer.css";
export const taskById = (id) => state.workspace.tasks.find((t) => t.id === id);
export const hasPlanningDetails = (task) => Boolean(task?.deadline && task.estimatedMinutes > 0);
export const taskColumns = {
  source: "Nguồn",
  planned: "Dự kiến",
  estimate: "Thời lượng",
  deadline: "Hạn chót",
  priority: "Ưu tiên",
};
const taskSources = {
  NATURAL_LANGUAGE: "Nhập tự nhiên",
  AI_BREAKDOWN: "Việc con",
};
const taskPopupTitles = {
  actions: "Thao tác công việc",
  deadline: "Chọn hạn chót",
  estimate: "Chỉnh thời lượng dự kiến",
};
export function taskRows(tasks, plannedDays, options = {}) {
  return <TaskList tasks={tasks} plannedDays={plannedDays} {...options} />;
}

function groupProjectTasks(tasks, projectContext, plannedStarts) {
  if (!projectContext) return tasks.map((task) => ({ task, children: [] }));

  const parents = new Set(projectContext.filter((task) => !task.parentId).map((task) => task.id));
  const matches = new Set(tasks.map((task) => task.id));
  const children = new Map();
  for (const task of tasks) {
    if (!parents.has(task.parentId)) continue;
    if (!children.has(task.parentId)) children.set(task.parentId, []);
    children.get(task.parentId).push(task);
  }

  return projectContext
    .filter((task) => children.has(task.id) || (matches.has(task.id) && !parents.has(task.parentId)))
    .map((task) => ({ task, children: children.has(task.id) ? orderProjectChildren(children.get(task.id), plannedStarts) : [] }));
}

function TaskList({ tasks, plannedDays, plannedStarts, columns = Object.keys(taskColumns), addTask, emptyMessage,
  selectedId, onSelect, toolbarHost, onWorkspaceChanged, projectContext, revealMatchingChildren = false }) {
  const groups = groupProjectTasks(tasks, projectContext, plannedStarts);
  const signature = groups.map(({ task }) => task.id).join(",");
  const [paging, setPaging] = useState({ signature, page: 0 });
  const [toggledProjects, setToggledProjects] = useState(() => new Set());
  const [openChecklists, setOpenChecklists] = useState(() => new Set());
  const checklistPrefix = useId();
  const page = paging.signature === signature ? paging.page : 0;
  const pages = Math.ceil(groups.length / 12);
  const projectKey = (id) => revealMatchingChildren ? `filtered:${id}` : id;
  const visible = groups.slice(page * 12, (page + 1) * 12).flatMap(({ task, children }) => {
    const expanded = revealMatchingChildren !== toggledProjects.has(projectKey(task.id));
    const row = { task, childCount: children.length, expanded };
    return children.length && expanded
      ? [row, ...children.map((child) => ({ task: child, nested: true }))]
      : [row];
  });
  const includes = (column) => columns.includes(column);
  const columnCount = columns.length + 2;
  const [localSelected, setLocalSelected] = useState(null);
  const [popup, setPopup] = useState(null);
  const selectedRow = useRef(null);
  const selectionToolbar = useRef(null);
  const popupRoot = useRef(null);
  const selected = visible.find(({ task }) => task.id === (onSelect ? selectedId : localSelected))?.task;
  const selectTask = onSelect || setLocalSelected;
  const today = dayKey(Date.now());
  function showPopup(event, task, kind) {
    selectTask(task.id);
    const anchor = event.currentTarget;
    setPopup((current) => current?.task.id === task.id && current.kind === kind ? null : { kind, anchor, task });
  }
  function closePopup(restoreFocus = true) {
    if (restoreFocus && popup?.anchor.isConnected) popup.anchor.focus({ preventScroll: true });
    setPopup(null);
  }
  function clearSelection() {
    selectTask(null);
    closePopup(false);
  }
  function toggleProject(id, expanded) {
    if (expanded && selected?.parentId === id) clearSelection();
    const key = projectKey(id);
    setToggledProjects((current) => {
      const next = new Set(current);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }
  useOutsideDismiss([selectedRow, selectionToolbar, popupRoot], clearSelection, Boolean(selected));
  const popupTask = popup && popup.task.id === selected?.id ? popup.task : null;
  async function saveTaskChanges(task, changes, label) {
    await api(`/api/tasks/${task.id}`, "PUT", {
      name: task.name, description: task.description ?? "", deadline: task.deadline ?? null,
      estimatedMinutes: task.estimatedMinutes || null, priority: task.priority || 2,
      categoryId: task.categoryId || null, parentId: task.parentId || null,
      taskType: task.taskType || "OTHER", isHardDeadline: Boolean(task.deadline && task.isHardDeadline),
      creationSource: task.creationSource || "MANUAL", splittable: task.splittable ?? true,
      minimumSessionMinutes: task.minimumSessionMinutes || 25, version: task.version,
      inheritsProjectDeadline: task.inheritsProjectDeadline,
      ...changes,
    });
    try {
      await onWorkspaceChanged();
      toast(label, "success");
    } catch {
      toast("Đã lưu thay đổi. Hãy tải lại để xem bản mới.", "error");
    }
  }
  function saveDeadline(task, day) {
    if ((task.deadline ? dayKey(task.deadline) : null) === day) return;
    const deadline = day ? toUtc(`${day}T${task.deadline ? localInput(task.deadline).slice(11, 16) : "23:59"}`) : null;
    return saveTaskChanges(task, { deadline, isHardDeadline: Boolean(deadline && task.isHardDeadline) },
      day ? "Đã cập nhật hạn chót." : "Đã bỏ hạn chót.");
  }
  return (
    <div className="work-task-board">
      {selected && toolbarHost && createPortal(
        <div ref={selectionToolbar} className="task-selection-toolbar" role="group" aria-label="Thao tác với công việc đã chọn">
          <TaskSelectionActions task={selected} />
          <button type="button" className="task-table-icon task-selection-clear" aria-label="Bỏ chọn công việc"
            title="Bỏ chọn công việc" onClick={() => {
              selectedRow.current?.querySelector(".title-button")?.focus({ preventScroll: true });
              clearSelection();
            }}>
            {icon("close")}
          </button>
        </div>, toolbarHost,
      )}
      <div className="work-task-table-scroll">
        <table className="work-task-table">
          <thead>
            <tr>
              <th scope="col" className="work-task-check-cell"><span className="sr-only">Hoàn thành</span></th>
              <th scope="col" className="work-task-name-cell">Tên công việc</th>
              {Object.entries(taskColumns).map(([key, label]) => includes(key) && (
                <th key={key} scope="col" className={`work-task-${key}-cell`}>
                  {key === "estimate" || key === "priority" ? (
                    <span title={label}>
                      {icon(key === "estimate" ? "clock" : "flag")}
                      <span className="sr-only">{label}</span>
                    </span>
                  ) : label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {visible.map(({ task, childCount, expanded, nested }) => {
              const completed = task.status === "Completed";
              const active = !completed && task.status !== "Cancelled";
              const canSchedule = hasPlanningDetails(task);
              const planned = plannedDays?.get(task.id);
              const plannedStart = plannedStarts?.get(task.id);
              const estimate = task.estimatedMinutes > 0 ? minutes(task.estimatedMinutes) : "";
              const hasChecklist = Boolean(task.parentId && (task.isTeamTask ? task.teamChecklist?.length : task.version));
              const checklistOpen = hasChecklist && openChecklists.has(task.id);
              const checklistId = `${checklistPrefix}-checklist-${task.id}`;
              return <Fragment key={task.id}>
                <tr className={[childCount > 0 && "is-project", nested && "is-project-child", completed && "is-completed", selected?.id === task.id && "is-selected"].filter(Boolean).join(" ")}
                  ref={selected?.id === task.id ? selectedRow : null}
                  onClick={(event) => {
                    if (!event.target.closest("button, a, input")) {
                      selectTask(task.id);
                      closePopup(false);
                    }
                  }}>
                  <td className="work-task-check-cell">
                    {!task.isTeamTask && task.status !== "Cancelled" ? (
                      <button
                        type="button"
                        role="checkbox"
                        aria-checked={completed}
                        aria-label={`${completed ? "Mở lại" : "Hoàn thành"} ${task.name}`}
                        className="task-complete-toggle"
                        data-action="toggle-task-completion"
                        data-id={task.id}
                      >
                        {completed && icon("check")}
                      </button>
                    ) : (
                      <span className="sr-only">{statusLabels[task.status]}</span>
                    )}
                  </td>
                  <td className="work-task-name-cell">
                    <div className="work-task-name-row">
                      <div className="work-task-title-group">
                        {childCount > 0 && (
                          <button type="button" className="work-project-toggle" aria-expanded={expanded}
                            aria-label={`${expanded ? "Thu gọn" : "Mở"} ${task.name} · ${childCount} việc con`}
                            title={expanded ? "Thu gọn việc con" : "Mở việc con"}
                            onClick={() => toggleProject(task.id, expanded)}>
                            {icon("chevron")}{icon("folder")}
                          </button>
                        )}
                        <button type="button" className="title-button" title={task.name}
                          aria-pressed={selected?.id === task.id}
                          onClick={() => {
                            if (selected?.id === task.id) openDetail(task.id);
                            else { selectTask(task.id); closePopup(false); }
                          }}>
                          {task.parentId && <span aria-hidden="true">↳ </span>}{task.name}
                        </button>
                        {hasChecklist && (
                          <button type="button" className="work-checklist-toggle" aria-expanded={Boolean(checklistOpen)}
                            aria-controls={checklistOpen ? checklistId : undefined}
                            aria-label={`${checklistOpen ? "Thu gọn" : "Mở"} checklist của ${task.name}`}
                            title={checklistOpen ? "Thu gọn checklist" : "Mở checklist"}
                            onClick={() => setOpenChecklists((current) => {
                              const next = new Set(current);
                              if (next.has(task.id)) next.delete(task.id);
                              else next.add(task.id);
                              return next;
                            })}>
                            {icon("tasks")} <span>Checklist</span> {icon("chevron")}
                          </button>
                        )}
                      </div>
                      <div className="task-row-actions">
                        {selected?.id === task.id && (
                          <button type="button" className="task-table-icon" aria-label={`Thao tác cho ${task.name}`}
                            title="Thao tác khác" aria-haspopup="dialog" aria-expanded={popup?.kind === "actions" && popup.task.id === task.id}
                            onClick={(event) => showPopup(event, task, "actions")}>
                            <span aria-hidden="true">•••</span>
                          </button>
                        )}
                        {selected?.id !== task.id && <>
                        {active && (
                          <button
                            type="button"
                            className="btn ghost icon-only"
                            data-action={canSchedule ? "schedule-task" : "edit-task"}
                            data-id={task.id}
                            aria-label={canSchedule ? `Xếp lịch công việc ${task.name}` : `Chỉnh sửa ${task.name}`}
                            title={canSchedule ? "Xếp vào giờ rảnh" : "Chỉnh sửa công việc"}
                          >
                            {icon(canSchedule ? "calendar" : "edit")}
                          </button>
                        )}
                        {!task.isTeamTask && <button
                          type="button"
                          className="btn ghost icon-only task-delete-button"
                          data-action="delete-task"
                          data-id={task.id}
                          aria-label={`Xóa công việc ${task.name}`}
                          title={`Xóa công việc ${task.name}`}
                        >
                          {icon("trash")}
                        </button>}
                        </>}
                      </div>
                    </div>
                  </td>
                  {includes("source") && <td className="work-task-source-cell">{task.isTeamTask ? "Nhóm" : taskSources[task.creationSource] || ""}</td>}
                  {includes("planned") && <td className="work-task-planned-cell">
                    {planned ? (
                      <button
                        type="button"
                        className="planned-date-link"
                        data-action="calendar-date"
                        data-id={planned}
                        aria-label={`Mở lịch dự kiến của ${task.name}`}
                        title={plannedStart != null ? `${dateFmt(plannedStart)} · ${timeFmt(plannedStart)}` : undefined}
                      >
                        {planned === today ? "Hôm nay" : dateFmt(`${planned}T12:00:00Z`, { timeZone: "UTC" })}
                        {task.parentId && plannedStart != null && dayKey(plannedStart) === planned && (
                          <small className="work-task-planned-time">{timeFmt(plannedStart)}</small>
                        )}
                      </button>
                    ) : null}
                  </td>}
                  {includes("estimate") && <td className="work-task-estimate-cell">
                    {task.isTeamTask ? estimate : (
                      <button type="button" className="work-task-estimate-button"
                        data-action={onWorkspaceChanged ? undefined : "edit-task"} data-id={task.id}
                        onClick={onWorkspaceChanged ? (event) => showPopup(event, task, "estimate") : undefined}
                        aria-haspopup="dialog" aria-expanded={popup?.kind === "estimate" && popup.task.id === task.id}
                        aria-label={`${estimate ? "Sửa" : "Thêm"} thời lượng cho ${task.name}`}
                        title={estimate ? "Sửa thời lượng dự kiến" : "Thêm thời lượng dự kiến"}>
                        {estimate || <span className="work-task-add-date" aria-hidden="true">{icon("plus")}</span>}
                      </button>
                    )}
                  </td>}
                  {includes("deadline") && <td className="work-task-deadline-cell">
                    {task.isTeamTask ? <TaskDeadlineLabel task={task} today={today} /> : (
                      <button type="button" className="work-task-date-button"
                        data-action={!onWorkspaceChanged || task.inheritsProjectDeadline ? "edit-task" : undefined} data-id={task.id}
                        onClick={onWorkspaceChanged && !task.inheritsProjectDeadline ? (event) => showPopup(event, task, "deadline") : undefined}
                        aria-haspopup="dialog" aria-expanded={popup?.kind === "deadline" && popup.task.id === task.id}
                        aria-label={`${task.deadline ? "Sửa" : "Thêm"} hạn chót cho ${task.name}`}
                        title={task.deadline ? `${dateFmt(task.deadline)} · ${timeFmt(task.deadline)}` : "Thêm hạn chót"}>
                        {task.deadline ? <TaskDeadlineLabel task={task} today={today} /> : <span className="work-task-add-date" aria-hidden="true">{icon("plus")}</span>}
                      </button>
                    )}
                  </td>}
                  {includes("priority") && <td className="work-task-priority-cell">
                    {task.priority === 2 ? <span className="sr-only">Vừa</span> : task.priority && (
                      <span className="work-task-flag" data-priority={task.priority} title={`Ưu tiên ${["", "thấp", "vừa", "cao", "khẩn"][task.priority]}`}>
                        {icon("flag")}
                        <span className="sr-only">{["", "Thấp", "Vừa", "Cao", "Khẩn"][task.priority]}</span>
                      </span>
                    )}
                  </td>}
                </tr>
                {checklistOpen && (
                  <tr className="work-task-checklist-row">
                    <td colSpan={columnCount}>
                      <div id={checklistId} className="work-task-checklist" role="group" aria-label={`Checklist của ${task.name}`}>
                        <TaskChecklist task={task} />
                      </div>
                    </td>
                  </tr>
                )}
              </Fragment>;
            })}
            {!tasks.length && emptyMessage && (
              <tr><td className="work-task-empty" colSpan={columnCount}>{emptyMessage}</td></tr>
            )}
          </tbody>
        </table>
      </div>
      {popupTask && (
        <TaskTablePopup key={`${popup.kind}:${popupTask.id}`} ref={popupRoot} anchor={popup.anchor}
          kind={popup.kind} title={taskPopupTitles[popup.kind]} onClose={closePopup}>
          {popup.kind === "deadline" && <TaskDeadlinePicker deadline={popupTask.deadline}
            onSave={(day) => saveDeadline(popupTask, day)} onClose={closePopup} />}
          {popup.kind === "estimate" && <TaskEstimateEditor task={popupTask}
            onSave={(changes) => saveTaskChanges(popupTask, changes, "Đã lưu thời lượng và cách chia phiên.")} onClose={closePopup} />}
          {popup.kind === "actions" && <TaskSelectionActions task={popupTask} menu onAction={() => closePopup(false)} />}
        </TaskTablePopup>
      )}
      {addTask}
      {pages > 1 && (
        <nav className="task-pagination" aria-label="Phân trang công việc">
          <button type="button" className="btn ghost small" disabled={page === 0}
            aria-label="Trang trước" onClick={() => setPaging({ signature, page: page - 1 })}>
            Trước
          </button>
          <span aria-live="polite">
            Trang {page + 1} / {pages} · {page * 12 + 1}–{Math.min((page + 1) * 12, groups.length)} / {groups.length}
          </span>
          <button type="button" className="btn ghost small" disabled={page + 1 >= pages}
            aria-label="Trang sau" onClick={() => setPaging({ signature, page: page + 1 })}>
            Sau
          </button>
        </nav>
      )}
    </div>
  );
}

function TaskDeadlineLabel({ task, today }) {
  if (!task.deadline) return null;
  const deadline = Date.parse(task.deadline);
  const now = Date.now();
  const remaining = task.remainingMinutes ?? task.estimatedMinutes;
  const scheduledMinutes = (state.workspace.sessions || []).filter((session) =>
    session.taskId === task.id && ["Scheduled", "Running", "Paused"].includes(session.status) &&
    Date.parse(session.end) > now && Date.parse(session.end) <= deadline
  ).reduce((total, session) => total + (Date.parse(session.end) - Math.max(now, Date.parse(session.start))) / 60000, 0);
  const status = task.status === "Completed" ? "Đã hoàn thành"
    : task.status === "Cancelled" ? ""
    : deadline < now ? "Quá hạn"
    : remaining > 0 && Math.floor(scheduledMinutes) >= remaining ? "Đúng tiến độ" : "";
  return (
    <time dateTime={task.deadline} className="task-deadline-chip"
      data-status={status === "Quá hạn" ? "late" : status ? "on-track" : undefined}
      title={status || undefined}>
      {dayKey(task.deadline) === today ? "Hôm nay" : dateFmt(task.deadline)}
    </time>
  );
}

function TaskSelectionActions({ task, menu = false, onAction }) {
  const active = !["Completed", "Cancelled"].includes(task.status);
  const session = (state.workspace.sessions || []).filter((item) => item.taskId === task.id &&
    ["Scheduled", "Running", "Paused", "NeedsReview"].includes(item.status))
    .sort((a, b) => Date.parse(a.start) - Date.parse(b.start))[0];
  const actions = [
    ["tasks", "Mở chi tiết", "task-detail"],
    ...(!task.isTeamTask ? [["edit", "Chỉnh sửa công việc", "edit-task"]] : []),
    ...(active && hasPlanningDetails(task) ? [["refresh", "Xem lịch đề xuất", "schedule-task"]] : []),
    ...(session ? [session.status === "NeedsReview"
      ? ["clock", "Ghi kết quả phiên", "session-outcome", session.id]
      : session.status === "Scheduled" && !sessionStartIssue(session, state.workspace)
        ? ["play", "Bắt đầu phiên", "session-start", session.id]
        : ["play", "Mở phiên làm việc", "session-detail", session.id]] : []),
    ...(!task.isTeamTask && task.status !== "Cancelled"
      ? [["check", task.status === "Completed" ? "Mở lại công việc" : "Đánh dấu hoàn thành", "toggle-task-completion"]] : []),
    ["copy", "Tạo bản sao", "duplicate-task"],
    ["link", "Sao chép liên kết", "copy-task-link"],
    ...(!task.isTeamTask ? [["trash", "Xóa công việc đã chọn", "delete-task"]] : []),
  ];
  return actions.map(([glyph, label, action, id = task.id]) => (
    <button type="button" key={action} className={menu ? "task-table-menu-item" : "task-table-icon"}
      data-action={action} data-id={id} aria-label={label} title={label} onClick={onAction}>
      {icon(glyph)}
      {menu && <span>{label}</span>}
    </button>
  ));
}
export const scopes = {
  all: ["Tất cả", () => true],
  open: ["Đang mở", (t) => !["Completed", "Cancelled"].includes(t.status)],
  progress: ["Đang làm", (t) => t.status === "InProgress"],
  completed: ["Hoàn thành", (t) => t.status === "Completed"],
};
export function filterTasks() {
  const search =
      document.querySelector("#task-search")?.value.toLowerCase() || "",
    status = document.querySelector("#task-status")?.value,
    category = document.querySelector("#task-category")?.value,
    priority = Number(document.querySelector("#task-priority")?.value || 0);
  const tasks = state.workspace.tasks.filter(
    (t) =>
      (scopes[state.taskScope || "all"]?.[1] || scopes.all[1])(t) &&
      (!search ||
        `${t.name} ${t.description || ""}`.toLowerCase().includes(search)) &&
      (!status || t.status === status) &&
      (!category || t.categoryId === category) &&
      (!priority || t.priority === priority) &&
      (!document.querySelector("#task-from")?.value ||
        (t.deadline && dayKey(t.deadline) >= document.querySelector("#task-from").value)) &&
      (!document.querySelector("#task-to")?.value ||
        (t.deadline && dayKey(t.deadline) <= document.querySelector("#task-to").value)),
  );
  setSlot(
    "task-list",
    tasks.length
      ? taskRows(tasks)
      : empty(
          "Không có công việc phù hợp",
          "Thử thay đổi từ khóa hoặc bộ lọc.",
          "",
          "",
        ),
  );
  setSlot("task-result-count", `${tasks.length} công việc`);
}
function TaskPriority({ task }) {
  return (
    <label className="field">
      <span>Mức ưu tiên</span>
      <Select name="priority" defaultValue={task.priority || 2}>
        {["Thấp", "Vừa", "Cao", "Khẩn"].map((label, index) => (
          <option key={label} value={index + 1}>{label}</option>
        ))}
      </Select>
    </label>
  );
}

function TaskCategory({ task }) {
  return (
    <label className="field">
      <span>Danh mục</span>
      <Select name="categoryId" defaultValue={task.categoryId || ""}>
        <option value="">Chưa phân loại</option>
        {state.workspace.categories.map((category) => (
          <option key={category.id} value={category.id}>{category.name}</option>
        ))}
      </Select>
    </label>
  );
}

function TaskDescription({ task }) {
  return (
    <label className="field wide">
      <span>Mô tả</span>
      <textarea
        name="description"
        maxLength={4000}
        defaultValue={task.description || ""}
        placeholder="Thêm mô tả"
      />
    </label>
  );
}

function TaskPlanningFields({ task, parentId, onParentChange }) {
  return (
    <>
      <label className="field">
        Loại công việc
        <Select name="taskType" defaultValue={task.taskType || "OTHER"}>
          {Object.entries(typeLabels).map(([key, label]) => (
            <option key={key} value={key}>{label}</option>
          ))}
          {task.taskType && !typeLabels[task.taskType] && (
            <option value={task.taskType}>{task.taskType}</option>
          )}
        </Select>
      </label>
      {field(
        "Thời lượng phiên tối thiểu (phút)",
        "minimumSessionMinutes",
        "number",
        task.minimumSessionMinutes || 25,
        'min="5" max="480" required',
      )}
      <label className="checkbox wide">
        <input type="checkbox" name="isHardDeadline" defaultChecked={task.isHardDeadline} />
        Deadline cứng: không cho xếp lịch sau hạn chót
      </label>
      <label className="checkbox wide">
        <input
          type="checkbox"
          name="splittable"
          defaultChecked={!task.requiresContinuousSession && (task.splittable ?? true)}
          disabled={task.requiresContinuousSession === true}
        />
        Cho phép chia thành nhiều phiên làm việc
      </label>
      {task.requiresContinuousSession && (
        <p className="reason wide">
          Hoạt động này cần diễn ra liên tục, không thể chia thành nhiều phiên.
        </p>
      )}
      <label className="field wide">
        Công việc cha
        <Select name="parentId" value={parentId || ""} onChange={(event) => onParentChange(event.target.value)}>
          <option value="">Công việc độc lập</option>
          {state.workspace.tasks
            .filter((item) => !item.parentId && item.id !== task.id)
            .map((item) => (
              <option key={item.id} value={item.id}>{item.name}</option>
            ))}
        </Select>
      </label>
    </>
  );
}

function detectTaskStart(normalized, now = Date.now()) {
  const midnightTomorrow = /\btoi\s+mai\s+(?:luc\s+)?12(?:h|\s+gio|:00)\b/.test(normalized)
    || /\b12(?:h|\s+gio|:00)\s+(?:dem\s+)?toi\s+mai\b/.test(normalized);
  const date = new Date(`${dayKey(now)}T12:00:00Z`);
  if (midnightTomorrow) {
    date.setUTCDate(date.getUTCDate() + 2);
    return `${date.toISOString().slice(0, 10)}T00:00`;
  }
  const match = normalized.match(/(?:^|\s)(?:vao\s+)?luc\s+(\d{1,2})(?:(?::|h)(\d{2}))?\s*(?:h|gio)?\s*(sang|chieu|toi)?(?=\s|[,.!?]|$)/);
  if (!match) return "";
  const [, rawHour, rawMinute, period] = match;
  let hour = Number(rawHour);
  const minute = Number(rawMinute || 0);
  if (hour > 23 || minute > 59 || (period && (hour < 1 || hour > 12))) return "";
  if (period) hour = hour % 12 + (period === "sang" ? 0 : 12);
  const clock = `${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`;
  const offset = /\bngay kia\b/.test(normalized) ? 2 : /\b(?:ngay mai|mai)\b/.test(normalized) ? 1
    : /\b(?:hom qua|ngay qua)\b/.test(normalized) ? -1 : 0;
  date.setUTCDate(date.getUTCDate() + offset);
  return `${date.toISOString().slice(0, 10)}T${clock}`;
}

function CreateTaskForm({ task, parentId }) {
  const [section, setSection] = useState("main");
  const [complete, setComplete] = useState(false);
  const [title, setTitle] = useState(task.name || "");
  const [manualStart, setManualStart] = useState(task.startDate ?? null);
  const [manualEstimate, setManualEstimate] = useState(task.estimatedMinutes || null);
  const [manualDeadline, setManualDeadline] = useState(task.deadline ? dayKey(task.deadline) : null);
  const [selectedParent, setSelectedParent] = useState(parentId);
  const [inheritsDeadline, setInheritsDeadline] = useState(Boolean(parentId));
  const projectDeadline = selectedParent ? taskById(selectedParent)?.deadline : null;
  const [analysis, setAnalysis] = useState(null);
  const pendingAnalysis = useRef(Promise.resolve());
  const text = title.trim();
  const canAnalyze = section !== "multiple" && text.length >= 4 && text !== task.name;
  useEffect(() => {
    if (!canAnalyze) return;
    let current = true;
    const timer = setTimeout(() => {
      pendingAnalysis.current = pendingAnalysis.current.then(async () => {
        if (!current) return;
        for (let attempt = 0; current; attempt++) {
          try {
            const result = await api("/api/tasks/preview-text", "POST", { text, useAi: true });
            if (current) setAnalysis({ text, result });
            return;
          } catch (error) {
            if (!current) return;
            const busy = error.code === "AI_CONCURRENT_LIMIT";
            if (!busy || attempt >= 4) return;
            const delay = Math.max(error.retryAfter || 1, 2 ** attempt);
            await new Promise((resolve) => setTimeout(resolve, delay * 1000));
          }
        }
      });
    }, 650);
    return () => {
      current = false;
      clearTimeout(timer);
    };
  }, [canAnalyze, text]);
  const preview = canAnalyze && analysis?.text === text ? analysis : null;
  const activities = preview?.result?.activities;
  const activity = activities?.length === 1 && ["TASK", "EVENT"].includes(activities[0].kind)
    ? activities[0]
    : null;
  const parsed = Array.isArray(activities);
  const normalized = title.toLocaleLowerCase("vi").normalize("NFD").replace(/\p{M}/gu, "");
  const today = dayKey(Date.now());
  const localStart = detectTaskStart(normalized);
  const parsedStart = Number.isFinite(Date.parse(activity?.start)) ? localInput(activity.start) : "";
  const detectedStart = localStart.endsWith("T00:00") && /\btoi\s+mai\b/.test(normalized)
    ? localStart : parsedStart || localStart;
  const startDate = manualStart ?? detectedStart.slice(0, 10);
  const pastStart = startDate && startDate < today;
  const suggestedStart = pastStart && manualStart == null ? "" : startDate;
  const duration = normalized.match(/\b(?:trong|mat|can|lam)\s+(\d{1,5})\s+phut\b/);
  const windowMinutes = (Date.parse(activity?.end) - Date.parse(activity?.start)) / 60000;
  const estimate = parsed ? (activity?.estimatedMinutes ?? windowMinutes) : Number(duration?.[1]);
  const suggestedEstimate = Number.isInteger(estimate) && estimate > 0 && estimate <= 43200 ? estimate : "";
  const detectedDeadline = Number.isFinite(Date.parse(activity?.deadline)) ? localInput(activity.deadline) : "";
  const deadlineDate = manualDeadline ?? detectedDeadline.slice(0, 10);
  const pastDeadline = deadlineDate && deadlineDate < today;
  const suggestedDeadline = pastDeadline && manualDeadline == null ? "" : deadlineDate;
  return (
    <div className="task-create-form">
      <div className="task-create-tabs" role="group" aria-label="Thiết lập công việc">
        <button type="button" aria-pressed={section === "main"} onClick={() => setSection("main")}>
          Một việc
        </button>
        {!parentId && (
          <button type="button" aria-pressed={section === "multiple"} onClick={() => setSection("multiple")}>
            Nhiều việc
          </button>
        )}
        <button type="button" aria-pressed={section === "options"} onClick={() => setSection("options")}>
          Tùy chọn
        </button>
      </div>
    <form
      hidden={section === "multiple"}
      data-form="task"
      onInvalid={(event) => {
        const input = event.target;
        const panel = input.closest("[data-task-create-panel]");
        if (!panel?.hidden) return;
        event.preventDefault();
        if (event.currentTarget.querySelector(":invalid:not(form)") !== input) return;
        setSection(panel.dataset.taskCreatePanel);
        requestAnimationFrame(() => input.reportValidity());
      }}
    >
      <input type="hidden" name="creationSource" value={task.creationSource || "MANUAL"} />
      <div className="task-create-content">
        <input
          className="task-create-name"
          name="name"
          type="text"
          required
          maxLength={200}
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          aria-label="Tên công việc"
          placeholder="Ví dụ: Gửi báo cáo cho Minh"
          data-dialog-autofocus
        />
        <div className="task-create-panel" data-task-create-panel="main" hidden={section !== "main"}>
          <label className="field">
            <span>Ngày bắt đầu</span>
            <div className="task-create-date">
              <input
                name="startDate"
                type="date"
                aria-label="Ngày bắt đầu"
                min={today}
                value={suggestedStart}
                onChange={(event) => setManualStart(event.target.value)}
              />
              {!suggestedStart && <span aria-hidden="true">Sớm nhất có thể</span>}
            </div>
          </label>
          {pastStart && <small role="status">Ngày bắt đầu đã qua.</small>}
          <label className="field">
            <span>Thời lượng (phút)</span>
            <input
              name="estimatedMinutes"
              type="number"
              min={1}
              max={43200}
              value={manualEstimate ?? suggestedEstimate}
              onChange={(event) => setManualEstimate(event.target.value)}
              aria-label="Thời gian dự kiến (phút, tùy chọn)"
              placeholder="Chưa ước lượng"
            />
          </label>
          <label className="field">
            <span>Hạn chót</span>
            <div className="task-create-date">
              <input
                name="deadline"
                type="date"
                min={today}
                value={inheritsDeadline ? "" : suggestedDeadline}
                disabled={inheritsDeadline}
                onChange={(event) => setManualDeadline(event.target.value)}
                aria-label="Hạn chót (tùy chọn)"
              />
              {inheritsDeadline ? (
                <span>{projectDeadline ? dateFmt(projectDeadline) : "Dự án chưa có hạn chót"}</span>
              ) : !suggestedDeadline && <span aria-hidden="true">Bất kỳ ngày nào</span>}
            </div>
          </label>
          {selectedParent && (
            <label className="checkbox">
              <input type="hidden" name="projectDeadlineOption" value="true" />
              <input type="checkbox" name="inheritsProjectDeadline" checked={inheritsDeadline}
                onChange={(event) => setInheritsDeadline(event.target.checked)} />
              Dùng hạn chót của dự án
            </label>
          )}
          {!inheritsDeadline && pastDeadline && <small role="status">Hạn chót đã qua.</small>}
          <TaskDescription task={task} />
          <TaskPriority task={task} />
        </div>
        <div className="task-create-panel" data-task-create-panel="options" hidden={section !== "options"}>
          <div className="form-grid">
            <TaskPlanningFields task={task} parentId={selectedParent} onParentChange={(value) => {
              setSelectedParent(value);
              setInheritsDeadline(Boolean(value));
            }} />
          </div>
        </div>
        <div className="task-create-tools">
          <button type="button" className="btn ghost small" onClick={(event) => {
            const data = new FormData(event.currentTarget.form);
            captureModal([data.get("name"), data.get("description")].filter(Boolean).join("\n"));
          }}>
            {icon("edit")} Lưu nháp
          </button>
        </div>
      </div>
      <div className="form-error" role="alert" />
      <div className="task-create-footer">
        <label className="checkbox">
          <input type="checkbox" name="markComplete" checked={complete} onChange={(event) => setComplete(event.target.checked)} />
          Đánh dấu hoàn thành
        </label>
        <div className="task-create-save">
          <button type="submit" className="btn primary">Lưu công việc</button>
          <button type="submit" className="btn secondary" data-next-action="open">Lưu và mở</button>
        </div>
        <button type="submit" className="btn ghost small task-create-preview" data-next-action="preview" disabled={complete}>
          {icon("calendar")} Lưu và xem lịch đề xuất
        </button>
      </div>
    </form>
    {!parentId && <div hidden={section !== "multiple"}><TaskBatchForm /></div>}
    </div>
  );
}

function TaskDeadlineField({ task, parentId, inherits, setInherits }) {
  const projectDeadline = inherits ? taskById(parentId)?.deadline : null;
  return (
    <div>
      <label className="field">
        Hạn chót (tùy chọn)
        <input type="datetime-local" name="deadline"
          defaultValue={task.deadline ? localInput(task.deadline) : ""}
          disabled={inherits} />
      </label>
      {inherits && <small>{projectDeadline ? dateFmt(projectDeadline) : "Dự án chưa có hạn chót"}</small>}
      {parentId && (
        <label className="checkbox">
          <input type="hidden" name="projectDeadlineOption" value="true" />
          <input type="checkbox" name="inheritsProjectDeadline" checked={inherits}
            onChange={(event) => setInherits(event.target.checked)} />
          Dùng hạn chót của dự án
        </label>
      )}
    </div>
  );
}

export function taskForm(task = {}, parentId = null) {
  if (!task.id) return <CreateTaskForm key={parentId ?? task.startDate} task={task} parentId={parentId} />;
  return <EditTaskForm key={task.id} task={task} />;
}

function EditTaskForm({ task }) {
  const [parentId, setParentId] = useState(task.parentId);
  const [inheritsDeadline, setInheritsDeadline] = useState(Boolean(task.inheritsProjectDeadline));
  return (
    <form data-form="task" data-id={task.id || ""}>
      <input type="hidden" name="version" defaultValue={task.version || ""} />
      <input
        type="hidden"
        name="creationSource"
        defaultValue={task.creationSource || "MANUAL"}
      />
      <div className="form-grid">
        <div className="wide">
          {field(
            "Tên công việc",
            "name",
            "text",
            task.name || "",
            'required maxlength="200" placeholder="Ví dụ: Hoàn thiện báo cáo dự án"',
          )}
        </div>
        <TaskDeadlineField task={task} parentId={parentId} inherits={inheritsDeadline} setInherits={setInheritsDeadline} />
        {field(
          "Thời gian dự kiến (phút, tùy chọn)",
          "estimatedMinutes",
          "number",
          task.estimatedMinutes || "",
          'min="1" max="43200"',
        )}
        {task.plannedMinutes > 0 && !task.isTeamTask && (
          <p className="reason wide">Bỏ hạn chót hoặc thời lượng sẽ hủy các phiên đã xếp chưa bắt đầu. Phiên đang làm hoặc đã khóa cần được xử lý trước.</p>
        )}
        <TaskPriority task={task} />
        <TaskCategory task={task} />
      </div>
      <details className="form-section" open>
        <summary>Tùy chọn thêm · mô tả, loại việc, cách chia thời gian</summary>
        <div className="form-grid">
          <TaskDescription task={task} />
          <TaskPlanningFields task={task} parentId={parentId} onParentChange={(value) => {
            setParentId(value);
            if (!value) setInheritsDeadline(false);
          }} />
        </div>
      </details>
      <Slot id="task-conflict" />
      <div className="form-error" />
      <div className="form-actions">
        {button("Hủy", "close-modal", "ghost")}
        <button type="submit" className="btn primary">Lưu thay đổi</button>
        {state.noraAvailable && <button type="button" className="btn ghost" onClick={() => askNoraForTask(task, parentId)}>Nhờ NORA</button>}
      </div>
    </form>
  );
}
export function askNoraForTask(task = {}, parentId = null) {
    askNora(task.id
      ? `Tôi muốn cập nhật công việc “${task.name}” (mã ${task.id}). Hỏi tôi điều cần thay đổi rồi kiểm tra và cập nhật lịch.`
      : parentId ? `Tôi muốn bổ sung việc liên quan đến “${taskById(parentId)?.name || parentId}”. Hỏi tôi nội dung rồi đề xuất cách sắp xếp.`
      : "Tôi có công việc mới. Hỏi tôi nội dung và hạn chót; nếu thiếu thời lượng, đề xuất ước lượng để tôi xác nhận trong chat, rồi tự xếp lịch.");
}
export function openTask(task = {}, parentId = null) {
  setSlot("task-conflict", null);
  const title = task.id
    ? "Chỉnh sửa công việc"
    : parentId
      ? "Thêm công việc con"
      : <>Thêm công việc vào <strong>Inbox</strong></>;
  modal(
    title,
    task.id ? "Bạn có thể lưu chỉ với tên. Để xếp lịch, hãy thêm hạn chót và thời gian dự kiến." : "",
    taskForm(task, parentId),
  );
}
export function taskInput(data) {
  const deadline = data.get("deadline");
  const estimate = data.get("estimatedMinutes");
  const startDate = data.get("startDate");
  const parentId = data.get("parentId") || null;
  const inheritsProjectDeadline = data.has("inheritsProjectDeadline");
  const parent = inheritsProjectDeadline && parentId ? taskById(parentId) : null;
  return {
    name: String(data.get("name")),
    description: String(data.get("description") || ""),
    deadline: inheritsProjectDeadline ? parent?.deadline || null
      : deadline ? toUtc(String(deadline).includes("T") ? String(deadline) : `${deadline}T23:59`) : null,
    ...(startDate ? { startDate: String(startDate) } : {}),
    estimatedMinutes: estimate ? Number(estimate) : null,
    priority: Number(data.get("priority")),
    categoryId: data.get("categoryId") || null,
    parentId,
    ...(data.has("projectDeadlineOption") ? { inheritsProjectDeadline } : {}),
    taskType: String(data.get("taskType") || "OTHER"),
    isHardDeadline: inheritsProjectDeadline ? Boolean(parent?.isHardDeadline) : Boolean(deadline) && data.has("isHardDeadline"),
    creationSource: String(data.get("creationSource") || "MANUAL"),
    splittable: data.has("splittable"),
    minimumSessionMinutes: Number(data.get("minimumSessionMinutes") || 25),
    version: data.get("version") || null,
  };
}
function ProjectTaskList({ children, projectId }) {
  const plannedStarts = plannedTaskStarts(state.workspace.sessions);
  const ordered = orderProjectChildren(children, plannedStarts);
  return (
    <table className="project-task-list">
      <thead><tr><th>Công việc</th><th>Dự kiến</th><th>Thời lượng</th><th>Checklist</th></tr></thead>
      <tbody>
        {ordered.map((task) => {
          const completed = task.status === "Completed";
          const predecessors = task.dependencies.map(taskById).filter(Boolean);
          const start = plannedStarts.get(task.id);
          return (
            <tr key={task.id}>
              <td>
                <div className="project-task-name">
                  <input type="checkbox" checked={completed} readOnly
                    disabled={task.isTeamTask || task.status === "Cancelled"}
                    aria-label={`${completed ? "Mở lại" : "Hoàn thành"} ${task.name}`}
                    data-action="toggle-task-completion" data-id={task.id} data-return-project={projectId} />
                  <div>
                    {button(task.name, "task-detail", "small ghost", task.id)}
                    {predecessors.length > 0 && <small>Sau khi xong {predecessors.map((item) => item.name).join(", ")}</small>}
                  </div>
                </div>
              </td>
              <td>{start == null ? "—" : `${dateFmt(start)} · ${timeFmt(start)}`}</td>
              <td>{task.estimatedMinutes > 0 ? minutes(task.estimatedMinutes) : "—"}</td>
              <td>{task.checklist?.length ? `${task.checklist.filter((item) => item.completed).length}/${task.checklist.length}` : "—"}</td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

export function openDetail(id, { afterSession = false, initialTab = "details" } = {}) {
  const t = taskById(id);
  if (!t) return;
  const children = state.workspace.tasks.filter((x) => x.parentId === id);
  const parent = t.parentId ? taskById(t.parentId) : null;
  const deps = t.dependencies.map(taskById).filter(Boolean);
  const canToggle =
    !t.isTeamTask && !children.length && t.status !== "Cancelled";
  const completed = t.status === "Completed";
  modal(
    t.name,
    afterSession
      ? "Lượt làm vừa kết thúc. Cập nhật phần công việc bạn đã hoàn thành."
      : "",
    <TaskDetailDrawer
      task={t}
      parent={parent}
      childrenTasks={children}
      deps={deps}
      afterSession={afterSession}
      initialTab={initialTab}
    />,
    {
      placement: "right",
      context:
        parent?.name ||
        (t.isTeamTask ? "Công việc nhóm" : children.length ? "Dự án" : "Inbox"),
      leading: canToggle ? (
        <button
          type="button"
          className="task-complete-toggle"
          role="checkbox"
          aria-checked={completed}
          aria-label={
            completed ? "Mở lại công việc" : "Đánh dấu công việc hoàn thành"
          }
          data-action="toggle-task-completion"
          data-id={id}
          data-keep-task-detail="true"
        >
          {completed && icon("check")}
        </button>
      ) : undefined,
    },
  );
}

export function TaskDrawerSource({ task, lines, canAttach }) {
  const picker = useRef(null);
  const [attachedName, setAttachedName] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const name = task.sourceFileName || attachedName;
  const path = `/api/tasks/${task.id}/source`;

  async function attach(event) {
    const file = event.currentTarget.files[0];
    event.currentTarget.value = "";
    if (!file || busy) return;
    const userId = state.workspace?.profile?.id;
    setBusy(true);
    setError("");
    try {
      const input = await readTeamBriefFile(file, { allowCsv: true });
      if (state.workspace?.profile?.id !== userId) return;
      const { name } = await api(path, "PUT", input);
      const workspace = state.workspace;
      if (!workspace || workspace.profile?.id !== userId) return;
      const projectId = task.parentId ?? task.id;
      workspace.tasks = workspace.tasks.map((item) => (item.parentId ?? item.id) === projectId
        ? { ...item, sourceFileName: name } : item);
      setAttachedName(name);
      window.dispatchEvent(new Event("workspace-updated"));
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="task-drawer-brief-section" data-tone="source" aria-label="Tài liệu nguồn">
      <h3>{icon("folder")}Tài liệu nguồn</h3>
      {name ? (
        <a className="task-drawer-source-link" href={path} download={name}>
          <span>{name}</span><span aria-hidden="true">↓</span>
        </a>
      ) : (
        <>
          <TaskNoteContent lines={lines} />
          {canAttach && <>
            <input ref={picker} type="file" hidden aria-label="Chọn file gốc" accept={`${TEAM_BRIEF_ACCEPT},.csv`} onChange={attach} />
            <button type="button" className="btn small secondary task-drawer-source-attach" disabled={busy}
              onClick={() => picker.current?.click()}>
              {busy ? "Đang gắn file…" : "Gắn file gốc"}
            </button>
            {error && <p className="warning" role="alert">{error}</p>}
          </>}
        </>
      )}
    </section>
  );
}

function TaskDrawerDescription({ task, canAttachSource }) {
  const description = task.description;
  const sections = parseTaskNotes(description ?? "").filter((section) =>
    section.lines.some((line) => line.trim()),
  );
  const source = sections.find((section) => section.title === "Tài liệu nguồn");
  const structured = sections.some((section) => section.explicit);
  return (
    <section className="task-drawer-brief" aria-label="Mô tả">
      {structured ? sections.filter((section) => section !== source).map((section, index) => (
        <section key={index} className="task-drawer-brief-section"
          data-tone={section.title === "Tóm tắt" ? "summary" : undefined}>
          <h3>{icon(section.glyph)}{section.title}</h3>
          <TaskNoteContent lines={section.lines} />
        </section>
      )) : (
        <div><h3>Mô tả</h3><p>{description?.trim() ? description : "Chưa có mô tả"}</p></div>
      )}
      {(source || task.sourceFileName) && (
        <TaskDrawerSource key={task.id} task={task} lines={source?.lines ?? []} canAttach={canAttachSource} />
      )}
    </section>
  );
}

function TaskDetailDrawer({
  task: t,
  parent,
  childrenTasks: children,
  deps,
  afterSession,
  initialTab,
}) {
  const id = t.id;
  const sessions = state.workspace.sessions
    .filter(
      (session) => session.taskId === id && session.status !== "Cancelled",
    )
    .sort((a, b) => Date.parse(a.start) - Date.parse(b.start));
  const planned = sessions.find(
    (session) =>
      ["Scheduled", "Running", "Paused"].includes(session.status) &&
      Date.parse(session.end) > Date.now(),
  );
  const canShowChecklist = Boolean(t.version && !children.length && (!t.isTeamTask || t.teamChecklist?.length));
  const [tab, setTab] = useState(initialTab === "checklist" && !canShowChecklist ? "details" : initialTab);
  const scroll = useRef(null);
  useEffect(() => {
    scroll.current.scrollTop = 0;
  }, [tab]);
  const tabs = [
    ["details", "Chi tiết"],
    ...(canShowChecklist ? [["checklist", "Checklist"]] : []),
    ["children", "Việc con"],
    ["sessions", "Phiên đã xếp"],
  ];
  const today = dayKey(Date.now());
  return (
    <div className="task-detail-drawer">
      <nav
        role="tablist"
        aria-label="Thông tin công việc"
        className="task-drawer-tabs"
        onKeyDown={(event) => {
          const index = tabs.findIndex(([key]) => key === tab);
          const keys = {
            ArrowRight: (index + 1) % tabs.length,
            ArrowLeft: (index + tabs.length - 1) % tabs.length,
            Home: 0,
            End: tabs.length - 1,
          };
          if (keys[event.key] == null) return;
          event.preventDefault();
          setTab(tabs[keys[event.key]][0]);
          event.currentTarget
            .querySelectorAll("[role=tab]")
            [keys[event.key]].focus();
        }}
      >
        {tabs.map(([key, label]) => (
          <button
            type="button"
            role="tab"
            id={`task-drawer-tab-${key}`}
            aria-controls={`task-drawer-panel-${key}`}
            key={key}
            aria-selected={tab === key}
            tabIndex={tab === key ? 0 : -1}
            onClick={() => setTab(key)}
          >
            {label}
          </button>
        ))}
      </nav>
      <div className="task-drawer-scroll" ref={scroll}>
        <section
          role="tabpanel"
          id="task-drawer-panel-details"
          aria-labelledby="task-drawer-tab-details"
          hidden={tab !== "details"}
        >
          {parent &&
            button(
              `Về ${parent.name}`,
              "task-detail",
              "small ghost",
              parent.id,
            )}
          {afterSession && (
            <div className="info post-session-help section-gap">
              <h3>Bạn đã làm xong cả công việc chưa?</h3>
              <p>
                Xong phiên chỉ kết thúc lượt làm và dừng đồng hồ. Nếu còn việc,
                cập nhật tiến độ bên dưới để tiếp tục vào lần sau.
              </p>
              {!["Completed", "Cancelled"].includes(t.status) &&
                button(
                  "Đã hoàn thành cả công việc",
                  "task-finish",
                  "primary small",
                  id,
                  "check",
                )}
            </div>
          )}
          <TaskDrawerDescription task={t} canAttachSource={!t.isTeamTask && Boolean(parent || children.length)} />
          <dl className="task-drawer-facts">
            <div>
              {icon("flag")}
              <dt>Ưu tiên</dt>
              <dd>{["", "Thấp", "Vừa", "Cao", "Khẩn"][t.priority] || "Vừa"}</dd>
            </div>
            <div>
              {icon("users")}
              <dt>Người thực hiện</dt>
              <dd>Bạn</dd>
            </div>
          </dl>
          <div className="actions task-drawer-badges">
            {badge(
              statusLabels[t.status],
              t.status === "Completed" ? "green" : "purple",
            )}
            {badge(typeLabels[t.taskType] || t.taskType)}
            {t.deadline &&
              badge(t.isHardDeadline ? "Deadline cứng" : "Deadline mềm")}
          </div>
          <h3 className="task-drawer-section-title">Xếp lịch</h3>
          <dl className="task-drawer-facts">
            <div>
              {icon("calendar")}
              <dt>Dự kiến</dt>
              <dd>
                {planned
                  ? `${dayKey(planned.start) === today ? "Hôm nay" : dateFmt(planned.start)} · ${timeFmt(planned.start)}`
                  : "Chưa xếp phiên"}
              </dd>
            </div>
            {t.workWindowStart && (
              <div>
                {icon("today")}
                <dt>Khung giờ thực hiện</dt>
                <dd>
                  {dateFmt(t.workWindowStart)}
                  {t.workWindowEnd &&
                    ` · ${timeFmt(t.workWindowStart)} → ${dateFmt(t.workWindowEnd)} · ${timeFmt(t.workWindowEnd)}`}
                </dd>
              </div>
            )}
            <div>
              {icon("calendar")}
              <dt>{children.length ? "Hạn chót project" : "Hạn chót"}</dt>
              <dd>
                {t.deadline
                  ? `${dateFmt(t.deadline)} · ${timeFmt(t.deadline)}`
                  : "Chưa có hạn"}
              </dd>
            </div>
            <div>
              {icon("clock")}
              <dt>Thời gian dự kiến / điều chỉnh</dt>
              <dd>
                {t.estimatedMinutes > 0
                  ? `${minutes(t.estimatedMinutes)} / ${minutes(t.adjustedMinutes)}`
                  : "Chưa ước lượng"}
              </dd>
            </div>
            <div>
              {icon("clock")}
              <dt>Đã thực hiện / đang lên lịch</dt>
              <dd>
                {minutes(t.actualMinutes)}
                {" / "}
                {minutes(t.plannedMinutes)}
              </dd>
            </div>
            <div>
              {icon("insights")}
              <dt>Tiến độ</dt>
              <dd>{t.progress}%</dd>
            </div>
          </dl>
          {hasPlanningDetails(t) && (
            <TaskEisenhowerDetail
              task={t}
              onChanged={async () => {
                state.workspace = await api("/api/workspace");
                openDetail(id);
              }}
            />
          )}
          {secretaryMode() ? (
            <div className="form-actions">
              {!["Completed", "Cancelled"].includes(t.status) &&
                button(
                  "Đã hoàn thành công việc",
                  "task-finish",
                  "primary small",
                  id,
                  "check",
                )}
            </div>
          ) : (
            <form data-form="task-progress" data-id={id}>
              <div className="form-grid">
                {field(
                  "Tiến độ (%)",
                  "progress",
                  "number",
                  t.progress,
                  'min="0" max="100" required',
                )}
                <label className="field">
                  Trạng thái
                  <Select name="status">
                    {nodeList(
                      ["Todo", "InProgress", "Completed", "Cancelled"].map(
                        (key) => (
                          <option value={key} selected={key === t.status}>
                            {statusLabels[key]}
                          </option>
                        ),
                      ),
                    )}
                  </Select>
                </label>
              </div>
              <div className="form-error" />
              <div className="form-actions">
                <button type="submit" className="btn secondary">
                  Cập nhật tiến độ
                </button>
              </div>
            </form>
          )}
        </section>
        {canShowChecklist && (
          <section role="tabpanel" id="task-drawer-panel-checklist" aria-labelledby="task-drawer-tab-checklist"
            className="task-detail-steps" hidden={tab !== "checklist"}>
            <TaskChecklist task={t} />
          </section>
        )}
        <section
          role="tabpanel"
          id="task-drawer-panel-children"
          aria-labelledby="task-drawer-tab-children"
          hidden={tab !== "children"}
        >
          {parent &&
            button(
              `Về ${parent.name}`,
              "task-detail",
              "small ghost",
              parent.id,
            )}
          <div className="spaced">
            <h3>Công việc con</h3>
            {children.length ? (
              <ProjectTaskList children={children} projectId={id} />
            ) : (
              <p className="muted">
                <small>Chưa có công việc con.</small>
              </p>
            )}
            <div className="actions">
              {button(
                "Thêm việc con",
                "subtask",
                "small secondary",
                id,
                "plus",
              )}
              {hasPlanningDetails(t) &&
                button(
                  "Gợi ý chia nhỏ",
                  "breakdown",
                  "small ghost",
                  id,
                  "spark",
                )}
            </div>
          </div>
          <div className="spaced">
            <h3>Phải hoàn thành trước</h3>
            {nodeList(
              deps.map((d) => (
                <div className="list-line">
                  <span>{d.name}</span>
                  {button(
                    "Bỏ phụ thuộc",
                    "remove-dependency",
                    "small ghost",
                    `${id}|${d.id}`,
                    "close",
                  )}
                </div>
              )),
            )}
            {!secretaryMode() && (
              <form data-form="dependency" data-id={id}>
                <div className="actions">
                  <Select name="predecessor" className="filter">
                    {nodeList(
                      state.workspace.tasks
                        .filter(
                          (x) => x.id !== id && !t.dependencies.includes(x.id),
                        )
                        .map((x) => <option value={x.id}>{x.name}</option>),
                    )}
                  </Select>
                  <button type="submit" className="btn small secondary">
                    Thêm phụ thuộc
                  </button>
                </div>
                <div className="form-error" />
              </form>
            )}
          </div>
        </section>
        <section
          role="tabpanel"
          id="task-drawer-panel-sessions"
          aria-labelledby="task-drawer-tab-sessions"
          hidden={tab !== "sessions"}
        >
          {sessions.length ? (
            sessions.map((session) => (
              <div className="list-line" key={session.id}>
                <span>
                  {dateFmt(session.start)} · {timeFmt(session.start)}–
                  {timeFmt(session.end)}
                </span>
                {button(
                  statusLabels[session.status] || session.status,
                  "session-detail",
                  "small ghost",
                  session.id,
                  "clock",
                )}
              </div>
            ))
          ) : (
            <p className="muted">Công việc chưa có phiên đã xếp.</p>
          )}
        </section>
      </div>
      <footer className="form-actions task-drawer-footer">
        {button("Xóa công việc", "delete-task", "danger small", id, "trash")}
        {button(
          t.isTeamTask ? "Xem lịch cho phần còn lại" : "Lập lại lịch liên quan",
          "replan-task",
          "secondary small",
          id,
          "refresh",
        )}
        {button("Chỉnh sửa", "edit-task", "primary small", id, "edit")}
      </footer>
    </div>
  );
}

export function captureModal(text = "") {
  modal(
    "Ghi việc trước",
    "Bản nháp được lưu trong Ghi chú.",
    <form data-form="capture">
      <label className="field">
        Mô tả bằng câu tự nhiên
        <textarea
          name="text"
          required={true}
          maxLength="2000"
          placeholder="Làm báo cáo SWP trước thứ Sáu, khoảng 3 tiếng"
          defaultValue={text}
        />
      </label>
      <div className="form-error" />
      <div className="form-actions">
        <button className="btn primary" type="submit">Lưu nháp</button>
      </div>
    </form>,
  );
}
export async function breakdownModal(id) {
  if (secretaryMode()) { askNora(`Hãy tư vấn cách chia nhỏ công việc “${taskById(id)?.name || id}” và đề xuất thời lượng để tôi xác nhận, chưa lưu thêm việc.`); return; }
  const t = taskById(id);
  const result = await api(`/api/tasks/${id}/breakdown`, "POST");
  state.breakdown = result;
  modal(
    "Chia nhỏ công việc",
    `Gợi ý từ ${["OpenAI", "Gemini", "VertexAI"].includes(result.source) ? result.source : "quy tắc mẫu"} · chỉ lưu những mục bạn chọn.`,
    <>
      <form data-form="breakdown" data-id={id}>
        <div className="info">
          Công việc cha trở thành nhóm; chỉ các việc con được tính vào workload
          để tránh cộng trùng.
        </div>
        <div className="spaced">
          {nodeList(
            result.subtasks.map((s, index) => (
              <label className="list-line">
                <span className="checkbox">
                  <input
                    type="checkbox"
                    name="part"
                    defaultValue={index}
                    defaultChecked={true}
                  />
                  {s.name}
                </span>
                <small>{minutes(s.estimatedMinutes)}</small>
              </label>
            )),
          )}
        </div>
        <div className="form-error" />
        <div className="form-actions">
          <button type="submit" className="btn primary">
            Lưu các việc con đã chọn
          </button>
        </div>
      </form>
      <div className="spaced">
        <label className="checkbox">
          <input type="checkbox" id="breakdown-ai" />
          Cho phép gửi tên và mô tả công việc đến AI
        </label>
        {button("Thử gợi ý AI", "ai-breakdown", "small ghost", t.id, "spark")}
      </div>
    </>,
  );
}
