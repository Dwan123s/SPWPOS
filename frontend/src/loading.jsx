import { useEffect, useState } from "react";
import { icon } from "./ui.jsx";
import { MascotEyes } from "./mascot-eyes.jsx";
import { planningQuoteOfDay } from "./planning-quotes.js";

const preferenceKey = "planora.loading.motion-paused";
const readMotionPaused = () => {
  try {
    return localStorage.getItem(preferenceKey) === "true";
  } catch {
    return false;
  }
};

export function Loading({
  label = "Đang tải…",
  fullscreen = false,
}) {
  const [paused, setPaused] = useState(readMotionPaused);
  const [quote] = useState(planningQuoteOfDay);
  const [reduced, setReduced] = useState(false);
  const [hidden, setHidden] = useState(() => document.hidden);

  useEffect(() => {
    const media = matchMedia("(prefers-reduced-motion: reduce)");
    const onMediaChange = () => setReduced(media.matches);
    const onVisibilityChange = () => setHidden(document.hidden);
    onMediaChange();
    media.addEventListener("change", onMediaChange);
    document.addEventListener("visibilitychange", onVisibilityChange);
    return () => {
      media.removeEventListener("change", onMediaChange);
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, []);

  const toggleMotion = () => {
    const next = !paused;
    setPaused(next);
    try {
      localStorage.setItem(preferenceKey, String(next));
    } catch {
      /* Browser storage is optional. */
    }
  };
  const motionState = reduced
    ? "reduced"
    : paused || hidden
      ? "paused"
      : "running";

  return (
    <section
      className={`planora-loading ${fullscreen ? "planora-loading--fullscreen" : "planora-loading--inline"}`}
      data-motion-state={motionState}
      aria-busy="true"
      aria-label={fullscreen ? "Đang tải PLANORA" : undefined}
    >
      {fullscreen && (
        <>
          <header className="loading-header">
            <img
              src="/assets/planora-brand.png"
              alt="PLANORA"
              width="170"
              height="58"
              decoding="async"
            />
          </header>
          <button
            type="button"
            className="loading-motion-control"
            onClick={toggleMotion}
            aria-label={
              reduced
                ? "Chuyển động đang tắt theo cài đặt hệ thống"
                : paused
                  ? "Bật chuyển động của NORA"
                  : "Tạm dừng chuyển động của NORA"
            }
            aria-pressed={paused || reduced}
            disabled={reduced}
          >
            {icon(paused || reduced ? "play" : "pause")}
          </button>
        </>
      )}
      <div className="loading-content">
        <div className="loading-art" aria-hidden="true">
          <span className="loading-halo" />
          <span className="loading-orbit loading-orbit--outer" />
          <span className="loading-orbit loading-orbit--inner" />
          <span className="loading-art-shadow" />
          <div className="loading-mascot">
            <img
              src="/assets/start/planora-owl-v1.png"
              alt=""
              width="1254"
              height="1254"
              decoding="async"
              fetchPriority={fullscreen ? "high" : "auto"}
            />
            {fullscreen && <MascotEyes rootSelector=".planora-loading" />}
          </div>
          {fullscreen && (
            <>
              <span className="loading-token loading-token--calendar">
                {icon("calendar")}
              </span>
              <span className="loading-token loading-token--check">
                {icon("check")}
              </span>
              <span className="loading-spark loading-spark--one" />
              <span className="loading-spark loading-spark--two" />
            </>
          )}
        </div>
        <div className="loading-copy">
          <p
            className="loading-label"
            role="status"
            aria-live="polite"
            aria-atomic="true"
          >
            {label}
          </p>
          <div className="loading-progress-track" aria-hidden="true">
            <span />
          </div>
          {fullscreen && (
            <figure className="loading-quote">
              <blockquote>{quote}</blockquote>
            </figure>
          )}
        </div>
      </div>
    </section>
  );
}
