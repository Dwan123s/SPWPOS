import { useLayoutEffect, useRef } from "react";
import { icon } from "./ui.jsx";

export function TeamDialog({
  title,
  subtitle,
  children,
  onClose,
  busy,
  wide = false,
  className = "",
}) {
  const host = useRef(null);
  useLayoutEffect(() => {
    const el = host.current;
    const opener = document.activeElement;
    el.showModal();
    return () => {
      if (el.open) el.close();
      if (opener?.isConnected) opener.focus({ preventScroll: true });
    };
  }, []);
  return (
    <dialog
      ref={host}
      className={`team-dialog${wide ? " team-dialog-wide" : ""} ${className}`}
      aria-label={title}
      onCancel={(e) => {
        e.preventDefault();
        if (!busy) onClose();
      }}
    >
      <header className="team-dialog-header">
        <div>
          <span className="team-eyebrow">PLANORA WORKSPACE</span>
          <h2>{title}</h2>
          {subtitle && <p>{subtitle}</p>}
        </div>
        <button
          type="button"
          className="team-icon-button"
          aria-label="Đóng cửa sổ"
          disabled={busy}
          onClick={onClose}
        >
          {icon("close")}
        </button>
      </header>
      {children}
    </dialog>
  );
}
