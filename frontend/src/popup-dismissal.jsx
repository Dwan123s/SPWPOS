import { forwardRef, useEffect, useRef } from "react";

function isOutside(event, root) {
  if (event.composedPath().includes(root)) {
    if (root.tagName !== "DIALOG" || event.target !== root) return false;
    const rect = root.getBoundingClientRect();
    return (
      event.clientX < rect.left ||
      event.clientX > rect.right ||
      event.clientY < rect.top ||
      event.clientY > rect.bottom
    );
  }
  return true;
}

// Capture clicks so child controls stopping propagation do not strand a popup.
// Remember where a gesture started: selecting text then releasing outside is
// not a request to discard an editor.
export function useOutsideDismiss(refs, dismiss, enabled = true) {
  const latest = useRef({ refs, dismiss });
  latest.current = { refs, dismiss };
  useEffect(() => {
    if (!enabled) return;
    let startedOutside = null;
    const outside = (event) => {
      const roots = latest.current.refs
        .map((ref) => ref.current)
        .filter(Boolean);
      if (!roots.length) return false;
      const topDialog = [...document.querySelectorAll("dialog[open]")].at(-1);
      if (
        topDialog &&
        !roots.some((root) => root === topDialog || topDialog.contains(root))
      )
        return false;
      return roots.every((root) => isOutside(event, root));
    };
    const down = (event) => {
      startedOutside = outside(event);
    };
    const click = (event) => {
      const dismissible = startedOutside !== false && outside(event);
      startedOutside = null;
      if (dismissible) latest.current.dismiss();
    };
    const cancel = () => {
      startedOutside = null;
    };
    document.addEventListener("pointerdown", down, true);
    document.addEventListener("pointercancel", cancel, true);
    document.addEventListener("click", click, true);
    return () => {
      document.removeEventListener("pointerdown", down, true);
      document.removeEventListener("pointercancel", cancel, true);
      document.removeEventListener("click", click, true);
    };
  }, [enabled]);
}

// Use for floating panels only; ordinary inline disclosures remain expanded.
export const PopupDetails = forwardRef(function PopupDetails(
  { children, onKeyDown, ...props },
  forwardedRef,
) {
  const root = useRef(null);
  useOutsideDismiss([root], () => {
    if (root.current) root.current.open = false;
  });
  return (
    <details
      {...props}
      ref={(node) => {
        root.current = node;
        if (typeof forwardedRef === "function") forwardedRef(node);
        else if (forwardedRef) forwardedRef.current = node;
      }}
      onKeyDown={(event) => {
        onKeyDown?.(event);
        if (
          event.key === "Escape" &&
          !event.defaultPrevented &&
          root.current?.open
        ) {
          event.preventDefault();
          event.stopPropagation();
          root.current.open = false;
          root.current.querySelector("summary")?.focus();
        }
      }}
    >
      {children}
    </details>
  );
});
