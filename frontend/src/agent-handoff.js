import { closeModal, state } from "./ui.jsx";

export const secretaryMode = (workspace = state.workspace) =>
  workspace?.features?.includes("secretary-v1") === true;

let queued = "";
export function takeAgentDraft() {
  const draft = queued;
  queued = "";
  return draft;
}
export function askNora(text) {
  queued = text;
  closeModal();
  window.dispatchEvent(new Event("nora-draft"));
  location.hash = "agent";
}
