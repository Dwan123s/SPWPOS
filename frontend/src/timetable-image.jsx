import { useEffect, useRef, useState } from "react";
import { icon } from "./ui.jsx";
import "./styles/timetable-image.css";

export const TIMETABLE_IMAGE_MAX_BYTES = 5 * 1024 * 1024;
export const TIMETABLE_TEXT_MAX_LENGTH = 1600;
const formats = ["image/png", "image/jpeg", "image/webp"];
const defaultRequest =
  "Nhập thời khóa biểu, tự phân tích chi tiết lịch bận và các khoảng rảnh từng ngày.";
export function timetableRequest(text, reading) {
  if (!reading?.trim()) return text.trim();
  return `${text.trim() || defaultRequest}\n\nThời khóa biểu từ ảnh (dữ liệu để đối chiếu):\n${reading.trim()}\n\nHỏi lại nếu thiếu ngày áp dụng, giờ học hoặc số tuần lặp; không tự đoán.`;
}

function previewImage(file, signal) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    const abort = () => reader.abort();
    reader.onload = () => resolve(reader.result);
    reader.onerror = () =>
      reject(new Error("Chưa mở được ảnh. Bạn chọn lại ảnh nhé."));
    reader.onabort = () => reject(new DOMException("Cancelled", "AbortError"));
    reader.onloadend = () => signal.removeEventListener("abort", abort);
    signal.addEventListener("abort", abort, { once: true });
    reader.readAsDataURL(file);
  });
}

export function useTimetableImage({ enabled, readImage }) {
  const [attachment, setAttachment] = useState(null);
  const [phase, setPhase] = useState("idle");
  const [error, setError] = useState("");
  const session = useRef(null);
  const enabledRef = useRef(enabled);
  enabledRef.current = enabled;
  const cancel = () => {
    session.current?.abort();
    session.current = null;
  };
  useEffect(
    () => () => {
      cancel();
    },
    [],
  );
  function remove() {
    cancel();
    setAttachment(null);
    setPhase("idle");
    setError("");
  }
  function cancelRead() {
    cancel();
    setPhase(attachment ? (attachment.text ? "ready" : "selected") : "idle");
  }
  async function select(file) {
    if (!file || !enabledRef.current) return;
    if (!formats.includes(file.type)) {
      setError("Bạn chọn ảnh PNG, JPG hoặc WebP nhé.");
      return;
    }
    if (!file.size || file.size > TIMETABLE_IMAGE_MAX_BYTES) {
      setError("Ảnh cần có dung lượng từ 1 byte đến 5 MB.");
      return;
    }
    cancel();
    const controller = new AbortController();
    session.current = controller;
    setAttachment(null);
    setError("");
    setPhase("selecting");
    try {
      const preview = await previewImage(file, controller.signal);
      if (session.current !== controller) return;
      setAttachment({
        file,
        preview,
        text: "",
        warnings: [],
        needsReview: false,
      });
      setPhase("selected");
    } catch (failure) {
      if (session.current === controller) {
        setError(failure.message);
        setPhase("idle");
      }
    }
  }
  function paste(event) {
    if (!enabledRef.current || phase === "reading") return;
    const clipboard = event.clipboardData;
    const item = Array.from(clipboard?.items || []).find(
      (entry) => entry.kind === "file" && entry.type?.startsWith("image/"),
    );
    const file =
      item?.getAsFile?.() ||
      Array.from(clipboard?.files || []).find((entry) =>
        entry.type?.startsWith("image/"),
      );
    // Leave ordinary text/HTML pastes to the browser. Only accept actual
    // clipboard image files; never fetch an image URL copied from a page.
    if (!file) return;
    event.preventDefault();
    void select(file);
  }
  async function read() {
    if (!attachment || !enabledRef.current || phase === "reading") return;
    if (phase === "ready") return attachment;
    cancel();
    const controller = new AbortController();
    session.current = controller;
    setPhase("reading");
    setError("");
    try {
      const reading = await readImage(attachment.file, controller.signal);
      if (session.current !== controller) return;
      setAttachment((previous) => ({ ...previous, ...reading }));
      setPhase("ready");
      return reading;
    } catch (failure) {
      if (session.current === controller) {
        setError(failure.message);
        setPhase("selected");
      }
      throw failure;
    }
  }
  function edit(value) {
    setAttachment((previous) =>
      previous ? { ...previous, text: value } : null,
    );
  }
  return {
    attachment,
    phase,
    error,
    select,
    paste,
    read,
    edit,
    remove,
    cancelRead,
    active: phase === "selecting" || phase === "reading",
    ready: phase === "ready" && !!attachment?.text.trim(),
  };
}

