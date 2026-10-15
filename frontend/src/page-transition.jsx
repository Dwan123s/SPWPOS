import { useLayoutEffect, useRef } from "react";

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
    const animation = host.current.animate?.(
      [
        { opacity: 0, transform: "translateY(7px)" },
        { opacity: 1, transform: "translateY(0)" },
      ],
      { duration: 240, easing: "cubic-bezier(.22,1,.36,1)" },
    );
    return () => animation?.cancel();
  }, [route]);
  return (
    <div ref={host} className="page-scene" data-page={route}>
      {children}
    </div>
  );
}
