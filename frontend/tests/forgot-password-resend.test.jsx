import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
import {
  ForgotPasswordResend,
  markForgotPasswordOtpSent,
} from "../src/forgot-password-resend.jsx";
import { api } from "../src/api.js";
import { toast } from "../src/ui.jsx";

vi.mock("../src/api.js", () => ({ api: vi.fn() }));
vi.mock("../src/ui.jsx", () => ({ toast: vi.fn() }));

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-10-24T05:00:00Z"));
  vi.mocked(api).mockReset();
  vi.mocked(toast).mockReset();
});
afterEach(() => vi.useRealTimers());

function mount(email) {
  return render(
    <div className="auth-panel-content">
      <input aria-label="Mã OTP" name="otp" defaultValue="123456" />
      <div className="form-error">Mã đã hết hạn.</div>
      <ForgotPasswordResend email={email} />
    </div>,
  );
}

it("waits after the first email, prevents duplicate sends, and resets the code and cooldown", async () => {
  const email = "reset@example.test";
  markForgotPasswordOtpSent(email);
  mount(email);
  const button = screen.getByRole("button", { name: "Gửi lại OTP (60s)" });
  expect(button.disabled).toBe(true);
  fireEvent.click(button);
  expect(api).not.toHaveBeenCalled();

  await act(() => vi.advanceTimersByTimeAsync(60_000));
  expect(button.textContent).toBe("Gửi lại OTP");
  expect(button.disabled).toBe(false);
  let complete;
  vi.mocked(api).mockImplementation(
    () =>
      new Promise((resolve) => {
        complete = resolve;
      }),
  );
  fireEvent.click(button);
  fireEvent.click(button);
  expect(button.textContent).toBe("Đang gửi…");
  expect(button.disabled).toBe(true);
  expect(api).toHaveBeenCalledExactlyOnceWith(
    "/api/auth/forgot-password",
    "POST",
    { email },
  );
  await act(async () => complete({}));
  expect(screen.getByLabelText("Mã OTP").value).toBe("");
  expect(document.activeElement).toBe(screen.getByLabelText("Mã OTP"));
  expect(document.querySelector(".form-error").textContent).toBe("");
  expect(button.textContent).toBe("Gửi lại OTP (60s)");
  expect(button.disabled).toBe(true);
  expect(toast).toHaveBeenCalledWith(
    expect.stringContaining("Nếu email có tài khoản"),
  );
});

it("keeps the entered code after a failed request and allows retry", async () => {
  mount("retry@example.test");
  vi.mocked(api).mockRejectedValueOnce(new Error("Không thể kết nối."));
  const button = screen.getByRole("button", { name: "Gửi lại OTP" });
  await act(async () => fireEvent.click(button));
  expect(toast).toHaveBeenCalledWith("Không thể kết nối.", "error");
  expect(screen.getByLabelText("Mã OTP").value).toBe("123456");
  expect(button.disabled).toBe(false);
  vi.mocked(api).mockResolvedValueOnce({});
  await act(async () => fireEvent.click(button));
  expect(api).toHaveBeenCalledTimes(2);
  expect(button.disabled).toBe(true);
  expect(screen.getByLabelText("Mã OTP").value).toBe("");
});

it("retains the remaining cooldown when returning to the OTP screen", async () => {
  markForgotPasswordOtpSent("return@example.test");
  const view = mount("return@example.test");
  await act(() => vi.advanceTimersByTimeAsync(20_000));
  view.unmount();
  mount("return@example.test");
  expect(
    screen.getByRole("button", { name: "Gửi lại OTP (40s)" }).disabled,
  ).toBe(true);
  expect(api).not.toHaveBeenCalled();
});
