import { useId, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useOutsideDismiss } from "./popup-dismissal.jsx";
import { icon } from "./ui.jsx";
import "./quick-create-menu.css";

export function QuickCreateMenu() {
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState({});
  const trigger = useRef(null);
  const menu = useRef(null);
  const lastItem = useRef(false);
  const id = useId();

  function close(restoreFocus = false) {
    setOpen(false);
    if (restoreFocus) trigger.current?.focus();
  }

  useOutsideDismiss([trigger, menu], () => close(), open);
  useLayoutEffect(() => {
    if (!open) return;
    const panel = menu.current;
    panel.showPopover?.();
    const anchor = trigger.current.getBoundingClientRect();
    const width = Math.min(220, window.innerWidth - 16);
    const height = panel.offsetHeight;
    setPosition({
      width,
      left: Math.max(8, Math.min(anchor.right + 8, window.innerWidth - width - 8)),
      top: Math.max(8, Math.min(anchor.top, window.innerHeight - height - 8)),
    });
    const items = panel.querySelectorAll('[role="menuitem"]');
    (lastItem.current ? items[items.length - 1] : items[0])?.focus();
    const dismissScroll = (event) => {
      if (!(event.target instanceof Node) || !panel.contains(event.target))
        close();
    };
    const dismissResize = () => close();
    window.addEventListener("resize", dismissResize);
    window.addEventListener("scroll", dismissScroll, true);
    return () => {
      window.removeEventListener("resize", dismissResize);
      window.removeEventListener("scroll", dismissScroll, true);
    };
  }, [open]);

  function navigate(event) {
    const items = [...menu.current.querySelectorAll('[role="menuitem"]')];
    const index = items.indexOf(document.activeElement);
    if (event.key === "Escape" || event.key === "Tab") {
      if (event.key === "Escape") event.preventDefault();
      event.stopPropagation();
      close(true);
      return;
    }
    const shortcut = { t: 0, e: 1 }[event.key.toLowerCase()];
    if (
      shortcut !== undefined &&
      !event.ctrlKey &&
      !event.metaKey &&
      !event.altKey
    ) {
      event.preventDefault();
      items[shortcut].click();
      return;
    }
    const target = {
      ArrowDown: (index + 1) % items.length,
      ArrowUp: (index - 1 + items.length) % items.length,
      Home: 0,
      End: items.length - 1,
    }[event.key];
    if (target !== undefined) {
      event.preventDefault();
      event.stopPropagation();
      items[target].focus();
    }
  }

  return (
    <>
      <button
        ref={trigger}
        type="button"
        className="btn primary icon-only workspace-rail-add"
        aria-label="Tạo mới"
        title="Tạo mới"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? id : undefined}
        onClick={() => {
          lastItem.current = false;
          setOpen(!open);
        }}
        onKeyDown={(event) => {
          if (["ArrowDown", "ArrowUp"].includes(event.key)) {
            event.preventDefault();
            lastItem.current = event.key === "ArrowUp";
            setOpen(true);
          }
        }}
      >
        {icon("plus")}
      </button>
      {open &&
        createPortal(
          <div
            ref={menu}
            id={id}
            className="quick-create-menu"
            popover={
              typeof HTMLElement.prototype.showPopover === "function"
                ? "manual"
                : undefined
            }
            role="menu"
            aria-label="Tạo mới"
            style={position}
            onKeyDown={navigate}
          >
            <button
              type="button"
              role="menuitem"
              tabIndex={-1}
              data-action="new-task"
              onClick={() => close(true)}
            >
              <span>Công việc</span>
              <kbd aria-hidden="true">T</kbd>
            </button>
            <button
              type="button"
              role="menuitem"
              tabIndex={-1}
              data-action="new-event"
              onClick={() => close(true)}
            >
              <span>Sự kiện</span>
              <kbd aria-hidden="true">E</kbd>
            </button>
          </div>,
          document.body,
        )}
    </>
  );
}
