import { beforeEach, expect, it, vi } from "vitest";
import {
  fireEvent,
  render,
  screen,
  within,
  waitFor,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NotesPage } from "../src/notes.jsx";

import { state } from "../src/ui.jsx";

vi.mock("../src/calendar.jsx", () => ({ planModal: vi.fn() }));
import { planModal } from "../src/calendar.jsx";

let notes,
  calls,
  failPlan,
  failAi,
  failAvailability,
  failDelete,
  aiErrorCode,
  aiAvailable,
  aiSource;
const candidate = (name, minutes) => ({
  name,
  description: "",
  deadline: "2026-10-15T14:00:00Z",
  estimatedMinutes: minutes,
  priority: 2,
  taskType: "WRITING",
  minimumSessionMinutes: 25,
  splittable: true,
});
beforeEach(() => {
  localStorage.clear();
  notes = [];
  calls = [];
  failPlan = false;
  failAi = false;
  failAvailability = false;
  failDelete = false;
  aiErrorCode = undefined;
  aiAvailable = true;
  aiSource = "Gemini";
  state.workspace = {
    availability: [{ dayOfWeek: 1 }],
    exceptions: [],
    preference: { planningHorizonDays: 7 },
  };
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url, options = {}) => {
      const body = options.body ? JSON.parse(options.body) : undefined;
      calls.push({ url, method: options.method, body });
      if (failAi && (url.endsWith("/analyze") || url.endsWith("/schedule")))
        return {
          ok: false,
          status: 503,
          json: async () => ({
            error: {
              code: aiErrorCode,
              message: "AI chưa phân tích được ghi chú. Thử lại.",
            },
          }),
        };
      let data;
      if (url === "/api/workspace") data = state.workspace;
      if (url === "/api/time/availability" && options.method === "PUT") {
        if (failAvailability)
          return {
            ok: false,
            status: 503,
            json: async () => ({
              error: { message: "Chưa lưu được giờ rảnh. Thử lại." },
            }),
          };
        state.workspace = { ...state.workspace, availability: body.slots };
        data = {};
      }
      if (url === "/api/notes/agent")
        data = { aiAvailable, maxTasks: 12, provider: "Gemini" };
      if (url === "/api/notes" && options.method === "GET") data = notes;
      if (url.startsWith("/api/notes/") && options.method === "DELETE") {
        if (failDelete)
          return {
            ok: false,
            status: 409,
            json: async () => ({
              error: { message: "Ghi chú đã thay đổi. Tải lại trước khi xóa." },
            }),
          };
        const id = url.split("?")[0].split("/").at(-1);
        notes = notes.filter((item) => item.id !== id);
        data = {};
      }
      if (url === "/api/notes" && options.method === "POST") {
        data = {
          ...body,
          id: "note-1",
          version: "saved",
          updatedAt: "2026-10-08T10:00:00Z",
          createdTaskIds: [],
          analysis: null,
        };
        notes = [data, ...notes.filter((item) => item.id !== data.id)];
      }
      if (url.endsWith("/analyze")) {
        data = {
          ...notes[0],
          version: "analyzed",
          analysis: {
            source: aiSource,
            tasks: [candidate("Báo cáo", 120), candidate("Rà soát", 60)],
            messages: ["Kiểm tra deadline trước khi lưu."],
          },
        };
        notes = [data, ...notes.filter((item) => item.id !== data.id)];
      }
      if (url.endsWith("/tasks")) {
        data = { ...notes[0], version: "imported", createdTaskIds: ["task-1"] };
        notes = [data];
      }
      if (url.endsWith("/schedule")) {
        const note = {
          ...notes[0],
          version: "scheduled",
          createdTaskIds: ["task-1"],
          planId: "auto-plan",
          analysis: {
            source: aiSource,
            tasks: [candidate("Báo cáo", 120)],
            messages: [],
          },
        };
        notes = [note];
        data = {
          note,
          plan: {
            id: "auto-plan",
            isApplied: true,
            sessions: [{ preserved: false }],
            warnings: [],
          },
        };
      }
      if (url === "/api/plans/reschedule") {
        if (failPlan)
          return {
            ok: false,
            status: 503,
            json: async () => ({
              error: { message: "Không tạo được lịch. Thử lại." },
            }),
          };
        data = { id: "plan-1", sessions: [], warnings: [] };
      }
      return { ok: true, status: 200, json: async () => ({ data }) };
    }),
  );
});
async function ready() {
  await waitFor(() =>
    expect(
      screen
        .getByRole("button", { name: "Xem trước công việc" })
        .closest("fieldset").disabled,
    ).toBe(false),
  );
}
async function analyze(user) {
  await ready();
  await user.type(screen.getByLabelText("Tiêu đề ghi chú"), "Tuần này");
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Viết báo cáo 2 tiếng\nRà soát 60 phút",
  );
  await user.click(screen.getByRole("button", { name: "Xem trước công việc" }));
  await screen.findByRole("heading", { name: "Xem trước công việc" });
}
async function useQuickSchedule(user) {
  const options = document.querySelector(".note-quick-options");
  if (options && !options.open)
    await user.click(within(options).getByText("Tùy chọn tạo lịch nhanh"));
  await user.click(
    screen.getByRole("button", { name: "Tạo và lưu lịch ngay" }),
  );
}
it("cancels note deletion without changing the saved note or editor", async () => {
  const user = userEvent.setup();
  const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await analyze(user);
  await user.click(
    screen.getByRole("button", { name: "Xóa ghi chú", exact: true }),
  );
  expect(confirm).toHaveBeenCalledWith(expect.stringContaining("Tuần này"));
  expect(calls.some((call) => call.method === "DELETE")).toBe(false);
  expect(
    screen.getByRole("heading", { name: "Xem trước công việc" }),
  ).toBeTruthy();
  expect(notes).toHaveLength(1);
});
it("deletes the reviewed note with its latest version and clears the editor", async () => {
  const user = userEvent.setup();
  vi.spyOn(window, "confirm").mockReturnValue(true);
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await analyze(user);
  await user.click(
    screen.getByRole("button", { name: "Xóa ghi chú", exact: true }),
  );
  await screen.findByText("Đã xóa ghi chú “Tuần này”.");
  expect(calls.filter((call) => call.method === "DELETE")).toEqual([
    {
      url: "/api/notes/note-1?version=analyzed",
      method: "DELETE",
      body: undefined,
    },
  ]);
  expect(screen.getByLabelText("Nội dung ghi chú").value).toBe("");
  expect(
    screen.queryByRole("heading", { name: "Xem trước công việc" }),
  ).toBeNull();
  expect(notes).toHaveLength(0);
});
it("deletes a library note without losing the unsaved current note", async () => {
  notes = ["Đang viết", "Ghi chú cũ"].map((title, index) => ({
    id: `saved-${index}`,
    title,
    content: `Nội dung ${index}`,
    version: `version ${index}`,
    createdTaskIds: [],
    analysis: null,
    updatedAt: "2026-10-08T10:00:00Z",
  }));
  const user = userEvent.setup();
  vi.spyOn(window, "confirm").mockReturnValue(true);
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  await user.click(screen.getByRole("button", { name: /^Đang viết/ }));
  await user.type(screen.getByLabelText("Nội dung ghi chú"), " chưa lưu");
  await user.click(
    screen.getByRole("button", { name: "Xóa ghi chú Ghi chú cũ" }),
  );
  await screen.findByText("Đã xóa ghi chú “Ghi chú cũ”.");
  expect(screen.getByLabelText("Nội dung ghi chú").value).toBe(
    "Nội dung 0 chưa lưu",
  );
  expect(screen.queryByRole("button", { name: /^Ghi chú cũ/ })).toBeNull();
  expect(calls.find((call) => call.method === "DELETE").url).toBe(
    "/api/notes/saved-1?version=version%201",
  );
});
it("preserves saved and unsaved content when note deletion fails", async () => {
  const user = userEvent.setup();
  const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await analyze(user);
  await user.click(screen.getByRole("button", { name: "Sửa ghi chú" }));
  await user.type(screen.getByLabelText("Nội dung ghi chú"), " chưa lưu");
  failDelete = true;
  await user.click(
    screen.getByRole("button", { name: "Xóa ghi chú", exact: true }),
  );
  await screen.findByRole("alert");
  expect(screen.getByLabelText("Nội dung ghi chú").value).toContain("chưa lưu");
  expect(notes).toHaveLength(1);
  expect(confirm).toHaveBeenCalledWith(
    expect.stringContaining("Các thay đổi chưa lưu"),
  );
  failDelete = false;
  await user.click(
    screen.getByRole("button", { name: "Xóa ghi chú", exact: true }),
  );
  await screen.findByText("Đã xóa ghi chú “Tuần này”.");
  expect(notes).toHaveLength(0);
});
it.each(["Gemini", "VertexAI"])(
  "saves a note, reviews %s tasks, and previews the schedule without applying it",
  async (source) => {
    aiSource = source;
    const refresh = vi.fn(async () => {}),
      user = userEvent.setup();
    render(<NotesPage onWorkspaceChanged={refresh} />);
    expect(document.querySelector(".note-quick-options").open).toBe(false);
    expect(
      screen
        .getByRole("button", { name: "Xem trước công việc" })
        .classList.contains("primary"),
    ).toBe(true);
    await analyze(user);
    expect(
      calls.some((c) => c.url === "/api/notes" && c.method === "POST"),
    ).toBe(true);
    expect(calls.some((c) => c.url.endsWith("/tasks"))).toBe(false);
    const section = screen
      .getByRole("heading", { name: "Xem trước công việc" })
      .closest("section");
    await user.click(within(section).getByLabelText("Việc 2"));
    const name = within(section).getAllByLabelText("Tên công việc")[0];
    await user.clear(name);
    await user.type(name, "Báo cáo đã chỉnh");
    await user.click(
      screen.getByRole("button", { name: "Tạo công việc & xem lịch" }),
    );
    await waitFor(() => expect(planModal).toHaveBeenCalledOnce());
    const submitted = calls.find((c) => c.url.endsWith("/tasks"));
    expect(submitted.body.tasks).toHaveLength(1);
    expect(submitted.body.tasks[0].name).toBe("Báo cáo đã chỉnh");
    expect(refresh).toHaveBeenCalledOnce();
    expect(
      calls.find((c) => c.url === "/api/plans/reschedule").body.affectedTaskIds,
    ).toEqual(["task-1"]);
    expect(calls.some((c) => c.url.endsWith("/apply"))).toBe(false);
  },
);
it("keeps imported tasks after a planning failure and retries only the plan", async () => {
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn(async () => {})} />);
  await analyze(user);
  failPlan = true;
  await user.click(
    screen.getByRole("button", { name: "Tạo công việc & xem lịch" }),
  );
  await screen.findByRole("alert");
  failPlan = false;
  await user.click(screen.getByRole("button", { name: "Xem lịch đề xuất" }));
  await waitFor(() => expect(planModal).toHaveBeenCalledOnce());
  expect(calls.filter((c) => c.url.endsWith("/tasks"))).toHaveLength(1);
  expect(calls.filter((c) => c.url === "/api/plans/reschedule")).toHaveLength(
    2,
  );
});
it("offers availability setup and explains why edited notes need fresh analysis", async () => {
  const user = userEvent.setup();
  state.workspace.availability = [];
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await analyze(user);
  expect(
    screen.getByRole("button", { name: "Tạo công việc & xem lịch" }).disabled,
  ).toBe(false);
  expect(
    screen.getByRole("button", { name: "Chọn giờ rảnh để kiểm tra" }),
  ).toBeTruthy();
  await user.click(screen.getByRole("button", { name: "Sửa ghi chú" }));
  expect(
    screen.getByRole("link", { name: "giờ rảnh" }).getAttribute("href"),
  ).toBe("#time");
  await user.type(screen.getByLabelText("Nội dung ghi chú"), " mới");
  expect(screen.getByText(/Bạn đã sửa nội dung/)).toBeTruthy();
  await user.click(screen.getByRole("button", { name: "2 Kiểm tra" }));
  expect(
    screen.getByText(
      "Nội dung ghi chú đã thay đổi. Phân tích lại trước khi tạo công việc.",
    ),
  ).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Tạo công việc & xem lịch" }).disabled,
  ).toBe(true);
  expect(calls.some((c) => c.url.endsWith("/tasks"))).toBe(false);
});

