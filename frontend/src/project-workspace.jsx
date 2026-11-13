import { useState } from "react";
import { dateFmt, icon, statusLabels } from "./ui.jsx";
import { taskColumns, taskRows, TaskDrawerSource } from "./tasks.jsx";
import { orderProjectChildren, smartTaskLists } from "./task-smart-lists.js";
import { PopupDetails } from "./popup-dismissal.jsx";
import "./project-workspace.css";

export function ProjectWorkspace({
  projectId,
  workspace,
  onBack,
  onWorkspaceChanged,
}) {
  const project = workspace.tasks.find(
    (task) =>
      task.id === projectId &&
      !task.isDeleted &&
      !task.isTeamTask &&
      !task.parentId,
  );
  const [tab, setTab] = useState("list");
  const [query, setQuery] = useState("");
  const [incomplete, setIncomplete] = useState(false);
  const [columns, setColumns] = useState(Object.keys(taskColumns));
  const [toolbarHost, setToolbarHost] = useState(null);
  const model = smartTaskLists(workspace);
  if (!project)
    return (
      <section className="project-workspace">
        <button type="button" className="btn ghost" onClick={onBack}>
          {icon("back")} Quay lại Projects
        </button>
        <p role="status">
          Project không còn trong danh sách. Hãy quay lại để chọn project khác.
        </p>
      </section>
    );
  const children = orderProjectChildren(
    workspace.tasks.filter(
      (task) => task.parentId === project.id && !task.isDeleted,
    ),
    model.plannedStarts,
  );
  const visible = children.filter(
    (task) =>
      (!incomplete || !["Completed", "Cancelled"].includes(task.status)) &&
      `${task.name} ${task.description || ""}`
        .toLocaleLowerCase("vi")
        .includes(query.trim().toLocaleLowerCase("vi")),
  );
  return (
    <section
      className="project-workspace"
      aria-label={`Project ${project.name}`}
    >
      <header className="project-page-header">
        <nav className="project-breadcrumb" aria-label="Đường dẫn project">
          <button type="button" onClick={onBack}>
            {icon("back")} Projects
          </button>
          {icon("chevron")}
          <h2>{project.name}</h2>
        </nav>
        <button
          type="button"
          className="btn secondary small"
          data-action="subtask"
          data-id={project.id}
        >
          {icon("plus")} Thêm công việc
        </button>
      </header>
      <nav className="task-smart-nav" aria-label="Các mục project">
        {[
          ["list", "tasks", "Danh sách"],
          ["details", "edit", "Chi tiết"],
          ["files", "folder", "Tài liệu"],
        ].map(([key, glyph, label]) => (
          <button
            type="button"
            key={key}
            className={`task-smart-nav-item${tab === key ? " is-active" : ""}`}
            aria-pressed={tab === key}
            onClick={() => setTab(key)}
          >
            {icon(glyph)}
            {label}
          </button>
        ))}
      </nav>
      <div hidden={tab !== "list"}>
        <div className="task-smart-toolbar" ref={setToolbarHost}>
          <label className="project-page-search">
            {icon("search")}
            <input
              type="search"
              aria-label="Tìm việc trong project"
              placeholder="Tìm công việc…"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
          </label>
          <div className="task-work-toolbar-end">
            <button
              type="button"
              className="task-work-tool"
              aria-pressed={incomplete}
              onClick={() => setIncomplete(!incomplete)}
            >
              {icon("check")} Chưa hoàn thành
            </button>
            <PopupDetails className="task-work-popup task-column-picker">
              <summary>
                <span className="task-work-columns-icon" aria-hidden="true" />{" "}
                Cột
              </summary>
              <div>
                {Object.entries(taskColumns).map(([key, label]) => (
                  <label className="checkbox" key={key}>
                    <input
                      type="checkbox"
                      checked={columns.includes(key)}
                      onChange={(e) =>
                        setColumns((current) =>
                          e.target.checked
                            ? [...current, key]
                            : current.filter((item) => item !== key),
                        )
                      }
                    />
                    {label}
                  </label>
                ))}
              </div>
            </PopupDetails>
            {project.deadline && (
              <span className="project-page-due">
                {icon("calendar")} {dateFmt(project.deadline)}
              </span>
            )}
          </div>
        </div>
        {taskRows(visible, model.plannedDays, {
          columns,
          plannedStarts: model.plannedStarts,
          toolbarHost,
          onWorkspaceChanged,
          emptyMessage:
            query || incomplete
              ? "Không có công việc phù hợp."
              : "Project chưa có công việc con.",
          addTask: (
            <button
              type="button"
              className="task-quick-add-button"
              data-action="subtask"
              data-id={project.id}
            >
              {icon("plus")} Thêm công việc vào {project.name}…
            </button>
          ),
        })}
        <p className="project-page-count">
          {children.length} công việc ·{" "}
          {children.filter((task) => task.status === "Completed").length} đã
          xong
        </p>
      </div>
      {tab === "details" && (
        <section className="project-page-details" aria-label="Chi tiết project">
          <h3>{project.name}</h3>
          <p>{project.description?.trim() || "Chưa có mô tả."}</p>
          <dl>
            <div>
              <dt>Trạng thái</dt>
              <dd>{statusLabels[project.status]}</dd>
            </div>
            <div>
              <dt>Tiến độ</dt>
              <dd>{project.progress || 0}%</dd>
            </div>
            <div>
              <dt>Hạn chót</dt>
              <dd>
                {project.deadline ? dateFmt(project.deadline) : "Chưa có"}
              </dd>
            </div>
          </dl>
          <button
            type="button"
            className="btn secondary small"
            data-action="edit-task"
            data-id={project.id}
          >
            {icon("edit")} Chỉnh sửa project
          </button>
        </section>
      )}
      {tab === "files" && (
        <div className="project-page-details">
          <TaskDrawerSource task={project} lines={[]} canAttach />
        </div>
      )}
    </section>
  );
}
