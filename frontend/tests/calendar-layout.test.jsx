import { beforeEach, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
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
  "keeps fixed events in their day and normal document flow in %s view",
  async (view) => {
    state.calendarView = view;
    const { container } = render(calendarPage());
    // Use real Tailwind utilities: the former `fixed` event modifier took
    // cards out of document flow and made them span the entire calendar.
    const classes = Array.from(container.querySelectorAll("[class]"))
      .flatMap((element) => Array.from(element.classList));
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
        const card = title.closest(
          view === "week" ? ".calendar-block" : ".session-card",
        );
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