it("automatically schedules a saved note and reports the applied calendar", async () => {
  const user = userEvent.setup(),
    refresh = vi.fn(async () => {});
  render(<NotesPage onWorkspaceChanged={refresh} />);
  await waitFor(() =>
    expect(
      screen
        .getByRole("button", { name: "Xem trước công việc" })
        .closest("fieldset").disabled,
    ).toBe(false),
  );
  await user.type(screen.getByLabelText("Tiêu đề ghi chú"), "Tự lên lịch");
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Báo cáo 120 phút",
  );
  await useQuickSchedule(user);
  await screen.findByText(/Đã tự động áp dụng lịch với 1 phiên mới/);
  expect(calls.find((c) => c.url.endsWith("/schedule")).body).toEqual({
    version: "saved",
    useAi: true,
    horizonDays: 7,
  });
  expect(planModal).toHaveBeenCalledWith(
    expect.objectContaining({ isApplied: true }),
  );
  expect(refresh).toHaveBeenCalledOnce();
  expect(screen.getByRole("region", { name: "Kết quả xếp lịch" })).toBeTruthy();
  expect(screen.queryByRole("button", { name: "Xem lịch đề xuất" })).toBeNull();
});

it("allows saving but disables AI actions when Gemini is not configured", async () => {
  aiAvailable = false;
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn(async () => {})} />);
  await screen.findByText(/AI chưa sẵn sàng. Bạn vẫn có thể lưu bản nháp/);
  expect(
    screen.getByRole("button", { name: "Xem trước công việc" }).disabled,
  ).toBe(true);
  await user.click(screen.getByText("Tùy chọn tạo lịch nhanh"));
  expect(
    screen.getByRole("button", { name: "Tạo và lưu lịch ngay" }).disabled,
  ).toBe(true);
  expect(screen.queryByRole("checkbox")).toBeNull();
  await user.type(screen.getByLabelText("Tiêu đề ghi chú"), "Chờ AI");
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Viết báo cáo 60 phút",
  );
  await user.click(screen.getByRole("button", { name: "Lưu bản nháp" }));
  await waitFor(() =>
    expect(
      calls.some((c) => c.url === "/api/notes" && c.method === "POST"),
    ).toBe(true),
  );
  expect(
    calls.some(
      (c) => c.url.endsWith("/analyze") || c.url.endsWith("/schedule"),
    ),
  ).toBe(false);
});

