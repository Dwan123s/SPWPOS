import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { TeamChat } from "../src/team-chat.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";
const message = {
  id: 1,
  userId: "member",
  author: "Thành viên A",
  text: "Mình đã xem task.",
  createdAt: "2026-11-01T12:00:00Z",
};
beforeEach(() => {
  vi.resetAllMocks();
  state.workspace = workspace();
});
it("renders approved completion as a system message even when the approver is the current user", async () => {
  api.mockResolvedValue({
    items: [
      {
        ...message,
        userId: state.workspace.profile.id,
        isSystem: true,
        text: "An đã hoàn thành task Báo cáo. Được trưởng nhóm duyệt.",
      },
    ],
    hasMore: false,
  });
  render(<TeamChat teamId="team" name="Đồ án" />);
  expect(await screen.findByText("✓ Hệ thống")).toBeTruthy();
  expect(
    screen.getByText(/An đã hoàn thành/).closest("article").className,
  ).toContain("system");
  expect(screen.queryByText("Bạn")).toBeNull();
});
it("loads shared history and renders text safely", async () => {
  api.mockResolvedValue({
    items: [{ ...message, text: "<img src=x onerror=alert(1)>" }],
    nextAfter: 1,
    hasMore: false,
  });
  render(<TeamChat teamId="team" name="Đồ án" />);
  expect(await screen.findByText("<img src=x onerror=alert(1)>")).toBeTruthy();
  expect(screen.getByRole("log").querySelector("img")).toBeNull();
  expect(screen.getByText(message.author)).toBeTruthy();
});
it("uses account avatars and display names, and falls back to initials when an image fails", async () => {
  api.mockResolvedValue({
    items: [message],
    authors: [
      { userId: "member", name: "An Nguyễn", avatarUrl: "/assets/an.png" },
    ],
    nextAfter: 1,
    hasMore: false,
  });
  render(<TeamChat teamId="team" name="Đồ án" />);
  const image = await screen.findByRole("img", {
    name: "Ảnh đại diện của An Nguyễn",
  });
  expect(image.getAttribute("src")).toBe("/assets/an.png");
  expect(screen.getByText("An Nguyễn")).toBeTruthy();
  fireEvent.error(image);
  expect(screen.queryByRole("img")).toBeNull();
  expect(screen.getByLabelText("Ảnh đại diện của An Nguyễn").textContent).toBe(
    "AN",
  );
});
it("shows the current account avatar on outgoing messages and one day divider per date", async () => {
  state.workspace.profile.avatarUrl = "/assets/me.png";
  state.workspace.profile.displayName = "Minh An";
  api.mockResolvedValue({
    items: [
      { ...message, userId: state.workspace.profile.id },
      {
        ...message,
        id: 2,
        userId: state.workspace.profile.id,
        text: "Tin tiếp theo",
      },
    ],
    nextAfter: 2,
    hasMore: false,
  });
  render(<TeamChat teamId="team" name="Đồ án" />);
  expect(
    (
      await screen.findAllByRole("img", { name: "Ảnh đại diện của Minh An" })
    ).every((image) => image.getAttribute("src") === "/assets/me.png"),
  ).toBe(true);
  expect(
    screen.getByRole("log").querySelectorAll(".team-chat-day"),
  ).toHaveLength(1);
  expect(
    screen.getByText("Tin tiếp theo").closest(".team-chat-row").className,
  ).toContain("mine");
});
it("sends only explicit shared text and keeps the request id when retrying a failed send", async () => {
  let calls = 0;
  api.mockImplementation(async (_, method) => {
    if (method === "POST") {
      if (++calls === 1) throw new Error("Mất kết nối. Thử gửi lại.");
      return {
        ...message,
        userId: state.workspace.profile.id,
        text: "Hẹn nhóm 8h",
      };
    }
    return { items: [], nextAfter: null, hasMore: false };
  });
  render(<TeamChat teamId="team" name="Đồ án" />);
  const input = screen.getByRole("textbox", { name: "Tin nhắn cho nhóm" });
  fireEvent.change(input, { target: { value: "Hẹn nhóm 8h" } });
  fireEvent.click(screen.getByRole("button", { name: "Gửi tin nhắn" }));
  expect(await screen.findByRole("alert")).toHaveProperty(
    "textContent",
    "Mất kết nối. Thử gửi lại.",
  );
  expect(input.value).toBe("Hẹn nhóm 8h");
  fireEvent.click(screen.getByRole("button", { name: "Gửi tin nhắn" }));
  expect(await screen.findByText("Hẹn nhóm 8h")).toBeTruthy();
  await waitFor(() => expect(input.value).toBe(""));
  const posts = api.mock.calls.filter(([, method]) => method === "POST");
  expect(posts).toHaveLength(2);
  expect(posts[0][2]).toEqual(posts[1][2]);
  expect(Object.keys(posts[0][2]).sort()).toEqual(["requestId", "text"]);
});
it("shows older messages without duplicating the current page", async () => {
  api.mockImplementation(async (path) =>
    path.includes("?before=")
      ? {
          items: [{ ...message, id: 1, text: "Tin cũ" }],
          hasMore: false,
          nextAfter: 1,
        }
      : {
          items: [{ ...message, id: 2, text: "Tin mới" }],
          hasMore: true,
          nextAfter: 2,
        },
  );
  render(<TeamChat teamId="team" name="Đồ án" />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Xem tin nhắn cũ hơn" }),
  );
  expect(await screen.findByText("Tin cũ")).toBeTruthy();
  expect(screen.getAllByText("Tin mới")).toHaveLength(1);
});
