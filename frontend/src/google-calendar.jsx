import { useCallback, useEffect, useState } from "react";
import { api } from "./api.js";
import "./styles/google-calendar.css";

const root = "/api/integrations/google-calendar";
const labels = {
  NotConfigured: "Chưa cấu hình máy chủ",
  Disconnected: "Chưa kết nối",
  Connected: "Đã kết nối",
  Synced: "Đã đồng bộ",
  Pending: "Có thay đổi chờ gửi",
  Syncing: "Đang đồng bộ",
  Conflict: "Có lịch cần bạn chọn",
  Reconnect: "Cần kết nối lại",
  Retrying: "Đang chờ thử lại",
  SettingUp: "Cần kiểm tra bước tạo lịch",
};
const callbackMessages = {
  connected: "Đã kết nối Google. PLANORA đang đồng bộ lịch của bạn.",
  CALENDAR_CONSENT_DENIED:
    "Bạn chưa cấp quyền Calendar. Có thể kết nối lại khi sẵn sàng.",
  CALENDAR_OAUTH_STATE: "Liên kết đã dùng hoặc hết hạn. Hãy bấm kết nối lại.",
  CALENDAR_ACCOUNT_MISMATCH:
    "Hãy chọn đúng tài khoản Google đã kết nối trước đó.",
  CALENDAR_OFFLINE_ACCESS:
    "Google chưa cấp quyền đồng bộ nền. Hãy kết nối lại và cấp quyền.",
  CALENDAR_SCOPE_REQUIRED:
    "Google chưa cấp quyền Calendar. Bấm Kết nối Google Calendar, rồi tích ô cho phép PLANORA tạo và quản lý lịch trước khi bấm Tiếp tục. Bật API trong Google Cloud chưa thay thế bước cấp quyền này.",
  CALENDAR_CLIENT_REJECTED:
    "Google từ chối cấu hình kết nối của máy chủ. Cần kiểm tra lại OAuth Client ID và Client Secret.",
  CALENDAR_CODE_EXPIRED:
    "Mã cấp quyền Google đã hết hạn hoặc đã được dùng. Hãy bấm Kết nối Google Calendar để bắt đầu lại.",
  CALENDAR_CALLBACK_MISMATCH:
    "Địa chỉ quay về PLANORA chưa khớp cấu hình OAuth. Cần kiểm tra lại redirect URI trên Google Cloud và máy chủ.",
  CALENDAR_IDENTITY_REQUIRED:
    "Chưa xác nhận được tài khoản Google. Hãy kết nối lại và cấp quyền đăng nhập cùng quyền Calendar.",
  CALENDAR_ORGANIZATION_ONLY:
    "Ứng dụng OAuth đang giới hạn trong tổ chức Google Workspace. Cần kiểm tra Audience để dùng tài khoản Gmail cá nhân.",
  CALENDAR_NETWORK_ERROR:
    "Máy chủ chưa kết nối được tới Google hoặc phản hồi quá lâu. Hãy thử kết nối lại sau ít phút.",
  CALENDAR_NOT_CONFIGURED:
    "Máy chủ chưa bật cấu hình Google Calendar. Cần kiểm tra cấu hình và khởi động lại API.",
  CALENDAR_PROVIDER_ERROR:
    "Google tạm thời chưa hoàn tất kết nối. Hãy thử lại sau ít phút.",
};
function describe(snapshot) {
  if (!snapshot) return "Đã xóa / hủy";
  const format = (value) =>
    new Intl.DateTimeFormat("vi-VN", {
      timeZone: "Asia/Ho_Chi_Minh",
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    }).format(new Date(value));
  return `${snapshot.title} · ${format(snapshot.start)} – ${format(snapshot.end)}`;
}

