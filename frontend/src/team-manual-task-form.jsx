import { useRef, useState } from "react";
import { icon, localInput, toUtc, zone } from "./ui.jsx";
import { manualTaskContent, manualTaskDescription } from "./team-manual-task-content.js";
import { EisenhowerPriorityField } from "./eisenhower-priority.jsx";

const durations = [[30, "30 phút"], [60, "1 giờ"], [120, "2 giờ"]];

export function TeamManualTaskForm({ task, busy, hidden, onEdit, onPreview }) {
  const [content] = useState(() => manualTaskContent(task));
  const [items, setItems] = useState(() => content.checklist.map((text) => ({ id: crypto.randomUUID(), text })));
  const [hasChecklist, setHasChecklist] = useState(content.hasChecklist);
  const newItem = useRef(null);
  const [duration, setDuration] = useState(task?.estimatedMinutes ?? "");
  const [error, setError] = useState("");
  const initialDeadline = task?.deadline ? localInput(task.deadline) : "";
  const [initialDate, initialTime] = initialDeadline.split("T");

  function edited() {
    setError("");
    onEdit();
  }

  function changeChecklist(next) {
    setItems(next);
    setHasChecklist(true);
    edited();
  }

  function addItem(index = items.length) {
    if (busy || items.length >= 100) return;
    const item = { id: crypto.randomUUID(), text: "" };
    newItem.current = item.id;
    changeChecklist([...items.slice(0, index), item, ...items.slice(index)]);
  }

  function submit(event) {
    event.preventDefault();
    if (busy) return;
    const data = new FormData(event.currentTarget);
    const name = String(data.get("name")).trim();
    if (!name) {
      setError("Hãy nhập tên công việc.");
      return;
    }
    const description = manualTaskDescription(String(data.get("description")), items, hasChecklist);
    if (description.length > 4000) {
      setError("Mô tả và checklist quá dài. Hãy rút gọn để tổng nội dung không vượt quá 4.000 ký tự.");
      return;
    }
    const dateTime = `${data.get("dueDate")}T${data.get("dueTime")}`;
    let deadline;
    try {
      deadline = task?.deadline && dateTime === initialDeadline ? task.deadline : toUtc(dateTime);
    } catch (failure) {
      setError(failure.message);
      return;
    }
    setError("");
    onPreview({
      name,
      description,
      estimatedMinutes: Number(data.get("estimatedMinutes")),
      deadline,
      priority: task?.priority ?? 2,
      importanceLevel: String(data.get("importanceLevel") || "Unknown"),
      splittable: data.has("splittable"),
    });
  }

  return (
    <form className="team-manual-form" aria-label="Nhập công việc thủ công" hidden={hidden} onSubmit={submit} onChange={edited}>
      <fieldset disabled={busy}>
        <label>
          <span>Tên công việc</span>
          <input required name="name" maxLength={200} defaultValue={task?.name} placeholder="Ví dụ: Viết báo cáo kiểm thử" />
        </label>
        <div className="team-manual-schedule">
          <div className="team-manual-duration">
            <label>
              <span>Thời lượng (phút)</span>
              <input required name="estimatedMinutes" type="number" min="1" max="43200" inputMode="numeric"
                value={duration} onChange={(event) => setDuration(event.target.value)} placeholder="Nhập số phút" />
            </label>
            <div className="team-manual-presets" role="group" aria-label="Chọn nhanh thời lượng">
              {durations.map(([value, label]) => (
                <button key={value} type="button" aria-pressed={Number(duration) === value} onClick={() => {
                  setDuration(value);
                  edited();
                }}>
                  {label}
                </button>
              ))}
            </div>
          </div>
          <fieldset className="team-manual-deadline">
            <legend>Hạn chót</legend>
            <label>
              <span>Ngày</span>
              <input required name="dueDate" type="date" defaultValue={initialDate} />
            </label>
            <label>
              <span>Giờ</span>
              <input required name="dueTime" type="time" defaultValue={initialTime} />
            </label>
            <small>Múi giờ {zone()}</small>
          </fieldset>
        </div>
        <label>
          <span>Mô tả <small>(tùy chọn)</small></span>
          <textarea name="description" rows={3} maxLength={4000} defaultValue={content.description} placeholder="Yêu cầu hoặc kết quả cần bàn giao" />
        </label>
        <section className="team-manual-checklist" aria-labelledby="team-manual-checklist-title">
          <div className="team-manual-checklist-heading">
            <h3 id="team-manual-checklist-title">Checklist <small>(tùy chọn)</small></h3>
            {items.length > 0 && <small>{items.filter((item) => item.text.trim()).length} mục</small>}
          </div>
          <p>Chia công việc thành các bước nhỏ để người nhận theo dõi và đánh dấu hoàn thành.</p>
          {items.length > 0 && <ol>
            {items.map((item, index) => <li key={item.id}>
              <span className="team-manual-checklist-box" aria-hidden="true" />
              <input aria-label={`Nội dung checklist ${index + 1}`} maxLength={300}
                placeholder={`Việc cần làm ${index + 1}`} value={item.text}
                ref={(input) => {
                  if (input && newItem.current === item.id) { input.focus(); newItem.current = null; }
                }}
                onChange={(event) => changeChecklist(items.map((row) => row.id === item.id ? { ...row, text: event.target.value } : row))}
                onKeyDown={(event) => {
                  if (event.key === "Enter" && !event.nativeEvent.isComposing) { event.preventDefault(); addItem(index + 1); }
                }} />
              <button type="button" className="team-icon-button" aria-label={`Xóa mục checklist ${index + 1}`}
                onClick={() => changeChecklist(items.filter((row) => row.id !== item.id))}>{icon("close")}</button>
            </li>)}
          </ol>}
          <button type="button" className="team-manual-checklist-add" disabled={items.length >= 100} onClick={() => addItem()}>
            {icon("plus")} Thêm mục checklist
          </button>
        </section>
        <div className="team-manual-options">
          <EisenhowerPriorityField task={task} />
          <label className="team-manual-split">
            <input name="splittable" type="checkbox" defaultChecked={task?.splittable ?? true} />
            <span>Cho phép chia thành nhiều phiên</span>
          </label>
        </div>
        {error && <p className="team-error" role="alert">{error}</p>}
        <div className="team-actions end">
          <button type="submit" className="btn primary">
            {busy ? "Đang chuẩn bị…" : task ? "Xem trước thay đổi" : "Xem bản nháp"}
          </button>
        </div>
      </fieldset>
    </form>
  );
}
