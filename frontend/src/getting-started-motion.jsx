import { useLayoutEffect, useRef, useState } from "react";
import { icon } from "./ui.jsx";

const preferenceKey = "planora.start.motion-paused";
export const readStartMotionPaused = () => {
  try {
    return localStorage.getItem(preferenceKey) === "true";
  } catch {
    return false;
  }
};
export function GettingStartedMotion() {
  const host = useRef(null),
    toggle = useRef(null);
  const [paused, setPaused] = useState(readStartMotionPaused),
    [reduced, setReduced] = useState(false);
  useLayoutEffect(() => {
    const root = host.current.closest(".getting-started"),
      media = matchMedia("(prefers-reduced-motion: reduce)");
    const effects = new Set(),
      seen = new WeakSet();
    let manual = readStartMotionPaused();
    const finish = () => {
      effects.forEach((effect) => effect.cancel());
      effects.clear();
      root.getAnimations?.({ subtree: true }).forEach((effect) => {
        if (
          typeof CSSAnimation !== "function" ||
          !(effect instanceof CSSAnimation)
        )
          effect.cancel();
      });
    };
    const sync = () => {
      root.dataset.motionState = media.matches
        ? "reduced"
        : manual || document.hidden
          ? "paused"
          : "running";
      setReduced(media.matches);
      if (root.dataset.motionState !== "running") finish();
    };
    toggle.current = (value) => {
      manual = value;
      sync();
    };
    sync();
    const reveal = (element, delay = 0) => {
      if (
        seen.has(element) ||
        root.dataset.motionState !== "running" ||
        !element.animate
      )
        return;
      seen.add(element);
      const effect = element.animate(
        [
          { opacity: 0, translate: "0 24px", scale: 0.985 },
          { opacity: 1, translate: "0 0", scale: 1 },
        ],
        {
          duration: 700,
          delay,
          easing: "cubic-bezier(0.22, 1, 0.36, 1)",
          fill: "backwards",
        },
      );
      effects.add(effect);
      effect.finished.catch(() => {}).then(() => effects.delete(effect));
    };
    let observer;
    if (typeof IntersectionObserver === "function") {
      observer = new IntersectionObserver(
        (entries) =>
          entries
            .filter((entry) => entry.isIntersecting)
            .forEach((entry, index) => {
              reveal(entry.target, Math.min(index, 3) * 70);
              observer.unobserve(entry.target);
            }),
        { threshold: 0.12 },
      );
      root
        .querySelectorAll("[data-start-reveal]")
        .forEach((element) => observer.observe(element));
    }
    root.addEventListener("pointerdown", finish);
    root.addEventListener("keydown", finish);
    document.addEventListener("visibilitychange", sync);
    media.addEventListener("change", sync);
    return () => {
      toggle.current = null;
      observer?.disconnect();
      finish();
      root.removeEventListener("pointerdown", finish);
      root.removeEventListener("keydown", finish);
      document.removeEventListener("visibilitychange", sync);
      media.removeEventListener("change", sync);
    };
  }, []);
  return (
    <button
      ref={host}
      className="start-motion-toggle"
      type="button"
      aria-pressed={paused}
      disabled={reduced}
      onClick={() => {
        const next = !paused;
        setPaused(next);
        try {
          localStorage.setItem(preferenceKey, String(next));
        } catch {
          /* Optional preference. */
        }
        toggle.current?.(next);
      }}
    >
      {icon(paused || reduced ? "play" : "pause")}
      <span>
        {reduced
          ? "Đã giảm chuyển động"
          : paused
            ? "Bật animation"
            : "Tạm dừng animation"}
      </span>
    </button>
  );
}
