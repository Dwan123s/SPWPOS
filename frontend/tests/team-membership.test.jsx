import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { TeamsPage } from "../src/teams.jsx";
import { TeamJoinRequests } from "../src/team-membership.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
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
  window.history.replaceState({}, "", "/");
  sessionStorage.clear();
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
