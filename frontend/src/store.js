import { flushSync } from "react-dom";

const listeners = new Set();
let snapshot = {
  screen: null,
  screenKey: "loading",
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
export function setScreen(screen, screenKey) {
  update({ screen, screenKey: `${screenKey}-${++screenVersion}`, slots: {} });
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
