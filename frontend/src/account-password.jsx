import { useEffect, useState } from "react";
import { api } from "./api.js";
import { button, field, icon, PasswordField } from "./ui.jsx";
import { Loading } from "./loading.jsx";
import { PasswordChecklist } from "./password-checklist.jsx";

export function AccountPassword({ email }) {
  const [methods, setMethods] = useState(null);
  const [error, setError] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    let active = true;
    setError(false);
    api("/api/auth/login-methods")
      .then((data) => {
        if (typeof data?.hasPassword !== "boolean")
          throw new Error("Missing account methods");
        if (active) setMethods(data);
      })
      .catch(() => {
        if (active) setError(true);
      });
    return () => {
      active = false;
    };
  }, [attempt]);
  if (error)
    return (
      <div className="stack">
        <p role="alert">Chưa tải được phương thức đăng nhập của tài khoản.</p>
        <button
          className="btn secondary"
          type="button"
          onClick={() => setAttempt((value) => value + 1)}
        >
          Thử lại
        </button>
      </div>
    );
  if (!methods) return <Loading label="Đang tải phương thức đăng nhập…" />;
  const setup = !methods.hasPassword;
  return (
    <>
      <div className="card-header">
        <div>
          <h2>
            {setup
              ? "Thiết lập đăng nhập bằng mật khẩu"
              : "Đổi mật khẩu tài khoản"}
          </h2>
          <p>
            {setup
              ? "Tạo mật khẩu để đăng nhập bằng email hoặc Google vào cùng tài khoản, công việc và lịch hiện tại."
              : "Yêu cầu mã xác thực OTP gửi về Email để bảo vệ tài khoản của bạn."}
          </p>
        </div>
      </div>
      <div className="security-steps-banner">
        <div className="step-box">
          <span className="step-num">1</span>
          <div>
            <strong>Bước 1: Yêu cầu mã OTP</strong>
            <p>
              Mã xác minh 6 chữ số sẽ được gửi tới Email: <b>{email}</b>
            </p>
          </div>
          {button("Gửi mã OTP", "change-otp", "secondary small", "", "shield")}
        </div>
      </div>
      <form
        data-form="password"
        data-password-mode={setup ? "setup" : "change"}
      >
        <div className="form-grid">
          {!setup && (
            <div className="wide">
              {field(
                "Mật khẩu hiện tại",
                "currentPassword",
                "password",
                "",
                'required maxlength="128" autocomplete="current-password"',
              )}
            </div>
          )}
          <div className="wide">
            {field(
              "Mã OTP xác nhận từ Email",
              "otp",
              "text",
              "",
              'required pattern="[0-9]{6,8}" maxlength="8" inputmode="numeric" autocomplete="one-time-code" placeholder="Nhập 6 chữ số OTP"',
            )}
          </div>
          {field(
            "Mật khẩu mới",
            "newPassword",
            "password",
            "",
            'required minlength="8" maxlength="32" aria-describedby="password-requirements" autocomplete="new-password"',
          )}
          <PasswordField
            label="Nhập lại mật khẩu mới"
            name="confirmNewPassword"
            confirmFor="newPassword"
            extra='required minlength="8" maxlength="32" autocomplete="new-password"'
          />
        </div>
        <PasswordChecklist passwordName="newPassword" />
        <div className="form-error" role="alert" />
        <p className="muted">
          {setup
            ? "Sau khi thiết lập, hãy đăng nhập lại bằng email và mật khẩu mới hoặc bằng Google."
            : "Sau khi đổi mật khẩu, hãy đăng nhập lại."}
        </p>
        <div className="form-actions right-align">
          <button type="submit" className="btn primary">
            {icon("lock")}
            {setup ? " Thiết lập mật khẩu" : " Đổi mật khẩu ngay"}
          </button>
        </div>
      </form>
    </>
  );
}
