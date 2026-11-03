import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { PetPaymentDialog } from "../src/pet-payment-dialog.jsx";
import { NoraInventory } from "../src/nora-inventory.jsx";
import { loadPayOsCheckout } from "../src/payos-checkout.js";
import { state } from "../src/ui.jsx";
import { api } from "../src/api.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
vi.mock("../src/payos-checkout.js", async (original) => ({
  ...(await original()),
  loadPayOsCheckout: vi.fn(),
}));
const order = {
  id: "existing",
  amountVnd: 10000,
  status: "PENDING",
  checkoutUrl: "https://pay.payos.vn/web/existing",
  returnUrl: "https://public-planora.example/?payment=returned",
};
let config, exit, usePayOS;
beforeEach(() => {
  vi.clearAllMocks();
  exit = vi.fn(() =>
    document
      .getElementById(config.ELEMENT_ID)
      ?.querySelector("iframe")
      ?.remove(),
  );
  usePayOS = vi.fn((options) => {
    config = options;
    return {
      open: () =>
        document
          .getElementById(options.ELEMENT_ID)
          .appendChild(document.createElement("iframe")),
      exit,
    };
  });
  loadPayOsCheckout.mockResolvedValue({ usePayOS });
});
it("embeds checkout and cleans up the iframe, scroll lock and focus when closing", async () => {
  const opener = document.createElement("button");
  document.body.appendChild(opener);
  opener.focus();
  const close = vi.fn();
  const { unmount } = render(
    <PetPaymentDialog order={order} onClose={close} onCheck={vi.fn()} />,
  );
  expect((await screen.findByRole("dialog")).open).toBe(true);
  await screen.findByTitle("Thanh toán MILO qua payOS");
  expect(config.embedded).toBe(true);
  expect(config.CHECKOUT_URL).toBe(order.checkoutUrl);
  expect(decodeURIComponent(config.RETURN_URL)).toBe(order.returnUrl);
  expect(decodeURIComponent(config.RETURN_URL)).not.toBe(window.location.href);
  fireEvent(
    screen.getByRole("dialog"),
    new Event("cancel", { cancelable: true }),
  );
  expect(close).toHaveBeenCalledOnce();
  unmount();
  expect(exit).toHaveBeenCalledOnce();
  expect(document.body.style.overflow).toBe("");
  expect(document.activeElement).toBe(opener);
  opener.remove();
});
it("retries SDK loading failures and rejects checkout URLs outside payOS", async () => {
  loadPayOsCheckout.mockRejectedValueOnce(new Error("offline"));
  const { rerender } = render(
    <PetPaymentDialog order={order} onClose={vi.fn()} onCheck={vi.fn()} />,
  );
  await screen.findByRole("alert");
  fireEvent.click(screen.getByRole("button", { name: "Tải lại thanh toán" }));
  await screen.findByTitle("Thanh toán MILO qua payOS");
  expect(loadPayOsCheckout).toHaveBeenCalledTimes(2);
  rerender(
    <PetPaymentDialog
      order={{
        ...order,
        checkoutUrl: "https://pay.payos.vn.evil.example/web/x",
      }}
      onClose={vi.fn()}
      onCheck={vi.fn()}
    />,
  );
  expect(await screen.findByRole("alert")).toBeTruthy();
  expect(loadPayOsCheckout).toHaveBeenCalledTimes(2);
  expect(exit).toHaveBeenCalledOnce();
});
it("reopens an existing order without creating another, and never unlocks from iframe success alone", async () => {
  const profile = {
    selectedSkin: "classic",
    userLevel: 1,
    experiencePoints: 0,
    inventory: [
      { code: "classic", name: "NORA", unlocked: true },
      { code: "fox", name: "MILO", priceVnd: 10000, unlocked: false },
    ],
  };
  state.workspace = { gamification: profile };
  api.mockImplementation(async (path) =>
    path.endsWith("profile")
      ? profile
      : path.endsWith("capabilities")
        ? { available: true }
        : order,
  );
  render(<NoraInventory />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Tiếp tục thanh toán" }),
  );
  await screen.findByTitle("Thanh toán MILO qua payOS");
  await act(async () => config.onSuccess({ status: "PAID" }));
  expect(api).toHaveBeenCalledWith("/api/commerce/payments/existing");
  expect(screen.queryByRole("button", { name: "Sử dụng" })).toBeNull();
  expect(screen.queryByText("Thanh toán thành công!")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Đóng thanh toán" }));
  expect(screen.queryByRole("dialog")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Tiếp tục thanh toán" }));
  await waitFor(() => expect(usePayOS).toHaveBeenCalledTimes(2));
  expect(api.mock.calls.some(([, method]) => method === "POST")).toBe(false);
});
it("cleans up checkout after backend confirmation and shows the success view", async () => {
  const props = { onClose: vi.fn(), onCheck: vi.fn() };
  const { rerender } = render(<PetPaymentDialog {...props} order={order} />);
  await screen.findByTitle("Thanh toán MILO qua payOS");
  rerender(
    <PetPaymentDialog {...props} order={{ ...order, status: "PAID" }} />,
  );
  expect(screen.getByText("Thanh toán thành công!")).toBeTruthy();
  expect(exit).toHaveBeenCalledOnce();
  expect(screen.queryByTitle("Thanh toán MILO qua payOS")).toBeNull();
});
it("preserves the server return URL including query separators and hash through the official SDK", async () => {
  const returnUrl =
    "https://public-planora.example/?payment=returned&source=pet#settings";
  render(
    <PetPaymentDialog
      order={{ ...order, returnUrl }}
      onClose={vi.fn()}
      onCheck={vi.fn()}
    />,
  );
  await screen.findByTitle("Thanh toán MILO qua payOS");
  const sdkFrameUrl = new URL(
    `${config.CHECKOUT_URL.replace("/web/", "/embedded/")}?redirect_uri=${config.RETURN_URL}`,
  );
  expect(sdkFrameUrl.searchParams.get("redirect_uri")).toBe(returnUrl);
  expect(sdkFrameUrl.hash).toBe("");
  expect([...sdkFrameUrl.searchParams.keys()]).toEqual(["redirect_uri"]);
});
it("does not substitute the localhost URL when the server return URL is missing", async () => {
  render(
    <PetPaymentDialog
      order={{ ...order, returnUrl: null }}
      onClose={vi.fn()}
      onCheck={vi.fn()}
    />,
  );
  expect(await screen.findByRole("alert")).toBeTruthy();
  expect(loadPayOsCheckout).not.toHaveBeenCalled();
});
it("reloads the profile after latest-order reconciliation instead of keeping the parallel stale profile", async () => {
  const profile = {
    selectedSkin: "classic",
    userLevel: 1,
    experiencePoints: 0,
    inventory: [
      { code: "classic", name: "NORA", unlocked: true },
      { code: "fox", name: "MILO", priceVnd: 10000, unlocked: false },
    ],
  };
  state.workspace = { gamification: profile };
  let profileCalls = 0;
  api.mockImplementation(async (path) => {
    if (path.endsWith("capabilities")) return { available: true };
    if (path.endsWith("latest")) return { ...order, status: "PAID" };
    if (path.endsWith("profile"))
      return ++profileCalls === 1
        ? profile
        : {
            ...profile,
            inventory: profile.inventory.map((s) => ({ ...s, unlocked: true })),
          };
  });
  render(<NoraInventory />);
  await screen.findByRole("button", { name: "Sử dụng" });
  expect(profileCalls).toBe(2);
  expect(api.mock.calls.some(([, method]) => method === "POST")).toBe(false);
});
it("lets a user reconcile a paid transfer even when the local order has expired", async () => {
  const profile = {
    selectedSkin: "classic",
    userLevel: 1,
    experiencePoints: 0,
    inventory: [
      { code: "classic", name: "NORA", unlocked: true },
      { code: "fox", name: "MILO", priceVnd: 10000, unlocked: false },
    ],
  };
  state.workspace = { gamification: profile };
  let paid = false;
  api.mockImplementation(async (path) => {
    if (path.endsWith("capabilities")) return { available: true };
    if (path.endsWith("latest")) return { ...order, status: "EXPIRED" };
    if (path.endsWith("profile"))
      return paid
        ? {
            ...profile,
            inventory: profile.inventory.map((s) => ({ ...s, unlocked: true })),
          }
        : profile;
    paid = true;
    return { ...order, status: "PAID" };
  });
  render(<NoraInventory />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Kiểm tra thanh toán" }),
  );
  await screen.findByRole("button", { name: "Sử dụng" });
  expect(api).toHaveBeenCalledWith("/api/commerce/payments/existing");
});
