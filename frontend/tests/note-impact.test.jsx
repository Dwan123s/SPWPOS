import { beforeEach, expect, it, vi } from "vitest";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NoteImpact } from "../src/note-impact.jsx";

const drafts = ["Báo cáo", "Rà soát"].map((name) => ({
  name,
  description: "",
  estimatedMinutes: 60,
  deadline: "2026-11-01T17:00:00Z",
  priority: 2,
  taskType: "WRITING",
  minimumSessionMinutes: 25,
  selected: true,
}));
const props = {
  drafts,
  horizonDays: 7,
  hasTime: true,
  enabled: true,
  workspace: {},
  onSetTime: vi.fn(),
};
const result = (overload = false) => ({
  before: {
    horizonDemandMinutes: 30,
    tasks: [{ taskId: "old", name: "Việc cũ", score: 0, reasons: [] }],
  },
  after: {
    horizonDemandMinutes: overload ? 180 : 90,
    effectiveCapacityMinutes: 120,
    riskLevel: overload ? "Overload" : "Low",
    tasks: overload
      ? [
          {
            taskId: "old",
            name: "Việc cũ",
            score: 55,
            reasons: ["DEADLINE_CAPACITY_DEFICIT"],
          },
        ]
      : [],
  },
  addedMinutes: 120,
  proposedPlan: {
    warnings: overload
      ? [
          {
            taskId: "new",
            taskName: "Báo cáo",
            code: "INSUFFICIENT_CAPACITY",
            message: "Không đủ giờ trước hạn chót.",
            unscheduledMinutes: 60,
          },
        ]
      : [],
  },
});
const response = (value) => ({
  ok: true,
  status: 200,
  json: async () => ({ data: value }),
});
beforeEach(() =>
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => response(result())),
  ),
);

it("automatically checks all selected drafts together and shows actionable overload without saving", async () => {
  vi.mocked(fetch).mockResolvedValue(response(result(true)));
  render(<NoteImpact {...props} />);
  await screen.findByText(/Khối lượng sau khi thêm vượt khả năng thực hiện 1h/);
  expect(screen.getByText(/Không đủ giờ trước hạn chót/)).toBeTruthy();
  expect(screen.getByText("Công việc đang có bị tăng rủi ro")).toBeTruthy();
  expect(fetch).toHaveBeenCalledOnce();
  const [url, options] = vi.mocked(fetch).mock.calls[0];
  expect(url).toBe("/api/plans/scenario");
  const input = JSON.parse(options.body);
  expect(input.newTask.name).toBe("Báo cáo");
  expect(input.additionalTasks.map((task) => task.name)).toEqual(["Rà soát"]);
  expect(input.plan.isReschedule).toBe(true);
});

it("hides stale conclusions immediately and ignores an older response after drafts change", async () => {
  let releaseOld;
  vi.mocked(fetch).mockImplementationOnce(
    () =>
      new Promise((resolve) => {
        releaseOld = resolve;
      }),
  );
  const view = render(<NoteImpact {...props} />);
  await waitFor(() => expect(fetch).toHaveBeenCalledOnce());
  view.rerender(
    <NoteImpact
      {...props}
      drafts={[
        { ...drafts[0], estimatedMinutes: 30 },
        { ...drafts[1], selected: false },
      ]}
    />,
  );
  await screen.findByText(/Chưa thấy vấn đề khi thêm các việc được chọn/);
  await act(async () => releaseOld(response(result(true))));
  expect(
    screen.queryByText(/Khối lượng sau khi thêm vượt khả năng/),
  ).toBeNull();
  expect(
    JSON.parse(vi.mocked(fetch).mock.calls[1][1].body).additionalTasks,
  ).toEqual([]);
  view.rerender(
    <NoteImpact {...props} drafts={[{ ...drafts[0], deadline: "" }]} />,
  );
  expect(screen.queryByText(/Chưa thấy vấn đề khi thêm/)).toBeNull();
  expect(
    screen.getByText(/Điền tên việc, thời lượng và hạn chót hợp lệ/),
  ).toBeTruthy();
});

it("preserves the opened mobile calendar and chosen day after a task edit", async () => {
  vi.stubGlobal("matchMedia", () => ({ matches: true }));
  const forecast = result();
  forecast.proposedPlan = {
    ...forecast.proposedPlan,
    start: "2026-10-14T00:00:00Z",
    sessions: [],
  };
  vi.mocked(fetch).mockResolvedValue(response(forecast));
  const user = userEvent.setup();
  const view = render(<NoteImpact {...props} showPreview />);
  await screen.findByText(/Chưa thấy vấn đề/);
  await user.click(screen.getByText(/Xem lịch dự kiến/));
  await waitFor(() =>
    expect(document.querySelector(".note-calendar-disclosure").open).toBe(true),
  );
  await user.selectOptions(
    screen.getByRole("combobox", { name: "Ngày xem lịch dự kiến" }),
    "2026-10-15",
  );
  view.rerender(
    <NoteImpact
      {...props}
      drafts={[{ ...drafts[0], estimatedMinutes: 30 }]}
      showPreview
    />,
  );
  expect(
    screen.queryByRole("region", { name: "Lịch thử từ ghi chú" }),
  ).toBeNull();
  await screen.findByRole("combobox", { name: "Ngày xem lịch dự kiến" });
  expect(document.querySelector(".note-calendar-disclosure").open).toBe(true);
  expect(
    screen.getByRole("combobox", { name: "Ngày xem lịch dự kiến" }).value,
  ).toBe("2026-10-15");
});

it("reports unavailable checks and permits retry instead of claiming a safe schedule", async () => {
  vi.mocked(fetch).mockRejectedValueOnce(new Error("Mất kết nối."));
  render(<NoteImpact {...props} />);
  await screen.findByText(/Chưa kiểm tra được tác động: Mất kết nối/);
  expect(screen.queryByText(/Chưa thấy vấn đề/)).toBeNull();
  await userEvent
    .setup()
    .click(screen.getByRole("button", { name: "Thử kiểm tra lại" }));
  await screen.findByText(/Chưa thấy vấn đề khi thêm/);
});

it("asks for availability and never simulates already imported work again", async () => {
  const onSetTime = vi.fn();
  const view = render(
    <NoteImpact {...props} hasTime={false} onSetTime={onSetTime} />,
  );
  await userEvent
    .setup()
    .click(screen.getByRole("button", { name: "Chọn giờ rảnh để kiểm tra" }));
  expect(onSetTime).toHaveBeenCalledOnce();
  expect(fetch).not.toHaveBeenCalled();
  view.rerender(<NoteImpact {...props} enabled={false} />);
  expect(
    screen.queryByRole("heading", { name: "Kiểm tra trước khi nhận việc" }),
  ).toBeNull();
});
