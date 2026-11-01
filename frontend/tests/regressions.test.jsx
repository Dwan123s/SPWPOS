import { describe, it, expect, beforeEach } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { state, loadLabel } from "../src/ui.jsx";
import {
  todayPage,
  insightsPage,
  scenarioPage,
  notificationsPage,
  settingsPage,
  adminPage,
} from "../src/pages.jsx";
import { taskForm } from "../src/tasks.jsx";
import { tasksPage } from "../src/page-tasks.jsx";
import { timePage } from "../src/page-time.jsx";
import { weekStart, calendarPage } from "../src/schedule.jsx";
import { workspace, insights, resetFixtures } from "./fixtures.js";
beforeEach(() => {
  resetFixtures();
  state.workspace = workspace();
  state.insights = insights;
  state.roles = ["Admin"];
  state.conflicts = [];
  state.calendarStart = null;
  state.calendarView = "week";
  state.scenario = null;
  state.settingsTab = "profile";
});
describe("React view regressions", () => {
  it("keeps unavailable analysis unknown instead of inventing low risk", () => {
    state.insights = null;
    const html = renderToStaticMarkup(todayPage());
    expect(html).toContain("Chưa xác định");
    expect(html).not.toContain("0% thời gian rảnh");
    expect(renderToStaticMarkup(insightsPage())).toContain(
      "Chưa có kết quả phân tích",
    );
  });
  it("uses demand inside the horizon rather than the entire backlog", () => {
    expect(
      loadLabel({
        riskLevel: "Low",
        effectiveCapacityMinutes: 1000,
        loadRatio: 0.3,
        remainingMinutes: 1500,
        horizonDemandMinutes: 300,
      }),
    ).toContain("300 phút đến hạn");
  });
  it("starts calendar weeks Monday across month and year boundaries", () => {
    expect(weekStart("2026-10-07")).toBe("2026-10-05");
    expect(weekStart("2026-10-04")).toBe("2026-09-28");
    expect(weekStart("2027-01-01")).toBe("2026-12-28");
  });
  it("does not repeat a fixed event ending at midnight on the next day", () => {
    state.workspace = {
      preference: { timeZoneId: "UTC" },
      sessions: [],
      events: [
        {
          id: "event",
          name: "Midnight boundary",
          start: "2026-10-05T22:00:00Z",
          end: "2026-10-06T00:00:00Z",
        },
      ],
    };
    state.calendarStart = "2026-10-07";
    const html = renderToStaticMarkup(calendarPage());
    expect(state.calendarStart).toBe("2026-10-05");
    const calendar = document.createElement("div");
    calendar.innerHTML = html;
    expect(
      calendar.querySelectorAll('[data-action="event-detail"][data-id="event"]'),
    ).toHaveLength(1);
    expect(html).toContain("22:00 — 24:00");
  });
  it.each(["week", "agenda"])(
    "keeps fixed events in their day instead of applying Tailwind fixed positioning (%s)",
    (view) => {
      state.workspace = {
        preference: { timeZoneId: "Asia/Ho_Chi_Minh" },
        sessions: [],
        events: [
          {
            id: "meeting",
            name: "Cuộc họp buổi trưa",
            start: "2026-10-19T12:00:00+07:00",
            end: "2026-10-19T13:00:00+07:00",
          },
        ],
      };
      state.calendarStart = "2026-10-19";
      state.calendarView = view;
      const root = document.createElement("div");
      root.innerHTML = renderToStaticMarkup(calendarPage());
      const event = root.querySelector(
        '[data-action="event-detail"][data-id="meeting"]',
      );
      const day = event.closest(view === "week" ? ".week-day" : ".agenda-day");
      expect(day.querySelector("strong").textContent).toBe("19");
      expect(day.textContent).toContain("12:00 — 13:00");
      expect(root.querySelectorAll(".fixed-event")).toHaveLength(1);
      expect(root.querySelector(".fixed")).toBeNull();
      expect(root.querySelectorAll('[data-id="meeting"]')).toHaveLength(1);
    },
  );
  it("retains selected date headings in extreme time zones", () => {
    state.workspace = {
      preference: { timeZoneId: "Pacific/Kiritimati" },
      sessions: [],
      events: [],
    };
    state.calendarStart = "2026-10-05";
    expect(renderToStaticMarkup(calendarPage())).toContain(
      "<h2>05-10 — 11/10/2026</h2>",
    );
  });
  it.each([
    todayPage,
    tasksPage,
    calendarPage,
    timePage,
    insightsPage,
    scenarioPage,
    notificationsPage,
    settingsPage,
    adminPage,
  ])(
    "renders a native React screen without object or template artifacts (%s)",
    (view) => {
      const html = renderToStaticMarkup(view());
      expect(html.length).toBeGreaterThan(100);
      expect(html).not.toMatch(
        /\[object Object\]|__PLANORA_EXPR_|&lt;(div|section|form)/,
      );
    },
  );
  it.each(["profile", "categories", "security", "notifications"])(
    "renders settings tab %s",
    (tab) => {
      state.settingsTab = tab;
      expect(renderToStaticMarkup(settingsPage())).toContain(
        "settings-content-card",
      );
    },
  );
  it("escapes user text through React and preserves edit selections", () => {
    const task = {
      ...state.workspace.tasks[0],
      name: "<script>alert(1)</script>",
      priority: 4,
    };
    const html = renderToStaticMarkup(taskForm(task));
    expect(html).toContain("&lt;script&gt;");
    expect(html).not.toContain("<script>");
    expect(html).toContain('value="4" selected=""');
  });
});
