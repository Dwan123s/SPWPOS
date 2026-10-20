import { icon, state } from "./ui.jsx";
import {
  NoraReply,
  planSessionName,
  planDay,
  planHours,
} from "./nora-chat-ui.jsx";
import {
  compactScheduleText,
  savedEventForChange,
  resultDate,
} from "./nora-schedule.js";

export function NoraResultCard({
  turn,
  busy,
  onAction,
  onViewResult,
  onEditEvent,
}) {
  const sessions =
    turn.plan?.sessions?.filter((session) => !session.preserved) || [];
  const saved = turn.status === "APPLIED",
    preview = turn.status === "PREVIEW",
    undone = turn.status === "UNDONE";
  const reply = compactScheduleText(turn.reply || "");
  const explanation =
    /^(?:(?:tôi|mình)\s+)?(?:đã|vừa)\s+(?:lên lịch|xếp|thêm|lưu|cập nhật)/i.test(
      reply,
    ) && !/(?:vì |để |ưu tiên|tuy nhiên|lưu ý)/i.test(reply)
      ? ""
      : reply;
  return (
    <section
      className={`nora-result-card${undone ? " is-historical" : ""}`}
      aria-label="Kết quả yêu cầu"
    >
      <div className="nora-result-title">
        {icon(saved ? "check" : undone ? "undo" : "calendar")}
        <h3>
          {saved
            ? "Lịch đã lưu"
            : preview
              ? "Xem trước thay đổi"
              : undone
                ? "Đã hoàn tác"
                : "Thay đổi chưa được lưu"}
        </h3>
      </div>
      {undone && (
        <p className="nora-result-hint">
          Các mục dưới đây là lịch sử của thay đổi đã hoàn tác.
        </p>
      )}
      {saved && !turn.canUndo && (
        <p className="nora-result-hint">
          Kết quả của yêu cầu này. Lịch hiện tại có thể đã thay đổi.
        </p>
      )}
      {explanation && (
        <div className="nora-result-reason">
          <NoraReply text={explanation} />
        </div>
      )}
      <ul className="nora-result-items">
        {(turn.changes || []).map((change, index) => {
          const event = saved ? savedEventForChange(change) : null;
          return (
            <li key={index}>
              <div>
                <strong>{change.name}</strong>
                <span>
                  {compactScheduleText(
                    change.after || "Không còn khoảng thời gian",
                  )}
                </span>
                {change.before && (
                  <details>
                    <summary>Trước khi thay đổi</summary>
                    <p>{compactScheduleText(change.before)}</p>
                  </details>
                )}
              </div>
              {saved &&
                change.taskId &&
                state.workspace?.tasks?.some(
                  (task) => task.id === change.taskId,
                ) && (
                  <button
                    type="button"
                    className="nora-result-edit"
                    disabled={busy}
                    data-action="edit-task"
                    data-id={change.taskId}
                    aria-label={`Sửa công việc ${change.name}`}
                  >
                    {icon("edit")}Sửa
                  </button>
                )}
              {event && (
                <button
                  type="button"
                  className="nora-result-edit"
                  disabled={busy}
                  aria-label={`Sửa ${change.name}`}
                  onClick={() => onEditEvent(event)}
                >
                  {icon("edit")}Sửa
                </button>
              )}
            </li>
          );
        })}
      </ul>
      {!!sessions.length && (
        <div className="nora-result-sessions">
          <p className="nora-session-count">
            {sessions.length} khoảng làm việc ·{" "}
            {sessions.reduce(
              (sum, item) =>
                sum +
                Math.round((new Date(item.end) - new Date(item.start)) / 60000),
              0,
            )}{" "}
            phút
          </p>
          <ul>
            {sessions.map((item) => (
              <li key={item.id}>
                <strong>{planSessionName(item, turn)}</strong>
                <span>{`${planDay(item.start)} · ${planHours(item.start)}–${planHours(item.end)}${planDay(item.start) !== planDay(item.end) ? ` (${planDay(item.end)})` : ""}`}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
      <div className="nora-result-actions">
        {saved && turn.canUndo && (
          <button
            type="button"
            disabled={busy}
            onClick={() => onAction(turn, "undo")}
          >
            {icon("undo")}Hoàn tác
          </button>
        )}
        {preview && (
          <button
            type="button"
            className="is-primary"
            disabled={busy}
            onClick={() => onAction(turn, "apply")}
          >
            Lưu thay đổi
          </button>
        )}
        <button
          type="button"
          onClick={(event) => onViewResult(turn, event.currentTarget)}
        >
          {icon("panel")}Xem kết quả này
        </button>
        <a
          href="#calendar"
          onClick={() => {
            state.calendarStart = resultDate(turn);
          }}
        >
          Mở lịch {icon("arrow")}
        </a>
      </div>
      {preview && (
        <p className="nora-result-hint">
          Chưa lưu vào lịch. Bản xem trước có hiệu lực tối đa 30 phút.
        </p>
      )}
      {((turn.reply && !explanation) || turn.outcome) && (
        <details className="nora-result-explanation">
          <summary>Giải thích và chi tiết</summary>
          {!explanation && <NoraReply text={reply} />}
          {turn.outcome && (
            <NoraReply text={compactScheduleText(turn.outcome)} />
          )}
        </details>
      )}
    </section>
  );
}
