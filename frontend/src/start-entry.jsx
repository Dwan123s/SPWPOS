import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";

export function StartEntry({ onContinue }) {
  const [started, setStarted] = useState(false);
  const clock = useRef(null),
    ring = useRef(null),
    completed = useRef(false),
    alive = useRef(true);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  useEffect(() => {
    if (!started) return;
    const root = clock.current.closest(".getting-started");
    let stopped = false,
      effect;
    const next = () => {
      if (stopped || !alive.current) return;
      const commit = () =>
        onContinue ? onContinue() : (location.hash = "login");
      if (
        root.dataset.motionState === "running" &&
        typeof document.startViewTransition === "function"
      ) {
        document.documentElement.dataset.startTransition = "login";
        const transition = document.startViewTransition(commit);
        transition.finished
          .catch(() => {})
          .finally(() => {
            delete document.documentElement.dataset.startTransition;
          });
      } else commit();
    };
    if (root.dataset.motionState !== "running" || !ring.current.animate) {
      queueMicrotask(next);
      return () => {
        stopped = true;
      };
    }
    const rect = clock.current.getBoundingClientRect(),
      diameter = Math.hypot(innerWidth, innerHeight) * 2;
    ring.current.style.setProperty(
      "--portal-x",
      `${rect.left + rect.width / 2}px`,
    );
    ring.current.style.setProperty(
      "--portal-y",
      `${rect.top + rect.height / 2}px`,
    );
    ring.current.style.width = `${diameter}px`;
    ring.current.style.height = `${diameter * 0.72}px`;
    effect = ring.current.animate(
      [
        {
          opacity: 0,
          transform: "translate(-50%, -50%) scale(.015) rotate(-18deg)",
          offset: 0,
        },
        {
          opacity: 0.8,
          transform: "translate(-50%, -50%) scale(.08) rotate(0deg)",
          offset: 0.35,
        },
        {
          opacity: 1,
          transform: "translate(-50%, -50%) scale(1) rotate(16deg)",
          offset: 1,
        },
      ],
      {
        duration: 950,
        delay: 280,
        easing: "cubic-bezier(.65,0,.25,1)",
        fill: "both",
      },
    );
    effect.finished.catch(() => {}).then(next);
    return () => {
      stopped = true;
      effect.cancel();
    };
  }, [started, onContinue]);
  return (
    <div
      className={`start-entry start-clock-entry ${started ? "is-placed" : ""}`}
    >
      <button
        ref={clock}
        id="start-entry-clock"
        className="start-entry-clock"
        type="button"
        aria-label="Chạm đồng hồ để bắt đầu"
        aria-describedby="start-entry-hint"
        disabled={started}
        onClick={() => {
          if (completed.current) return;
          completed.current = true;
          setStarted(true);
        }}
      >
        <span className="start-clock-halo" aria-hidden="true" />
        <span className="start-clock-tooltip">Bắt đầu ngày mới</span>
      </button>
      <p id="start-entry-hint" className="start-entry-hint" role="status">
        {started
          ? "Một ngày mới đang mở ra…"
          : "Chạm vào đồng hồ. Để ngày của bạn bắt đầu."}
      </p>
      {started &&
        createPortal(
          <div className="start-portal-overlay" aria-hidden="true">
            <div ref={ring} className="start-portal-ring" />
            <span>PLANORA</span>
          </div>,
          document.body,
        )}
    </div>
  );
}
