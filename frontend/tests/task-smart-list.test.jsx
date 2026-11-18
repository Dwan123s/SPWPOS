import { beforeEach, expect, it, vi } from "vitest";
import {
  act,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import { TaskSmartList } from "../src/task-smart-list.jsx";
import { tasksPage } from "../src/page-tasks.jsx";
import { state } from "../src/ui.jsx";
import { getSnapshot } from "../src/store.js";
import { workspace } from "./fixtures.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";
function openScopePicker() {
  const picker = screen.getByLabelText(/^Lọc danh sách/);
  if (!picker.closest("details").open) fireEvent.click(picker);
}
function chooseScope(name) {
  openScopePicker();
  fireEvent.click(screen.getByRole("button", { name, exact: true }));
}
beforeEach(() => {
  vi.resetAllMocks();
  vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-11-04T01:00:00Z"));
  state.workspace = workspace();
  state.taskScope = "personal";
  state.workspace.features = [];
  state.workspace.tasks = [
    {
      ...state.workspace.tasks[0],
      id: "today",
      name: "Báo cáo hôm nay",
      deadline: "2026-11-04T18:00:00+07:00",
      status: "Todo",
    },
    {
      ...state.workspace.tasks[1],
      id: "week",
      name: "Chuẩn bị tuần này",
      deadline: "2026-11-10T23:59:00+07:00",
      status: "Todo",
    },
    {
      ...state.workspace.tasks[1],
      id: "later",
      name: "Việc tuần sau",
      deadline: "2026-11-11T00:00:00+07:00",
      status: "Todo",
    },
  ];
  state.workspace.sessions = [];
});
it("opens the personal Inbox when the former draft scope is selected", () => {
  state.taskScope = "inbox";
  render(<TaskSmartList />);
  expect(screen.getByRole("button", { name: "Inbox", pressed: true })).toBeTruthy();
  expect(screen.queryByRole("button", { name: "Ghi nháp" })).toBeNull();
  expect(screen.queryByPlaceholderText(/Ghi nhanh một ý tưởng/)).toBeNull();
  expect(api.mock.calls.some(([path]) => path === "/api/notes")).toBe(false);
});

it("warns when a task becomes overdue while the Today pane is open", async () => {
  const deadline = Date.parse("2026-11-04T01:00:05Z");
  state.workspace.tasks[0].deadline = new Date(deadline).toISOString();
  state.workspace.tasks[1].deadline = "2026-11-03T16:00:00+07:00";
  state.workspace.tasks.push({
    ...state.workspace.tasks[0], id: "done", name: "Việc đã xong", status: "Completed",
    completedAt: "2026-11-04T00:00:00Z",
  });
  vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
  try {
    await act(async () => { render(<TaskSmartList />); });
    const pane = within(screen.getByRole("complementary", { name: "Việc hôm nay" }));
    const dueToday = pane.getByRole("button", { name: "Xem Báo cáo hôm nay trong Hôm nay" });
    expect(dueToday.querySelector("time").dataset.overdue).toBe("false");
    expect(pane.getByRole("status").textContent).toBe("1 việc quá hạn");
    expect(pane.getByRole("progressbar", { name: "Đã hoàn thành 1/3 việc hôm nay" }).value).toBe(1);
    expect(pane.queryByRole("button", { name: /Việc đã xong/ })).toBeNull();

    Date.now.mockReturnValue(deadline);
    await act(async () => { await vi.advanceTimersByTimeAsync(5000); });
    expect(dueToday.querySelector("time").dataset.overdue).toBe("true");
    expect(pane.getByRole("status").textContent).toBe("2 việc quá hạn");
  } finally {
    vi.useRealTimers();
  }
});

it("shows the task table and a title-only add row by default", () => {
  state.taskScope = undefined;
  state.workspace.tasks.unshift({
    ...state.workspace.tasks[0],
    id: "checklist-item",
    name: "Kiểm tra email",
    deadline: null,
    estimatedMinutes: 0,
  });
  render(<TaskSmartList />);
  expect(screen.getByRole("button", { name: "Inbox" }).getAttribute("aria-pressed")).toBe("true");
  const table = screen.getByRole("table");
  expect(within(table).getByRole("columnheader", { name: "Hạn chót" })).toBeTruthy();
  const checkbox = within(table).getByRole("checkbox", { name: "Hoàn thành Kiểm tra email" });
  expect(checkbox.getAttribute("aria-checked")).toBe("false");
  expect(checkbox.dataset.action).toBe("toggle-task-completion");
  const add = screen.getByRole("button", { name: "Thêm công việc…" });
  expect(add.dataset.action).toBe("new-task");
  expect(add.getAttribute("aria-haspopup")).toBe("dialog");
  fireEvent.click(screen.getByText("Cột", { selector: "summary" }));
  fireEvent.click(screen.getByRole("checkbox", { name: "Hạn chót" }));
  expect(within(table).queryByRole("columnheader", { name: "Hạn chót" })).toBeNull();
});

it("shows undated work in the Today pane and adds a real due-date badge after a deadline is set", () => {
  const template = state.workspace.tasks[0];
  state.workspace.tasks.push({ ...template, id: "undated", name: "Kiểm tra email", deadline: null });
  const view = render(<TaskSmartList />);
  const pane = within(screen.getByRole("complementary", { name: "Việc hôm nay" }));
  const undated = pane.getByRole("button", { name: "Xem Kiểm tra email trong Hôm nay" });
  expect(undated.querySelector("time")).toBeNull();
  expect(undated.hasAttribute("aria-describedby")).toBe(false);
  expect(pane.getByRole("checkbox", { name: "Đánh dấu xong Kiểm tra email trong Hôm nay" }).dataset.id).toBe("undated");
  const dated = pane.getByRole("button", { name: "Xem Báo cáo hôm nay trong Hôm nay" });
  expect(dated.querySelector("time").dateTime).toBe(template.deadline);
  expect(dated.querySelector("time").dataset.overdue).toBe("false");
  expect(pane.getByRole("progressbar", { name: "Đã hoàn thành 0/2 việc hôm nay" })).toBeTruthy();
  state.workspace.tasks = state.workspace.tasks.map((task) => task.id === "undated" ? { ...task, deadline: "2026-11-04T19:00:00+07:00" } : task);
  view.rerender(<TaskSmartList />);
  expect(pane.getByRole("button", { name: "Xem Kiểm tra email trong Hôm nay" }).querySelector("time").dateTime).toBe("2026-11-04T19:00:00+07:00");
  state.workspace.tasks = state.workspace.tasks.map((task) => task.id === "undated" ? { ...task, status: "Completed", completedAt: "2026-11-04T01:00:00Z" } : task);
  view.rerender(<TaskSmartList />);
  expect(pane.queryByRole("button", { name: "Xem Kiểm tra email trong Hôm nay" })).toBeNull();
  expect(pane.getByRole("progressbar", { name: "Đã hoàn thành 1/2 việc hôm nay" })).toBeTruthy();
});

it("separates standalone Inbox tasks from Projects and keeps project children in task details", () => {
  state.taskScope = "personal";
  const template = state.workspace.tasks[0];
  state.workspace.tasks.push(
    { ...template, id: "project", name: "Thuyết trình SPWPOS", parentId: null, progress: 40 },
    { ...template, id: "project-child", name: "Chuẩn bị slide", parentId: "project" },
    { ...template, id: "deleted", name: "Project đã xóa", isDeleted: true },
  );
  render(<TaskSmartList />);
  const main = within(screen.getByRole("region", { name: "Nội dung Inbox" }));
  expect(main.getByRole("button", { name: "Báo cáo hôm nay" })).toBeTruthy();
  expect(main.queryByText("Thuyết trình SPWPOS")).toBeNull();
  expect(main.queryByText("Chuẩn bị slide")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Projects", exact: true }));
  const projects = within(screen.getByRole("table", { name: "Danh sách Projects" }));
  expect(projects.getByRole("progressbar", { name: "Tiến độ Thuyết trình SPWPOS" }).value).toBe(40);
  expect(projects.getByRole("button", { name: "Mở project Thuyết trình SPWPOS" })).toBeTruthy();
  expect(projects.queryByText("Báo cáo hôm nay")).toBeNull();
  expect(projects.queryByText("Chuẩn bị slide")).toBeNull();
  expect(projects.queryByText("Project đã xóa")).toBeNull();
  expect(screen.getByRole("button", { name: "Projects", exact: true }).getAttribute("aria-pressed")).toBe("true");
  fireEvent.click(screen.getByRole("button", { name: "Công việc", exact: true }));
  expect(within(screen.getByRole("table")).getByRole("button", { name: "Thuyết trình SPWPOS", exact: true })).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Inbox", exact: true }));
  expect(screen.getByRole("region", { name: "Nội dung Inbox" })).toBeTruthy();
  expect(within(screen.getByRole("table")).queryByText("Thuyết trình SPWPOS")).toBeNull();
});

it("filters Projects, preserves its columns separately and offers a project-specific add flow", () => {
  state.taskScope = "projects";
  const template = state.workspace.tasks[0];
  state.workspace.tasks.push(
    { ...template, id: "active-project", name: "SPWPOS", parentId: null },
    { ...template, id: "child-a", parentId: "active-project" },
    { ...template, id: "completed-project", name: "Project hoàn thành", parentId: null, status: "Completed" },
    { ...template, id: "child-b", parentId: "completed-project", status: "Completed" },
  );
  render(<TaskSmartList />);
  fireEvent.click(screen.getByRole("button", { name: "Project đang hoạt động" }));
  expect(screen.queryByRole("button", { name: "Mở project Project hoàn thành" })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Tìm kiếm" }));
  fireEvent.change(screen.getByRole("searchbox", { name: "Tìm project" }), { target: { value: "không tồn tại" } });
  expect(screen.getByText("Không có project phù hợp với bộ lọc.")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Bỏ bộ lọc" }));
  fireEvent.click(screen.getByText("Cột", { selector: "summary" }));
  fireEvent.click(screen.getByRole("checkbox", { name: "Tiến độ" }));
  expect(within(screen.getByRole("table")).queryByRole("columnheader", { name: "Tiến độ" })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Inbox", exact: true }));
  expect(within(screen.getByRole("table")).getByRole("columnheader", { name: "Hạn chót" })).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Projects", exact: true }));
  expect(within(screen.getByRole("table")).queryByRole("columnheader", { name: "Tiến độ" })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Thêm project…" }));
  // The shared app modal receives a batch form, not the single Inbox task form.
  expect(getSnapshot().modal.title).toBe("Thêm project");
});

it("opens a separate project workspace with task controls and returns to Projects", () => {
  state.taskScope = "projects";
  const template = state.workspace.tasks[0];
  state.workspace.tasks.push(
    { ...template, id: "project", name: "SPWPOS", parentId: null },
    { ...template, id: "child", name: "Chuẩn bị slide", parentId: "project", checklist: [{ id: "step", text: "Soạn nội dung", completed: false }] },
    { ...template, id: "deleted-child", name: "Việc con đã xóa", parentId: "project", isDeleted: true },
    { ...template, id: "other", name: "SSG", parentId: null },
    { ...template, id: "other-child", name: "Việc SSG", parentId: "other" },
  );
  render(<TaskSmartList />);
  fireEvent.click(screen.getByRole("button", { name: "Mở project SPWPOS" }));
  expect(location.hash).toBe("#tasks/project/project");
  expect(screen.queryByRole("table", { name: "Danh sách Projects" })).toBeNull();
  const region = within(screen.getByRole("region", { name: "Project SPWPOS" }));
  expect(region.getByRole("columnheader", { name: "Nguồn" })).toBeTruthy();
  expect(region.getByRole("columnheader", { name: "Hạn chót" })).toBeTruthy();
  expect(region.getByRole("checkbox", { name: "Hoàn thành Chuẩn bị slide" }).dataset.action).toBe("toggle-task-completion");
  const add = region.getByRole("button", { name: "Thêm công việc vào SPWPOS…" });
  expect(add.dataset.action).toBe("subtask");
  expect(add.dataset.id).toBe("project");
  expect(region.queryByText("Việc con đã xóa")).toBeNull();
  fireEvent.click(region.getByRole("button", { name: "Mở checklist của Chuẩn bị slide" }));
  expect(region.getByRole("group", { name: "Checklist của Chuẩn bị slide" })).toBeTruthy();
  fireEvent.click(region.getByRole("button", { name: "Chi tiết", exact: true }));
  expect(region.getByRole("region", { name: "Chi tiết project" })).toBeTruthy();
  fireEvent.click(region.getByRole("button", { name: "Tài liệu", exact: true }));
  expect(region.getByRole("button", { name: "Gắn file gốc" })).toBeTruthy();
  fireEvent.click(region.getByRole("button", { name: "Projects", exact: true }));
  expect(screen.queryByRole("region", { name: "Project SPWPOS" })).toBeNull();
  expect(screen.getByRole("table", { name: "Danh sách Projects" })).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Mở project SSG" }));
  expect(screen.getByRole("region", { name: "Project SSG" })).toBeTruthy();
});

it("restores the project page from a route and handles a missing project safely", () => {
  const template = state.workspace.tasks[0];
  state.workspace.tasks.push({ ...template, id: "project", name: "SWP", parentId: null },
    { ...template, id: "child", name: "Việc riêng SWP", parentId: "project" });
  const view = render(tasksPage({ projectId: "project" }));
  expect(screen.getByRole("region", { name: "Project SWP" })).toBeTruthy();
  expect(screen.queryByRole("complementary", { name: "Việc hôm nay" })).toBeNull();
  view.rerender(tasksPage({ projectId: "missing" }));
  expect(screen.getByText(/Project không còn trong danh sách/)).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Quay lại Projects" }));
  expect(screen.getByRole("table", { name: "Danh sách Projects" })).toBeTruthy();
});

it("switches between Today and seven days while preserving filters", async () => {
  render(<TaskSmartList />);
  fireEvent.click(screen.getByRole("button", { name: "Công việc" }));
  chooseScope("Hôm nay");
  expect(screen.getByRole("button", { name: "Báo cáo hôm nay" })).toBeTruthy();
  expect(
    screen.queryByRole("button", { name: "Chuẩn bị tuần này" }),
  ).toBeNull();
  chooseScope("7 ngày tới");
  expect(
    screen.getByRole("button", { name: "Chuẩn bị tuần này" }),
  ).toBeTruthy();
  expect(screen.queryByRole("button", { name: "Việc tuần sau" })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Tìm kiếm" }));
  fireEvent.change(screen.getByRole("searchbox", { name: "Tìm công việc" }), {
    target: { value: "Chuẩn bị" },
  });
  expect(screen.queryByRole("button", { name: "Báo cáo hôm nay" })).toBeNull();
  chooseScope("Hôm nay");
  expect(screen.getByText("Không có công việc phù hợp")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Bỏ bộ lọc" }));
});

it("keeps completed tasks searchable in All and clears that status when switching to Today", async () => {
  state.workspace.tasks.push({
    ...state.workspace.tasks[0],
    id: "done",
    name: "Việc đã hoàn thành",
    status: "Completed",
  });
  render(<TaskSmartList />);
  fireEvent.click(screen.getByRole("button", { name: "Công việc" }));
  const incomplete = screen.getByRole("button", { name: "Chưa hoàn thành" });
  fireEvent.click(incomplete);
  expect(screen.queryByRole("button", { name: "Việc đã hoàn thành" })).toBeNull();
  fireEvent.click(incomplete);
  expect(screen.getByRole("button", { name: "Việc đã hoàn thành" })).toBeTruthy();
  openScopePicker();
  fireEvent.change(screen.getByRole("combobox", { name: "Lọc trạng thái" }), {
    target: { value: "Completed" },
  });
  expect(
    screen.getByRole("button", { name: "Việc đã hoàn thành" }),
  ).toBeTruthy();
  expect(screen.queryByRole("button", { name: "Báo cáo hôm nay" })).toBeNull();
  chooseScope("Hôm nay");
  expect(
    screen.queryByRole("button", { name: "Việc đã hoàn thành" }),
  ).toBeNull();
  expect(screen.getByRole("button", { name: "Báo cáo hôm nay" })).toBeTruthy();
  openScopePicker();
  expect(screen.getByRole("combobox", { name: "Lọc trạng thái" }).value).toBe(
    "",
  );
});
