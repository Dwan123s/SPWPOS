import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, within } from "@testing-library/react";
import { todayPage } from "../src/dashboard.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";

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
  state.workspace = structuredClone(workspace());
  Object.assign(state.workspace, {
    tasks: [],
    sessions: [],
    availability: [],
    events: [
      {
        id: "class",
        name: "PRN212",
        start: "2026-10-30T03:00:00Z",
        end: "2026-10-30T04:00:00Z",
      },
    ],
  });
  state.insights = {
    capacityFactor: 0.8,
    plannedMinutes: 0,
    actualMinutes: 0,
    remainingMinutes: 0,
    biases: [{ taskType: "All", samples: 0, ratio: 1 }],
  };
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

it("renders useful analytics for an imported timetable without pretending it has actual execution data", () => {
  render(todayPage());
  const overview = within(
    screen.getByRole("region", { name: "Tổng quan lịch hôm nay" }),
  );
  expect(overview.getByText("50%")).toBeTruthy();
  expect(overview.getByText("Thời lượng lịch đã trôi")).toBeTruthy();
  expect(overview.getByText("30p còn lại")).toBeTruthy();
  expect(screen.queryByText("0 mẫu")).toBeNull();
  const horizon = screen.getByRole("region", { name: "Khả năng trong 7 ngày" });
  expect(within(horizon).getByText("1h")).toBeTruthy();
  expect(horizon.querySelector(".horizon-planned").style.height).toBe("100%");
  expect(horizon.querySelector(".horizon-capacity")).toBeNull();
  expect(
    within(horizon).getByText("Chưa đặt giờ rảnh để tính khả năng xếp thêm."),
  ).toBeTruthy();
  const milestones = within(
    screen.getByRole("region", { name: "Lịch sắp tới" }),
  );
  const entry = milestones.getByRole("button", { name: /PRN212/ });
  expect(entry.dataset.action).toBe("edit-event");
  expect(entry.dataset.id).toBe("class");
  expect(milestones.getByText(/Đang diễn ra · Kết thúc/)).toBeTruthy();
});
it("keeps real estimation results and hard task deadlines when recorded work exists", () => {
  state.insights.plannedMinutes = 100;
  state.insights.actualMinutes = 125;
  state.workspace.tasks = [
    {
      id: "task",
      name: "Nộp báo cáo",
      status: "Todo",
      priority: 2,
      isHardDeadline: true,
      deadline: "2026-10-31T04:00:00Z",
      remainingMinutes: 60,
    },
  ];
  render(todayPage());
  const estimation = within(
    screen.getByRole("region", { name: "Độ khớp ước lượng" }),
  );
  expect(estimation.getByText("80%")).toBeTruthy();
  expect(estimation.getByText("Dự kiến so với thực tế")).toBeTruthy();
  const deadlines = within(
    screen.getByRole("region", { name: "Deadline cần giữ" }),
  );
  expect(
    deadlines.getByRole("button", { name: /Nộp báo cáo/ }).dataset.action,
  ).toBe("task-detail");
});
it("refreshes calendar progress at the event boundary and continues showing historical time", () => {
  state.workspace.events[0].end = "2026-10-30T03:30:10Z";
  render(todayPage());
  act(() => vi.advanceTimersByTime(10000));
  const overview = within(
    screen.getByRole("region", { name: "Tổng quan lịch hôm nay" }),
  );
  expect(overview.getByText("100%")).toBeTruthy();
  expect(overview.getByText(/1 đã qua · 0 đang diễn ra/)).toBeTruthy();
  expect(overview.getByText("0p còn lại")).toBeTruthy();
});
