import { state } from "./ui.jsx";

const empty = Object.freeze({
  status: "disabled",
  step: 0,
  completed: [],
  noteId: null,
});

export const subscribeGuide = () => () => {};
export function getGuide() {
  return empty;
}
export function startGuide() {}
export function skipGuide() {}
export function completeGuideStep() {}
export function advanceGuide() {
  return false;
}
