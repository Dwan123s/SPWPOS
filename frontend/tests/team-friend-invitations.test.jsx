import { beforeEach, expect, it, vi } from "vitest";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import {
  TeamInviteDialog,
  ReceivedTeamInvitations,
} from "../src/team-friend-invitations.jsx";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";

const team = { id: "team", name: "SWP391" };
const friend = {
  userId: "friend",
  name: "Minh Anh",
  avatarUrl: "",
  status: "Available",
};
const invitation = {
  id: "invitation",
  teamId: "team",
  teamName: "SWP391",
  senderName: "Reiko",
  version: "v1",
  expiresAt: "2026-11-25T00:00:00Z",
};
beforeEach(() => vi.resetAllMocks());

it("shows friends first, sends an invitation once, and can revoke it", async () => {
  api.mockImplementation(async (path, method) => {
    if (path.includes("invite-friends"))
      return {
        items: [
          friend,
          { ...friend, userId: "member", name: "Thanh", status: "Member" },
        ],
        nextOffset: null,
      };
    if (method === "POST" && path.endsWith("friend-invitations"))
      return {
        ...friend,
        status: "Invited",
        invitationId: "invitation",
        version: "v1",
      };
    return {};
  });
  render(<TeamInviteDialog team={team} onClose={vi.fn()} />);
  expect(
    screen
      .getByRole("tab", { name: "Mời bạn bè" })
      .getAttribute("aria-selected"),
  ).toBe("true");
  fireEvent.click(await screen.findByRole("button", { name: "Mời Minh Anh" }));
  expect(
    await screen.findByRole("button", { name: "Thu hồi lời mời Minh Anh" }),
  ).toBeTruthy();
  expect(api).toHaveBeenCalledWith(
    "/api/teams/team/friend-invitations",
    "POST",
    { userId: "friend" },
  );
  expect(
    within(screen.getByRole("article", { name: "Thanh" })).queryByRole(
      "button",
    ),
  ).toBeNull();
  fireEvent.click(
    screen.getByRole("button", { name: "Thu hồi lời mời Minh Anh" }),
  );
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/teams/team/friend-invitations/invitation/revoke",
      "POST",
      { version: "v1" },
    ),
  );
  await screen.findByText("Đã thu hồi lời mời Minh Anh.");
});

it("keeps the invite action available after a send error and reports the error locally", async () => {
  api.mockImplementation(async (path) => {
    if (path.includes("invite-friends"))
      return { items: [friend], nextOffset: null };
    throw new Error("Bạn không còn là bạn bè.");
  });
  render(<TeamInviteDialog team={team} onClose={vi.fn()} />);
  fireEvent.click(await screen.findByRole("button", { name: "Mời Minh Anh" }));
  expect(await screen.findByRole("alert")).toHaveProperty(
    "textContent",
    expect.stringContaining("Bạn không còn là bạn bè."),
  );
  expect(screen.getByRole("button", { name: "Mời Minh Anh" }).disabled).toBe(
    false,
  );
  expect(screen.queryByText("Đã gửi lời mời · Chờ phản hồi")).toBeNull();
});

it("searches all friend pages on the server and preserves the search when loading more", async () => {
  api.mockImplementation(async (path) => ({
    items: path.includes("offset=24")
      ? [{ ...friend, userId: "next", name: "Anh Hai" }]
      : [friend],
    nextOffset: path.includes("offset=24") ? null : 24,
  }));
  render(<TeamInviteDialog team={team} onClose={vi.fn()} />);
  await screen.findByRole("button", { name: "Mời Minh Anh" });
  fireEvent.change(screen.getByRole("searchbox"), { target: { value: "Anh" } });
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/teams/team/invite-friends?search=Anh&offset=0",
      "GET",
      undefined,
      true,
      expect.any(AbortSignal),
    ),
  );
  fireEvent.click(
    await screen.findByRole("button", { name: "Xem thêm bạn bè" }),
  );
  await screen.findByRole("button", { name: "Mời Anh Hai" });
  expect(api).toHaveBeenCalledWith(
    "/api/teams/team/invite-friends?search=Anh&offset=24",
    "GET",
    undefined,
    true,
    expect.any(AbortSignal),
  );
});

