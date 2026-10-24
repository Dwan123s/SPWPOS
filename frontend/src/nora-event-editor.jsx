import { useOutsideDismiss } from "./popup-dismissal.jsx";
import { useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { api } from "./api.js";
import { icon, localInput, state, toUtc } from "./ui.jsx";

export function NoraEventEditor({ event, onCancel, onSaved }) {
  const dialog = useRef(null),
    nameField = useRef(null);
  const [name, setName] = useState(event.name);
  const [start, setStart] = useState(() => localInput(event.start));
  const [end, setEnd] = useState(() => localInput(event.end));
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  useOutsideDismiss([dialog], onCancel, !busy);
  useLayoutEffect(() => {
    const previous = document.activeElement,
      node = dialog.current;
    node.showModal?.();
    nameField.current?.focus();
    return () => {
      node.close?.();
      if (previous?.isConnected) previous.focus?.();
    };
  }, []);
  return createPortal(
    <dialog
      ref={dialog}
      className="nora-delete-dialog nora-event-dialog"
      aria-labelledby="nora-event-edit-title"
      open={
        typeof HTMLDialogElement.prototype.showModal !== "function"
          ? true
          : undefined
      }
      onCancel={(event) => {
        event.preventDefault();
        if (!busy) onCancel();
      }}
      onKeyDown={(event) => {
        event.stopPropagation();
        if (event.key === "Escape") {
          event.preventDefault();
          if (!busy) onCancel();
        }
        if (event.key === "Tab") {
          const controls = [
              ...event.currentTarget.querySelectorAll(
                "button:not(:disabled),input:not(:disabled)",
              ),
            ],
            first = controls[0],
            last = controls.at(-1);
          if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last?.focus();
          } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first?.focus();
          }
        }
      }}
    >
      <div className="nora-delete-dialog-heading">
        <h2 id="nora-event-edit-title">Sửa sự kiện</h2>
        <button
          className="nora-delete-dialog-close"
          type="button"
          disabled={busy}
          aria-label="Đóng sửa sự kiện"
          onClick={onCancel}
        >
          {icon("close")}
        </button>
      </div>
      <p>Chỉnh tên hoặc thời gian. Các thông tin khác được giữ nguyên.</p>
      <form
        onSubmit={async (submit) => {
          submit.preventDefault();
          submit.stopPropagation();
          if (busy) return;
          setError("");
          try {
            const submitted = new FormData(submit.currentTarget);
            const savedName = String(submitted.get("name") || "").trim();
            const from = toUtc(String(submitted.get("start") || "")),
              to = toUtc(String(submitted.get("end") || ""));
            if (!savedName || new Date(to) <= new Date(from))
              throw new Error("Nhập tên và giờ kết thúc sau giờ bắt đầu.");
            setBusy(true);
            await api(`/api/time/events/${event.id}`, "PUT", {
              name: savedName,
              start: from,
              end: to,
              description: event.description || "",
              eventType: event.eventType || "OTHER",
              repeatWeeks: 1,
              isUrgent: event.isUrgent ?? null,
            });
            await onSaved();
          } catch (failure) {
            setError(failure.message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <label>
          Tên sự kiện
          <input
            ref={nameField}
            name="name"
            required
            maxLength={200}
            value={name}
            disabled={busy}
            onChange={(event) => setName(event.target.value)}
          />
        </label>
        <label>
          Bắt đầu
          <input
            type="datetime-local"
            name="start"
            required
            value={start}
            disabled={busy}
            onChange={(event) => setStart(event.target.value)}
          />
        </label>
        <label>
          Kết thúc
          <input
            type="datetime-local"
            name="end"
            required
            value={end}
            disabled={busy}
            onChange={(event) => setEnd(event.target.value)}
          />
        </label>
        <small>
          Múi giờ:{" "}
          {state.workspace?.preference?.timeZoneId || "Asia/Ho_Chi_Minh"}
        </small>
        {error && (
          <p role="alert" className="nora-delete-dialog-error">
            {error}
          </p>
        )}
        <div className="nora-delete-dialog-actions">
          <button type="button" disabled={busy} onClick={onCancel}>
            Hủy
          </button>
          <button type="submit" className="is-primary" disabled={busy}>
            {busy ? "Đang lưu…" : "Lưu thay đổi"}
          </button>
        </div>
      </form>
    </dialog>,
    document.body,
  );
}
