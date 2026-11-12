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
      {matrix && (
        <div className="eis-view-switch">
          <button
            type="button"
            onClick={() => setView("list")}
          >
            {icon("tasks")} Danh sách
          </button>
        </div>
      )}
      <div hidden={matrix}>
        <TaskSmartList onShowMatrix={eisenhowerEnabled() ? () => setView("matrix") : undefined} />
      </div>
      {matrix && <EisenhowerMatrix />}
    </>
  );
}
