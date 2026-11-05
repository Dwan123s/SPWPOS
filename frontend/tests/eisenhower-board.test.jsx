import { beforeEach, expect, it, vi } from "vitest";
import {
  fireEvent,
  render,
  screen,
  within,
  waitFor,
} from "@testing-library/react";
import { EisenhowerBoard } from "../src/eisenhower-board.jsx";
import { moveChecklist } from "../src/eisenhower-drag.js";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";
const columns = [
  {
    id: "Q1",
    label: "Làm trước",
    caption: "Quan trọng · Khẩn cấp",
    tone: "urgent",
  },
  {
    id: "Q2",
    label: "Chủ động lên lịch",
    caption: "Quan trọng · Chưa khẩn cấp",
    tone: "schedule",
  },
  {
    id: "Q3",
    label: "Xử lý / phân công",
    caption: "Ảnh hưởng thấp hơn · Khẩn cấp",
    tone: "delegate",
  },
  {
    id: "Q4",
    label: "Có thể để sau",
    caption: "Ảnh hưởng thấp hơn · Chưa khẩn cấp",
    tone: "later",
  },
  { id: "Unknown", label: "Cần làm rõ", caption: "", tone: "unknown" },
  { id: "Review", label: "Chờ bạn duyệt", caption: "", tone: "review" },
];
const item = (id = "a", quadrant = "Q2", extra = {}) => ({
  id,
  name: `Report ${id}`,
  kind: "Personal",
  quadrant,
  deadline: "2026-11-08T12:00:00+07:00",
  estimatedMinutes: 120,
  remainingMinutes: 120,
  version: `version-${id}`,
  urgencySource: "User",
  canEdit: true,
  description: "Yêu cầu",
  explanation: "Còn đủ thời gian trước hạn.",
  checklist: [
    { id: "step1", text: "Đọc đề", completed: false },
    { id: "step2", text: "Viết nháp", completed: false },
  ],
  ...extra,
});
let refresh, openTeam;
function setup(tasks = [item()]) {
  const view = { tasks, revision: "revision1" };
  return render(
    <EisenhowerBoard
      view={view}
      columns={columns}
      retry={vi.fn()}
      onRefresh={refresh}
      onOpenTeam={openTeam}
    />,
  );
}
beforeEach(() => {
  vi.resetAllMocks();
  state.workspace = workspace();
  state.workspace.features = ["eisenhower-v1"];
  refresh = vi.fn().mockResolvedValue();
  openTeam = vi.fn();
  api.mockResolvedValue({ id: "created" });
  window.PointerEvent = MouseEvent;
});
it("quickly captures an undated task in the selected quadrant without asking for duration or calling AI", async () => {
  setup([]);
  fireEvent.click(
    screen.getByRole("button", { name: "Thêm task vào Chủ động lên lịch" }),
  );
  const dialog = screen.getByRole("dialog", { name: "Thêm task vào ma trận" });
  fireEvent.change(within(dialog).getByLabelText("Tên công việc"), {
    target: { value: "Report SWP" },
  });
  fireEvent.change(within(dialog).getByLabelText("Mô tả"), {
    target: { value: "Đọc yêu cầu" },
  });
  expect(within(dialog).queryByLabelText("Thời lượng (phút)")).toBeNull();
  expect(
    within(dialog).queryByRole("button", { name: "Thêm bước" }),
  ).toBeNull();
  fireEvent.click(
    within(dialog).getByRole("button", { name: "Thêm task", exact: true }),
  );
  await waitFor(() => expect(api).toHaveBeenCalledTimes(1));
  expect(api.mock.calls[0][0]).toBe("/api/eisenhower/drafts");
  expect(api.mock.calls[0][2]).toMatchObject({
    name: "Report SWP",
    description: "Đọc yêu cầu",
    deadline: null,
    quadrant: "Q2",
    requestId: expect.any(String),
  });
  expect(api.mock.calls[0][2]).not.toHaveProperty("estimatedMinutes");
  expect(api.mock.calls[0][2]).not.toHaveProperty("checklist");
  await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  expect(
    api.mock.calls.some(
      ([url]) => url.includes("/plans") || url.includes("/agent"),
    ),
  ).toBe(false);
});
it("lets the compact popup set or clear a deadline, and keeps the draft and request id after a failed save", async () => {
  api
    .mockRejectedValueOnce(new Error("Không lưu được task nháp."))
    .mockResolvedValue({ id: "created" });
  setup([]);
  fireEvent.click(
    screen.getByRole("button", { name: "Thêm task vào Có thể để sau" }),
  );
  const dialog = screen.getByRole("dialog", { name: "Thêm task vào ma trận" });
  fireEvent.change(within(dialog).getByLabelText("Tên công việc"), {
    target: { value: "Một ý tưởng mới" },
  });
  fireEvent.click(
    within(dialog).getByRole("button", { name: "Hạn chót", exact: true }),
  );
  fireEvent.change(within(dialog).getByLabelText("Hạn chót"), {
    target: { value: "2026-11-08T12:00" },
  });
  fireEvent.click(within(dialog).getByRole("button", { name: "Xong" }));
  fireEvent.click(
    within(dialog).getByRole("button", { name: "Thêm task", exact: true }),
  );
  expect(await within(dialog).findByRole("alert")).toHaveProperty(
    "textContent",
    "Không lưu được task nháp.",
  );
  expect(within(dialog).getByLabelText("Tên công việc").value).toBe(
    "Một ý tưởng mới",
  );
  const requestId = api.mock.calls[0][2].requestId;
  fireEvent.click(within(dialog).getByRole("button", { name: /12:00/ }));
  fireEvent.click(
    within(dialog).getByRole("button", { name: "Không có ngày hạn" }),
  );
  fireEvent.click(
    within(dialog).getByRole("button", { name: "Thêm task", exact: true }),
  );
  await waitFor(() => expect(api).toHaveBeenCalledTimes(2));
  expect(api.mock.calls[1][2]).toMatchObject({
    deadline: null,
    requestId,
    quadrant: "Q4",
  });
});
it("renders undated Inbox tasks safely and moves them without creating a real task or applying a plan", async () => {
  setup([
    item("draft", "Q2", {
      kind: "Draft",
      deadline: null,
      estimatedMinutes: 0,
      remainingMinutes: 0,
      checklist: [],
    }),
  ]);
  expect(within(screen.getAllByRole("article")[0]).getByText("Chưa có ngày hạn")).toBeTruthy();
  expect(screen.queryByText("0 phút")).toBeNull();
  fireEvent.change(
    screen.getByRole("combobox", { name: "Thời hạn công việc" }),
    { target: { value: "NoDate" } },
  );
  fireEvent.click(
    screen.getByRole("button", { name: "Tùy chọn Report draft" }),
  );
  fireEvent.change(
    screen.getByRole("combobox", { name: "Di chuyển Report draft" }),
    { target: { value: "Q1" } },
  );
  await waitFor(() => expect(api).toHaveBeenCalledTimes(1));
  expect(api.mock.calls[0]).toEqual([
    "/api/eisenhower/drafts/draft/position",
    "POST",
    {
      version: "version-draft",
      revision: "revision1",
      quadrant: "Q1",
      beforeId: null,
    },
  ]);
});
it("toggles a checklist step without marking the whole task completed", async () => {
  setup();
  expect(screen.queryByRole("checkbox", { name: "Đọc đề" })).toBeNull();
  fireEvent.click(
    screen.getByRole("button", { name: "Checklist Report a · 0/2" }),
  );
  fireEvent.click(screen.getByRole("checkbox", { name: "Đọc đề" }));
  await waitFor(() => expect(api).toHaveBeenCalledTimes(1));
  expect(api.mock.calls[0]).toEqual([
    "/api/eisenhower/tasks/a/checklist",
    "PUT",
    {
      version: "version-a",
      items: [
        { id: "step1", text: "Đọc đề", completed: true },
        { id: "step2", text: "Viết nháp", completed: false },
      ],
    },
  ]);
});
it("locks splitting for a continuous activity and explains the constraint", () => {
  setup([item("meeting", "Q2", {name: "Họp nhóm", splittable: false, requiresContinuousSession: true})]);
  fireEvent.click(screen.getByRole("button", { name: "Họp nhóm", exact: true }));
  const dialog = within(screen.getByRole("dialog", { name: "Chi tiết task" }));
  const option = dialog.getByRole("checkbox", {name: "Có thể chia thành nhiều phiên làm việc"});
  expect(option.disabled).toBe(true);
  expect(option.checked).toBe(false);
  expect(dialog.getByText("Hoạt động này cần diễn ra liên tục, không thể chia thành nhiều phiên.")).toBeTruthy();
});
it("opens task detail and reorders checklist with the keyboard, saving the exact order", async () => {
  setup();
  fireEvent.click(
    screen.getByRole("button", { name: "Report a", exact: true }),
  );
  const dialog = screen.getByRole("dialog", { name: "Chi tiết task" });
  fireEvent.keyDown(within(dialog).getByRole("button", { name: "Kéo mục 2" }), {
    key: "ArrowUp",
    altKey: true,
  });
  expect(within(dialog).getByLabelText("Nội dung mục 1").value).toBe(
    "Viết nháp",
  );
  fireEvent.click(within(dialog).getByRole("button", { name: "Lưu thay đổi" }));
  await waitFor(() => expect(api).toHaveBeenCalledTimes(1));
  expect(api.mock.calls[0][2].checklist.map((i) => i.id)).toEqual([
    "step2",
    "step1",
  ]);
});
it("moves tasks between quadrants from a mobile/keyboard accessible selector, without changing deadline", async () => {
  setup();
  fireEvent.click(screen.getByRole("button", { name: "Tùy chọn Report a" }));
  fireEvent.change(
    screen.getByRole("combobox", { name: "Di chuyển Report a" }),
    { target: { value: "Q1" } },
  );
  await waitFor(() => expect(api).toHaveBeenCalledTimes(1));
  expect(api.mock.calls[0]).toEqual([
    "/api/eisenhower/tasks/a/position",
    "POST",
    {
      version: "version-a",
      revision: "revision1",
      quadrant: "Q1",
      beforeId: null,
    },
  ]);
  expect(api.mock.calls[0][2]).not.toHaveProperty("deadline");
});
it("reorders tasks in a quadrant with Alt+arrow and handles stale writes without optimistic movement", async () => {
  setup([item("a"), item("b")]);
  api.mockRejectedValue(new Error("Task vừa thay đổi. Tải lại."));
  fireEvent.keyDown(screen.getByRole("button", { name: "Kéo task Report b" }), {
    key: "ArrowUp",
    altKey: true,
  });
  await screen.findByRole("alert");
  expect(api.mock.calls[0][2]).toMatchObject({ quadrant: "Q2", beforeId: "a" });
  expect(screen.getAllByRole("article")[0].textContent).toContain("Report a");
});
it("mouse/touch pointer drop saves only on release and Escape cancels", async () => {
  setup();
  const target = screen.getByRole("region", { name: "Làm trước" });
  document.elementFromPoint = vi.fn(() => target);
  const handle = screen.getByRole("button", { name: "Kéo task Report a" });
  fireEvent.pointerDown(handle, { button: 0, clientX: 10, clientY: 10 });
  fireEvent.pointerMove(window, { clientX: 100, clientY: 100 });
  expect(api).not.toHaveBeenCalled();
  expect(target.className).toContain("is-drop-zone");
  fireEvent.keyDown(window, { key: "Escape" });
  fireEvent.pointerUp(window, { clientX: 100, clientY: 100 });
  expect(api).not.toHaveBeenCalled();
  fireEvent.pointerDown(handle, { button: 0, clientX: 10, clientY: 10 });
  fireEvent.pointerMove(window, { clientX: 100, clientY: 100 });
  fireEvent.pointerUp(window, { clientX: 100, clientY: 100 });
  await waitFor(() => expect(api).toHaveBeenCalledTimes(1));
  expect(api.mock.calls[0][2].quadrant).toBe("Q1");
});
it("Q4 only asks for a schedule on demand and applies only after a separate explicit click", async () => {
  setup([item("a", "Q4", { recommendation: "Backlog" })]);
  expect(api).not.toHaveBeenCalled();
  api.mockResolvedValueOnce({
    id: "plan",
    start: "2026-11-05T00:00:00Z",
    end: "2026-11-08T00:00:00Z",
    warnings: [],
    sessions: [
      {
        id: "s",
        taskId: "a",
        start: "2026-11-05T01:00:00Z",
        end: "2026-11-05T03:00:00Z",
        preserved: false,
      },
    ],
  });
  fireEvent.click(screen.getByRole("button", { name: "Tùy chọn Report a" }));
  fireEvent.click(screen.getByRole("button", { name: "Chọn giờ làm" }));
  await screen.findByRole("dialog", { name: "NORA · Lịch đề xuất" });
  expect(api.mock.calls[0][2].explicitRequest).toBe(true);
  expect(api).toHaveBeenCalledTimes(1);
  fireEvent.click(screen.getByRole("button", { name: "Áp dụng vào Calendar" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/plans/plan/apply", "POST"),
  );
});
it("infeasible proposals cannot be applied and show missing minutes", async () => {
  setup();
  api.mockResolvedValueOnce({
    id: "plan",
    sessions: [],
    warnings: [
      { taskName: "Report a", message: "Không đủ giờ", unscheduledMinutes: 30 },
    ],
  });
  fireEvent.click(screen.getByRole("button", { name: "Tùy chọn Report a" }));
  fireEvent.click(screen.getByRole("button", { name: "Đề xuất lịch" }));
  await screen.findByRole("dialog", { name: "NORA · Lịch đề xuất" });
  expect(
    screen.getByRole("button", { name: "Áp dụng vào Calendar" }).disabled,
  ).toBe(true);
  expect(screen.getByText(/Còn thiếu 30 phút/)).toBeTruthy();
});
it("accepted team task edits only its private checklist and directs completion to the team", async () => {
  setup([
    item("a", "Q2", {
      kind: "Accepted",
      canEdit: false,
      teamTaskId: "shared",
      teamId: "group",
    }),
  ]);
  fireEvent.click(
    screen.getByRole("button", { name: "Report a", exact: true }),
  );
  const dialog = screen.getByRole("dialog", { name: "Chi tiết task" });
  expect(within(dialog).getByLabelText("Tên công việc").disabled).toBe(true);
  expect(
    within(dialog).queryByRole("button", { name: "Đánh dấu hoàn thành task" }),
  ).toBeNull();
  fireEvent.click(within(dialog).getByLabelText("Hoàn thành mục 1"));
  fireEvent.click(within(dialog).getByRole("button", { name: "Lưu thay đổi" }));
  await waitFor(() => expect(api).toHaveBeenCalledTimes(1));
  expect(api.mock.calls[0][0]).toBe("/api/eisenhower/tasks/a/checklist");
});
it("checklist reorder never mutates its source and rejects unknown targets", () => {
  const rows = item().checklist;
  expect(moveChecklist(rows, "step2", "step1").map((i) => i.id)).toEqual([
    "step2",
    "step1",
  ]);
  expect(rows[0].id).toBe("step1");
  expect(moveChecklist(rows, "step1", "foreign")).toBe(rows);
});
it("filters personal/team work, combines search and resets without a request", () => {
  setup([
    item("personal"),
    item("team", "Q1", {
      kind: "Accepted",
      canEdit: false,
      teamId: "group",
      teamTaskId: "shared",
    }),
  ]);
  fireEvent.change(screen.getByLabelText("Nguồn công việc"), {
    target: { value: "Team" },
  });
  expect(
    screen.queryByRole("button", { name: "Report personal", exact: true }),
  ).toBeNull();
  expect(
    screen.getByRole("button", { name: "Report team", exact: true }),
  ).toBeTruthy();
  expect(
    screen.queryByRole("button", { name: "Hoàn thành task Report team" }),
  ).toBeNull();
  fireEvent.change(screen.getByLabelText("Tìm trong ma trận"), {
    target: { value: "missing" },
  });
  expect(screen.queryAllByRole("article")).toHaveLength(0);
  fireEvent.click(screen.getByRole("button", { name: "Xóa bộ lọc" }));
  expect(screen.getAllByRole("article")).toHaveLength(2);
  expect(api).not.toHaveBeenCalled();
});
it("filters deadlines by the account timezone and keeps overdue work visible", () => {
  vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-11-04T00:00:00Z"));
  setup([
    item("old", "Q2", { deadline: "2026-11-03T15:00:00Z" }),
    item("last", "Q2", { deadline: "2026-11-10T16:59:00Z" }),
    item("outside", "Q2", { deadline: "2026-11-10T17:00:00Z" }),
  ]);
  fireEvent.change(screen.getByLabelText("Thời hạn công việc"), {
    target: { value: "7" },
  });
  expect(screen.getAllByRole("article")).toHaveLength(2);
  expect(
    screen.getByRole("button", { name: "Report last", exact: true }),
  ).toBeTruthy();
  expect(
    screen.queryByRole("button", { name: "Report outside", exact: true }),
  ).toBeNull();
  fireEvent.change(screen.getByLabelText("Thời hạn công việc"), {
    target: { value: "Overdue" },
  });
  expect(screen.getAllByRole("article")).toHaveLength(1);
  expect(
    screen.getByRole("button", { name: "Report old", exact: true }),
  ).toBeTruthy();
  expect(api).not.toHaveBeenCalled();
});
it("sorts without writes and only allows cross-quadrant drops under deadline sorting", async () => {
  setup([
    item("a", "Q2", { order: 1, deadline: "2026-11-09T12:00:00Z" }),
    item("b", "Q2", { order: 2, deadline: "2026-11-08T12:00:00Z" }),
  ]);
  fireEvent.change(screen.getByLabelText("Sắp xếp công việc"), {
    target: { value: "Deadline" },
  });
  expect(screen.getAllByRole("article")[0].textContent).toContain("Report b");
  const handle = screen.getByRole("button", { name: "Kéo task Report a" });
  fireEvent.keyDown(handle, { key: "ArrowUp", altKey: true });
  document.elementFromPoint = vi.fn(() =>
    screen.getByRole("region", { name: "Chủ động lên lịch" }),
  );
  fireEvent.pointerDown(handle, { button: 0, clientX: 10, clientY: 10 });
  fireEvent.pointerMove(window, { clientX: 100, clientY: 100 });
  fireEvent.pointerUp(window, { clientX: 100, clientY: 100 });
  expect(api).not.toHaveBeenCalled();
  document.elementFromPoint = vi.fn(() =>
    screen.getByRole("region", { name: "Làm trước" }),
  );
  fireEvent.pointerDown(handle, { button: 0, clientX: 10, clientY: 10 });
  fireEvent.pointerMove(window, { clientX: 100, clientY: 100 });
  fireEvent.pointerUp(window, { clientX: 100, clientY: 100 });
  await waitFor(() => expect(api).toHaveBeenCalledTimes(1));
  expect(api.mock.calls[0][2]).toMatchObject({
    quadrant: "Q1",
    beforeId: null,
  });
});
it("keeps review and clarification queues accessible outside the four quadrants", () => {
  setup([
    item("unknown", "Unknown"),
    item("review", "Review", {
      kind: "Review",
      teamId: "group",
      teamTaskId: "shared",
    }),
  ]);
  expect(screen.queryByRole("region", { name: "Cần làm rõ" })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: /Chờ bạn duyệt/ }));
  fireEvent.click(
    screen.getByRole("button", { name: "Report review", exact: true }),
  );
  expect(openTeam).toHaveBeenCalledWith(
    expect.objectContaining({ teamTaskId: "shared" }),
  );
  expect(
    screen.queryByRole("button", { name: "Hoàn thành task Report review" }),
  ).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: /Cần làm rõ/ }));
  expect(
    screen.getByRole("button", { name: "Report unknown", exact: true }),
  ).toBeTruthy();
  expect(api).not.toHaveBeenCalled();
});
