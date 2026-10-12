import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { api } from "../src/api.js";
import { AccountPassword } from "../src/account-password.jsx";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
beforeEach(() => {
  vi.clearAllMocks();
});
it("offers Google users a first password without asking for a password they do not have", async () => {
  api.mockResolvedValue({ hasPassword: false, googleLinked: true });
  render(<AccountPassword email="member@example.com" />);
  await screen.findByRole("heading", {
    name: "Thiết lập đăng nhập bằng mật khẩu",
  });
  expect(screen.queryByLabelText("Mật khẩu hiện tại")).toBeNull();
  expect(screen.getByText("member@example.com")).toBeTruthy();
  expect(screen.getByLabelText("Mã OTP xác nhận từ Email")).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Thiết lập mật khẩu" }).closest("form")
      .dataset.passwordMode,
  ).toBe("setup");
  expect(
    screen.getByText(/cùng tài khoản, công việc và lịch hiện tại/),
  ).toBeTruthy();
});
it("keeps the current password requirement for accounts that already have one", async () => {
  api.mockResolvedValue({ hasPassword: true, googleLinked: true });
  render(<AccountPassword email="member@example.com" />);
  await screen.findByRole("heading", { name: "Đổi mật khẩu tài khoản" });
  expect(screen.getByLabelText("Mật khẩu hiện tại").required).toBe(true);
  expect(
    screen.getByRole("button", { name: "Đổi mật khẩu ngay" }).closest("form")
      .dataset.passwordMode,
  ).toBe("change");
});
it("waits for account status and can retry a failed lookup", async () => {
  api
    .mockRejectedValueOnce(new Error("Unavailable"))
    .mockResolvedValueOnce({ hasPassword: false, googleLinked: true });
  render(<AccountPassword email="member@example.com" />);
  expect(screen.queryByLabelText("Mật khẩu hiện tại")).toBeNull();
  expect(await screen.findByRole("alert")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Thử lại" }));
  await screen.findByRole("heading", {
    name: "Thiết lập đăng nhập bằng mật khẩu",
  });
  await waitFor(() => expect(api).toHaveBeenCalledTimes(2));
});
