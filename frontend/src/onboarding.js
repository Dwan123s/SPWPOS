import { state } from "./ui.jsx";

const listeners = new Set();
const cache = new Map();
const key = () =>
  `planora.onboarding.v2.${state.user?.id || state.workspace?.profile?.id || "guest"}`;
const empty = Object.freeze({
  status: "new",
  step: 0,
  completed: [],
  noteId: null,
});
export const subscribeGuide = (callback) => {
  listeners.add(callback);
  return () => listeners.delete(callback);
};
export function getGuide() {
  const id = key();
  if (!cache.has(id)) {
    try {
      const value = JSON.parse(localStorage.getItem(id));
      cache.set(
        id,
        value &&
          ["active", "skipped", "done"].includes(value.status) &&
          Number.isInteger(value.step) &&
          value.step >= 0 &&
          value.step < 4 &&
          Array.isArray(value.completed)
          ? value
          : empty,
      );
    } catch {
      cache.set(id, empty);
    }
  }
  return cache.get(id);
}
function save(value) {
  cache.set(key(), value);
  try {
    localStorage.setItem(key(), JSON.stringify(value));
  } catch {
    /* Optional storage. */
  }
  listeners.forEach((callback) => callback());
}
export function startGuide() {
  const current = getGuide();
  save(
    current.status === "active"
      ? current
      : { ...empty, status: "active", completed: [] },
  );
}
export function skipGuide() {
  save({ ...getGuide(), status: "skipped" });
}
export function completeGuideStep(step, noteId = null) {
  const current = getGuide();
  if (current.status !== "active") return;
  save({
    ...current,
    completed: [...new Set([...current.completed, step])],
    noteId: noteId || current.noteId,
  });
}
export function advanceGuide() {
  const current = getGuide();
  if (current.status !== "active" || !current.completed.includes(current.step))
    return false;
  save(
    current.step === 3
      ? { ...current, status: "done" }
      : { ...current, step: current.step + 1 },
  );
  return true;
}
