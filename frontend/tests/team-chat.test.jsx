import { beforeEach, expect, it, vi } from "vitest";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { TeamChat } from "../src/team-chat.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";
import { syncPetProfile } from "../src/pet-appearance.jsx";
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
  expect(screen.getByRole("log").querySelector(".team-chat-pet")).toBeNull();
});
it("loads shared history and renders text safely", async () => {
  api.mockResolvedValue({
    items: [{ ...message, text: "<img src=x onerror=alert(1)>" }],
    nextAfter: 1,
    hasMore: false,
  });
  render(<TeamChat teamId="team" name="Đồ án" />);
  expect(await screen.findByText("<img src=x onerror=alert(1)>")).toBeTruthy();
  expect(
    [...screen.getByRole("log").querySelectorAll("img")].every(
      (image) =>
        image.getAttribute("src") === "/assets/start/planora-owl-v1.png",
    ),
  ).toBe(true);
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
  expect(
    screen.queryByRole("img", { name: "Ảnh đại diện của An Nguyễn" }),
  ).toBeNull();
  expect(screen.getByLabelText("Ảnh đại diện của An Nguyễn").textContent).toBe(
    "AN",
  );
});
it("perches each sender's equipped pet above the bubble with the correct evolution and updates your pet immediately", async () => {
  state.workspace.gamification = {
    selectedSkin: "classic",
    userLevel: 5,
    experiencePoints: 400,
  };
  api.mockResolvedValue({
    items: [
      message,
      {
        ...message,
        id: 2,
        userId: state.workspace.profile.id,
        text: "Pet của mình",
      },
    ],
    authors: [
      {
        userId: "member",
        name: "An Nguyễn",
        selectedSkin: "fox",
        userLevel: 10,
      },
    ],
    nextAfter: 2,
    hasMore: false,
  });
  render(<TeamChat teamId="team" name="Đồ án" />);
  const fox = await screen.findByRole("img", {
    name: "MILO của An Nguyễn · Cấp 10",
  });
  expect(fox.className).toContain("pet-stage-3");
  expect(fox.querySelector("img").getAttribute("src")).toBe(
    "/assets/pets/milo-fox-v1.png",
  );
  expect(fox.closest(".team-chat-author-line")).toBeTruthy();
  const own = screen.getByRole("img", { name: "NORA của bạn · Cấp 5" });
  expect(own.querySelector(".pet-evolution-wings")).toBeTruthy();
  act(() =>
    syncPetProfile({
      selectedSkin: "fox",
      userLevel: 10,
      experiencePoints: 900,
    }),
  );
  expect(
    screen.getByRole("img", { name: "MILO của bạn · Cấp 10" }),
  ).toBeTruthy();
  expect(
    screen.getByRole("log").querySelectorAll(".team-chat-pet"),
  ).toHaveLength(2);
});
it("refreshes another member's pet from author metadata even when the next page has no new messages", async () => {
  api.mockResolvedValueOnce({
    items: [message],
    authors: [
      {
        userId: "member",
        name: "An Nguyễn",
        selectedSkin: "fox",
        userLevel: 10,
      },
    ],
    nextAfter: 1,
    hasMore: false,
  });
  api.mockResolvedValue({
    items: [],
    authors: [
      {
        userId: "member",
        name: "An Nguyễn",
        selectedSkin: "classic",
        userLevel: 10,
      },
    ],
    nextAfter: 1,
    hasMore: false,
  });
  render(<TeamChat teamId="team" name="Đồ án" />);
  await screen.findByRole("img", { name: "MILO của An Nguyễn · Cấp 10" });
  fireEvent(document, new Event("visibilitychange"));
  await screen.findByRole("img", { name: "NORA của An Nguyễn · Cấp 10" });
  expect(screen.getAllByText(message.text)).toHaveLength(1);
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
it("keeps history, draft and reading position when expanding and closing with Escape", async () => {
  api.mockResolvedValue({ items: [message], nextAfter: 1, hasMore: false });
  const { unmount } = render(<TeamChat teamId="team" name="Đồ án" />);
  await screen.findByText(message.text);
  expect(document.activeElement).not.toBe(
    screen.getByRole("button", { name: "Phóng to khung chat" }),
  );
  fireEvent.change(screen.getByRole("textbox"), {
    target: { value: "Tin nhắn đang soạn\nDòng tiếp theo" },
  });
  screen.getByRole("log").scrollTop = 120;
  fireEvent.click(screen.getByRole("button", { name: "Phóng to khung chat" }));
  const dialog = screen.getByRole("dialog", {
    name: "Chat chung Đồ án mở rộng",
  });
  expect(dialog.open).toBe(true);
  expect(document.body.style.overflow).toBe("hidden");
  expect(document.activeElement).toBe(screen.getByRole("textbox"));
  expect(screen.getByRole("textbox").value).toBe(
    "Tin nhắn đang soạn\nDòng tiếp theo",
  );
  expect(screen.getByRole("log").scrollTop).toBe(120);
  expect(screen.getByText(message.text)).toBeTruthy();
  fireEvent(dialog, new Event("cancel", { cancelable: true }));
  expect(screen.queryByRole("dialog")).toBeNull();
  expect(screen.getByRole("textbox").value).toBe(
    "Tin nhắn đang soạn\nDòng tiếp theo",
  );
  expect(screen.getByRole("log").scrollTop).toBe(120);
  const expandButton = screen.getByRole("button", {
    name: "Phóng to khung chat",
  });
  expect(document.activeElement).toBe(expandButton);
  expect(document.body.style.overflow).toBe("");
  fireEvent.click(expandButton);
  fireEvent.click(screen.getByRole("button", { name: "Thu nhỏ khung chat" }));
  expect(screen.queryByRole("dialog")).toBeNull();
  expect(api).toHaveBeenCalledTimes(1);
  fireEvent.click(screen.getByRole("button", { name: "Phóng to khung chat" }));
  unmount();
  expect(document.body.style.overflow).toBe("");
});
it("sends with Enter while leaving Shift+Enter and IME composition for editing", async () => {
  api.mockImplementation(async (_, method, request) =>
    method === "POST"
      ? { ...message, text: request.text, userId: state.workspace.profile.id }
      : { items: [], nextAfter: 0, hasMore: false },
  );
  render(<TeamChat teamId="team" name="Đồ án" />);
  await screen.findByText("Bắt đầu cuộc trò chuyện");
  const input = screen.getByRole("textbox", { name: "Tin nhắn cho nhóm" });
  fireEvent.change(input, { target: { value: "Hẹn nhóm chiều nay" } });
  expect(fireEvent.keyDown(input, { key: "Enter", shiftKey: true })).toBe(true);
  expect(fireEvent.keyDown(input, { key: "Enter", isComposing: true })).toBe(
    true,
  );
  expect(fireEvent.keyDown(input, { key: "Enter", keyCode: 229 })).toBe(true);
  expect(api.mock.calls.filter(([, method]) => method === "POST")).toHaveLength(
    0,
  );
  expect(fireEvent.keyDown(input, { key: "Enter" })).toBe(false);
  expect(await screen.findByText("Hẹn nhóm chiều nay")).toBeTruthy();
  await waitFor(() => expect(input.value).toBe(""));
  expect(api.mock.calls.filter(([, method]) => method === "POST")).toHaveLength(
    1,
  );
});
