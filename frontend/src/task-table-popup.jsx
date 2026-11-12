import { createPortal } from "react-dom";
import { useEffect, useId, useLayoutEffect, useRef, useState } from "react";
import { useOutsideDismiss } from "./popup-dismissal.jsx";
import { dayKey, icon, minutes } from "./ui.jsx";
import { addCalendarDays } from "./task-smart-lists.js";
import "./task-table-popup.css";

export function TaskTablePopup({ ref: forwardedRef, anchor, kind, title, onClose, children }) {
  const localRef = useRef(null);
  const root = forwardedRef ?? localRef;
  const trigger = useRef(anchor);
  useOutsideDismiss([root, trigger], () => onClose(false));
  useLayoutEffect(() => {
    const node = root.current;
    function position() {
      const rect = anchor.getBoundingClientRect();
      const width = node.offsetWidth;
      const height = node.offsetHeight;
      const below = rect.bottom + 6;
      node.style.left = `${Math.max(8, Math.min(rect.right - width, innerWidth - width - 8))}px`;
      node.style.top = `${below + height <= innerHeight - 8 ? below : Math.max(8, rect.top - height - 6)}px`;
    }
    position();
    (node.querySelector("[data-popup-autofocus]") || node).focus({ preventScroll: true });
    const observer = typeof ResizeObserver === "function" ? new ResizeObserver(position) : null;
    observer?.observe(node);
    window.addEventListener("resize", position);
    window.addEventListener("scroll", position, true);
    return () => {
      observer?.disconnect();
      window.removeEventListener("resize", position);
      window.removeEventListener("scroll", position, true);
    };
  }, [anchor, root]);
  return createPortal(
    <section ref={root} className={`task-table-popup is-${kind}`}
      role="dialog" aria-label={title} tabIndex={-1}
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          event.stopPropagation();
          onClose();
        }
      }}>
      {children}
    </section>, document.body,
  );
}

function usePopupSave(onSave, onClose) {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);
  async function save(value) {
    if (pending) return;
    setPending(true);
    setError("");
    try {
      await onSave(value);
      if (mounted.current) onClose();
    } catch (failure) {
      if (mounted.current) setError(failure.message);
    } finally {
      if (mounted.current) setPending(false);
    }
  }
  return { pending, error, save };
}

const monthFormat = new Intl.DateTimeFormat("vi-VN", { timeZone: "UTC", month: "long", year: "numeric" });
const dayFormat = new Intl.DateTimeFormat("vi-VN", { timeZone: "UTC", dateStyle: "full" });
const utcDate = (day) => new Date(`${day}T12:00:00Z`);

export function TaskDeadlinePicker({ deadline, onSave, onClose }) {
  const today = dayKey(Date.now());
  const original = deadline ? dayKey(deadline) : "";
  const initialDay = original || today;
  const [selected, setSelected] = useState(initialDay);
  const [month, setMonth] = useState(`${initialDay.slice(0, 7)}-01`);
  const { pending, error, save } = usePopupSave(onSave, onClose);
  const monthStart = utcDate(month);
  const first = addCalendarDays(month, -((monthStart.getUTCDay() + 6) % 7));
  function changeMonth(direction) {
    const date = utcDate(month);
    date.setUTCMonth(date.getUTCMonth() + direction);
    setMonth(date.toISOString().slice(0, 10));
  }
  return (
    <>
      <header className="task-date-picker-heading">
        <button type="button" className="task-table-icon" aria-label="Tháng trước" onClick={() => changeMonth(-1)}>‹</button>
        <strong aria-live="polite">{monthFormat.format(monthStart)}</strong>
        <button type="button" className="task-table-icon" aria-label="Tháng sau" onClick={() => changeMonth(1)}>›</button>
      </header>
      <div className="task-date-picker-grid">
        {["T2", "T3", "T4", "T5", "T6", "T7", "CN"].map((name) => <b key={name}>{name}</b>)}
        {Array.from({ length: 42 }, (_, index) => {
          const day = addCalendarDays(first, index);
          return (
            <button type="button" key={day} aria-label={dayFormat.format(utcDate(day))}
              aria-current={day === today ? "date" : undefined} aria-pressed={selected === day}
              data-other-month={day.slice(0, 7) !== month.slice(0, 7) || undefined}
              onClick={() => {
                setSelected(day);
                if (day.slice(0, 7) !== month.slice(0, 7)) setMonth(`${day.slice(0, 7)}-01`);
              }}>
              {Number(day.slice(-2))}
            </button>
          );
        })}
      </div>
      {error && <p role="alert" className="task-date-picker-error">{error}</p>}
      <footer className="task-date-picker-actions">
        <button type="button" className="btn ghost small" disabled={!original || pending} onClick={() => save(null)}>Bỏ hạn chót</button>
        <div>
          <button type="button" className="btn ghost small" onClick={() => onClose()}>Hủy</button>
          <button type="button" className="btn primary small" disabled={pending} onClick={() => save(selected)}>
            {icon("check")} Áp dụng
          </button>
        </div>
      </footer>
    </>
  );
}

