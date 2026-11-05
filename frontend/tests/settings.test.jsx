import { beforeEach, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { settingsPage } from "../src/page-settings.jsx";
import { state } from "../src/ui.jsx";
import { setAvatar } from "../src/interactive.jsx";
import { update } from "../src/store.js";
import { setNotificationSoundEnabled } from "../src/notification-sound.js";

vi.mock("../src/google-calendar.jsx", () => ({
  GoogleCalendarCard: () => <div>Calendar integration</div>,
}));
vi.mock("../src/telegram.jsx", () => ({
  TelegramCard: () => <div>Telegram integration</div>,
}));
vi.mock("../src/google-signin.jsx", () => ({
  GoogleAccountCard: () => <div>Google account</div>,
}));
vi.mock("../src/account-password.jsx", () => ({
  AccountPassword: () => <div>Account password</div>,
}));
vi.mock("../src/nora-inventory.jsx", () => ({
  NoraInventory: () => <div>Pet inventory</div>,
}));

beforeEach(() => {
  state.settingsTab = undefined;
  state.roles = [];
  state.workspace = {
    profile: {
      id: "settings-user",
      firstName: "Minh",
      lastName: "Nguyễn",
      displayName: "Minh Nguyễn",
      email: "minh@example.test",
      bio: "Giới thiệu ban đầu",
      avatarUrl: "data:image/png;base64,AAA",
    },
    categories: [],
    tasks: [],
    notificationSettings: {
      inAppEnabled: true,
      emailEnabled: false,
      deadlineLeadHours: 24,
      sessionReminderLeadMinutes: 10,
      workloadWarningLevel: "HIGH",
    },
  };
  localStorage.clear();
  update({ slots: {} });
  setNotificationSoundEnabled("settings-user", true);
});

it("puts essential settings first and only loads integrations when opened", async () => {
  const user = userEvent.setup();
  render(settingsPage());
  const nav = screen.getByRole("navigation", { name: "Mục cài đặt" });
  const buttons = within(nav).getAllByRole("button");
  expect(buttons.map((button) => button.id)).toEqual([
    "settings-nav-profile",
    "settings-nav-security",
    "settings-nav-notifications",
    "settings-nav-connections",
    "settings-nav-categories",
    "settings-nav-pets",
  ]);
  expect(buttons[0].getAttribute("aria-current")).toBe("page");
  expect(screen.queryByText("Telegram integration")).toBeNull();
  await user.click(within(nav).getByRole("button", { name: /Kết nối/ }));
  expect(screen.getByText("Telegram integration")).toBeTruthy();
  expect(screen.getByRole("region", { name: /Kết nối/ })).toBeTruthy();
});

it("keeps drafts across sections, submits only actual profile fields and restores the saved values", async () => {
  const user = userEvent.setup();
  render(settingsPage());
  const save = screen.getByRole("button", { name: "Lưu thông tin hồ sơ" });
  expect(save.disabled).toBe(true);
  await user.clear(screen.getByLabelText("Tên", { exact: true }));
  await user.type(screen.getByLabelText("Tên", { exact: true }), "Lan");
  expect(save.disabled).toBe(false);
  await user.click(screen.getByRole("button", { name: /Bảo mật/ }));
  expect(screen.getByText("Account password")).toBeTruthy();
  await user.click(screen.getByRole("button", { name: /Hồ sơ/ }));
  expect(screen.getByLabelText("Tên", { exact: true }).value).toBe("Lan");
  expect(screen.getByRole("status").textContent).toContain("chưa lưu");
  const form = save.closest("form");
  expect(Object.fromEntries(new FormData(form))).toEqual({
    firstName: "Lan",
    lastName: "Nguyễn",
    displayName: "Minh Nguyễn",
    bio: "Giới thiệu ban đầu",
    avatarUrl: "data:image/png;base64,AAA",
  });
  await user.click(screen.getByRole("button", { name: "Hoàn tác" }));
  expect(screen.getByLabelText("Tên", { exact: true }).value).toBe("Minh");
  expect(save.disabled).toBe(true);
});

it("recognizes avatar-only changes and undo restores the image and hidden form value", async () => {
  const user = userEvent.setup();
  render(settingsPage());
  const save = screen.getByRole("button", { name: "Lưu thông tin hồ sơ" });
  act(() => setAvatar(""));
  expect(save.disabled).toBe(false);
  expect(document.querySelector("#hero-avatar-img")).toBeNull();
  await user.click(screen.getByRole("button", { name: "Hoàn tác" }));
  expect(document.getElementById("avatarUrl-hidden").value).toBe(
    "data:image/png;base64,AAA",
  );
  expect(document.querySelector("#hero-avatar-img").getAttribute("src")).toBe(
    "data:image/png;base64,AAA",
  );
  expect(save.disabled).toBe(true);
});

it("updates sound availability immediately, preserves notification drafts and restores switches and thresholds", async () => {
  const user = userEvent.setup();
  render(settingsPage());
  await user.click(screen.getByRole("button", { name: /Thông báo/ }));
  const inApp = screen.getByRole("switch", {
    name: "Bật thông báo trong ứng dụng",
  });
  const email = screen.getByRole("switch", { name: "Gửi nhắc nhở qua email" });
  const sound = screen.getByRole("checkbox", { name: "Âm thanh thông báo" });
  expect(sound.disabled).toBe(false);
  await user.click(inApp);
  expect(sound.disabled).toBe(true);
  await user.click(email);
  fireEvent.change(screen.getByLabelText(/Nhắc deadline trước/), {
    target: { value: "48" },
  });
  await user.click(screen.getByRole("button", { name: /Thiết lập kết nối/ }));
  await user.click(screen.getByRole("button", { name: /Thông báo/ }));
  const form = screen
    .getByRole("button", { name: "Lưu cài đặt thông báo" })
    .closest("form");
  expect(Object.fromEntries(new FormData(form))).toEqual({
    emailEnabled: "on",
    deadlineLeadHours: "48",
    sessionReminderLeadMinutes: "10",
    workloadWarningLevel: "HIGH",
  });
  await user.click(screen.getByRole("button", { name: "Hoàn tác" }));
  expect(inApp.checked).toBe(true);
  expect(email.checked).toBe(false);
  expect(sound.disabled).toBe(false);
  expect(screen.getByLabelText(/Nhắc deadline trước/).value).toBe("24");
  expect(
    screen.getByRole("button", { name: "Lưu cài đặt thông báo" }).disabled,
  ).toBe(true);
});

it("does not mark browser-only sound preferences as pending server changes", async () => {
  const user = userEvent.setup();
  render(settingsPage());
  await user.click(screen.getByRole("button", { name: /Thông báo/ }));
  await user.click(
    screen.getByRole("checkbox", { name: "Âm thanh thông báo" }),
  );
  expect(
    screen.getByRole("button", { name: "Lưu cài đặt thông báo" }).disabled,
  ).toBe(true);
});

it("warns before closing a dirty page and clears the warning when the edit is reverted", () => {
  render(settingsPage());
  const firstName = screen.getByLabelText("Tên", { exact: true });
  fireEvent.change(firstName, { target: { value: "Lan" } });
  const dirtyExit = new Event("beforeunload", { cancelable: true });
  window.dispatchEvent(dirtyExit);
  expect(dirtyExit.defaultPrevented).toBe(true);
  fireEvent.change(firstName, { target: { value: "Minh" } });
  const cleanExit = new Event("beforeunload", { cancelable: true });
  window.dispatchEvent(cleanExit);
  expect(cleanExit.defaultPrevented).toBe(false);
  expect(
    screen.getByRole("button", { name: "Lưu thông tin hồ sơ" }).disabled,
  ).toBe(true);
});
