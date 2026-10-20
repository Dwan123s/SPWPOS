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

const activities = [
  {
    kind: "EVENT",
    name: "Đi học",
    type: "CLASS",
    start: "2026-10-21T00:00:00Z",
    end: "2026-10-21T05:00:00Z",
  },
  {
    kind: "EVENT",
    name: "Đi làm",
    type: "WORK",
    start: "2026-10-21T06:00:00Z",
    end: "2026-10-21T10:00:00Z",
  },
  {
    kind: "EVENT",
    name: "Họp đồng nghiệp",
    type: "MEETING",
    start: "2026-10-21T10:00:00Z",
    end: "2026-10-21T10:15:00Z",
  },
  {
    kind: "TASK",
    name: "Báo cáo RDS",
    type: "WRITING",
    start: "2026-10-21T13:30:00Z",
    estimatedMinutes: null,
    question: "Bạn cần bao lâu để làm báo cáo RDS?",
  },
];
let calls, note, failApply;
beforeEach(() => {
  localStorage.clear();
  calls = [];
  failApply = false;
  state.workspace = {
    profile: { id: "account" },
    tasks: [],
    events: [],
    availability: [{ dayOfWeek: 3 }],
    preference: { timeZoneId: "Asia/Ho_Chi_Minh", planningHorizonDays: 14 },
  };
  note = {
    id: "note-1",
    title: "Thời gian biểu",
    content: "Thứ 4 đi học, đi làm, họp rồi làm báo cáo RDS từ 20h30",
    version: "v1",
    createdTaskIds: [],
    updatedAt: "2026-10-20T12:02:00Z",
    analysis: {
      source: "Gemini",
      tasks: [],
      activities,
      messages: [],
      needsReview: false,
    },
  };
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url, options = {}) => {
      const body = options.body ? JSON.parse(options.body) : undefined;
      calls.push({ url, body, method: options.method });
      let data;
      if (url === "/api/notes") data = [note];
      else if (url === "/api/notes/agent") data = { aiAvailable: true };
      else if (url.endsWith("/proposals"))
        data = {
          id: "p1",
          start: "2026-10-20T12:10:00Z",
          end: "2026-11-03T12:10:00Z",
          warnings: [],
          sessions: body.activities.some((x) => x.kind === "TASK")
            ? [
                {
                  id: "s1",
                  taskId: "t1",
                  start: activities[3].start,
                  end: "2026-10-21T14:30:00Z",
                  preserved: false,
                },
              ]
            : [],
          noteContext: {
            events: body.activities.filter((x) => x.kind === "EVENT"),
            taskIds: body.activities.some((x) => x.kind === "TASK")
              ? ["t1"]
              : [],
            activities: body.activities,
          },
        };
      else if (url.endsWith("/apply")) {
        if (failApply)
          return {
            ok: false,
            status: 409,
            json: async () => ({
              error: {
                code: "PLAN_STALE",
                message: "Lịch đã thay đổi. Xem trước lại.",
              },
            }),
          };
        data = {
          note: {
            ...note,
            version: "v2",
            planId: "p1",
            createdTaskIds: ["t1"],
          },
          plan: { id: "p1", isApplied: true },
        };
      } else throw new Error(`Unexpected request: ${url}`);
      return { ok: true, json: async () => ({ data }) };
    }),
  );
});
async function open() {
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await userEvent.click(
    await screen.findByRole("button", { name: "Thời gian biểu" }),
  );
}
it("replaces a stale AI question with a deadline error and clears it when the deadline is corrected", async () => {
  note.analysis = {
    ...note.analysis,
    activities: activities.map((activity) =>
      activity.kind === "TASK"
        ? {
            ...activity,
            estimatedMinutes: 30,
            deadline: "2026-10-21T03:45:00Z",
            question:
              "What is the estimated duration or end time for the report task?",
          }
        : activity,
    ),
  };
  await open();
  const deadline = screen.getByLabelText("Hạn chót Báo cáo RDS", {
    exact: true,
  });
  const duration = screen.getByRole("spinbutton", {
    name: "Thời lượng Báo cáo RDS (phút)",
  });
  const error = screen.getByText(
    "Hạn chót không được trước giờ kết thúc công việc.",
  );
  expect(deadline.getAttribute("aria-invalid")).toBe("true");
  expect(deadline.getAttribute("aria-describedby")).toBe(error.id);
  expect(deadline.parentElement.contains(error)).toBe(true);
  expect(duration.getAttribute("aria-invalid")).toBeNull();
  expect(
    screen.queryByText(
      "What is the estimated duration or end time for the report task?",
    ),
  ).toBeNull();
  const preview = screen.getByRole("button", { name: "Xem lịch đề xuất" });
  expect(preview.disabled).toBe(true);
  fireEvent.change(deadline, { target: { value: "2026-10-21T22:45" } });
  expect(deadline.getAttribute("aria-invalid")).toBeNull();
  expect(
    screen.queryByText("Hạn chót không được trước giờ kết thúc công việc."),
  ).toBeNull();
  expect(preview.disabled).toBe(false);
  expect(deadline.closest("details").open).toBe(true);
  fireEvent.change(duration, { target: { value: "180" } });
  expect(deadline.getAttribute("aria-invalid")).toBe("true");
  expect(preview.disabled).toBe(true);
  fireEvent.change(duration, { target: { value: "30" } });
  fireEvent.change(deadline, { target: { value: "2026-10-21T21:00" } });
  expect(deadline.getAttribute("aria-invalid")).toBeNull();
  expect(preview.disabled).toBe(false);
  fireEvent.change(deadline, { target: { value: "" } });
  expect(preview.disabled).toBe(false);
  expect(
    calls.some(
      (call) => call.url.endsWith("/proposals") || call.url.endsWith("/apply"),
    ),
  ).toBe(false);
});
it("keeps the task editor and focus open when the second duration digit completes validation", async () => {
  await open();
  const user = userEvent.setup();
  const duration = screen.getByRole("spinbutton", {
    name: "Thời lượng Báo cáo RDS (phút)",
  });
  const editor = duration.closest("details");
  expect(editor.open).toBe(true);
  await user.type(duration, "30");
  expect(duration.value).toBe("30");
  expect(editor.open).toBe(true);
  expect(document.activeElement).toBe(duration);
  const deadline = screen.getByLabelText("Hạn chót Báo cáo RDS", {
    exact: true,
  });
  await user.click(deadline);
  fireEvent.change(deadline, { target: { value: "2026-10-21T22:00" } });
  expect(deadline.value).toBe("2026-10-21T22:00");
  expect(editor.open).toBe(true);
  await user.click(
    within(editor).getByText("Chỉnh sửa", { selector: "summary" }),
  );
  await waitFor(() => expect(editor.open).toBe(false));
  await user.click(
    within(editor).getByText("Chỉnh sửa", { selector: "summary" }),
  );
  await waitFor(() => expect(editor.open).toBe(true));
  expect(deadline.value).toBe("2026-10-21T22:00");
});
it("keeps all four activities, requests the missing report duration, and saves only after confirming the preview", async () => {
  await open();
  const review = screen.getByRole("region", { name: "Hoạt động từ ghi chú" });
  for (const activity of activities)
    expect(
      within(review).getByText(activity.name, { selector: "strong" }),
    ).toBeTruthy();
  expect(within(review).getByText("20:30", { exact: false })).toBeTruthy();
  const preview = screen.getByRole("button", { name: "Xem lịch đề xuất" });
  expect(preview.disabled).toBe(true);
  expect(screen.getByText("Nhập thời lượng từ 5 đến 43200 phút.")).toBeTruthy();
  fireEvent.change(
    screen.getByRole("spinbutton", { name: "Thời lượng Báo cáo RDS (phút)" }),
    { target: { value: "60" } },
  );
  await userEvent.click(preview);
  expect(
    (await screen.findByRole("button", { name: "Lưu sự kiện & công việc" }))
      .disabled,
  ).toBe(false);
  expect(
    calls.find((x) => x.url.endsWith("/proposals")).body.activities,
  ).toHaveLength(4);
  expect(
    calls.some(
      (x) =>
        x.url.endsWith("/apply") ||
        x.url.endsWith("/tasks") ||
        x.url === "/api/time/events",
    ),
  ).toBe(false);
  await userEvent.click(
    screen.getByRole("button", { name: "Lưu sự kiện & công việc" }),
  );
  expect(await screen.findByText("Đã lưu 4 hoạt động vào lịch.")).toBeTruthy();
  expect(calls.filter((x) => x.url.endsWith("/apply"))).toHaveLength(1);
});
it("a failed confirmation preserves edited activities and allows a fresh preview", async () => {
  await open();
  failApply = true;
  fireEvent.change(
    screen.getByRole("spinbutton", { name: "Thời lượng Báo cáo RDS (phút)" }),
    { target: { value: "60" } },
  );
  await userEvent.click(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }),
  );
  await userEvent.click(
    await screen.findByRole("button", { name: "Lưu sự kiện & công việc" }),
  );
  expect(
    await screen.findByText("Lịch đã thay đổi. Xem trước lại."),
  ).toBeTruthy();
  await userEvent.click(
    screen.getByRole("button", { name: "Quay lại chỉnh sửa" }),
  );
  expect(
    screen.getByRole("spinbutton", { name: "Thời lượng Báo cáo RDS (phút)" })
      .value,
  ).toBe("60");
  await userEvent.click(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }),
  );
  await waitFor(() =>
    expect(calls.filter((x) => x.url.endsWith("/proposals"))).toHaveLength(2),
  );
});
it("events can be reviewed without availability or creating an incomplete task", async () => {
  state.workspace.availability = [];
  await open();
  await userEvent.click(
    screen.getByRole("checkbox", { name: "Chọn Báo cáo RDS" }),
  );
  const preview = screen.getByRole("button", { name: "Xem lịch đề xuất" });
  expect(preview.disabled).toBe(false);
  await userEvent.click(preview);
  const calendar = await screen.findByRole("region", {
    name: "Lịch thử từ ghi chú",
  });
  expect(within(calendar).getByText("Họp đồng nghiệp")).toBeTruthy();
  expect(within(calendar).getByText("17:15")).toBeTruthy();
  expect(
    screen.getByRole("combobox", { name: "Ngày xem lịch dự kiến" }).value,
  ).toBe("2026-10-21");
  expect(
    calls
      .find((x) => x.url.endsWith("/proposals"))
      .body.activities.map((x) => x.kind),
  ).toEqual(["EVENT", "EVENT", "EVENT"]);
  expect(calls.some((x) => x.url.endsWith("/apply"))).toBe(false);
});
