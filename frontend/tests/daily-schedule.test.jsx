import { render, fireEvent, screen, within } from "@testing-library/react";
import { beforeEach, expect, it } from "vitest";
import { todayPage } from "../src/dashboard.jsx";
import { state } from "../src/ui.jsx";
import { workspace, insights } from "./fixtures.js";

beforeEach(() => {
  state.workspace = structuredClone(workspace());
  state.insights = insights;
  const day = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
  }).format(new Date());
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

it("shows a centered add-task action and a date filter in the empty deadlines card", () => {
  state.workspace.tasks.forEach((task) => { task.deadline = null; });
  render(todayPage());
  const section = screen.getByRole("region", { name: "Công việc sắp đến hạn" });
  const card = within(section);
  expect(card.getByText("Không có việc nào sắp đến hạn.")).toBeTruthy();
  expect(card.getByRole("button", { name: "Thêm công việc" }).dataset.action).toBe("new-task");
  expect(card.queryByRole("button", { name: "Xem tất cả công việc" })).toBeNull();
  fireEvent.change(card.getByRole("combobox", { name: "Khoảng thời gian đến hạn" }), { target: { value: "day" } });
  expect(card.getByRole("heading", { name: "Đến hạn hôm nay hoặc quá hạn" })).toBeTruthy();
});

it("shows the completed session with its task deadline and opens the summary from the date", () => {
  state.workspace.sessions[0].status = "Completed";
  state.workspace.tasks[0].deadline = "2026-11-11T15:00:00+07:00";
  render(todayPage());
  const schedule = within(
    screen.getByRole("region", { name: "Lịch hôm nay", exact: true }),
  );
  expect(schedule.getByRole("heading", { name: "Đã hoàn thành" })).toBeTruthy();
  const session = schedule.getByRole("button", { name: /Phiên ngắn 12 phút/ });
  expect(session.getAttribute("data-id")).toBe("short");
  const due = within(session).getByText(/^Hạn 11[/\-]11$/);
  expect(due.getAttribute("datetime")).toBe(state.workspace.tasks[0].deadline);
  const trigger = schedule.getByLabelText("Tóm tắt lịch hôm nay");
  fireEvent.click(trigger);
  expect(trigger.closest("details").open).toBe(true);
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
