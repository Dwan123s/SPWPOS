import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { icon } from "./ui.jsx";

// Sample both circular arcs for native timing functions, with denser points
// around the steep midpoint. Share the curve with the background glass shapes.
const circularHalf = Array.from({ length: 65 }, (_, index) => {
  const angle = (index / 64) * (Math.PI / 2);
  return { time: Math.sin(angle) / 2, value: (1 - Math.cos(angle)) / 2 };
});
const circularScaleEasing = `linear(${[
  ...circularHalf,
  ...circularHalf
    .slice(0, -1)
    .reverse()
    .map(({ time, value }) => ({ time: 1 - time, value: 1 - value })),
]
  .map(({ time, value }) => `${value.toFixed(6)} ${(time * 100).toFixed(6)}%`)
  .join(", ")})`;

function revealLogin(root, reduced) {
  const animations = [];
  const targets = [];
  let finished = false;

  const finish = () => {
    finished = true;
    for (const element of targets) {
      element.classList.remove("login-entrance-pending");
    }
    for (const animation of animations) animation.cancel();
    delete root.dataset.depthEntrance;
    root.dataset.entranceState = "complete";
  };
  if (reduced || typeof root.animate !== "function" || !document.timeline) {
    finish();
    return { finish, cleanup: () => delete root.dataset.entranceState };
  }
  const scaleEasing = CSS.supports(
    "animation-timing-function",
    circularScaleEasing,
  )
    ? circularScaleEasing
    : "cubic-bezier(0.785, 0.135, 0.15, 0.86)";
  root.style.setProperty("--login-entrance-scale-easing", scaleEasing);

  const amplitude = Number(
    getComputedStyle(root).getPropertyValue("--login-motion-amplitude"),
  );
  const prepare = (selector, delay, duration = 1400, initialScale = null) =>
    [...root.querySelectorAll(selector)].map((element, index) => {
      const opacity = Number(getComputedStyle(element).opacity);
      targets.push(element);
      element.classList.add("login-entrance-pending");
      return {
        element,
        opacity,
        delay: delay + index * 40,
        duration,
        initialScale:
          initialScale === null ? null : 1 + (initialScale - 1) * amplitude,
      };
    });
  const panels = [
    ...prepare(".auth-story > .brand-lockup", 0, 900),
    ...prepare(".auth-card", 600, 1100),
    ...prepare(".login-headline-panel", 120, 1300),
  ];
  const artwork = [
    ...prepare(".login-sprite--ribbon-background", 0, 1600, 0.82),
    // One start time and delay keep the front and back of the ribbon aligned.
    ...prepare(".login-sprite--ribbon-back", 380),
    ...prepare(".login-sprite--ribbon-front", 380),
    ...prepare(".login-sprite--calendar", 440),
    ...prepare(".login-sprite--checklist", 700),
    ...prepare(".login-sprite--clock", 900),
    ...prepare(".login-sprite--workspace-photo", 1200),
    ...prepare('.login-scene-art > [data-sprite="bubble"]', 660),
    ...prepare(".login-motion-orbs > .login-sprite", 150, 1600, 0.84),
    ...prepare(
      ".login-card-ribbon,.login-card-bubble,.login-card-bubble-small",
      350,
    ),
    ...prepare(".login-scene-shadow", 550),
  ];
  const reveal = (items) => {
    if (finished) return Promise.resolve();
    const startTime = document.timeline.currentTime;
    return Promise.all(
      items.map(({ element, opacity, delay, duration, initialScale }) => {
        const timing = { duration, delay, fill: "both" };
        const effects = [
          element.animate([{ opacity: 0 }, { opacity }], {
            ...timing,
            easing: "cubic-bezier(0.22, 0.61, 0.36, 1)",
          }),
        ];
        if (initialScale !== null) {
          effects.push(
            element.animate([{ scale: initialScale }, { scale: 1 }], {
              ...timing,
              easing: scaleEasing,
            }),
          );
        }
        for (const animation of effects) animation.startTime = startTime;
        animations.push(...effects);
        return Promise.all(
          effects.map((animation) => animation.finished.catch(() => {})),
        );
      }),
    );
  };

  // Scale the complete foreground framebuffer once. Individual foreground
  // sprites only fade, so their overlap and relative positions stay intact.
  const foregroundZoom = root.querySelector(".login-foreground").animate(
    [
      { scale: 1 + 0.08 * amplitude, filter: `blur(${8 * amplitude}px)` },
      { scale: 1, filter: "blur(0px)" },
    ],
    {
      duration: 1600,
      easing: scaleEasing,
      fill: "both",
    },
  );
  foregroundZoom.pause();
  foregroundZoom.currentTime = 0;
  animations.push(foregroundZoom);
  root.dataset.entranceState = "entering";
  // Panels reveal on their own schedule, with the login card delayed 600ms.
  // Decode artwork before its reveal so a slow download cannot consume the fade.
  const images = artwork.flatMap(({ element }) => [
    ...element.querySelectorAll("img"),
    ...(element.tagName === "IMG" ? [element] : []),
  ]);
  Promise.all([
    reveal(panels),
    Promise.all(images.map((image) => image.decode().catch(() => {}))).then(
      () => {
        if (finished) return;
        root.dataset.depthEntrance = "entering";
        foregroundZoom.play();
        foregroundZoom.startTime = document.timeline.currentTime;
        return Promise.all([
          reveal(artwork),
          foregroundZoom.finished.catch(() => {}),
        ]);
      },
    ),
  ]).then(() => {
    if (!finished) finish();
  });

  return {
    finish,
    cleanup: () => {
      finish();
      delete root.dataset.entranceState;
      root.style.removeProperty("--login-entrance-scale-easing");
    },
  };
}