it("identifies Gemini capabilities and the actual analysis provider", async () => {
  aiSource = "Gemini";
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn(async () => {})} />);
  await ready();
  await analyze(user);
  expect(screen.queryByText("Gemini sẵn sàng")).toBeNull();
  expect(screen.getByText(/Phân tích bằng Gemini/)).toBeTruthy();
  expect(calls.find((call) => call.url.endsWith("/analyze")).body.useAi).toBe(
    true,
  );
});

it("preserves saved note on AI failure and retries without creating tasks from rules", async () => {
  const user = userEvent.setup(),
    refresh = vi.fn(async () => {});
  render(<NotesPage onWorkspaceChanged={refresh} />);
  await ready();
  await user.type(screen.getByLabelText("Tiêu đề ghi chú"), "AI retry");
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Viết báo cáo 60 phút",
  );
  failAi = true;
  await useQuickSchedule(user);
  await screen.findByRole("alert");
  expect(screen.getByLabelText("Nội dung ghi chú").value).toBe(
    "Viết báo cáo 60 phút",
  );
  expect(notes).toHaveLength(1);
  expect(notes[0].createdTaskIds).toEqual([]);
  expect(refresh).not.toHaveBeenCalled();
  expect(planModal).not.toHaveBeenCalled();
  expect(calls.some((c) => c.url.endsWith("/tasks"))).toBe(false);
  failAi = false;
  await useQuickSchedule(user);
  await screen.findByText(/Đã tự động áp dụng lịch với 1 phiên mới/);
  expect(
    calls.filter((c) => c.url === "/api/notes" && c.method === "POST"),
  ).toHaveLength(1);
  expect(
    calls
      .filter((c) => c.url.endsWith("/schedule"))
      .every((c) => c.body.useAi === true),
  ).toBe(true);
});

