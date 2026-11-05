import { useEffect, useRef, useState } from "react";
import { icon } from "./ui.jsx";
import "./styles/timetable-image.css";

export const TIMETABLE_IMAGE_MAX_COUNT = 2;
export const timetableImagePayload = (attachment, preview = false) => {
  if (!attachment) return null;
  const parts = (attachment.images || [attachment]).map((part) => ({
    name: part.file.name,
    mimeType: part.file.type,
    ...(preview ? { url: part.preview } : { data: part.preview.split(",")[1] }),
  }));
  return {
    ...parts[0],
    ...(parts.length > 1 ? { additionalImages: parts.slice(1) } : {}),
  };
};
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
  function remove(index) {
    if (typeof index === "number" && attachment?.images?.length > 1) {
      cancel();
      const images = attachment.images.filter(
        (_, position) => position !== index,
      );
      setAttachment({
        ...attachment,
        ...images[0],
        images,
        text: "",
        warnings: [],
        needsReview: false,
      });
      setPhase("selected");
      setError("");
      return;
    }
    cancel();
    setAttachment(null);
    setPhase("idle");
    setError("");
  }
  function cancelRead() {
    cancel();
    setPhase(attachment ? (attachment.text ? "ready" : "selected") : "idle");
  }
  async function select(selection) {
    if (!selection || !enabledRef.current) return;
    const files = Array.isArray(selection) ? selection : [selection];
    if (!files.length) return;
    const existing =
      files.length === 1
        ? attachment?.images || (attachment ? [attachment] : [])
        : [];
    if (existing.length + files.length > TIMETABLE_IMAGE_MAX_COUNT) {
      setError("Mỗi lần gửi tối đa 2 ảnh thời khóa biểu.");
      return;
    }
    for (const file of files) {
      if (!formats.includes(file.type)) {
        setError("Bạn chọn ảnh PNG, JPG hoặc WebP nhé.");
        return;
      }
      if (!file.size || file.size > TIMETABLE_IMAGE_MAX_BYTES) {
        setError("Ảnh cần có dung lượng từ 1 byte đến 5 MB.");
        return;
      }
    }
    cancel();
    const controller = new AbortController();
    session.current = controller;
    setAttachment(null);
    setError("");
    setPhase("selecting");
    try {
      const images = [
        ...existing,
        ...(await Promise.all(
          files.map(async (file) => ({
            file,
            preview: await previewImage(file, controller.signal),
          })),
        )),
      ];
      if (session.current !== controller) return;
      setAttachment({
        ...images[0],
        images,
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
    const items = Array.from(clipboard?.items || [])
      .filter(
        (entry) => entry.kind === "file" && entry.type?.startsWith("image/"),
      )
      .map((entry) => entry.getAsFile?.())
      .filter(Boolean);
    const files = items.length
      ? items
      : Array.from(clipboard?.files || []).filter((entry) =>
          entry.type?.startsWith("image/"),
        );
    // Leave ordinary text/HTML pastes to the browser. Only accept actual
    // clipboard image files; never fetch an image URL copied from a page.
    if (!files.length) return;
    event.preventDefault();
    void select(files);
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
      const readings = [];
      for (const part of attachment.images || [attachment])
        readings.push(await readImage(part.file, controller.signal));
      const reading =
        readings.length === 1
          ? readings[0]
          : {
              text: readings
                .map((item, index) => `Ảnh ${index + 1}:\n${item.text}`)
                .join("\n\n"),
              warnings: readings.flatMap((item) => item.warnings),
              needsReview: readings.some((item) => item.needsReview),
            };
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
        multiple
        accept={formats.join(",")}
        className="nora-sr-only"
        aria-label="Chọn ảnh thời khóa biểu"
        disabled={disabled}
        onChange={(event) => {
          const files = Array.from(event.target.files || []);
          event.target.value = "";
          image.select(files);
        }}
      />
      <button
        type="button"
        className="nora-image-button"
        aria-label="Đính kèm ảnh thời khóa biểu"
        title="Đính kèm tối đa 2 ảnh thời khóa biểu"
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
          {(value.images || [value]).map((part, index, parts) => (
            <div
              className="nora-timetable-heading"
              key={`${part.file.name}:${index}`}
            >
              <img
                src={part.preview}
                alt={
                  parts.length === 1
                    ? "Ảnh thời khóa biểu đã chọn"
                    : `Ảnh thời khóa biểu đã chọn ${index + 1}`
                }
              />
              <div>
                <strong>{part.file.name}</strong>
                <small>
                  {parts.length > 1
                    ? `Ảnh ${index + 1}/${parts.length} · `
                    : ""}
                  PNG, JPG, WebP · tối đa 5 MB/ảnh
                </small>
              </div>
              <button
                type="button"
                aria-label={
                  parts.length === 1
                    ? "Gỡ ảnh thời khóa biểu"
                    : `Gỡ ảnh thời khóa biểu: ${part.file.name}`
                }
                disabled={disabled}
                onClick={() => image.remove(index)}
              >
                {icon("close")}
              </button>
            </div>
          ))}
          {image.phase === "selected" && (
            <p>
              Bấm Gửi để NORA đọc ảnh bằng{" "}
              {provider || "dịch vụ AI đang cấu hình"} và gộp lịch bận từ tối đa
              2 ảnh để tính giờ rảnh từng ngày. Bạn có thể ghi thêm ngày áp dụng
              hoặc số tuần học bên dưới.
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
                  maxLength={
                    TIMETABLE_TEXT_MAX_LENGTH * (value.images?.length || 1)
                  }
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
