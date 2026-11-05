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
let calls, note, failApply, failPreview, outsideHours, customWarnings;
beforeEach(() => {
  vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-10-20T12:00:00Z"));
  localStorage.clear();
  calls = [];
  failApply = false;
  failPreview = false;
  outsideHours = false;
  customWarnings = null;
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
      if (url.endsWith("/proposals") && failPreview)
        return {
          ok: false,
          status: 400,
          json: async () => ({
            error: {
              code: "EVENT_IN_PAST",
              message:
                "Giờ bắt đầu của “Đá bóng” đã qua. Chọn giờ sau thời điểm hiện tại.",
            },
          }),
        };
      let data;
      if (url === "/api/notes") data = [note];
      else if (url === "/api/notes/agent") data = { aiAvailable: true };
      else if (url.endsWith("/proposals"))
        data = {
          id: "p1",
          start: "2026-10-20T12:10:00Z",
          end: "2026-11-03T12:10:00Z",
          warnings:
            customWarnings ??
            (outsideHours && !body.allowRequestedTimeOutsideAvailability
              ? [
                  {
                    taskName: "Báo cáo RDS",
                    code: "NOTE_OUTSIDE_AVAILABILITY",
                    message:
                      "Khung giờ 21/10 20:30–21:00 nằm ngoài giờ rảnh đã thiết lập.",
                    unscheduledMinutes: 30,
                  },
                ]
              : []),
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
            allowRequestedTimeOutsideAvailability:
              body.allowRequestedTimeOutsideAvailability,
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
it("shows a past event start beside its field instead of asking for times that are already present", async () => {
  note.analysis.activities = [
    {
      ...activities[0],
      name: "Đá bóng",
      start: "2026-10-20T11:59:00Z",
      end: "2026-10-21T11:59:00Z",
    },
  ];
  await open();
  const start = screen.getByLabelText("Bắt đầu Đá bóng");
  expect(start.getAttribute("aria-invalid")).toBe("true");
  expect(
    screen.getByText("Giờ bắt đầu đã qua. Chọn giờ sau thời điểm hiện tại."),
  ).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }).disabled,
  ).toBe(true);
  expect(calls.some((x) => x.url.endsWith("/proposals"))).toBe(false);
  fireEvent.change(start, { target: { value: "2026-10-20T20:00" } });
  expect(start.getAttribute("aria-invalid")).toBeNull();
  expect(
    screen.queryByText("Giờ bắt đầu đã qua. Chọn giờ sau thời điểm hiện tại."),
  ).toBeNull();
  await userEvent.click(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }),
  );
  await screen.findByRole("button", { name: "Lưu sự kiện & công việc" });
  expect(
    calls.find((x) => x.url.endsWith("/proposals")).body.activities[0],
  ).toMatchObject({
    start: "2026-10-20T13:00:00.000Z",
    end: "2026-10-21T11:59:00Z",
  });
});
it("clears an outdated server preview error after correcting the event and retries with the edited times", async () => {
  note.analysis.activities = [{ ...activities[0], name: "Đá bóng" }];
  failPreview = true;
  await open();
  await userEvent.click(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }),
  );
  await screen.findByText(
    "Giờ bắt đầu của “Đá bóng” đã qua. Chọn giờ sau thời điểm hiện tại.",
  );
  await userEvent.click(screen.getByText("Chỉnh sửa"));
  fireEvent.change(screen.getByLabelText("Bắt đầu Đá bóng"), {
    target: { value: "2026-10-21T08:00" },
  });
  expect(
    screen.queryByText(
      "Giờ bắt đầu của “Đá bóng” đã qua. Chọn giờ sau thời điểm hiện tại.",
    ),
  ).toBeNull();
  failPreview = false;
  await userEvent.click(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }),
  );
  await screen.findByRole("button", { name: "Lưu sự kiện & công việc" });
  expect(calls.filter((x) => x.url.endsWith("/proposals"))).toHaveLength(2);
  expect(
    calls.filter((x) => x.url.endsWith("/proposals")).at(-1).body.activities[0]
      .start,
  ).toBe("2026-10-21T01:00:00.000Z");
  expect(calls.some((x) => x.url.endsWith("/apply"))).toBe(false);
});
it("rechecks a pending event when returning to the tab after its start has passed", async () => {
  note.analysis.activities = [
    {
      ...activities[0],
      start: "2026-10-20T12:01:00Z",
      end: "2026-10-20T13:00:00Z",
    },
  ];
  await open();
  expect(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }).disabled,
  ).toBe(false);
  vi.mocked(Date.now).mockReturnValue(Date.parse("2026-10-20T12:02:00Z"));
  fireEvent.focus(window);
  expect(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }).disabled,
  ).toBe(true);
  expect(
    screen.getByLabelText("Bắt đầu Đi học").getAttribute("aria-invalid"),
  ).toBe("true");
  expect(calls.some((x) => x.url.endsWith("/proposals"))).toBe(false);
});
it("explains the actual deadline and late finish, disables saving, and opens the affected task when returning to edit", async () => {
  note.analysis.activities = [
    {
      ...activities[3],
      start: null,
      estimatedMinutes: 60,
      deadline: "2026-10-21T14:00:00Z",
    },
  ];
  customWarnings = [
    {
      taskId: "t1",
      taskName: "Báo cáo RDS",
      code: "SOFT_DEADLINE_EXCEEDED",
      unscheduledMinutes: 0,
      message: "Đề xuất có phiên sau deadline mềm; kiểm tra trước khi áp dụng.",
    },
  ];
  await open();
  expect(
    screen.getByText(/Các ô tick này dùng để chọn mục muốn thêm/),
  ).toBeTruthy();
  await userEvent.click(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }),
  );
  const save = await screen.findByRole("button", {
    name: "Lưu sự kiện & công việc",
  });
  expect(save.disabled).toBe(true);
  const warning = screen.getByRole("alert");
  expect(warning.textContent).toContain(
    "Chưa thể lưu các việc mới và lịch đề xuất",
  );
  expect(warning.textContent).toContain("Hạn chót:");
  expect(warning.textContent).toContain("21:00");
  expect(warning.textContent).toContain("21:30");
  expect(warning.textContent).toContain("trễ 30p");
  expect(warning.textContent).not.toContain("deadline mềm");
  expect(
    document.getElementById(save.getAttribute("aria-describedby")).textContent,
  ).toContain("Nút lưu đang tắt");
  expect(calls.some((call) => call.url.endsWith("/apply"))).toBe(false);
  await userEvent.click(
    screen.getByRole("button", { name: "Quay lại chỉnh sửa" }),
  );
  expect(
    screen.getByLabelText("Hạn chót Báo cáo RDS").closest("details").open,
  ).toBe(true);
});
it("replaces unsupported categories in older analysis with Other and sends the value actually shown in review", async () => {
  note.analysis.activities = activities.map((activity) =>
    activity.kind === "TASK"
      ? { ...activity, type: "DESIGN", estimatedMinutes: 30 }
      : activity,
  );
  await open();
  expect(screen.queryByRole("option", { name: "Thiết kế" })).toBeNull();
  expect(screen.getByRole("combobox", { name: "Loại Báo cáo RDS" }).value).toBe(
    "OTHER",
  );
  await userEvent.click(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }),
  );
  await screen.findByRole("button", { name: "Lưu sự kiện & công việc" });
  const request = calls.find((call) => call.url.endsWith("/proposals"));
  expect(
    request.body.activities.find((activity) => activity.kind === "TASK").type,
  ).toBe("OTHER");
  expect(request.body.activities[0].type).toBe("CLASS");
});
it("offers one-off requested hours after an availability warning without applying until confirmed", async () => {
  outsideHours = true;
  note.analysis.activities = activities.map((x) =>
    x.kind === "TASK" ? { ...x, estimatedMinutes: 30 } : x,
  );
  await open();
  await userEvent.click(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }),
  );
  expect(
    (await screen.findByRole("button", { name: "Lưu sự kiện & công việc" }))
      .disabled,
  ).toBe(true);
  await userEvent.click(
    screen.getByRole("button", { name: "Dùng giờ đã chọn cho lần này" }),
  );
  await userEvent.click(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }),
  );
  expect(
    (await screen.findByRole("button", { name: "Lưu sự kiện & công việc" }))
      .disabled,
  ).toBe(false);
  expect(
    calls.filter((x) => x.url.endsWith("/proposals")).at(-1).body
      .allowRequestedTimeOutsideAvailability,
  ).toBe(true);
  expect(
    screen.getByText(
      "Giờ rảnh bổ sung chỉ được lưu cho ngày đã chọn khi bạn xác nhận lịch.",
    ),
  ).toBeTruthy();
  expect(calls.some((x) => x.url.endsWith("/apply"))).toBe(false);
  await userEvent.click(
    screen.getByRole("button", { name: "Lưu sự kiện & công việc" }),
  );
  await screen.findByText("Đã lưu 4 hoạt động vào lịch.");
});
it("distinguishes original analysis remarks from current field errors after editing", async () => {
  const original =
    "The task 'Báo cáo RDS' has a start time but no estimated duration or end time.";
  const alternate =
    "The task 'Báo cáo RDS' starts at 20h30 but its duration or completion deadline is missing.";
  note.analysis.messages = [original, alternate, "Kiểm tra giờ rảnh buổi tối."];
  await open();
  expect(screen.getByText("Nhận xét từ lần phân tích ban đầu")).toBeTruthy();
  const duration = screen.getByRole("spinbutton", {
    name: "Thời lượng Báo cáo RDS (phút)",
  });
  fireEvent.change(duration, { target: { value: "30" } });
  expect(duration.getAttribute("aria-invalid")).toBeNull();
  expect(screen.queryByText("Bổ sung thời lượng công việc.")).toBeNull();
  expect(
    screen.getByText(
      "Các nhận xét này dựa trên ghi chú ban đầu, chưa phản ánh chỉnh sửa của bạn. Thông tin còn thiếu hoặc chưa hợp lệ được hiển thị ngay dưới từng ô nhập.",
    ),
  ).toBeTruthy();
  expect(screen.queryByText(original)).toBeNull();
  expect(screen.queryByText(alternate)).toBeNull();
  expect(screen.getByText("Kiểm tra giờ rảnh buổi tối.")).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }).disabled,
  ).toBe(false);
  fireEvent.change(duration, { target: { value: "43201" } });
  expect(screen.getByText("Thời lượng quá dài (tối đa 720 giờ).")).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }).disabled,
  ).toBe(true);
  fireEvent.change(duration, { target: { value: "2" } });
  expect(screen.getByText("Thời lượng cần ít nhất 5 phút.")).toBeTruthy();
});
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
  expect(screen.getByText("Bổ sung thời lượng công việc.")).toBeTruthy();
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
it("uses a confirmed daily routine instead of requesting manual availability again", async () => {
  state.workspace.availability = [];
  state.workspace.availabilityPolicy = {
    confirmed: true,
    hasDailyRoutine: true,
  };
  note.analysis.activities = [
    {
      ...activities[3],
      start: null,
      estimatedMinutes: 30,
      deadline: "2026-10-21T15:00:00Z",
    },
  ];
  await open();
  expect(
    screen.queryByRole("button", { name: "Thiết lập giờ rảnh" }),
  ).toBeNull();
  const preview = screen.getByRole("button", { name: "Xem lịch đề xuất" });
  expect(preview.disabled).toBe(false);
  await userEvent.click(preview);
  await screen.findByRole("button", { name: "Lưu sự kiện & công việc" });
  expect(calls.some((x) => x.url === "/api/time/availability")).toBe(false);
});
it("allows a timed task to be checked without configured hours and asks before saving one-off hours", async () => {
  state.workspace.availability = [];
  outsideHours = true;
  note.analysis.activities = [{ ...activities[3], estimatedMinutes: 30 }];
  await open();
  expect(
    screen.queryByRole("button", { name: "Thiết lập giờ rảnh" }),
  ).toBeNull();
  await userEvent.click(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }),
  );
  expect(
    (await screen.findByRole("button", { name: "Lưu sự kiện & công việc" }))
      .disabled,
  ).toBe(true);
  expect(
    calls.find((x) => x.url.endsWith("/proposals")).body
      .allowRequestedTimeOutsideAvailability,
  ).toBe(false);
  await userEvent.click(
    screen.getByRole("button", { name: "Dùng giờ đã chọn cho lần này" }),
  );
  await userEvent.click(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }),
  );
  expect(
    (await screen.findByRole("button", { name: "Lưu sự kiện & công việc" }))
      .disabled,
  ).toBe(false);
  expect(
    calls.filter((x) => x.url.endsWith("/proposals")).at(-1).body
      .allowRequestedTimeOutsideAvailability,
  ).toBe(true);
  expect(
    calls.some(
      (x) => x.url.endsWith("/apply") || x.url === "/api/time/availability",
    ),
  ).toBe(false);
});
it("shows every blocker for the SSG105 note and can keep only the valid selected activities", async () => {
  vi.mocked(Date.now).mockReturnValue(Date.parse("2026-11-04T00:00:00Z"));
  state.workspace.availability = [];
  note.title = "Project SSG105";
  note.analysis.needsReview = true;
  note.analysis.activities = [
    {
      kind: "TASK",
      name: "Chuẩn bị tiền quỹ",
      type: "OTHER",
      deadline: "2026-11-02T16:59:00Z",
      estimatedMinutes: null,
    },
    {
      kind: "TASK",
      name: "Chế biến đồ ăn",
      type: "OTHER",
      start: "2026-11-03T09:00:00Z",
      end: "2026-11-03T12:30:00Z",
      estimatedMinutes: 210,
    },
    {
      kind: "TASK",
      name: "Đóng gói",
      type: "OTHER",
      start: "2026-11-03T12:30:00Z",
      end: "2026-11-03T13:00:00Z",
      estimatedMinutes: 30,
    },
    {
      kind: "EVENT",
      name: "Bán đồ ăn buổi sáng",
      type: "WORK",
      start: "2026-11-04T00:30:00Z",
      end: "2026-11-04T04:30:00Z",
    },
    {
      kind: "EVENT",
      name: "Bán đồ ăn buổi chiều",
      type: "WORK",
      start: "2026-11-04T07:00:00Z",
      end: "2026-11-04T10:45:00Z",
    },
    {
      kind: "EVENT",
      name: "Mua quà",
      type: "OTHER",
      start: "2026-11-05T09:00:00Z",
      end: "2026-11-05T10:30:00Z",
    },
    {
      kind: "EVENT",
      name: "Tổ chức sự kiện",
      type: "OTHER",
      start: "2026-11-06T08:00:00Z",
      end: "2026-11-06T10:30:00Z",
    },
  ];
  render(<NotesPage onWorkspaceChanged={vi.fn()} />);
  await userEvent.click(
    await screen.findByRole("button", { name: "Project SSG105" }),
  );
  expect(
    screen.getByText("3 mục cần kiểm tra trước khi xem lịch"),
  ).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }).disabled,
  ).toBe(true);
  expect(calls.some((x) => x.url.endsWith("/proposals"))).toBe(false);
  await userEvent.click(
    screen.getByRole("button", { name: "Chỉ giữ các mục hợp lệ đã chọn" }),
  );
  expect(
    screen.getByRole("checkbox", { name: "Chọn Chuẩn bị tiền quỹ" }).checked,
  ).toBe(false);
  expect(
    screen.getByRole("checkbox", { name: "Chọn Chế biến đồ ăn" }).checked,
  ).toBe(false);
  expect(screen.getByRole("checkbox", { name: "Chọn Đóng gói" }).checked).toBe(
    false,
  );
  expect(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }).disabled,
  ).toBe(true);
  await userEvent.click(
    screen.getByRole("checkbox", {
      name: "Tôi đã kiểm tra các lưu ý, giờ rảnh và điều kiện trong ghi chú.",
    }),
  );
  await userEvent.click(
    screen.getByRole("button", { name: "Xem lịch đề xuất" }),
  );
  await screen.findByRole("button", { name: "Lưu sự kiện & công việc" });
  expect(
    calls
      .find((x) => x.url.endsWith("/proposals"))
      .body.activities.map((x) => x.name),
  ).toEqual([
    "Bán đồ ăn buổi sáng",
    "Bán đồ ăn buổi chiều",
    "Mua quà",
    "Tổ chức sự kiện",
  ]);
  expect(
    calls.some(
      (x) => x.url.endsWith("/apply") || x.url === "/api/time/availability",
    ),
  ).toBe(false);
});