async function setHours(user) {
  await screen.findByRole("heading", { name: "Chọn giờ rảnh để tạo lịch" });
  await user.click(screen.getByLabelText("Thứ Hai"));
  await user.type(screen.getByLabelText("Giờ bắt đầu"), "19:00");
  await user.type(screen.getByLabelText("Giờ kết thúc"), "22:00");
}
it("saves hours inline and continues the proposal while preserving reviewed tasks", async () => {
  state.workspace.availability = [];
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn(async () => {})} />);
  await analyze(user);
  const section = screen
    .getByRole("heading", { name: "Xem trước công việc" })
    .closest("section");
  const name = within(section).getAllByLabelText("Tên công việc")[0];
  await user.clear(name);
  await user.type(name, "Báo cáo đã chỉnh");
  await user.click(within(section).getByLabelText("Việc 2"));
  await user.click(
    screen.getByRole("button", { name: "Tạo công việc & xem lịch" }),
  );
  await setHours(user);
  expect(calls.some((c) => c.url.endsWith("/tasks"))).toBe(false);
  // A window saved in another tab must not be overwritten.
  state.workspace.availability = [
    { dayOfWeek: 2, startMinute: 540, endMinute: 600 },
  ];
  await user.click(
    screen.getByRole("button", { name: "Lưu giờ rảnh & tiếp tục" }),
  );
  await waitFor(() => expect(planModal).toHaveBeenCalledOnce());
  expect(
    calls.find((c) => c.url === "/api/time/availability").body.slots,
  ).toEqual([
    { dayOfWeek: 2, startMinute: 540, endMinute: 600 },
    { dayOfWeek: 1, startMinute: 1140, endMinute: 1320 },
  ]);
  const tasks = calls.find((c) => c.url.endsWith("/tasks")).body.tasks;
  expect(tasks).toHaveLength(1);
  expect(tasks[0].name).toBe("Báo cáo đã chỉnh");
  expect(calls.filter((c) => c.url.endsWith("/analyze"))).toHaveLength(1);
  expect(calls.some((c) => c.url.endsWith("/apply"))).toBe(false);
});
it("does not create work when saving hours fails and allows retry", async () => {
  state.workspace.availability = [];
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn(async () => {})} />);
  await analyze(user);
  await user.click(
    screen.getByRole("button", { name: "Tạo công việc & xem lịch" }),
  );
  await setHours(user);
  failAvailability = true;
  await user.click(
    screen.getByRole("button", { name: "Lưu giờ rảnh & tiếp tục" }),
  );
  await screen.findByText("Chưa lưu được giờ rảnh. Thử lại.");
  expect(calls.some((c) => c.url.endsWith("/tasks"))).toBe(false);
  expect(screen.getByLabelText("Giờ bắt đầu").value).toBe("19:00");
  failAvailability = false;
  await user.click(
    screen.getByRole("button", { name: "Lưu giờ rảnh & tiếp tục" }),
  );
  await waitFor(() => expect(planModal).toHaveBeenCalledOnce());
  expect(calls.filter((c) => c.url.endsWith("/tasks"))).toHaveLength(1);
});
it("rejects empty days and backwards time without saving hours or tasks", async () => {
  state.workspace.availability = [];
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn(async () => {})} />);
  await analyze(user);
  await user.click(
    screen.getByRole("button", { name: "Tạo công việc & xem lịch" }),
  );
  await screen.findByRole("heading", { name: "Chọn giờ rảnh để tạo lịch" });
  await user.type(screen.getByLabelText("Giờ bắt đầu"), "19:00");
  await user.type(screen.getByLabelText("Giờ kết thúc"), "18:00");
  await user.click(
    screen.getByRole("button", { name: "Lưu giờ rảnh & tiếp tục" }),
  );
  await screen.findByText("Chọn ít nhất một ngày trong tuần.");
  await user.click(screen.getByLabelText("Thứ Hai"));
  await user.click(
    screen.getByRole("button", { name: "Lưu giờ rảnh & tiếp tục" }),
  );
  await screen.findByText("Giờ kết thúc phải sau giờ bắt đầu trong cùng ngày.");
  expect(
    calls.some(
      (c) => c.url === "/api/time/availability" || c.url.endsWith("/tasks"),
    ),
  ).toBe(false);
});
it("continues automatic scheduling after inline availability setup", async () => {
  state.workspace.availability = [];
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn(async () => {})} />);
  await ready();
  await user.type(screen.getByLabelText("Tiêu đề ghi chú"), "Auto");
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Viết báo cáo 60 phút",
  );
  await useQuickSchedule(user);
  await setHours(user);
  await user.click(
    screen.getByRole("button", { name: "Lưu giờ rảnh & tiếp tục" }),
  );
  await screen.findByText(/Đã tự động áp dụng lịch với 1 phiên mới/);
  expect(calls.find((c) => c.url.endsWith("/schedule")).body.useAi).toBe(true);
});
it("explains a missing task selection beside the proposal button", async () => {
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await analyze(user);
  await user.click(screen.getByLabelText("Việc 1"));
  await user.click(screen.getByLabelText("Việc 2"));
  expect(
    screen.getByText("Chọn ít nhất một công việc để tạo lịch."),
  ).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Tạo công việc & xem lịch" }).disabled,
  ).toBe(true);
});

