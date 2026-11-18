import { useState } from "react";
import { EisenhowerMatrix, eisenhowerEnabled } from "./eisenhower.jsx";
import { TaskSmartList } from "./task-smart-list.jsx";
export function tasksPage({ onWorkspaceChanged, projectId } = {}) {
  return <TasksWorkspace onWorkspaceChanged={onWorkspaceChanged} projectId={projectId} />;
}
function TasksWorkspace({ onWorkspaceChanged, projectId }) {
  const [view, setView] = useState("list");
  const matrix = view === "matrix" && eisenhowerEnabled();
  return <TaskSmartList projectId={projectId} onWorkspaceChanged={onWorkspaceChanged}
    onShowMatrix={eisenhowerEnabled() ? () => setView("matrix") : undefined}
    onShowList={() => setView("list")} matrix={matrix ? <EisenhowerMatrix /> : null} />;
}
