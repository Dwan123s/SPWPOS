import { useEffect, useState } from "react";
import { api } from "./api.js";
import { icon } from "./ui.jsx";
import "./styles/telegram.css";

const root = "/api/integrations/telegram";
export function TelegramCard() {
  const [status, setStatus] = useState(null);
  const [link, setLink] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    async function refresh() {
      try {
        const result = await api(`${root}/status`);
        if (active) {
          setStatus(result);
          if (result.connected && !result.linkExpiresAt) setLink(null);
          setError("");
        }
      } catch (failure) { if (active) setError(failure.message); }
    }
    void refresh();
    const timer = setInterval(() => { if (!document.hidden) void refresh(); }, 5000);
    window.addEventListener("focus", refresh);
    return () => { active = false; clearInterval(timer); window.removeEventListener("focus", refresh); };
  }, []);
  async function act(operation) {
    if (busy) return;
    setBusy(true); setError("");
    try {
      await operation();
      setStatus(await api(`${root}/status`));
    } catch (failure) { setError(failure.message); }
    finally { setBusy(false); }
  }
  const expired = link && new Date(link.expiresAt).getTime() <= Date.now();
  return (
    <section className="card settings-content-card telegram-card" aria-label="Thông báo Telegram">
      <div className="telegram-heading">
        <span className="telegram-mark" aria-hidden="true">{icon("bell")}</span>
        <div><h2>Nhắc lịch qua Telegram</h2><p>Lịch học, phiên làm việc, deadline và cảnh báo lịch được gửi đến Telegram riêng của bạn, kể cả khi đóng PLANORA.</p></div>
        <span className={`badge ${status?.connected && status?.enabled ? "green" : ""}`}>
          {!status ? "Đang kiểm tra" : !status.configured ? "Chưa cấu hình" : status.connected ? status.enabled ? "Đang bật" : "Đã tạm dừng" : "Chưa kết nối"}
        </span>
      </div>
      {error && <p role="alert" className="form-error">{error}</p>}
      {status && !status.configured && <p className="telegram-hint">{status.configurationError === "MISSING_BOT_TOKEN"
        ? "Telegram đã bật nhưng máy chủ chưa lưu token của bot. Quản trị viên cần bổ sung token trước khi kết nối."
        : status.configurationError ? "Cấu hình Telegram trên máy chủ chưa hợp lệ. Quản trị viên cần kiểm tra lại trước khi kết nối."
        : "Quản trị viên cần bật bot Telegram trên máy chủ trước khi bạn kết nối."}</p>}
      {status?.connected && <p className="telegram-hint">Đã kết nối với <strong>{status.displayName || "tài khoản Telegram của bạn"}</strong>. Chỉ gửi thông báo cá nhân vào cuộc trò chuyện riêng với bot.</p>}
      {status?.errorCode && <p className="telegram-hint" role="status">Bot chưa gửi được tin nhắn. Hãy bỏ chặn bot, kết nối lại và nhấn Start để tiếp tục nhận nhắc lịch.</p>}
      {link && status?.linkExpiresAt && (
        <div className="telegram-connect-steps" role="status">
          <strong>{expired ? "Liên kết đã hết hạn" : "Hoàn tất kết nối trong Telegram"}</strong>
          <p>{expired ? "Bấm tạo lại liên kết để tiếp tục." : "Bấm mở bot bên dưới, rồi nhấn Start. PLANORA sẽ tự cập nhật khi kết nối xong. Liên kết có hiệu lực 10 phút."}</p>
          {!expired && <a className="btn primary" href={link.url} target="_blank" rel="noopener noreferrer">Mở bot Telegram · Nhấn Start</a>}
        </div>
      )}
      <div className="telegram-actions">
        {status?.configured && (!status.connected || status.errorCode || link) && <button type="button" className="btn primary" disabled={busy} onClick={() => act(async () => setLink(await api(`${root}/connect`, "POST")))}>
          {busy ? "Đang kết nối…" : link ? "Tạo lại liên kết" : status.connected ? "Kết nối lại Telegram" : "Kết nối Telegram"}
        </button>}
        {status?.configured && status.connected && !status.errorCode && <button type="button" className="btn secondary" disabled={busy} onClick={() => act(() => api(`${root}/preferences`, "PUT", { enabled: !status.enabled }))}>
          {status.enabled ? "Tạm dừng nhắc Telegram" : "Bật lại nhắc Telegram"}
        </button>}
        {(status?.connected || status?.linkExpiresAt) && <button type="button" className="btn ghost" disabled={busy} onClick={() => act(async () => { await api(root, "DELETE"); setLink(null); })}>Ngắt kết nối Telegram</button>}
      </div>
      <small>Thời gian nhắc dùng chung với phần cài đặt thông báo. Bạn cũng có thể gửi /stop cho bot để ngắt kết nối.</small>
    </section>
  );
}
