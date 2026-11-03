import { useId, useState } from "react";
import { icon } from "./ui.jsx";

export function TeamRecipientSelect({
  members,
  value,
  onChange,
  busy,
  label = "Người nhận task",
}) {
  const id = useId();
  return (
    <div className="team-recipient-field">
      <label htmlFor={id}>
        {icon("users")}
        {label}
      </label>
      <select
        id={id}
        value={value || ""}
        disabled={busy}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">Cả nhóm · Tự nhận task</option>
        {value && !members.some((member) => member.userId === value) && (
          <option value={value} disabled>
            Thành viên không còn trong nhóm · Chọn lại
          </option>
        )}
        {members.map((member) => (
          <option value={member.userId} key={member.userId}>
            {member.name}
            {member.role === "Leader" ? " · Trưởng nhóm" : ""}
          </option>
        ))}
      </select>
      <small>
        {value
          ? "Chỉ người được chọn mới có thể nhận task. NORA sẽ xếp theo lịch cá nhân của họ."
          : "Thành viên có thể tự nhận task và để NORA xếp theo lịch riêng."}
      </small>
    </div>
  );
}

export function TeamRecipientEditor({ task, members, busy, onSave }) {
  const [value, setValue] = useState(task.designatedUserId || "");
  return (
    <section
      className="team-recipient-editor"
      aria-label="Thay đổi người nhận task"
    >
      <TeamRecipientSelect
        members={members}
        value={value}
        onChange={setValue}
        busy={busy}
      />
      <button
        type="button"
        className="btn secondary"
        disabled={busy || value === (task.designatedUserId || "")}
        onClick={() => onSave(task, value || null)}
      >
        Lưu người nhận
      </button>
    </section>
  );
}
