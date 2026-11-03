import { beforeEach, expect, it, vi } from "vitest";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { TeamsPage } from "../src/teams.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
import {
  clearTeamInvite,
  pendingTeamInvite,
  teamInviteLink,
} from "../src/team-invite-link.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";
const code = "CD".repeat(20);
const group = { id: "team", name: "Nhóm SSG105", role: "Leader" };
const invite = { id: "invite", code, expiresAt: "2026-11-08T14:00:00Z" };
beforeEach(() => {
  vi.resetAllMocks();
  clearTeamInvite();
  history.replaceState(null, "", "/app/#teams");
  state.workspace = workspace();
  Object.defineProperty(navigator, "clipboard", {
    configurable: true,
    value: { writeText: vi.fn().mockResolvedValue(undefined) },
  });
  api.mockImplementation(async (path) => {
    if (path === "/api/teams") return [group];
    if (path === "/api/teams/team")
      return { team: group, members: [], tasks: [], activity: [] };
    if (path === "/api/teams/team/invites") return invite;
    if (path === `/api/teams/invites/${code}`)
      return {
        teamName: group.name,
        memberCount: 3,
        expiresAt: invite.expiresAt,
        alreadyMember: false,
      };
    if (path === "/api/teams/join") return { id: "team", status: "Pending" };
    if (path === "/api/teams/join-requests") return [];
    return {};
  });
});
it("leader can create and copy a join link from the group hub", async () => {
  render(<TeamsPage />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Tạo link mời", exact: true }),
  );
  const picker = await screen.findByRole("dialog", {
    name: "Tạo link mời vào nhóm",
  });
  expect(within(picker).getByRole("combobox").value).toBe("team");
  fireEvent.click(within(picker).getByRole("button", { name: "Tạo link mời" }));
  const dialog = await screen.findByRole("dialog", {
    name: "Mời thành viên vào nhóm",
  });
  expect(
    within(dialog).getByRole("textbox", { name: "Link tham gia nhóm" }).value,
  ).toBe(teamInviteLink(code));
  fireEvent.click(
    within(dialog).getByRole("button", { name: "Sao chép link mời" }),
  );
  await waitFor(() =>
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
      teamInviteLink(code),
    ),
  );
  expect(
    await screen.findByRole("button", { name: "Đã sao chép link" }),
  ).toBeTruthy();
});
it("opening a link previews the group and requests approval only after explicit confirmation", async () => {
  history.replaceState(null, "", `/app/#teams?join=${code}`);
  render(<TeamsPage />);
  const dialog = await screen.findByRole("dialog", {
    name: "Lời mời tham gia nhóm",
  });
  expect(
    await within(dialog).findByRole("heading", { name: group.name }),
  ).toBeTruthy();
  expect(api.mock.calls.some(([, method]) => method === "POST")).toBe(false);
  fireEvent.click(
    within(dialog).getByRole("button", { name: "Gửi yêu cầu tham gia" }),
  );
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/teams/join", "POST", { text: code }),
  );
  await waitFor(() =>
    expect(
      screen.queryByRole("dialog", { name: "Lời mời tham gia nhóm" }),
    ).toBeNull(),
  );
  expect(pendingTeamInvite()).toBeNull();
  expect(api.mock.calls.some(([path]) => path === "/api/teams/team")).toBe(false);
  expect(screen.getByText(/Đã gửi yêu cầu/)).toBeTruthy();
});
it("invalid or revoked invitations show a useful error and never auto join", async () => {
  history.replaceState(null, "", `/app/#teams?join=${code}`);
  api.mockImplementation(async (path) => {
    if (path === "/api/teams") return [];
    throw new Error("Link mời đã bị thu hồi. Hãy xin link mới.");
  });
  render(<TeamsPage />);
  expect(await screen.findByRole("alert")).toHaveProperty(
    "textContent",
    "Link mời đã bị thu hồi. Hãy xin link mới.",
  );
  expect(
    screen.queryByRole("button", { name: "Gửi yêu cầu tham gia" }),
  ).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Đóng lời mời" }));
  expect(pendingTeamInvite()).toBeNull();
  expect(api.mock.calls.some(([, method]) => method === "POST")).toBe(false);
});
it("the normal join form also accepts the full link", async () => {
  render(<TeamsPage />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Tham gia bằng link hoặc mã" }),
  );
  const dialog = await screen.findByRole("dialog", { name: "Tham gia nhóm" });
  fireEvent.change(
    within(dialog).getByRole("textbox", { name: "Mã mời hoặc link tham gia" }),
    { target: { value: teamInviteLink(code) } },
  );
  fireEvent.click(
    within(dialog).getByRole("button", { name: "Gửi yêu cầu tham gia", exact: true }),
  );
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/teams/join", "POST", { text: code }),
  );
});
