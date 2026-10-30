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
      expect(detail.dataset.action).toBe("edit-event");
      expect(detail.dataset.id).toBe(event.id);
      expect(title.title).toBe(event.name);
    });
    expect(screen.getByText(/Cuộn trong khung lịch để xem thêm/)).toBeTruthy();
  },
);

it.each(["week", "agenda"])(
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
        view === "week" ? ".week-day" : ".agenda-day",
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
