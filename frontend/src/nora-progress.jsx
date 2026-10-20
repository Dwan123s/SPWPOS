import { useEffect, useState } from "react";
import { icon } from "./ui.jsx";
import { NoraAvatar } from "./nora-mascot.jsx";
import { NoraReply } from "./nora-chat-ui.jsx";
import { NoraPromptMessage } from "./nora-message-actions.jsx";

export function NoraWalker({ active = true }) {
  return (
    <div
      className={`nora-walk-track${active ? " is-active" : ""}`}
      aria-hidden="true"
    >
      <span className="nora-walk-line" />
      <span className="nora-walker">
        <span className="nora-walk-sprite" />
      </span>
      <i />
      <i />
      <i />
    </div>
  );
}

export function NoraProgress({ pending, busy, onEdit }) {
  const [elapsed, setElapsed] = useState(0);
  const [expanded, setExpanded] = useState(false);
  useEffect(() => {
    const update = () =>
      setElapsed(
        Math.max(0, Math.floor((Date.now() - pending.startedAt) / 1000)),
      );
    update();
    if (!busy) return;
    const timer = setInterval(update, 1000);
    return () => clearInterval(timer);
  }, [pending.startedAt, busy]);
  const phase = pending.phases.at(-1);
  return (
    <article className="nora-live-turn">
      <NoraPromptMessage
        text={pending.text}
        createdAt={pending.startedAt}
        busy={busy}
        onEdit={onEdit}
      />
      <div className="nora-live-response">
        <div className="agent-response-heading">
          <strong>
            <NoraAvatar />
            NORA
          </strong>
          <span className="nora-live-badge">
            {busy
              ? pending.reply
                ? "Đang phản hồi"
                : "Đang xử lý"
              : pending.cancelled
                ? "Đã dừng"
                : pending.failure?.category === "validation"
                  ? "Chưa thực hiện"
                  : "Chưa hoàn tất"}
          </span>
        </div>
        <div
          className={`nora-process-card${busy ? " is-working" : ""}${expanded ? " is-expanded" : ""}`}
        >
          <button
            type="button"
            className="nora-process-toggle"
            onClick={() => setExpanded(!expanded)}
            aria-expanded={expanded}
            aria-controls="nora-process-steps"
          >
            <span className="nora-process-indicator" aria-hidden="true">
              {busy ? (
                <>
                  <i />
                  <i />
                  <i />
                </>
              ) : (
                icon("stop")
              )}
            </span>
            <span className="nora-process-label" role="status">
              {busy
                ? phase?.message || "Đang kết nối với NORA"
                : pending.cancelled
                  ? "Đã dừng nhận phản hồi"
                  : pending.failure?.label || "Yêu cầu chưa hoàn tất"}
            </span>
            <small>{elapsed}s</small>
            <span className="nora-process-chevron" aria-hidden="true">
              {icon("arrow")}
            </span>
          </button>
          <NoraWalker active={busy} />
          <button
            type="button"
            className="nora-process-caption"
            onClick={() => setExpanded(!expanded)}
            aria-expanded={expanded}
            aria-controls="nora-process-steps"
          >
            <span>
              {expanded ? "Thu gọn các bước" : "Xem các bước kiểm tra"}
            </span>
            <span>{Math.max(0, pending.phases.length - 1)} bước hoàn tất</span>
          </button>
          {expanded && (
            <ol
              id="nora-process-steps"
              className="nora-process-steps"
              aria-label="Các bước xử lý thực tế"
            >
              {pending.phases.map((step, index) => (
                <li
                  key={step.stage}
                  className={
                    index === pending.phases.length - 1
                      ? busy
                        ? "is-current"
                        : "is-stopped"
                      : "is-complete"
                  }
                >
                  <span>
                    {icon(
                      index === pending.phases.length - 1
                        ? busy
                          ? "clock"
                          : "stop"
                        : "check",
                    )}
                  </span>
                  <div>
                    {step.message}
                    <small>
                      {step.stage === "UNDERSTANDING"
                        ? "Đọc yêu cầu trong ngữ cảnh lịch của bạn"
                        : step.stage === "PLANNING"
                          ? "Đối chiếu giờ rảnh, khoảng bận và giới hạn"
                          : step.stage === "SAVING"
                            ? "Chỉ lưu sau khi kế hoạch được kiểm tra"
                            : "Chỉ sử dụng dữ liệu thuộc tài khoản của bạn"}
                    </small>
                  </div>
                </li>
              ))}
            </ol>
          )}
        </div>
        {pending.reply && <NoraReply text={pending.reply} streaming={busy} />}
        {busy && (
          <small className="nora-draft-caption">
            Phản hồi đang được soạn. Kết quả lịch sẽ xuất hiện sau khi kiểm tra
            xong.
          </small>
        )}
        {!busy && (
          <small className="nora-draft-caption" role="status">
            {pending.reply && <>Phản hồi trên là bản nháp chưa hoàn tất. </>}
            {pending.failure?.saveStatus ||
              "Chưa nhận được kết quả xác nhận; hãy kiểm tra lịch sử."}
          </small>
        )}
      </div>
    </article>
  );
}
