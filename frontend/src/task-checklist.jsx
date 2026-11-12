import { useEffect, useId, useLayoutEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { Checklist } from "./eisenhower-board.jsx";
import { TeamChecklist } from "./team-task-checklist.jsx";
import { state, toast } from "./ui.jsx";

function checklistOf(task) {
  return (task.isTeamTask ? task.teamChecklist : task.checklist) ?? [];
}

function itemsToSave(draft, saved) {
  const previousText = new Map(saved.map((item) => [item.id, item.text]));
  return draft.map((item) => ({ ...item, text: item.text.trim() || previousText.get(item.id) }))
    .filter((item) => item.text);
}

function sameItems(left, right) {
  return left.length === right.length && left.every(({ id, text, completed }, index) =>
    id === right[index].id && text === right[index].text && completed === right[index].completed,
  );
}

const allChecked = (items) => items.length > 0 && items.every((item) => item.completed);

function cacheChecklist(userId, taskId, previousVersion, result, previousRevision) {
  const workspace = state.workspace;
  if (!workspace || workspace.profile?.id !== userId) return;
  const index = workspace.tasks.findIndex((entry) => entry.id === taskId);
  const task = workspace.tasks[index];
  if (!task || task.version !== previousVersion) return;
  workspace.tasks = workspace.tasks.map((entry, i) => i === index ? {
    ...task, version: result.version, checklist: result.checklist, teamChecklist: result.teamChecklist,
  } : entry);
  if (result.revision && workspace.preference && workspace.preference.revision === previousRevision)
    workspace.preference.revision = result.revision;
  window.dispatchEvent(new Event("workspace-updated"));
}

export function TaskChecklist({ task }) {
  if (task.isTeamTask && (task.teamStatus !== "Assigned" || !task.version)) {
    return <TeamChecklist items={task.teamChecklist} />;
  }
  return <TaskChecklistEditor key={task.id} task={task} />;
}

function TaskChecklistEditor({ task }) {
  const userId = state.workspace?.profile?.id;
  const path = `/api/tasks/${task.id}`;
  const [items, setItems] = useState(() => checklistOf(task));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [confirmCompletion, setConfirmCompletion] = useState(false);
  const draft = useRef(items);
  const saved = useRef({ version: task.version, items });
  const timer = useRef(null);
  const pending = useRef(false);
  const mounted = useRef(false);
  const editor = useRef(null);
  const completionRequested = useRef(false);

  useEffect(() => {
    mounted.current = true;
    function sync() {
      if (state.workspace?.profile?.id !== userId) return;
      const current = state.workspace?.tasks.find((entry) => entry.id === task.id);
      if (!current || ["Completed", "Cancelled"].includes(current.status) || !allChecked(checklistOf(current))) {
        setConfirmCompletion(false);
      }
      if (!current || pending.current || timer.current || saved.current.version === current.version ||
          !sameItems(itemsToSave(draft.current, saved.current.items), saved.current.items)) return;
      draft.current = checklistOf(current);
      saved.current = { version: current.version, items: draft.current };
      setItems(draft.current);
    }
    sync();
    window.addEventListener("workspace-updated", sync);
    return () => {
      mounted.current = false;
      window.removeEventListener("workspace-updated", sync);
    };
  }, [task.id, userId]);

  async function saveLatest() {
    clearTimeout(timer.current);
    timer.current = null;
    if (pending.current || state.workspace?.profile?.id !== userId) return;
    const submittedDraft = draft.current;
    const items = itemsToSave(submittedDraft, saved.current.items);
    if (sameItems(items, saved.current.items)) {
      if (mounted.current) {
        setSaving(false);
        if (completionRequested.current && allChecked(items)) {
          const current = state.workspace.tasks.find((entry) => entry.id === task.id);
          const topDialog = [...document.querySelectorAll("dialog[open]")].at(-1);
          if (current && !current.isTeamTask && !["Completed", "Cancelled"].includes(current.status) && allChecked(checklistOf(current)) &&
              !editor.current?.closest("[hidden]") &&
              (!topDialog || topDialog.contains(editor.current))) setConfirmCompletion(true);
        }
      }
      completionRequested.current = false;
      return;
    }
    const version = saved.current.version;
    const revision = state.workspace.preference?.revision;
    pending.current = true;
    try {
      const result = await api(`${path}/checklist`, "PUT", { version, items });
      saved.current = { version: result.version, items: checklistOf({ ...task, ...result }) };
      if (draft.current === submittedDraft && !sameItems(items, saved.current.items)) {
        draft.current = saved.current.items;
        if (mounted.current) setItems(draft.current);
      }
      cacheChecklist(userId, task.id, version, result, revision);
    } catch (failure) {
      clearTimeout(timer.current);
      timer.current = null;
      if (mounted.current) {
        setError(failure);
        setSaving(false);
      } else if (state.workspace?.profile?.id === userId) {
        toast(`Chưa lưu được checklist của “${task.name}”. ${failure.message}`, "error");
      }
      return;
    } finally {
      pending.current = false;
    }
    if (!timer.current) void saveLatest();
  }

  function change(next, editingText = false) {
    const nextItems = itemsToSave(next, saved.current.items);
    if (!allChecked(nextItems)) completionRequested.current = false;
    else if (!task.isTeamTask && !editingText && nextItems.some((item) =>
      draft.current.some((previous) => previous.id === item.id && !previous.completed),
    )) completionRequested.current = true;
    draft.current = next;
    setItems(next);
    setError(null);
    setSaving(true);
    if (!editingText) {
      void saveLatest();
      return;
    }
    clearTimeout(timer.current);
    timer.current = setTimeout(saveLatest, 450);
  }

  async function reload() {
    const version = state.workspace?.tasks.find((entry) => entry.id === task.id)?.version;
    pending.current = true;
    setSaving(true);
    try {
      const current = await api(path);
      completionRequested.current = false;
      saved.current = { version: current.version, items: checklistOf(current) };
      draft.current = saved.current.items;
      cacheChecklist(userId, task.id, version, current);
      setItems(draft.current);
      setError(null);
    } catch (failure) {
      setError({ code: "TASK_STALE", message: failure.message });
    } finally {
      pending.current = false;
      setSaving(false);
    }
  }

  const stale = error?.code === "TASK_STALE";
  return (
    <div className="task-detail-checklist" ref={editor}>
      {task.isTeamTask ? <TeamChecklist items={items} busy={stale} onToggle={(id, completed) =>
        change(draft.current.map((item) => item.id === id ? { ...item, completed } : item))} /> : (
        <Checklist items={items} onChange={change} onTextChange={(next) => change(next, true)} disabled={stale} />
      )}
      {saving && <p className="reason" role="status">Đang lưu checklist…</p>}
      {error && <div className="warning" role="alert">
        <p>{error.message}</p>
        <button type="button" className="btn small secondary" disabled={saving} onClick={stale ? reload : () => change(draft.current)}>
          {stale ? "Tải checklist mới" : "Thử lưu lại"}
        </button>
      </div>}
      {confirmCompletion && <ChecklistCompletionDialog task={task} onDismiss={() => setConfirmCompletion(false)} />}
    </div>
  );
}

function ChecklistCompletionDialog({ task, onDismiss }) {
  const dialog = useRef(null);
  const cancel = useRef(null);
  const title = useId();
  useLayoutEffect(() => {
    const node = dialog.current;
    const opener = document.activeElement;
    node.showModal();
    cancel.current.focus();
    return () => {
      if (node.open) node.close();
      if (opener?.isConnected) opener.focus({ preventScroll: true });
    };
  }, []);

  function dismiss() {
    if (!dialog.current.querySelector('button[type="submit"]').disabled) onDismiss();
  }

  return (
    <dialog ref={dialog} className="checklist-completion-dialog" aria-labelledby={title}
      aria-describedby={`${title}-description`} onClose={onDismiss}
      onCancel={(event) => {
        event.preventDefault();
        event.stopPropagation();
        dismiss();
      }}>
      <h2 id={title}>Bạn đã hoàn thành “{task.name}” chưa?</h2>
      <p id={`${title}-description`}>Tất cả mục trong checklist đã được đánh dấu.</p>
      <form data-form="complete-checklist-task" data-id={task.id}>
        <div className="form-error" role="alert" />
        <div className="form-actions">
          <button ref={cancel} type="button" className="btn secondary" onClick={dismiss}>Chưa hoàn thành</button>
          <button type="submit" className="btn primary">Đã hoàn thành</button>
        </div>
      </form>
    </dialog>
  );
}