it("starts with a blank note and no sample-content action", async () => {
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  expect(screen.getByLabelText("Nội dung ghi chú").value).toBe("");
  expect(calls.some((c) => c.method === "POST")).toBe(false);
  expect(screen.queryByRole("button", { name: "Dùng ví dụ" })).toBeNull();
});
it("keeps the note intact while changing writing size, paper and focus mode", async () => {
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  const content = screen.getByLabelText("Nội dung ghi chú");
  await user.type(content, "Báo cáo 90 phút");
  await user.click(screen.getByText("Aa"));
  await user.selectOptions(
    screen.getByRole("combobox", { name: "Cỡ chữ" }),
    "20",
  );
  expect(
    document
      .querySelector(".note-studio")
      .style.getPropertyValue("--note-writing-size"),
  ).toBe("20px");
  await user.click(screen.getByRole("button", { name: "Giấy kẻ dòng" }));
  expect(
    document.querySelector(".note-studio").classList.contains("is-ruled"),
  ).toBe(true);
  await user.click(screen.getByRole("button", { name: "Tập trung viết" }));
  expect(screen.queryByRole("region", { name: "Ghi chú của bạn" })).toBeNull();
  expect(content.value).toBe("Báo cáo 90 phút");
  await user.click(screen.getByRole("button", { name: "Thoát tập trung" }));
  expect(screen.getByRole("region", { name: "Ghi chú của bạn" })).toBeTruthy();
  expect(content.value).toBe("Báo cáo 90 phút");
  expect(calls.some((call) => call.method === "POST")).toBe(false);
});
it("saves a title from the first line when the user only writes note content", async () => {
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Viết báo cáo trước thứ Sáu\nÔn chương 4",
  );
  await user.click(screen.getByRole("button", { name: "Lưu bản nháp" }));
  await screen.findByText(/Đã lưu bản nháp/);
  expect(
    calls.find((call) => call.url === "/api/notes" && call.method === "POST")
      .body.title,
  ).toBe("Viết báo cáo trước thứ Sáu");
  expect(calls.some((call) => call.url.endsWith("/analyze"))).toBe(false);
});
it("preserves task edits and selections when returning to the note and back to review", async () => {
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await analyze(user);
  await user.click(screen.getByLabelText("Việc 2"));
  const name = screen.getAllByLabelText("Tên công việc")[0];
  await user.clear(name);
  await user.type(name, "Báo cáo đã sửa");
  await user.click(screen.getByRole("button", { name: "Sửa ghi chú" }));
  expect(screen.getByLabelText("Nội dung ghi chú").value).toContain(
    "Viết báo cáo 2 tiếng",
  );
  await user.click(screen.getByRole("button", { name: "2 Kiểm tra" }));
  expect(screen.getAllByLabelText("Tên công việc")[0].value).toBe(
    "Báo cáo đã sửa",
  );
  expect(screen.getByLabelText("Việc 2").checked).toBe(false);
  expect(calls.filter((call) => call.url.endsWith("/analyze"))).toHaveLength(1);
});
it("shows an actionable quota message and retries the saved note without duplicates", async () => {
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn(async () => {})} />);
  await ready();
  await user.type(screen.getByLabelText("Tiêu đề ghi chú"), "Retry");
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Viết báo cáo 60 phút",
  );
  aiErrorCode = "NOTE_AI_QUOTA_EXCEEDED";
  failAi = true;
  await useQuickSchedule(user);
  await screen.findByText(
    "AI đang đạt giới hạn sử dụng. Ghi chú được giữ nguyên; hãy chờ rồi bấm Thử lại.",
  );
  failAi = false;
  await user.click(
    screen.getByRole("button", { name: "Thử lại", exact: true }),
  );
  await screen.findByText(/Đã tự động áp dụng lịch với 1 phiên mới/);
  expect(
    calls.filter((c) => c.url === "/api/notes" && c.method === "POST"),
  ).toHaveLength(1);
});

