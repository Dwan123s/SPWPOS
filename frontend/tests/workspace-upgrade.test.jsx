import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { CatchUpCard } from "../src/catch-up.jsx";
import { NoraInventory } from "../src/nora-inventory.jsx";
import { state } from "../src/ui.jsx";
import { api } from "../src/api.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
const session = { id: "session", taskId: "task", taskName: "Viết báo cáo", start: "2026-10-28T07:00:00Z", end: "2026-10-28T08:00:00Z" };
beforeEach(() => {
  vi.clearAllMocks();
  state.workspace = { features: ["ambient-tracking"], preference: { timeZoneId: "Asia/Ho_Chi_Minh" }, tasks: [{ id: "task", progress: 60 }] };
  api.mockImplementation(async (path, method) => method === "POST" ? { message: "Đã ghi nhận và xếp phiên bù." } : [session]);
});
it("records completion without starting a timer and refreshes the workspace", async () => {
  const refresh = vi.fn(); render(<CatchUpCard onWorkspaceChanged={refresh} />);
  fireEvent.click(await screen.findByRole("button", { name: "Đã hoàn thành" }));
  await screen.findByRole("status");
  expect(api).toHaveBeenCalledWith("/api/sessions/session/catch-up", "POST", { outcome: "COMPLETED" });
  expect(refresh).toHaveBeenCalledOnce(); expect(api.mock.calls.some(([path]) => path.endsWith("/start"))).toBe(false);
});
it("sends absolute partial progress and actual minutes", async () => {
  render(<CatchUpCard />); fireEvent.click(await screen.findByRole("button", { name: "Hoàn thành một phần" }));
  expect(screen.getByLabelText("Tiến độ hiện tại").value).toBe("60");
  fireEvent.change(screen.getByLabelText("Tiến độ hiện tại"), { target: { value: "75" } });
  fireEvent.change(screen.getByLabelText("Số phút đã làm"), { target: { value: "35" } });
  fireEvent.click(screen.getByRole("button", { name: "Ghi nhận một phần" }));
  await waitFor(() => expect(api).toHaveBeenCalledWith("/api/sessions/session/catch-up", "POST", { outcome: "PARTIAL", progress: 75, actualMinutes: 35 }));
});
it("keeps the session available when reporting fails", async () => {
  api.mockImplementation(async (_, method) => { if (method === "POST") throw new Error("Thử lại giúp mình"); return [session]; });
  render(<CatchUpCard />); fireEvent.click(await screen.findByRole("button", { name: "Bị lỡ / Chưa làm" }));
  await screen.findByText("Thử lại giúp mình"); expect(screen.getByRole("button", { name: "Bị lỡ / Chưa làm" }).disabled).toBe(false);
});
it("equips unlocked skins and disables locked inventory", async () => {
  const inventory = [{ code: "classic", name: "NORA cổ điển", requiredXp: 0, unlocked: true }, { code: "forest", name: "NORA rừng xanh", requiredXp: 100, unlocked: true }, { code: "cosmic", name: "NORA vũ trụ", requiredXp: 600, unlocked: false }];
  const profile = { experiencePoints: 112, userLevel: 2, selectedSkin: "classic", inventory };
  state.workspace.gamification = profile;
  api.mockImplementation(async (_, method) => method === "POST" ? {} : { ...profile, selectedSkin: "forest" });
  render(<NoraInventory />);
  expect(screen.getByRole("button", { name: "Chưa mở khóa" }).disabled).toBe(true);
  fireEvent.click(screen.getByRole("button", { name: "Sử dụng" }));
  await waitFor(() => expect(document.documentElement.dataset.noraSkin).toBe("forest"));
  expect(api).toHaveBeenCalledWith("/api/workspace/gamification/skin", "POST", { code: "forest" });
});
