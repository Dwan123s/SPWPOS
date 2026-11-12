import { beforeEach, expect, it, vi } from "vitest";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { api } from "../src/api.js";
import {
  EventConflictReview,
  TodayWorkload,
  WorkloadWeek,
} from "../src/workload.jsx";
import { state } from "../src/ui.jsx";
import { App } from "../src/App.jsx";
import { update } from "../src/store.js";
import { openSessionOutcome } from "../src/workload.jsx";
import { workspace, resetFixtures } from "./fixtures.js";

vi.mock("../src/api.js", () => ({ api: vi.fn() }));

const view = () => ({
  asOf: "2026-11-11T03:00:00Z",
  start: "2026-11-09",
  end: "2026-11-15",
  completedTasks: 0,
  next: { kind: "SETUP", reason: "Xác nhận giờ làm." },
  tasks: [],
  days: [
    {
      date: "2026-11-11",
      status: "UNCONFIGURED",
      workingMinutes: null,
      reservedMinutes: null,
      schedulableMinutes: null,
      plannedSeconds: 0,
      recordedActualSeconds: null,
      sessions: [],
    },
  ],
});

beforeEach(() => {
  resetFixtures();
  state.workspace = workspace();
  api.mockReset();
  update({
    screen: null,
    screenKey: "loading",
    modal: null,
    toasts: [],
    slots: {},
  });
});

it("records total remaining work without generating a recovery plan", async () => {
  api.mockResolvedValue({
    sessionId: "ended",
    taskId: "task",
    outcome: "PARTIAL",
    actualSeconds: 3600,
    remainingMinutes: 90,
  });
  const changed = vi.fn();
  render(<App initialize={false} />);
  act(() =>
    openSessionOutcome(
      { id: "ended", name: "Report", timerSeconds: 3600 },
      changed,
    ),
  );
  fireEvent.change(
    screen.getByLabelText("Tổng số phút còn cần, gồm cả các phiên đã đặt"),
    { target: { value: "90" } },
  );
  fireEvent.submit(
    screen.getByRole("button", { name: "Lưu kết quả" }).closest("form"),
  );
  await screen.findByRole("button", { name: "Xem phương án" });
  expect(api).toHaveBeenCalledWith("/api/sessions/ended/outcome", "POST", {
    outcome: "PARTIAL",
    actualMinutes: null,
    remainingMinutes: 90,
  });
  expect(api.mock.calls.some(([path]) => path.includes("/api/plans"))).toBe(
    false,
  );
  expect(changed).toHaveBeenCalledOnce();
});

it("offers setup and capture with AI disabled and creates no proposal on opening", async () => {
  api.mockResolvedValue(view());
  render(<TodayWorkload />);
  expect(
    await screen.findByRole("button", { name: "Thiết lập giờ làm" }),
  ).toBeTruthy();
  expect(screen.getByRole("button", { name: "Ghi việc trước" })).toBeTruthy();
  expect(api.mock.calls).toEqual([["/api/intelligence/workload"]]);
  fireEvent.click(screen.getByRole("button", { name: "Bỏ qua" }));
  expect(screen.queryByRole("region", { name: "Bước tiếp theo" })).toBeNull();
});

it("shows unplanned Inbox items separately from scheduled workload", async () => {
  api.mockResolvedValue({
    ...view(),
    tasksMissingPlanningDetails: 2,
    tasksAwaitingSchedule: 1,
    next: { kind: "INBOX", reason: "Có 2 việc chưa đủ thông tin để xếp lịch." },
  });
  render(<WorkloadWeek />);
  expect(await screen.findByText("Thiếu thông tin xếp lịch")).toBeTruthy();
  expect(screen.getByText("Đủ thông tin, chưa xếp đủ thời gian")).toBeTruthy();
  const open = screen.getByRole("button", { name: "Mở công việc" });
  expect(open.dataset.action).toBe("open-task-list");
  expect(screen.queryByRole("button", { name: "Ghi việc trước" })).toBeNull();
});

it("discards an older response after a newer workspace refresh", async () => {
  let finishOld;
  api.mockImplementationOnce(
    () =>
      new Promise((resolve) => {
        finishOld = resolve;
      }),
  );
  const fresh = view();
  fresh.next = { kind: "CAPTURE", reason: "Giờ làm đã lưu." };
  api.mockResolvedValueOnce(fresh);
  render(<TodayWorkload />);
  act(() => window.dispatchEvent(new Event("workspace-updated")));
  await screen.findByText("Giờ làm đã lưu.");
  await act(async () => finishOld(view()));
  expect(
    screen.queryByRole("button", { name: "Thiết lập giờ làm" }),
  ).toBeNull();
});

