import { useEffect, useState } from "react";
import { api } from "./api.js";
import { icon } from "./ui.jsx";
import { secretaryMode } from "./agent-handoff.js";
const statusLabels = {
  PROPOSED: "Chờ bạn xem",
  BLOCKED: "Cần thêm thời gian",
  STALE: "Cần tạo lại",
  NEEDS_DECISION: "Cần quyết định deadline",
  APPLIED: "Đã áp dụng",
  DISMISSED: "Đã bỏ qua",
};
const time = (minute) =>
  `${String(Math.floor(minute / 60)).padStart(2, "0")}:${String(minute % 60).padStart(2, "0")}`;
const minute = (text) =>
  Number(text.split(":")[0]) * 60 + Number(text.split(":")[1]);
export function RecoveryPanel({ onWorkspaceChanged }) {
  const [data, setData] = useState(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [pending, setPending] = useState(true);
  const [enabled, setEnabled] = useState(true);
  const [page, setPage] = useState(1);
  const [refresh, setRefresh] = useState(0);
  useEffect(() => {
    let active = true;
    setPending(true);
    api(`/api/recovery?page=${page}`)
      .then((value) => {
        if (!Array.isArray(value?.items) || !value.settings)
          throw new Error("Chưa tải được đề xuất phục hồi.");
        if (active) {
          if (
            page > 1 &&
            !value.items.length &&
            value.total <= (page - 1) * 20
          ) {
            setPage(Math.max(1, Math.ceil(value.total / 20)));
            return;
          }
          setData(value);
          setEnabled(value.settings.enabled);
          setError("");
        }
      })
      .catch((e) => {
        if (active) setError(e.message);
      })
      .finally(() => {
        if (active) setPending(false);
      });
    return () => {
      active = false;
    };
  }, [page, refresh]);
  async function action(path, body, method = "POST") {
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      await api(path, method, body);
      setRefresh((x) => x + 1);
      await onWorkspaceChanged?.();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section
      className="card section-gap recovery-panel"
      aria-label="Phục hồi kế hoạch"
      aria-busy={pending || busy}
    >
      <div className="card-header">
        <div>
          <h2>
            <span className="recovery-symbol" aria-hidden="true">
              {icon("refresh")}
            </span>
            Lấy lại nhịp làm việc
          </h2>
          <p>
            Gợi ý lịch mới khi bạn lỡ {data?.settings.missedDays ?? 2} ngày đã
            lên lịch. Ngày nghỉ không có phiên không bị tính trễ.
          </p>
        </div>
        <button
          type="button"
          className="btn secondary"
          disabled={busy || pending || data?.settings.enabled === false}
          onClick={() => action("/api/recovery/scan")}
        >
          {busy ? "Đang cập nhật…" : "Kiểm tra kế hoạch"}
        </button>
      </div>
      {error && (
        <p role="alert" className="inline-feedback is-error">
          {error}{" "}
          <button
            className="btn ghost small"
            type="button"
            disabled={busy || pending}
            onClick={() => setRefresh((x) => x + 1)}
          >
            Tải lại
          </button>
        </p>
      )}
      {!data && pending && (
        <p role="status" className="inline-feedback">
          Đang kiểm tra đề xuất của bạn…
        </p>
      )}
      {data && (
        <>
          {!secretaryMode() && <details className="recovery-settings">
            <summary>
              Cài đặt phục hồi{" "}
              <span>
                {data.settings.enabled
                  ? data.settings.autoApply
                    ? "Tự áp dụng"
                    : "Chỉ đề xuất"
                  : "Đã tắt"}
              </span>
            </summary>
            <form
              className="recovery-settings-form"
              key={data.settings.version}
              onSubmit={(e) => {
                e.preventDefault();
                const f = new FormData(e.currentTarget);
                action(
                  "/api/recovery/settings",
                  {
                    enabled: f.has("enabled"),
                    autoApply: f.has("enabled") && f.has("autoApply"),
                    missedDays: Number(f.get("missedDays")),
                    quietStartMinute: minute(f.get("quietStart")),
                    quietEndMinute: minute(f.get("quietEnd")),
                    version: data.settings.version,
                  },
                  "PUT",
                );
              }}
            >
              <label>
                <input
                  type="checkbox"
                  name="enabled"
                  checked={enabled}
                  disabled={busy}
                  onChange={(e) => setEnabled(e.target.checked)}
                />{" "}
                Tạo đề xuất phục hồi
              </label>
              <label>
                Số ngày đã lên lịch bị lỡ{" "}
                <input
                  type="number"
                  name="missedDays"
                  min="1"
                  max="14"
                  required
                  defaultValue={data.settings.missedDays}
                />
              </label>
              <label>
                <input
                  type="checkbox"
                  name="autoApply"
                  defaultChecked={data.settings.autoApply}
                  disabled={!enabled || busy}
                />{" "}
                Tự áp dụng khi đủ thời gian và không có cảnh báo
              </label>
              <p className="reason">
                Mặc định chỉ đề xuất. Nếu bật tự áp dụng, hệ thống có thể đổi
                các phiên chưa khóa; luôn giữ phiên đang chạy, deadline và giờ
                nghỉ. Deadline cứng đã qua cần bạn quyết định.
              </p>
              <label>
                Giờ yên lặng từ{" "}
                <input
                  name="quietStart"
                  type="time"
                  required
                  defaultValue={time(data.settings.quietStartMinute)}
                />
              </label>
              <label>
                đến{" "}
                <input
                  name="quietEnd"
                  type="time"
                  required
                  defaultValue={time(data.settings.quietEndMinute)}
                />
              </label>
              <p className="reason">
                Theo múi giờ tài khoản. Hai giờ bằng nhau nghĩa là không có giờ
                yên lặng.
              </p>
              <button
                type="submit"
                className="btn primary"
                disabled={busy || pending}
              >
                Lưu cài đặt
              </button>
            </form>
          </details>}
          {!data.items.length && (
            <div className="recovery-empty">
              <span className="recovery-empty-icon" aria-hidden="true">
                {icon("check")}
              </span>
              <div>
                <strong>
                  {data.settings.enabled
                    ? "Bạn đang giữ nhịp tốt"
                    : "Phục hồi tự động đang tắt"}
                </strong>
                <p>
                  {data.settings.enabled
                    ? "Chưa có công việc cần phục hồi. Lịch của bạn vẫn được giữ nguyên."
                    : "Bật tạo đề xuất trong cài đặt khi bạn muốn được hỗ trợ sắp xếp lại."}
                </p>
              </div>
            </div>
          )}
          {data.items.map((row) => (
            <article
              key={row.id}
              className="recovery-proposal"
              data-status={row.status}
            >
              <h3>
                <span className="badge">
                  {statusLabels[row.status] || row.status}
                </span>
              </h3>
              <p>{row.message}</p>
              <p className="reason">
                {row.taskIds.length} công việc bị lỡ ·{" "}
                {new Date(row.createdAt).toLocaleString("vi-VN")}
              </p>
              {row.plan && (
                <details>
                  <summary>
                    Xem {row.plan.sessions.filter((s) => !s.preserved).length}{" "}
                    phiên đề xuất
                  </summary>
                  <p>Phiên đang chạy hoặc đã khóa được giữ lại.</p>
                  <ul>
                    {row.plan.sessions
                      .filter((s) => !s.preserved)
                      .map((s) => (
                        <li key={s.id}>
                          {new Date(s.start).toLocaleString("vi-VN")} –{" "}
                          {new Date(s.end).toLocaleTimeString("vi-VN")}
                        </li>
                      ))}
                  </ul>
                  {row.plan.warnings.map((warning, index) => (
                    <p key={index} className="warning">
                      {warning.taskName}: {warning.message}
                      {warning.unscheduledMinutes > 0
                        ? ` Thiếu ${warning.unscheduledMinutes} phút.`
                        : ""}
                    </p>
                  ))}
                </details>
              )}
              {row.status === "PROPOSED" && (
                <button
                  className="btn secondary small"
                  type="button"
                  disabled={busy}
                  onClick={() => action(`/api/recovery/${row.id}/apply`)}
                >
                  Áp dụng đề xuất
                </button>
              )}
              {["STALE", "BLOCKED", "NEEDS_DECISION"].includes(row.status) && (
                <button
                  className="btn secondary small"
                  type="button"
                  disabled={busy}
                  onClick={() => action(`/api/recovery/${row.id}/rebuild`)}
                >
                  Tạo lại với lịch hiện tại
                </button>
              )}
              {!["APPLIED", "DISMISSED"].includes(row.status) && (
                <button
                  className="btn secondary small"
                  type="button"
                  disabled={busy}
                  onClick={() => action(`/api/recovery/${row.id}/dismiss`)}
                >
                  Bỏ qua đề xuất
                </button>
              )}
            </article>
          ))}
          {data.total > 20 && (
            <nav className="pagination" aria-label="Phân trang phục hồi">
              <button
                className="btn secondary small"
                type="button"
                disabled={busy || page === 1}
                onClick={() => setPage((x) => x - 1)}
              >
                Trước
              </button>
              <span>
                {" "}
                Trang {page} · {data.total} đề xuất{" "}
              </span>
              <button
                className="btn secondary small"
                type="button"
                disabled={busy || page * 20 >= data.total}
                onClick={() => setPage((x) => x + 1)}
              >
                Sau
              </button>
            </nav>
          )}
        </>
      )}
    </section>
  );
}
