import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { api } from "./api.js";
import { dateFmt, timeFmt, icon, state } from "./ui.jsx";
import { startNoraReminderAlarm } from "./notification-sound.js";
import "./styles/nora-reminders.css";

export const refreshNoraReminders = () =>
  window.dispatchEvent(new Event("nora-reminders-changed"));

function NoraAlarmWindow({ children }) {
  const dialog = useRef(null);
  useEffect(() => {
    const node = dialog.current,
      previous = document.activeElement;
    node.showModal();
    node.querySelector("button")?.focus();
    return () => {
      if (node.open) node.close();
      if (previous?.isConnected) previous.focus({ preventScroll: true });
    };
  }, []);
  return createPortal(
    <dialog
      ref={dialog}
      className="nora-reminder-alarm"
      role="alertdialog"
      aria-modal="true"
      aria-labelledby="nora-alarm-title"
      aria-describedby="nora-alarm-description"
      onCancel={(event) => event.preventDefault()}
    >
      {children}
    </dialog>,
    document.body,
  );
}

// Keep alarms accurate across route changes. The server stores notifications;
// this clock only requests their feed when due, never creates them in the browser.
export function NoraReminderClock({ userId }) {
  const [due, setDue] = useState([]);
  const [acknowledged, setAcknowledged] = useState(() => {
    try {
      return new Set(
        JSON.parse(
          localStorage.getItem(`planora.nora-reminders.ack.${userId}`) || "[]",
        ),
      );
    } catch {
      return new Set();
    }
  });
  const ackRef = useRef(acknowledged);
  const ackKey = `planora.nora-reminders.ack.${userId}`;
  const active = due.filter((row) => !acknowledged.has(row.id));
  const ringing = active.length > 0;
  useEffect(() => {
    const synchronize = (event) => {
      if (event.key !== ackKey) return;
      try {
        const next = new Set(JSON.parse(event.newValue || "[]"));
        ackRef.current = next;
        setAcknowledged(next);
      } catch {
        /* Keep the current acknowledgement on invalid storage. */
      }
    };
    window.addEventListener("storage", synchronize);
    return () => window.removeEventListener("storage", synchronize);
  }, [ackKey]);
  useEffect(() => {
    if (ringing && userId) return startNoraReminderAlarm(userId);
  }, [ringing, userId]);
  function dismiss() {
    const next = new Set([...ackRef.current, ...active.map((row) => row.id)]);
    ackRef.current = next;
    setAcknowledged(next);
    try {
      localStorage.setItem(ackKey, JSON.stringify([...next].slice(-500)));
    } catch {
      /* Local acknowledgement still stops the alarm. */
    }
    for (const row of active)
      void api(`/api/notifications/${row.id}/read`, "POST")
        .then(() => {
          window.dispatchEvent(new Event("nora-reminder-due"));
        })
        .catch(() => {
          /* Retried on the next refresh; the local dismissal remains. */
        });
    refreshNoraReminders();
  }
  useEffect(() => {
    if (!userId) return;
    let stopped = false,
      busy = false,
      alarm,
      queued = false;
    const refresh = async () => {
      if (stopped) return;
      if (busy) {
        queued = true;
        return;
      }
      busy = true;
      try {
        const rows = await api("/api/agent/reminders");
        if (stopped || !Array.isArray(rows)) return;
        setDue(
          state.workspace?.notificationSettings?.inAppEnabled === false
            ? []
            : rows.filter((row) => row.due || Date.parse(row.at) <= Date.now()),
        );
        for (const row of rows.filter((row) => ackRef.current.has(row.id)))
          await api(`/api/notifications/${row.id}/read`, "POST").catch(
            () => {},
          );
        clearTimeout(alarm);
        const next = rows
          .map((row) => Date.parse(row.at))
          .filter((at) => at > Date.now())
          .sort((a, b) => a - b)[0];
        if (next)
          alarm = setTimeout(
            () => {
              if (stopped) return;
              if (state.workspace?.notificationSettings?.inAppEnabled !== false)
                setDue((current) => [
                  ...new Map(
                    [
                      ...current,
                      ...rows.filter((row) => Date.parse(row.at) <= Date.now()),
                    ].map((row) => [row.id, row]),
                  ).values(),
                ]);
              window.dispatchEvent(new Event("nora-reminder-due"));
              void refresh();
            },
            Math.min(86400000, Math.max(1, next - Date.now() + 50)),
          );
      } catch {
        /* The durable server notification will be retried by polling. */
      } finally {
        busy = false;
        if (queued && !stopped) {
          queued = false;
          void refresh();
        }
      }
    };
    const visible = () => {
      if (!document.hidden) void refresh();
    };
    void refresh();
    const interval = setInterval(visible, 60000);
    window.addEventListener("nora-reminders-changed", refresh);
    window.addEventListener("workspace-updated", visible);
    window.addEventListener("focus", visible);
    document.addEventListener("visibilitychange", visible);
    return () => {
      stopped = true;
      clearInterval(interval);
      clearTimeout(alarm);
      window.removeEventListener("nora-reminders-changed", refresh);
      window.removeEventListener("workspace-updated", visible);
      window.removeEventListener("focus", visible);
      document.removeEventListener("visibilitychange", visible);
    };
  }, [userId]);
  return ringing && userId ? (
    <NoraAlarmWindow>
      <span className="nora-alarm-bell" aria-hidden="true">
        {icon("bell")}
      </span>
      <div className="nora-alarm-content">
        <h2 id="nora-alarm-title">
          NORA nhắc bạn
          {active.length > 1 ? ` · ${active.length} lời nhắc` : ""}
        </h2>
        <div id="nora-alarm-description">
          {active.map((row) => (
            <p key={row.id}>{row.message}</p>
          ))}
        </div>
        <span className="nora-alarm-caption">
          Đã đến giờ. Bấm Tắt chuông khi bạn đã nhận lời nhắc.
        </span>
        <button type="button" className="btn primary" onClick={dismiss}>
          {icon("check")} Tắt chuông
        </button>
      </div>
    </NoraAlarmWindow>
  ) : null;
}

