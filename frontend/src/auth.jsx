import { authView } from "./auth-view.jsx";
import { $, state, field, toast, PasswordField, icon } from "./ui.jsx";
import { api, submit } from "./api.js";
import { setScreen } from "./store.js";
import { transitionAuthPanel } from "./auth-panel-motion.js";
import { validateNewPassword } from "./password-policy.js";
import { PasswordChecklist } from "./password-checklist.jsx";
import { notifyLoginPasswordRejected } from "./password-login-feedback.js";
let currentMode = "login",
  onLogin = () => {};
export function authSetup(callback) {
  onLogin = callback;
}
export function renderAuth(mode = "login") {
  const titles = {
    login: [
      "Đăng nhập vào PLANORA",
      "Đăng nhập để tiếp tục kế hoạch làm việc của bạn.",
    ],
    register: [
      "Tạo tài khoản PLANORA",
      "Điền thông tin và xác minh email để bắt đầu.",
    ],
    verify: [
      "Xác minh email",
      "Nhập mã xác nhận 6 số được gửi đến hộp thư của bạn.",
    ],
    forgot: [
      "Tìm tài khoản của bạn",
      "Nhập email đã đăng ký để nhận mã xác minh đặt lại mật khẩu.",
    ],
    forgotOtp: [
      "Xác minh mã OTP",
      "Mã xác minh có hiệu lực trong vòng 5 phút.",
    ],
    reset: [
      "Đặt mật khẩu mới",
      "Thiết lập mật khẩu an toàn mới cho tài khoản của bạn.",
    ],
  };
  const buttons = {
    login: "Đăng nhập",
    register: "Tạo tài khoản",
    verify: "Xác minh email",
    forgot: "Gửi mã xác minh",
    forgotOtp: "Xác minh mã",
    reset: "Đặt mật khẩu mới",
  };
  const fields = (
    <>
      {mode === "register" && (
        <div className="form-grid">
          {field(
            "Tên",
            "firstName",
            "text",
            "",
            'required maxlength="100" autocomplete="given-name" placeholder="Tên của bạn"',
          )}
          {field(
            "Họ",
            "lastName",
            "text",
            "",
            'required maxlength="100" autocomplete="family-name" placeholder="Họ của bạn"',
          )}
        </div>
      )}
      {mode === "login" ? (
        <>
          <label className="field">
            <span className="auth-visually-hidden">Email</span>
            <div className="input-with-action login-input-shell">
              <span className="login-input-icon" aria-hidden="true">
                {icon("mail")}
              </span>
              <input
                type="email"
                name="email"
                defaultValue={state.authEmail}
                required
                maxLength={254}
                autoComplete="email"
                placeholder="Địa chỉ email"
              />
            </div>
          </label>
          <PasswordField
            label="Mật khẩu"
            hiddenLabel
            leadingIcon="lock"
            loginFeedback
            name="password"
            extra={
              'required maxlength="128" autocomplete="current-password" placeholder="Mật khẩu"'
            }
          />
        </>
      ) : (
        mode !== "reset" &&
        field(
          "Email",
          "email",
          "email",
          state.authEmail,
          'required maxlength="254" autocomplete="email" placeholder="Nhập Email của bạn"',
        )
      )}
      {["register", "reset"].includes(mode) && (
        <>
          {field(
            "Mật khẩu" + (mode === "reset" ? " mới" : ""),
            "password",
            "password",
            "",
            'required minlength="8" maxlength="32" aria-describedby="password-requirements" autocomplete="new-password" placeholder="Mật khẩu (8–32 ký tự)"',
          )}
          <PasswordField
            label="Nhập lại mật khẩu"
            name="confirmPassword"
            confirmFor="password"
            extra='required minlength="8" maxlength="32" autocomplete="new-password" placeholder="Nhập lại mật khẩu"'
          />
          <PasswordChecklist />
        </>
      )}
      {["verify", "forgotOtp"].includes(mode) && (
        <div className="auth-otp">
          {field(
            "Mã OTP",
            "otp",
            "text",
            "",
            'required inputmode="numeric" pattern="[0-9]{6,8}" minlength="6" maxlength="8" autocomplete="one-time-code" placeholder="Mã xác thực"',
          )}
        </div>
      )}
    </>
  );
  const [title, description] = titles[mode] || titles.login;
  transitionAuthPanel(() => {
    currentMode = mode;
    setScreen(
      authView(mode, title, description, fields, buttons, onLogin),
      `auth-${mode}`,
    );
    $("#app input")?.focus({ preventScroll: true });
  }, mode);
}
export async function handleAuthAction(action) {
  if (!action.startsWith("auth-")) return false;
  const email = $('input[name="email"]');
  if (email) state.authEmail = email.value;
  if (action === "auth-resend") {
    try {
      await api("/api/auth/resend-verification-otp", "POST", {
        email: state.authEmail,
      });
      toast("Nếu tài khoản cần xác minh, mã mới sẽ được gửi.");
    } catch (e) {
      toast(e.message, "error");
    }
    return true;
  }
  renderAuth(action.slice(5));
  return true;
}
export async function handleAuthSubmit(form) {
  if (form.dataset.form !== "auth") return false;
  try {
    await submit(form, async (data) => {
      const email = String(data.get("email") || state.authEmail);
      state.authEmail = email;
      const password = String(data.get("password") || "");
      if (["register", "reset"].includes(currentMode)) {
        validateNewPassword(password, email);
        if (password !== String(data.get("confirmPassword") || "")) {
          throw new Error("Mật khẩu xác nhận không khớp.");
        }
      }
      switch (currentMode) {
        case "login":
          await api(
            "/api/browser-session/login",
            "POST",
            {
              email,
              password,
            },
            false,
          );
          await onLogin();
          break;
        case "register":
          await api("/api/auth/register", "POST", {
            email,
            password,
            confirmPassword: String(data.get("confirmPassword")),
            firstName: String(data.get("firstName")),
            lastName: String(data.get("lastName")),
          });
          renderAuth("verify");
          toast("Kiểm tra hộp thư để lấy mã xác minh.");
          break;
        case "verify":
          await api("/api/auth/verify-email", "POST", {
            email,
            otp: String(data.get("otp")),
          });
          renderAuth("login");
          toast("Email đã xác minh. Bạn có thể đăng nhập.", "success");
          break;
        case "forgot":
          await api("/api/auth/forgot-password", "POST", {
            email,
          });
          renderAuth("forgotOtp");
          toast("Nếu email có tài khoản, mã xác minh sẽ được gửi.");
          break;
        case "forgotOtp":
          state.resetToken = (
            await api("/api/auth/verify-forgot-password-otp", "POST", {
              email,
              otp: String(data.get("otp")),
            })
          ).resetToken;
          renderAuth("reset");
          break;
        case "reset":
          await api("/api/auth/reset-password", "POST", {
            resetToken: state.resetToken,
            newPassword: password,
            confirmNewPassword: String(data.get("confirmPassword")),
          });
          state.resetToken = null;
          renderAuth("login");
          toast("Đã đặt lại mật khẩu. Hãy đăng nhập lại.", "success");
          break;
      }
    });
  } catch (e) {
    if (form.dataset.mode === "login" && e.code === "INVALID_CREDENTIALS") {
      notifyLoginPasswordRejected(form);
    }
    if (e.code === "EMAIL_NOT_VERIFIED") {
      renderAuth("verify");
      toast("Xác minh email trước khi đăng nhập.");
    }
  }
  return true;
}
