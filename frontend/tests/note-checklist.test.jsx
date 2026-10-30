import { beforeEach, expect, it, vi } from "vitest";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { useState } from "react";
import { NoteChecklist } from "../src/note-checklist.jsx";
import { state } from "../src/ui.jsx";

const task = {
  id: "task-1",
  name: "Viết báo cáo",
  description: "Tổng hợp kết quả tuần này",
  status: "Todo",
  progress: 0,
  estimatedMinutes: 90,
  actualMinutes: 30,
  deadline: "2026-10-30T10:00:00Z",
  version: "task-v1",
};
const note = { id: "note-1", createdTaskIds: [task.id] };
beforeEach(() => {
  state.workspace = { preference: { timeZoneId: "Asia/Ho_Chi_Minh" } };
});
function Harness({
  initial = { tasks: [task], sessions: [] },
  change,
  refresh = vi.fn(),
}) {
  const [workspace, setWorkspace] = useState(initial);
  return (
    <NoteChecklist
      note={note}
      workspace={workspace}
      onRefresh={refresh}
      onComplete={async (item, completed) => {
        await change?.(item, completed);
        setWorkspace((current) => ({
          ...current,
          tasks: current.tasks.map((t) =>
            t.id === item.id
              ? {
                  ...t,
                  status: completed ? "Completed" : "Todo",
                  progress: completed ? 100 : 0,
                  version: completed ? "task-v2" : "task-v3",
                }
              : t,
          ),
        }));
      }}
    />
  );
}
it("checks a linked task, groups completed work below, then allows reopening", async () => {
  const change = vi.fn();
  render(<Harness change={change} />);
  fireEvent.click(
    screen.getByRole("checkbox", { name: "Hoàn thành Viết báo cáo" }),
  );
  await screen.findByText("Đã hoàn thành · 1");
  expect(change).toHaveBeenCalledExactlyOnceWith(task, true);
  expect(screen.getByText("1/1 đã xong")).toBeTruthy();
  expect(
    screen.queryByRole("checkbox", { name: "Hoàn thành Viết báo cáo" }),
  ).toBeNull();
  fireEvent.click(screen.getByText("Đã hoàn thành · 1"));
  fireEvent.click(
    screen.getByRole("checkbox", { name: "Mở lại Viết báo cáo" }),
  );
  await screen.findByRole("checkbox", { name: "Hoàn thành Viết báo cáo" });
  expect(change.mock.calls[1][0].version).toBe("task-v2");
  expect(change.mock.calls[1][1]).toBe(false);
  expect(screen.queryByText("Đã hoàn thành · 1")).toBeNull();
});
it("does not infer completion from actual minutes or one completed session", () => {
  render(
    <Harness
      initial={{
        tasks: [{ ...task, actualMinutes: 100, progress: 40 }],
        sessions: [{ id: "past", taskId: task.id, status: "Completed" }],
      }}
    />,
  );
  expect(
    screen.getByRole("checkbox", { name: "Hoàn thành Viết báo cáo" }).checked,
  ).toBe(false);
  expect(screen.getByText(/40% hoàn thành/)).toBeTruthy();
});
it("shows task details and calendar sessions when the title is expanded", () => {
  render(
    <Harness
      initial={{
        tasks: [task],
        sessions: [
          {
            id: "planned",
            taskId: task.id,
            status: "Scheduled",
            start: "2026-10-26T10:00:00Z",
            end: "2026-10-26T11:00:00Z",
          },
        ],
      }}
    />,
  );
  fireEvent.click(screen.getByText(task.name));
  expect(screen.getByText(task.description)).toBeTruthy();
  expect(
    screen.getByRole("list", { name: "Lịch của Viết báo cáo" }).textContent,
  ).toContain("26");
  expect(
    screen.getByRole("button", { name: "Chỉnh sửa công việc" }).dataset.id,
  ).toBe(task.id);
  expect(screen.getByRole("button", { name: "Xếp lịch" }).dataset.action).toBe(
    "schedule-task",
  );
});
it.each(["Running", "Paused"])(
  "explains why a task with a %s session cannot be ticked",
  (status) => {
    const change = vi.fn();
    render(
      <Harness
        change={change}
        initial={{
          tasks: [task],
          sessions: [
            {
              id: "active",
              taskId: task.id,
              status,
              start: "2026-10-25T10:00:00Z",
              end: "2026-10-25T11:00:00Z",
            },
          ],
        }}
      />,
    );
    const checkbox = screen.getByRole("checkbox", {
      name: "Hoàn thành Viết báo cáo",
    });
    expect(checkbox.disabled).toBe(true);
    expect(screen.getByText(/Kết thúc phiên đang thực hiện/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Mở phiên" }).dataset.id).toBe(
      "active",
    );
    fireEvent.click(checkbox);
    expect(change).not.toHaveBeenCalled();
  },
);
it("blocks duplicate clicks and keeps the checkbox unchanged when saving fails", async () => {
  let reject;
  const change = vi.fn(
    () =>
      new Promise((resolve, fail) => {
        reject = fail;
      }),
  );
  render(<Harness change={change} />);
  const checkbox = screen.getByRole("checkbox", {
    name: "Hoàn thành Viết báo cáo",
  });
  fireEvent.click(checkbox);
  fireEvent.click(checkbox);
  expect(change).toHaveBeenCalledTimes(1);
  expect(checkbox.disabled).toBe(true);
  await act(async () => reject(new Error("Công việc đã thay đổi ở nơi khác.")));
  expect(screen.getByRole("alert").textContent).toContain("đã thay đổi");
  expect(checkbox.checked).toBe(false);
  expect(checkbox.disabled).toBe(false);
});
it("reads externally updated status and does not show unrelated tasks", () => {
  const props = {
    note,
    workspace: {
      tasks: [task, { ...task, id: "unrelated", name: "Việc khác" }],
      sessions: [],
    },
    onComplete: vi.fn(),
    onRefresh: vi.fn(),
  };
  const view = render(<NoteChecklist {...props} />);
  expect(screen.queryByText("Việc khác")).toBeNull();
  view.rerender(
    <NoteChecklist
      {...props}
      workspace={{
        tasks: [
          { ...task, status: "Completed", progress: 100, version: "latest" },
        ],
        sessions: [],
      }}
    />,
  );
  expect(screen.getByText("1/1 đã xong")).toBeTruthy();
  fireEvent.click(screen.getByText("Đã hoàn thành · 1"));
  expect(
    screen.getByRole("checkbox", { name: "Mở lại Viết báo cáo" }).checked,
  ).toBe(true);
});
it("refreshes server state on demand without resending a status change", async () => {
  const refresh = vi.fn();
  const change = vi.fn();
  render(<Harness change={change} refresh={refresh} />);
  fireEvent.click(screen.getByRole("button", { name: "Tải lại danh sách" }));
  await waitFor(() => expect(refresh).toHaveBeenCalledTimes(1));
  expect(change).not.toHaveBeenCalled();
});
it("keeps cancelled work separate from completed work and never offers a completion toggle for it", () => {
  render(
    <Harness
      initial={{ tasks: [{ ...task, status: "Cancelled" }], sessions: [] }}
    />,
  );
  fireEvent.click(screen.getByText("Đã hủy · 1"));
  expect(
    screen.getByRole("checkbox", { name: "Hoàn thành Viết báo cáo" }).disabled,
  ).toBe(true);
  expect(screen.getByText("0/0 đã xong")).toBeTruthy();
});
