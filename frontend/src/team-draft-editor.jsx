import { useState } from "react";
import { icon, localInput, toUtc, zone } from "./ui.jsx";
import { parseTaskNotes } from "./team-task-notes.jsx";
import "./team-draft-editor.css";

const sections = [
  ["summary", "Tóm tắt", "TÓM TẮT"],
  ["requirements", "Việc cần thực hiện", "YÊU CẦU CẦN THỰC HIỆN"],
  ["deliverables", "Đầu ra cần bàn giao", "ĐẦU RA CẦN BÀN GIAO"],
  ["criteria", "Tiêu chí hoàn thành", "TIÊU CHÍ HOÀN THÀNH"],
  ["notes", "Ghi chú & phối hợp", "GHI CHÚ & PHỐI HỢP"],
];
export const draftEditValues = (task) => ({
  id: task.id,
  name: task.name,
  description: task.description,
  estimatedMinutes: task.estimatedMinutes,
  deadline: task.deadline || null,
});

function initialValues(task) {
  const parsed = parseTaskNotes(task.description || "");
  const content = Object.fromEntries(
    sections.map(([key, title]) => [
      key,
      parsed
        .filter(
          (s) =>
            s.title === title ||
            (key === "summary" && s.title === "Nội dung công việc"),
        )
        .flatMap((s) => s.lines)
        .join("\n")
        .trim(),
    ]),
  );
  const preserved = parsed.filter(
    (s) =>
      !sections.some(([, title]) => s.title === title) &&
      s.title !== "Nội dung công việc",
  );
  return {
    ...content,
    name: task.name,
    minutes: task.estimatedMinutes,
    deadline: task.deadline ? localInput(task.deadline) : "",
    preserved,
  };
}
function descriptionOf(value) {
  return [
    ...sections
      .filter(([key]) => value[key].trim())
      .map(([key, , heading]) => `${heading}\n${value[key].trim()}`),
    ...value.preserved.map(
      (s) =>
        `${s.title.toLocaleUpperCase("vi-VN")}\n${s.lines.join("\n").trim()}`,
    ),
  ].join("\n\n");
}
export function TeamDraftOptions({ tasks, busy, onSave }) {
  const [deadline, setDeadline] = useState("");
  const [error, setError] = useState("");
  return (
    <section className="team-draft-options" aria-label="Tùy chỉnh bản nháp">
      <div>
        <h3>{icon("calendar")} Tùy chỉnh của trưởng nhóm</h3>
        <p>
          Chốt hạn chung tại đây, hoặc chỉnh thời lượng và nội dung của từng gói
          bên dưới.
        </p>
      </div>
      <form
        onSubmit={async (event) => {
          event.preventDefault();
          setError("");
          try {
            const due = toUtc(deadline);
            await onSave(
              tasks.map((task) => ({
                ...draftEditValues(task),
                deadline: due,
              })),
            );
          } catch (failure) {
            setError(failure.message);
          }
        }}
      >
        <label className="field">
          Hạn chót chung
          <input
            type="datetime-local"
            required
            value={deadline}
            onChange={(event) => setDeadline(event.target.value)}
            disabled={busy}
          />
        </label>
        <button className="btn secondary" disabled={busy || !deadline}>
          Áp dụng hạn chót chung
        </button>
      </form>
      <small>
        Múi giờ: {zone()}. Lưu bản nháp chưa giao việc hoặc đổi lịch cá nhân.
      </small>
      {error && (
        <p role="alert" className="team-error">
          {error}
        </p>
      )}
    </section>
  );
}
export function TeamDraftTaskEditor({
  task,
  busy,
  disabled,
  editing,
  onEditingChange,
  onSave,
}) {
  const [value, setValue] = useState(() => initialValues(task));
  const [error, setError] = useState("");
  const description = descriptionOf(value);
  const update = (key, next) =>
    setValue((previous) => ({ ...previous, [key]: next }));
  if (!editing)
    return (
      <button
        type="button"
        className="btn secondary small team-draft-edit-button"
        disabled={busy || disabled}
        onClick={() => onEditingChange(true)}
      >
        {icon("edit")} Chỉnh sửa gói việc
      </button>
    );
  return (
    <form
      className="team-draft-task-editor"
      aria-label={`Chỉnh sửa ${task.name}`}
      onSubmit={async (event) => {
        event.preventDefault();
        setError("");
        try {
          const result = await onSave({
            id: task.id,
            name: value.name.trim(),
            description,
            estimatedMinutes: Number(value.minutes),
            deadline: value.deadline ? toUtc(value.deadline) : null,
          });
          if (result) onEditingChange(false);
        } catch (failure) {
          setError(failure.message);
        }
      }}
    >
      <header>
        <strong>{icon("edit")} Chỉnh sửa gói việc</strong>
        <p>Thay đổi trực tiếp đề xuất của NORA trước khi giao cho nhóm.</p>
      </header>
      <label className="field">
        Tên gói việc
        <input
          autoFocus
          required
          maxLength={200}
          value={value.name}
          onChange={(event) => update("name", event.target.value)}
          disabled={busy}
        />
      </label>
      <div className="team-draft-edit-grid">
        <label className="field">
          Thời lượng (phút)
          <input
            type="number"
            min={1}
            max={43200}
            step={1}
            required
            value={value.minutes}
            onChange={(event) => update("minutes", event.target.value)}
            disabled={busy}
          />
        </label>
        <label className="field">
          Hạn chót
          <input
            type="datetime-local"
            value={value.deadline}
            onChange={(event) => update("deadline", event.target.value)}
            disabled={busy}
          />
        </label>
      </div>
      {sections.map(([key, title]) => (
        <label className="field" key={key}>
          {title}
          <textarea
            aria-label={title}
            rows={key === "summary" ? 3 : 4}
            required={key === "summary"}
            maxLength={3000}
            value={value[key]}
            onChange={(event) => update(key, event.target.value)}
            disabled={busy}
          />
        </label>
      ))}
      {value.preserved.length > 0 && (
        <p className="team-draft-source-hint">
          Tham chiếu và tài liệu nguồn được giữ kèm nội dung.
        </p>
      )}
      <small className={description.length > 4000 ? "team-error" : ""}>
        {description.length}/4000 ký tự nội dung
      </small>
      {error && (
        <p role="alert" className="team-error">
          {error}
        </p>
      )}
      <div className="team-actions end">
        <button
          type="button"
          className="btn secondary"
          disabled={busy}
          onClick={() => {
            setValue(initialValues(task));
            setError("");
            onEditingChange(false);
          }}
        >
          Hủy chỉnh sửa
        </button>
        <button
          className="btn primary"
          disabled={busy || description.length > 4000 || !description.trim()}
        >
          Lưu chỉnh sửa
        </button>
      </div>
    </form>
  );
}
