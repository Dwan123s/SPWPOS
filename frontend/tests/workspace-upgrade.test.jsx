import { beforeEach, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { CatchUpCard } from "../src/catch-up.jsx";
import { NoraInventory } from "../src/nora-inventory.jsx";
import { PetArtwork, PetProgress, syncPetProfile } from "../src/pet-appearance.jsx";
import { state } from "../src/ui.jsx";
import { api } from "../src/api.js";
import { loadPayOsCheckout } from "../src/payos-checkout.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
vi.mock("../src/payos-checkout.js", async (original) => ({
  ...(await original()),
  loadPayOsCheckout: vi.fn(),
}));
const session = {
  id: "session",
  taskId: "task",
  taskName: "Viết báo cáo",
  start: "2026-10-28T07:00:00Z",
  end: "2026-10-28T08:00:00Z",
};
beforeEach(() => {
  vi.clearAllMocks();
  loadPayOsCheckout.mockResolvedValue({
    usePayOS: vi.fn(() => ({ open: vi.fn(), exit: vi.fn() })),
  });
  state.workspace = {
    features: ["ambient-tracking"],
    preference: { timeZoneId: "Asia/Ho_Chi_Minh" },
    tasks: [{ id: "task", progress: 60 }],
  };
  api.mockImplementation(async (path, method) =>
    method === "POST" ? { message: "Đã ghi nhận và xếp phiên bù." } : [session],
  );
});
it("records completion without starting a timer and refreshes the workspace", async () => {
  const refresh = vi.fn();
  render(<CatchUpCard onWorkspaceChanged={refresh} />);
  fireEvent.click(await screen.findByRole("button", { name: "Đã hoàn thành" }));
  await screen.findByRole("status");
  expect(api).toHaveBeenCalledWith("/api/sessions/session/catch-up", "POST", {
    outcome: "COMPLETED",
  });
  expect(refresh).toHaveBeenCalledOnce();
  expect(api.mock.calls.some(([path]) => path.endsWith("/start"))).toBe(false);
});
it("sends absolute partial progress and actual minutes", async () => {
  render(<CatchUpCard />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Hoàn thành một phần" }),
  );
  expect(screen.getByLabelText("Tiến độ hiện tại").value).toBe("60");
  fireEvent.change(screen.getByLabelText("Tiến độ hiện tại"), {
    target: { value: "75" },
  });
  fireEvent.change(screen.getByLabelText("Số phút đã làm"), {
    target: { value: "35" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Ghi nhận một phần" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/sessions/session/catch-up", "POST", {
      outcome: "PARTIAL",
      progress: 75,
      actualMinutes: 35,
    }),
  );
});
it("keeps the session available when reporting fails", async () => {
  api.mockImplementation(async (_, method) => {
    if (method === "POST") throw new Error("Thử lại giúp mình");
    return [session];
  });
  render(<CatchUpCard />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Bị lỡ / Chưa làm" }),
  );
  await screen.findByText("Thử lại giúp mình");
  expect(
    screen.getByRole("button", { name: "Bị lỡ / Chưa làm" }).disabled,
  ).toBe(false);
});
it("equips the purchased fox and keeps only the default Nora and Milo in inventory", async () => {
  const inventory = [
    { code: "classic", name: "NORA mặc định", requiredXp: 0, unlocked: true },
    {
      code: "fox",
      name: "MILO",
      requiredXp: 0,
      priceVnd: 10000,
      unlocked: true,
    },
  ];
  const profile = {
    experiencePoints: 112,
    userLevel: 2,
    selectedSkin: "classic",
    inventory,
  };
  state.workspace.gamification = profile;
  api.mockImplementation(async (_, method) =>
    method === "POST" ? {} : { ...profile, selectedSkin: "fox" },
  );
  render(<NoraInventory />);
  expect(screen.queryByText("NORA rừng xanh")).toBeNull();
  expect(screen.queryByText("NORA hoàng hôn")).toBeNull();
  expect(screen.queryByText("NORA vũ trụ")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Sử dụng" }));
  await waitFor(() =>
    expect(document.documentElement.dataset.noraSkin).toBe("fox"),
  );
  expect(api).toHaveBeenCalledWith("/api/workspace/gamification/skin", "POST", {
    code: "fox",
  });
});
it("creates the fixed-price fox checkout without trusting a return URL or unlocking early", async () => {
  const profile = {
    experiencePoints: 400,
    userLevel: 5,
    selectedSkin: "classic",
    inventory: [
      { code: "classic", name: "NORA", requiredXp: 0, unlocked: true },
      {
        code: "fox",
        name: "MILO",
        requiredXp: 0,
        priceVnd: 10000,
        unlocked: false,
      },
    ],
  };
  state.workspace.gamification = profile;
  const purchase = {
    id: "purchase",
    amountVnd: 10000,
    status: "PENDING",
    checkoutUrl: "https://pay.payos.vn/web/test",
    returnUrl: "https://public-planora.example/?payment=returned",
    expiresAt: "2026-11-30T10:00:00Z",
  };
  let paid = false;
  api.mockImplementation(async (path, method) => {
    if (path.endsWith("capabilities")) return { available: true };
    if (path.endsWith("latest")) return null;
    if (path.endsWith("profile"))
      return paid
        ? {
            ...profile,
            inventory: profile.inventory.map((s) => ({ ...s, unlocked: true })),
          }
        : profile;
    if (method === "POST") return purchase;
    return { ...purchase, status: paid ? "PAID" : "PENDING" };
  });
  render(<NoraInventory />);
  const buy = screen.getByRole("button", { name: "Mua skin · 10.000đ" });
  await waitFor(() => expect(buy.disabled).toBe(false));
  fireEvent.click(buy);
  const dialog = await screen.findByRole("dialog", {
    name: "Thanh toán skin MILO",
  });
  expect(dialog.open).toBe(true);
  const sdk = await loadPayOsCheckout.mock.results[0].value;
  await waitFor(() =>
    expect(sdk.usePayOS).toHaveBeenCalledWith(
      expect.objectContaining({
        CHECKOUT_URL: purchase.checkoutUrl,
        embedded: true,
      }),
    ),
  );
  expect(screen.queryByRole("button", { name: "Sử dụng" })).toBeNull();
  const creation = api.mock.calls.find(
    ([path, method]) => path === "/api/commerce/payments" && method === "POST",
  );
  expect(Object.keys(creation[2]).sort()).toEqual(["code", "requestId"]);
  expect(creation[2].code).toBe("fox");
  paid = true;
  fireEvent.click(screen.getByRole("button", { name: "Tôi đã thanh toán" }));
  expect(await screen.findByRole("button", { name: "Sử dụng" })).toBeTruthy();
  expect(await screen.findByText(/MILO đã vào tủ đồ/)).toBeTruthy();
});
it("shows distinct evolution decorations at levels five and ten for both animals", () => {
  const { container, rerender } = render(
    <PetArtwork code="classic" level={1} alt="Pet" />,
  );
  expect(container.querySelector(".pet-evolution-wings")).toBeNull();
  rerender(<PetArtwork code="classic" level={5} alt="Pet" />);
  expect(container.querySelector(".pet-evolution-wings")).toBeTruthy();
  expect(container.querySelector(".pet-evolution-crown")).toBeNull();
  rerender(<PetArtwork code="fox" level={10} alt="Pet" />);
  expect(container.querySelector(".pet-evolution-crown")).toBeTruthy();
  expect(container.querySelector("img").getAttribute("src")).toBe(
    "/assets/pets/milo-fox-v1.png",
  );
});
it("updates header XP within the same level and opens the pet inventory", () => {
  state.workspace.gamification = { selectedSkin: "classic", userLevel: 1, experiencePoints: 25 };
  render(<PetProgress compact />);
  expect(screen.getByText("25 XP")).toBeTruthy();
  act(() => syncPetProfile({ selectedSkin: "classic", userLevel: 1, experiencePoints: 50 }));
  expect(screen.getByText("50 XP")).toBeTruthy();
  expect(screen.getByRole("progressbar").getAttribute("value")).toBe("50");
  fireEvent.click(screen.getByRole("link", { name: "Mở Pet và XP" }));
  expect(state.settingsTab).toBe("pets");
});
