import { state } from "./ui.jsx";

let pending = null;

export function openTeamTask(teamId, taskId, { selectedSessionId, initialSection } = {}) {
  pending = { teamId, taskId, account: state.workspace?.profile?.id,
    ...(selectedSessionId ? { selectedSessionId } : {}), ...(initialSection ? { initialSection } : {}) };
  location.hash = "teams";
  window.dispatchEvent(new Event("open-team-task"));
}

export function pendingTeamTask(userId) {
  return pending?.account === userId ? pending : null;
}

export function teamReviewTarget(item) {
  return item.kind === "TEAM_COMPLETION_REQUEST" && item.teamId && item.teamTaskId;
}

export function finishTeamTask(request) {
  if (pending === request) pending = null;
}
