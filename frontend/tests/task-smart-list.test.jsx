import { beforeEach, expect, it, vi } from "vitest";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { TaskSmartList } from "../src/task-smart-list.jsx";
import { tasksPage } from "../src/page-tasks.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
import { takeInboxNote } from "../src/task-inbox-link.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";
let notes, failSave;
function openScopePicker() {
  const picker = screen.getByLabelText(/^Lọc danh sách/);
  if (!picker.closest("details").open) fireEvent.click(picker);
}
function chooseScope(name) {
  openScopePicker();
  fireEvent.click(screen.getByRole("button", { name, exact: true }));
}
const draft = {
  id: "draft",
  title: "Ý tưởng ứng dụng học nhóm",
  content: "Một ứng dụng giúp nhóm cùng học",
  version: "v1",
  analysisImported: false,
  createdTaskIds: [],
  updatedAt: "2026-11-04T01:00:00Z",
};
beforeEach(() => {
  vi.resetAllMocks();
  vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-11-04T01:00:00Z"));
  state.workspace = workspace();
  state.taskScope = "inbox";
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
  notes = [
    draft,
    {
      ...draft,
      id: "imported",
      title: "Đã lập kế hoạch",
      analysisImported: true,
    },
  ];
  failSave = false;
  window.history.replaceState({}, "", "/");
  takeInboxNote(state.workspace.profile.id);
  api.mockImplementation(async (path, method = "GET", body) => {
    if (path === "/api/notes" && method === "GET") return [...notes];
    if (method === "PATCH" && path === "/api/notes/draft/completion") {
      if (failSave) throw new Error("Không thể lưu trạng thái.");
      const note = {
        ...notes.find((item) => item.id === "draft"),
        inboxCompleted: body.completed,
        inboxCompletedAt: body.completed ? "2026-11-04T01:00:00Z" : null,
        version: body.version === "v1" ? "v2" : "v3",
      };
      notes = [note, ...notes.filter((item) => item.id !== note.id)];
      return note;
    }
    if (method === "POST" || method === "PUT") {
      if (failSave) throw new Error("Không thể lưu bản nháp.");
      const note = {
        ...draft,
        ...body,
        id: method === "POST" ? "new" : "draft",
        version: "v2",
      };
      notes = [note, ...notes.filter((item) => item.id !== note.id)];
      return note;
    }
    if (method === "DELETE") {
      notes = notes.filter((item) => item.id !== "draft");
      return {};
    }
    return {};
  });
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

it("saves ideas in Ghi nháp without deadlines, task creation or automatic AI calls", async () => {
  render(<TaskSmartList />);
  await screen.findByRole("button", { name: draft.title });
  expect(screen.queryByRole("button", { name: "Đã lập kế hoạch" })).toBeNull();
  expect(screen.queryByRole("button", { name: "Báo cáo hôm nay" })).toBeNull();
  fireEvent.change(
    screen.getByRole("textbox", { name: "Ý tưởng hoặc nhiệm vụ mới" }),
    { target: { value: "Thử một ý tưởng mới" } },
  );
  fireEvent.keyDown(
    screen.getByRole("textbox", { name: "Ý tưởng hoặc nhiệm vụ mới" }),
    { key: "Enter" },
  );
  expect(
    await screen.findByRole("button", { name: "Thử một ý tưởng mới" }),
  ).toBeTruthy();
  expect(api).toHaveBeenCalledWith("/api/notes", "POST", {
    title: "Thử một ý tưởng mới",
    content: "Thử một ý tưởng mới",
  });
  expect(
    api.mock.calls.some(
      ([path]) => path.includes("/api/tasks") || path.includes("/analyze"),
    ),
  ).toBe(false);
});
it("checks an Inbox item, persists across remounts and reopens it with the current version", async () => {
  const view = render(<TaskSmartList />);
  fireEvent.click(
    await screen.findByRole("checkbox", {
      name: `Đánh dấu hoàn thành: ${draft.title}`,
    }),
  );
  const completed = await screen.findByRole("checkbox", {
    name: `Mở lại: ${draft.title}`,
  });
  expect(completed.checked).toBe(true);
  expect(api).toHaveBeenCalledWith("/api/notes/draft/completion", "PATCH", {
    version: "v1",
    completed: true,
  });
  expect(
    within(
      screen.getByRole("navigation", { name: "Danh sách thông minh" }),
    ).getByRole("button", { name: "Ghi nháp" }).textContent.trim(),
  ).toBe("Ghi nháp");
  expect(
    screen.getByRole("list", { name: "Đã hoàn thành" }).contains(completed),
  ).toBe(true);
  view.unmount();
  render(<TaskSmartList />);
  const restored = await screen.findByRole("checkbox", {
    name: `Mở lại: ${draft.title}`,
  });
  expect(restored.checked).toBe(true);
  fireEvent.click(restored);
  expect(
    (
      await screen.findByRole("checkbox", {
        name: `Đánh dấu hoàn thành: ${draft.title}`,
      })
    ).checked,
  ).toBe(false);
  expect(api).toHaveBeenCalledWith("/api/notes/draft/completion", "PATCH", {
    version: "v2",
    completed: false,
  });
  expect(screen.queryByRole("list", { name: "Đã hoàn thành" })).toBeNull();
  expect(
    api.mock.calls.some(
      ([path]) => path.includes("/api/tasks") || path.includes("/analyze"),
    ),
  ).toBe(false);
});
it("keeps an Inbox item unchecked when saving completion fails", async () => {
  render(<TaskSmartList />);
  const checkbox = await screen.findByRole("checkbox", {
    name: `Đánh dấu hoàn thành: ${draft.title}`,
  });
  failSave = true;
  fireEvent.click(checkbox);
  expect(await screen.findByRole("alert")).toHaveProperty(
    "textContent",
    "Không thể lưu trạng thái.",
  );
  expect(checkbox.checked).toBe(false);
  expect(screen.queryByRole("list", { name: "Đã hoàn thành" })).toBeNull();
});
it("switches between Today and seven days while preserving filters and the unsaved Inbox draft", async () => {
  render(<TaskSmartList />);
  fireEvent.change(
    screen.getByRole("textbox", { name: "Ý tưởng hoặc nhiệm vụ mới" }),
    { target: { value: "Bản nháp đang gõ" } },
  );
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
  fireEvent.click(screen.getByRole("button", { name: "Ghi nháp" }));
  expect(
    screen.getByRole("textbox", { name: "Ý tưởng hoặc nhiệm vụ mới" }).value,
  ).toBe("Bản nháp đang gõ");
});
it("preserves the input on failure and sends the chosen draft to the detailed editor", async () => {
  failSave = true;
  render(<TaskSmartList />);
  await screen.findByRole("button", { name: draft.title });
  const input = screen.getByRole("textbox", {
    name: "Ý tưởng hoặc nhiệm vụ mới",
  });
  fireEvent.change(input, { target: { value: "Giữ ý tưởng này" } });
  fireEvent.click(screen.getByRole("button", { name: "Lưu vào Inbox" }));
  expect(await screen.findByRole("alert")).toHaveProperty(
    "textContent",
    "Không thể lưu bản nháp.",
  );
  expect(input.value).toBe("Giữ ý tưởng này");
  fireEvent.click(screen.getByRole("button", { name: draft.title }));
  expect(window.location.hash).toBe("#notes");
  expect(takeInboxNote(state.workspace.profile.id)).toBe("draft");
});
it("edits with the note version and requires confirmation before deleting", async () => {
  render(<TaskSmartList />);
  await screen.findByRole("button", { name: draft.title });
  fireEvent.click(screen.getByRole("button", { name: "Chỉnh sửa" }));
  fireEvent.change(
    screen.getByRole("textbox", { name: "Sửa nội dung Inbox" }),
    { target: { value: "Nội dung bổ sung" } },
  );
  fireEvent.click(screen.getByRole("button", { name: "Lưu chỉnh sửa" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/notes/draft", "PUT", {
      title: draft.title,
      content: "Nội dung bổ sung",
      version: "v1",
    }),
  );
  await screen.findByText("Nội dung bổ sung");
  fireEvent.click(
    screen.getByRole("button", { name: `Xóa khỏi Inbox: ${draft.title}` }),
  );
  expect(api.mock.calls.some(([, method]) => method === "DELETE")).toBe(false);
  fireEvent.click(
    within(screen.getByRole("alertdialog")).getByRole("button", {
      name: "Xóa vĩnh viễn",
    }),
  );
  await waitFor(() =>
    expect(screen.queryByRole("button", { name: draft.title })).toBeNull(),
  );
  expect(api).toHaveBeenCalledWith("/api/notes/draft?version=v2", "DELETE");
});
it("keeps Inbox composing intact when switching to Eisenhower and back", async () => {
  state.workspace.features = ["eisenhower-v1"];
  render(tasksPage());
  await screen.findByRole("button", { name: draft.title });
  fireEvent.change(
    screen.getByRole("textbox", { name: "Ý tưởng hoặc nhiệm vụ mới" }),
    { target: { value: "Chưa lưu" } },
  );
  fireEvent.click(
    screen.getByRole("button", { name: "Eisenhower", exact: true }),
  );
  fireEvent.click(
    screen.getByRole("button", { name: "Danh sách", exact: true }),
  );
  expect(
    screen.getByRole("textbox", { name: "Ý tưởng hoặc nhiệm vụ mới" }).value,
  ).toBe("Chưa lưu");
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
