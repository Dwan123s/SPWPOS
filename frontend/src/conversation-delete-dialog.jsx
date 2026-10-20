import { useOutsideDismiss } from "./popup-dismissal.jsx";
import { useId, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { icon } from "./ui.jsx";
import "./styles/confirmation-dialog.css";

export function ConversationDeleteDialog({
  title,
  busy,
  error,
  onCancel,
  onConfirm,
  headingText = "Xóa cuộc trò chuyện?",
  descriptionContent,
  containerSelector = ".agent-main",
}) {
  const dialog = useRef(null);
  const cancel = useRef(null);
  const heading = useId();
  const description = useId();
  const [position, setPosition] = useState({});
  useOutsideDismiss([dialog], onCancel, !busy);
  useLayoutEffect(() => {
    const previous = document.activeElement;
    const node = dialog.current;
    function place() {
      const area = document
        .querySelector(containerSelector)
        ?.getBoundingClientRect();
      const width = Math.min(420, window.innerWidth - 32);
      setPosition({
        left: Math.max(
          16,
          Math.min(
            window.innerWidth - width - 16,
            (area ? area.left + area.width / 2 : window.innerWidth / 2) -
              width / 2,
          ),
        ),
        top: Math.max(
          16,
          Math.min(
            window.innerHeight - node.offsetHeight - 16,
            (area
              ? (Math.max(0, area.top) +
                  Math.min(window.innerHeight, area.bottom)) /
                2
              : window.innerHeight / 2) -
              node.offsetHeight / 2,
          ),
        ),
        width,
      });
    }
    if (node.showModal) node.showModal();
    place();
    cancel.current?.focus();
    window.addEventListener("resize", place);
    const observer =
      typeof ResizeObserver === "function" ? new ResizeObserver(place) : null;
    observer?.observe(node);
    return () => {
      observer?.disconnect();
      window.removeEventListener("resize", place);
      node.close?.();
      if (previous?.isConnected) previous.focus?.();
    };
  }, []);
  return createPortal(
    <dialog
      ref={dialog}
      open={
        typeof HTMLDialogElement.prototype.showModal !== "function"
          ? true
          : undefined
      }
      className="nora-delete-dialog"
      style={position}
      role="alertdialog"
      aria-modal="true"
      aria-labelledby={heading}
      aria-describedby={description}
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
            ...event.currentTarget.querySelectorAll("button:not(:disabled)"),
          ];
          const first = controls[0],
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
        <h2 id={heading}>{headingText}</h2>
        <button
          type="button"
          className="nora-delete-dialog-close"
          aria-label="Đóng xác nhận xóa"
          disabled={busy}
          onClick={onCancel}
        >
          {icon("close")}
        </button>
      </div>
      <p id={description}>
        {descriptionContent || (
          <>
            Cuộc trò chuyện <strong>{title}</strong> sẽ bị xóa vĩnh viễn. Bạn
            không thể hoàn tác thao tác này. Các mục đã lưu trong lịch vẫn được
            giữ.
          </>
        )}
      </p>
      {error && (
        <p className="nora-delete-dialog-error" role="alert">
          {error}
        </p>
      )}
      <div className="nora-delete-dialog-actions">
        <button ref={cancel} type="button" disabled={busy} onClick={onCancel}>
          Hủy
        </button>
        <button
          type="button"
          className="is-danger"
          disabled={busy}
          onClick={onConfirm}
        >
          {busy ? "Đang xóa…" : "Xóa vĩnh viễn"}
        </button>
      </div>
    </dialog>,
    document.body,
  );
}