it("filters real note states and searches Vietnamese titles without accents", async () => {
  notes = [
    {
      id: "draft",
      title: "Ý tưởng hôm nay",
      content: "Lên ý tưởng cho báo cáo",
      version: "draft-v",
      createdTaskIds: [],
      analysis: null,
      updatedAt: "2026-10-14T04:00:00Z",
    },
    {
      id: "reviewed",
      title: "Báo cáo đã kiểm tra",
      content: "Báo cáo 120 phút",
      version: "review-v",
      createdTaskIds: [],
      analysis: {
        source: "Gemini",
        tasks: [candidate("Báo cáo", 120)],
        messages: [],
      },
      updatedAt: "2026-10-14T03:00:00Z",
    },
    {
      id: "created",
      title: "Việc đã tạo",
      content: "Ôn chương 4",
      version: "created-v",
      createdTaskIds: ["task-1"],
      analysis: {
        source: "Gemini",
        tasks: [candidate("Ôn bài", 60)],
        messages: [],
      },
      updatedAt: "2026-10-13T04:00:00Z",
    },
  ];
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  const user = userEvent.setup();
  const library = within(
    screen.getByRole("region", { name: "Ghi chú của bạn" }),
  );
  await user.selectOptions(
    library.getByRole("combobox", { name: "Lọc ghi chú" }),
    "reviewed",
  );
  expect(
    library.queryByRole("button", { name: "Ý tưởng hôm nay", exact: true }),
  ).toBeNull();
  await user.type(
    library.getByRole("searchbox", { name: "Tìm ghi chú" }),
    "bao cao",
  );
  expect(
    library.getByRole("button", { name: "Báo cáo đã kiểm tra", exact: true }),
  ).toBeTruthy();
  await user.type(
    library.getByRole("searchbox", { name: "Tìm ghi chú" }),
    " xyz",
  );
  expect(library.getByText("Không tìm thấy ghi chú.")).toBeTruthy();
  await user.click(library.getByRole("button", { name: "Bỏ bộ lọc" }));
  expect(
    library.getByRole("button", { name: "Ý tưởng hôm nay", exact: true }),
  ).toBeTruthy();
  expect(library.getByRole("combobox", { name: "Lọc ghi chú" }).value).toBe(
    "all",
  );
});
it("asks before discarding unsaved note content when opening another note", async () => {
  notes = [
    {
      id: "other",
      title: "Ghi chú đã lưu",
      content: "Nội dung đã lưu",
      version: "other-v",
      createdTaskIds: [],
      analysis: null,
      updatedAt: "2026-10-14T04:00:00Z",
    },
  ];
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  const user = userEvent.setup();
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Ý tưởng chưa lưu",
  );
  await user.click(
    screen.getByRole("button", { name: "Ghi chú đã lưu", exact: true }),
  );
  expect(screen.getByText("Có thay đổi chưa lưu")).toBeTruthy();
  expect(screen.getByLabelText("Nội dung ghi chú").value).toBe(
    "Ý tưởng chưa lưu",
  );
  await user.click(screen.getByRole("button", { name: "Tiếp tục chỉnh sửa" }));
  expect(screen.queryByText("Có thay đổi chưa lưu")).toBeNull();
  await user.click(
    screen.getByRole("button", { name: "Ghi chú đã lưu", exact: true }),
  );
  await user.click(
    screen.getByRole("button", { name: "Bỏ thay đổi và chuyển" }),
  );
  expect(screen.getByLabelText("Nội dung ghi chú").value).toBe(
    "Nội dung đã lưu",
  );
  expect(
    calls.some((call) => call.method === "POST" || call.method === "PUT"),
  ).toBe(false);
});
it("keeps edited AI tasks when selecting the same note and confirms switching away from review", async () => {
  notes = [
    {
      id: "other",
      title: "Ghi chú khác",
      content: "Nội dung khác",
      version: "other-v",
      createdTaskIds: [],
      analysis: null,
      updatedAt: "2026-10-14T04:00:00Z",
    },
  ];
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await analyze(user);
  const name = screen.getAllByLabelText("Tên công việc")[0];
  await user.clear(name);
  await user.type(name, "Báo cáo đã sửa");
  await user.click(
    screen.getByRole("button", { name: "Tuần này", exact: true }),
  );
  expect(screen.getAllByLabelText("Tên công việc")[0].value).toBe(
    "Báo cáo đã sửa",
  );
  expect(screen.queryByText("Có thay đổi chưa lưu")).toBeNull();
  await user.click(
    screen.getByRole("button", { name: "Ghi chú khác", exact: true }),
  );
  expect(screen.getByText("Có thay đổi chưa lưu")).toBeTruthy();
  await user.click(screen.getByRole("button", { name: "Tiếp tục chỉnh sửa" }));
  expect(screen.getAllByLabelText("Tên công việc")[0].value).toBe(
    "Báo cáo đã sửa",
  );
  expect(calls.filter((call) => call.url.endsWith("/analyze"))).toHaveLength(1);
});
it("inserts a checklist at the editor selection from the top toolbar without sending the note", async () => {
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  const user = userEvent.setup();
  const body = screen.getByLabelText("Nội dung ghi chú");
  await user.type(body, "Viết báo cáo");
  body.setSelectionRange(4, 4);
  await user.click(
    screen.getByRole("button", { name: "Thêm dòng checklist", exact: true }),
  );
  expect(body.value).toBe("Viết\n☐  báo cáo");
  expect(
    screen
      .getByRole("button", { name: "Thêm dòng checklist" })
      .closest(".note-desk-header"),
  ).toBeTruthy();
  expect(calls.some((call) => call.method === "POST")).toBe(false);
});

