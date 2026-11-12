import { beforeEach, expect, it, vi } from "vitest";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { StrictMode } from "react";
import { TeamsPage } from "../src/teams.jsx";
import { TeamJoinRequests } from "../src/team-membership.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
import { NotificationCenter } from "../src/notifications.jsx";
import {
  pendingTeamJoinReview,
  finishTeamJoinReview,
  requestTeamJoinReview,
} from "../src/team-notification-link.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";
const team = { id: "team", name: "SSG", role: "Leader" };
const applicant = {
  userId: "applicant",
  name: "Ngọc",
  email: "ngoc@example.com",
  version: "request-version",
};
beforeEach(() => {
  vi.resetAllMocks();
  state.workspace = workspace();
  finishTeamJoinReview(pendingTeamJoinReview(state.workspace.profile.id));
  window.history.replaceState({}, "", "/");
  sessionStorage.clear();
});

it("opens the notification's group and focuses approvals after navigation, including StrictMode", async () => {
  state.workspace.notifications = [
    {
      id: "join",
      kind: "TEAM_JOIN_REQUEST",
      teamId: "requested-team",
      title: "Có yêu cầu tham gia nhóm",
      message: "Có yêu cầu mới.",
      createdAt: "2026-11-03T01:26:00Z",
      isRead: false,
    },
  ];
  api.mockImplementation(async (path) => {
    if (path === "/api/teams")
      return [team, { ...team, id: "requested-team", name: "Nhóm đích" }];
    if (path === "/api/teams/join-requests") return [];
    if (path === "/api/teams/requested-team")
      return {
        team: { ...team, id: "requested-team", name: "Nhóm đích" },
        members: [],
        tasks: [],
        activity: [],
        joinRequests: [applicant],
      };
    return {};
  });
  const view = render(<NotificationCenter workspace={state.workspace} />);
  const link = screen.getByRole("link", { name: "Mở nhóm để duyệt" });
  expect(link.getAttribute("href")).toBe("#teams");
  fireEvent.click(link);
  view.rerender(
    <StrictMode>
      <TeamsPage />
    </StrictMode>,
  );
  expect(
    await screen.findByRole("button", { name: "Duyệt Ngọc" }),
  ).toBeTruthy();
  await waitFor(() =>
    expect(document.activeElement).toBe(
      screen.getByRole("region", { name: "Duyệt yêu cầu tham gia" }),
    ),
  );
  expect(api.mock.calls.some(([path]) => path === "/api/teams/team")).toBe(
    false,
  );
  expect(api.mock.calls.some(([, method]) => method === "POST")).toBe(false);
  expect(state.workspace.notifications[0].isRead).toBe(false);
});

it("handles a review link while the groups screen is already mounted and shows access errors", async () => {
  api.mockImplementation(async (path) => {
    if (path === "/api/teams") return [team];
    if (path === "/api/teams/join-requests") return [];
    if (path === "/api/teams/unavailable")
      throw new Error("Bạn không còn quyền truy cập nhóm này.");
    if (path === "/api/teams/team")
      return {
        team,
        members: [],
        tasks: [],
        activity: [],
        joinRequests: [applicant],
      };
    return {};
  });
  render(<TeamsPage />);
  await screen.findByRole("button", { name: "SSG" });
  act(() => requestTeamJoinReview("unavailable"));
  expect(await screen.findByRole("alert")).toHaveProperty(
    "textContent",
    "Bạn không còn quyền truy cập nhóm này.",
  );
  act(() => requestTeamJoinReview("team"));
  expect(
    await screen.findByRole("button", { name: "Duyệt Ngọc" }),
  ).toBeTruthy();
  expect(screen.queryByRole("alert")).toBeNull();
  await waitFor(() =>
    expect(document.activeElement).toBe(
      screen.getByRole("region", { name: "Duyệt yêu cầu tham gia" }),
    ),
  );
});

it("submits a pending request without opening private group data", async () => {
  let sent = false;
  api.mockImplementation(async (path, method) => {
    if (path === "/api/teams") return [];
    if (path === "/api/teams/join-requests")
      return sent
        ? [{ teamId: "team", teamName: "SSG", status: "Pending" }]
        : [];
    if (path === "/api/teams/join" && method === "POST") {
      sent = true;
      return { id: "team", status: "Pending" };
    }
    return {};
  });
  render(<TeamsPage />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Tham gia bằng link hoặc mã" }),
  );
  fireEvent.change(
    screen.getByRole("textbox", { name: "Mã mời hoặc link tham gia" }),
    { target: { value: "A".repeat(40) } },
  );
  fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu tham gia" }));
  expect(
    await screen.findByText("Chờ trưởng nhóm duyệt", { exact: false }),
  ).toBeTruthy();
  expect(api.mock.calls.some(([path]) => path === "/api/teams/team")).toBe(
    false,
  );
});

it("only leaders can see applicant identity and approval buttons", () => {
  const onReview = vi.fn();
  const view = render(
    <TeamJoinRequests
      detail={{ team: { ...team, role: "Member" }, joinRequests: [applicant] }}
      onReview={onReview}
    />,
  );
  expect(screen.queryByText(applicant.email)).toBeNull();
  view.rerender(
    <TeamJoinRequests
      detail={{ team, joinRequests: [applicant] }}
      onReview={onReview}
    />,
  );
  expect(screen.getByText(applicant.email)).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Duyệt Ngọc" }));
  expect(onReview).toHaveBeenCalledWith(applicant, true);
  fireEvent.click(screen.getByRole("button", { name: "Từ chối Ngọc" }));
  expect(onReview).toHaveBeenCalledWith(applicant, false);
});

it("leader approves through the API and kick requires explicit confirmation", async () => {
  let pending = true;
  api.mockImplementation(async (path, method, body) => {
    if (path === "/api/teams") return [team];
    if (path === "/api/teams/join-requests") return [];
    if (path === "/api/teams/team")
      return {
        team,
        members: [
          {
            userId: "member",
            name: "Minh",
            role: "Member",
            version: "member-version",
          },
        ],
        tasks: [],
        activity: [],
        joinRequests: pending ? [applicant] : [],
      };
    if (path === "/api/teams/team/join-requests/applicant/review") {
      expect(body).toEqual({ version: "request-version", approved: true });
      pending = false;
    }
    return {};
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: "SSG" }));
  fireEvent.click(await screen.findByRole("button", { name: "Duyệt Ngọc" }));
  await waitFor(() =>
    expect(screen.queryByRole("button", { name: "Duyệt Ngọc" })).toBeNull(),
  );
  fireEvent.click(screen.getByRole("button", { name: "Loại khỏi nhóm" }));
  expect(
    await screen.findByRole("dialog", { name: "Loại thành viên khỏi nhóm" }),
  ).toBeTruthy();
  expect(api.mock.calls.some(([path]) => path.endsWith("/remove"))).toBe(false);
  fireEvent.click(
    screen.getByRole("button", { name: "Xác nhận loại thành viên" }),
  );
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/teams/team/members/member/remove",
      "POST",
      { version: "member-version" },
    ),
  );
});
