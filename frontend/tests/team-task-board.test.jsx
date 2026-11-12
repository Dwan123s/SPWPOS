import { expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { TaskBoard } from "../src/team-task-board.jsx";

const makeTask = (index, extra = {}) => ({
  id: `task-${index}`,
  name: `Công việc ${index}`,
  status: "Open",
  priority: 2,
  deadline: new Date(Date.UTC(2026, 10, index + 1, 12)).toISOString(),
  estimatedMinutes: 60,
  progress: 0,
  description: `Ghi chú đầy đủ ${index}`,
  ...extra,
});
const detail = (tasks) => ({
  team: { id: "team", role: "Leader" },
  members: [
    { userId: "me", name: "Đặng Minh" },
    { userId: "other", name: "Bảo An" },
  ],
  tasks,
});
function board(tasks, props = {}) {
  return <TaskBoard detail={detail(tasks)} uid="me" {...props} />;
}
const rows = () => screen.queryAllByRole("article");
const statusButton = (name) =>
  within(screen.getByLabelText("Lọc trạng thái")).getByRole("button", { name });

it("keeps a large backlog on eight rows and archives completed work behind its status filter", () => {
  render(
    board([
      ...Array.from({ length: 19 }, (_, i) => makeTask(i)),
      makeTask(30, { status: "Completed", assigneeId: "me", progress: 100 }),
    ]),
  );
  expect(rows()).toHaveLength(8);
  expect(rows()[0].getAttribute("aria-label")).toBe("Công việc 0");
  expect(screen.getByText("1–8 / 19 công việc")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Trang công việc tiếp" }));
  expect(rows()).toHaveLength(8);
  expect(rows()[0].getAttribute("aria-label")).toBe("Công việc 8");
  fireEvent.click(statusButton(/^Hoàn thành/));
  expect(rows()).toHaveLength(1);
  expect(rows()[0].getAttribute("aria-label")).toBe("Công việc 30");
  expect(screen.getByText("Trang 1 / 1")).toBeTruthy();
});

it("combines owner, status and accent-insensitive search then resets pagination", () => {
  render(
    board([
      ...Array.from({ length: 10 }, (_, i) => makeTask(i)),
      makeTask(11, {
        name: "Đề xuất proposal",
        assigneeId: "me",
        status: "Assigned",
      }),
    ]),
  );
  fireEvent.click(screen.getByRole("button", { name: "Trang công việc tiếp" }));
  fireEvent.click(screen.getByRole("button", { name: "Việc của tôi" }));
  fireEvent.change(screen.getByRole("textbox"), {
    target: { value: "dang minh" },
  });
  expect(rows()).toHaveLength(1);
  expect(rows()[0].getAttribute("aria-label")).toBe("Đề xuất proposal");
  expect(screen.getByText("Trang 1 / 1")).toBeTruthy();
  fireEvent.click(statusButton(/^Chờ nhận/));
  expect(rows()).toHaveLength(0);
  expect(screen.getByText("Không có công việc phù hợp")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Đặt lại bộ lọc" }));
  expect(rows()).toHaveLength(8);
});

it("shows available work directly when switching from the completed filter", () => {
  render(
    board([
      makeTask(0),
      makeTask(1, { status: "Completed", assigneeId: "me" }),
    ]),
  );
  fireEvent.click(statusButton(/^Hoàn thành/));
  fireEvent.click(screen.getByRole("button", { name: "Có thể nhận" }));
  expect(rows()).toHaveLength(1);
  expect(rows()[0].getAttribute("aria-label")).toBe("Công việc 0");
});

it("orders urgent tasks before later deadlines when priority is selected without changing the source array", () => {
  const tasks = [makeTask(7), makeTask(9, { priority: 4 }), makeTask(0)];
  render(board(tasks));
  expect(rows()[0].getAttribute("aria-label")).toBe("Công việc 0");
  fireEvent.change(screen.getByRole("combobox"), {
    target: { value: "priority" },
  });
  expect(rows()[0].getAttribute("aria-label")).toBe("Công việc 9");
  expect(tasks.map((task) => task.id)).toEqual(["task-7", "task-9", "task-0"]);
});

it("opens complete requirements and submission files in a task popup, then hands off return without stacking dialogs", () => {
  const onReturn = vi.fn();
  const task = makeTask(0, {
    assigneeId: "me",
    status: "Assigned",
    completionSubmissionNote: "Báo cáo cần sửa",
    completionFiles: [{ id: "file", name: "proposal.docx", size: 42 }],
  });
  render(board([task], { onReturn }));
  expect(screen.queryByText(task.description)).toBeNull();
  expect(screen.queryByText("proposal.docx")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Chi tiết" }));
  const dialog = screen.getByRole("dialog", { name: task.name });
  expect(within(dialog).getByText(task.description)).toBeTruthy();
  expect(within(dialog).getByText("proposal.docx")).toBeTruthy();
  fireEvent.click(within(dialog).getByRole("button", { name: "Đóng" }));
  expect(screen.queryByRole("dialog")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: task.name }));
  fireEvent.click(screen.getByRole("button", { name: "Trả việc về nhóm" }));
  expect(onReturn).toHaveBeenCalledWith(task);
  expect(screen.queryByRole("dialog")).toBeNull();
});

it("opens when clicking the task row and closes on Escape without changing the task", () => {
  const onClaim = vi.fn();
  const task = makeTask(0);
  render(board([task], { onClaim }));
  fireEvent.click(screen.getByRole("article", { name: task.name }));
  const dialog = screen.getByRole("dialog", { name: task.name });
  fireEvent(dialog, new Event("cancel", { bubbles: false, cancelable: true }));
  expect(screen.queryByRole("dialog")).toBeNull();
  expect(onClaim).not.toHaveBeenCalled();
});

it("refreshes the open task from current data even when it leaves the active list", () => {
  const task = makeTask(0, { assigneeId: "me", status: "Assigned" });
  const { rerender } = render(board([task]));
  fireEvent.click(screen.getByRole("button", { name: task.name }));
  rerender(
    board([
      {
        ...task,
        status: "Completed",
        progress: 100,
        completionApproved: true,
        description: "Nội dung mới nhất",
      },
    ]),
  );
  const dialog = screen.getByRole("dialog", { name: task.name });
  expect(within(dialog).getByText("Nội dung mới nhất")).toBeTruthy();
  expect(within(dialog).getByText("Đã hoàn thành")).toBeTruthy();
  expect(
    within(dialog).queryByRole("button", {
      name: "Báo hoàn thành · Gửi duyệt",
    }),
  ).toBeNull();
  rerender(board([]));
  expect(screen.queryByRole("dialog")).toBeNull();
});

it("hands off task claiming from the popup and keeps failed progress updates visible for retry", () => {
  const onClaim = vi.fn(),
    onProgress = vi.fn();
  const task = makeTask(0);
  const { rerender } = render(board([task], { onClaim, onProgress }));
  fireEvent.click(screen.getByRole("button", { name: task.name }));
  fireEvent.click(
    within(screen.getByRole("dialog")).getByRole("button", {
      name: "Xem lịch trước khi nhận",
    }),
  );
  expect(onClaim).toHaveBeenCalledWith(task);
  expect(screen.queryByRole("dialog")).toBeNull();
  const assigned = { ...task, assigneeId: "me", status: "Assigned" };
  rerender(board([assigned], { onClaim, onProgress }));
  fireEvent.click(screen.getByRole("button", { name: task.name }));
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận 25%" }));
  expect(onProgress).toHaveBeenCalledWith(assigned, 25);
  rerender(
    board([assigned], { onClaim, onProgress, error: "Lỗi mạng, hãy thử lại" }),
  );
  expect(
    within(screen.getByRole("dialog")).getByRole("alert").textContent,
  ).toContain("Lỗi mạng");
});

it("preserves direct claim and leader review actions on their exact task with busy protection", () => {
  const onClaim = vi.fn(),
    onReviewCompletion = vi.fn();
  const open = makeTask(0),
    review = makeTask(1, {
      status: "PendingReview",
      assigneeId: "other",
      progress: 100,
    });
  const { rerender } = render(
    board([open, review], { onClaim, onReviewCompletion }),
  );
  fireEvent.click(
    screen.getByRole("button", { name: "Xem lịch trước khi nhận" }),
  );
  fireEvent.click(screen.getByRole("button", { name: "Kiểm tra & duyệt" }));
  expect(onClaim).toHaveBeenCalledWith(open);
  expect(onReviewCompletion).toHaveBeenCalledWith(review);
  rerender(board([open, review], { onClaim, onReviewCompletion, busy: true }));
  expect(
    screen.getByRole("button", { name: "Kiểm tra & duyệt" }).disabled,
  ).toBe(true);
  expect(
    screen.getByRole("button", { name: "Xem lịch trước khi nhận" }).disabled,
  ).toBe(true);
});

it("clamps the last page when live updates shrink the backlog and does not jump back when new tasks arrive", () => {
  const tasks = Array.from({ length: 17 }, (_, i) => makeTask(i));
  const { rerender } = render(board(tasks));
  fireEvent.click(screen.getByRole("button", { name: "Trang công việc tiếp" }));
  fireEvent.click(screen.getByRole("button", { name: "Trang công việc tiếp" }));
  expect(rows()).toHaveLength(1);
  rerender(board(tasks.slice(0, 8)));
  expect(screen.getByText("Trang 1 / 1")).toBeTruthy();
  expect(rows()).toHaveLength(8);
  rerender(board(tasks));
  expect(screen.getByText("Trang 1 / 3")).toBeTruthy();
});
