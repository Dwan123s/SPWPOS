import { useEffect, useRef, useState } from "react";
import { state } from "./ui.jsx";
import { createCalendarRange, minuteHeight } from "./calendar-drag.js";

export function useCalendarRangeSelect({
  host,
  scroll,
  gesture,
  suppressClick,
  busy,
  scopeKey,
  onSelect,
  onError,
}) {
  const [selection, setSelection] = useState(null),
    [touchSelect, setTouchSelect] = useState(false);
  const current = useRef(null);
  const openingClickCleanup = useRef(null);
  current.current = { onSelect, onError };
  useEffect(() => {
    if (!touchSelect) return;
    const escape = (event) => {
      if (event.key === "Escape") setTouchSelect(false);
    };
    window.addEventListener("keydown", escape);
    return () => window.removeEventListener("keydown", escape);
  }, [touchSelect]);
  useEffect(
    () => () => {
      if (gesture.current?.kind === "select") gesture.current.cleanup();
      openingClickCleanup.current?.();
    },
    [scopeKey],
  );
  function pointerDown(event) {
    if (
      busy.current ||
      gesture.current ||
      event.button !== 0 ||
      event.target.closest(
        ".calendar-positioned-item,button,input,textarea,select,a",
      )
    )
      return;
    const track = event.currentTarget,
      rect = track.getBoundingClientRect();
    const anchor = {
      date: track.dataset.calendarDate,
      minute: (event.clientY - rect.top) / minuteHeight,
    };
    const info = {
      kind: "select",
      pointerId: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      active: false,
      touch: event.pointerType === "touch",
      userId: state.workspace?.profile?.id,
      range: null,
      valid: true,
      cleanup: null,
    };
    try {
      info.range = createCalendarRange(anchor);
      if (Date.parse(info.range.start) <= Date.now()) {
        current.current.onError(
          "Không thể tạo lịch trong quá khứ. Hãy chọn khoảng giờ sắp tới.",
        );
        return;
      }
    } catch (failure) {
      current.current.onError(failure.message);
      return;
    }
    gesture.current = info;
    setSelection(info.range);
    function update() {
      const clip = scroll.current?.getBoundingClientRect();
      const lane = [
        ...(host.current?.querySelectorAll("[data-calendar-date]") || []),
      ].find((node) => {
        const box = node.getBoundingClientRect();
        return info.x >= box.left && info.x < box.right;
      });
      info.valid =
        !!clip &&
        !!lane &&
        info.x >= clip.left + 54 &&
        info.x < clip.right &&
        info.y >= clip.top + 64 &&
        info.y <= clip.bottom;
      if (!info.valid) {
        info.range = null;
        setSelection(null);
        return;
      }
      try {
        info.range = createCalendarRange(
          anchor,
          {
            date: lane.dataset.calendarDate,
            minute: (info.y - lane.getBoundingClientRect().top) / minuteHeight,
          },
          info.active,
        );
        if (Date.parse(info.range.start) <= Date.now()) {
          info.range = null;
          info.valid = false;
          setSelection(null);
          return;
        }
        setSelection(info.range);
      } catch {
        info.range = null;
        info.valid = false;
        setSelection(null);
      }
    }
    function move(e) {
      if (e.pointerId !== info.pointerId) return;
      info.x = e.clientX;
      info.y = e.clientY;
      if (
        !info.active &&
        Math.hypot(info.x - event.clientX, info.y - event.clientY) < 6
      )
        return;
      if (info.touch && !touchSelect) {
        cleanup();
        return;
      }
      info.active = true;
      e.preventDefault();
      document.body.classList.add("calendar-is-selecting");
      update();
    }
    function finish(e) {
      if (e.pointerId !== info.pointerId) return;
      info.x = e.clientX;
      info.y = e.clientY;
      update();
      const range = info.range,
        valid = info.valid && state.workspace?.profile?.id === info.userId;
      cleanup();
      if (!valid || !range) return;
      if (Date.parse(range.start) <= Date.now()) {
        current.current.onError("Khoảng giờ đã qua. Hãy chọn giờ mới.");
        return;
      }
      // showModal() can retarget the compatibility click following pointerup to
      // its backdrop. Consume that one click before outside-dismiss listeners
      // see it, while allowing the next deliberate pointer gesture normally.
      let timer;
      const clearOpeningClick = () => {
        clearTimeout(timer);
        window.removeEventListener("click", openingClick, true);
        window.removeEventListener("pointerdown", clearOpeningClick, true);
        openingClickCleanup.current = null;
      };
      const openingClick = (click) => {
        if (Math.hypot(click.clientX - info.x, click.clientY - info.y) > 4)
          return;
        clearOpeningClick();
        click.preventDefault();
        click.stopImmediatePropagation();
      };
      window.addEventListener("click", openingClick, true);
      window.addEventListener("pointerdown", clearOpeningClick, true);
      openingClickCleanup.current = clearOpeningClick;
      timer = setTimeout(clearOpeningClick, 400);
      suppressClick.current = true;
      setTimeout(() => {
        suppressClick.current = false;
      }, 0);
      setTouchSelect(false);
      current.current.onSelect(range);
    }
    function cancel(e) {
      if (e?.pointerId != null && e.pointerId !== info.pointerId) return;
      cleanup();
    }
    function key(e) {
      if (e.key === "Escape") {
        e.preventDefault();
        cleanup();
        setTouchSelect(false);
      }
    }
    function visibility() {
      if (document.hidden) cleanup();
    }
    const timer = setInterval(() => {
      if (!info.active || !scroll.current) return;
      const box = scroll.current.getBoundingClientRect(),
        view = scroll.current;
      if (
        info.y < box.top ||
        info.y > box.bottom ||
        info.x < box.left ||
        info.x > box.right
      )
        return;
      let moved = false;
      if (info.y > box.bottom - 40) {
        view.scrollTop += 16;
        moved = true;
      } else if (info.y < box.top + 100) {
        view.scrollTop -= 16;
        moved = true;
      }
      if (info.x > box.right - 30) {
        view.scrollLeft += 14;
        moved = true;
      } else if (info.x < box.left + 85) {
        view.scrollLeft -= 14;
        moved = true;
      }
      if (moved) update();
    }, 50);
    function cleanup() {
      clearInterval(timer);
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", finish);
      window.removeEventListener("pointercancel", cancel);
      window.removeEventListener("keydown", key);
      window.removeEventListener("blur", cancel);
      document.removeEventListener("visibilitychange", visibility);
      document.body.classList.remove("calendar-is-selecting");
      if (gesture.current === info) gesture.current = null;
      setSelection(null);
    }
    info.cleanup = cleanup;
    window.addEventListener("pointermove", move, { passive: false });
    window.addEventListener("pointerup", finish);
    window.addEventListener("pointercancel", cancel);
    window.addEventListener("keydown", key);
    window.addEventListener("blur", cancel);
    document.addEventListener("visibilitychange", visibility);
  }
  return { selection, pointerDown, touchSelect, setTouchSelect };
}
