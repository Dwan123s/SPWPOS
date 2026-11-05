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
  const [selectedImage, setSelectedImage] = useState(null);
  const images = (
    image ? [image, ...(image.additionalImages || [])] : []
  ).filter(
    (part) =>
      typeof part.url === "string" &&
      (/^data:image\/(?:png|jpeg|webp);base64,[a-z0-9+/=]+$/i.test(part.url) ||
        /^\/api\/agent\/turns\/[a-f0-9-]+\/image(?:\?index=[01])?$/i.test(
          part.url,
        )),
  );
  return (
    <div className="nora-prompt-message">
      <div className="agent-user-message">
        <span>Bạn</span>
        {images.length > 0 && (
          <div className="nora-message-images">
            {images.map((part, index) => (
              <button
                type="button"
                className="nora-message-image"
                key={part.url}
                aria-label={
                  images.length === 1
                    ? "Xem ảnh thời khóa biểu"
                    : `Xem ảnh thời khóa biểu ${index + 1}`
                }
                onClick={() => {
                  setSelectedImage(part);
                  preview.current?.showModal();
                }}
              >
                <img
                  src={part.url}
                  alt={part.name || "Ảnh thời khóa biểu"}
                  loading="lazy"
                />
              </button>
            ))}
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
              {selectedImage && (
                <img
                  src={selectedImage.url}
                  alt={selectedImage.name || "Ảnh thời khóa biểu"}
                />
              )}
            </dialog>
          </div>
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
