import { useEffect, useRef, useState } from "react";
import { icon, state } from "./ui.jsx";

export function NoraMessageTime({ value }) {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  const timeZone =
    state.workspace?.preference?.timeZoneId || "Asia/Ho_Chi_Minh";
  return (
    <time
      dateTime={date.toISOString()}
      title={date.toLocaleString("vi-VN", { timeZone })}
    >
      {new Intl.DateTimeFormat("vi-VN", {
        timeZone,
        day: "2-digit",
        month: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
      }).format(date)}
    </time>
  );
}

export function NoraCopyButton({ text, label, onError }) {
  const [copied, setCopied] = useState(false);
  useEffect(() => {
    if (!copied) return;
    const timer = setTimeout(() => setCopied(false), 2000);
    return () => clearTimeout(timer);
  }, [copied]);
  return (
    <button
      type="button"
      className="nora-copy-button"
      aria-label={label}
      title={copied ? "Đã sao chép" : label}
      onClick={async () => {
        onError(false);
        try {
          await navigator.clipboard.writeText(text);
          setCopied(true);
        } catch {
          setCopied(false);
          onError(true);
        }
      }}
    >
      {icon(copied ? "check" : "copy")}
      <span className="nora-sr-only" role="status">
        {copied ? "Đã sao chép" : ""}
      </span>
    </button>
  );
}

export function NoraPromptMessage({ text, image, createdAt, busy, onEdit }) {
  const [copyError, setCopyError] = useState(false);
  const preview = useRef(null);
  const imageUrl =
    image?.url &&
    (/^data:image\/(?:png|jpeg|webp);base64,[a-z0-9+/=]+$/i.test(image.url) ||
      /^\/api\/agent\/turns\/[a-f0-9-]+\/image$/i.test(image.url))
      ? image.url
      : null;
  return (
    <div className="nora-prompt-message">
      <div className="agent-user-message">
        <span>Bạn</span>
        {imageUrl && (
          <>
            <button
              type="button"
              className="nora-message-image"
              aria-label="Xem ảnh thời khóa biểu"
              onClick={() => preview.current?.showModal()}
            >
              <img
                src={imageUrl}
                alt={image.name || "Ảnh thời khóa biểu"}
                loading="lazy"
              />
            </button>
            <dialog
              ref={preview}
              className="nora-image-dialog"
              aria-label="Ảnh thời khóa biểu phóng to"
            >
              <button
                type="button"
                autoFocus
                aria-label="Đóng ảnh"
                onClick={() => preview.current?.close()}
              >
                {icon("close")}
              </button>
              <img src={imageUrl} alt={image.name || "Ảnh thời khóa biểu"} />
            </dialog>
          </>
        )}
        {text && <p>{text}</p>}
      </div>
      <div className="nora-message-footer nora-prompt-footer">
        <NoraMessageTime value={createdAt} />
        <NoraCopyButton
          text={text}
          label="Sao chép yêu cầu"
          onError={setCopyError}
        />
        <button
          className="nora-edit-request"
          type="button"
          disabled={busy || !onEdit}
          onClick={() => onEdit(text)}
          aria-label="Sửa và gửi lại yêu cầu"
          title="Chép vào ô nhập để sửa và gửi thành yêu cầu mới"
        >
          {icon("edit")}
        </button>
      </div>
      {copyError && (
        <small className="nora-copy-error" role="status">
          Không thể sao chép tự động. Bạn có thể chọn nội dung yêu cầu để sao
          chép.
        </small>
      )}
    </div>
  );
}
