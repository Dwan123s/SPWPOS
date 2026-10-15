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
      { opacity: 0, translate: "0 10px", scale: 0.99 },
      { opacity: 1, translate: "0 0", scale: 1 },
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
      <picture className="workspace-ribbon-art">
        <source
          srcSet="/assets/login-sprites/ribbon-background.webp"
          type="image/webp"
        />
        <img
          src="/assets/login-sprites/ribbon-background.png"
          width="1000"
          height="1000"
          alt=""
          decoding="async"
          loading="lazy"
          fetchPriority="low"
        />
      </picture>
      <picture className="workspace-bubble-art workspace-bubble-art--one">
        <source srcSet="/assets/login-sprites/bubble.webp" type="image/webp" />
        <img
          src="/assets/login-sprites/bubble.png"
          width="1000"
          height="1000"
          alt=""
          decoding="async"
          loading="lazy"
          fetchPriority="low"
        />
      </picture>
      <picture className="workspace-bubble-art workspace-bubble-art--two">
        <source srcSet="/assets/login-sprites/bubble.webp" type="image/webp" />
        <img
          src="/assets/login-sprites/bubble.png"
          width="1000"
          height="1000"
          alt=""
          decoding="async"
          loading="lazy"
          fetchPriority="low"
        />
      </picture>
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
    const surfaceSelector =
      ".page-header, .content .card, .focus-card, .profile-hero-card, .backlog-panel, .focus-cockpit, .runway-card, .intelligence-card, .console-note-link";
    const seen = new WeakSet();
    let observer;
    let manuallyPaused = readPaused();
    const finish = () => {
      observer?.disconnect();
      animations.forEach((animation) => animation.cancel());
      animations.clear();
    };
    const reveal = (element, delay = 0) => {
      if (root.dataset.motionState !== "running") return;
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
    }
    const observeSurfaces = (node) => {
      if (!(node instanceof Element)) return;
      const surfaces = [
        ...(node.matches(surfaceSelector) ? [node] : []),
        ...node.querySelectorAll(surfaceSelector),
      ];
      surfaces.forEach((element, index) => {
        if (seen.has(element)) return;
        seen.add(element);
        // Route content already has one coordinated transition. Animating
        // every nested card again makes navigation linger and flicker.
        if (element.closest(".page-scene")) return;
        const editing =
          root.contains(document.activeElement) &&
          document.activeElement.matches(
            "input,textarea,select,[contenteditable=true]",
          );
        if (root.dataset.motionState !== "running" || editing) return;
        if (observer) observer.observe(element);
        else reveal(element, Math.min(index, 4) * 40);
      });
    };
    observeSurfaces(root);
    // Notes, security cards and API results mount after the shell's first render.
    const mutations = new MutationObserver((records) => {
      records.forEach((record) => record.addedNodes.forEach(observeSurfaces));
    });
    mutations.observe(root.querySelector(".content") || root, {
      childList: true,
      subtree: true,
    });
    // Interaction finishes reveals so controls never remain partly faded while editing.
    root.addEventListener("pointerdown", finish);
    root.addEventListener("keydown", finish);
    document.addEventListener("visibilitychange", sync);
    media.addEventListener("change", sync);
    return () => {
      mutations.disconnect();
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
