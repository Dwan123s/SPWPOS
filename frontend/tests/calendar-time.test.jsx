import { afterEach, beforeEach, expect, it, vi } from "vitest";
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import { calendarPage } from "../src/schedule.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";

const now = new Date("2026-10-30T03:00:00Z"); // 10:00 in Ho Chi Minh City.
beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date", "setTimeout", "clearTimeout"] });
  vi.setSystemTime(now);
  state.calendarStart = "2026-10-26";
  state.calendarView = "week";
  state.conflicts = [];
  state.workspace = {
    ...workspace(),
    tasks: [],
    sessions: [],
    events: [
      {
        id: "past",
        name: "Buổi đã qua",
        start: "2026-10-30T01:00:00Z",
        end: now.toISOString(),
      },
      {
        id: "current",
        name: "Buổi đang học",
        start: now.toISOString(),
        end: "2026-10-30T04:00:00Z",
      },
      {
        id: "future",
        name: "Buổi sắp tới",
        start: "2026-10-30T07:00:00Z",
        end: "2026-10-30T08:00:00Z",
      },
    ],
  };
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

it.each(["week", "agenda"])(
  "distinguishes actual time for events and sessions in %s view",
  (view) => {
    state.calendarView = view;
    state.workspace.sessions = [
      {
        id: "stale-running",
        taskId: "task",
        taskName: "Phiên cũ chưa kết thúc timer",
        status: "Running",
        start: "2026-10-30T00:00:00Z",
        end: "2026-10-30T01:00:00Z",
        actualSeconds: 0,
      },
    ];
    render(calendarPage());
    for (const [title, phase, label] of [
      ["Buổi đã qua", "past", "Đã qua"],
      ["Buổi đang học", "current", "Đang diễn ra"],
      ["Buổi sắp tới", "future", "Sắp tới"],
      ["Phiên cũ chưa kết thúc timer", "past", "Đã qua"],
    ]) {
      const card = screen.getByText(title).closest(".calendar-timed-card");
      expect(card.classList.contains(`calendar-time-${phase}`)).toBe(true);
      expect(
        within(card).getByText(label).classList.contains("calendar-time-tag"),
      ).toBe(true);
    }
    expect(screen.getByLabelText("Màu lịch theo thời gian").textContent).toBe(
      "Đã quaĐang diễn raSắp tới",
    );
  },
);

it("updates at start/end boundaries without reloading, then clears its timer", () => {
  state.workspace.events = [
    {
      id: "boundary",
      name: "Buổi kế tiếp",
      start: "2026-10-30T03:00:10Z",
      end: "2026-10-30T03:00:20Z",
    },
  ];
  const { unmount } = render(calendarPage());
  const card = () =>
    screen.getByText("Buổi kế tiếp").closest(".calendar-timed-card");
  expect(within(card()).getByText("Sắp tới")).toBeTruthy();
  act(() => vi.advanceTimersByTime(10000));
  expect(within(card()).getByText("Đang diễn ra")).toBeTruthy();
  act(() => vi.advanceTimersByTime(10000));
  expect(within(card()).getByText("Đã qua")).toBeTruthy();
  unmount();
  expect(vi.getTimerCount()).toBe(0);
});

it("refreshes a suspended tab and handles an event spanning local midnight", () => {
  state.workspace.events = [
    {
      id: "overnight",
      name: "Sự kiện qua đêm",
      start: "2026-10-29T16:00:00Z",
      end: "2026-10-30T04:00:00Z",
    },
  ];
  render(calendarPage());
  const cards = () =>
    screen
      .getAllByText("Sự kiện qua đêm")
      .map((title) => title.closest(".calendar-timed-card"));
  expect(cards()).toHaveLength(2);
  for (const card of cards())
    expect(within(card).getByText("Đang diễn ra")).toBeTruthy();
  vi.setSystemTime(new Date("2026-10-30T05:00:00Z"));
  fireEvent.focus(window);
  for (const card of cards())
    expect(within(card).getByText("Đã qua")).toBeTruthy();
});
