import { beforeEach, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TelegramCard } from "../src/telegram.jsx";
import { api } from "../src/api.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
let status;
beforeEach(() => {
  status = { configured: true, connected: false, enabled: false };
  api.mockImplementation(async (url, method) => {
    if (url.endsWith("/status")) return { ...status };
    if (url.endsWith("/connect")) {
      status.linkExpiresAt = new Date(Date.now() + 600000).toISOString();
      return { url: "https://t.me/planora_test_bot?start=single-use-token", expiresAt: status.linkExpiresAt };
    }
    if (method === "DELETE") { status = { configured: true, connected: false, enabled: false }; }
  });
});

it("offers Start in Telegram instead of token or chat-id input and detects successful linking", async () => {
  render(<TelegramCard />);
  await userEvent.setup().click(await screen.findByRole("button", { name: "Kết nối Telegram", exact: true }));
  const link = await screen.findByRole("link", { name: "Mở bot Telegram · Nhấn Start" });
  expect(link.href).toBe("https://t.me/planora_test_bot?start=single-use-token");
  expect(document.querySelector("input")).toBeNull();
  status = { configured: true, connected: true, enabled: true, displayName: "Nguyễn An" };
  await act(async () => fireEvent(window, new Event("focus")));
  await screen.findByText("Nguyễn An");
  expect(screen.queryByRole("link", { name: /Mở bot/ })).toBeNull();
  expect(screen.getByRole("button", { name: "Tạm dừng nhắc Telegram" })).toBeTruthy();
});

it("explains server configuration without requesting credentials", async () => {
  status.configured = false; render(<TelegramCard />);
  await screen.findByText("Chưa cấu hình");
  expect(screen.queryByRole("button", { name: "Kết nối Telegram" })).toBeNull();
  expect(document.querySelector("input")).toBeNull();
});

it("explains a missing server token while keeping account data out of configuration prompts", async () => {
  status = { configured: false, connected: false, enabled: false, configurationError: "MISSING_BOT_TOKEN" };
  render(<TelegramCard />);
  await screen.findByText(/máy chủ chưa lưu token/);
  expect(screen.queryByRole("button", { name: "Kết nối Telegram" })).toBeNull();
  expect(document.querySelector("input")).toBeNull();
});

it("pauses independently of other notification channels and can disconnect", async () => {
  status = { configured: true, connected: true, enabled: true, displayName: "An" };
  render(<TelegramCard />);
  await userEvent.setup().click(await screen.findByRole("button", { name: "Tạm dừng nhắc Telegram" }));
  expect(api).toHaveBeenCalledWith("/api/integrations/telegram/preferences", "PUT", { enabled: false });
  await waitFor(() => expect(screen.getByRole("button", { name: "Ngắt kết nối Telegram" }).disabled).toBe(false));
  await userEvent.setup().click(screen.getByRole("button", { name: "Ngắt kết nối Telegram" }));
  await screen.findByText("Chưa kết nối");
  expect(api).toHaveBeenCalledWith("/api/integrations/telegram", "DELETE");
  expect(api.mock.calls.some(([url]) => url === "/api/notifications/settings")).toBe(false);
});

it("prompts a blocked bot to reconnect and keeps the user's action error visible", async () => {
  status = { configured: true, connected: true, enabled: false, errorCode: "BOT_BLOCKED" };
  render(<TelegramCard />);
  const connect = await screen.findByRole("button", { name: "Kết nối lại Telegram" });
  expect(screen.getByText(/bỏ chặn bot/)).toBeTruthy();
  api.mockImplementation(async (url) => { if (url.endsWith("/connect")) throw new Error("Bot chưa phản hồi."); return status; });
  await userEvent.setup().click(connect);
  expect((await screen.findByRole("alert")).textContent).toBe("Bot chưa phản hồi.");
});
