import { beforeEach, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { taskRows } from "../src/tasks.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";

beforeEach(() => {
  state.workspace = workspace();
});

it("keeps a long task list on pages and resets when the filter changes", async () => {
  const tasks = Array.from({ length: 25 }, (_, i) => ({
    ...state.workspace.tasks[1],
    id: `item-${i}`,
    name: `Work item ${i}`,
  }));
  const view = render(taskRows(tasks));
  expect(screen.getAllByRole("button", { name: /^Work item/ })).toHaveLength(
    12,
  );
  await userEvent.click(screen.getByRole("button", { name: "Trang sau" }));
  expect(screen.getByRole("button", { name: "Work item 12" })).toBeTruthy();
  expect(screen.queryByRole("button", { name: "Work item 0" })).toBeNull();
  view.rerender(taskRows([tasks[0]]));
  expect(screen.getByRole("button", { name: "Work item 0" })).toBeTruthy();
});

it("offers scheduling for unfinished tasks and preserves the full long title", () => {
  const task = {
    ...state.workspace.tasks[1],
    name: "A very long task title ".repeat(8),
  };
  render(taskRows([task, state.workspace.tasks[5]]));
  expect(screen.getByRole("button", { name: task.name.trim() }).title).toBe(
    task.name,
  );
  expect(
    screen.getAllByRole("button", { name: /^Xếp lịch công việc/ }),
  ).toHaveLength(1);
});

it("shows the on-track label when scheduled work covers the remaining time before the deadline", () => {
  vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-11-12T07:00:00Z"));
  const task = { ...state.workspace.tasks[1], deadline: "2026-11-12T12:00:00Z", remainingMinutes: 45 };
  state.workspace.sessions = [{ taskId: task.id, status: "Scheduled", start: "2026-11-12T08:00:00Z", end: "2026-11-12T08:45:00Z" }];
  render(taskRows([task]));
  expect(screen.getByTitle("Đúng tiến độ").textContent).toBe("Hôm nay");
});
