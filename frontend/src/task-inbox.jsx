import { useRef, useState } from "react";
import { api } from "./api.js";
import { dateFmt, icon } from "./ui.jsx";
import { openInboxNote } from "./task-inbox-link.js";
import { ConversationDeleteDialog } from "./conversation-delete-dialog.jsx";

export function TaskInbox({
  notes,
  loading,
  error,
  onRefresh,
  onSaved,
  onDeleted,
  query,
}) {
  const [content, setContent] = useState("");
  const [busy, setBusy] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [editing, setEditing] = useState(null);
  const [editText, setEditText] = useState("");
  const [deleting, setDeleting] = useState(null);
  const lock = useRef(false);
  async function save(event, note = null) {
    event.preventDefault();
    const text = (note ? editText : content).trim();
    if (!text || lock.current) return;
    lock.current = true;
    setBusy(true);
    setSaveError("");
    try {
      const saved = await api(
        `/api/notes${note ? `/${note.id}` : ""}`,
        note ? "PUT" : "POST",
        {
          title: note?.title || text.split("\n")[0].slice(0, 120),
          content: text,
          ...(note ? { version: note.version } : {}),
        },
      );
      onSaved(saved);
      if (note) setEditing(null);
      else setContent("");
    } catch (failure) {
      setSaveError(failure.message);
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  const term = query.trim().toLocaleLowerCase("vi");
  const visible = notes.filter((note) =>
    `${note.title}\n${note.content}`.toLocaleLowerCase("vi").includes(term),
  );
  const pending = visible.filter((note) => !note.inboxCompleted);
  const completed = visible.filter((note) => note.inboxCompleted);
  async function toggleCompletion(note, checked) {
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    setSaveError("");
    try {
      const saved = await api(`/api/notes/${note.id}/completion`, "PATCH", {
        version: note.version,
        completed: checked,
      });
      onSaved(saved);
    } catch (failure) {
      setSaveError(failure.message);
      if (failure.code === "NOTE_STALE") onRefresh();
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  const renderNote = (note) => (
    <li key={note.id} className={note.inboxCompleted ? "is-completed" : ""}>
      <input
        type="checkbox"
        className="task-inbox-check"
        checked={!!note.inboxCompleted}
        disabled={busy || editing?.id === note.id}
        aria-label={`${note.inboxCompleted ? "Mở lại" : "Đánh dấu hoàn thành"}: ${note.title}`}
        onChange={(event) => toggleCompletion(note, event.target.checked)}
      />
      <div className="task-inbox-body">
        {editing?.id === note.id ? (
          <form onSubmit={(event) => save(event, editing)}>
            <label className="sr-only" htmlFor={`inbox-edit-${note.id}`}>
              Sửa nội dung Inbox
            </label>
            <textarea
              id={`inbox-edit-${note.id}`}
              value={editText}
              maxLength={8000}
              onChange={(event) => setEditText(event.target.value)}
              disabled={busy}
              rows={3}
            />
            <div className="actions">
              <button
                type="submit"
                className="btn primary small"
                disabled={busy || !editText.trim()}
              >
                Lưu chỉnh sửa
              </button>
              <button
                type="button"
                className="btn ghost small"
                disabled={busy}
                onClick={() => setEditing(null)}
              >
                Hủy
              </button>
            </div>
          </form>
        ) : (
          <>
            <button
              type="button"
              className="task-inbox-title"
              onClick={() => openInboxNote(note.id)}
            >
              {note.title}
            </button>
            {note.content !== note.title && <p>{note.content}</p>}
            <small>
              {note.inboxCompleted ? "Đã hoàn thành" : "Chưa hoàn thành"} ·{" "}
              {dateFmt(
                note.inboxCompletedAt || note.updatedAt || note.createdAt,
              )}
            </small>
            <div className="task-inbox-actions">
              <button type="button" onClick={() => openInboxNote(note.id)}>
                {icon("arrow")} Hoàn thiện trong Nhập việc
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => {
                  setEditing(note);
                  setEditText(note.content);
                  setSaveError("");
                }}
              >
                Chỉnh sửa
              </button>
              <button
                type="button"
                className="task-inbox-delete"
                disabled={busy}
                onClick={() => {
                  setDeleting(note);
                  setSaveError("");
                }}
                aria-label={`Xóa khỏi Inbox: ${note.title}`}
              >
                {icon("trash")}
              </button>
            </div>
          </>
        )}
      </div>
    </li>
  );
  return (
    <div className="task-inbox-content">
      <form className="task-inbox-capture" onSubmit={save}>
        <label className="sr-only" htmlFor="inbox-capture">
          Ý tưởng hoặc nhiệm vụ mới
        </label>
        <textarea
          id="inbox-capture"
          value={content}
          maxLength={8000}
          rows={2}
          disabled={busy}
          placeholder="Ghi nhanh một ý tưởng hoặc việc cần nhớ…"
          onChange={(event) => setContent(event.target.value)}
          onKeyDown={(event) => {
            if (
              event.key === "Enter" &&
              !event.shiftKey &&
              !event.nativeEvent.isComposing &&
              event.nativeEvent.keyCode !== 229
            )
              save(event);
          }}
        />
        <div>
          <span>
            Enter để lưu · Shift + Enter để xuống dòng
          </span>
          <button
            type="submit"
            className="btn primary small"
            disabled={busy || !content.trim()}
          >
            {icon("plus")} Lưu vào Inbox
          </button>
        </div>
      </form>
      {saveError && (
        <p className="form-error" role="alert">
          {saveError}
        </p>
      )}
      {error && (
        <div className="task-smart-error" role="alert">
          <p>{error}</p>
          <button
            type="button"
            className="btn secondary small"
            onClick={onRefresh}
          >
            Tải lại Inbox
          </button>
        </div>
      )}
      {loading ? (
        <p role="status" className="task-smart-empty">
          Đang tải Inbox…
        </p>
      ) : visible.length ? (
        <>
          <p className="task-inbox-progress" role="status">
            {pending.length} mục cần làm · {completed.length} đã hoàn thành
          </p>
          {pending.length > 0 ? (
            <ul className="task-inbox-items" aria-label="Chưa hoàn thành">
              {pending.map(renderNote)}
            </ul>
          ) : (
            <p className="task-inbox-all-done">
              {icon("check")} Đã hoàn thành tất cả mục trong danh sách này.
            </p>
          )}
          {completed.length > 0 && (
            <details className="task-inbox-completed" open>
              <summary>
                Đã hoàn thành <span>{completed.length}</span>
              </summary>
              <ul className="task-inbox-items" aria-label="Đã hoàn thành">
                {completed.map(renderNote)}
              </ul>
            </details>
          )}
        </>
      ) : (
        !error && (
          <div className="task-smart-empty">
            {icon("mail")}
            <h3>
              {term
                ? "Không có ý tưởng phù hợp"
                : "Thêm việc cần làm, tick khi hoàn thành"}
            </h3>
            <p>
              {term
                ? "Thử một từ khóa khác."
                : "Ghi nhanh một việc cần nhớ, rồi tick khi làm xong. Bạn vẫn có thể mở Nhập việc để lên kế hoạch chi tiết."}
            </p>
          </div>
        )
      )}
      {deleting && (
        <ConversationDeleteDialog
          title={deleting.title}
          headingText="Xóa mục khỏi Inbox?"
          containerSelector=".task-smart-main"
          busy={busy}
          error={saveError}
          descriptionContent={
            <>
              Bản nháp <strong>{deleting.title}</strong> sẽ bị xóa khỏi Inbox và
              thư viện Nhập việc.
            </>
          }
          onCancel={() => {
            if (!busy) setDeleting(null);
          }}
          onConfirm={async () => {
            if (lock.current) return;
            lock.current = true;
            setBusy(true);
            setSaveError("");
            try {
              await api(
                `/api/notes/${deleting.id}?version=${encodeURIComponent(deleting.version)}`,
                "DELETE",
              );
              onDeleted(deleting.id);
              setDeleting(null);
            } catch (failure) {
              setSaveError(failure.message);
            } finally {
              lock.current = false;
              setBusy(false);
            }
          }}
        />
      )}
    </div>
  );
}
