import { expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { TaskBoard } from "../src/team-task-board.jsx";
import { TeamTaskSplitDialog } from "../src/team-task-checklist.jsx";

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
const boardNow = Date.parse("2026-11-01T00:00:00Z");
function board(tasks, props = {}) {
  return <TaskBoard detail={detail(tasks)} uid="me" now={boardNow} {...props} />;
}
const rows = () => screen.queryAllByRole("article");
const filterStatus = (value) =>
  fireEvent.change(screen.getByRole("combobox", { name: "Lọc trạng thái" }), { target: { value } });

it("shows hours and minutes for longer team tasks in the board and detail", () => {
  render(board([
    makeTask(0),
    makeTask(1, { estimatedMinutes: 90 }),
    makeTask(2, { estimatedMinutes: 120 }),
  ]));
  expect(screen.getByText("60 phút")).toBeTruthy();
  expect(screen.getByText("1 giờ 30 phút")).toBeTruthy();
  expect(screen.getByText("2 giờ")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Công việc 1" }));
  expect(within(screen.getByRole("dialog")).getByText("1 giờ 30 phút")).toBeTruthy();
});

it("shows checkable requirements separately from notes for the assignee", () => {
  const onChecklistToggle = vi.fn();
  const parent = makeTask(0, { status: "Split", subtaskCount: 2, subtasksCompleted: 0 });
  const child = makeTask(1, { parentTaskId: parent.id, assigneeId: "me", status: "Assigned",
    description: "Yêu cầu\n- Vẽ biểu đồ\n\nGhi chú\nDùng số liệu tuần này.",
    checklist: [{ id: "step", text: "Vẽ biểu đồ", completed: false }] });
  render(board([parent, child], { onChecklistToggle }));
  fireEvent.click(screen.getByRole("button", { name: "Cả nhóm", exact: true }));
  fireEvent.click(screen.getByRole("button", { name: "Công việc 0" }));
  fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Việc con" }));
  fireEvent.click(within(screen.getByRole("region", { name: "Việc con" })).getByRole("button", { name: "Công việc 1" }));
  fireEvent.click(screen.getByRole("button", { name: "Ghi chú & phối hợp" }));
  expect(screen.getByText("Dùng số liệu tuần này.")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Checklist" }));
  expect(screen.queryByText("Dùng số liệu tuần này.")).toBeNull();
  fireEvent.click(screen.getByRole("checkbox", { name: "Vẽ biểu đồ" }));
  expect(onChecklistToggle).toHaveBeenCalledWith(child, "step", true);
});

it("opens only the selected document category and keeps the source download reachable", () => {
  const task = makeTask(0, {
    description: "Ghi chú\nKiểm tra tên file.\n\nTham chiếu tài liệu\nĐọc mục 2.\n\nCăn cứ deadline\nHạn đã thống nhất.",
    sourceFiles: [{ id: "source", name: "yeu-cau.docx", size: 1200 }],
  });
  render(board([task]));
  fireEvent.click(screen.getByRole("button", { name: task.name }));
  const dialog = within(screen.getByRole("dialog", { name: task.name }));
  expect(dialog.getByRole("button", { name: "Tổng quan", pressed: true })).toBeTruthy();
  fireEvent.click(dialog.getByRole("button", { name: "Tham chiếu tài liệu" }));
  expect(dialog.getByText("Đọc mục 2.")).toBeTruthy();
  expect(dialog.queryByText("Hạn đã thống nhất.")).toBeNull();
  fireEvent.click(dialog.getByRole("button", { name: "Căn cứ deadline" }));
  expect(dialog.getByText("Hạn đã thống nhất.")).toBeTruthy();
  fireEvent.click(dialog.getByRole("button", { name: "Tài liệu nguồn" }));
  expect(dialog.getByText("yeu-cau.docx")).toBeTruthy();
  expect(dialog.getByRole("button", { name: "Tải yeu-cau.docx" })).toBeTruthy();
  expect(dialog.queryByText("Đọc mục 2.")).toBeNull();
});

it("shows assignment as the main leader action and puts splitting in the row menu", () => {
  const parent = makeTask(0);
  const child = makeTask(1, { parentTaskId: parent.id });
  const assigned = makeTask(2, { status: "Assigned", assigneeId: "other" });
  const onSplit = vi.fn();
  render(board([parent, child, assigned], { onSplit, onRecipientChange: vi.fn() }));
  const row = within(screen.getByRole("article", { name: parent.name }));
  expect(row.getByRole("button", { name: "Chỉ định người nhận" })).toBeTruthy();
  expect(row.queryByRole("button", { name: "Chia thành 2 việc con" })).toBeNull();
  const more = row.getByRole("button", { name: "Thêm thao tác" });
  fireEvent.click(more);
  const menu = screen.getByRole("dialog", { name: "Thao tác công việc" });
  fireEvent.keyDown(menu, { key: "Escape" });
  expect(screen.queryByRole("dialog", { name: "Thao tác công việc" })).toBeNull();
  expect(document.activeElement).toBe(more);
  fireEvent.click(more);
  const split = screen.getByRole("button", { name: "Chia thành 2 việc con" });
  fireEvent.click(split);
  expect(onSplit).toHaveBeenCalledWith(parent);
});

it("prepares two independently assignable children with requirements and notes", () => {
  const parent = makeTask(0, { estimatedMinutes: 90, designatedUserId: "other",
    checklist: [{ id: "a", text: "Thu số liệu" }, { id: "b", text: "Viết báo cáo" }] });
  const onSubmit = vi.fn();
  render(<TeamTaskSplitDialog task={parent} members={detail([]).members} onSubmit={onSubmit} />);
  const first = within(screen.getByRole("group", { name: "Việc con 1" }));
  const second = within(screen.getByRole("group", { name: "Việc con 2" }));
  fireEvent.change(first.getByLabelText("Tên công việc"), { target: { value: "Thu thập" } });
  fireEvent.change(second.getByLabelText("Tên công việc"), { target: { value: "Báo cáo" } });
  fireEvent.change(second.getByLabelText("Ghi chú"), { target: { value: "Xuất PDF" } });
  fireEvent.change(second.getByLabelText("Người nhận việc con 2"), { target: { value: "" } });
  fireEvent.click(screen.getByRole("button", { name: "Tạo 2 việc con" }));
  expect(onSubmit).toHaveBeenCalledWith([
    { name: "Thu thập", estimatedMinutes: 45, requirements: ["Thu số liệu"], notes: "", designatedUserId: "other" },
    { name: "Báo cáo", estimatedMinutes: 45, requirements: ["Viết báo cáo"], notes: "Xuất PDF", designatedUserId: null },
  ]);
});

it("keeps a large backlog on eight rows and archives completed work behind its status filter", () => {
  render(
    board([
      ...Array.from({ length: 19 }, (_, i) => makeTask(i)),
      makeTask(30, { status: "Completed", assigneeId: "me", progress: 100 }),
    ]),
  );
  fireEvent.click(screen.getByRole("button", { name: "Cả nhóm", exact: true }));
  expect(rows()).toHaveLength(8);
  expect(rows()[0].getAttribute("aria-label")).toBe("Công việc 0");
  expect(screen.getByText("1–8 / 19 công việc")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Trang công việc tiếp" }));
  expect(rows()).toHaveLength(8);
  expect(rows()[0].getAttribute("aria-label")).toBe("Công việc 8");
  filterStatus("Completed");
  expect(rows()).toHaveLength(1);
  expect(rows()[0].getAttribute("aria-label")).toBe("Công việc 30");
  expect(screen.getByText("1–1 / 1 công việc")).toBeTruthy();
  expect(screen.queryByRole("button", { name: "Trang công việc tiếp" })).toBeNull();
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
  fireEvent.click(screen.getByRole("button", { name: "Cả nhóm", exact: true }));
  fireEvent.click(screen.getByRole("button", { name: "Trang công việc tiếp" }));
  fireEvent.click(screen.getByRole("button", { name: "Việc của tôi" }));
  fireEvent.change(screen.getByRole("textbox"), {
    target: { value: "dang minh" },
  });
  expect(rows()).toHaveLength(1);
  expect(rows()[0].getAttribute("aria-label")).toBe("Đề xuất proposal");
  expect(screen.getByText("1–1 / 1 công việc")).toBeTruthy();
  filterStatus("Open");
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
  fireEvent.click(screen.getByRole("button", { name: "Cả nhóm", exact: true }));
  filterStatus("Completed");
  fireEvent.click(screen.getByRole("button", { name: "Có thể nhận" }));
  expect(rows()).toHaveLength(1);
  expect(rows()[0].getAttribute("aria-label")).toBe("Công việc 0");
});

it("keeps the leader's review queue accessible and allows choosing a recipient inline", () => {
  vi.spyOn(Date, "now").mockReturnValue(boardNow);
  const relative = (hours) => new Date(boardNow + hours * 3_600_000).toISOString();
  const review = makeTask(1, { status: "PendingReview", deadline: relative(100) });
  const overdue = makeTask(2, { status: "Assigned", assigneeId: "other", deadline: relative(-1) });
  const near = makeTask(3, { deadline: relative(24) });
  const later = makeTask(4, { deadline: relative(72) });
  const onReviewCompletion = vi.fn();
  const onRecipientChange = vi.fn();
  const onRequestAvailability = vi.fn();
  render(board([later, near, overdue, review], { onReviewCompletion, onRecipientChange, onRequestAvailability }));
  fireEvent.click(screen.getByRole("button", { name: "Cần xử lý" }));
  expect(screen.getByRole("button", { name: "Cần xử lý", pressed: true })).toBeTruthy();
  expect(rows().map((row) => row.getAttribute("aria-label"))).toEqual([review.name, overdue.name, near.name]);
  fireEvent.click(screen.getByRole("button", { name: "Kiểm tra & duyệt" }));
  expect(onReviewCompletion).toHaveBeenCalledWith(review);
  fireEvent.click(screen.getByRole("button", { name: "Chỉ định người nhận" }));
  fireEvent.change(screen.getByRole("combobox", { name: "Người nhận task" }), { target: { value: "other" } });
  fireEvent.click(screen.getByRole("button", { name: "Yêu cầu kiểm tra lịch" }));
  expect(onRequestAvailability).toHaveBeenCalledWith(near, "other");
  expect(onRecipientChange).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Lưu người nhận" }));
  expect(onRecipientChange).toHaveBeenCalledWith(near, "other");
});

it.each(["Member", "Leader"])("shows personal and claimable work together for %s", (role) => {
  const near = makeTask(1, { status: "Assigned", assigneeId: "me" });
  const revision = makeTask(4, { status: "Assigned", assigneeId: "me", completionApproved: false });
  const shared = makeTask(5, { availabilityCheck: { userId: "me", canTake: null } });
  const available = makeTask(6);
  const designated = makeTask(7, { designatedUserId: "me" });
  const reserved = makeTask(8, { designatedUserId: "other" });
  const teamDetail = detail([near, revision, shared, available, designated, reserved]);
  teamDetail.team.role = role;
  render(board([], { detail: teamDetail, onClaim: vi.fn() }));
  expect(screen.getByRole("heading", { name: /Bảng công việc/ })).toBeTruthy();
  const mine = within(screen.getByRole("region", { name: "Việc của tôi" }));
  const claimable = within(screen.getByRole("region", { name: "Có thể nhận" }));
  expect(mine.getAllByRole("article").map((row) => row.getAttribute("aria-label"))).toEqual([revision.name, near.name, shared.name, designated.name]);
  expect(mine.getByRole("button", { name: "Kiểm tra lịch & phản hồi" })).toBeTruthy();
  expect(claimable.getAllByRole("article").map((row) => row.getAttribute("aria-label"))).toEqual([available.name]);
  expect(screen.queryByRole("article", { name: reserved.name })).toBeNull();
});

it("sorts by Eisenhower importance and deadline instead of the legacy priority without changing the source array", () => {
  const tasks = [makeTask(7), makeTask(9, { priority: 1, importance: { level: "Important" }, deadline: new Date(boardNow + 18 * 3600000).toISOString() }), makeTask(0, { priority: 4, importance: { level: "Lower" } })];
  render(board(tasks));
  expect(rows()[0].getAttribute("aria-label")).toBe("Công việc 0");
  fireEvent.change(screen.getByRole("combobox", { name: "Sắp xếp công việc" }), {
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
  fireEvent.click(within(dialog).getByRole("button", { name: "Nội dung công việc" }));
  expect(within(dialog).getByText(task.description)).toBeTruthy();
  fireEvent.click(within(dialog).getByRole("button", { name: "Kết quả đã gửi" }));
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
  expect(within(dialog).getByText("Đã hoàn thành")).toBeTruthy();
  fireEvent.click(within(dialog).getByRole("button", { name: "Nội dung công việc" }));
  expect(within(dialog).getByText("Nội dung mới nhất")).toBeTruthy();
  expect(
    within(dialog).queryByRole("button", {
      name: "Báo hoàn thành · Gửi duyệt",
    }),
  ).toBeNull();
  rerender(board([]));
  expect(screen.queryByRole("dialog")).toBeNull();
});

it.each(["Leader", "Member"])("keeps overdue work out of the claim pool for %s", (role) => {
  const overdue = makeTask(0, { deadline: "2026-10-31T12:00:00Z" });
  const open = makeTask(1);
  const onExtendDeadline = vi.fn();
  const data = detail([overdue, open]);
  data.team.role = role;
  render(board([], { detail: data, onExtendDeadline, onClaim: vi.fn() }));
  const section = within(screen.getByRole("region", { name: "Quá hạn · Chưa có người nhận" }));
  expect(section.getByRole("article", { name: overdue.name })).toBeTruthy();
  expect(section.queryByRole("button", { name: "Xem lịch trước khi nhận" })).toBeNull();
  const available = within(screen.getByRole("region", { name: "Có thể nhận" }));
  expect(available.queryByRole("article", { name: overdue.name })).toBeNull();
  expect(available.getByRole("article", { name: open.name })).toBeTruthy();
  if (role === "Leader") {
    fireEvent.click(section.getByRole("button", { name: "Gia hạn" }));
    expect(onExtendDeadline).toHaveBeenCalledWith(overdue);
  } else {
    expect(section.queryByRole("button", { name: "Gia hạn" })).toBeNull();
    expect(section.getByText("Chờ trưởng nhóm gia hạn")).toBeTruthy();
  }
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
  fireEvent.click(screen.getByRole("button", { name: "Cần xử lý" }));
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
  fireEvent.click(within(screen.getByRole("region", { name: "Có thể nhận" })).getByRole("button", { name: /Xem tất cả/ }));
  fireEvent.click(screen.getByRole("button", { name: "Trang công việc tiếp" }));
  fireEvent.click(screen.getByRole("button", { name: "Trang công việc tiếp" }));
  expect(rows()).toHaveLength(1);
  rerender(board(tasks.slice(0, 8)));
  expect(screen.getByText("1–8 / 8 công việc")).toBeTruthy();
  expect(rows()).toHaveLength(8);
  rerender(board(tasks));
  expect(screen.getByText("Trang 1 / 3")).toBeTruthy();
});