function RemindersDialog({ onClose, onCompose }) {
  const dialog = useRef(null),
    mounted = useRef(true);
  const [rows, setRows] = useState([]),
    [loading, setLoading] = useState(true),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(null);
  async function load() {
    try {
      const data = await api("/api/agent/reminders");
      if (mounted.current) {
        setRows(Array.isArray(data) ? data : []);
        setError("");
      }
    } catch (failure) {
      if (mounted.current) setError(failure.message);
    } finally {
      if (mounted.current) setLoading(false);
    }
  }
  useEffect(() => {
    mounted.current = true;
    const node = dialog.current,
      opener = document.activeElement;
    node.showModal();
    void load();
    const refresh = () => void load();
    const interval = setInterval(refresh, 20000);
    window.addEventListener("nora-reminders-changed", refresh);
    window.addEventListener("nora-reminder-due", refresh);
    return () => {
      mounted.current = false;
      clearInterval(interval);
      window.removeEventListener("nora-reminders-changed", refresh);
      window.removeEventListener("nora-reminder-due", refresh);
      if (node.open) node.close();
      if (opener?.isConnected) opener.focus({ preventScroll: true });
    };
  }, []);
  async function cancel(row) {
    if (busy) return;
    setBusy(row.id);
    setError("");
    try {
      await api(`/api/agent/reminders/${row.id}`, "DELETE");
      if (mounted.current)
        setRows((current) => current.filter((item) => item.id !== row.id));
      refreshNoraReminders();
      window.dispatchEvent(new Event("nora-reminder-due"));
    } catch (failure) {
      if (mounted.current) setError(failure.message);
    } finally {
      if (mounted.current) setBusy(null);
    }
  }
  return createPortal(
    <dialog
      ref={dialog}
      className="nora-reminders-dialog"
      aria-labelledby="nora-reminders-title"
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClick={(event) => {
        if (event.target !== event.currentTarget) return;
        const box = event.currentTarget.getBoundingClientRect();
        if (
          event.clientX < box.left ||
          event.clientX > box.right ||
          event.clientY < box.top ||
          event.clientY > box.bottom
        )
          onClose();
      }}
    >
      <header>
        <h2 id="nora-reminders-title">{icon("bell")} Lời nhắc của bạn</h2>
        <button
          type="button"
          className="btn ghost icon-only"
          aria-label="Đóng lời nhắc"
          onClick={onClose}
        >
          {icon("close")}
        </button>
      </header>
      <p>
        Nói với NORA nội dung và giờ cần nhắc. Ví dụ: “1 phút nữa nhắc tôi trả
        lời email khách hàng”.
      </p>
      <p>
        NORA báo trên web và gửi qua email, Telegram nếu bạn đã bật hoặc kết nối
        các kênh đó.
      </p>
      {error && (
        <p role="alert">
          {error}
          <button type="button" className="btn ghost small" onClick={load}>
            Thử lại
          </button>
        </p>
      )}
      {loading ? (
        <p role="status">Đang tải lời nhắc…</p>
      ) : rows.length ? (
        <ul>
          {rows.map((row) => (
            <li key={row.id}>
              <div>
                <strong>{row.message}</strong>
                <span>
                  {row.due ? "Đã đến giờ" : "Sắp nhắc"} · {timeFmt(row.at)} ·{" "}
                  {dateFmt(row.at)}
                </span>
              </div>
              <button
                type="button"
                className="btn ghost small"
                disabled={!!busy}
                aria-label={`Hủy nhắc: ${row.message}`}
                onClick={() => cancel(row)}
              >
                {busy === row.id ? "Đang hủy…" : "Hủy nhắc"}
              </button>
            </li>
          ))}
        </ul>
      ) : (
        !error && <p className="nora-reminders-empty">Chưa có lời nhắc nào.</p>
      )}
      <footer>
        <button
          type="button"
          className="btn primary"
          onClick={() => {
            onClose();
            onCompose("Nhắc tôi ");
          }}
        >
          {icon("plus")} Đặt lời nhắc với NORA
        </button>
      </footer>
    </dialog>,
    document.body,
  );
}

export function NoraReminders({ onCompose }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        type="button"
        className="nora-plan-toggle nora-reminders-launcher"
        aria-label="Lời nhắc"
        title="Lời nhắc NORA"
        aria-haspopup="dialog"
        aria-expanded={open}
        onClick={() => setOpen(true)}
      >
        {icon("bell")}
        <span>Lời nhắc</span>
      </button>
      {open && (
        <RemindersDialog onClose={() => setOpen(false)} onCompose={onCompose} />
      )}
    </>
  );
}
