import { useLayoutEffect, useRef } from "react";

// Sampled damped spring: damping ratio .55, angular frequency 22 rad/s.
// Match the native view-transition stops in workspace-polish.css.
const pageArrival = [
  { offset: 0, opacity: 0, transform: "translateY(20px) scale(0.985)" },
  {
    offset: 0.05,
    opacity: 0.227,
    transform: "translateY(17.733px) scale(0.9867)",
  },
  {
    offset: 0.1,
    opacity: 0.455,
    transform: "translateY(12.801px) scale(0.9904)",
  },
  {
    offset: 0.15,
    opacity: 0.682,
    transform: "translateY(7.396px) scale(0.99445)",
  },
  {
    offset: 0.2,
    opacity: 0.909,
    transform: "translateY(2.845px) scale(0.99787)",
  },
  { offset: 0.25, opacity: 1, transform: "translateY(-0.29px) scale(1.00022)" },
  { offset: 0.3, opacity: 1, transform: "translateY(-1.983px) scale(1.00149)" },
  {
    offset: 0.35,
    opacity: 1,
    transform: "translateY(-2.521px) scale(1.00189)",
  },
  { offset: 0.4, opacity: 1, transform: "translateY(-2.301px) scale(1.00173)" },
  {
    offset: 0.45,
    opacity: 1,
    transform: "translateY(-1.702px) scale(1.00128)",
  },
  { offset: 0.5, opacity: 1, transform: "translateY(-1.016px) scale(1.00076)" },
  { offset: 0.6, opacity: 1, transform: "translateY(-0.002px) scale(1)" },
  { offset: 0.7, opacity: 1, transform: "translateY(0.316px) scale(0.99976)" },
  { offset: 0.8, opacity: 1, transform: "translateY(0.226px) scale(0.99983)" },
  { offset: 0.9, opacity: 1, transform: "translateY(0.062px) scale(0.99995)" },
  { offset: 1, opacity: 1, transform: "translateY(0px) scale(1)" },
];

export function motionAllowed() {
  try {
    return (
      !document.hidden &&
      !window.matchMedia("(prefers-reduced-motion: reduce)").matches &&
      localStorage.getItem("planora.workspace.motion-paused") !== "true"
    );
  } catch {
    return false;
  }
}
export function PageTransition({ route, children }) {
  const host = useRef(null);
  const lastRoute = useRef(route);
  useLayoutEffect(() => {
    const changed = lastRoute.current !== route;
    lastRoute.current = route;
    if (changed) {
      window.scrollTo?.({ top: 0, behavior: "instant" });
      const target =
        route === "agent"
          ? host.current.closest("main")
          : host.current.querySelector("h1");
      target?.focus({ preventScroll: true });
    }
    if (!motionAllowed() || typeof document.startViewTransition === "function")
      return;
    const animation = host.current.animate?.(pageArrival, {
      duration: 480,
      easing: "linear",
    });
    return () => animation?.cancel();
  }, [route]);
  return (
    <div ref={host} className="page-scene" data-page={route}>
      {children}
    </div>
  );
}
