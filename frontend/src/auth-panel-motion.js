let activeTransition;

// Animate the content viewport so the glass, artwork and decorations stay live.
export async function transitionAuthPanel(commit, nextMode) {
  activeTransition?.cancel();
  const viewport = document.querySelector(".auth-panel-viewport");
  const outgoing = viewport?.firstElementChild;
  const reduced = window.matchMedia("(prefers-reduced-motion: reduce)");
  const paused = document.querySelector(
    '.login-motion-toggle[aria-pressed="true"]',
  );
  if (!outgoing || !outgoing.animate || reduced.matches || paused) {
    commit();
    if (!outgoing) {
      document.querySelector(".auth-layout")?.scrollIntoView?.({
        block: "start",
        behavior: "instant",
      });
    }
    return;
  }

  const effects = [];
  let cancelled = false;
  const transition = {
    cancel() {
      if (cancelled) return;
      cancelled = true;
      effects.forEach((effect) => effect.cancel());
      outgoing.inert = false;
      delete viewport.dataset.transitioning;
      if (activeTransition === transition) activeTransition = undefined;
    },
  };
  activeTransition = transition;
  const animate = (element, frames, options) => {
    const effect = element.animate(frames, { fill: "both", ...options });
    effects.push(effect);
    return effect.finished.catch(() => {});
  };
  // Disable the departing form before delaying the mode change.
  outgoing.inert = true;
  viewport.dataset.transitioning = "true";
  const oldHeight = viewport.getBoundingClientRect().height;
  const direction = nextMode === "login" ? -1 : 1;
  await animate(
    outgoing,
    [
      { opacity: 1, transform: "translateX(0) scale(1)", filter: "blur(0px)" },
      {
        opacity: 0,
        transform: `translateX(${-direction * 20}px) scale(0.985)`,
        filter: "blur(4px)",
      },
    ],
    { duration: 140, easing: "cubic-bezier(0.4, 0, 1, 1)" },
  );
  if (cancelled || !viewport.isConnected) {
    transition.cancel();
    return;
  }

  commit();
  const incoming = viewport.firstElementChild;
  const newHeight = incoming.getBoundingClientRect().height;
  const easing = "cubic-bezier(0.22, 1, 0.36, 1)";
  const reveal = [
    animate(
      viewport,
      [{ height: `${oldHeight}px` }, { height: `${newHeight}px` }],
      {
        duration: 460,
        easing,
      },
    ),
    animate(
      incoming,
      [
        {
          opacity: 0,
          transform: `translateX(${direction * 20}px) scale(0.985)`,
          filter: "blur(4px)",
        },
        {
          opacity: 1,
          transform: "translateX(0) scale(1)",
          filter: "blur(0px)",
        },
      ],
      { duration: 360, easing },
    ),
  ];
  const rows = incoming.querySelectorAll(
    ".fb-header-row, .fb-subtitle, form > .form-grid, form > .field, form > .auth-otp, .auth-submit, .auth-register, .auth-forgot, .auth-resend",
  );
  rows.forEach((row, index) =>
    reveal.push(
      animate(
        row,
        [
          { opacity: 0, transform: "translateY(12px)" },
          { opacity: 1, transform: "translateY(0)" },
        ],
        { duration: 300, delay: 35 + index * 18, easing },
      ),
    ),
  );
  await Promise.all(reveal);
  transition.cancel();
}
