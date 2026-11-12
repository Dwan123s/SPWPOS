import { state } from "./ui.jsx";

let pending = null;

export function openTeamTask(teamId, taskId) {
  pending = { teamId, taskId, account: state.workspace?.profile?.id };
  location.hash = "teams";
  window.dispatchEvent(new Event("open-team-task"));
}

export function takeTeamTask() {
  const task = pending;
  pending = null;
  return task?.account === state.workspace?.profile?.id ? task : null;
}
