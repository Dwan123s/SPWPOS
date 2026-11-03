import { state } from "./ui.jsx";

let pending = null;
export function requestTeamJoinReview(teamId) {
  if (!teamId || !state.workspace?.profile?.id) return;
  pending = { teamId, userId: state.workspace.profile.id };
  window.dispatchEvent(new Event("team-join-review"));
}
export function pendingTeamJoinReview(userId) {
  return pending?.userId === userId ? pending : null;
}
export function finishTeamJoinReview(request) {
  if (pending === request) pending = null;
}
