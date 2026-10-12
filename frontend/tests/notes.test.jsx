import { beforeEach, expect, it, vi } from "vitest";
import { render, screen, within, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NotesPage } from "../src/notes.jsx";
import { state } from "../src/ui.jsx";

vi.mock("../src/calendar.jsx", () => ({ planModal: vi.fn() }));
import { planModal } from "../src/calendar.jsx";

let notes, calls, failPlan;
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
      let data;
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
            source: "Rules",
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
            source: "Rules",
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
        .getByRole("button", { name: "Agent phân tích" })
        .closest("fieldset").disabled,
    ).toBe(false),
  );
  await user.type(screen.getByLabelText("Tiêu đề ghi chú"), "Tuần này");
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Viết báo cáo 2 tiếng\nRà soát 60 phút",
  );
  await user.click(screen.getByRole("button", { name: "Agent phân tích" }));
  await screen.findByRole("heading", { name: "Công việc agent đề xuất" });
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
    .getByRole("heading", { name: "Công việc agent đề xuất" })
    .closest("section");
  await user.click(within(section).getByLabelText("Việc 2"));
  const name = within(section).getAllByLabelText("Tên công việc")[0];
  await user.clear(name);
  await user.type(name, "Báo cáo đã chỉnh");
  await user.click(
    screen.getByRole("button", { name: "Tạo công việc & đề xuất lịch" }),
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
    screen.getByRole("button", { name: "Tạo công việc & đề xuất lịch" }),
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
it("requires fresh analysis after editing and availability before creating tasks", async () => {
  const user = userEvent.setup();
  state.workspace.availability = [];
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await analyze(user);
  expect(
    screen.getByRole("button", { name: "Tạo công việc & đề xuất lịch" })
      .disabled,
  ).toBe(true);
  expect(
    screen.getAllByRole("link", { name: "giờ rảnh" })[0].getAttribute("href"),
  ).toBe("#time");
  await user.type(screen.getByLabelText("Nội dung ghi chú"), " mới");
  expect(screen.getByText(/Bạn đã sửa nội dung/)).toBeTruthy();
  expect(calls.some((c) => c.url.endsWith("/tasks"))).toBe(false);
});

it("automatically schedules a saved note and reports the applied calendar", async () => {
  const user = userEvent.setup(),
    refresh = vi.fn(async () => {});
  render(<NotesPage onWorkspaceChanged={refresh} />);
  await waitFor(() =>
    expect(
      screen
        .getByRole("button", { name: "Agent phân tích" })
        .closest("fieldset").disabled,
    ).toBe(false),
  );
  await user.type(screen.getByLabelText("Tiêu đề ghi chú"), "Tự lên lịch");
  await user.type(
    screen.getByLabelText("Nội dung ghi chú"),
    "Báo cáo 120 phút",
  );
  await user.click(
    screen.getByLabelText("Cho phép gửi nội dung ghi chú đến AI để phân tích"),
  );
  await user.click(screen.getByRole("button", { name: "Agent tự xếp lịch" }));
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
});