export function LoginMotionControl() {
  const host = useRef(null);
  const controller = useRef(null);
  const [paused, setPaused] = useState(false);
  const [reduced, setReduced] = useState(false);

  useLayoutEffect(() => {
    const root = host.current.closest(".login-composition");
    const card = root.querySelector(".auth-card");
    const reduce = window.matchMedia("(prefers-reduced-motion: reduce)");
    let editing = false;
    let manuallyPaused = false;
    const entrance = revealLogin(root, reduce.matches);

    const sync = () => {
      // Interaction makes all controls fully visible immediately. Pausing or
      // reduced motion must never leave a panel frozen halfway through a fade.
      if (reduce.matches || manuallyPaused || editing || document.hidden) {
        entrance.finish();
      }
      setReduced(reduce.matches);
      root.dataset.motionState = reduce.matches
        ? "reduced"
        : manuallyPaused || editing || document.hidden
          ? "paused"
          : "running";
    };
    const interact = () => {
      editing = true;
      sync();
    };
    const point = (event) => {
      editing = card.contains(event.target);
      sync();
    };
    const leave = (event) => {
      if (!card.contains(event.relatedTarget)) {
        editing = false;
        sync();
      }
    };
    controller.current = (value) => {
      manuallyPaused = value;
      sync();
    };
    root.addEventListener("pointerdown", point);
    card.addEventListener("keydown", interact);
    card.addEventListener("input", interact);
    card.addEventListener("focusout", leave);
    document.addEventListener("visibilitychange", sync);
    reduce.addEventListener("change", sync);
    sync();

    return () => {
      controller.current = null;
      entrance.cleanup();
      delete root.dataset.motionState;
      root.removeEventListener("pointerdown", point);
      card.removeEventListener("keydown", interact);
      card.removeEventListener("input", interact);
      card.removeEventListener("focusout", leave);
      document.removeEventListener("visibilitychange", sync);
      reduce.removeEventListener("change", sync);
    };
  }, []);

  useEffect(() => {
    controller.current?.(paused);
  }, [paused]);

  return (
    <span ref={host} className="login-motion-control" hidden={reduced}>
      <button
        type="button"
        className="login-motion-toggle"
        aria-pressed={paused}
        onClick={() => setPaused((value) => !value)}
      >
        {icon(paused ? "play" : "pause")}
        {paused ? "Tiếp tục chuyển động" : "Tạm dừng chuyển động"}
      </button>
    </span>
  );
}
