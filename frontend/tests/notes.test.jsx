import { beforeEach, expect, it, vi } from "vitest";
import { render, screen, within, waitFor } from "@testing-library/react";
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
  notes = [];
  calls = [];
  failPlan = false;
  failAi = false;
  failAvailability = false;
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
      if (url === "/api/notes" && options.method === "POST") {
        data = {
          ...body,
          id: "note-1",
          version: "saved",
          updatedAt: "2026-10-08T10:00:00Z",
          createdTaskIds: [],
          analysis: null,
        };
        notes = [data];
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
        notes = [data];
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
async function analyze(user) {
  await waitFor(() =>
    expect(
      screen
        .getByRole("button", { name: "Xem trước công việc" })
        .closest("fieldset").disabled,
    ).toBe(false),
  );
  await user.type(screen.getByLabelText("Tiêu đề ghi chú"), "Tuần này");
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Viết báo cáo 2 tiếng\nRà soát 60 phút",
  );
  await user.click(screen.getByRole("button", { name: "Xem trước công việc" }));
  await screen.findByRole("heading", { name: "Xem trước công việc" });
}
it("saves a note, reviews selected tasks, and previews the schedule without applying it", async () => {
  const refresh = vi.fn(async () => {}),
    user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={refresh} />);
  await analyze(user);
  expect(calls.some((c) => c.url === "/api/notes" && c.method === "POST")).toBe(
    true,
  );
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
});
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
    screen.getAllByRole("link", { name: "giờ rảnh" })[0].getAttribute("href"),
  ).toBe("#time");
  await user.type(screen.getByLabelText("Nội dung ghi chú"), " mới");
  expect(screen.getByText(/Bạn đã sửa nội dung/)).toBeTruthy();
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
  await user.click(
    screen.getByRole("button", { name: "Tạo và lưu lịch ngay" }),
  );
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
  await waitFor(() =>
    expect(
      screen.getByRole("button", { name: "Xem trước công việc" }).disabled,
    ).toBe(false),
  );
  await analyze(user);
  expect(screen.getByText("Gemini sẵn sàng")).toBeTruthy();
  expect(screen.getByText(/Phân tích bằng Gemini/)).toBeTruthy();
  expect(calls.find((call) => call.url.endsWith("/analyze")).body.useAi).toBe(
    true,
  );
});

it("preserves saved note on AI failure and retries without creating tasks from rules", async () => {
  const user = userEvent.setup(),
    refresh = vi.fn(async () => {});
  render(<NotesPage onWorkspaceChanged={refresh} />);
  await screen.findByText("Gemini sẵn sàng");
  await user.type(screen.getByLabelText("Tiêu đề ghi chú"), "AI retry");
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Viết báo cáo 60 phút",
  );
  failAi = true;
  await user.click(
    screen.getByRole("button", { name: "Tạo và lưu lịch ngay" }),
  );
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
  await user.click(
    screen.getByRole("button", { name: "Tạo và lưu lịch ngay" }),
  );
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
  await screen.findByText("Gemini sẵn sàng");
  await user.type(screen.getByLabelText("Tiêu đề ghi chú"), "Auto");
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Viết báo cáo 60 phút",
  );
  await user.click(
    screen.getByRole("button", { name: "Tạo và lưu lịch ngay" }),
  );
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

it("fills an example without sending or saving it and keeps existing text intact", async () => {
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await screen.findByText("Gemini sẵn sàng");
  await user.click(screen.getByRole("button", { name: "Dùng ví dụ" }));
  expect(screen.getByLabelText("Nội dung ghi chú").value).toContain(
    "Viết báo cáo ngày mai",
  );
  expect(calls.some((c) => c.method === "POST")).toBe(false);
  expect(screen.queryByRole("button", { name: "Dùng ví dụ" })).toBeNull();
});
it("shows an actionable quota message and retries the saved note without duplicates", async () => {
  const user = userEvent.setup();
  render(<NotesPage onWorkspaceChanged={vi.fn(async () => {})} />);
  await screen.findByText("Gemini sẵn sàng");
  await user.type(screen.getByLabelText("Tiêu đề ghi chú"), "Retry");
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Viết báo cáo 60 phút",
  );
  aiErrorCode = "NOTE_AI_QUOTA_EXCEEDED";
  failAi = true;
  await user.click(
    screen.getByRole("button", { name: "Tạo và lưu lịch ngay" }),
  );
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
