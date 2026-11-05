import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { icon, localInput, toUtc, dateFmt, timeFmt } from "./ui.jsx";

export function EisenhowerQuickCreate({
  task,
  quadrant,
  columns,
  busy,
  error,
  onSave,
  onClose,
}) {
  const dialog = useRef(null),
    nameField = useRef(null);
  const requestId = useRef(crypto.randomUUID());
  const [name, setName] = useState(task?.name || ""),
    [description, setDescription] = useState(task?.description || "");
  const [deadline, setDeadline] = useState(
    task?.deadline ? localInput(task.deadline) : "",
  );
  const [group, setGroup] = useState(
    task?.quadrant || (quadrant?.startsWith("Q") ? quadrant : "Unknown"),
  );
  const [dateOpen, setDateOpen] = useState(false),
    [priorityOpen, setPriorityOpen] = useState(false),
    [validation, setValidation] = useState("");
  const current = columns.find((column) => column.id === group);
  let dueLabel = "Hạn chót";
  if (deadline) {
    try {
      const value = toUtc(deadline);
      dueLabel = `${dateFmt(value)} · ${timeFmt(value)}`;
    } catch {
      dueLabel = "Kiểm tra ngày giờ";
    }
  }
  useEffect(() => {
    const opener = document.activeElement,
      node = dialog.current;
    node.showModal();
    nameField.current?.focus();
    return () => {
      if (node.open) node.close();
      if (opener?.isConnected) opener.focus({ preventScroll: true });
    };
  }, []);
  return createPortal(
    <dialog
      ref={dialog}
      className="eis-quick-dialog"
      aria-label={task ? "Sửa task nháp" : "Thêm task vào ma trận"}
      onCancel={(event) => {
        event.preventDefault();
        if (!busy) onClose();
      }}
      onClick={(event) => {
        if (event.target === event.currentTarget && !busy) {
          const box = event.currentTarget.getBoundingClientRect();
          if (
            event.clientX < box.left ||
            event.clientX > box.right ||
            event.clientY < box.top ||
            event.clientY > box.bottom
          )
            onClose();
        }
      }}
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (!name.trim() || busy) return;
          try {
            onSave({
              name: name.trim(),
              description,
              deadline: deadline ? toUtc(deadline) : null,
              quadrant: group,
              requestId: requestId.current,
            });
          } catch (failure) {
            setValidation(failure.message);
          }
        }}
      >
        <div className="eis-quick-topbar">
          <button
            type="button"
            className={`eis-quick-due${deadline ? " has-date" : ""}`}
            disabled={busy}
            aria-expanded={dateOpen}
            onClick={() => {
              setDateOpen((value) => !value);
              setPriorityOpen(false);
            }}
          >
            {icon("calendar")}
            {dueLabel}
          </button>
          <button
            type="button"
            className={`eis-quick-priority tone-${current?.tone || "unknown"}`}
            disabled={busy}
            aria-label={`Vị trí ưu tiên: ${current?.label || "Cần làm rõ"}`}
            title={current?.caption || "Chưa phân loại"}
            aria-expanded={priorityOpen}
            onClick={() => {
              setPriorityOpen((value) => !value);
              setDateOpen(false);
            }}
          >
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M5 21V3m0 1h14l-3 5 3 5H5" />
            </svg>
          </button>
          <button
            type="button"
            className="eis-quick-close"
            aria-label="Đóng cửa sổ"
            disabled={busy}
            onClick={onClose}
          >
            {icon("close")}
          </button>
        </div>
        {dateOpen && (
          <div className="eis-quick-date-panel">
            <label>
              Hạn chót
              <input
                type="datetime-local"
                value={deadline}
                aria-label="Hạn chót"
                disabled={busy}
                onChange={(event) => setDeadline(event.target.value)}
              />
            </label>
            <div>
              <button
                type="button"
                disabled={busy}
                onClick={() => {
                  setDeadline("");
                  setDateOpen(false);
                }}
              >
                Không có ngày hạn
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => setDateOpen(false)}
              >
                Xong
              </button>
            </div>
          </div>
        )}
        {priorityOpen && (
          <div
            className="eis-quick-priority-panel"
            role="group"
            aria-label="Chọn vị trí ưu tiên"
          >
            {columns
              .filter(
                (column) =>
                  column.id.startsWith("Q") || column.id === "Unknown",
              )
              .map((column) => (
                <button
                  key={column.id}
                  type="button"
                  className={`tone-${column.tone}`}
                  disabled={busy}
                  aria-pressed={group === column.id}
                  onClick={() => {
                    setGroup(column.id);
                    setPriorityOpen(false);
                  }}
                >
                  {column.caption || column.label}
                </button>
              ))}
          </div>
        )}
        <div className="eis-quick-body">
          <label className="sr-only" htmlFor="eis-quick-name">
            Tên công việc
          </label>
          <input
            id="eis-quick-name"
            ref={nameField}
            required
            maxLength={120}
            placeholder="Bạn muốn làm gì?"
            value={name}
            disabled={busy}
            onChange={(event) => setName(event.target.value)}
          />
          <label className="sr-only" htmlFor="eis-quick-description">
            Mô tả
          </label>
          <textarea
            id="eis-quick-description"
            maxLength={4000}
            rows={6}
            placeholder="Thêm mô tả…"
            value={description}
            disabled={busy}
            onChange={(event) => setDescription(event.target.value)}
          />
          {(error || validation) && (
            <p className="eis-board-error" role="alert">
              {error || validation}
            </p>
          )}
        </div>
        <footer>
          <span>{icon("mail")} Inbox</span>
          <button type="submit" disabled={busy || !name.trim()}>
            {busy ? "Đang lưu…" : task ? "Lưu" : "Thêm task"}
          </button>
        </footer>
      </form>
    </dialog>,
    document.body,
  );
}
