import { useState } from "react";
import { api } from "./api.js";
import { dayKey, icon } from "./ui.jsx";
import { readTeamBriefFile, TEAM_BRIEF_ACCEPT } from "./team-brief-file.js";
import { Checklist } from "./eisenhower-board.jsx";

const deadlineDate = (deadline) => deadline?.includes("T") ? dayKey(deadline) : deadline || "";

export function TaskBatchForm() {
  const [text, setText] = useState("");
  const [file, setFile] = useState(null);
  const [draft, setDraft] = useState(null);
  const [requestId, setRequestId] = useState("");
  const [sequential, setSequential] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function analyze() {
    if (busy) return;
    setError("");
    setBusy(true);
    try {
      const source = file ? await readTeamBriefFile(file, { allowCsv: true }) : null;
      const preview = await api("/api/tasks/batch/preview", "POST", {
        text: text.trim(),
        file: source,
      });
      setDraft({
        ...preview,
        tasks: preview.tasks.map((task) => ({ ...task, key: crypto.randomUUID() })),
      });
      setRequestId(crypto.randomUUID());
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  function editTask(key, changes) {
    setDraft((current) => ({
      ...current,
      tasks: current.tasks.map((task) => task.key === key ? { ...task, ...changes } : task),
    }));
  }

  function moveTask(index, offset) {
    setDraft((current) => {
      const tasks = current.tasks.slice();
      const [task] = tasks.splice(index, 1);
      tasks.splice(index + offset, 0, task);
      return { ...current, tasks };
    });
  }

  const tasks = draft?.tasks ?? [];
  return (
    <form className="task-batch-form" data-form="task-batch">
      <div className="task-create-content">
        <fieldset className="task-batch-source" disabled={busy}>
          <label className="field">
            <span>Danh sách việc</span>
            <textarea
              rows={7}
              maxLength={12000}
              placeholder={"Mỗi việc một dòng, hoặc dán yêu cầu của project…"}
              value={text}
              onChange={(event) => {
                setText(event.target.value);
                setDraft(null);
                setError("");
              }}
            />
          </label>
          <div className="task-batch-upload">
            <label className="field">
              <span>Tải tài liệu</span>
              <input type="file" name="source" accept={`${TEAM_BRIEF_ACCEPT},.csv`} onChange={(event) => {
                setFile(event.target.files[0] ?? null);
                setDraft(null);
                setError("");
              }} />
              <small>DOCX, PDF, TXT, Markdown hoặc CSV · tối đa 8 MB</small>
            </label>
            <button type="button" className="btn primary" disabled={!file && !text.trim()} onClick={analyze}>
              {icon("spark")} Phân tích bằng NORA
            </button>
          </div>
        </fieldset>
        {busy && <p role="status">NORA đang đọc và sắp xếp công việc…</p>}
        {error && <p className="warning" role="alert">{error}</p>}
        {draft && (
          <section className="task-batch-review" aria-label="Công việc NORA đề xuất">
            <h3>Công việc NORA đề xuất</h3>
            {draft.summary && <p>{draft.summary}</p>}
            {draft.warnings?.map((message) => <p key={message}>{message}</p>)}
            {!tasks.length && <p>Chưa có công việc để lưu.</p>}
            {!!tasks.length && (
              <>
                <div className="form-grid">
                  <label className="field">
                    Tên project
                    <input required maxLength={200} value={draft.projectName}
                      onChange={(event) => setDraft({ ...draft, projectName: event.target.value })} />
                  </label>
                  <label className="field">
                    Hạn chót project
                    <input type="date" min={dayKey(Date.now())} value={deadlineDate(draft.projectDeadline)}
                      onChange={(event) => setDraft({ ...draft, projectDeadline: event.target.value || null })} />
                  </label>
                </div>
                <p>Việc con dùng hạn chót của project nếu để trống hạn riêng.</p>
                <ol className="task-batch-list">
                  {tasks.map((task, index) => (
                    <li key={task.key}>
                      <div className="task-batch-item-title">
                        <label className="field">
                          Việc {index + 1}
                          <input required maxLength={200} value={task.name}
                            onChange={(event) => editTask(task.key, { name: event.target.value })} />
                        </label>
                        <div className="actions">
                          <button type="button" className="btn ghost small" disabled={index === 0}
                            aria-label={`Đưa việc ${index + 1} lên`} onClick={() => moveTask(index, -1)}>↑</button>
                          <button type="button" className="btn ghost small" disabled={index === tasks.length - 1}
                            aria-label={`Đưa việc ${index + 1} xuống`} onClick={() => moveTask(index, 1)}>↓</button>
                          <button type="button" className="btn ghost small" aria-label={`Bỏ việc ${index + 1}`}
                            onClick={() => setDraft({ ...draft, tasks: tasks.filter((item) => item.key !== task.key) })}>{icon("close")}</button>
                        </div>
                      </div>
                      <div className="form-grid">
                        <label className="field">
                          Thời lượng (phút)
                          <input type="number" min={1} max={43200} value={task.estimatedMinutes ?? ""}
                            onChange={(event) => editTask(task.key, { estimatedMinutes: event.target.valueAsNumber || null })} />
                        </label>
                        <label className="field">
                          Hạn riêng (tùy chọn)
                          <input type="date" min={dayKey(Date.now())} max={deadlineDate(draft.projectDeadline) || undefined}
                            value={deadlineDate(task.deadline)}
                            onChange={(event) => editTask(task.key, { deadline: event.target.value || null })} />
                        </label>
                      </div>
                      <details open>
                        <summary>Yêu cầu · {task.checklist?.length || 0} bước</summary>
                        <Checklist items={task.checklist || []} showCompletion={false} disabled={busy}
                          onChange={(checklist) => editTask(task.key, { checklist })} />
                      </details>
                      {task.description && <details><summary>Đầu ra và tài liệu tham chiếu</summary><p>{task.description}</p></details>}
                    </li>
                  ))}
                </ol>
                <label className="checkbox">
                  <input type="checkbox" checked={sequential} onChange={(event) => setSequential(event.target.checked)} />
                  Xếp tuần tự theo danh sách
                </label>
              </>
            )}
          </section>
        )}
      </div>
      <div className="form-error" role="alert" />
      {!!tasks.length && (
        <div className="task-batch-footer">
          <input type="hidden" name="batch" value={JSON.stringify({
            requestId, projectName: draft.projectName, projectDeadline: draft.projectDeadline || null, sequential,
            tasks: tasks.map(({ key, ...task }) => ({
              ...task, checklist: task.checklist?.filter((item) => item.text.trim()) || [],
            })),
          })} />
          <button type="submit" className="btn secondary" disabled={busy}>Lưu project</button>
          <button type="submit" className="btn primary" data-next-action="preview" disabled={busy}>Lưu và xem lịch đề xuất</button>
        </div>
      )}
    </form>
  );
}
