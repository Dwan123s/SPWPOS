import { beforeEach, expect, it, vi } from "vitest";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { NotesPage } from "../src/notes.jsx";
import { state } from "../src/ui.jsx";

vi.mock("../src/calendar.jsx", () => ({ planModal: vi.fn() }));
let stored, calls, revision, createGate, failSave;
const response = (data) => ({
  ok: true,
  status: 200,
  json: async () => ({ data }),
});
beforeEach(() => {
  localStorage.clear();
  stored = [];
  calls = [];
  revision = 0;
  createGate = null;
  failSave = false;
  state.user = { id: "autosave-account" };
  state.workspace = {
    availability: [{ dayOfWeek: 1 }],
    preference: {},
    tasks: [],
    events: [],
  };
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url, options = {}) => {
      const method = options.method || "GET";
      const body = options.body ? JSON.parse(options.body) : undefined;
      calls.push({ url, method, body });
      if (url === "/api/notes/agent")
        return response({ aiAvailable: true, provider: "Gemini" });
      if (url === "/api/notes" && method === "GET")
        return response(stored.map((x) => ({ ...x })));
      if (url === "/api/workspace") return response(state.workspace);
      if (url.endsWith("/completion") && method === "PATCH") {
        const taskId = url.split("/").at(-2);
        const task = state.workspace.tasks.find((item) => item.id === taskId);
        expect(body.version).toBe(task.version);
        state.workspace = {
          ...state.workspace,
          tasks: state.workspace.tasks.map((item) =>
            item.id === taskId
              ? {
                  ...item,
                  status: body.completed ? "Completed" : "Todo",
                  progress: body.completed ? 100 : 0,
                  version: "task-v2",
                }
              : item,
          ),
        };
        return response(state.workspace);
      }
      if (url.endsWith("/analyze")) {
        const note = stored.find((x) => x.id === url.split("/")[3]);
        expect(body.version).toBe(note.version);
        const next = {
          ...note,
          version: `v${++revision}`,
          analysisImported: false,
          analysis: {
            source: "Gemini",
            tasks: [],
            activities: [],
            messages: [],
          },
        };
        stored = [next];
        return response(next);
      }
      if (method === "POST" || method === "PUT") {
        if (createGate) await createGate.promise;
        if (failSave)
          return {
            ok: false,
            status: 409,
            json: async () => ({
              error: { code: "NOTE_STALE", message: "Ghi chú đã thay đổi." },
            }),
          };
        const old =
          method === "PUT"
            ? stored.find((x) => x.id === url.split("/").at(-1))
            : null;
        if (old) expect(body.version).toBe(old.version);
        const saved = {
          ...old,
          ...body,
          id: old?.id || "note-1",
          version: `v${++revision}`,
          createdTaskIds: old?.createdTaskIds || [],
          analysis: null,
          planId: null,
          analysisImported: false,
          updatedAt: "2026-10-24T14:00:00Z",
        };
        stored = [saved, ...stored.filter((x) => x.id !== saved.id)];
        return response(saved);
      }
      throw new Error(`Unexpected request ${method} ${url}`);
    }),
  );
});
const saves = () =>
  calls.filter(
    (x) => ["POST", "PUT"].includes(x.method) && !x.url.endsWith("/analyze"),
  );
