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
  await screen.findByRole("heading", { name: "Tải công việc trong tuần" });
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
  await screen.findByText("2026-11-09 – 2026-11-22");
  expect(screen.getByRole("columnheader", { name: /21\/11/ })).toBeTruthy();
  fireEvent.click(screen.getByRole("checkbox", { name: "Hiện cuối tuần" }));
  expect(screen.queryByRole("columnheader", { name: /21\/11/ })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Tuần sau" }));
  await screen.findByText("2026-11-16 – 2026-11-29");
  expect(api).toHaveBeenCalledWith("/api/intelligence/workload?week=2");
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
  expect(popup.querySelector('[data-id="task-0"]').textContent).toMatch(/^Viết đề cương.*30p$/);
  expect(popup.querySelector('[data-id="task-1"]').textContent).toMatch(/^Rà soát tài liệu.*30p$/);
  expect(popup.querySelector('[data-id="task-2"]')).toBeNull();
  const inbox = screen.getByRole("button", { name: /^Inbox,/ });
  const inboxPopup = document.getElementById(inbox.getAttribute("popovertarget"));
  expect(inboxPopup.querySelector('[data-id="task-2"]').textContent).toMatch(/^Kiểm tra email.*30p$/);
  expect(inboxPopup.querySelector('[data-action="new-task"]')).toBeTruthy();
});

it("compares team capacity and only changes the recipient after confirmation", async () => {
  const team = {
    start: "2026-11-09",
    end: "2026-11-15",
    members: [
      {
        userId: "an",
        name: "An",
        timeZoneId: "Asia/Ho_Chi_Minh",
        days: [
          {
            date: "2026-11-11",
            workingMinutes: 480,
            busyMinutes: 60,
            reservedMinutes: 180,
            schedulableMinutes: 240,
            recordedActualSeconds: 0,
          },
        ],
      },
    ],
    tasks: [
      {
        id: "report",
        name: "Báo cáo nhóm",
        status: "Open",
        risk: "UNASSIGNED",
        deadline: "2026-11-12T10:00:00Z",
        remainingMinutes: 60,
        version: "v1",
      },
      {
        id: "launch",
        name: "Chạy thử",
        status: "Assigned",
        risk: "BEHIND",
        deadline: "2026-11-13T10:00:00Z",
        remainingMinutes: 90,
        shortfallMinutes: 30,
        assigneeId: "an",
      },
    ],
  };
  const wednesday = team.members[0].days[0];
  team.members[0].days.push({ ...wednesday, date: "2026-11-14" });
  team.members.push({
    userId: "binh",
    name: "Bình",
    timeZoneId: "UTC",
    days: team.members[0].days,
  });
  api.mockImplementation(async (path, method, body) => {
    if (path === "/api/intelligence/workload") return view();
    if (path === "/api/teams")
      return [{ id: "swp", name: "SWP", role: "Leader" }];
    if (path === "/api/teams/swp/workload?week=0") return team;
    if (path === "/api/teams/swp/workload?week=1") {
      return {
        ...team,
        start: "2026-11-16",
        end: "2026-11-22",
        members: [
          { ...team.members[1], days: [{ ...wednesday, date: "2026-11-18" }] },
        ],
      };
    }
    if (path === "/api/teams/swp/workload/report/candidates")
      return [{ userId: "an", status: "FITS", schedulableMinutes: 240 }];
    if (path === "/api/teams/tasks/report/recipient" && method === "POST") {
      team.tasks[0] = {
        ...team.tasks[0],
        designatedUserId: body.userId,
        risk: "WAITING",
      };
      return {};
    }
    throw new Error("Unexpected API call");
  });
  render(
    <>
      <App initialize={false} />
      <WorkloadWeek />
    </>,
  );
  fireEvent.click(screen.getByRole("button", { name: "Nhóm", exact: true }));
  fireEvent.click(await screen.findByText("Điều phối công việc nhóm"));
  await screen.findByText("Báo cáo nhóm");
  await screen.findByRole("columnheader", { name: /18\/11/ });
  fireEvent.click(screen.getByRole("checkbox", { name: "Hiện cuối tuần" }));
  expect(screen.queryByRole("columnheader", { name: /14\/11/ })).toBeNull();
  fireEvent.change(screen.getByLabelText("Lọc theo thành viên"), {
    target: { value: "an" },
  });
  expect(screen.queryByRole("rowheader", { name: "Bình" })).toBeNull();
  expect(screen.queryByText("Báo cáo nhóm")).toBeNull();
  fireEvent.change(screen.getByLabelText("Lọc theo thành viên"), {
    target: { value: "" },
  });
  fireEvent.click(screen.getByRole("checkbox", { name: "Hiện số giờ" }));
  expect(screen.getByRole("button", { name: /^An,/ }).textContent).toContain(
    "Còn chỗ",
  );
  fireEvent.click(screen.getByRole("button", { name: /^An,/ }));
  expect(screen.getByText("Thực làm cho nhóm")).toBeTruthy();
  fireEvent.change(screen.getByLabelText("Lọc tình trạng công việc nhóm"), {
    target: { value: "risk" },
  });
  expect(screen.queryByText("Báo cáo nhóm")).toBeNull();
  expect(screen.getByText("Dự kiến trễ")).toBeTruthy();
  fireEvent.change(screen.getByLabelText("Lọc tình trạng công việc nhóm"), {
    target: { value: "all" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Chọn người nhận" }));
  fireEvent.click(await screen.findByRole("radio", { name: /^An/ }));
  expect(api.mock.calls.some(([, method]) => method === "POST")).toBe(false);
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận người nhận" }));
  await screen.findByRole("button", { name: "Đổi người nhận" });
  expect(api).toHaveBeenCalledWith(
    "/api/teams/tasks/report/recipient",
    "POST",
    {
      version: "v1",
      userId: "an",
    },
  );
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
    screen.getByRole("button", { name: "Giữ giờ sự kiện và xem lịch đề xuất" }),
  );
  const confirm = await screen.findByRole("button", {
    name: "Xác nhận sự kiện và lịch mới",
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
