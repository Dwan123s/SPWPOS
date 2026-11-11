import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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
  deadline: "2026-11-04T12:00:00Z",
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
it("members cannot create tasks and viewing a private proposal never claims automatically", async () => {
  routes();
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(
    await screen.findByRole("button", { name: "Nhận task · NORA xếp lịch" }),
  );
  expect(screen.queryByRole("button", { name: "Giao task mới" })).toBeNull();
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
it("only the explicit confirmation applies the selected plan and offers undo", async () => {
  routes();
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(
    await screen.findByRole("button", {
      name: "Nhận task · NORA xếp lịch",
    }),
  );
  fireEvent.click(
    await screen.findByRole("button", { name: "Xác nhận phương án 1" }),
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
  expect(screen.queryByRole("button", { name: "Giao task mới" })).toBeNull();
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
  fireEvent.click(
    await screen.findByRole("button", {
      name: "Nhận task · NORA xếp lịch",
    }),
  );
  fireEvent.click(
    await screen.findByRole("button", { name: "Xác nhận phương án 1" }),
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
  fireEvent.click(await screen.findByRole("button", { name: "Chi tiết" }));
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
  expect(screen.getByText(/Giờ rảnh sau khi trừ/)).toBeTruthy();
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
    screen.queryByRole("button", { name: /Xác nhận phương án/ }),
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
  fireEvent.click(
    await screen.findByRole("button", { name: "Nhận task · NORA xếp lịch" }),
  );
  expect(
    await screen.findByText("Còn một bước: xác nhận giờ có thể làm việc"),
  ).toBeTruthy();
  expect(screen.queryByText(/Cho phép thử dời phiên/)).toBeNull();
  expect(screen.getByRole("link", { name: "Mở Giờ rảnh" }).getAttribute("href")).toBe("#time");
  expect(api.mock.calls.some(([p]) => p === "/api/time/policy/preview")).toBe(false);
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
  fireEvent.click(
    await screen.findByRole("button", { name: "Nhận task · NORA xếp lịch" }),
  );
  expect(
    await screen.findByRole("dialog", {
      name: "NORA · Nhận task và xếp lịch riêng",
    }),
  ).toBeTruthy();
  expect(screen.getByText(/NORA đang phân tích công việc/)).toBeTruthy();
  expect(
    screen.queryByRole("button", { name: "Xác nhận phương án 1" }),
  ).toBeNull();
  finish(proposal);
  expect(
    await screen.findByRole("button", { name: "Xác nhận phương án 1" }),
  ).toBeTruthy();
});
