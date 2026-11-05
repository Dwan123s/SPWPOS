import { useState } from "react";
import { icon } from "./ui.jsx";
import { EisenhowerMatrix, eisenhowerEnabled } from "./eisenhower.jsx";
import { TaskSmartList } from "./task-smart-list.jsx";
export function tasksPage() {
  return <TasksWorkspace />;
}
function TasksWorkspace() {
  const [view, setView] = useState("list");
  const matrix = view === "matrix" && eisenhowerEnabled();
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
            aria-pressed={!matrix}
            onClick={() => setView("list")}
          >
            {icon("tasks")} Danh sách
          </button>
          <button
            type="button"
            aria-pressed={matrix}
            onClick={() => setView("matrix")}
          >
            {icon("spark")} Eisenhower
          </button>
        </div>
      )}
      <div hidden={matrix}>
        <TaskSmartList />
      </div>
      {matrix && <EisenhowerMatrix />}
    </>
  );
}
