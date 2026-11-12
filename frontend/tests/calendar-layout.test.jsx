import { beforeEach, expect, it } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { compile } from "tailwindcss";
import { state } from "../src/ui.jsx";
import { calendarPage } from "../src/schedule.jsx";
import { workspace } from "./fixtures.js";

beforeEach(() => {
  state.workspace = {
    ...workspace(),
    preference: { timeZoneId: "Asia/Ho_Chi_Minh" },
    tasks: [],
    sessions: [],
    events: Array.from({ length: 7 }, (_, index) => ({
      id: `event-${index}`,
      name: `Meeting ${index + 1}`,
      start: `2026-10-${19 + index}T08:00:00Z`,
      end: `2026-10-${19 + index}T10:00:00Z`,
    })),
  };
  state.conflicts = [];
  state.calendarStart = "2026-10-19";
});

it.each(["week", "agenda"])(
  "keeps every event reachable inside the keyboard-accessible calendar when a day is busy in %s view",
  (view) => {
    state.calendarView = view;
    state.workspace.events = Array.from({ length: 60 }, (_, index) => ({
      id: `busy-${index}`,
      name: `Công việc có tên đầy đủ cần xem chi tiết ${index + 1}`,
      start: "2026-10-21T08:00:00Z",
      end: "2026-10-21T08:30:00Z",
    }));
    render(calendarPage());
    const viewport = screen.getByRole("region", {
      name:
        view === "week"
          ? "Lịch 7 ngày, cuộn dọc và ngang để xem thêm"
          : "Danh sách lịch 7 ngày, cuộn để xem thêm",
    });
    expect(viewport.tabIndex).toBe(0);
    viewport.focus();
    expect(document.activeElement).toBe(viewport);
    state.workspace.events.forEach((event) => {
      const title = within(viewport).getByText(event.name);
      const entry = title.closest(".calendar-timed-card");
      const detail = entry;
      expect(detail.dataset.action).toBe("event-detail");
      expect(detail.dataset.id).toBe(event.id);
      expect(title.title).toBe(event.name);
    });
    expect(screen.getByText(/Cuộn trong khung lịch để xem thêm/)).toBeTruthy();
  },
);

it("opens details from calendar cards and keeps deletion controls inside the popup", () => {
  state.calendarView = "week";
  state.workspace.sessions = [
    {
      id: "scheduled",
      taskName: "Chưa làm",
      status: "Scheduled",
      start: "2026-10-21T08:00:00Z",
      end: "2026-10-21T09:00:00Z",
      actualSeconds: 0,
    },
    {
      id: "locked",
      taskName: "Đã khóa",
      status: "Scheduled",
      isLocked: true,
      start: "2026-10-21T09:00:00Z",
      end: "2026-10-21T10:00:00Z",
      actualSeconds: 0,
    },
    {
      id: "running",
      taskName: "Đang làm",
      status: "Running",
      start: "2026-10-21T10:00:00Z",
      end: "2026-10-21T11:00:00Z",
    },
    {
      id: "completed",
      taskName: "Đã làm",
      status: "Completed",
      start: "2026-10-21T11:00:00Z",
      end: "2026-10-21T12:00:00Z",
    },
  ];
  render(calendarPage());
  expect(screen.queryByRole("button", { name: /Xóa/ })).toBeNull();
  state.workspace.sessions.forEach((session) => {
    const card = screen.getByText(session.taskName).closest("button");
    expect(card.dataset.action).toBe("session-detail");
    expect(card.dataset.id).toBe(session.id);
  });
});

it.each(["week", "agenda", "board"])(
  "keeps fixed events in their day and normal document flow in %s view",
  async (view) => {
    state.calendarView = view;
    const { container } = render(calendarPage());
    // Use real Tailwind utilities: the former `fixed` event modifier took
    // cards out of document flow and made them span the entire calendar.
    const classes = Array.from(container.querySelectorAll("[class]")).flatMap(
      (element) => Array.from(element.classList),
    );
    const utilities = await compile("@tailwind utilities;");
    const style = document.createElement("style");
    style.textContent = utilities.build(classes);
    document.head.append(style);
    try {
      const days = container.querySelectorAll(
        { week: ".week-day", agenda: ".agenda-day", board: ".plan-day" }[view],
      );
      expect(days).toHaveLength(7);
      state.workspace.events.forEach((event, index) => {
        const title = screen.getByText(event.name);
        const card = title.closest(".calendar-block");
        expect(days[index].contains(card)).toBe(true);
        expect(["fixed", "absolute"]).not.toContain(
          getComputedStyle(card).position,
        );
      });
    } finally {
      style.remove();
    }
  },
);
