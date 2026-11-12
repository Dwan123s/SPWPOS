import { beforeEach, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { App } from "../src/App.jsx";
import { timePage } from "../src/page-time.jsx";
import { scenarioPage } from "../src/page-scenario.jsx";
import { NotesPage } from "../src/notes.jsx";
import { openTask, captureModal, openDetail, taskForm, taskInput } from "../src/tasks.jsx";
import { eventModal, exceptionModal, sessionDetail } from "../src/calendar.jsx";
import { takeAgentDraft } from "../src/agent-handoff.js";
import { dayKey, state } from "../src/ui.jsx";
import { update } from "../src/store.js";
import { workspace, resetFixtures } from "./fixtures.js";

beforeEach(() => {
  resetFixtures();
  state.workspace = workspace();
  state.workHoursWeek = null;
  state.workspace.features = [
    ...(state.workspace.features || []),
    "secretary-v1",
  ];
  takeAgentDraft();
  update({
    screen: null,
    screenKey: "loading",
    modal: null,
    toasts: [],
    slots: {},
  });
  window.history.replaceState(null, "", "/");
});

it("keeps manual working hours available independently of NORA", () => {
  state.noraAvailable = false;
  render(timePage());
  expect(document.querySelector("form[data-form='availability']")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Lưu giờ làm việc", exact: true })).toBeTruthy();
  expect(screen.queryByRole("button", { name: "Gửi thời khóa biểu" })).toBeNull();
});

it("uses the selected project's deadline for a new child and allows a separate deadline", () => {
  const project = { id: "project", name: "Dự án", deadline: "2026-11-25T09:00:00Z", isHardDeadline: true };
  const next = { id: "second-project", name: "Dự án khác", deadline: "2026-11-26T10:00:00Z", isHardDeadline: true };
  state.workspace.tasks = [project, next];
  const { container } = render(taskForm({}, project.id));
  const input = () => taskInput(new FormData(container.querySelector("form")));
  expect(input()).toMatchObject({ parentId: project.id, deadline: project.deadline, inheritsProjectDeadline: true });

  fireEvent.click(screen.getByRole("button", { name: "Tùy chọn" }));
  fireEvent.change(screen.getByLabelText("Công việc cha"), { target: { value: next.id } });
  expect(input()).toMatchObject({ parentId: next.id, deadline: next.deadline, inheritsProjectDeadline: true });
  fireEvent.click(screen.getByRole("button", { name: "Một việc" }));
  fireEvent.click(screen.getByRole("checkbox", { name: "Dùng hạn chót của dự án" }));
  expect(input()).toMatchObject({ parentId: next.id, deadline: null, inheritsProjectDeadline: false });
});

it("shows separate gaps from saved timetable events in the availability page", async () => {
  state.noraAvailable = true;
  state.workspace.availabilityPolicy = null;
  state.workspace.events = [
    { id: "class", name: "Đi học", eventType: "CLASS", start: "2026-11-12T01:00:00Z", end: "2026-11-12T05:00:00Z" },
    { id: "shift", name: "Ca làm thêm", eventType: "WORK", start: "2026-11-12T06:00:00Z", end: "2026-11-12T12:00:00Z" },
  ];
  const days = [{
    date: "2026-11-12",
    workGaps: [
      { start: "2026-11-12T05:00:00Z", end: "2026-11-12T06:00:00Z" },
      { start: "2026-11-12T12:00:00Z", end: "2026-11-12T14:00:00Z" },
    ],
    focusSlots: [],
    schedulableMinutes: 180,
  }];
  const requests = vi.spyOn(globalThis, "fetch").mockImplementation(async (path) => ({
    ok: true,
    status: 200,
    json: async () => ({ data: path === "/api/time/policy/slots" ? days : {
      id: "routine",
      policy: { awakeStart: 420, awakeEnd: 1380, lunchStart: 720, lunchEnd: 750, dinnerStart: 1080, dinnerEnd: 1110, commuteMinutes: 30, focusMinutes: 45 },
      days, before: [], after: [], warnings: [],
    } }),
  }));
  const reload = vi.fn();
  render(timePage({ onWorkspaceChanged: reload }));
  fireEvent.click(screen.getByRole("button", { name: "Theo thời khóa biểu" }));
  const confirm = await screen.findByRole("button", { name: "Áp dụng giờ làm việc tuần này" });
  expect(screen.getByText(/Giờ rảnh: 12:00–13:00 · 19:00–21:00/)).toBeTruthy();
  expect(requests.mock.calls.some(([path]) => path === "/api/time/policy/routine/confirm")).toBe(false);
  fireEvent.click(confirm);
  await waitFor(() => expect(reload).toHaveBeenCalledOnce());
  expect(requests.mock.calls.some(([path, options]) =>
    path === "/api/time/policy/routine/confirm" && options.method === "POST",
  )).toBe(true);
  fireEvent.click(screen.getByRole("button", { name: "Gửi thời khóa biểu" }));
  expect(location.hash).toBe("#agent");
  expect(takeAgentDraft()).toBe("Nhập thời khóa biểu trong ảnh và cập nhật giờ làm việc.");
});

it("opens the working-hours table on the applied timetable week", async () => {
  const week = "2099-01-05";
  state.workspace.availability = [{ dayOfWeek: 1, startMinute: 540, endMinute: 1020 }];
  state.workspace.availabilityPolicy = { confirmed: true, timetableWeeks: [week] };
  const request = vi.spyOn(globalThis, "fetch").mockResolvedValue({
    ok: true,
    status: 200,
    json: async () => ({ data: { slots: [
      { dayOfWeek: 1, startMinute: 720, endMinute: 780 },
      { dayOfWeek: 1, startMinute: 1140, endMinute: 1260 },
    ] } }),
  });
  render(timePage());
  expect(screen.getByLabelText("Chọn ngày trong tuần").value).toBe(week);
  expect((await screen.findByLabelText("Bắt đầu Thứ hai, khung 1")).value).toBe("12:00");
  expect(screen.getByLabelText("Bắt đầu Thứ hai, khung 2").value).toBe("19:00");
  expect(request.mock.calls[0][0]).toBe(`/api/time/availability/weeks/${week}`);
  fireEvent.click(screen.getByRole("button", { name: "Giờ làm việc thường lệ" }));
  expect(screen.getByLabelText("Bắt đầu Thứ hai").value).toBe("09:00");
});

it("opens the specific timetable week selected in NORA", async () => {
  const week = "2099-01-12";
  state.workHoursWeek = week;
  state.workspace.availabilityPolicy = { confirmed: true, timetableWeeks: ["2099-01-05", week] };
  const request = vi.spyOn(globalThis, "fetch").mockResolvedValue({
    ok: true, status: 200,
    json: async () => ({ data: { slots: [], revision: "week-revision" } }),
  });
  render(timePage());
  expect(screen.getByLabelText("Chọn ngày trong tuần").value).toBe(week);
  await screen.findByRole("button", { name: "Lưu giờ làm việc tuần này" });
  expect(request.mock.calls[0][0]).toBe(`/api/time/availability/weeks/${week}`);
  expect(document.querySelector("input[name=weekRevision]").value).toBe("week-revision");
});

it("opens task and event forms without a NORA action in the create-task form", () => {
  state.noraAvailable = true;
  render(<App initialize={false} />);
  act(() => openTask());
  expect(screen.getByRole("button", { name: "Lưu công việc" })).toBeTruthy();
  expect(document.querySelector("input[name='deadline']").value).toBe("");
  expect(screen.queryByRole("button", { name: "Nhờ NORA" })).toBeNull();
  fireEvent.change(screen.getByLabelText("Tên công việc"), { target: { value: "Five-hour report" } });
  fireEvent.click(screen.getByRole("button", { name: "Lưu nháp" }));
  expect(screen.getByRole("textbox").value).toBe("Five-hour report");
  act(() => eventModal());
  expect(screen.getByRole("button", { name: "Lưu Lịch Trình" })).toBeTruthy();
  expect(location.hash).not.toBe("#agent");
});

it("opens the Inbox task form with the date passed by Workload", () => {
  const startDate = dayKey(Date.now() + 86400000);
  render(<App initialize={false} />);
  act(() => openTask({ startDate }));
  expect(screen.getByLabelText("Ngày bắt đầu").value).toBe(startDate);
  expect(screen.getByLabelText("Hạn chót (tùy chọn)").value).toBe("");
});

it("autofills an explicit start and duration without using the deadline field", () => {
  render(<App initialize={false} />);
  act(() => openTask());
  fireEvent.change(screen.getByLabelText("Tên công việc"), {
    target: { value: "Check mail lúc 2h chiều mai, làm trong 15 phút" },
  });
  expect(screen.getByLabelText("Ngày bắt đầu").value).toBe(dayKey(Date.now() + 86400000));
  expect(screen.getByLabelText("Thời gian dự kiến (phút, tùy chọn)").value).toBe("15");
  expect(screen.getByLabelText("Hạn chót (tùy chọn)").value).toBe("");
  fireEvent.change(screen.getByLabelText("Ngày bắt đầu"), { target: { value: "" } });
  expect(screen.getByLabelText("Ngày bắt đầu").value).toBe("");
});

it("retries when AI is busy, then fills time while preserving manual duration", async () => {
  state.noraAvailable = false;
  const start = new Date(Date.now() + 86400000).toISOString();
  let attempts = 0;
  const fetchPreview = vi.spyOn(globalThis, "fetch").mockImplementation(async (path) => {
    if (path !== "/api/tasks/preview-text") {
      return { ok: true, status: 200, json: async () => ({ data: [] }) };
    }
    if (++attempts === 1) {
      return {
        ok: false, status: 429, headers: new Headers(),
        json: async () => ({ error: { code: "AI_CONCURRENT_LIMIT", message: "Đang có yêu cầu AI xử lý." } }),
      };
    }
    return {
      ok: true, status: 200,
      json: async () => ({ data: { activities: [{ kind: "EVENT", name: "Họp nhóm", start, end: new Date(Date.parse(start) + 45 * 60000).toISOString() }] } }),
    };
  });
  render(<App initialize={false} />);
  act(() => openTask());
  fireEvent.change(screen.getByLabelText("Tên công việc"), {
    target: { value: "Họp nhóm vào giữa buổi chiều mai trong ba phần tư giờ" },
  });
  const duration = screen.getByLabelText("Thời gian dự kiến (phút, tùy chọn)");
  fireEvent.change(duration, { target: { value: "25" } });
  await waitFor(() => expect(screen.getByLabelText("Ngày bắt đầu").value).toBe(dayKey(start)), { timeout: 3500 });
  expect(duration.value).toBe("25");
  expect(fetchPreview.mock.calls.filter(([path]) => path === "/api/tasks/preview-text")).toHaveLength(2);
  expect(screen.getByLabelText("Hạn chót (tùy chọn)").value).toBe("");
});

it("fills midnight after tomorrow evening without asking a question", () => {
  render(<App initialize={false} />);
  act(() => openTask());
  fireEvent.change(screen.getByLabelText("Tên công việc"), {
    target: { value: "cuộc họp vào tối mai 12h" },
  });
  expect(screen.getByLabelText("Ngày bắt đầu").value).toBe(dayKey(Date.now() + 2 * 86400000));
  expect(screen.queryByText(/Hãy chọn giờ bắt đầu/)).toBeNull();
});

it("leaves a plain title quiet and flags a past start without rolling it forward", () => {
  render(<App initialize={false} />);
  act(() => openTask());
  const name = screen.getByLabelText("Tên công việc");
  fireEvent.change(name, { target: { value: "soạn báo cáo demo iot" } });
  expect(screen.getByLabelText("Ngày bắt đầu").value).toBe("");
  expect(screen.queryByText(/Ngày bắt đầu đã qua/)).toBeNull();
  fireEvent.change(name, { target: { value: "Kiểm tra mail lúc 2h chiều hôm qua" } });
  expect(screen.getByText("Ngày bắt đầu đã qua.")).toBeTruthy();
  expect(screen.getByLabelText("Ngày bắt đầu").value).toBe("");
});

it("lets users finish work without entering percentages or dependencies", () => {
  render(<App initialize={false} />);
  act(() => openDetail(state.workspace.tasks[0].id));
  expect(
    document.querySelector(
      "form[data-form='task-progress'], form[data-form='dependency']",
    ),
  ).toBeNull();
  expect(
    screen.getByRole("button", { name: "Đã hoàn thành công việc" }),
  ).toBeTruthy();
});

it("keeps the session result action available in secretary mode", () => {
  render(<App initialize={false} />);
  act(() => sessionDetail(state.workspace.sessions[0].id));
  expect(screen.getByRole("button", { name: "Ghi kết quả phiên" })).toBeTruthy();
});

it("replaces the scenario parameter form with an unsent conversation request", () => {
  render(scenarioPage());
  expect(document.querySelector("form, input, select, textarea")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Nhờ NORA đánh giá" }));
  expect(takeAgentDraft()).toContain("Chỉ tư vấn");
});

it("keeps the original Notes editor available in secretary mode", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn(
      async (url) =>
        new Response(
          JSON.stringify({
            data:
              url === "/api/notes"
                ? []
                : { aiAvailable: true, provider: "Gemini" },
          }),
          { status: 200 },
        ),
    ),
  );
  render(<NotesPage />);
  await screen.findByLabelText("Nội dung ghi chú");
  expect(screen.getByRole("button", { name: "Lưu bản nháp" })).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Tạo và lưu lịch ngay" }),
  ).toBeTruthy();
});
