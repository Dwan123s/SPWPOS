import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { compile } from "tailwindcss";
import { sessionDetail } from "../src/calendar.jsx";
import { getSnapshot, hideModal } from "../src/store.js";
import { state } from "../src/ui.jsx";
import { calendarPage } from "../src/schedule.jsx";
import { todayPage } from "../src/dashboard.jsx";
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
afterEach(() => hideModal());

function addTeamSessions() {
  const name = "Phân tích tổng quan 41 công trình";
  state.workspace.tasks = [{
    id: "team-task",
    name,
    isTeamTask: true,
    teamName: "Nghiên cứu",
    teamChecklist: [
      { id: "one", text: "Lọc tài liệu theo phạm vi", completed: true },
      { id: "two", text: "Đối chiếu phương pháp nghiên cứu", completed: false },
      { id: "three", text: "Tổng hợp kết quả", completed: false },
    ],
  }];
  const session = {
    taskId: "team-task",
    taskName: name,
    status: "Scheduled",
  };
  state.workspace.sessions = [
    { ...session, id: "second", start: "2026-10-21T09:00:00Z", end: "2026-10-21T10:00:00Z" },
    { ...session, id: "skipped", status: "Skipped", start: "2026-10-21T06:00:00Z", end: "2026-10-21T07:00:00Z" },
    { ...session, id: "first", start: "2026-10-21T08:00:00Z", end: "2026-10-21T09:00:00Z" },
  ];
}

it.each(["week", "agenda", "board"])("shows distinct suggested team requirements in %s view", (view) => {
  state.calendarView = view;
  addTeamSessions();
  render(calendarPage());
  const first = screen.getByTitle("Gợi ý: Lọc tài liệu theo phạm vi").closest("button");
  const second = screen.getByTitle("Gợi ý: Đối chiếu phương pháp nghiên cứu").closest("button");
  expect(first.dataset.id).toBe("first");
  expect(first.querySelector(".block-time").textContent).toContain("Nhóm · Nghiên cứu");
  expect(screen.getByRole("button", { name: /^Meeting 1,/ }).textContent).not.toContain("Nhóm ·");
  expect(within(first).getByText("Lọc tài liệu theo phạm vi")).toBeTruthy();
  expect(second.dataset.id).toBe("second");
  expect(within(second).getByText("Đối chiếu phương pháp nghiên cứu")).toBeTruthy();
  expect(second.textContent).not.toMatch(/Phiên 2\/2|Phân tích tổng quan|Gợi ý ·/);
});

it("hides ended sessions and events in both Today schedule views", () => {
  vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-10-21T09:30:00Z"));
  addTeamSessions();
  state.workspace.events = [
    { id: "ended", name: "Sự kiện đã qua", start: "2026-10-21T07:00:00Z", end: "2026-10-21T08:00:00Z" },
    { id: "later", name: "Sự kiện sắp tới", start: "2026-10-21T11:00:00Z", end: "2026-10-21T12:00:00Z" },
  ];
  render(todayPage());
  const schedule = screen.getByRole("region", { name: "Lịch hôm nay" });
  for (const view of ["agenda", "timeline"]) {
    if (view === "timeline") fireEvent.click(within(schedule).getByRole("button", { name: "Dòng thời gian" }));
    expect(schedule.querySelector('[data-id="first"]')).toBeNull();
    expect(schedule.querySelector('[data-id="second"]')).toBeTruthy();
    expect(within(schedule).queryByText("Sự kiện đã qua")).toBeNull();
    expect(within(schedule).getByText("Sự kiện sắp tới")).toBeTruthy();
  }
});

it("shows every requirement suggested for the selected session in its details", () => {
  addTeamSessions();
  act(() => sessionDetail("second"));
  expect(getSnapshot().modal.subtitle).toBe("Phiên 2/2 · Công việc nhóm");
  render(getSnapshot().modal.body);
  const focus = within(screen.getByRole("region", { name: "Gợi ý cho phiên này" }));
  expect(focus.getByText("Đối chiếu phương pháp nghiên cứu")).toBeTruthy();
  expect(focus.getByText("Tổng hợp kết quả")).toBeTruthy();
  expect(focus.queryByText("Lọc tài liệu theo phạm vi")).toBeNull();
});

it("shows the original task name on every slot for older tasks without a checklist", () => {
  state.calendarView = "board";
  addTeamSessions();
  delete state.workspace.tasks[0].teamChecklist;
  render(calendarPage());
  const titles = screen.getAllByTitle(state.workspace.tasks[0].name);
  expect(titles.map((title) => title.closest("button").dataset.id)).toEqual(["skipped", "first", "second"]);
  expect(screen.queryByText(/Gợi ý/)).toBeNull();
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
