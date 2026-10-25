import { useEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { toast } from "./ui.jsx";

const cooldowns = new Map();
const emailKey = (email) => email.trim().toLowerCase();
export function markForgotPasswordOtpSent(email) {
  const until = Date.now() + 60_000;
  cooldowns.set(emailKey(email), until);
  return until;
}

export function ForgotPasswordResend({ email }) {
  const [now, setNow] = useState(Date.now);
  const [busy, setBusy] = useState(false);
  const pending = useRef(false);
  const remaining = Math.max(
    0,
    Math.ceil(((cooldowns.get(emailKey(email)) || 0) - now) / 1000),
  );
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);

  async function resend(event) {
    if (pending.current || remaining > 0) return;
    const panel = event.currentTarget.closest(".auth-panel-content");
    pending.current = true;
    setBusy(true);
    try {
      await api("/api/auth/forgot-password", "POST", { email });
      markForgotPasswordOtpSent(email);
      setNow(Date.now());
      const otp = panel?.querySelector('input[name="otp"]');
      if (otp) {
        otp.value = "";
        otp.focus();
      }
      const error = panel?.querySelector(".form-error");
      if (error) error.textContent = "";
      toast(
        "Nếu email có tài khoản, mã xác minh sẽ được gửi. Hãy dùng mã OTP mới nhất.",
      );
    } catch (error) {
      toast(error.message, "error");
    } finally {
      pending.current = false;
      setBusy(false);
    }
  }

  return (
    <p className="auth-resend">
      Chưa nhận được mã?{" "}
      <button
        type="button"
        className="text-btn"
        disabled={busy || remaining > 0}
        onClick={resend}
      >
        {busy
          ? "Đang gửi…"
          : remaining > 0
            ? `Gửi lại OTP (${remaining}s)`
            : "Gửi lại OTP"}
      </button>
    </p>
  );
}
