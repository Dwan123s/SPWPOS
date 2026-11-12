import { TaskEisenhowerDetail } from "./eisenhower.jsx";
import { useEffect, useRef, useState } from "react";
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
} from "./ui.jsx";
import { api } from "./api.js";
import { askNora, secretaryMode } from "./agent-handoff.js";
import { TaskBatchForm } from "./task-batch.jsx";
import { Checklist } from "./eisenhower-board.jsx";
import "./task-create.css";
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
export function taskRows(tasks, plannedDays, options = {}) {
  return <TaskList tasks={tasks} plannedDays={plannedDays} {...options} />;
}
function TaskList({ tasks, plannedDays, columns = Object.keys(taskColumns), addTask, emptyMessage }) {
  const signature = tasks.map((task) => task.id).join(",");
  const [paging, setPaging] = useState({ signature, page: 0 });
  const page = paging.signature === signature ? paging.page : 0;
  const pages = Math.ceil(tasks.length / 12);
  const visible = tasks.slice(page * 12, (page + 1) * 12);
  const includes = (column) => columns.includes(column);
  const columnCount = columns.length + 2;
  return (
    <div className="work-task-board">
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
            {visible.map((task) => {
              const completed = task.status === "Completed";
              const active = !completed && task.status !== "Cancelled";
              const canSchedule = hasPlanningDetails(task);
              const planned = plannedDays?.get(task.id);
              return (
                <tr key={task.id} className={completed ? "is-completed" : undefined}>
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
                      <button type="button" className="title-button" data-action="task-detail" data-id={task.id} title={task.name}>
                        {task.parentId ? "↳ " : ""}{task.name}
                      </button>
                      <div className="task-row-actions">
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
                        <button
                          type="button"
                          className="btn ghost icon-only task-delete-button"
                          data-action="delete-task"
                          data-id={task.id}
                          aria-label={`Xóa công việc ${task.name}`}
                          title={`Xóa công việc ${task.name}`}
                        >
                          {icon("trash")}
                        </button>
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
                      >
                        {dateFmt(`${planned}T12:00:00Z`, { timeZone: "UTC" })}
                      </button>
                    ) : null}
                  </td>}
                  {includes("estimate") && <td className="work-task-estimate-cell">{task.estimatedMinutes > 0 ? minutes(task.estimatedMinutes) : ""}</td>}
                  {includes("deadline") && <td className="work-task-deadline-cell">
                    {task.isTeamTask ? (task.deadline ? dateFmt(task.deadline) : "") : (
                      <button type="button" className="work-task-date-button" data-action="edit-task" data-id={task.id}
                        aria-label={`${task.deadline ? "Sửa" : "Thêm"} hạn chót cho ${task.name}`}
                        title={task.deadline ? `${dateFmt(task.deadline)} · ${timeFmt(task.deadline)}` : "Thêm hạn chót"}>
                        {task.deadline ? <time dateTime={task.deadline}>{dateFmt(task.deadline)}</time> : <span className="work-task-add-date" aria-hidden="true">{icon("plus")}</span>}
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
              );
            })}
            {!tasks.length && emptyMessage && (
              <tr><td className="work-task-empty" colSpan={columnCount}>{emptyMessage}</td></tr>
            )}
          </tbody>
        </table>
      </div>
      {addTask}
      {pages > 1 && (
        <nav className="task-pagination" aria-label="Phân trang công việc">
          <button type="button" className="btn ghost small" disabled={page === 0}
            aria-label="Trang trước" onClick={() => setPaging({ signature, page: page - 1 })}>
            Trước
          </button>
          <span aria-live="polite">
            Trang {page + 1} / {pages} · {page * 12 + 1}–{Math.min((page + 1) * 12, tasks.length)} / {tasks.length}
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
function TaskChecklistForm({ task }) {
  const [items, setItems] = useState(task.checklist || []);
  return (
    <form className="task-detail-checklist" data-form="task-checklist" data-id={task.id}>
      <Checklist items={items} onChange={setItems} />
      <input type="hidden" name="version" value={task.version} />
      <input type="hidden" name="items" value={JSON.stringify(items.filter((item) => item.text.trim()))} />
      <div className="form-error" role="alert" />
      <button type="submit" className="btn small secondary">Lưu checklist</button>
    </form>
  );
}

function ProjectTaskList({ children, projectId }) {
  const byId = new Map(children.map((task) => [task.id, task]));
  const visited = new Set();
  const ordered = [];
  function visit(task) {
    if (visited.has(task.id)) return;
    visited.add(task.id);
    task.dependencies.filter((id) => byId.has(id)).forEach((id) => visit(byId.get(id)));
    ordered.push(task);
  }
  children.forEach(visit);
  return (
    <table className="project-task-list">
      <thead><tr><th>Công việc</th><th>Thời lượng</th><th>Checklist</th></tr></thead>
      <tbody>
        {ordered.map((task) => {
          const completed = task.status === "Completed";
          const predecessors = task.dependencies.map(taskById).filter(Boolean);
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
              <td>{task.estimatedMinutes > 0 ? minutes(task.estimatedMinutes) : "—"}</td>
              <td>{task.checklist?.length ? `${task.checklist.filter((item) => item.completed).length}/${task.checklist.length}` : "—"}</td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

export function openDetail(id, afterSession = false) {
  const t = taskById(id);
  if (!t) return;
  const children = state.workspace.tasks.filter((x) => x.parentId === id);
  const parent = t.parentId ? taskById(t.parentId) : null;
  const deps = t.dependencies.map(taskById).filter(Boolean);
  modal(
    t.name,
    afterSession
      ? "Lượt làm vừa kết thúc. Cập nhật phần công việc bạn đã hoàn thành."
      : "Chi tiết công việc",
    <>
      {parent && button(`Về ${parent.name}`, "task-detail", "small ghost", parent.id)}
      {afterSession && (
        <div className="info post-session-help section-gap">
          <h3>Bạn đã làm xong cả công việc chưa?</h3>
          <p>
            Xong phiên chỉ kết thúc lượt làm và dừng đồng hồ. Nếu còn việc, cập
            nhật tiến độ bên dưới để tiếp tục vào lần sau.
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
      <div className="actions">
        {badge(
          statusLabels[t.status],
          t.status === "Completed" ? "green" : "purple",
        )}
        {badge(typeLabels[t.taskType] || t.taskType)}
        {badge(`Ưu tiên ${t.priority}`)}
        {t.deadline && badge(t.isHardDeadline ? "Deadline cứng" : "Deadline mềm")}
      </div>
      {hasPlanningDetails(t) && (
        <TaskEisenhowerDetail task={t} onChanged={async () => { state.workspace = await api("/api/workspace"); openDetail(id); }} />
      )}
      <dl className="detail-list">
        {t.workWindowStart && (
          <div>
            <dt>Khung giờ thực hiện</dt>
            <dd>
              {dateFmt(t.workWindowStart)}
              {t.workWindowEnd && ` · ${timeFmt(t.workWindowStart)} → ${dateFmt(t.workWindowEnd)} · ${timeFmt(t.workWindowEnd)}`}
            </dd>
          </div>
        )}
        <div>
          <dt>{children.length ? "Hạn chót project" : "Hạn chót"}</dt>
          <dd>
            {t.deadline ? `${dateFmt(t.deadline)} · ${timeFmt(t.deadline)}` : "Chưa có hạn"}
          </dd>
        </div>
        <div>
          <dt>Thời gian dự kiến / điều chỉnh</dt>
          <dd>
            {t.estimatedMinutes > 0 ? `${minutes(t.estimatedMinutes)} / ${minutes(t.adjustedMinutes)}` : "Chưa ước lượng"}
          </dd>
        </div>
        <div>
          <dt>Đã thực hiện / đang lên lịch</dt>
          <dd>
            {minutes(t.actualMinutes)}
            {" / "}
            {minutes(t.plannedMinutes)}
          </dd>
        </div>
        <div>
          <dt>Tiến độ</dt>
          <dd>{t.progress}%</dd>
        </div>
      </dl>
      {t.description ? (
        <p className="detail-description">{t.description}</p>
      ) : (
        ""
      )}
      {!t.isTeamTask && t.version && !children.length && (
        <details className="task-detail-steps" open={!!t.checklist?.length}>
          <summary>Các bước thực hiện</summary>
          <TaskChecklistForm key={`${id}:${t.version}`} task={t} />
        </details>
      )}
      {secretaryMode() ? <div className="form-actions">
        {!["Completed", "Cancelled"].includes(t.status) && button("Đã hoàn thành công việc", "task-finish", "primary small", id, "check")}
      </div> : <form data-form="task-progress" data-id={id}>
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
                ["Todo", "InProgress", "Completed", "Cancelled"].map((key) => (
                  <option value={key} selected={key === t.status}>
                    {statusLabels[key]}
                  </option>
                )),
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
      </form>}
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
          {button("Thêm việc con", "subtask", "small secondary", id, "plus")}
          {hasPlanningDetails(t) && button("Gợi ý chia nhỏ", "breakdown", "small ghost", id, "spark")}
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
        {!secretaryMode() && <form data-form="dependency" data-id={id}>
          <div className="actions">
            <Select name="predecessor" className="filter">
              {nodeList(
                state.workspace.tasks
                  .filter((x) => x.id !== id && !t.dependencies.includes(x.id))
                  .map((x) => <option value={x.id}>{x.name}</option>),
              )}
            </Select>
            <button type="submit" className="btn small secondary">
              Thêm phụ thuộc
            </button>
          </div>
          <div className="form-error" />
        </form>}
      </div>
      <div className="form-actions">
        {button("Xóa công việc", "delete-task", "danger small", id, "trash")}
        {button(
          t.isTeamTask ? "Xem lịch cho phần còn lại" : "Lập lại lịch liên quan",
          "replan-task",
          "secondary small",
          id,
          "refresh",
        )}
        {button("Chỉnh sửa", "edit-task", "primary small", id, "edit")}
      </div>
    </>,
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
