import { beforeEach, expect, it, vi } from "vitest";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { TeamsPage, PrivateClaimPreview } from "../src/teams.jsx";
import { AvailabilityPolicyCard } from "../src/availability-policy.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";

const team = { id: "team", name: "Nhóm kiểm thử", role: "Member" };
const shared = {
  id: "shared",
  name: "Báo cáo chung",
  description: "Nội dung chung",
  estimatedMinutes: 60,
  deadline: new Date(Date.now() + 86400000).toISOString(),
  assigneeId: null,
  progress: 0,
  status: "Open",
  version: "version",
};
const option = {
  index: 0,
  label: "Xếp vào giờ trống",
  before: [],
  after: [
    {
      id: "session",
      taskName: "Việc riêng tư",
      start: "2026-11-03T12:00:00Z",
      end: "2026-11-03T13:00:00Z",
      preserved: false,
    },
  ],
  explanations: [],
};
const proposal = {
  id: "proposal",
  taskId: "shared",
  canUndo: true,
  options: [option],
  warnings: [],
};
beforeEach(() => {
  vi.resetAllMocks();
  state.workspace = workspace();
  state.noraAvailable = false;
});
function routes(extra = {}) {
  api.mockImplementation(async (path, method = "GET") => {
    if (extra[path]) return extra[path](method);
    if (path === "/api/teams") return [team];
    if (path === "/api/teams/team")
      return { team, members: [], tasks: [shared], activity: [] };
    if (path === "/api/teams/tasks/shared/preview") return proposal;
    return {};
  });
}

