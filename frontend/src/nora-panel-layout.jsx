import { useEffect, useRef, useState } from "react";

const storageKey = "planora.nora.panel-widths";
const limits = {
  history: { min: 220, max: 400, initial: 260, label: "hội thoại" },
  plan: { min: 280, max: 480, initial: 320, label: "lịch" },
};
const clamp = (value, min, max) => Math.max(min, Math.min(max, value));

function readWidths() {
  let saved;
  try {
    saved = JSON.parse(localStorage.getItem(storageKey));
  } catch {
    /* Storage can be unavailable. */
  }
  return Object.fromEntries(
    Object.entries(limits).map(([side, limit]) => [
      side,
      Number.isFinite(saved?.[side])
        ? clamp(saved[side], limit.min, limit.max)
        : limit.initial,
    ]),
  );
}

export function useNoraPanelLayout(wide, historyCollapsed, planOpen) {
  const root = useRef(null);
  const [preferred, setPreferred] = useState(readWidths);
  const [available, setAvailable] = useState(window.innerWidth);
  const [resizing, setResizing] = useState(false);
  useEffect(() => {
    const measure = () => {
      const width = root.current?.getBoundingClientRect().width;
      if (width) setAvailable(width);
    };
    measure();
    const observer =
      typeof ResizeObserver === "function" ? new ResizeObserver(measure) : null;
    if (root.current) observer?.observe(root.current);
    window.addEventListener("resize", measure);
    return () => {
      observer?.disconnect();
      window.removeEventListener("resize", measure);
    };
  }, []);
  useEffect(() => {
    if (resizing) return;
    try {
      localStorage.setItem(storageKey, JSON.stringify(preferred));
    } catch {
      /* Resizing still works without storage. */
    }
  }, [preferred, resizing]);

  // Preserve at least 440 px for the conversation on desktop. Keep preferred
  // widths separately so a temporary smaller window does not overwrite them.
  const widths = {
    history: historyCollapsed ? 0 : preferred.history,
    plan: planOpen ? preferred.plan : 0,
  };
  const budget = Math.max(
    (historyCollapsed ? 0 : limits.history.min) +
      (planOpen ? limits.plan.min : 0),
    available - 440,
  );
  let excess = Math.max(0, widths.history + widths.plan - budget);
  for (const side of ["history", "plan"]) {
    if (!widths[side]) continue;
    const reduction = Math.min(excess, widths[side] - limits[side].min);
    widths[side] -= reduction;
    excess -= reduction;
  }
  const maximum = (side) =>
    Math.max(
      limits[side].min,
      Math.min(
        limits[side].max,
        budget - widths[side === "history" ? "plan" : "history"],
      ),
    );
  return {
    root,
    resizing,
    setResizing,
    widths,
    style: {
      "--nora-history-width": `${widths.history}px`,
      "--nora-plan-width": `${widths.plan}px`,
      gridTemplateColumns: wide
        ? "var(--nora-history-width) minmax(0, 1fr) var(--nora-plan-width)"
        : undefined,
    },
    options(side) {
      return {
        side,
        value: widths[side],
        min: limits[side].min,
        max: maximum(side),
        label: limits[side].label,
        resize(value) {
          setPreferred((old) => ({
            ...old,
            [side]: Math.round(clamp(value, limits[side].min, maximum(side))),
          }));
        },
        reset() {
          setPreferred((old) => ({ ...old, [side]: limits[side].initial }));
        },
      };
    },
  };
}

export function NoraPanelResize({ options, onResizing }) {
  const drag = useRef(null);
  const { side, value, min, max, label, resize, reset } = options;
  const finish = () => {
    drag.current = null;
    onResizing(false);
  };
  useEffect(() => {
    window.addEventListener("blur", finish);
    return () => window.removeEventListener("blur", finish);
  }, []);
  return (
    <div
      className={`nora-panel-resize is-${side}`}
      role="separator"
      tabIndex={0}
      aria-orientation="vertical"
      aria-label={`Đổi chiều rộng bảng ${label}`}
      aria-controls={
        side === "history" ? "nora-history-list" : "nora-plan-panel"
      }
      aria-valuemin={min}
      aria-valuemax={max}
      aria-valuenow={value}
      aria-valuetext={`${value} pixel`}
      title="Kéo để đổi chiều rộng. Dùng phím mũi tên hoặc bấm đúp để đặt lại."
      onDoubleClick={reset}
      onPointerDown={(event) => {
        if (event.button !== 0) return;
        event.preventDefault();
        event.currentTarget.dataset.pointerFocus = "true";
        drag.current = { x: event.clientX, width: value };
        event.currentTarget.setPointerCapture(event.pointerId);
        event.currentTarget.focus();
        onResizing(true);
      }}
      onPointerMove={(event) => {
        if (!drag.current) return;
        const change =
          (event.clientX - drag.current.x) * (side === "history" ? 1 : -1);
        resize(drag.current.width + change);
      }}
      onPointerUp={finish}
      onPointerCancel={finish}
      onLostPointerCapture={finish}
      onBlur={(event) => {
        delete event.currentTarget.dataset.pointerFocus;
      }}
      onKeyDown={(event) => {
        delete event.currentTarget.dataset.pointerFocus;
        const direction = side === "history" ? 1 : -1;
        if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
          event.preventDefault();
          resize(
            value +
              (event.key === "ArrowRight" ? 1 : -1) *
                direction *
                (event.shiftKey ? 40 : 20),
          );
        } else if (event.key === "Home" || event.key === "End") {
          event.preventDefault();
          resize(event.key === "Home" ? min : max);
        }
      }}
    />
  );
}