it("keeps link invitations separate and creates a link only when requested", async () => {
  api.mockImplementation(async (path) =>
    path.includes("invite-friends")
      ? { items: [], nextOffset: null }
      : { id: "link", code: "A".repeat(40), expiresAt: invitation.expiresAt },
  );
  render(<TeamInviteDialog team={team} onClose={vi.fn()} />);
  await screen.findByText("Bạn chưa có kết nối bạn bè");
  expect(
    api.mock.calls.some(
      ([path, method]) => path.endsWith("/invites") && method === "POST",
    ),
  ).toBe(false);
  fireEvent.click(screen.getByRole("tab", { name: "Link / mã mời" }));
  fireEvent.click(screen.getByRole("button", { name: "Tạo link mời" }));
  expect((await screen.findByLabelText("Link tham gia nhóm")).value).toContain(
    "/join.html?code=" + "A".repeat(40),
  );
  fireEvent.click(screen.getByRole("button", { name: "Thu hồi link mời" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/teams/team/invites/link", "DELETE"),
  );
  await screen.findByText("Đã thu hồi link và mã mời.");
});

it("copies a readable invitation with the group name, dedicated link and joining instructions", async () => {
  const writeText = vi.fn().mockResolvedValue(undefined);
  Object.defineProperty(navigator, "clipboard", { configurable: true, value: { writeText } });
  api.mockImplementation(async path => path.includes("invite-friends") ? { items: [], nextOffset: null } : { id: "link", code: "A".repeat(40), expiresAt: invitation.expiresAt });
  render(<TeamInviteDialog team={team} onClose={vi.fn()} />);
  fireEvent.click(screen.getByRole("tab", { name: "Link / mã mời" }));
  fireEvent.click(screen.getByRole("button", { name: "Tạo link mời" }));
  fireEvent.click(await screen.findByRole("button", { name: "Sao chép lời mời" }));
  await screen.findByRole("button", { name: "Đã sao chép lời mời" });
  const copied = writeText.mock.calls[0][0];
  expect(copied).toContain("nhóm “SWP391” trên PLANORA");
  expect(copied).toContain("/join.html?code=" + "A".repeat(40));
  expect(copied).toContain("đăng nhập và gửi yêu cầu tham gia nhóm");
  expect(copied).not.toContain("#teams?");
});

it("accepts a received invitation and opens the joined team", async () => {
  const accepted = vi.fn();
  api.mockImplementation(async (path, method) =>
    method === "POST" ? { id: "team" } : [invitation],
  );
  render(<ReceivedTeamInvitations onAccepted={accepted} />);
  fireEvent.click(await screen.findByRole("button", { name: "Tham gia nhóm" }));
  await waitFor(() => expect(accepted).toHaveBeenCalledWith("team"));
  expect(api).toHaveBeenCalledWith(
    "/api/teams/friend-invitations/invitation/respond",
    "POST",
    { version: "v1", accepted: true },
  );
  expect(screen.queryByRole("article")).toBeNull();
});

it("declines without joining and retains invitations when acceptance fails", async () => {
  const accepted = vi.fn();
  api.mockImplementation(async (path, method, body) => {
    if (method !== "POST") return [invitation];
    if (body.accepted) throw new Error("Nhóm đã đầy.");
    return { id: "team" };
  });
  render(<ReceivedTeamInvitations onAccepted={accepted} />);
  fireEvent.click(await screen.findByRole("button", { name: "Tham gia nhóm" }));
  await screen.findByText("Nhóm đã đầy.");
  expect(screen.getByRole("article", { name: "Lời mời SWP391" })).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Từ chối" }));
  await waitFor(() => expect(screen.queryByRole("article")).toBeNull());
  expect(accepted).not.toHaveBeenCalled();
});
