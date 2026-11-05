import { useEffect, useRef, useState } from "react";

// Handle-only pointer dragging supports mouse, pen and touch without making
// ordinary scrolling or selecting task text move an item.
export function useEisenhowerDrag(resolve, commit, disabled = false) {
  const latest = useRef({ resolve, commit, disabled });
  latest.current = { resolve, commit, disabled };
  const cleanup = useRef(null);
  const [drag, setDrag] = useState(null);
  useEffect(() => () => cleanup.current?.(), []);
  function start(event, id) {
    if (latest.current.disabled || event.button !== 0) return;
    event.stopPropagation();
    cleanup.current?.();
    const from = { x: event.clientX, y: event.clientY },
      pointer = event.pointerId;
    let moved = false,
      target = null;
    const end = () => {
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", up);
      window.removeEventListener("pointercancel", cancel);
      window.removeEventListener("keydown", key);
      document.body.classList.remove("eis-is-dragging");
      cleanup.current = null;
      setDrag(null);
    };
    const move = (e) => {
      if (e.pointerId !== pointer) return;
      if (!moved && Math.hypot(e.clientX - from.x, e.clientY - from.y) < 8)
        return;
      moved = true;
      e.preventDefault();
      target = latest.current.resolve(
        document.elementFromPoint(e.clientX, e.clientY),
        id,
        e.clientY,
      );
      setDrag({ id, target });
      document.body.classList.add("eis-is-dragging");
      const scroll = document
        .elementFromPoint(e.clientX, e.clientY)
        ?.closest(".eis-items");
      if (scroll) {
        const bounds = scroll.getBoundingClientRect();
        if (e.clientY < bounds.top + 35) scroll.scrollTop -= 20;
        if (e.clientY > bounds.bottom - 35) scroll.scrollTop += 20;
      }
    };
    const up = (e) => {
      if (e.pointerId !== pointer) return;
      // Resolve the actual release point as well; leaving the board cancels.
      const drop = moved
        ? latest.current.resolve(
            document.elementFromPoint(e.clientX, e.clientY),
            id,
            e.clientY,
          )
        : null;
      end();
      if (moved && drop && !latest.current.disabled)
        latest.current.commit(id, drop);
    };
    const cancel = () => end();
    const key = (e) => {
      if (e.key === "Escape") {
        e.preventDefault();
        end();
      }
    };
    window.addEventListener("pointermove", move, { passive: false });
    window.addEventListener("pointerup", up);
    window.addEventListener("pointercancel", cancel);
    window.addEventListener("keydown", key);
    cleanup.current = end;
  }
  return { drag, start };
}

export function moveChecklist(items, id, beforeId) {
  if (id === beforeId) return items;
  const selected = items.find((i) => i.id === id);
  if (!selected || (beforeId && !items.some((i) => i.id === beforeId)))
    return items;
  const rest = items.filter((i) => i.id !== id);
  rest.splice(
    beforeId ? rest.findIndex((i) => i.id === beforeId) : rest.length,
    0,
    selected,
  );
  return rest;
}
