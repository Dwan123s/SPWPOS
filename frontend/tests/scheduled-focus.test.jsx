import { afterEach, beforeEach, expect, it, vi } from "vitest";
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import { ScheduledFocus } from "../src/scheduled-focus.jsx";
import { scheduledFocus } from "../src/scheduled-focus.js";
import { todayPage } from "../src/dashboard.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";

const start = "2026-10-30T03:00:00Z";
const end = "2026-10-30T04:00:00Z";
let data;
beforeEach(() => {
  vi.useFakeTimers({
    toFake: [
      "Date",
      "setTimeout",
      "clearTimeout",
      "setInterval",
      "clearInterval",
    ],
  });
  vi.setSystemTime(new Date("2026-10-30T03:30:00Z"));
  data = structuredClone(workspace());
  data.tasks = [];
  data.sessions = [];
  data.events = [{ id: "course", name: "PRN212 - SE20B02", start, end }];
  state.workspace = data;
  state.insights = null;
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

it("automatically focuses a fixed timetable entry with a live remaining-time clock", () => {
  const before = JSON.stringify(data);
  render(<ScheduledFocus workspace={data} />);
  expect(
    screen.getByRole("heading", { name: "PRN212 - SE20B02" }),
  ).toBeTruthy();
  expect(screen.getByText("ĐANG DIỄN RA · TỰ ĐỘNG THEO LỊCH")).toBeTruthy();
  expect(screen.getByRole("timer").textContent).toBe("30:00");
  expect(screen.getByText("ĐẾM NGƯỢC KẾT THÚC")).toBeTruthy();
  expect(screen.getByRole("progressbar").getAttribute("value")).toBe("50");
  expect(screen.queryByRole("button", { name: /Bắt đầu|Tạm dừng/ })).toBeNull();
  act(() => vi.advanceTimersByTime(1000));
  expect(screen.getByRole("timer").textContent).toBe("29:59");
  expect(JSON.stringify(data)).toBe(before);
});
it("waits until start, activates at the boundary and switches to the next entry at end", () => {
  vi.setSystemTime(new Date("2026-10-30T02:59:50Z"));
  data.events[0].end = "2026-10-30T03:00:10Z";
  data.events.push({
    id: "next",
    name: "SWT301",
    start: "2026-10-30T03:00:10Z",
    end,
  });
  render(<ScheduledFocus workspace={data} />);
  expect(screen.getByText("BẮT ĐẦU SAU")).toBeTruthy();
  expect(screen.getByRole("timer").textContent).toBe("00:10");
  act(() => vi.advanceTimersByTime(10000));
  expect(screen.getByText("ĐANG DIỄN RA · TỰ ĐỘNG THEO LỊCH")).toBeTruthy();
  expect(
    screen.getByRole("heading", { name: "PRN212 - SE20B02" }),
  ).toBeTruthy();
  act(() => vi.advanceTimersByTime(10000));
  expect(screen.getByRole("heading", { name: "SWT301" })).toBeTruthy();
});
it("a stale running timer cannot block the current calendar entry, and urgent entries take priority", () => {
  data.sessions = [
    {
      id: "old",
      taskName: "Old timer",
      status: "Running",
      start: "2026-10-29T01:00:00Z",
      end: "2026-10-29T02:00:00Z",
    },
  ];
  expect(scheduledFocus(data).current.id).toBe("course");
  data.sessions.push({
    id: "running",
    taskName: "Working",
    status: "Running",
    start,
    end,
  });
  data.events.push({
    id: "urgent",
    name: "Việc khẩn",
    isUrgent: true,
    start,
    end,
  });
  expect(scheduledFocus(data).current.id).toBe("urgent");
});
it("supports scheduled work sessions, midnight and the next day without starting a backend timer", () => {
  data.events = [];
  data.sessions = [
    {
      id: "session",
      taskName: "Báo cáo",
      status: "Scheduled",
      start: "2026-10-29T23:30:00+07:00",
      end: "2026-10-30T00:30:00+07:00",
    },
  ];
  vi.setSystemTime(new Date("2026-10-30T00:00:00+07:00"));
  render(<ScheduledFocus workspace={data} />);
  expect(screen.getByRole("heading", { name: "Báo cáo" })).toBeTruthy();
  expect(screen.getByRole("timer").textContent).toBe("30:00");
  expect(data.sessions[0].status).toBe("Scheduled");
});
it("updates on refocus and clears its ticker when leaving the page", () => {
  const { unmount } = render(<ScheduledFocus workspace={data} />);
  vi.setSystemTime(new Date("2026-10-31T03:00:00Z"));
  fireEvent.focus(window);
  expect(screen.getByText("Không còn lịch sắp tới")).toBeTruthy();
  unmount();
  expect(vi.getTimerCount()).toBe(0);
});
it("counts fixed calendar events on today's schedule and highlights their active slot", () => {
  render(todayPage());
  const schedule = within(
    screen.getByRole("region", { name: "Lịch hôm nay", exact: true }),
  );
  expect(schedule.getByText("1 lịch")).toBeTruthy();
  const title = schedule.getByText("PRN212 - SE20B02");
  expect(
    title.closest(".daily-agenda-row").classList.contains("is-active"),
  ).toBe(true);
  expect(schedule.getByText("Đang diễn ra")).toBeTruthy();
});