export function GoogleCalendarCard() {
  const [status, setStatus] = useState(null);
  const [conflicts, setConflicts] = useState([]);
  const [reminder, setReminder] = useState(15);
  const [atStart, setAtStart] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(() => {
    const result = new URLSearchParams(window.location.search).get(
      "googleCalendar",
    );
    return result && result !== "connected"
      ? callbackMessages[result] ||
          "Chưa kết nối được Google Calendar. Hãy bấm kết nối để thử lại."
      : "";
  });
  const [notice, setNotice] = useState(() => {
    const result = new URLSearchParams(window.location.search).get(
      "googleCalendar",
    );
    return result === "connected" ? callbackMessages.connected : "";
  });
  const refresh = useCallback(async (initial = false) => {
    const result = await api(`${root}/status`);
    setStatus(result);
    if (initial) {
      setReminder(result.reminderMinutes);
      setAtStart(result.remindAtStart);
    }
    setConflicts(result.conflicts ? await api(`${root}/conflicts`) : []);
  }, []);
  useEffect(() => {
    let active = true;
    // Remove the OAuth result from the URL after displaying it. Tokens/codes
    // never reach this page; the backend callback exchanges them first.
    if (new URLSearchParams(window.location.search).has("googleCalendar")) {
      const url = new URL(window.location.href);
      url.searchParams.delete("googleCalendar");
      history.replaceState(null, "", url.pathname + url.search + url.hash);
    }
    void refresh(true).catch((failure) => {
      if (active) setError(failure.message);
    });
    const timer = setInterval(() => {
      if (active && !document.hidden) void refresh().catch(() => {});
    }, 15000);
    return () => {
      active = false;
      clearInterval(timer);
    };
  }, [refresh]);
  async function act(operation, message) {
    if (busy) return;
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await operation();
      if (message) setNotice(message);
      await refresh();
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section
      className="card settings-content-card google-calendar-card"
      aria-label="Kết nối Google Calendar"
    >
      <div className="card-header">
        <div>
          <h2>Google Calendar</h2>
          <p>Lịch của bạn trên PLANORA và điện thoại luôn cùng một nhịp.</p>
        </div>
        <span
          className={`badge ${status?.status === "Synced" ? "green" : "blue"}`}
          role="status"
        >
          {status ? labels[status.status] || status.status : "Đang kiểm tra…"}
        </span>
      </div>
      <p>
        Đồng bộ hai chiều trong lịch riêng <strong>PLANORA</strong>: tạo, đổi
        giờ hoặc xóa ở bên nào cũng được cập nhật sang bên còn lại.
      </p>
      {status && !status.configured && (
        <p className="calendar-help">
          Tính năng đang chờ quản trị viên cấu hình Google Calendar.
        </p>
      )}
      {status?.connected && (
        <div className="calendar-connection-info">
          <strong>{status.email}</strong>
          <span>
            {status.lastSyncedAt
              ? `Đồng bộ gần nhất: ${new Date(status.lastSyncedAt).toLocaleString("vi-VN")}`
              : "Đang chuẩn bị đồng bộ lần đầu…"}
          </span>
          {status.pendingChanges > 0 && (
            <span>{status.pendingChanges} thay đổi đang chờ gửi</span>
          )}
          <span>
            {status.webhookEnabled
              ? "Tự động cập nhật khi lịch thay đổi"
              : "Tự động kiểm tra thay đổi khoảng mỗi phút"}
          </span>
        </div>
      )}
      <div className="calendar-reminder-options">
        <label className="field">
          Nhắc trước giờ bắt đầu (phút)
          <input
            type="number"
            min="0"
            max="40320"
            value={reminder}
            disabled={busy}
            onChange={(event) => setReminder(event.target.value)}
          />
        </label>
        <label className="calendar-check">
          <input
            type="checkbox"
            checked={atStart}
            disabled={busy}
            onChange={(event) => setAtStart(event.target.checked)}
          />
          Nhắc thêm đúng giờ bắt đầu
        </label>
      </div>
      <p className="calendar-help">
        Áp dụng cho sự kiện mới. Lời nhắc riêng bạn đã chỉnh trên Google sẽ được
        giữ. Trên điện thoại, bật đồng bộ lịch PLANORA và cho phép Google
        Calendar gửi thông báo.
      </p>
      {status?.lastError && (
        <p role="alert" className="calendar-error">
          {status.lastError}
        </p>
      )}
      {error && (
        <p role="alert" className="calendar-error">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="calendar-notice">
          {notice}
        </p>
      )}
      <div className="calendar-connection-actions">
        {(!status?.connected ||
          ["Reconnect", "SettingUp"].includes(status?.status)) && (
          <button
            type="button"
            className="btn primary"
            disabled={!status?.configured || busy || reminder === ""}
            onClick={() =>
              act(async () => {
                const result = await api(`${root}/connect`, "POST", {
                  reminderMinutes: Number(reminder),
                  remindAtStart: atStart,
                });
                const target = new URL(result.authorizationUrl);
                if (target.origin !== "https://accounts.google.com")
                  throw new Error("Địa chỉ cấp quyền Google không hợp lệ.");
                window.location.assign(target.href);
              })
            }
          >
            {status?.connected
              ? "Kết nối lại Google"
              : "Kết nối Google Calendar"}
          </button>
        )}
        {status?.connected && (
          <>
            <button
              type="button"
              className="btn primary"
              disabled={busy || status.status === "Reconnect"}
              onClick={() =>
                act(
                  () => api(`${root}/sync`, "POST"),
                  "Đã yêu cầu đồng bộ. Những thay đổi đang chờ sẽ được gửi khi Google phản hồi.",
                )
              }
            >
              Đồng bộ ngay
            </button>
            <button
              type="button"
              className="btn"
              disabled={busy || reminder === ""}
              onClick={() =>
                act(
                  () =>
                    api(`${root}/reminders`, "PUT", {
                      reminderMinutes: Number(reminder),
                      remindAtStart: atStart,
                    }),
                  "Đã lưu giờ nhắc cho sự kiện mới.",
                )
              }
            >
              Lưu giờ nhắc
            </button>
            <a
              className="btn"
              href={status.calendarUrl}
              target="_blank"
              rel="noopener noreferrer"
            >
              Mở Google Calendar
            </a>
            <button
              type="button"
              className="btn ghost danger"
              disabled={busy}
              onClick={() =>
                act(
                  () => api(`${root}/disconnect`, "POST"),
                  "Đã ngắt kết nối. Các sự kiện trên Google vẫn được giữ.",
                )
              }
            >
              Ngắt kết nối
            </button>
          </>
        )}
      </div>
      {conflicts.length > 0 && (
        <div className="calendar-sync-conflicts">
          <h3>Hai bên đang có thay đổi khác nhau</h3>
          <p>
            Chọn phiên bản bạn muốn giữ. Lịch còn lại sẽ được cập nhật theo lựa
            chọn này.
          </p>
          {conflicts.map((conflict) => (
            <article key={conflict.id} className="calendar-sync-conflict">
              <p>
                <strong>Trước khi sửa:</strong> {describe(conflict.baseline)}
              </p>
              <p>
                <strong>PLANORA:</strong> {describe(conflict.local)}
              </p>
              <p>
                <strong>Google:</strong> {describe(conflict.google)}
              </p>
              {conflict.readOnly && (
                <p>
                  Lịch lặp và sự kiện cả ngày được chỉnh trên Google Calendar.
                </p>
              )}
              {conflict.canKeepGoogle === false && (
                <p>
                  {conflict.readOnly
                    ? "Cấu trúc sự kiện đã đổi thành chuỗi. Hãy khôi phục sự kiện đơn trên Google hoặc tạo chuỗi mới riêng."
                    : "Phiên này đã có ghi nhận thực hiện. Giữ PLANORA để bảo toàn lịch sử phiên."}
                </p>
              )}
              <div className="calendar-connection-actions">
                {!conflict.readOnly && (
                  <button
                    type="button"
                    className="btn"
                    disabled={busy}
                    onClick={() =>
                      act(
                        () =>
                          api(
                            `${root}/conflicts/${conflict.id}/resolve`,
                            "POST",
                            { keep: "local" },
                          ),
                        "Đang giữ phiên bản PLANORA và đồng bộ sang Google.",
                      )
                    }
                  >
                    Giữ PLANORA
                  </button>
                )}
                <button
                  type="button"
                  className="btn"
                  disabled={busy || conflict.canKeepGoogle === false}
                  onClick={() =>
                    act(
                      () =>
                        api(
                          `${root}/conflicts/${conflict.id}/resolve`,
                          "POST",
                          { keep: "google" },
                        ),
                      "Đang lấy phiên bản Google về PLANORA.",
                    )
                  }
                >
                  Giữ Google
                </button>
              </div>
            </article>
          ))}
        </div>
      )}
      <p className="calendar-help">
        Để thêm lịch bằng điện thoại, chọn đúng lịch PLANORA khi tạo sự kiện.
        Ngắt kết nối sẽ dừng đồng bộ và giữ các sự kiện đã có trên Google.
      </p>
    </section>
  );
}
