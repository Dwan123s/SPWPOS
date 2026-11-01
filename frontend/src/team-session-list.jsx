import { icon, dateFmt, timeFmt } from "./ui.jsx";
import "./team-workspace.css";
export function SessionList({ items = [], showStatus = true }) {
  return (
    <ul className="team-session-list">
      {items.map((s) => (
        <li className={!s.preserved && showStatus ? "changed" : ""} key={s.id}>
          <span className="team-session-icon">{icon("clock")}</span>
          <div>
            <strong>{s.taskName}</strong>
            <p>
              {dateFmt(s.start)} · {timeFmt(s.start)}–{timeFmt(s.end)}
            </p>
          </div>
          {showStatus && (
            <small>{s.preserved ? "Giữ nguyên" : "Phiên mới"}</small>
          )}
        </li>
      ))}
    </ul>
  );
}
