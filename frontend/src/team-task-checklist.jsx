import { TeamDialog } from "./team-dialog.jsx";
import { TeamRecipientSelect } from "./team-recipient-select.jsx";
import { useState } from "react";
import { minutes } from "./ui.jsx";
import "./team-task-checklist.css";

export function TeamChecklist({ items = [], onToggle, busy }) {
  if (!items.length) return null;
  return (
    <section className="team-checklist" aria-label="Checklist công việc">
      <h3>Checklist công việc <span>{items.filter((item) => item.completed).length}/{items.length}</span></h3>
      <ul>
        {items.map((item) => (
          <li key={item.id}>
            <label>
              <input type="checkbox" checked={item.completed} disabled={busy || !onToggle}
                onChange={(event) => onToggle(item.id, event.target.checked)} />
              <span>{item.text}</span>
            </label>
          </li>
        ))}
      </ul>
    </section>
  );
}

export function TeamTaskSplitDialog({ task, members, busy, error, onClose, onSubmit }) {
  const [recipients, setRecipients] = useState([task.designatedUserId || "", task.designatedUserId || ""]);
  const knownRequirements = task.checklist?.map((item) => item.text) ?? [];
  const middle = Math.ceil(knownRequirements.length / 2);
  const minutesPerPart = Math.ceil(task.estimatedMinutes / 2);
  return (
    <TeamDialog title="Chia thành 2 việc con" subtitle={task.name} busy={busy} onClose={onClose} wide>
      <form className="team-form team-task-split-form" onSubmit={(event) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        onSubmit(recipients.map((userId, index) => ({
          name: data.get(`name-${index}`),
          estimatedMinutes: Number(data.get(`duration-${index}`)),
          requirements: data.get(`requirements-${index}`).split(/\r?\n/).map((line) => line.trim()).filter(Boolean),
          notes: data.get(`notes-${index}`),
          designatedUserId: userId || null,
        })));
      }}>
        <p>Hai việc con dùng chung hạn chót của việc gốc. Mỗi người nhận sẽ xem và xác nhận lịch riêng.</p>
        <div className="team-task-split-parts">
          {recipients.map((recipient, index) => (
            <fieldset key={index}>
              <legend>Việc con {index + 1}</legend>
              <label>Tên công việc <input name={`name-${index}`} required maxLength={200} /></label>
              <label>Thời lượng (phút) <input name={`duration-${index}`} type="number" required min={1} max={43200}
                defaultValue={Math.max(1, index === 0 ? minutesPerPart : task.estimatedMinutes - minutesPerPart)} /></label>
              <label>Yêu cầu, mỗi dòng một mục
                <textarea name={`requirements-${index}`} rows={4} maxLength={4000}
                  defaultValue={(index === 0 ? knownRequirements.slice(0, middle) : knownRequirements.slice(middle)).join("\n")} />
              </label>
              <label>Ghi chú <textarea name={`notes-${index}`} rows={2} maxLength={4000} /></label>
              <TeamRecipientSelect label={`Người nhận việc con ${index + 1}`} members={members}
                value={recipient} busy={busy} onChange={(userId) => setRecipients((previous) =>
                  previous.map((value, itemIndex) => itemIndex === index ? userId : value))} />
            </fieldset>
          ))}
        </div>
        {error && <p className="team-error" role="alert">{error}</p>}
        <div className="team-actions">
          <button type="button" className="btn secondary" disabled={busy} onClick={onClose}>Hủy</button>
          <button type="submit" className="btn primary" disabled={busy}>Tạo 2 việc con</button>
        </div>
      </form>
    </TeamDialog>
  );
}
