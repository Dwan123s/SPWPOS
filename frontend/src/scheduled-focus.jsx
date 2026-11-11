import { useEffect, useState } from "react";
import { button, dateFmt, timeFmt } from "./ui.jsx";
import { nextWorkflowStep } from "./workflow.js";
import { scheduledFocus, scheduleClock } from "./scheduled-focus.js";
import { FinishWorkButton } from "./work-time-review.jsx";
import "./styles/scheduled-focus.css";
import { FocusLauncher } from "./focus-timer.jsx";

export function ScheduledFocus({ workspace, onWorkspaceChanged }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const refresh = () => {
      if (!document.hidden) setNow(Date.now());
    };
    const interval = setInterval(refresh, 1000);
    window.addEventListener("focus", refresh);
    document.addEventListener("visibilitychange", refresh);
    refresh();
    return () => {
      clearInterval(interval);
      window.removeEventListener("focus", refresh);
      document.removeEventListener("visibilitychange", refresh);
    };
  }, []);
  const { current, next } = scheduledFocus(workspace, now);
  const focus = current || next;
  const hasCalendar =
    (workspace.events?.length || 0) + (workspace.sessions?.length || 0) > 0;
  const workflow = nextWorkflowStep(workspace, now);
  const remaining = focus
    ? ((current ? Date.parse(focus.end) : Date.parse(focus.start)) - now) / 1000
    : 0;
  const progress = current
    ? Math.min(
        100,
        Math.max(
          0,
          (100 * (now - Date.parse(current.start))) /
            (Date.parse(current.end) - Date.parse(current.start)),
        ),
      )
    : 0;
  return (
    <section
      className={`focus-cockpit scheduled-focus ${current ? "is-active" : ""}`}
      aria-label="Phiên tập trung"
    >
      <div className="cockpit-timer">
        <span>
          {current
            ? "ĐẾM NGƯỢC KẾT THÚC"
            : focus
              ? "BẮT ĐẦU SAU"
              : "PHIÊN TẬP TRUNG"}
        </span>
        <strong className="cockpit-time" role="timer" aria-live="off">
          {scheduleClock(remaining)}
        </strong>
        <small>
          {focus
            ? `${dateFmt(focus.start)} · ${timeFmt(focus.start)} — ${timeFmt(focus.end)}`
            : "Chưa có lịch sắp tới"}
        </small>
      </div>
      <div className="cockpit-content">
        <span className="console-kicker">
          {current
            ? "ĐANG DIỄN RA · TỰ ĐỘNG THEO LỊCH"
            : focus
              ? "LỊCH SẮP TỚI · TỰ ĐỘNG BẮT ĐẦU"
              : "CHUẨN BỊ LỊCH CỦA BẠN"}
        </span>
        <h2>
          {focus?.title ||
            (hasCalendar ? "Không còn lịch sắp tới" : workflow.title)}
        </h2>
        <p className="reason">
          {current
            ? "Phiên được kích hoạt theo lịch và tự chuyển khi hết giờ."
            : focus
              ? `Phiên sẽ tự kích hoạt lúc ${timeFmt(focus.start)} · ${dateFmt(focus.start)}.`
              : hasCalendar
                ? "Phiên tiếp theo sẽ tự hiện khi có lịch mới."
                : workflow.description}
        </p>
        {current && (
          <progress
            className="scheduled-focus-progress"
            aria-label="Tiến độ thời gian theo lịch"
            value={progress}
            max="100"
          />
        )}
        <div className="cockpit-actions">
          <FocusLauncher compact />
          {current && focus.kind !== "event" && (
            <FinishWorkButton
              sessionId={focus.id}
              onWorkspaceChanged={onWorkspaceChanged}
            />
          )}
          {focus
            ? button(
                "Chi tiết lịch",
                focus.kind === "event" ? "edit-event" : "session-detail",
                "small secondary",
                focus.id,
                "calendar",
              )
            : hasCalendar
              ? button(
                  "Mở lịch",
                  "nav",
                  "small secondary",
                  "calendar",
                  "calendar",
                )
              : null}
        </div>
      </div>
    </section>
  );
}
