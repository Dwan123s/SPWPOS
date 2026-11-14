import { render, screen, cleanup } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { Loading } from "../src/loading.jsx";
import { planningQuoteOfDay } from "../src/planning-quotes.js";
import { getSnapshot, setScreen } from "../src/store.js";

afterEach(() => {
  cleanup();
  delete HTMLElement.prototype.animate;
  localStorage.clear();
});

it("uses one quote throughout the local day and changes it the next day", () => {
  expect(planningQuoteOfDay(new Date(2026, 10, 13, 0, 1)))
    .toBe(planningQuoteOfDay(new Date(2026, 10, 13, 23, 59)));
  expect(planningQuoteOfDay(new Date(2026, 10, 14)))
    .not.toBe(planningQuoteOfDay(new Date(2026, 10, 13)));
  render(<Loading fullscreen />);
  expect(screen.getByText(planningQuoteOfDay())).toBeTruthy();
});

it("fades loading before showing ready content and prevents stale navigation", async () => {
  let finish;
  const cancel = vi.fn();
  HTMLElement.prototype.animate = vi.fn(() => ({
    cancel, finished: new Promise(resolve => { finish = resolve; }),
  }));
  setScreen(<Loading fullscreen />, "loading");
  render(<Loading fullscreen />);
  setScreen(<h1>Ready</h1>, "dashboard");
  expect(getSnapshot().pageKey).toBe("loading");
  const finishFirst = finish;
  setScreen(<h1>Login</h1>, "auth-login");
  const finishLatest = finish;
  finishFirst();
  await Promise.resolve();
  expect(getSnapshot().pageKey).toBe("loading");
  finishLatest();
  await Promise.resolve();
  expect(getSnapshot().pageKey).toBe("auth-login");
  expect(cancel).toHaveBeenCalled();
});

it("shows ready content immediately when loading motion is paused", () => {
  localStorage.setItem("planora.loading.motion-paused", "true");
  HTMLElement.prototype.animate = vi.fn();
  setScreen(<Loading fullscreen />, "loading");
  render(<Loading fullscreen />);
  setScreen(<h1>Ready</h1>, "dashboard");
  expect(getSnapshot().pageKey).toBe("dashboard");
  expect(HTMLElement.prototype.animate).not.toHaveBeenCalled();
});