it("remembers the appearance of a new note after saving, switching notes and reopening the page", async () => {
  notes = [
    {
      id: "other",
      title: "Ghi chú khác",
      content: "Nội dung khác",
      version: "other-v",
      createdTaskIds: [],
      analysis: null,
      updatedAt: "2026-10-14T04:00:00Z",
    },
  ];
  const user = userEvent.setup();
  const view = render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  await user.type(screen.getByLabelText("Nội dung ghi chú"), "Báo cáo 90 phút");
  await user.click(screen.getByText("Aa"));
  await user.selectOptions(
    screen.getByRole("combobox", { name: "Font chữ" }),
    "serif",
  );
  await user.selectOptions(
    screen.getByRole("combobox", { name: "Cỡ chữ" }),
    "24",
  );
  await user.click(screen.getByRole("radio", { name: "Màu chữ: Tím" }));
  await user.click(screen.getByRole("radio", { name: "Màu giấy: Kem" }));
  await user.click(screen.getByRole("button", { name: "Giấy kẻ dòng" }));
  await user.click(screen.getByRole("button", { name: "Lưu bản nháp" }));
  await screen.findByText(/Đã lưu bản nháp/);
  const post = calls.find((call) => call.method === "POST");
  expect(post.body.content).toBe("Báo cáo 90 phút");
  expect(Object.keys(post.body).sort()).toEqual(["content", "title"]);
  await user.click(
    screen.getByRole("button", { name: "Ghi chú khác", exact: true }),
  );
  expect(
    document
      .querySelector(".note-studio")
      .style.getPropertyValue("--note-paper"),
  ).toBe("#ffffff");
  await user.click(
    screen.getByRole("button", { name: "Báo cáo 90 phút", exact: true }),
  );
  const checkAppearance = () => {
    const studio = document.querySelector(".note-studio");
    expect(studio.style.getPropertyValue("--note-paper")).toBe("#fff8e8");
    expect(studio.style.getPropertyValue("--note-ink")).toBe("#7145a0");
    expect(studio.style.getPropertyValue("--note-writing-size")).toBe("24px");
    expect(studio.style.getPropertyValue("--note-writing-font")).toContain(
      "Georgia",
    );
    expect(studio.classList.contains("is-ruled")).toBe(true);
    expect(screen.getByLabelText("Nội dung ghi chú").value).toBe(
      "Báo cáo 90 phút",
    );
  };
  checkAppearance();
  view.unmount();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await ready();
  await user.click(
    screen.getByRole("button", { name: "Báo cáo 90 phút", exact: true }),
  );
  checkAppearance();
  expect(calls.filter((call) => call.method === "POST")).toHaveLength(1);
});

