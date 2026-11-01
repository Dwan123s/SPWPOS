import { useRef, useState } from "react";
import { downloadPrivateFile } from "./api.js";
import { icon } from "./ui.jsx";

const accept =
  ".pdf,.docx,.xlsx,.pptx,.txt,.md,.csv,.zip,.png,.jpg,.jpeg,.webp";
const sizeLabel = (size) =>
  size >= 1024 * 1024
    ? `${(size / (1024 * 1024)).toFixed(1)} MB`
    : `${Math.max(1, Math.ceil(size / 1024))} KB`;
const readFile = (file) =>
  new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () =>
      reject(new Error("Không đọc được file. Hãy chọn lại."));
    reader.onload = () =>
      resolve({
        name: file.name,
        size: file.size,
        data: String(reader.result).split(",")[1],
      });
    reader.readAsDataURL(file);
  });
export function CompletionFileUpload({
  files,
  onChange,
  busy,
  onReadingChange,
}) {
  const input = useRef(null);
  const [reading, setReading] = useState(false),
    [error, setError] = useState("");
  async function add(selected) {
    if (!selected?.length || busy || reading) return;
    setError("");
    const incoming = Array.from(selected);
    if (
      files.length + incoming.length > 3 ||
      incoming.some((file) => !file.size || file.size > 8 * 1024 * 1024) ||
      [...files, ...incoming].reduce((sum, file) => sum + file.size, 0) >
        20 * 1024 * 1024
    ) {
      setError(
        "Tối đa 3 file, mỗi file 8 MB và tổng cộng 20 MB. File phải có nội dung.",
      );
      return;
    }
    if (
      incoming.some(
        (file) =>
          !/\.(pdf|docx|xlsx|pptx|txt|md|csv|zip|png|jpe?g|webp)$/i.test(
            file.name,
          ),
      )
    ) {
      setError(
        "Hỗ trợ PDF, Word, Excel, PowerPoint, TXT, Markdown, CSV, ZIP và ảnh PNG, JPG, WebP.",
      );
      return;
    }
    setReading(true);
    onReadingChange?.(true);
    try {
      onChange([...files, ...(await Promise.all(incoming.map(readFile)))]);
    } catch (e) {
      setError(e.message);
    } finally {
      setReading(false);
      onReadingChange?.(false);
      if (input.current) input.current.value = "";
    }
  }
  return (
    <section
      className="team-completion-upload"
      aria-label="File báo cáo hoàn thành"
    >
      <div className="team-completion-upload-heading">
        <strong>{icon("folder")} File kết quả công việc</strong>
        <span>Không bắt buộc</span>
      </div>
      <p>
        Đính kèm báo cáo, sản phẩm hoặc minh chứng để trưởng nhóm kiểm tra. Chỉ
        bạn và trưởng nhóm được tải file.
      </p>
      <input
        ref={input}
        type="file"
        multiple
        accept={accept}
        aria-label="Chọn file báo cáo"
        className="sr-only"
        disabled={busy || reading}
        onChange={(e) => add(e.target.files)}
      />
      <button
        type="button"
        className="btn secondary"
        disabled={busy || reading || files.length >= 3}
        onClick={() => input.current?.click()}
      >
        {icon("plus")} {reading ? "Đang đọc file…" : "Đính kèm file"}
      </button>
      <small>Tối đa 3 file · 8 MB mỗi file · Tổng 20 MB</small>
      {files.length > 0 && (
        <ul className="team-completion-file-list">
          {files.map((file, index) => (
            <li key={`${file.name}-${index}`}>
              <span className="team-completion-file-icon">
                {icon("folder")}
              </span>
              <span>
                <strong>{file.name}</strong>
                <small>{sizeLabel(file.size)}</small>
              </span>
              <button
                type="button"
                className="team-icon-button"
                aria-label={`Bỏ file ${file.name}`}
                disabled={busy || reading}
                onClick={() => {
                  onChange(files.filter((_, i) => i !== index));
                  setError("");
                }}
              >
                {icon("close")}
              </button>
            </li>
          ))}
        </ul>
      )}
      {error && (
        <p className="team-error" role="alert">
          {error}
        </p>
      )}
    </section>
  );
}
export function CompletionReport({ task }) {
  const [downloading, setDownloading] = useState(null),
    [error, setError] = useState("");
  if (!task.completionSubmissionNote && !task.completionFiles?.length)
    return null;
  return (
    <section
      className="team-completion-report"
      aria-label="Báo cáo thành viên đã gửi"
    >
      <strong>{icon("folder")} Kết quả đã gửi</strong>
      {task.completionSubmissionNote && <p>{task.completionSubmissionNote}</p>}
      {!!task.completionFiles?.length && (
        <ul className="team-completion-file-list">
          {task.completionFiles.map((file) => (
            <li key={file.id}>
              <span className="team-completion-file-icon">
                {icon("folder")}
              </span>
              <span>
                <strong>{file.name}</strong>
                <small>{sizeLabel(file.size)}</small>
              </span>
              <button
                type="button"
                className="team-text-button"
                disabled={!!downloading}
                onClick={async () => {
                  setError("");
                  setDownloading(file.id);
                  try {
                    await downloadPrivateFile(
                      `/api/teams/tasks/${task.id}/completion/files/${file.id}`,
                      file.name,
                    );
                  } catch (e) {
                    setError(e.message);
                  } finally {
                    setDownloading(null);
                  }
                }}
              >
                {downloading === file.id ? "Đang tải…" : "Tải file"}
              </button>
            </li>
          ))}
        </ul>
      )}
      {error && (
        <p className="team-error" role="alert">
          {error}
        </p>
      )}
    </section>
  );
}
