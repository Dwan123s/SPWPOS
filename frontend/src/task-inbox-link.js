import { state } from "./ui.jsx";
let pending = null;
export function openInboxNote(noteId) {
  pending = { noteId, userId: state.workspace?.profile?.id };
  window.dispatchEvent(new Event("inbox-open-note"));
  window.location.hash = "notes";
}
export function takeInboxNote(userId) {
  const next = pending;
  pending = null;
  return next && next.userId === userId ? next.noteId : null;
}
