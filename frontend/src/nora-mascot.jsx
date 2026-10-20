import { useLayoutEffect, useRef, useState } from "react";
import { MascotEyes } from "./mascot-eyes.jsx";
import { icon } from "./ui.jsx";

const image = "/assets/start/planora-owl-v1.png";
const preferenceKey = "planora.nora.motion-paused";
function readPaused() {
  try {
    return localStorage.getItem(preferenceKey) === "true";
  } catch {
    return false;
  }
}

export function NoraAvatar() {
  return (
    <span className="nora-avatar" aria-hidden="true">
      <img src={image} alt="" width="64" height="64" decoding="async" />
    </span>
  );
}

export function NoraMascot({ thinking }) {
  return (
    <div
      className={`nora-mascot${thinking ? " is-thinking" : ""}`}
      role="img"
      aria-label="NORA, linh vật cú xanh ôm cuốn lịch"
    >
      <span className="nora-mascot-orbit" aria-hidden="true" />
      <span className="nora-mascot-halo" aria-hidden="true" />
      <div className="nora-mascot-figure">
        <img src={image} alt="" width="1254" height="1254" decoding="async" />
        <MascotEyes rootSelector=".nora-studio" />
      </div>
      <span className="nora-mascot-shadow" aria-hidden="true" />
    </div>
  );
}

// Share login's native animation approach and respect the workspace motion switch.
export function NoraMotionControl() {
  const host = useRef(null);
  const [paused, setPaused] = useState(readPaused);
  const [reduced, setReduced] = useState(false);
  const [inheritedPause, setInheritedPause] = useState(false);
  useLayoutEffect(() => {
    const root = host.current.closest(".nora-studio");
    const workspace = root.closest(".glass-workspace");
    const media = window.matchMedia("(prefers-reduced-motion: reduce)");
    const effects = new Set();
    const sync = () => {
      const parentPaused = ["paused", "reduced"].includes(
        workspace?.dataset.motionState,
      );
      setReduced(media.matches);
      setInheritedPause(parentPaused);
      root.dataset.motionState = media.matches
        ? "reduced"
        : paused || parentPaused || document.hidden
          ? "paused"
          : "running";
      if (root.dataset.motionState !== "running") {
        effects.forEach((effect) => effect.cancel());
        effects.clear();
      }
      // Also synchronize the native animation objects, which may have been
      // controlled by a browser capture or the surrounding workspace.
      root.getAnimations?.({ subtree: true }).forEach((animation) => {
        if (
          typeof CSSAnimation === "function" &&
          animation instanceof CSSAnimation &&
          animation.animationName.startsWith("nora-")
        ) {
          if (root.dataset.motionState === "running") {
            const end =
              animation.effect?.getComputedTiming().endTime ?? Infinity;
            if (
              animation.playState === "paused" &&
              Number(animation.currentTime) < end
            )
              animation.play();
          } else if (animation.playState === "running") animation.pause();
        }
      });
    };
    sync();
    if (root.dataset.motionState === "running") {
      root.querySelectorAll("[data-nora-reveal]").forEach((element, index) => {
        if (!element.animate) return;
        const effect = element.animate(
          [
            { opacity: 0, translate: "0 12px" },
            { opacity: 1, translate: "0 0" },
          ],
          {
            duration: 450,
            delay: index * 65,
            easing: "cubic-bezier(.22,1,.36,1)",
            fill: "backwards",
          },
        );
        effects.add(effect);
        effect.finished.catch(() => {}).then(() => effects.delete(effect));
      });
    }
    const observer = new MutationObserver(sync);
    if (workspace)
      observer.observe(workspace, {
        attributes: true,
        attributeFilter: ["data-motion-state"],
      });
    media.addEventListener("change", sync);
    document.addEventListener("visibilitychange", sync);
    return () => {
      observer.disconnect();
      effects.forEach((effect) => effect.cancel());
      media.removeEventListener("change", sync);
      document.removeEventListener("visibilitychange", sync);
    };
  }, [paused]);
  const label =
    reduced || inheritedPause
      ? "Chuyển động NORA đang được tạm dừng bởi hệ thống"
      : paused
        ? "Bật chuyển động của NORA"
        : "Tạm dừng chuyển động của NORA";
  return (
    <button
      ref={host}
      type="button"
      className="nora-motion-button"
      aria-label={label}
      title={label}
      aria-pressed={paused || reduced || inheritedPause}
      disabled={reduced || inheritedPause}
      onClick={() => {
        const next = !paused;
        setPaused(next);
        try {
          localStorage.setItem(preferenceKey, String(next));
        } catch {
          /* Optional preference. */
        }
      }}
    >
      {icon(paused || reduced || inheritedPause ? "play" : "pause")}
      <span>Chuyển động</span>
    </button>
  );
}
