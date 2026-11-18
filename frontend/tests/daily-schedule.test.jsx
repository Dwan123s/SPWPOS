import { render, fireEvent, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { todayPage } from "../src/dashboard.jsx";
import { state } from "../src/ui.jsx";
import { workspace, insights } from "./fixtures.js";

beforeEach(() => {
  state.workspace = structuredClone(workspace());
  state.insights = insights;
  const day = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
  }).format(new Date());
  vi.spyOn(Date, "now").mockReturnValue(Date.parse(day + "T10:00:00+07:00"));
  state.workspace.events = [];
  state.workspace.sessions = [
    {
      id: "short",
      taskId: state.workspace.tasks[0].id,
      taskName: "Phiên ngắn 12 phút",
      start: `${day}T10:42:00+07:00`,
      end: `${day}T10:54:00+07:00`,
      status: "Scheduled",
    },
  ];
});
afterEach(() => vi.restoreAllMocks());
it("keeps setup first in Home when shown and restores it above deadlines after reopening", () => {
  render(todayPage());
  const setup = screen.getByRole("region", { name: "Bắt đầu" });
  const home = setup.parentElement;
  expect(home.firstElementChild).toBe(setup);
  fireEvent.click(screen.getByRole("button", { name: "Ẩn hướng dẫn" }));
  expect(home.firstElementChild.getAttribute("aria-labelledby")).toBe("home-deadlines-heading");
  fireEvent.click(screen.getByRole("button", { name: "Hiện hướng dẫn" }));
  expect(home.firstElementChild).toBe(screen.getByRole("region", { name: "Bắt đầu" }));
});
it("shows the full short-session title, time and duration with a working detail target", () => {
  render(todayPage());
  const schedule = within(
    screen.getByRole("region", { name: "Lịch hôm nay", exact: true }),
  );
  expect(schedule.getByText("Phiên ngắn 12 phút")).toBeTruthy();
  expect(schedule.getByText("10:42")).toBeTruthy();
  expect(schedule.getByText("10:54")).toBeTruthy();
  const session = schedule.getByRole("button", { name: /Phiên ngắn 12 phút/ });
  expect(within(session).getByText("12p")).toBeTruthy();
  expect(session.dataset.action).toBe("session-detail");
  expect(session.dataset.id).toBe("short");
  expect(session.dataset.openTask).toBe("true");
  fireEvent.click(schedule.getByRole("button", { name: "Dòng thời gian" }));
  expect(
    schedule
      .getByRole("button", { name: "Dòng thời gian" })
      .getAttribute("aria-pressed"),
  ).toBe("true");
  expect(
    schedule.getByRole("button", {
      name: /Phiên ngắn 12 phút, 10:42 đến 10:54/,
    }),
  ).toBeTruthy();
});

it("keeps the add-task action in the page header and keeps filtering available in the empty deadlines card", () => {
  state.workspace.tasks.forEach((task) => { task.deadline = null; });
  render(todayPage());
  const section = screen.getByRole("region", { name: "Công việc sắp đến hạn" });
  const card = within(section);
  expect(card.getByText("Không có việc nào sắp đến hạn.")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Thêm việc", exact: true }).dataset.action).toBe("new-task");
  expect(card.queryByRole("button", { name: "Thêm công việc" })).toBeNull();
  expect(card.queryByRole("button", { name: "Xem tất cả công việc" })).toBeNull();
  fireEvent.change(card.getByRole("combobox", { name: "Khoảng thời gian đến hạn" }), { target: { value: "day" } });
  expect(card.getByRole("heading", { name: "Đến hạn hôm nay hoặc quá hạn" })).toBeTruthy();
});

it("keeps completed sessions in the day summary and out of the upcoming agenda", () => {
  state.workspace.sessions[0].status = "Completed";
  state.workspace.tasks[0].deadline = "2026-11-11T15:00:00+07:00";
  render(todayPage());
  const schedule = within(
    screen.getByRole("region", { name: "Lịch hôm nay", exact: true }),
  );
  expect(schedule.queryByRole("button", { name: /Phiên ngắn 12 phút/ })).toBeNull();
  const trigger = schedule.getByLabelText("Tóm tắt lịch hôm nay");
  fireEvent.click(trigger);
  expect(trigger.closest("details").open).toBe(true);
  expect(schedule.getByRole("progressbar", { name: "Thời lượng phiên đã hoàn thành" }).value).toBe(12);
  fireEvent.keyDown(trigger, { key: "Escape" });
  expect(trigger.closest("details").open).toBe(false);
});

it("collapses and reopens the agenda from the home header", () => {
  render(todayPage());
  const toggle = screen.getByRole("button", { name: "Ẩn lịch trong ngày" });
  const agenda = screen.getByRole("complementary", { name: "Lịch trong ngày" });
  expect(toggle.getAttribute("aria-controls")).toBe(agenda.id);
  expect(toggle.getAttribute("aria-expanded")).toBe("true");
  fireEvent.click(toggle);
  expect(toggle.getAttribute("aria-expanded")).toBe("false");
  expect(agenda.closest("[inert]")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Hiện lịch trong ngày" }));
  expect(toggle.getAttribute("aria-expanded")).toBe("true");
  expect(agenda.closest("[inert]")).toBeNull();
});
