import { useEffect, useState } from "react";
import { icon } from "./ui.jsx";

export function Loading({
  label = "Đang mở không gian làm việc…",
  fullscreen = false,
}) {
  const [paused, setPaused] = useState(() => document.hidden);
  useEffect(() => {
    const sync = () => setPaused(document.hidden);
    document.addEventListener("visibilitychange", sync);
    return () => document.removeEventListener("visibilitychange", sync);
  }, []);

  return (
    <section
      className={`planora-loading ${fullscreen ? "planora-loading--fullscreen" : "planora-loading--inline"}`}
      data-motion-state={paused ? "paused" : "running"}
      role="status"
      aria-live="polite"
      aria-atomic="true"
      aria-busy="true"
    >
      {fullscreen && (
        <>
          <div className="loading-atmosphere" aria-hidden="true">
            <span className="loading-halo loading-halo--left" />
            <span className="loading-halo loading-halo--right" />
            <span className="loading-orb loading-orb--one" />
            <span className="loading-orb loading-orb--two" />
            <span className="loading-orb loading-orb--three" />
          </div>
          <div className="loading-art" aria-hidden="true">
            <span className="loading-orbit loading-orbit--back" />
            <svg
              className="loading-orbit loading-orbit--front"
              viewBox="0 0 310 104"
              preserveAspectRatio="none"
            >
              <ellipse cx="155" cy="52" rx="153" ry="50" pathLength="100" />
            </svg>
            <div className="loading-logo-glass">
              <img
                src="/assets/planora-mark.png"
                alt=""
                width="112"
                height="112"
                decoding="async"
              />
            </div>
            <span className="loading-art-shadow" />
            <div className="loading-mini-card loading-mini-card--calendar">
              <span className="loading-calendar-binding">
                <i />
                <i />
              </span>
              <span className="loading-calendar-header" />
              <span className="loading-calendar-grid">
                {Array.from({ length: 9 }, (_, i) => (
                  <i key={i} />
                ))}
              </span>
            </div>
            <div className="loading-mini-card loading-mini-card--checklist">
              <span className="loading-checklist-title" />
              {[0, 1, 2].map((i) => (
                <span className="loading-checklist-row" key={i}>
                  {icon("check")}
                  <i />
                </span>
              ))}
            </div>
            <div className="loading-mini-card loading-mini-card--clock">
              <span className="loading-clock-face">
                <i className="loading-clock-hour" />
                <i className="loading-clock-minute" />
              </span>
            </div>
          </div>
        </>
      )}
      <div className="loading-copy">
        <p className="loading-label">
          {!fullscreen && (
            <span className="loading-spinner" aria-hidden="true" />
          )}
          {label}
        </p>
        <div className="loading-flow" aria-hidden="true">
          <span />
          <span />
          <span />
        </div>
      </div>
      {fullscreen && (
        <p className="loading-tagline" aria-hidden="true">
          Plan smarter. Work realistically.
        </p>
      )}
    </section>
  );
}
