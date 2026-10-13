import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { App } from "../src/App.jsx";
import { GettingStarted } from "../src/getting-started.jsx";
import { state } from "../src/ui.jsx";
import { update } from "../src/store.js";

beforeEach(() => {
  localStorage.removeItem("planora.start.motion-paused");
  Object.assign(state, {
    workspace: null,
    user: null,
    roles: [],
    insights: null,
  });
  history.replaceState(null, "", "/");
  update({
    screen: null,
    screenKey: "loading",
    modal: null,
    slots: {},
    toasts: [],
  });
});
function denySession() {
  const fetchMock = vi.fn(async () => ({
    ok: false,
    status: 401,
    json: async () => ({ error: { message: "Unauthenticated" } }),
  }));
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}
it("shows Getting Started at the public root and opens login only on request", async () => {
  const fetchMock = denySession();
  render(<App />);
  await screen.findByRole("heading", { level: 1, name: "PLANORA" });
  expect(screen.queryByLabelText("Tên đăng nhập hoặc email")).toBe(null);
  expect(
    screen
      .getAllByRole("link", { name: "Đăng nhập", exact: true })[0]
      .getAttribute("href"),
  ).toBe("#login");
  await act(async () => {
    location.hash = "login";
    window.dispatchEvent(new Event("hashchange"));
  });
  expect(await screen.findByLabelText("Tên đăng nhập hoặc email")).toBeTruthy();
  await act(async () => {
    location.hash = "getting-started";
    window.dispatchEvent(new Event("hashchange"));
  });
  expect(
    await screen.findByRole("heading", { level: 1, name: "PLANORA" }),
  ).toBeTruthy();
  expect(
    fetchMock.mock.calls.every(([url]) =>
      url.startsWith("/api/browser-session/"),
    ),
  ).toBe(true);
});
it("supports direct register links and reload without visiting the landing page first", async () => {
  denySession();
  location.hash = "register";
  render(<App />);
  expect(await screen.findByLabelText("Tên đăng nhập")).toBeTruthy();
  expect(screen.queryByRole("heading", { name: "PLANORA" })).toBe(null);
});
it("shows information below the hero and lets visitors explore without sending content", () => {
  const fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
  update({ screen: <GettingStarted />, screenKey: "intro-demo" });
  render(<App initialize={false} />);
  fireEvent.click(
    screen.getByRole("button", { name: "Cách hoạt động", exact: true }),
  );
  expect(document.getElementById("start-how")).toBe(document.activeElement);
  expect(screen.queryByRole("dialog")).toBe(null);
  const hero = document.querySelector(".start-hero");
  expect(hero.contains(document.getElementById("start-how"))).toBe(false);
  expect(hero.contains(document.getElementById("start-about"))).toBe(false);
  expect(screen.getByLabelText("Ghi chú minh họa").readOnly).toBe(true);
  const tabs = screen.getAllByRole("tab");
  fireEvent.keyDown(tabs[0], { key: "ArrowRight" });
  expect(tabs[1].getAttribute("aria-selected")).toBe("true");
  expect(tabs[1]).toBe(document.activeElement);
  fireEvent.click(tabs[2]);
  expect(
    within(screen.getByRole("tabpanel")).getByText("Viết báo cáo"),
  ).toBeTruthy();
  fireEvent.click(tabs[3]);
  expect(screen.getByText("25:00")).toBeTruthy();
  expect(fetchMock).not.toHaveBeenCalled();
});
it("remembers pause and keeps navigation available", () => {
  const first = render(<GettingStarted />);
  fireEvent.click(screen.getByRole("button", { name: "Tạm dừng animation" }));
  expect(document.querySelector(".getting-started").dataset.motionState).toBe(
    "paused",
  );
  first.unmount();
  render(<GettingStarted />);
  expect(
    screen
      .getByRole("button", { name: "Bật animation" })
      .getAttribute("aria-pressed"),
  ).toBe("true");
  expect(
    screen
      .getAllByRole("link", { name: "Đăng nhập", exact: true })[0]
      .getAttribute("href"),
  ).toBe("#login");
});
it("opens login once from the mascot clock without saving any data", async () => {
  const login = vi.fn(),
    fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
  render(<GettingStarted onLogin={login} />);
  const clock = screen.getByRole("button", { name: "Chạm đồng hồ để bắt đầu" });
  await act(async () => fireEvent.click(clock));
  fireEvent.click(clock);
  expect(login).toHaveBeenCalledTimes(1);
  expect(fetchMock).not.toHaveBeenCalled();
});
