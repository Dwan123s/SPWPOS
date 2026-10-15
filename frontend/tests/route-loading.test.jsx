import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { createPageResource, ResourcePage } from "../src/route-pages.jsx";
import { PageTransition } from "../src/page-transition.jsx";
import { setScreen, getSnapshot } from "../src/store.js";
import { App } from "../src/App.jsx";

afterEach(() => {
  delete document.startViewTransition;
  delete HTMLElement.prototype.animate;
  localStorage.removeItem("planora.workspace.motion-paused");
});
it("loads on demand, shares prefetch, and exposes content only after it is ready", async () => {
  let resolve;
  const load = vi.fn(
    () =>
      new Promise((done) => {
        resolve = done;
      }),
  );
  const resource = createPageResource(load, "Page");
  expect(load).not.toHaveBeenCalled();
  resource.preload();
  resource.preload();
  expect(load).toHaveBeenCalledOnce();
  const ready = vi.fn();
  render(<ResourcePage resource={resource} route="tasks" onReady={ready} />);
  expect(
    screen.getByRole("status", { name: "Đang tải nội dung…" }),
  ).toBeTruthy();
  expect(ready).not.toHaveBeenCalled();
  await act(async () => resolve({ Page: () => <h2>Công việc đã tải</h2> }));
  await screen.findByRole("heading", { name: "Công việc đã tải" });
  expect(ready).toHaveBeenCalledOnce();
  expect(screen.queryByRole("status")).toBeNull();
  expect(load).toHaveBeenCalledOnce();
});
it("recovers a failed lazy chunk without reloading the session or the shell", async () => {
  vi.spyOn(console, "error").mockImplementation(() => {});
  const load = vi
    .fn()
    .mockRejectedValueOnce(new Error("offline"))
    .mockResolvedValueOnce({ Page: () => <h2>Đã kết nối lại</h2> });
  const resource = createPageResource(load, "Page");
  render(<ResourcePage resource={resource} route="notes" />);
  await screen.findByRole("alert");
  fireEvent.click(screen.getByRole("button", { name: "Thử tải lại" }));
  await screen.findByRole("heading", { name: "Đã kết nối lại" });
  expect(load).toHaveBeenCalledTimes(2);
});
it("keeps shell DOM and draft inputs on a same-page refresh, then moves focus on navigation", async () => {
  const view = (route) => (
    <div>
      <aside>
        <input aria-label="Tìm nhanh" defaultValue="" />
      </aside>
      <PageTransition route={route}>
        <h1 tabIndex={-1}>{route}</h1>
        <input aria-label="Bản nháp" defaultValue="" />
      </PageTransition>
    </div>
  );
  act(() => setScreen(view("tasks"), "tasks", { group: "workspace" }));
  render(<App initialize={false} />);
  const shell = document.querySelector("aside");
  fireEvent.change(screen.getByLabelText("Bản nháp"), {
    target: { value: "Đang nhập dở" },
  });
  act(() => setScreen(view("tasks"), "tasks", { group: "workspace" }));
  expect(screen.getByLabelText("Bản nháp").value).toBe("Đang nhập dở");
  act(() => setScreen(view("calendar"), "calendar", { group: "workspace" }));
  expect(document.querySelector("aside")).toBe(shell);
  await waitFor(() =>
    expect(document.activeElement).toBe(
      screen.getByRole("heading", { name: "calendar" }),
    ),
  );
});
it("never lets a delayed transition restore a workspace after logout", () => {
  act(() => setScreen(<p>Trang đầu</p>, "today", { group: "workspace" }));
  const callbacks = [];
  const skip = vi.fn();
  document.startViewTransition = vi.fn((callback) => {
    callbacks.push(callback);
    return { skipTransition: skip, finished: Promise.resolve() };
  });
  act(() => setScreen(<p>Trang tiếp theo</p>, "tasks", { group: "workspace" }));
  act(() => setScreen(<p>Đã đăng xuất</p>, "auth-login"));
  act(() => callbacks[0]());
  expect(getSnapshot().pageKey).toBe("auth-login");
  expect(getSnapshot().screenGroup).toBeNull();
  expect(skip).toHaveBeenCalledOnce();
});
it("respects both OS reduced motion and the saved pause setting", () => {
  const animate = vi.fn();
  HTMLElement.prototype.animate = animate;
  localStorage.setItem("planora.workspace.motion-paused", "true");
  const view = render(
    <PageTransition route="tasks">
      <h1>Việc</h1>
    </PageTransition>,
  );
  expect(animate).not.toHaveBeenCalled();
  localStorage.removeItem("planora.workspace.motion-paused");
  vi.spyOn(window, "matchMedia").mockReturnValue({ matches: true });
  view.rerender(
    <PageTransition route="time">
      <h1>Giờ rảnh</h1>
    </PageTransition>,
  );
  expect(animate).not.toHaveBeenCalled();
});
