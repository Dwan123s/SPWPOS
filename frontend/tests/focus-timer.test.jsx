import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import {
  FocusLauncher,
  FocusTimerProvider,
  UpcomingFocus,
} from "../src/focus-timer.jsx";
import { ScheduledFocus } from "../src/scheduled-focus.jsx";
import {
  adjustFocus,
  clampFocusPosition,
  completeFocus,
  elapsedFocus,
  focusStorageKey,
  freshFocus,
  readFocus,
  remainingFocus,
  startFocus,
  tickFocus,
  toggleFocus,
} from "../src/focus-timer.js";

const initialTime = new Date("2026-11-04T03:00:00Z").getTime();
const workspace = {
  tasks: [{ id: "task-1", name: "Thiết kế Focus", status: "Todo" }],
  sessions: [],
  events: [
    {
      id: "meeting",
      name: "Họp nhóm",
      start: new Date(initialTime + 30 * 60000).toISOString(),
      end: new Date(initialTime + 60 * 60000).toISOString(),
    },
  ],
};
beforeEach(() => {
  localStorage.clear();
  vi.useFakeTimers();
  vi.setSystemTime(initialTime);
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("countdown transitions", () => {
  it("freezes remaining time on pause and resumes using a fresh deadline", () => {
    const start = startFocus(freshFocus(), null, initialTime);
    const paused = toggleFocus(start, initialTime + 60000);
    expect(remainingFocus(paused.timer, initialTime + 3600000)).toBe(
      24 * 60000,
    );
    const resumed = toggleFocus(paused, initialTime + 3600000);
    expect(remainingFocus(resumed.timer, initialTime + 3660000)).toBe(
      23 * 60000,
    );
  });
  it("catches up after sleep without inventing work cycles", () => {
    const start = startFocus(freshFocus(), null, initialTime);
    const rest = tickFocus(start, initialTime + 26 * 60000);
    expect(rest.timer.phase).toBe("break");
    expect(remainingFocus(rest.timer, initialTime + 26 * 60000)).toBe(
      4 * 60000,
    );
    const next = tickFocus(start, initialTime + 24 * 3600000);
    expect(next.timer).toMatchObject({
      phase: "work",
      round: 2,
      status: "paused",
      remainingMs: 25 * 60000,
    });
  });
  it("supports manual breaks and long breaks after round four", () => {
    let model = startFocus(freshFocus(), null, initialTime);
    model.settings.autoBreak = false;
    expect(tickFocus(model, model.timer.deadline).timer).toMatchObject({
      phase: "break",
      status: "paused",
    });
    model.timer.round = 4;
    expect(tickFocus(model, model.timer.deadline).timer).toMatchObject({
      phase: "longBreak",
      durationMs: 15 * 60000,
    });
  });
  it("counts Stopwatch up indefinitely, excludes pauses and freezes when completed", () => {
    const start = startFocus(
      { ...freshFocus(), mode: "stopwatch" },
      null,
      initialTime,
    );
    const later = initialTime + 4 * 3600000;
    expect(tickFocus(start, later)).toBe(start);
    expect(elapsedFocus(start.timer, later)).toBe(4 * 3600000);
    const paused = toggleFocus(start, later);
    expect(elapsedFocus(paused.timer, later + 60000)).toBe(4 * 3600000);
    const resumed = toggleFocus(paused, later + 60000);
    const complete = completeFocus(resumed, later + 120000);
    expect(complete.timer.status).toBe("completed");
    expect(elapsedFocus(complete.timer, later + 3600000)).toBe(
      4 * 3600000 + 60000,
    );
    expect(adjustFocus(start, 5, later)).toBe(start);
  });
  it("bounds adjustments, keeps paused timers paused and restores by account", () => {
    const model = toggleFocus(
      startFocus(freshFocus(), { title: "Thiết kế Focus" }, initialTime),
      initialTime,
    );
    const longer = adjustFocus(model, 1000, initialTime);
    expect(longer.timer.status).toBe("paused");
    expect(longer.timer.remainingMs).toBe(180 * 60000);
    expect(adjustFocus(model, -1000, initialTime).timer.remainingMs).toBe(1000);
    localStorage.setItem(focusStorageKey("a"), JSON.stringify(longer));
    expect(readFocus("a").timer.title).toBe("Thiết kế Focus");
    expect(readFocus("b").timer).toBeNull();
    localStorage.setItem(
      focusStorageKey("a"),
      '{"mode":"focus","timer":{"status":"running"}}',
    );
    expect(readFocus("a").timer).toBeNull();
    expect(clampFocusPosition({ x: 9000, y: -10 }, 390, 844)).toEqual({
      x: 234,
      y: 12,
    });
  });
});

function Harness({ page = "today", uid = "a" }) {
  return (
    <FocusTimerProvider uid={uid} workspace={workspace}>
      <FocusLauncher />
      {page === "today" ? (
        <>
          <ScheduledFocus workspace={workspace} />
          <UpcomingFocus workspace={workspace} />
        </>
      ) : (
        <h1>Lịch</h1>
      )}
    </FocusTimerProvider>
  );
}
function openAndStart() {
  fireEvent.click(
    screen.getAllByRole("button", {
      name: /Mở Stopwatch \/ Pomodoro|Pomodoro đang chạy/,
    })[0],
  );
  fireEvent.click(screen.getByRole("button", { name: "Bắt đầu Pomodoro" }));
}

it("keeps one timer through minimize, pause, navigation, hide and reload", () => {
  const view = render(<Harness />);
  openAndStart();
  fireEvent.click(screen.getByRole("button", { name: "Thu nhỏ bộ đếm" }));
  const mini = screen.getByRole("group", { name: /Bộ đếm thu nhỏ/ });
  // Minimal design: exactly a clock button and pause button, no task/title/window toolbar.
  expect(within(mini).getAllByRole("button")).toHaveLength(2);
  expect(within(mini).queryByText("Tập trung tự do")).toBeNull();
  act(() => vi.advanceTimersByTime(60000));
  expect(within(mini).getByRole("timer").textContent).toBe("24:00");
  expect(
    within(screen.getByRole("region", { name: "Phiên tập trung" })).getByRole(
      "timer",
    ).textContent,
  ).toBe("29:00");
  fireEvent.click(within(mini).getByRole("button", { name: "Tạm dừng" }));
  act(() => vi.advanceTimersByTime(120000));
  expect(within(mini).getByRole("timer").textContent).toBe("24:00");
  view.rerender(<Harness page="calendar" />);
  expect(within(mini).getByRole("timer").textContent).toBe("24:00");
  fireEvent.click(within(mini).getByRole("button", { name: "Tiếp tục" }));
  fireEvent.click(
    within(mini).getByRole("button", { name: "Phóng to Stopwatch / Pomodoro" }),
  );
  fireEvent.click(screen.getByRole("button", { name: "Ẩn bộ đếm" }));
  expect(screen.queryByRole("dialog")).toBeNull();
  act(() => vi.advanceTimersByTime(60000));
  view.unmount();
  render(<Harness />);
  fireEvent.click(
    screen.getAllByRole("button", {
      name: /Mở Stopwatch \/ Pomodoro|Pomodoro đang chạy/,
    })[0],
  );
  expect(
    within(screen.getByRole("dialog")).getByRole("timer").textContent,
  ).toBe("23:00");
});

it("configures a standalone timer, adjusts remaining time and warns about upcoming conflicts", () => {
  const before = JSON.stringify(workspace);
  render(<Harness />);
  fireEvent.click(
    screen.getAllByRole("button", {
      name: /Mở Stopwatch \/ Pomodoro|Pomodoro đang chạy/,
    })[0],
  );
  const dialog = within(screen.getByRole("dialog"));
  fireEvent.click(dialog.getByRole("button", { name: /Cấu hình thời gian/ }));
  fireEvent.click(dialog.getByRole("button", { name: "50/10" }));
  expect(dialog.queryByRole("combobox")).toBeNull();
  fireEvent.click(dialog.getByRole("button", { name: "Bắt đầu Pomodoro" }));
  expect(dialog.queryByText("Thiết kế Focus")).toBeNull();
  expect(dialog.getByRole("timer").textContent).toBe("50:00");
  expect(dialog.getByText(/Bộ đếm sẽ trùng/)).toBeTruthy();
  fireEvent.click(dialog.getByRole("button", { name: "Bớt 5 phút" }));
  expect(dialog.getByRole("timer").textContent).toBe("45:00");
  fireEvent.click(dialog.getByRole("button", { name: "Kết thúc phiên" }));
  expect(dialog.getByText("Đã kết thúc phiên")).toBeTruthy();
  expect(JSON.stringify(workspace)).toBe(before);
});

it("drags the mini ring, clamps to the viewport and persists its position", () => {
  // jsdom lacks PointerEvent; MouseEvent supplies pointer coordinates for this gesture.
  vi.stubGlobal("PointerEvent", MouseEvent);
  render(<Harness />);
  openAndStart();
  fireEvent.click(screen.getByRole("button", { name: "Thu nhỏ bộ đếm" }));
  const mini = screen.getByRole("group", { name: /Bộ đếm thu nhỏ/ });
  fireEvent.pointerDown(mini, { button: 0, clientX: 900, clientY: 700 });
  fireEvent.pointerMove(mini, { clientX: 300, clientY: 300 });
  fireEvent.pointerUp(mini, { clientX: 300, clientY: 300 });
  expect(mini.style.left).toBe("256px");
  expect(
    JSON.parse(localStorage.getItem(focusStorageKey("a"))).position,
  ).toEqual({ x: 256, y: 200 });
  fireEvent.pointerDown(mini, { button: 0, clientX: 300, clientY: 300 });
  fireEvent.pointerMove(mini, { clientX: -900, clientY: -900 });
  fireEvent.pointerUp(mini);
  expect(mini.style.left).toBe("12px");
  expect(mini.style.top).toBe("12px");
  fireEvent.click(within(mini).getByRole("button", { name: "Tạm dừng" }));
  expect(within(mini).getByRole("button", { name: "Tiếp tục" })).toBeTruthy();
  vi.unstubAllGlobals();
});

it("keeps upcoming and empty-state sections available and stops timers after unmount", () => {
  const view = render(<Harness />);
  expect(
    screen.getByRole("region", { name: "Công việc sắp tới" }),
  ).toBeTruthy();
  openAndStart();
  act(() => vi.advanceTimersByTime(25 * 60000));
  expect(
    within(screen.getByRole("dialog")).getByText("Nghỉ ngắn"),
  ).toBeTruthy();
  view.rerender(<Harness uid="b" />);
  expect(screen.queryByRole("dialog")).toBeNull();
  expect(readFocus("b").timer).toBeNull();
  view.unmount();
  expect(vi.getTimerCount()).toBe(0);
  render(<UpcomingFocus workspace={{}} />);
  expect(screen.getByText(/Chưa có lịch sắp tới/)).toBeTruthy();
});
