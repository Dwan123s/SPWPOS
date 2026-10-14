import { act, fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, afterEach, expect, it, vi } from "vitest";
import { App } from "../src/App.jsx";
import { update } from "../src/store.js";
import { state, modal } from "../src/ui.jsx";
import {
  getGuide,
  startGuide,
  completeGuideStep,
  advanceGuide,
  skipGuide,
} from "../src/onboarding.js";
import {
  TutorialButton,
  MenuLabelsControl,
  NextStep,
  workflowSteps,
  GuidedTutorial,
} from "../src/tutorial.jsx";

beforeEach(() => {
  localStorage.clear();
  state.user = { id: crypto.randomUUID() };
  state.workspace = {
    tasks: [],
    sessions: [],
    availability: [],
    exceptions: [],
    profile: { id: "tutorial-user" },
  };
  location.hash = "calendar";
  update({
    screen: null,
    screenKey: "tutorial-test",
    modal: null,
    slots: {},
    toasts: [],
  });
});
afterEach(() => {
  vi.useRealTimers();
});
function mount(route = "calendar") {
  update({
    screen: (
      <div className="planning-console">
        <MenuLabelsControl />
        <TutorialButton route={route} />
        <GuidedTutorial route={route} />
        <NextStep />
      </div>
    ),
  });
  return render(<App initialize={false} />);
}
it("walks all four tutorial lessons, focuses the new lesson, and can be reopened", async () => {
  mount();
  fireEvent.click(
    screen.getByRole("button", { name: "Hướng dẫn", exact: true }),
  );
  expect(
    screen.getByRole("heading", { name: "1. Nhập việc cần làm" }),
  ).toBeTruthy();
  expect(screen.getByRole("button", { name: "Quay lại" }).disabled).toBe(true);
  for (const name of [
    "2. Chọn thời gian bạn rảnh",
    "3. Tạo và lưu lịch",
    "4. Làm việc và cập nhật tiến độ",
  ]) {
    fireEvent.click(screen.getByRole("button", { name: "Tiếp theo" }));
    expect(screen.getByRole("heading", { name })).toBe(document.activeElement);
  }
  expect(screen.getByText(/Xong phiên chỉ dừng đồng hồ/)).toBeTruthy();
  fireEvent.click(
    screen.getByRole("button", { name: "Đã hiểu, bắt đầu dùng" }),
  );
  expect(document.querySelector("#modal").open).toBe(false);
  fireEvent.click(
    screen.getByRole("button", { name: "Hướng dẫn", exact: true }),
  );
  expect(
    screen.getByRole("heading", { name: "1. Nhập việc cần làm" }),
  ).toBeTruthy();
});
it("tutorial navigation changes page without saving sample data", () => {
  const fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
  mount();
  fireEvent.click(
    screen.getByRole("button", { name: "Hướng dẫn", exact: true }),
  );
  fireEvent.click(screen.getByRole("button", { name: "Mở Nhập việc" }));
  expect(location.hash).toBe("#notes");
  expect(document.querySelector("#modal").open).toBe(false);
  expect(fetchMock).not.toHaveBeenCalled();
  vi.unstubAllGlobals();
});
it("shows first-use tutorial once per account", async () => {
  vi.useFakeTimers();
  location.hash = "today";
  const view = mount("today");
  await act(async () => {
    vi.advanceTimersByTime(400);
  });
  expect(document.querySelector("#modal").open).toBe(true);
  expect(
    screen.getByRole("heading", { name: "Chào mừng đến PLANORA" }),
  ).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Bỏ qua", exact: true }));
  view.unmount();
  const second = mount("today");
  await act(async () => {
    vi.advanceTimersByTime(400);
  });
  expect(document.querySelector("#modal").open).toBe(false);
  second.unmount();
  state.user = { id: crypto.randomUUID() };
  mount("today");
  await act(async () => {
    vi.advanceTimersByTime(400);
  });
  expect(document.querySelector("#modal").open).toBe(true);
});
it("does not replace a dialog opened before the first-use tutorial", async () => {
  vi.useFakeTimers();
  location.hash = "today";
  mount("today");
  act(() => modal("Thêm công việc", "", <p>Nội dung đang nhập</p>));
  await act(async () => {
    vi.advanceTimersByTime(400);
  });
  expect(screen.getByRole("heading", { name: "Thêm công việc" })).toBeTruthy();
  expect(getGuide().status).toBe("new");
});
it("requires real completion before continuing each interactive mission, and allows skipping", () => {
  startGuide();
  mount("notes");
  expect(
    screen.getByRole("button", { name: "Tiếp tục", exact: true }).disabled,
  ).toBe(true);
  expect(advanceGuide()).toBe(false);
  act(() => completeGuideStep(0, "saved-note"));
  fireEvent.click(
    screen.getByRole("button", { name: "Tiếp tục", exact: true }),
  );
  expect(location.hash).toBe("#time");
  expect(getGuide().noteId).toBe("saved-note");
  expect(getGuide().step).toBe(1);
  expect(
    screen.getByRole("button", { name: "Tiếp tục", exact: true }).disabled,
  ).toBe(true);
  act(() => {
    completeGuideStep(1);
    advanceGuide();
    completeGuideStep(2);
    advanceGuide();
  });
  expect(
    screen.getByRole("button", { name: "Hoàn thành hướng dẫn" }).disabled,
  ).toBe(true);
  act(() => completeGuideStep(3));
  fireEvent.click(screen.getByRole("button", { name: "Hoàn thành hướng dẫn" }));
  expect(getGuide().status).toBe("done");
  expect(screen.queryByRole("heading", { name: "Bắt đầu một phiên" })).toBe(
    null,
  );
  act(() => {
    startGuide();
    skipGuide();
  });
  expect(getGuide().status).toBe("skipped");
});
it("remembers the compact menu while showing names by default", () => {
  const first = mount();
  expect(document.querySelector(".planning-console").dataset.navMode).toBe(
    "expanded",
  );
  fireEvent.click(screen.getByRole("button", { name: "Thu gọn menu" }));
  expect(document.querySelector(".planning-console").dataset.navMode).toBe(
    "compact",
  );
  first.unmount();
  mount();
  expect(
    screen
      .getByRole("button", { name: "Hiện tên menu" })
      .getAttribute("aria-expanded"),
  ).toBe("false");
});
it("chooses the next step from saved work, available hours, and active/future sessions", () => {
  const w = state.workspace;
  const next = () => workflowSteps(w, 1000).find((step) => !step.done).label;
  expect(next()).toBe("Nhập việc cần làm");
  w.tasks = [{ status: "Todo" }];
  expect(next()).toBe("Chọn giờ rảnh");
  w.availability = [{ dayOfWeek: 1 }];
  expect(next()).toBe("Xem lịch đề xuất");
  w.sessions = [{ status: "Completed", end: new Date(2000).toISOString() }];
  expect(next()).toBe("Xem lịch đề xuất");
  w.sessions = [{ status: "Scheduled", end: new Date(500).toISOString() }];
  expect(next()).toBe("Xem lịch đề xuất");
  w.sessions = [{ status: "Scheduled", end: new Date(2000).toISOString() }];
  expect(next()).toBe("Mở lịch để làm việc");
});
