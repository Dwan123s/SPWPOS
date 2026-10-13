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
  expect(schedule.getByText("12p")).toBeTruthy();
  const session = schedule.getByRole("button", { name: /Phiên ngắn 12 phút/ });
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