export function TimetableImageButton({ image, disabled }) {
  const field = useRef(null);
  return (
    <>
      <input
        ref={field}
        type="file"
        accept={formats.join(",")}
        className="nora-sr-only"
        aria-label="Chọn ảnh thời khóa biểu"
        disabled={disabled}
        onChange={(event) => {
          const file = event.target.files?.[0];
          event.target.value = "";
          image.select(file);
        }}
      />
      <button
        type="button"
        className="nora-image-button"
        aria-label="Đính kèm ảnh thời khóa biểu"
        title="Đính kèm ảnh thời khóa biểu"
        disabled={disabled}
        onClick={() => field.current?.click()}
      >
        {icon("image")}
      </button>
    </>
  );
}

export function TimetableImagePreview({ image, disabled, provider }) {
  const value = image.attachment;
  if (!value && !image.error && image.phase !== "selecting") return null;
  return (
    <section
      className="nora-timetable-attachment"
      aria-label="Ảnh thời khóa biểu"
    >
      {value && (
        <>
          <div className="nora-timetable-heading">
            <img src={value.preview} alt="Ảnh thời khóa biểu đã chọn" />
            <div>
              <strong>{value.file.name}</strong>
              <small>Thời khóa biểu · PNG, JPG, WebP · tối đa 5 MB</small>
            </div>
            <button
              type="button"
              aria-label="Gỡ ảnh thời khóa biểu"
              disabled={disabled}
              onClick={image.remove}
            >
              {icon("close")}
            </button>
          </div>
          {image.phase === "selected" && (
            <p>
              Bấm Gửi để NORA đọc ảnh bằng{" "}
              {provider || "dịch vụ AI đang cấu hình"} và tự tính giờ rảnh từng ngày. Bạn có thể
              ghi thêm ngày áp dụng hoặc số tuần học bên dưới.
            </p>
          )}
          {image.phase === "reading" && (
            <p role="status">NORA đang đọc môn học, ngày và giờ trong ảnh…</p>
          )}
          {image.phase === "ready" && (
            <>
              <label className="field">
                <span>Nội dung đọc từ ảnh — bạn có thể sửa lại</span>
                <textarea
                  aria-label="Nội dung thời khóa biểu"
                  rows={4}
                  value={value.text}
                  maxLength={TIMETABLE_TEXT_MAX_LENGTH}
                  disabled={disabled}
                  onChange={(event) => image.edit(event.target.value)}
                />
              </label>
              {value.warnings.length > 0 && (
                <ul className="nora-timetable-warnings">
                  {value.warnings.map((warning, i) => (
                    <li key={i}>{warning}</li>
                  ))}
                </ul>
              )}
              {value.needsReview && (
                <p>
                  Ảnh còn chi tiết cần làm rõ. Bạn bổ sung ngày áp dụng, giờ
                  theo tiết hoặc các phần chưa rõ trước khi gửi nhé.
                </p>
              )}
              <small>
                NORA dùng nội dung này cùng yêu cầu của bạn để xếp lịch.
              </small>
            </>
          )}
        </>
      )}
      {image.phase === "selecting" && <p role="status">Đang mở ảnh…</p>}
      {image.error && <p role="alert">{image.error}</p>}
    </section>
  );
}