it("shows missing actual separately from a recorded zero and uses day details", async () => {
  const data = view();
  data.days[0].status = "PAST";
  data.days.push({
    ...data.days[0],
    date: "2026-11-12",
    status: "PAST",
    recordedActualSeconds: 0,
  });
  api.mockResolvedValue(data);
  render(<WorkloadWeek />);
  await screen.findByRole("table");
  for (const [date, expected] of [["11/11", "—"], ["12/11", "0p"]]) {
    fireEvent.click(screen.getByRole("button", { name: new RegExp(`${date}.*dự kiến`) }));
    const label = document.querySelector(".workload-day:not([hidden]) dt:last-of-type");
    expect(label?.textContent).toBe("Thực làm");
    expect(label.nextElementSibling.textContent).toBe(expected);
  }
});

it("shows the following week and moves the workload window", async () => {
  const current = view();
  const next = {
    ...view(),
    start: "2026-11-16",
    end: "2026-11-22",
    days: [{ ...current.days[0], date: "2026-11-21" }],
  };
  const later = { ...view(), start: "2026-11-23", end: "2026-11-29", days: [] };
  api.mockImplementation(async (path) => {
    if (path.endsWith("?week=1")) return next;
    if (path.endsWith("?week=2")) return later;
    return current;
  });
  render(<WorkloadWeek />);
  await screen.findByTitle("2026-11-09 – 2026-11-22");
  expect(screen.getByRole("columnheader", { name: /21\/11/ })).toBeTruthy();
  fireEvent.click(screen.getByRole("checkbox", { name: "Hiện cuối tuần" }));
  expect(screen.queryByRole("columnheader", { name: /21\/11/ })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Tuần sau" }));
  await screen.findByTitle("2026-11-16 – 2026-11-29");
  expect(api).toHaveBeenCalledWith("/api/intelligence/workload?week=2");
  fireEvent.change(screen.getByRole("combobox", { name: "Chuyển đến tuần" }), { target: { value: "0" } });
  await screen.findByTitle("2026-11-09 – 2026-11-22");
});

it("groups child tasks under their project and standalone tasks under Inbox", async () => {
  const data = view();
  state.workspace.tasks = state.workspace.tasks.map((task, index) =>
    index < 2 ? { ...task, parentId: "task-3" } : task,
  );
  data.days[0].sessions = ["Viết đề cương", "Rà soát tài liệu", "Kiểm tra email"].map(
    (name, index) => ({
      id: `session-${index}`,
      taskId: `task-${index}`,
      name,
      start: "2026-11-11T09:00:00Z",
      end: "2026-11-11T09:30:00Z",
    }),
  );
  api.mockResolvedValue(data);
  render(<WorkloadWeek />);
  const project = state.workspace.tasks[3].name;
  const cell = await screen.findByRole("button", { name: (label) => label.startsWith(project + ",") });
  expect(cell.querySelector(".workload-group-hours").textContent).toBe("1h");
  expect(cell.querySelector(".workload-group-name").textContent).toBe(project);
  const popup = document.getElementById(cell.getAttribute("popovertarget"));
  expect(popup.getAttribute("popover")).toBe("auto");
  expect(popup.querySelector('[data-action="task-detail"][data-id="task-0"]').textContent).toMatch(/^Viết đề cương.*30p$/);
  expect(popup.querySelector('[data-action="task-detail"][data-id="task-1"]').textContent).toMatch(/^Rà soát tài liệu.*30p$/);
  expect(popup.querySelector('[data-action="toggle-task-completion"][data-id="task-0"]')).toBeTruthy();
  expect(popup.querySelector('[data-id="task-2"]')).toBeNull();
  const inbox = screen.getByRole("button", { name: /^Inbox,/ });
  const inboxPopup = document.getElementById(inbox.getAttribute("popovertarget"));
  expect(inboxPopup.querySelector('[data-action="task-detail"][data-id="task-2"]').textContent).toMatch(/^Kiểm tra email.*30p$/);
  expect(inboxPopup.querySelector('[data-action="new-task"]').dataset.startDate).toBe("2026-11-11");
  const person = screen.getByRole("button", { name: "Cá nhân", expanded: true });
  fireEvent.click(person);
  expect(screen.queryByRole("button", { name: /^Inbox,/ })).toBeNull();
  fireEvent.click(person);
  expect(screen.getByRole("button", { name: /^Inbox,/ })).toBeTruthy();
});

it("adds the Inbox band only after a task is scheduled in the visible dates", async () => {
  const empty = view();
  empty.days.push({ ...empty.days[0], date: "2026-11-12" });
  api.mockResolvedValue(empty);
  render(<WorkloadWeek />);
  const matrix = await screen.findByRole("table");
  const blankRow = matrix.querySelector(".workload-empty-row");
  expect(blankRow.cells).toHaveLength(3);
  expect(blankRow.textContent).toBe("");
  expect(matrix.querySelector(".workload-group-row")).toBeNull();
  expect(screen.queryByRole("button", { name: /^Inbox,/ })).toBeNull();

  const planned = {
    ...empty,
    days: [{
      ...empty.days[0],
      plannedSeconds: 1800,
      sessions: [{ id: "check-mail", taskId: "task-0", name: "Kiểm tra email",
        start: "2026-11-11T09:00:00Z", end: "2026-11-11T09:30:00Z" }],
    }, empty.days[1]],
  };
  api.mockResolvedValue(planned);
  act(() => window.dispatchEvent(new Event("workspace-updated")));
  const inbox = await screen.findByRole("button", { name: /^Inbox,/ });
  expect(matrix.querySelector(".workload-empty-row")).toBeNull();
  expect(inbox.closest("td").dataset.band).toBe("inbox");
  expect(inbox.closest("td").nextElementSibling.childElementCount).toBe(0);
  const popup = document.getElementById(inbox.getAttribute("popovertarget"));
  expect(popup.querySelector('[data-action="new-task"]').dataset.startDate).toBe("2026-11-11");
});

it("shows shared work and recipients without private capacity or a recipient picker", async () => {
  const team = {
    start: "2026-11-09",
    end: "2026-11-15",
    members: [{ userId: "an", name: "An" }, { userId: "binh", name: "Bình" }],
    tasks: [
      { id: "report", name: "Báo cáo nhóm", status: "Open", risk: "UNASSIGNED",
        deadline: "2026-11-12T10:00:00Z", estimatedMinutes: 60, progress: 0 },
      { id: "launch", name: "Chạy thử", status: "Assigned", risk: "ASSIGNED",
        deadline: "2026-11-13T10:00:00Z", estimatedMinutes: 90, progress: 50, assigneeId: "an" },
      { id: "review", name: "Rà soát nhóm", status: "Open", risk: "WAITING",
        deadline: "2026-11-14T10:00:00Z", estimatedMinutes: 30, progress: 0, designatedUserId: "an" },
    ],
  };
  api.mockImplementation(async (path) => {
    if (path === "/api/intelligence/workload") return view();
    if (path === "/api/teams") return [{ id: "swp", name: "SWP", role: "Leader" }];
    if (path === "/api/teams/swp/workload?week=0") return team;
    throw new Error("Unexpected API call");
  });
  render(<><App initialize={false} /><WorkloadWeek /></>);
  fireEvent.click(screen.getByRole("button", { name: "Nhóm", exact: true }));
  await screen.findByText("Báo cáo nhóm");
  expect(screen.getByText("50%")).toBeTruthy();
  expect(screen.getByText("Chờ người được chỉ định nhận")).toBeTruthy();
  expect(screen.queryByRole("checkbox", { name: "Hiện số giờ" })).toBeNull();
  expect(screen.queryByRole("button", { name: "Chọn người nhận" })).toBeNull();
  fireEvent.change(screen.getByLabelText("Lọc theo thành viên"), { target: { value: "an" } });
  expect(screen.queryByText("Báo cáo nhóm")).toBeNull();
  expect(screen.getByText("Rà soát nhóm")).toBeTruthy();
  fireEvent.change(screen.getByLabelText("Lọc tình trạng công việc nhóm"), { target: { value: "Assigned" } });
  expect(screen.queryByText("Rà soát nhóm")).toBeNull();
  expect(screen.getByText("Chạy thử")).toBeTruthy();
  expect(api.mock.calls.some(([url]) => url.endsWith("/candidates"))).toBe(false);
});

it("previews a conflicting event only on request and confirms only on a second action", async () => {
  const input = {
    name: "Meeting",
    start: "2026-11-12T08:00:00Z",
    end: "2026-11-12T09:00:00Z",
  };
  const onSaved = vi.fn();
  api
    .mockResolvedValueOnce({
      id: "proposal",
      event: input,
      before: [],
      after: [],
      warnings: [],
    })
    .mockResolvedValueOnce({ id: "event" });
  render(
    <EventConflictReview input={input} onSaved={onSaved} onBack={vi.fn()} />,
  );
  expect(api).not.toHaveBeenCalled();
  fireEvent.click(
    screen.getByRole("button", { name: "Giữ giờ này và xem lịch đề xuất" }),
  );
  const confirm = await screen.findByRole("button", {
    name: "Lưu Lịch Trình và áp dụng đề xuất",
  });
  expect(api.mock.calls).toEqual([
    ["/api/time/events/replan/preview", "POST", input],
  ]);
  fireEvent.click(confirm);
  await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
  expect(api).toHaveBeenLastCalledWith(
    "/api/time/events/replan/proposal/confirm",
    "POST",
  );
});
