import { beforeEach, afterEach, expect, it, vi } from "vitest";
import { transitionAuthPanel } from "../src/auth-panel-motion.js";

let effects;
const originalAnimate = Element.prototype.animate;
beforeEach(() => {
  effects = [];
  document.body.innerHTML = `<div class="auth-panel-viewport"><div data-auth-panel="login"><input /></div></div>`;
  Element.prototype.animate = vi.fn(() => {
    let finish, reject;
    const finished = new Promise((resolve, fail) => {
      finish = resolve;
      reject = fail;
    });
    const effect = {
      finished,
      finish,
      cancel: vi.fn(() => reject(new Error("cancelled"))),
    };
    effects.push(effect);
    return effect;
  });
});
afterEach(async () => {
  document.body.replaceChildren();
  await transitionAuthPanel(() => {});
  if (originalAnimate) Element.prototype.animate = originalAnimate;
  else delete Element.prototype.animate;
});
function replacePanel(mode) {
  document.querySelector(".auth-panel-viewport").innerHTML =
    `<div data-auth-panel="${mode}"><div class="fb-header-row"></div><form><label class="field"><input /></label><button class="auth-submit"></button></form></div>`;
}
async function finishExit(effect = effects[0]) {
  effect.finish();
  await Promise.resolve();
  await Promise.resolve();
}

it("blocks the departing form, commits the new step, and clears animation effects", async () => {
  const outgoing = document.querySelector("[data-auth-panel]");
  const commit = vi.fn(() => replacePanel("register"));
  const work = transitionAuthPanel(commit, "register");
  expect(outgoing.inert).toBe(true);
  expect(commit).not.toHaveBeenCalled();
  await finishExit();
  expect(commit).toHaveBeenCalledOnce();
  expect(document.querySelector("[data-auth-panel]").dataset.authPanel).toBe(
    "register",
  );
  expect(document.querySelector("[data-auth-panel]").inert).not.toBe(true);
  effects.forEach((effect) => effect.finish());
  await work;
  expect(
    document
      .querySelector(".auth-panel-viewport")
      .hasAttribute("data-transitioning"),
  ).toBe(false);
  expect(effects.every((effect) => effect.cancel.mock.calls.length === 1)).toBe(
    true,
  );
});

it("keeps only the latest navigation when interrupted during the exit", async () => {
  const obsolete = vi.fn(() => replacePanel("register"));
  const first = transitionAuthPanel(obsolete, "register");
  const latest = vi.fn(() => replacePanel("forgot"));
  const second = transitionAuthPanel(latest, "forgot");
  await first;
  expect(document.querySelector("[data-auth-panel]").inert).toBe(true);
  expect(
    document
      .querySelector(".auth-panel-viewport")
      .hasAttribute("data-transitioning"),
  ).toBe(true);
  await finishExit(effects[1]);
  effects.forEach((effect) => effect.finish());
  await second;
  expect(obsolete).not.toHaveBeenCalled();
  expect(latest).toHaveBeenCalledOnce();
  expect(document.querySelector("[data-auth-panel]").dataset.authPanel).toBe(
    "forgot",
  );
});

it("does not restore an auth screen after leaving it during an exit", async () => {
  const commit = vi.fn();
  const work = transitionAuthPanel(commit, "register");
  document.body.replaceChildren();
  await finishExit();
  await work;
  expect(commit).not.toHaveBeenCalled();
});

it.each(["reduced", "paused"])(
  "switches immediately when motion is %s",
  async (preference) => {
    if (preference === "reduced") {
      vi.spyOn(window, "matchMedia").mockReturnValue({ matches: true });
    } else {
      document.body.insertAdjacentHTML(
        "beforeend",
        '<button class="login-motion-toggle" aria-pressed="true"></button>',
      );
    }
    const commit = vi.fn(() => replacePanel("register"));
    await transitionAuthPanel(commit, "register");
    expect(commit).toHaveBeenCalledOnce();
    expect(effects).toHaveLength(0);
  },
);
