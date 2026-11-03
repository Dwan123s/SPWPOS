import { PopupDetails } from "./popup-dismissal.jsx";
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
import { taskRows, scopes } from "./tasks.jsx";
import { useState } from "react";
import { EisenhowerMatrix, eisenhowerEnabled } from "./eisenhower.jsx";
export function tasksPage() {
  return <TasksWorkspace />;
}
function TasksWorkspace() {
  const [view, setView] = useState("list");
  return (
    <>
      {eisenhowerEnabled() && (
        <div
          className="eis-view-switch"
          role="group"
          aria-label="Bố cục công việc"
        >
          <button
            type="button"
            aria-pressed={view === "list"}
            onClick={() => setView("list")}
          >
            {icon("tasks")} Danh sách
          </button>
          <button
            type="button"
            aria-pressed={view === "matrix"}
            onClick={() => setView("matrix")}
          >
            {icon("spark")} Eisenhower
          </button>
        </div>
      )}
      {view === "matrix" && eisenhowerEnabled() ? (
        <EisenhowerMatrix />
      ) : (
        <TaskListPage />
      )}
    </>
  );
}
function TaskListPage() {
  const scope = state.taskScope || "all";
  return (
    <>
      <div className="task-tabs" role="group" aria-label="Chế độ xem công việc">
        {nodeList(
          Object.entries(scopes).map(([key, [label, match]]) => (
            <button
              type="button"
              data-action="task-scope"
              data-id={key}
              className={key === scope ? "active" : ""}
              aria-pressed={key === scope}
            >
              {label}
              <span>{state.workspace.tasks.filter(match).length}</span>
            </button>
          )),
        )}
      </div>
      <section className="card task-workspace">
        <div className="filters task-filters">
          <label className="search-field">
            {icon("search")}
            <input
              className="filter search"
              id="task-search"
              type="search"
              placeholder="Tìm công việc…"
              aria-label="Tìm công việc"
            />
          </label>
          <Select
            className="filter"
            id="task-status"
            aria-label="Lọc trạng thái"
          >
            <option value={``}>Trạng thái</option>
            {nodeList(
              Object.entries(statusLabels)
                .filter(([key]) =>
                  ["Todo", "InProgress", "Completed", "Cancelled"].includes(
                    key,
                  ),
                )
                .map(([key, label]) => <option value={key}>{label}</option>),
            )}
          </Select>
          <Select
            className="filter"
            id="task-category"
            aria-label="Lọc danh mục"
          >
            <option value={``}>Danh mục công việc</option>
            {nodeList(
              state.workspace.categories.map((c) => (
                <option value={c.id}>{c.name}</option>
              )),
            )}
          </Select>
          <Select
            className="filter"
            id="task-priority"
            aria-label="Lọc mức ưu tiên"
          >
            <option value={``}>Ưu tiên</option>
            <option value="4">Khẩn</option>
            <option value="3">Cao</option>
            <option value="2">Vừa</option>
            <option value="1">Thấp</option>
          </Select>
          <PopupDetails className="date-filters">
            <summary>Khoảng deadline</summary>
            <div>
              <label className="field">
                Từ ngày
                <input className="filter" id="task-from" type="date" />
              </label>
              <label className="field">
                Đến ngày
                <input className="filter" id="task-to" type="date" />
              </label>
            </div>
          </PopupDetails>
        </div>
        <Slot id="task-list" as="div">
          {taskRows(
            state.workspace.tasks.filter(scopes[scope]?.[1] || scopes.all[1]),
          )}
        </Slot>
        <div className="task-list-footer">
          <Slot id="task-result-count" aria-live="polite" as="span">
            {
              state.workspace.tasks.filter(scopes[scope]?.[1] || scopes.all[1])
                .length
            }
            {" công việc"}
          </Slot>
          <span>Bấm tên công việc để xem chi tiết</span>
        </div>
      </section>
    </>
  );
}