async function ready() {
  await waitFor(() =>
    expect(
      screen.getByRole("button", { name: "Lưu bản nháp" }).closest("fieldset")
        .disabled,
    ).toBe(false),
  );
}
function write(content) {
  fireEvent.change(screen.getByLabelText("Nội dung ghi chú"), {
    target: { value: content },
  });
}
function deferred() {
  let resolve;
  const promise = new Promise((done) => {
    resolve = done;
  });
  return { promise, resolve };
}
it("autosaves a new draft and subsequent edits to the same note, surviving reload without calling AI", async () => {
  const view = render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  write("Báo cáo 60 phút");
  await waitFor(() => expect(stored[0]?.content).toBe("Báo cáo 60 phút"), {
    timeout: 3000,
  });
  write("Báo cáo 60 phút\nÔn bài 30 phút");
  await waitFor(() => expect(stored[0]?.content).toContain("Ôn bài"), {
    timeout: 3000,
  });
  expect(saves().map((x) => x.method)).toEqual(["POST", "PUT"]);
  expect(saves()[1].body.version).toBe("v1");
  expect(
    calls.some((x) => /analyze|schedule|proposals|\/tasks/.test(x.url)),
  ).toBe(false);
  view.unmount();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: /^Báo cáo 60 phút/ }));
  expect(screen.getByLabelText("Nội dung ghi chú").value).toContain(
    "Ôn bài 30 phút",
  );
});
it("keeps text entered during a slow create and serializes the next save with the returned version", async () => {
  createGate = deferred();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  write("Ý đầu tiên");
  await waitFor(() => expect(saves()).toHaveLength(1), { timeout: 3000 });
  write("Ý đầu tiên\nÝ mới viết trong khi lưu");
  await act(async () => createGate.resolve());
  expect(screen.getByLabelText("Nội dung ghi chú").value).toContain(
    "Ý mới viết",
  );
  await waitFor(() => expect(stored[0]?.content).toContain("Ý mới viết"), {
    timeout: 3000,
  });
  expect(saves().map((x) => x.method)).toEqual(["POST", "PUT"]);
  expect(saves()[1].body.version).toBe("v1");
});
it("flushes a pending autosave and latest text before analyzing", async () => {
  createGate = deferred();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  write("Ý đầu tiên");
  await waitFor(() => expect(saves()).toHaveLength(1), { timeout: 3000 });
  write("Ý đầu tiên\nThêm cuộc họp 30 phút");
  fireEvent.click(screen.getByRole("button", { name: "Xem trước công việc" }));
  expect(calls.some((x) => x.url.endsWith("/analyze"))).toBe(false);
  await act(async () => createGate.resolve());
  await waitFor(() =>
    expect(calls.some((x) => x.url.endsWith("/analyze"))).toBe(true),
  );
  expect(stored[0].content).toContain("Thêm cuộc họp");
  expect(calls.find((x) => x.url.endsWith("/analyze")).body.version).toBe("v2");
  expect(saves().filter((x) => x.method === "POST")).toHaveLength(1);
});
it("preserves the typed draft on a version conflict and supports an explicit save retry", async () => {
  failSave = true;
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  write("Không được mất ý này");
  await screen.findByText(/Bản trên server đã thay đổi/, {}, { timeout: 3000 });
  expect(screen.getByLabelText("Nội dung ghi chú").value).toBe(
    "Không được mất ý này",
  );
  expect(saves()).toHaveLength(1);
  failSave = false;
  fireEvent.click(screen.getByRole("button", { name: "Lưu bản nháp" }));
  await waitFor(() => expect(stored[0]?.content).toBe("Không được mất ý này"));
  expect(screen.queryByText(/Bản trên server đã thay đổi/)).toBeNull();
});
it("autosaves additions to a previously processed note and offers analysis of new work", async () => {
  stored = [
    {
      id: "note-1",
      title: "Kế hoạch cũ",
      content: "Báo cáo 60 phút",
      version: "v0",
      createdTaskIds: ["old-task"],
      analysisImported: true,
      hasProcessedContent: true,
      updatedAt: "2026-10-24T14:00:00Z",
      analysis: null,
    },
  ];
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: /^Kế hoạch cũ/ }));
  write("Báo cáo 60 phút\nThêm cuộc họp mới");
  await waitFor(() => expect(stored[0].content).toContain("cuộc họp mới"), {
    timeout: 3000,
  });
  expect(stored[0].createdTaskIds).toEqual(["old-task"]);
  expect(
    screen.getByRole("button", { name: "Nhận diện việc mới" }),
  ).toBeTruthy();
  expect(calls.some((x) => x.url.endsWith("/schedule"))).toBe(false);
});

function linkedNote() {
  stored = [
    {
      id: "note-1",
      title: "Checklist của tôi",
      content: "Ghi chú đang viết",
      version: "v0",
      createdTaskIds: ["task-1"],
      analysisImported: true,
      hasProcessedContent: true,
      updatedAt: "2026-10-25T04:00:00Z",
      analysis: null,
    },
  ];
  state.workspace = {
    ...state.workspace,
    tasks: [
      {
        id: "task-1",
        name: "Viết báo cáo",
        description: "Nội dung báo cáo",
        deadline: "2026-10-30T10:00:00Z",
        estimatedMinutes: 60,
        actualMinutes: 20,
        progress: 25,
        status: "InProgress",
        version: "task-v1",
      },
    ],
  };
}
it("ticks the linked task without losing unsaved text and autosaves that text with the unchanged note version", async () => {
  linkedNote();
  const changed = vi.fn();
  render(<NotesPage onWorkspaceChanged={changed} />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: /^Checklist của tôi/ }));
  write("Ghi chú đang viết\nThêm ý chưa kịp autosave");
  fireEvent.click(
    screen.getByRole("checkbox", { name: "Hoàn thành Viết báo cáo" }),
  );
  await screen.findByText("Đã hoàn thành · 1");
  expect(screen.getByLabelText("Nội dung ghi chú").value).toContain(
    "Thêm ý chưa kịp autosave",
  );
  const request = calls.find((item) => item.url.endsWith("/completion"));
  expect(request.url).toBe("/api/notes/note-1/tasks/task-1/completion");
  expect(request.body).toEqual({ version: "task-v1", completed: true });
  await waitFor(
    () => expect(stored[0].content).toContain("Thêm ý chưa kịp autosave"),
    { timeout: 3000 },
  );
  expect(saves()[0].body.version).toBe("v0");
  expect(changed).toHaveBeenCalledTimes(1);
});
it("synchronizes status changed elsewhere without resetting the note editor", async () => {
  linkedNote();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: /^Checklist của tôi/ }));
  write("Nội dung chưa lưu");
  state.workspace = {
    ...state.workspace,
    tasks: state.workspace.tasks.map((item) => ({
      ...item,
      status: "Completed",
      progress: 100,
      version: "external-version",
    })),
  };
  fireEvent(window, new Event("workspace-updated"));
  expect(screen.getByText("Đã hoàn thành · 1")).toBeTruthy();
  expect(screen.getByLabelText("Nội dung ghi chú").value).toBe(
    "Nội dung chưa lưu",
  );
  expect(calls.some((item) => item.url.endsWith("/completion"))).toBe(false);
});
it("refreshes linked task status when returning from another tab", async () => {
  linkedNote();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  fireEvent.click(screen.getByRole("button", { name: /^Checklist của tôi/ }));
  state.workspace = {
    ...state.workspace,
    tasks: state.workspace.tasks.map((item) => ({
      ...item,
      status: "Completed",
      progress: 100,
      version: "external-version",
    })),
  };
  fireEvent.focus(window);
  await screen.findByText("Đã hoàn thành · 1");
  expect(calls.some((item) => item.url === "/api/workspace")).toBe(true);
});
