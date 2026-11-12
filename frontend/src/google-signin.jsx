import { useEffect, useState } from "react";
import { api } from "./api.js";
import { modal } from "./ui.jsx";
import { googleErrorMessage, prepareGoogleClient } from "./firebase-google.js";

function GoogleMark() {
  return (
    <svg className="google-mark" viewBox="0 0 24 24" aria-hidden="true">
      <path
        fill="#4285F4"
        d="M21.6 12.23c0-.71-.06-1.39-.18-2.04H12v3.85h5.38a4.6 4.6 0 0 1-1.99 3.01v2.5h3.22c1.88-1.73 2.99-4.28 2.99-7.32Z"
      />
      <path
        fill="#34A853"
        d="M12 22c2.7 0 4.96-.89 6.61-2.45l-3.22-2.5c-.9.6-2.04.96-3.39.96-2.6 0-4.8-1.76-5.59-4.12H3.09v2.58A10 10 0 0 0 12 22Z"
      />
      <path
        fill="#FBBC05"
        d="M6.41 13.89a6 6 0 0 1 0-3.78V7.53H3.09a10 10 0 0 0 0 8.94l3.32-2.58Z"
      />
      <path
        fill="#EA4335"
        d="M12 5.99c1.47 0 2.79.5 3.82 1.49l2.87-2.87A9.6 9.6 0 0 0 12 2a10 10 0 0 0-8.91 5.53l3.32 2.58C7.2 7.75 9.4 5.99 12 5.99Z"
      />
    </svg>
  );
}

export function GoogleSignIn({ link = false, onSuccess }) {
  const [config, setConfig] = useState(null);
  const [client, setClient] = useState(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    let active = true;
    api("/api/browser-session/firebase/config", "GET", undefined, false)
      .then(async (data) => {
        if (!active) return;
        setConfig(data || { enabled: false });
        if (data?.enabled && !(link && data.linked)) {
          const ready = await prepareGoogleClient(data.config);
          if (active) setClient(ready);
        }
      })
      .catch(() => {
        if (active) {
          setConfig({ enabled: false });
          setError(
            "Đăng nhập Google hiện chưa khả dụng. Bạn vẫn có thể dùng email và mật khẩu.",
          );
        }
      });
    return () => {
      active = false;
    };
  }, [link]);
  const linked = link && config?.linked;
  return (
    <div className={`google-signin ${link ? "google-signin--link" : ""}`}>
      <button
        type="button"
        className="btn google-signin-button"
        disabled={!client || busy || linked}
        aria-busy={busy}
        onClick={async () => {
          if (!client || busy) return;
          setBusy(true);
          setError("");
          try {
            const idToken = await client.signIn();
            await api(
              `/api/browser-session/firebase/${link ? "link" : "login"}`,
              "POST",
              { idToken },
              false,
            );
            if (link) setConfig((previous) => ({ ...previous, linked: true }));
            await onSuccess?.();
          } catch (failure) {
            if (failure.code === "ACCOUNT_DISABLED") {
              modal(
                "Tài khoản bị khóa",
                "Thông báo từ hệ thống",
                <div className="account-locked-popup">
                  <div style={{ textAlign: "center", padding: "1.25rem 0.5rem" }}>
                    <div
                      style={{
                        display: "inline-flex",
                        alignItems: "center",
                        justifyContent: "center",
                        width: "56px",
                        height: "56px",
                        borderRadius: "50%",
                        backgroundColor: "#fee2e2",
                        color: "#dc2626",
                        marginBottom: "1rem",
                      }}
                    >
                      <svg
                        width="28"
                        height="28"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      >
                        <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                        <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                      </svg>
                    </div>
                    <p
                      style={{
                        fontSize: "1.05rem",
                        fontWeight: 600,
                        color: "#b91c1c",
                        lineHeight: "1.6",
                        margin: "0.5rem 0",
                      }}
                    >
                      {failure.message}
                    </p>
                    <p
                      style={{
                        color: "#000",
                        fontSize: "0.875rem",
                        marginTop: "0.75rem",
                        lineHeight: "1.5",
                      }}
                    >
                      Mọi quyền truy cập vào tài khoản đã bị tạm dừng. Nếu bạn có thắc mắc hoặc cần hỗ trợ, vui lòng liên hệ quản trị viên qua email hệ thống.
                    </p>
                  </div>
                  <div
                    style={{
                      display: "flex",
                      justifyContent: "center",
                      marginTop: "1rem",
                    }}
                  >
                    <button
                      type="button"
                      className="btn secondary"
                      data-action="close-modal"
                    >
                      Đã hiểu
                    </button>
                  </div>
                </div>,
              );
            } else {
              setError(googleErrorMessage(failure));
            }
          } finally {
            await client.signOut().catch(() => {});
            setBusy(false);
          }
        }}
      >
        {busy ? (
          <span className="loading-spinner" aria-hidden="true" />
        ) : (
          <GoogleMark />
        )}
        {busy
          ? "Đang xác thực Google…"
          : linked
            ? "Đã liên kết Google"
            : link
              ? "Liên kết Google"
              : "Đăng nhập với Google"}
      </button>
      {config && !config.enabled && !error && (
        <p className="google-signin-hint">
          Đăng nhập Google hiện chưa khả dụng.
        </p>
      )}
      {link && config?.hasPassword === false && (
        <p className="google-signin-hint">
          Bạn đang dùng Google. Để tạo mật khẩu cho cùng tài khoản, dùng phần
          “Thiết lập đăng nhập bằng mật khẩu” bên dưới.
        </p>
      )}
      {error && (
        <p className="google-signin-error" role="alert">
          {error}
        </p>
      )}
      {linked && (
        <p className="google-signin-success" role="status">
          Bạn có thể đăng nhập bằng Google vào cùng tài khoản và dữ liệu này.
        </p>
      )}
    </div>
  );
}

export function GoogleAccountCard() {
  return (
    <section className="google-account-card" aria-label="Tài khoản Google">
      <h2>Đăng nhập bằng Google</h2>
      <p>Liên kết Google để đăng nhập nhanh vào tài khoản hiện tại.</p>
      <GoogleSignIn link />
    </section>
  );
}
