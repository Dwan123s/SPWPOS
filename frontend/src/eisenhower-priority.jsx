import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { toUtc } from "./ui.jsx";
import { eisenhowerPriority, importanceOptions } from "./eisenhower-priority.js";
import "./eisenhower-priority.css";

const listeners = new Set();
let ticker;
function subscribe(listener) {
  listeners.add(listener);
  ticker ??= setInterval(() => listeners.forEach((refresh) => refresh()), 30000);
  return () => { listeners.delete(listener); if (!listeners.size) { clearInterval(ticker); ticker = null; } };
}
const clockSnapshot = () => Math.floor(Date.now() / 30000) * 30000;

export function EisenhowerPriorityBadge({ task }) {
  const now = useSyncExternalStore(subscribe, clockSnapshot, clockSnapshot);
  const value = eisenhowerPriority(task, now);
  return <span className={`eisenhower-priority-badge priority-${value.quadrant}`} title={value.caption}>
    {value.label}
  </span>;
}

export function EisenhowerPriorityField({ task = {}, value, onChange, disabled, deadline = task?.deadline, name = "importanceLevel" }) {
  const host = useRef(null);
  const [editedDeadline, setEditedDeadline] = useState(undefined);
  const [local, setLocal] = useState(() => eisenhowerPriority(task).importance);
  const level = value ?? local;
  const due = editedDeadline === undefined ? deadline : editedDeadline;
  const priority = eisenhowerPriority({ importanceLevel: level, deadline: due });
  useEffect(() => {
    const form = host.current?.closest("form");
    if (!form) return;
    function readDeadline(event) {
      if (!["dueDate", "dueTime", "deadline", "deadlineTime"].includes(event.target.name)) return;
      const data = new FormData(form);
      const date = data.get("dueDate") || data.get("deadline");
      const time = data.get("dueTime") || data.get("deadlineTime") || "23:59";
      try { setEditedDeadline(date ? toUtc(String(date).includes("T") ? String(date) : `${date}T${time}`) : null); }
      catch { setEditedDeadline(null); }
    }
    form.addEventListener("change", readDeadline);
    return () => form.removeEventListener("change", readDeadline);
  }, []);
  return <div className="eisenhower-priority-field" ref={host}>
    <label className="field">
      <span>Mức quan trọng (Eisenhower)</span>
      <select name={name} value={level} disabled={disabled} onChange={(event) => { setLocal(event.target.value); onChange?.(event.target.value); }}>
        {importanceOptions.map(([key, label]) => <option key={key} value={key}>{label}</option>)}
      </select>
    </label>
    <p>Độ khẩn cấp tự động theo deadline: còn tối đa 24 giờ hoặc quá hạn là khẩn cấp.</p>
    <small>{priority.label} · {priority.caption}</small>
  </div>;
}