it("changes custom colors and resets appearance without invalidating AI analysis or task edits", async () => {
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await analyze(user);
  const name = screen.getAllByLabelText("Tên công việc")[0];
  await user.clear(name);
  await user.type(name, "Báo cáo đã sửa");
  await user.click(screen.getByRole("button", { name: "Sửa ghi chú" }));
  const content = screen.getByLabelText("Nội dung ghi chú").value;
  await user.click(screen.getByText("Aa"));
  fireEvent.change(screen.getByLabelText("Tùy chọn màu chữ"), {
    target: { value: "#123456" },
  });
  fireEvent.change(screen.getByLabelText("Tùy chọn màu giấy"), {
    target: { value: "#faf0d2" },
  });
  expect(
    document.querySelector(".note-studio").style.getPropertyValue("--note-ink"),
  ).toBe("#123456");
  expect(
    document
      .querySelector(".note-studio")
      .style.getPropertyValue("--note-paper"),
  ).toBe("#faf0d2");
  await user.click(screen.getByRole("button", { name: "Khôi phục mặc định" }));
  expect(
    document
      .querySelector(".note-studio")
      .style.getPropertyValue("--note-paper"),
  ).toBe("#ffffff");
  expect(screen.getByLabelText("Nội dung ghi chú").value).toBe(content);
  await user.click(screen.getByRole("button", { name: "2 Kiểm tra" }));
  expect(screen.getAllByLabelText("Tên công việc")[0].value).toBe(
    "Báo cáo đã sửa",
  );
  expect(
    screen.getByRole("button", { name: "Tạo công việc & xem lịch" }).disabled,
  ).toBe(false);
  expect(calls.filter((call) => call.url.endsWith("/analyze"))).toHaveLength(1);
  expect(calls.some((call) => call.method === "PUT")).toBe(false);
});