it("opens friend invitations from the team header without creating a share link", async () => {
  const leaderTeam = { ...team, role: "Leader" };
  routes({
    "/api/teams": () => [leaderTeam],
    "/api/teams/team": () => ({ team: leaderTeam, members: [], tasks: [shared], activity: [] }),
    "/api/teams/team/invite-friends?search=&offset=0": () => ({ items: [{ userId: "friend", name: "Minh Anh", status: "Available", avatarUrl: "" }], nextOffset: null }),
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Mời thành viên" }));
  const dialog = within(await screen.findByRole("dialog", { name: "Mời thành viên vào nhóm" }));
  expect(await dialog.findByRole("button", { name: "Mời Minh Anh" })).toBeTruthy();
  expect(dialog.getByRole("tab", { name: "Link / mã mời" })).toBeTruthy();
  expect(api.mock.calls.some(([path, method]) => path === "/api/teams/team/invites" && method === "POST")).toBe(false);
});

it("saves a new deadline directly and moves the task back to the claim pool", async () => {
  const leaderTeam = { ...team, role: "Leader" };
  const now = Date.parse("2026-11-17T04:00:00Z");
  vi.spyOn(Date, "now").mockReturnValue(now);
  let task = { ...shared, deadline: "2026-11-16T04:00:00Z" };
  api.mockImplementation(async (path, method, body) => {
    if (path === "/api/teams") return [leaderTeam];
    if (path === "/api/teams/team") return { team: leaderTeam, members: [], tasks: [task], activity: [] };
    if (path === "/api/teams/tasks/shared/deadline" && method === "PUT") task = { ...task, deadline: body.deadline };
    return {};
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Gia hạn" }));
  const dialog = within(screen.getByRole("dialog", { name: "Gia hạn công việc" }));
  fireEvent.change(dialog.getByLabelText(/Hạn chót mới/), { target: { value: "2026-11-20T16:00" } });
  fireEvent.click(dialog.getByRole("button", { name: "Lưu hạn mới" }));
  await waitFor(() => {
    expect(api).toHaveBeenCalledWith("/api/teams/tasks/shared/deadline", "PUT", {
      version: shared.version,
      deadline: "2026-11-20T09:00:00.000Z",
    });
    expect(within(screen.getByRole("region", { name: "Có thể nhận" })).getByRole("article", { name: shared.name })).toBeTruthy();
  });
});

it("shows private options and enables confirmation while NORA prepares its explanation", async () => {
  state.noraAvailable = true;
  let explain;
  let signal;
  api.mockImplementation((path, _method, _body, _retry, requestSignal) => {
    signal = requestSignal;
    return new Promise((resolve) => { explain = resolve; });
  });
  const onConfirm = vi.fn();
  const { unmount } = render(<PrivateClaimPreview claim={proposal} onConfirm={onConfirm} />);
  expect(await screen.findByText("NORA đang chuẩn bị giải thích…")).toBeTruthy();
  expect(api.mock.calls[0][0]).toBe("/api/teams/claims/proposal/explanation");
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận nhận việc · Phương án 1" }));
  expect(onConfirm).toHaveBeenCalledWith(0);

  explain({ explanation: "Phương án này dùng một phiên và giữ các phiên khác." });
  expect(await screen.findByText("Giải thích của NORA")).toBeTruthy();
  unmount();
  expect(signal.aborted).toBe(true);
});
it("filters the team directory by role and unaccented name, and keeps the filters on return", async () => {
  const leader = { id: "project", name: "Đồ án phần mềm", role: "Leader" };
  routes({
    "/api/teams": () => [team, leader],
    "/api/teams/project": () => ({
      team: leader,
      members: [],
      tasks: [],
      activity: [],
    }),
  });
  render(<TeamsPage />);
  await screen.findByRole("button", { name: team.name });
  fireEvent.change(screen.getByRole("textbox", { name: "Tìm nhóm theo tên" }), {
    target: { value: "do an" },
  });
  fireEvent.change(
    screen.getByRole("combobox", { name: "Lọc nhóm theo vai trò" }),
    {
      target: { value: "Member" },
    },
  );
  expect(screen.getByText("Không tìm thấy nhóm phù hợp")).toBeTruthy();
  fireEvent.change(
    screen.getByRole("combobox", { name: "Lọc nhóm theo vai trò" }),
    {
      target: { value: "Leader" },
    },
  );
  fireEvent.click(screen.getByRole("button", { name: leader.name }));
  await screen.findByRole("heading", { name: leader.name });
  fireEvent.click(screen.getByRole("button", { name: "Tất cả nhóm" }));
  expect(screen.getByRole("button", { name: leader.name })).toBeTruthy();
  expect(screen.queryByRole("button", { name: team.name })).toBeNull();
  fireEvent.change(screen.getByRole("textbox", { name: "Tìm nhóm theo tên" }), {
    target: { value: "không khớp" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Xóa bộ lọc" }));
  expect(screen.getByRole("button", { name: team.name })).toBeTruthy();
});
it("summarizes active deadlines and opens the same task again after closing it", async () => {
  const due = new Date(Date.now() + 86400000).toISOString();
  routes({
    "/api/teams/team": () => ({
      team,
      members: [],
      activity: [],
      tasks: [
        { ...shared, deadline: due },
        {
          ...shared,
          id: "done",
          name: "Đã nộp",
          status: "Completed",
          deadline: due,
        },
        { ...shared, id: "no-date", name: "Chưa có hạn", deadline: null },
      ],
    }),
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  const overview = await screen.findByRole("button", { name: "Tổng quan" });
  fireEvent.click(overview);
  expect(await screen.findByText("1/3 công việc hoàn thành")).toBeTruthy();
  const deadlines = screen.getByRole("region", { name: "Hạn chót" });
  expect(within(deadlines).getByText("1 trong 7 ngày tới")).toBeTruthy();
  const open = within(deadlines).getByRole("button", { name: /Báo cáo chung/ });
  fireEvent.click(open);
  const dialog = await screen.findByRole("dialog", { name: shared.name });
  fireEvent.click(within(dialog).getByRole("button", { name: "Đóng cửa sổ" }));
  expect(screen.queryByRole("dialog", { name: shared.name })).toBeNull();
  fireEvent.click(overview);
  fireEvent.click(within(screen.getByRole("region", { name: "Hạn chót" })).getByRole("button", { name: /Báo cáo chung/ }));
  expect(await screen.findByRole("dialog", { name: shared.name })).toBeTruthy();
});

it("opens the matching work from each overview count and replaces an old board filter", async () => {
  const now = Date.parse("2026-11-17T09:00:00Z");
  vi.spyOn(Date, "now").mockReturnValue(now);
  const leader = { ...team, role: "Leader" };
  const task = (id, name, status, hours, extra = {}) => ({
    ...shared, id, name, status,
    deadline: hours === null ? null : new Date(now + hours * 3_600_000).toISOString(),
    ...extra,
  });
  routes({
    "/api/teams": () => [leader],
    "/api/teams/team": () => ({
      team: leader, members: [], activity: [], tasks: [
        task("overdue", "Việc đã quá hạn", "Assigned", 0, { assigneeId: "other" }),
        task("review", "Bản nộp cần duyệt", "PendingReview", -24, { assigneeId: "other" }),
        task("near", "Gói việc chờ xác nhận", "Open", 48, { designatedUserId: "other" }),
        task("later", "Việc sau hai ngày", "Open", 49),
        task("undated", "Việc chưa có hạn", "Open", null),
        task("claimed", "Việc đã có người làm", "Assigned", 24, { assigneeId: "other" }),
        task("done", "Việc đã hoàn thành", "Completed", -24),
      ],
    }),
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  const search = await screen.findByRole("textbox", { name: "Tìm task trong nhóm" });
  fireEvent.change(search, { target: { value: "Tìm không ra việc" } });
  const overview = screen.getByRole("button", { name: "Tổng quan" });
  for (const [metric, expected] of [
    ["Việc quá hạn", "Việc đã quá hạn"],
    ["Báo cáo chờ duyệt", "Bản nộp cần duyệt"],
    ["Sắp đến hạn, chưa nhận", "Gói việc chờ xác nhận"],
  ]) {
    fireEvent.click(overview);
    const summary = screen.getByRole("region", { name: "Việc cần xử lý" });
    fireEvent.click(within(summary).getByRole("button", { name: new RegExp(`^1 ${metric}`, "i") }));
    const board = screen.getByRole("region", { name: "Công việc nhóm" });
    expect(within(board).getByText(metric)).toBeTruthy();
    expect(within(board).getAllByRole("article").map((row) => row.getAttribute("aria-label"))).toEqual([expected]);
    expect(within(board).getByRole("textbox", { name: "Tìm task trong nhóm" }).value).toBe("");
    expect(document.activeElement).toBe(within(board).getByRole("heading", { name: /Bảng công việc/ }));
  }
  fireEvent.click(screen.getByRole("button", { name: "Bỏ lọc" }));
  expect(screen.getAllByRole("article")).toHaveLength(3);
});

it("shows a single create action for an empty group and keeps an unsent chat message when switching views", async () => {
  const leader = { ...team, role: "Leader" };
  routes({
    "/api/teams": () => [leader],
    "/api/teams/team": () => ({ team: leader, members: [], tasks: [], activity: [] }),
    "/api/teams/team/messages": () => ({ items: [] }),
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  expect(await screen.findByRole("heading", { name: "Nhóm chưa có công việc" })).toBeTruthy();
  expect(screen.getAllByRole("button", { name: "Đăng việc mới" })).toHaveLength(1);
  expect(screen.queryByRole("textbox", { name: "Tìm task trong nhóm" })).toBeNull();
  expect(api.mock.calls.some(([path]) => path.endsWith("/messages"))).toBe(false);

  fireEvent.click(screen.getByRole("button", { name: "Trao đổi" }));
  const message = screen.getByRole("textbox", { name: "Tin nhắn cho nhóm" });
  fireEvent.change(message, { target: { value: "Nội dung chưa gửi" } });
  fireEvent.click(screen.getByRole("button", { name: "Công việc" }));
  expect(screen.queryByRole("textbox", { name: "Tin nhắn cho nhóm" })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Trao đổi" }));
  expect(screen.getByRole("textbox", { name: "Tin nhắn cho nhóm" }).value).toBe("Nội dung chưa gửi");
});
it("members cannot create tasks and viewing a private proposal never claims automatically", async () => {
  routes();
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Có thể nhận" }));
  fireEvent.click(
    await screen.findByRole("button", { name: "Xem lịch trước khi nhận" }),
  );
  expect(screen.queryByRole("button", { name: "Đăng việc mới" })).toBeNull();
  expect(
    await screen.findByRole("region", { name: "Phương án riêng tư" }),
  ).toBeTruthy();
  expect(screen.getByText("Việc riêng tư")).toBeTruthy();
  expect(api.mock.calls.some(([p]) => p.includes("/confirm"))).toBe(false);
  fireEvent.click(screen.getByRole("button", { name: "Hủy xem phương án" }));
  await waitFor(() =>
    expect(
      screen.queryByRole("region", { name: "Phương án riêng tư" }),
    ).toBeNull(),
  );
  expect(api.mock.calls.some(([p]) => p.includes("/confirm"))).toBe(false);
});
it("sends an availability response without claiming or sharing a schedule", async () => {
  routes({
    "/api/teams/team": () => ({ team, members: [], activity: [], tasks: [{
      ...shared, availabilityCheck: { userId: state.workspace.profile.id, canTake: null },
    }] }),
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Kiểm tra lịch & phản hồi" }));
  fireEvent.click(await screen.findByRole("button", { name: "Báo có thể nhận" }));
  await waitFor(() => expect(api).toHaveBeenCalledWith(
    "/api/teams/tasks/shared/availability/respond", "POST", { version: "version", canTake: true },
  ));
  await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  expect(api.mock.calls.some(([path]) => path.endsWith("/confirm"))).toBe(false);
});

it("only the explicit confirmation applies the selected plan and offers undo", async () => {
  routes();
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Có thể nhận" }));
  fireEvent.click(
    await screen.findByRole("button", {
      name: "Xem lịch trước khi nhận",
    }),
  );
  fireEvent.click(
    await screen.findByRole("button", { name: "Xác nhận nhận việc · Phương án 1" }),
  );
  expect(
    await screen.findByRole("button", { name: "Hoàn tác nhận việc" }),
  ).toBeTruthy();
  expect(api).toHaveBeenCalledWith(
    "/api/teams/claims/proposal/confirm",
    "POST",
    { option: 0 },
  );
});
it("member completion requires a confirmation and sends the task for leader review", async () => {
  routes({
    "/api/teams/team": () => ({
      team,
      members: [],
      tasks: [
        {
          ...shared,
          status: "Assigned",
          assigneeId: state.workspace.profile.id,
        },
      ],
      activity: [],
    }),
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(
    await screen.findByRole("button", { name: "Báo hoàn thành · Gửi duyệt" }),
  );
  expect(api.mock.calls.some(([p]) => p.endsWith("/progress"))).toBe(false);
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận & gửi duyệt" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/teams/tasks/shared/completion/submit",
      "POST",
      {
        version: "version",
        requestId: expect.any(String),
        note: "",
        files: [],
      },
    ),
  );
});
it("only leader sees review controls and can request changes with a note", async () => {
  const leaderTeam = { ...team, role: "Leader" };
  routes({
    "/api/teams/team": () => ({
      team: leaderTeam,
      members: [{ userId: "assignee", name: "An", role: "Member" }],
      tasks: [
        {
          ...shared,
          assigneeId: "assignee",
          status: "PendingReview",
          progress: 100,
          completionRequestId: "submission",
        },
      ],
      activity: [],
    }),
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Cần xử lý" }));
  fireEvent.click(
    await screen.findByRole("button", { name: "Kiểm tra & duyệt" }),
  );
  fireEvent.change(screen.getByRole("textbox", { name: /Nhận xét/ }), {
    target: { value: "Bổ sung tài liệu" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Yêu cầu bổ sung" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/teams/tasks/shared/completion/review",
      "POST",
      {
        version: "version",
        requestId: "submission",
        approved: false,
        note: "Bổ sung tài liệu",
      },
    ),
  );
});
it("a member sees pending approval without leader controls or task creation", async () => {
  routes({
    "/api/teams/team": () => ({
      team,
      members: [],
      tasks: [
        {
          ...shared,
          status: "PendingReview",
          assigneeId: state.workspace.profile.id,
          progress: 100,
        },
      ],
      activity: [],
    }),
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  expect(await screen.findByText("Chờ trưởng nhóm xác nhận")).toBeTruthy();
  expect(screen.queryByRole("button", { name: "Kiểm tra & duyệt" })).toBeNull();
  expect(screen.queryByRole("button", { name: "Đăng việc mới" })).toBeNull();
  expect(
    screen.queryByRole("button", { name: "Báo hoàn thành · Gửi duyệt" }),
  ).toBeNull();
});
it("a stale confirmation leaves the private proposal visible and shows a useful error", async () => {
  routes({
    "/api/teams/claims/proposal/confirm": () => {
      throw new Error("Lịch đã thay đổi. Hãy xem lại.");
    },
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Có thể nhận" }));
  fireEvent.click(
    await screen.findByRole("button", {
      name: "Xem lịch trước khi nhận",
    }),
  );
  fireEvent.click(
    await screen.findByRole("button", { name: "Xác nhận nhận việc · Phương án 1" }),
  );
  expect(await screen.findByRole("alert")).toHaveProperty(
    "textContent",
    "Lịch đã thay đổi. Hãy xem lại.",
  );
  expect(
    screen.getByRole("region", { name: "Phương án riêng tư" }),
  ).toBeTruthy();
  expect(screen.queryByText("Đã nhận việc và cập nhật lịch.")).toBeNull();
});
it("returning a task first shows the affected private sessions and requires another click", async () => {
  const mine = {
    ...shared,
    assigneeId: state.workspace.profile.id,
    status: "Assigned",
  };
  routes({
    "/api/teams/team": () => ({
      team,
      members: [],
      tasks: [mine],
      activity: [],
    }),
    "/api/teams/tasks/shared/return/preview": () => ({
      taskId: "shared",
      version: "v",
      revision: "r",
      sessions: option.after,
    }),
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: mine.name }));
  fireEvent.click(
    await screen.findByRole("button", { name: "Trả việc về nhóm" }),
  );
  expect(
    await screen.findByRole("region", { name: "Xác nhận trả việc" }),
  ).toBeTruthy();
  expect(api.mock.calls.some(([p]) => p.endsWith("/return"))).toBe(false);
  fireEvent.click(
    screen.getByRole("button", {
      name: "Xác nhận trả việc và hủy các phiên trên",
    }),
  );
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/teams/tasks/shared/return", "POST", {
      version: "v",
      revision: "r",
    }),
  );
});
it("policy preview does not create actual hours before approval and has no numeric setup fields", async () => {
  api.mockResolvedValue({
    id: "policy",
    policy: {
      awakeStart: 420,
      awakeEnd: 1350,
      lunchStart: 705,
      lunchEnd: 765,
      dinnerStart: 1080,
      dinnerEnd: 1140,
      commuteMinutes: 30,
      focusMinutes: 45,
    },
    before: [],
    after: [],
    warnings: [],
  });
  state.workspace.events = [{ id: "event" }];
  render(<AvailabilityPolicyCard />);
  fireEvent.click(
    screen.getByRole("button", { name: "Xem đề xuất giờ sinh hoạt" }),
  );
  expect(
    await screen.findByRole("button", { name: "Xác nhận chính sách và lịch" }),
  ).toBeTruthy();
  expect(api.mock.calls.some(([p]) => p.includes("/confirm"))).toBe(false);
  expect(
    document.querySelector("input[type='number'], input[type='time']"),
  ).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Hủy đề xuất" }));
  expect(api.mock.calls.some(([p]) => p.includes("/confirm"))).toBe(false);
});
it("automatically proposes meals, sleep and free time without asking for work-hour input", async () => {
  api.mockResolvedValue({
    id: "routine",
    policy: {
      awakeStart: 420,
      awakeEnd: 1380,
      lunchStart: 720,
      lunchEnd: 750,
      dinnerStart: 1080,
      dinnerEnd: 1110,
      commuteMinutes: 30,
      focusMinutes: 45,
    },
    before: [],
    after: [],
    warnings: [],
    days: [
      {
        date: "2026-11-02",
        calendarGapMinutes: 1100,
        schedulableMinutes: 400,
        workGaps: [
          { start: "2026-11-02T05:45:00Z", end: "2026-11-02T08:00:00Z" },
        ],
        focusSlots: [],
        routine: {
          awakeStart: 360,
          awakeEnd: 1320,
          lunchStart: 735,
          lunchEnd: 765,
          dinnerStart: 1080,
          dinnerEnd: 1110,
        },
        timeline: [
          {
            kind: "MEAL",
            label: "Ăn trưa · 30 phút",
            start: "2026-11-02T05:15:00Z",
            end: "2026-11-02T05:45:00Z",
          },
        ],
      },
    ],
  });
  state.workspace.events = [{ id: "class" }];
  render(<AvailabilityPolicyCard autoPreview />);
  expect(
    await screen.findByRole("button", { name: "Xác nhận chính sách và lịch" }),
  ).toBeTruthy();
  expect(api).toHaveBeenCalledWith("/api/time/policy/preview", "POST");
  expect(screen.getByText(/Thức dậy/)).toBeTruthy();
  expect(screen.getByText(/Thời Gian Làm Việc sau khi trừ/)).toBeTruthy();
  expect(
    screen.queryByRole("button", { name: "Xem đề xuất giờ sinh hoạt" }),
  ).toBeNull();
  expect(api.mock.calls.some(([p]) => p.includes("/confirm"))).toBe(false);
});

it("anchors the automatic proposal to the uploaded timetable and refreshes when another timetable is sent", async () => {
  api.mockResolvedValue({
    id: "week-proposal",
    policy: {},
    before: [],
    after: [],
    warnings: [],
    days: [],
    weekStart: "2026-11-09",
    weekEnd: "2026-11-15",
  });
  const { rerender } = render(
    <AvailabilityPolicyCard autoPreview timetableTurnId="first-upload" />,
  );
  await screen.findByText(/Đề xuất chỉ áp dụng trong tuần này/);
  expect(api).toHaveBeenCalledWith(
    "/api/time/policy/preview?timetableTurnId=first-upload",
    "POST",
  );
  rerender(
    <AvailabilityPolicyCard autoPreview timetableTurnId="second-upload" />,
  );
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/time/policy/preview?timetableTurnId=second-upload",
      "POST",
    ),
  );
  expect(api.mock.calls.some(([url]) => url.includes("/confirm"))).toBe(false);
});
it("does not render a confirmation button for infeasible private options", () => {
  render(
    <PrivateClaimPreview
      claim={{
        ...proposal,
        options: [],
        warnings: ["Chưa đủ thời gian trước deadline."],
      }}
      busy={false}
      onConfirm={vi.fn()}
      onCancel={vi.fn()}
    />,
  );
  expect(screen.getByRole("status").textContent).toContain("deadline");
  expect(
    screen.queryByRole("button", { name: /Xác nhận nhận việc/ }),
  ).toBeNull();
});
it("explains real blocking constraints instead of a generic insufficient-capacity message", () => {
  render(
    <PrivateClaimPreview
      claim={{
        ...proposal,
        options: [],
        warnings: [],
        analysis: "Chưa xếp được đủ 180 phút trước hạn.",
        failure: {
          code: "INSUFFICIENT_TIME_BEFORE_DEADLINE",
          requiredMinutes: 180,
          capacityMinutes: 120,
          missingMinutes: 60,
          needsAvailability: false,
          blockers: ["Mỗi phiên tối đa 90 phút, nghỉ 10 phút giữa các phiên."],
        },
      }}
      busy={false}
      onCancel={vi.fn()}
      onConfirm={vi.fn()}
      onDiscuss={vi.fn()}
    />,
  );
  expect(screen.getByText("Vì sao chưa xếp được task?")).toBeTruthy();
  expect(screen.getByText("60 phút")).toBeTruthy();
  expect(screen.getByText(/Mỗi phiên tối đa 90 phút/)).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Nhờ NORA phân tích điều kiện" }),
  ).toBeTruthy();
});
it("directs a member to working hours without automatically claiming", async () => {
  let configured = false;
  routes({
    "/api/teams/tasks/shared/preview": () =>
      configured
        ? proposal
        : {
            ...proposal,
            options: [],
            warnings: [],
            analysis: "Chưa xác nhận giờ làm việc.",
            failure: {
              code: "WORK_HOURS_NOT_CONFIRMED",
              requiredMinutes: 60,
              needsAvailability: true,
              blockers: ["Xác nhận khung sinh hoạt trước khi xếp task."],
            },
          },
    "/api/time/policy/preview": () => ({
      id: "policy",
      policy: {
        awakeStart: 420,
        awakeEnd: 1350,
        lunchStart: 705,
        lunchEnd: 765,
        dinnerStart: 1080,
        dinnerEnd: 1140,
        commuteMinutes: 30,
        focusMinutes: 45,
      },
      before: [],
      after: [],
      warnings: [],
      days: [],
    }),
    "/api/time/policy/policy/confirm": () => {
      configured = true;
      return {};
    },
    "/api/time/policy/slots": () => [],
  });
  render(<TeamsPage onWorkspaceChanged={vi.fn()} />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Có thể nhận" }));
  fireEvent.click(
    await screen.findByRole("button", { name: "Xem lịch trước khi nhận" }),
  );
  expect(
    await screen.findByText("Còn một bước: xác nhận giờ có thể làm việc"),
  ).toBeTruthy();
  expect(screen.queryByText(/Cho phép thử dời phiên/)).toBeNull();
  expect(
    screen.getByRole("link", { name: "Mở Giờ làm việc" }).getAttribute("href"),
  ).toBe("#time");
  expect(api.mock.calls.some(([p]) => p === "/api/time/policy/preview")).toBe(
    false,
  );
  expect(
    api.mock.calls.some(([p]) => p === "/api/teams/claims/proposal/confirm"),
  ).toBe(false);
});
it("opens the NORA popup immediately while the personal schedule is being analyzed", async () => {
  let finish;
  routes({
    "/api/teams/tasks/shared/preview": () =>
      new Promise((resolve) => {
        finish = resolve;
      }),
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Có thể nhận" }));
  fireEvent.click(
    await screen.findByRole("button", { name: "Xem lịch trước khi nhận" }),
  );
  expect(
    await screen.findByRole("dialog", {
      name: "Xem lịch trước khi nhận việc",
    }),
  ).toBeTruthy();
  expect(screen.getByText(/NORA đang phân tích công việc/)).toBeTruthy();
  expect(
    screen.queryByRole("button", { name: "Xác nhận nhận việc · Phương án 1" }),
  ).toBeNull();
  finish(proposal);
  expect(
    await screen.findByRole("button", { name: "Xác nhận nhận việc · Phương án 1" }),
  ).toBeTruthy();
});