const estimatePresets = [null, 5, 15, 30, 45, 60, 90, 120];
const durationFields = [
  { key: "hours", label: "Giờ", max: 720 },
  { key: "minutes", label: "Phút", max: 59 },
];

export function TaskEstimateEditor({ task, onSave, onClose }) {
  const originalEstimate = task.estimatedMinutes || null;
  const originalSplit = !task.requiresContinuousSession && (task.splittable ?? true);
  const [estimatedMinutes, setEstimatedMinutes] = useState(originalEstimate);
  const [splittable, setSplittable] = useState(originalSplit);
  const [view, setView] = useState(null);
  const [custom, setCustom] = useState({ hours: 0, minutes: 0 });
  const [inputError, setInputError] = useState("");
  const id = useId();
  const picker = useRef(null);
  const trigger = useRef(null);
  const minutesInput = useRef(null);
  const customOption = useRef(null);
  const { pending, error, save } = usePopupSave(onSave, onClose);
  useOutsideDismiss([picker], () => setView(null), Boolean(view));
  useLayoutEffect(() => {
    if (view === "custom") {
      minutesInput.current?.focus({ preventScroll: true });
      minutesInput.current?.select();
    } else if (view === "presets") {
      const option = picker.current?.querySelector('[aria-pressed="true"]') ?? customOption.current;
      option?.focus({ preventScroll: true });
    }
  }, [view]);

  function choose(value) {
    setEstimatedMinutes(value);
    setView(null);
    trigger.current?.focus({ preventScroll: true });
  }

  function openCustom() {
    setCustom({ hours: Math.floor((estimatedMinutes ?? 0) / 60), minutes: (estimatedMinutes ?? 0) % 60 });
    setInputError("");
    setView("custom");
  }

  function changePart(key, value) {
    setCustom((previous) => ({ ...previous, [key]: value }));
    setInputError("");
  }

  function confirmCustom() {
    const hours = Number(custom.hours);
    const remainingMinutes = Number(custom.minutes);
    const duration = hours * 60 + remainingMinutes;
    if (!Number.isInteger(hours) || !Number.isInteger(remainingMinutes) || hours < 0 ||
        remainingMinutes < 0 || remainingMinutes > 59 || duration < 1 || duration > 43200) {
      setInputError("Nhập thời lượng từ 1 phút đến 720 giờ. Ô phút nhận từ 0 đến 59.");
      return;
    }
    choose(duration);
  }

  function submit(event) {
    event.preventDefault();
    if (view) return;
    if (estimatedMinutes === originalEstimate && splittable === originalSplit) {
      onClose();
      return;
    }
    void save({ estimatedMinutes, splittable });
  }

  return (
    <form className="task-estimate-form" onSubmit={submit}>
      <header className="task-estimate-heading">
        <h3>Chỉnh thời lượng dự kiến</h3>
        <button type="button" className="task-table-icon" aria-label="Đóng" onClick={() => onClose()}>{icon("close")}</button>
      </header>
      <div className="task-estimate-fields">
        <div className="task-estimate-picker" ref={picker} onKeyDown={(event) => {
          if (event.key !== "Escape" || !view) return;
          event.preventDefault();
          event.stopPropagation();
          if (view === "custom") setView("presets");
          else {
            setView(null);
            trigger.current?.focus({ preventScroll: true });
          }
        }}>
          <button type="button" className="task-estimate-trigger" ref={trigger} data-popup-autofocus=""
            aria-label={`Thời lượng dự kiến · ${estimatedMinutes ? minutes(estimatedMinutes) : "Chưa ước lượng"}`}
            aria-expanded={Boolean(view)} aria-controls={view ? `${id}-options` : undefined} disabled={pending}
            onClick={() => setView(view ? null : "presets")}>
            {estimatedMinutes ? minutes(estimatedMinutes) : "Chưa ước lượng"}
            {icon("chevron")}
          </button>
          {view === "presets" && (
            <div className="task-estimate-options" id={`${id}-options`} role="group" aria-label="Chọn thời lượng">
              <div className="task-estimate-presets">
                {estimatePresets.map((duration) => (
                  <button type="button" key={duration ?? "clear"} aria-pressed={estimatedMinutes === duration}
                    onClick={() => choose(duration)}>
                    {duration === null ? "Bỏ thời lượng" : duration === 60 ? "1 giờ" : minutes(duration, "long")}
                  </button>
                ))}
              </div>
              <button type="button" className="task-estimate-custom-option" ref={customOption} onClick={openCustom}>
                Tùy chỉnh
              </button>
            </div>
          )}
          {view === "custom" && (
            <div className="task-estimate-options" id={`${id}-options`} role="group" aria-label="Thời lượng tùy chỉnh">
              <div className="task-estimate-parts">
                {durationFields.map(({ key, label, max }) => {
                  const part = Number(custom[key]);
                  return (
                    <div key={key}>
                      <button type="button" className="task-estimate-increment" aria-label={`Tăng ${label.toLowerCase()}`}
                        disabled={part >= max} onClick={() => changePart(key, Math.min(max, Math.max(0, part + 1)))}>
                        {icon("chevron")}
                      </button>
                      <label className="task-estimate-part">
                        <input type="number" min="0" max={max} step="1" inputMode="numeric" aria-label={label}
                          ref={key === "minutes" ? minutesInput : undefined} value={custom[key]}
                          aria-invalid={Boolean(inputError)} aria-describedby={inputError ? `${id}-error` : undefined}
                          onChange={(event) => changePart(key, event.target.value)}
                          onKeyDown={(event) => {
                            if (event.key !== "Enter") return;
                            event.preventDefault();
                            confirmCustom();
                          }} />
                        <span aria-hidden="true">{key === "hours" ? "h" : "p"}</span>
                      </label>
                      <button type="button" aria-label={`Giảm ${label.toLowerCase()}`}
                        disabled={part <= 0} onClick={() => changePart(key, Math.max(0, Math.min(max, part - 1)))}>
                        {icon("chevron")}
                      </button>
                    </div>
                  );
                })}
              </div>
              {inputError && <p id={`${id}-error`} className="task-date-picker-error" role="alert">{inputError}</p>}
              <footer className="task-estimate-actions">
                <button type="button" className="btn ghost small" onClick={() => setView("presets")}>Quay lại</button>
                <button type="button" className="btn ghost small" onClick={confirmCustom}>OK</button>
              </footer>
            </div>
          )}
        </div>
        <label className="task-estimate-split">
          Chia thành nhiều phiên
          <input type="checkbox" role="switch" aria-describedby={`${id}-split`} checked={splittable}
            disabled={pending || task.requiresContinuousSession} onChange={(event) => setSplittable(event.target.checked)} />
        </label>
        <p id={`${id}-split`}>
          {task.requiresContinuousSession
            ? "Công việc này cần một phiên liên tục."
            : "Cho phép Planora xếp việc vào nhiều khung giờ trống, kể cả qua nhiều ngày."}
        </p>
        {originalEstimate && estimatedMinutes === null && (
          <p>Bỏ thời lượng sẽ hủy các phiên đã xếp chưa bắt đầu. Phiên đang làm hoặc đã khóa cần được xử lý trước.</p>
        )}
        {error && <p className="task-date-picker-error" role="alert">{error}</p>}
      </div>
      <footer className="task-estimate-actions">
        <button type="button" className="btn ghost small" onClick={() => onClose()}>Hủy</button>
        <button type="submit" className="btn primary small" disabled={pending || Boolean(view)}>{pending ? "Đang lưu…" : "Lưu"}</button>
      </footer>
    </form>
  );
}
