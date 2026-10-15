import { flushSync } from "react-dom";
import { motionAllowed } from "./page-transition.jsx";

const listeners = new Set();
let snapshot = {
  screen: null,
  screenKey: "loading",
  screenGroup: null,
  pageKey: "loading",
  modal: null,
  modalKey: 0,
  toasts: [],
  slots: {},
};
export const subscribe = (listener) => {
  listeners.add(listener);
  return () => listeners.delete(listener);
};
export const getSnapshot = () => snapshot;
export function update(patch) {
  flushSync(() => {
    snapshot = { ...snapshot, ...patch };
    listeners.forEach((listener) => listener());
  });
}
let screenVersion = 0;
let navigationVersion = 0;
let transition;
export function setScreen(screen, screenKey, { group = null } = {}) {
  const version = ++navigationVersion;
  const animate =
    group === "workspace" &&
    snapshot.screenGroup === group &&
    snapshot.pageKey !== screenKey &&
    motionAllowed();
  const commit = () => {
    if (version !== navigationVersion) return;
    update({
      screen,
      screenKey: `${screenKey}-${++screenVersion}`,
      screenGroup: group,
      pageKey: screenKey,
      slots: {},
    });
  };
  transition?.skipTransition();
  transition = null;
  if (animate && typeof document.startViewTransition === "function") {
    try {
      transition = document.startViewTransition(commit);
      transition.finished.catch(() => {});
      return;
    } catch {
      /* Older browsers still commit and use the CSS/WAAPI reveal. */
    }
  }
  commit();
}
export function setSlot(id, node) {
  update({ slots: { ...snapshot.slots, [id]: node } });
}
export function showModal(title, subtitle, body) {
  update({ modal: { title, subtitle, body }, modalKey: snapshot.modalKey + 1 });
}
export function hideModal() {
  update({ modal: null });
}
export function notify(message, type = "") {
  const id = crypto.randomUUID();
  update({ toasts: [...snapshot.toasts, { id, message, type }] });
  setTimeout(
    () => update({ toasts: snapshot.toasts.filter((t) => t.id !== id) }),
    6000,
  );
}
