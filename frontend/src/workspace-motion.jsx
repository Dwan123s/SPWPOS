import { useLayoutEffect, useRef, useState } from "react";
import { icon } from "./ui.jsx";

const preferenceKey = "planora.workspace.motion-paused";
const easing = "cubic-bezier(0.22, 1, 0.36, 1)";
function readPaused() {
  try {
    return localStorage.getItem(preferenceKey) === "true";
  } catch {
    return false;
  }
}

// Use the same native animation API as login, without delaying navigation or input.
export function revealSurface(element, delay = 0) {
  if (!element?.animate || document.hidden) return null;
  return element.animate(
    [
      { opacity: 0, translate: "0 12px" },
      { opacity: 1, translate: "0 0" },
    ],
    { duration: 420, delay, easing, fill: "backwards" },
  );
}

export function revealModal(element) {
  if (
    readPaused() ||
    document.querySelector('.glass-workspace[data-motion-state="paused"]') ||
    window.matchMedia("(prefers-reduced-motion: reduce)").matches
  )
    return null;
  return revealSurface(element);
}

export function WorkspaceAtmosphere() {
  return (
    <div className="workspace-atmosphere" aria-hidden="true">
      <span className="workspace-halo workspace-halo--one" />
      <span className="workspace-halo workspace-halo--two" />
      <span className="workspace-orb workspace-orb--one" />
      <span className="workspace-orb workspace-orb--two" />
    </div>
  );
}

export function WorkspaceMotionControl() {
  const host = useRef(null);
  const syncMotion = useRef(null);
  const [paused, setPaused] = useState(readPaused);
  const [reduced, setReduced] = useState(false);

  useLayoutEffect(() => {
    const root = host.current.closest(".glass-workspace");
    const media = window.matchMedia("(prefers-reduced-motion: reduce)");
    const animations = new Set();
    const targets = [
      ...root.querySelectorAll(
        ".page-header, .content .card, .focus-card, .profile-hero-card",
      ),
    ];
    let observer;
    let manuallyPaused = readPaused();
    const finish = () => {
      observer?.disconnect();
      animations.forEach((animation) => animation.cancel());
      animations.clear();
    };
    const reveal = (element, delay = 0) => {
      const animation = revealSurface(element, delay);
      if (!animation) return;
      animations.add(animation);
      animation.finished
        .catch(() => {})
        .then(() => animations.delete(animation));
    };
    const sync = () => {
      setReduced(media.matches);
      root.dataset.motionState = media.matches
        ? "reduced"
        : manuallyPaused || document.hidden
          ? "paused"
          : "running";
      if (root.dataset.motionState !== "running") finish();
    };
    syncMotion.current = (value) => {
      manuallyPaused = value;
      sync();
    };
    sync();
    if (root.dataset.motionState === "running") {
      if (typeof IntersectionObserver === "function") {
        observer = new IntersectionObserver(
          (entries) => {
            entries
              .filter((entry) => entry.isIntersecting)
              .forEach((entry, index) => {
                reveal(entry.target, Math.min(index, 4) * 40);
                observer.unobserve(entry.target);
              });
          },
          { threshold: 0.05 },
        );
        targets.forEach((element) => observer.observe(element));
      } else {
        targets.forEach((element, index) =>
          reveal(element, Math.min(index, 4) * 40),
        );
      }
    }
    // Interaction finishes reveals so controls never remain partly faded while editing.
    root.addEventListener("pointerdown", finish);
    root.addEventListener("keydown", finish);
    document.addEventListener("visibilitychange", sync);
    media.addEventListener("change", sync);
    return () => {
      finish();
      syncMotion.current = null;
      root.removeEventListener("pointerdown", finish);
      root.removeEventListener("keydown", finish);
      document.removeEventListener("visibilitychange", sync);
      media.removeEventListener("change", sync);
    };
  }, []);

  return (
    <button
      ref={host}
      type="button"
      className="btn ghost icon-only workspace-motion-toggle"
      aria-label={
        reduced
          ? "Hệ thống đang giảm chuyển động"
          : paused
            ? "Bật chuyển động"
            : "Tạm dừng chuyển động"
      }
      title={
        reduced
          ? "Hệ thống đang giảm chuyển động"
          : paused
            ? "Bật chuyển động"
            : "Tạm dừng chuyển động"
      }
      aria-pressed={paused || reduced}
      disabled={reduced}
      onClick={() => {
        const value = !paused;
        setPaused(value);
        try {
          localStorage.setItem(preferenceKey, String(value));
        } catch {
          /* Private browsing may block storage. */
        }
        syncMotion.current?.(value);
      }}
    >
      {icon(paused || reduced ? "play" : "pause")}
    </button>
  );
}
