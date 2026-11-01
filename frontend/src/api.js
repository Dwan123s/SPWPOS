import { toast } from "./ui.jsx";
import { createSessionRefresh } from "./session-refresh.js";
const refreshSession = createSessionRefresh({
  locks: globalThis.navigator?.locks,
  probe: () => api("/api/browser-session/me", "GET", undefined, false),
  renew: () => api("/api/browser-session/refresh", "POST", undefined, false),
});
export async function api(
  path,
  method = "GET",
  body = undefined,
  retry = true,
) {
  const response = await fetch(path, {
    method,
    credentials: "same-origin",
    headers: {
      "X-SPWPOS": "1",
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  const publicAuth =
    /^\/api\/auth\/(register|verify-email|resend-verification-otp|login|forgot-password|verify-forgot-password-otp|reset-password)$/.test(
      path,
    );
  if (
    response.status === 401 &&
    retry &&
    !path.startsWith("/api/browser-session/refresh") &&
    !path.startsWith("/api/browser-session/login") &&
    !path.startsWith("/api/browser-session/firebase/login") &&
    !publicAuth
  ) {
    try {
      await refreshSession();
    } catch (error) {
      if (error.status === 401) {
        window.dispatchEvent(new Event("session-expired"));
        const expired = new Error(
          "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
        );
        expired.status = 401;
        expired.code = "SESSION_EXPIRED";
        throw expired;
      }
      throw error;
    }
    return api(path, method, body, false);
  }
  let json;
  try {
    json = await response.json();
  } catch {
    json = {};
  }
  if (!response.ok) {
    const e = new Error(
      json.error?.message || "Không thể thực hiện yêu cầu. Hãy thử lại.",
    );
    e.code = json.error?.code;
    e.status = response.status;
    throw e;
  }
  return json.data;
}
export async function submit(form, operation) {
  const btn = form.querySelector('button[type="submit"]');
  if (btn) btn.disabled = true;
  let error = form.querySelector(".form-error");
  if (error) error.textContent = "";
  try {
    return await operation(new FormData(form));
  } catch (e) {
    if (error) error.textContent = e.message;
    else toast(e.message, "error");
    throw e;
  } finally {
    if (btn && btn.isConnected) btn.disabled = form.dataset.stale === "true";
  }
}
export async function downloadPrivateFile(path, filename, retry = true) {
  const response = await fetch(path, {
    credentials: "same-origin",
    headers: { "X-SPWPOS": "1" },
  });
  if (response.status === 401 && retry) {
    await refreshSession();
    return downloadPrivateFile(path, filename, false);
  }
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(
      body.error?.message ||
        "Không tải được file. Hãy đăng nhập lại hoặc thử lại.",
    );
  }
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
