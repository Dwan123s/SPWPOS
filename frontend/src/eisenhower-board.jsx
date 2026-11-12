import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { api } from "./api.js";
import {
  state,
  icon,
  dateFmt,
  timeFmt,
  localInput,
  toUtc,
  dayKey,
} from "./ui.jsx";
import { askNora } from "./agent-handoff.js";
import { moveChecklist, useEisenhowerDrag } from "./eisenhower-drag.js";
import "./eisenhower-board.css";
import { EisenhowerQuickCreate } from "./eisenhower-quick-create.jsx";
import "./eisenhower-quick-create.css";
import { openInboxNote } from "./task-inbox-link.js";

const canManage = (t) =>
  ["Personal", "Accepted", "Draft"].includes(t.kind) && !!t.version;
const recommendation = (t) =>
  t.recommendation ||
  (["Q1", "Q2"].includes(t.quadrant)
    ? "SuggestSchedule"
    : t.quadrant === "Q4"
      ? "Backlog"
      : "OnDemand");
const advice = {
  Q1: "Ưu tiên xử lý, đề xuất lịch sớm và theo dõi deadline.",
  Q2: "Chủ động phân bổ thời gian, tránh dồn việc sát hạn.",
  Q3: "Xử lý nhanh, gom việc hoặc trao đổi phân công. Xếp lịch khi cần.",
  Q4: "Giữ trong backlog. Chỉ đề xuất lịch khi bạn yêu cầu.",
};
function Dialog({ title, children, onClose, busy }) {
  const ref = useRef(null);
  useEffect(() => {
    const opener = document.activeElement,
      node = ref.current;
    node.showModal();
    return () => {
      if (node.open) node.close();
      opener?.isConnected && opener.focus({ preventScroll: true });
    };
  }, []);
  return createPortal(
    <dialog
      ref={ref}
      className="eis-dialog"
      aria-label={title}
      onCancel={(e) => {
        e.preventDefault();
        if (!busy) onClose();
      }}
    >
      <header>
        <div>
          <span>PLANORA · EISENHOWER</span>
          <h2>{title}</h2>
        </div>
        <button
          type="button"
          aria-label="Đóng cửa sổ"
          disabled={busy}
          onClick={onClose}
        >
          {icon("close")}
        </button>
      </header>
      {children}
    </dialog>,
    document.body,
  );
}
export function Checklist({ items, onChange, onTextChange = onChange, disabled, showCompletion = true }) {
  const list = useRef(null);
  const dragOrigin = useRef(null);
  const sortable = !disabled && items.length > 1;
  const { drag, start } = useEisenhowerDrag(
    (element, id, y) => {
      if (!list.current?.contains(element)) return null;
      const top = list.current.getBoundingClientRect().top;
      // Offsets describe the original layout while the cards animate around it.
      const before = [...list.current.children].find((row) =>
        row.dataset.checkId !== id && y < top + row.offsetTop + row.offsetHeight / 2,
      );
      return { beforeId: before?.dataset.checkId ?? null };
    },
    (id, drop) => reorder(id, drop.beforeId),
    !sortable,
  );
  const from = drag ? items.findIndex((item) => item.id === drag.id) : -1;
  const before = drag?.target?.beforeId ? items.findIndex((item) => item.id === drag.target.beforeId) : items.length;
  const to = !drag?.target || before === -1 ? from : before > from ? before - 1 : before;

  function reorder(id, beforeId) {
    const next = moveChecklist(items, id, beforeId);
    if (next !== items) onChange(next);
  }

  function beginDrag(event, id) {
    if (!sortable || event.button !== 0 || event.target.closest("button, input")) return;
    const { currentTarget: row } = event;
    dragOrigin.current = {
      top: list.current.getBoundingClientRect().top,
      step: row.offsetHeight + (Number.parseFloat(getComputedStyle(list.current).rowGap) || 0),
    };
    start(event, id);
  }

  function displacement(index) {
    if (from === -1) return 0;
    if (index === from) return drag.offsetY + dragOrigin.current.top - list.current.getBoundingClientRect().top;
    if (index > from && index <= to) return -dragOrigin.current.step;
    if (index < from && index >= to) return dragOrigin.current.step;
    return 0;
  }

  function update(id, patch, change = onChange) {
    change(items.map((i) => (i.id === id ? { ...i, ...patch } : i)));
  }
  return (
    <section className="eis-check-editor" aria-label="Checklist của task">
      <div className="eis-section-title">
        <h3>Checklist</h3>
        {showCompletion && <span>
          {items.filter((i) => i.completed).length}/{items.length} xong
        </span>}
      </div>
      {showCompletion && <p>
        Các bước nhỏ để hoàn thành việc. Tích checklist không tự hoàn thành cả
        task.
      </p>}
      <div ref={list} className={`eis-check-list${drag ? " is-sorting" : ""}`} data-check-list>
        {items.map((item, index) => (
          <div
            className={`eis-check-row${sortable ? " is-sortable" : ""}${drag?.id === item.id ? " is-dragging" : ""}`}
            key={item.id}
            data-check-id={item.id}
            role="group"
            aria-label={`Mục checklist ${index + 1}`}
            aria-keyshortcuts={sortable ? "Alt+ArrowUp Alt+ArrowDown" : undefined}
            tabIndex={sortable ? 0 : undefined}
            style={drag ? { transform: `translateY(${displacement(index)}px)` } : undefined}
            onPointerDown={(event) => beginDrag(event, item.id)}
            onKeyDown={(event) => {
              if (!sortable || event.target !== event.currentTarget || !event.altKey ||
                  !["ArrowUp", "ArrowDown"].includes(event.key)) return;
              event.preventDefault();
              if (event.key === "ArrowUp" && index > 0) reorder(item.id, items[index - 1].id);
              if (event.key === "ArrowDown") reorder(item.id, items[index + 2]?.id ?? null);
            }}
          >
            {showCompletion && <input
              type="checkbox"
              aria-label={`Hoàn thành mục ${index + 1}`}
              checked={item.completed}
              disabled={disabled}
              onChange={(e) => update(item.id, { completed: e.target.checked })}
            />}
            <input
              aria-label={`Nội dung mục ${index + 1}`}
              value={item.text}
              maxLength={300}
              disabled={disabled}
              onChange={(e) => update(item.id, { text: e.target.value }, onTextChange)}
              className={item.completed ? "is-completed" : ""}
            />
            <button
              type="button"
              aria-label={`Xóa mục ${index + 1}`}
              disabled={disabled}
              onClick={() => onChange(items.filter((i) => i.id !== item.id))}
            >
              {icon("close")}
            </button>
          </div>
        ))}
      </div>
      <button
        type="button"
        className="eis-inline-add"
        disabled={disabled || items.length >= 100}
        onClick={() =>
          onChange([
            ...items,
            { id: crypto.randomUUID(), text: "", completed: false },
          ])
        }
      >
        {icon("plus")} Thêm bước
      </button>
    </section>
  );
}
function Editor({
  task,
  quadrant,
  columns,
  busy,
  error,
  onSave,
  onClose,
  onComplete,
  onTeam,
}) {
  const [name, setName] = useState(task?.name || ""),
    [notes, setNotes] = useState(task?.description || "");
  const [deadline, setDeadline] = useState(
    task?.deadline ? localInput(task.deadline) : "",
  );
  const [minutes, setMinutes] = useState(
    task?.estimatedMinutes || task?.remainingMinutes || "",
  );
  const [group, setGroup] = useState(
    quadrant || (task?.urgencySource === "User" ? task.quadrant : "Auto"),
  );
  const [items, setItems] = useState(task?.checklist || []),
    [split, setSplit] = useState(task?.splittable !== false);
  const [validation, setValidation] = useState("");
  const personal =
    !task || (task.canEdit !== false && task.kind === "Personal");
  return (
    <Dialog
      title={task ? "Chi tiết task" : "Thêm task vào ma trận"}
      onClose={onClose}
      busy={busy}
    >
      <form
        onSubmit={(e) => {
          e.preventDefault();
          try {
            onSave({
              name: name.trim(),
              description: notes,
              deadline: toUtc(deadline),
              estimatedMinutes: Number(minutes),
              quadrant: group,
              checklist: items.filter((i) => i.text.trim()),
              splittable: !task?.requiresContinuousSession && split,
            });
          } catch (failure) {
            setValidation(failure.message);
          }
        }}
      >
        {(error || validation) && (
          <p className="eis-board-error" role="alert">
            {error || validation}
          </p>
        )}
        <label>
          Tên công việc
          <input
            autoFocus
            required
            maxLength={200}
            value={name}
            onChange={(e) => setName(e.target.value)}
            disabled={busy || !personal}
            placeholder="Bạn cần làm gì?"
          />
        </label>
        <div className="eis-editor-grid">
          <label>
            Hạn chót
            <input
              type="datetime-local"
              required
              value={deadline}
              onChange={(e) => setDeadline(e.target.value)}
              disabled={busy || !personal}
            />
          </label>
          <label>
            Thời lượng (phút)
            <input
              type="number"
              required
              min="1"
              max="43200"
              value={minutes}
              onChange={(e) => setMinutes(e.target.value)}
              disabled={busy || !personal}
            />
          </label>
        </div>
        <label>
          Vị trí ưu tiên
          <select
            aria-label="Vị trí ưu tiên"
            value={group}
            disabled={busy || !personal}
            onChange={(e) => setGroup(e.target.value)}
          >
            <option value="Auto">
              Giữ mức quan trọng, tự tính độ khẩn cấp
            </option>
            {columns
              .filter((c) => c.id.startsWith("Q"))
              .map((c) => (
                <option key={c.id} value={c.id}>
                  {c.id} · {c.caption}
                </option>
              ))}
          </select>
        </label>
        <label>
          Yêu cầu & ghi chú
          <textarea
            rows="3"
            maxLength={4000}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            disabled={busy || !personal}
            placeholder="Kết quả cần đạt, tài liệu và điều cần nhớ…"
          />
        </label>
        <label className="eis-check-label">
          <input
            type="checkbox"
            checked={!task?.requiresContinuousSession && split}
            disabled={busy || !personal || task?.requiresContinuousSession === true}
            onChange={(e) => setSplit(e.target.checked)}
          />
          Có thể chia thành nhiều phiên làm việc
        </label>
        {task?.requiresContinuousSession && (
          <p className="eis-editor-note">
            Hoạt động này cần diễn ra liên tục, không thể chia thành nhiều phiên.
          </p>
        )}
        <Checklist items={items} onChange={setItems} disabled={busy} />
        {!personal && (
          <p className="eis-editor-note">
            Yêu cầu và deadline task nhóm do trưởng nhóm quản lý. Checklist này
            chỉ dành riêng cho bạn.
          </p>
        )}
        {task && <div className="eis-editor-note">{task.explanation}</div>}
        <footer>
          <div>
            {task && personal && (
              <button type="button" disabled={busy} onClick={onComplete}>
                {icon("check")} Đánh dấu hoàn thành task
              </button>
            )}
            {task?.teamTaskId && (
              <button type="button" disabled={busy} onClick={onTeam}>
                Mở task trong nhóm ↗
              </button>
            )}
          </div>
          <button type="submit" className="btn primary" disabled={busy}>
            {busy ? "Đang lưu…" : task ? "Lưu thay đổi" : "Tạo task"}
          </button>
        </footer>
        <p className="eis-editor-note">
          Lưu task/checklist chưa tạo phiên trong Calendar. Bạn xem đề xuất rồi
          chọn áp dụng.
        </p>
      </form>
    </Dialog>
  );
}
function Proposal({ plan, task, busy, error, onApply, onClose }) {
  const blocked = (plan.warnings || []).some(
    (w) => w.unscheduledMinutes > 0 || w.code === "SOFT_DEADLINE_EXCEEDED",
  );
  const slots = plan.sessions.filter((s) => !s.preserved);
  const names = Object.fromEntries(
    (state.workspace.tasks || []).map((t) => [t.id, t.name]),
  );
  const kept = new Set(
    plan.sessions.filter((s) => s.preserved).map((s) => s.id),
  );
  const moved = (state.workspace.sessions || []).filter(
    (s) =>
      s.status === "Scheduled" &&
      !kept.has(s.id) &&
      Date.parse(s.start) >= Date.parse(plan.start) &&
      Date.parse(s.start) < Date.parse(plan.end),
  );
  return (
    <Dialog title="NORA · Lịch đề xuất" onClose={onClose} busy={busy}>
      <p className="eis-editor-note">
        Dựa trên deadline, thời lượng và lịch riêng của bạn. Lịch hiện tại chưa
        thay đổi.
      </p>
      {error && (
        <p className="eis-board-error" role="alert">
          {error}
        </p>
      )}
      {moved.length > 0 && (
        <section className="eis-plan-section">
          <h3>Phiên cũ sẽ được thay thế</h3>
          {moved.map((s) => (
            <p key={s.id}>
              {names[s.taskId] || task.name} · {dateFmt(s.start)} ·{" "}
              {timeFmt(s.start)}–{timeFmt(s.end)}
            </p>
          ))}
        </section>
      )}
      <section className="eis-plan-section">
        <h3>Phiên mới</h3>
        {slots.length ? (
          slots.map((s) => (
            <p key={s.id}>
              <strong>{names[s.taskId] || task.name}</strong>
              <span>
                {dateFmt(s.start)} · {timeFmt(s.start)}–{timeFmt(s.end)}
              </span>
            </p>
          ))
        ) : (
          <p>Chưa có phiên mới phù hợp.</p>
        )}
      </section>
      {(plan.warnings || []).map((w, i) => (
        <p key={i} className="eis-board-error">
          {w.taskName}: {w.message}{" "}
          {w.unscheduledMinutes > 0 &&
            `Còn thiếu ${w.unscheduledMinutes} phút.`}
        </p>
      ))}
      <footer>
        <button type="button" onClick={onClose} disabled={busy}>
          Để sau
        </button>
        <button
          type="button"
          className="btn primary"
          onClick={onApply}
          disabled={busy || blocked || !slots.length}
        >
          {busy ? "Đang áp dụng…" : "Áp dụng vào Calendar"}
        </button>
      </footer>
    </Dialog>
  );
}
function TaskRow({
  task: t,
  columns,
  busy,
  drag,
  start,
  onOpen,
  onMove,
  onChecklist,
  onSuggest,
  onComplete,
  onReorder,
  manual,
}) {
  const [expanded, setExpanded] = useState(false),
    [actions, setActions] = useState(false);
  const manageable = canManage(t),
    items = t.checklist || [];
  return (
    <article
      data-eis-card={t.id}
      className={`eis-task eis-board-task${drag?.id === t.id ? " is-dragging" : ""}${drag?.target?.beforeId === t.id ? " is-drop-before" : ""}`}
    >
      <div className="eis-row-main">
        {manageable && (
          <button
            className="eis-drag-handle"
            type="button"
            disabled={busy}
            aria-label={`Kéo task ${t.name}`}
            title={
              manual
                ? "Kéo để phân loại hoặc sắp thứ tự"
                : "Kéo sang ô khác; chọn Thủ công để sắp thứ tự"
            }
            onPointerDown={(e) => start(e, t.id)}
            onKeyDown={(e) => onReorder(e, t)}
          >
            {icon("menu")}
          </button>
        )}
        {t.kind === "Personal" && manageable ? (
          <button
            type="button"
            className="eis-task-complete"
            aria-label={`Hoàn thành task ${t.name}`}
            disabled={busy}
            onClick={() => onComplete(t)}
          >
            {icon("check")}
          </button>
        ) : (
          <span
            className="eis-task-kind"
            title={
              t.kind === "Draft"
                ? "Task nháp trong Inbox"
                : t.kind === "Review"
                  ? "Chờ trưởng nhóm duyệt"
                  : "Task nhóm"
            }
          >
            {icon(
              t.kind === "Draft"
                ? "edit"
                : t.kind === "Review"
                  ? "check"
                  : "users",
            )}
          </span>
        )}
        <div className="eis-row-content">
          <h4>
            <button type="button" onClick={() => onOpen(t)}>
              {t.name}
            </button>
          </h4>
          <div className="eis-facts">
            <span title="Hạn chót">
              {icon("calendar")}
              {t.deadline ? (
                <>
                  {dateFmt(t.deadline)} · {timeFmt(t.deadline)}
                </>
              ) : (
                "Chưa có ngày hạn"
              )}
            </span>
            {t.kind !== "Review" && t.kind !== "Draft" && (
              <span>{t.remainingMinutes} phút</span>
            )}
            {!!items.length && (
              <button
                type="button"
                className="eis-check-toggle"
                aria-label={`Checklist ${t.name} · ${items.filter((i) => i.completed).length}/${items.length}`}
                aria-expanded={expanded}
                onClick={() => setExpanded((v) => !v)}
              >
                {icon("check")}
                {items.filter((i) => i.completed).length}/{items.length}
                <span aria-hidden="true">{expanded ? "⌃" : "⌄"}</span>
              </button>
            )}
            {t.kind !== "Personal" && (
              <span className="eis-source-label">
                {t.kind === "Draft"
                  ? "Inbox · Bản nháp"
                  : t.kind === "Review"
                    ? "Chờ duyệt"
                    : t.kind === "NeedsAcceptance"
                      ? "Chưa nhận"
                      : "Nhóm"}
              </span>
            )}
            {t.urgencySource === "User" && (
              <span title="Bạn đã chọn phân loại này" className="eis-user-mark">
                Bạn chọn
              </span>
            )}
          </div>
          {(t.atRisk || t.overdue || t.blocked) && (
            <div className="eis-signals">
              {(t.atRisk || t.overdue) && (
                <span className="risk">
                  {t.overdue ? "Đã quá hạn" : "Nguy cơ trễ hạn"}
                </span>
              )}
              {t.blocked && <span>Có việc phụ thuộc</span>}
            </div>
          )}
        </div>
        <button
          type="button"
          className="eis-row-more"
          aria-label={`Tùy chọn ${t.name}`}
          aria-expanded={actions}
          onClick={() => setActions((v) => !v)}
        >
          ···
        </button>
      </div>
      {expanded && (
        <div
          className="eis-card-checklist"
          aria-label={`Các bước của ${t.name}`}
        >
          {items.slice(0, 3).map((i, n) => (
            <label key={i.id}>
              <input
                type="checkbox"
                checked={i.completed}
                disabled={busy || !manageable}
                onChange={() => onChecklist(t, n)}
              />
              <span className={i.completed ? "is-completed" : ""}>
                {i.text}
              </span>
            </label>
          ))}
          <button type="button" onClick={() => onOpen(t)}>
            Mở checklist đầy đủ · {items.length} bước ↗
          </button>
        </div>
      )}
      {actions && (
        <div className="eis-card-actions" aria-label={`Thao tác ${t.name}`}>
          <button type="button" onClick={() => onOpen(t)}>
            {icon("edit")}Chi tiết task
          </button>
          {manageable && (
            <>
              <button
                type="button"
                disabled={busy}
                onClick={() => {
                  setActions(false);
                  onSuggest(t);
                }}
              >
                {icon(t.kind === "Draft" ? "edit" : "calendar")}
                {t.kind === "Draft"
                  ? "Hoàn thiện trong Nhập việc"
                  : recommendation(t) === "SuggestSchedule"
                    ? "Đề xuất lịch"
                    : recommendation(t) === "QuickAction"
                      ? "Xếp giờ nếu cần"
                      : "Chọn giờ làm"}
              </button>
              <label className="eis-move-select">
                <span className="sr-only">Di chuyển {t.name}</span>
                <select
                  aria-label={`Di chuyển ${t.name}`}
                  disabled={busy}
                  value={t.quadrant}
                  onChange={(e) => {
                    setActions(false);
                    onMove(t.id, { quadrant: e.target.value });
                  }}
                >
                  <option value="Unknown">Tự tính độ khẩn cấp</option>
                  {columns
                    .filter((c) => c.id.startsWith("Q"))
                    .map((c) => (
                      <option value={c.id} key={c.id}>
                        {c.id} · {c.label}
                      </option>
                    ))}
                </select>
              </label>
            </>
          )}
          {recommendation(t) === "QuickAction" && (
            <small className="eis-quick-note">
              Việc ngắn: có thể làm ngay, không bắt buộc tạo phiên.
            </small>
          )}
        </div>
      )}
    </article>
  );
}

export function EisenhowerBoard({
  view,
  error,
  retry,
  columns,
  onRefresh,
  onOpenTeam,
}) {
  const [query, setQuery] = useState(""),
    [mobile, setMobile] = useState("Q1"),
    [editor, setEditor] = useState(null);
  const [busy, setBusy] = useState(false),
    [failure, setFailure] = useState(""),
    [message, setMessage] = useState("");
  const [proposal, setProposal] = useState(null),
    [scope, setScope] = useState("All"),
    [period, setPeriod] = useState("All"),
    [sort, setSort] = useState("Manual"),
    [tab, setTab] = useState("Matrix");
  const guard = useRef(false),
    mounted = useRef(true),
    owner = state.workspace.profile.id;
  const ownerRef = useRef(owner);
  ownerRef.current = owner;
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  const today = dayKey(Date.now());
  const [year, month, date] = today.split("-").map(Number);
  const endDay = (days) =>
    new Date(Date.UTC(year, month - 1, date + days)).toISOString().slice(0, 10);
  const tasks = (view?.tasks || [])
    .filter((t) => {
      const due = t.deadline ? dayKey(t.deadline) : null;
      return (
        t.name
          .toLocaleLowerCase("vi")
          .includes(query.trim().toLocaleLowerCase("vi")) &&
        (scope === "All" ||
          (scope === "Personal"
            ? ["Personal", "Draft"].includes(t.kind)
            : !["Personal", "Draft"].includes(t.kind))) &&
        (period === "All" ||
          (period === "NoDate"
            ? !due
            : period === "Overdue"
              ? due && Date.parse(t.deadline) < Date.now()
              : due &&
                due <=
                  (period === "Today" ? today : endDay(Number(period) - 1))))
      );
    })
    .sort((a, b) =>
      sort === "Deadline"
        ? (a.deadline ? Date.parse(a.deadline) : Infinity) -
          (b.deadline ? Date.parse(b.deadline) : Infinity)
        : sort === "Duration"
          ? a.remainingMinutes - b.remainingMinutes
          : (a.order || 0) - (b.order || 0) ||
            (a.deadline ? Date.parse(a.deadline) : Infinity) -
              (b.deadline ? Date.parse(b.deadline) : Infinity),
    );
  const visibleColumns = columns.filter((c) =>
    tab === "Matrix" ? c.id.startsWith("Q") : c.id === tab,
  );
  const revision = view?.revision || state.workspace.preference.revision;
  async function run(action, done) {
    if (guard.current) return;
    guard.current = true;
    setBusy(true);
    setFailure("");
    try {
      const result = await action();
      if (!mounted.current || ownerRef.current !== owner) return;
      await onRefresh();
      if (mounted.current && ownerRef.current === owner) done?.(result);
    } catch (e) {
      if (mounted.current && ownerRef.current === owner) setFailure(e.message);
    } finally {
      guard.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  function move(id, drop) {
    const task = view?.tasks.find((t) => t.id === id);
    if (!task || !canManage(task) || drop.beforeId === id) return;
    if (task.kind === "Draft" && task.quadrant === drop.quadrant) return;
    run(
      () =>
        api(
          `/api/eisenhower/${task.kind === "Draft" ? "drafts" : "tasks"}/${id}/position`,
          "POST",
          {
            version: task.version,
            revision,
            quadrant: drop.quadrant,
            beforeId:
              task.kind === "Draft" ||
              view.tasks.find((row) => row.id === drop.beforeId)?.kind ===
                "Draft"
                ? null
                : drop.beforeId || null,
          },
        ),
      () => {
        setMobile(drop.quadrant);
        if (drop.quadrant.startsWith("Q")) setTab("Matrix");
        setMessage(
          `Đã cập nhật vị trí “${task.name}”. Deadline và lịch giữ nguyên.`,
        );
      },
    );
  }
  const { drag, start } = useEisenhowerDrag(
    (element, id, y) => {
      const section = element?.closest("[data-eis-drop]");
      if (!section) return null;
      if (sort !== "Manual")
        return view?.tasks.find((t) => t.id === id)?.quadrant ===
          section.dataset.eisDrop
          ? null
          : { quadrant: section.dataset.eisDrop, beforeId: null };
      const card = element.closest("[data-eis-card]");
      if (card?.dataset.eisCard === id) return null;
      let beforeId = card?.dataset.eisCard || null;
      if (
        card &&
        y >
          card.getBoundingClientRect().top +
            card.getBoundingClientRect().height / 2
      )
        beforeId = card.nextElementSibling?.dataset.eisCard || null;
      if (
        beforeId &&
        !canManage(view.tasks.find((t) => t.id === beforeId) || {})
      )
        return null;
      return { quadrant: section.dataset.eisDrop, beforeId };
    },
    move,
    busy,
  );
  function open(task = null, quadrant = "Auto") {
    setFailure("");
    setEditor({ task, quadrant: task ? undefined : quadrant });
  }
  function checklist(task, index) {
    const items = task.checklist.map((i, n) =>
      n === index ? { ...i, completed: !i.completed } : i,
    );
    run(() =>
      api(`/api/eisenhower/tasks/${task.id}/checklist`, "PUT", {
        version: task.version,
        items,
      }),
    );
  }
  function suggest(task) {
    if (task.kind === "Draft") {
      openInboxNote(task.id);
      return;
    }
    setFailure("");
    run(
      () =>
        api(`/api/eisenhower/tasks/${task.id}/schedule`, "POST", {
          version: task.version,
          revision,
          explicitRequest: !["Q1", "Q2"].includes(task.quadrant),
        }),
      (plan) => setProposal({ plan, task }),
    );
  }
  function save(value) {
    const task = editor.task;
    if (!task || task.kind === "Draft") {
      run(
        () =>
          api(
            `/api/eisenhower/drafts${task ? `/${task.id}` : ""}`,
            task ? "PUT" : "POST",
            {
              ...value,
              version: task?.version || "00000000-0000-0000-0000-000000000000",
            },
          ),
        () => {
          setEditor(null);
          if (!task) {
            setScope("All");
            setPeriod("All");
            setQuery("");
            setTab(value.quadrant.startsWith("Q") ? "Matrix" : "Unknown");
            if (value.quadrant.startsWith("Q")) setMobile(value.quadrant);
          }
          setMessage(
            task
              ? "Đã lưu task nháp trong Inbox."
              : "Đã thêm task nháp vào Inbox và ma trận.",
          );
        },
      );
      return;
    }
    run(
      () =>
        task && task.canEdit === false
          ? api(`/api/eisenhower/tasks/${task.id}/checklist`, "PUT", {
              version: task.version,
              items: value.checklist,
            })
          : api(
              `/api/eisenhower/tasks${task ? `/${task.id}` : ""}`,
              task ? "PUT" : "POST",
              {
                ...value,
                version:
                  task?.version || "00000000-0000-0000-0000-000000000000",
              },
            ),
      () => {
        setEditor(null);
        setMessage(
          task
            ? "Đã lưu task và checklist."
            : "Đã tạo task. Chọn đề xuất lịch khi bạn sẵn sàng.",
        );
      },
    );
  }
  function complete(task) {
    run(
      () =>
        api(`/api/tasks/${task.id}/status`, "PATCH", {
          status: "Completed",
          progress: 100,
        }),
      () => {
        setEditor(null);
        setMessage(
          "Đã hoàn thành task và giải phóng các phiên tương lai của việc này.",
        );
      },
    );
  }
  function reorder(e, task, rows, column) {
    if (!e.altKey || sort !== "Manual" || task.kind === "Draft") return;
    rows = rows.filter((row) => row.kind !== "Draft");
    const index = rows.findIndex((t) => t.id === task.id);
    if (e.key === "ArrowUp" && index > 0) {
      e.preventDefault();
      move(task.id, { quadrant: column, beforeId: rows[index - 1].id });
    }
    if (e.key === "ArrowDown" && index < rows.length - 1) {
      e.preventDefault();
      move(task.id, {
        quadrant: column,
        beforeId: rows[index + 2]?.id || null,
      });
    }
  }
  return (
    <section
      className="eis-workspace eis-board"
      aria-label="Ma trận Eisenhower"
    >
      <div className="eis-heading">
        <div>
          <span className="team-eyebrow">TẬP TRUNG ĐÚNG VIỆC</span>
          <h2>Ma trận Eisenhower</h2>
          <p>Chọn điều quan trọng. Dành thời gian đúng lúc.</p>
        </div>
        <div className="eis-toolbar">
          <label className="team-search">
            {icon("search")}
            <input
              aria-label="Tìm trong ma trận"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Tìm công việc…"
            />
          </label>
          <button
            className="btn primary"
            type="button"
            disabled={busy}
            onClick={() => open()}
          >
            {icon("plus")}Thêm task
          </button>
          <button
            className="btn secondary"
            type="button"
            onClick={() =>
              askNora(
                "Giúp tôi làm rõ công việc mới, thời lượng, deadline và mức quan trọng để đưa vào ma trận Eisenhower. Chỉ đề xuất lịch khi tôi yêu cầu.",
              )
            }
          >
            {icon("spark")}Nhờ NORA
          </button>
        </div>
      </div>
      <div className="eis-board-controls">
        <div
          className="eis-board-tabs"
          role="group"
          aria-label="Chế độ xem ma trận"
        >
          {[
            { id: "Matrix", label: "Bốn ô ưu tiên" },
            ...columns.filter((c) => !c.id.startsWith("Q")),
          ].map((c) => (
            <button
              type="button"
              key={c.id}
              aria-pressed={tab === c.id}
              onClick={() => setTab(c.id)}
            >
              {c.label}
              <span>
                {
                  tasks.filter((t) =>
                    c.id === "Matrix"
                      ? t.quadrant.startsWith("Q")
                      : t.quadrant === c.id,
                  ).length
                }
              </span>
            </button>
          ))}
        </div>
        <div className="eis-filters" aria-label="Bộ lọc ma trận">
          <select
            aria-label="Nguồn công việc"
            value={scope}
            onChange={(e) => setScope(e.target.value)}
          >
            <option value="All">Tất cả công việc</option>
            <option value="Personal">Cá nhân</option>
            <option value="Team">Nhóm</option>
          </select>
          <select
            aria-label="Thời hạn công việc"
            value={period}
            onChange={(e) => setPeriod(e.target.value)}
          >
            <option value="All">Mọi thời hạn</option>
            <option value="NoDate">Chưa có ngày hạn</option>
            <option value="Today">Đến hôm nay</option>
            <option value="7">7 ngày tới</option>
            <option value="30">30 ngày tới</option>
            <option value="Overdue">Đã quá hạn</option>
          </select>
          <select
            aria-label="Sắp xếp công việc"
            value={sort}
            onChange={(e) => setSort(e.target.value)}
          >
            <option value="Manual">Thứ tự thủ công</option>
            <option value="Deadline">Deadline gần nhất</option>
            <option value="Duration">Thời lượng ngắn nhất</option>
          </select>
          {(query || scope !== "All" || period !== "All") && (
            <button
              type="button"
              onClick={() => {
                setQuery("");
                setScope("All");
                setPeriod("All");
              }}
            >
              Xóa bộ lọc
            </button>
          )}
        </div>
      </div>
      {(error || (failure && !editor && !proposal)) && (
        <p role="alert" className="eis-board-error">
          {error || failure}{" "}
          <button
            type="button"
            onClick={() => {
              setFailure("");
              retry();
            }}
          >
            Thử lại
          </button>
        </p>
      )}
      {message && (
        <p className="eis-board-message" role="status">
          {message}
        </p>
      )}
      {!view ? (
        <p role="status" className="eis-feedback">
          Đang đánh giá công việc từ lịch của bạn…
        </p>
      ) : (
        <>
          {tab === "Matrix" && (
            <div
              className="eis-mobile-tabs"
              role="group"
              aria-label="Chọn nhóm Eisenhower"
            >
              {columns
                .filter((c) => c.id.startsWith("Q"))
                .map((c) => (
                  <button
                    type="button"
                    key={c.id}
                    aria-pressed={mobile === c.id}
                    data-eis-drop={c.id}
                    className={
                      drag?.target?.quadrant === c.id ? "is-drop-zone" : ""
                    }
                    onClick={() => setMobile(c.id)}
                  >
                    <b>{c.id}</b>
                    {c.label}
                    <span>
                      {tasks.filter((t) => t.quadrant === c.id).length}
                    </span>
                  </button>
                ))}
            </div>
          )}
          <div className={`eis-grid${tab !== "Matrix" ? " eis-queue" : ""}`}>
            {visibleColumns.map((c) => {
              const rows = tasks.filter((t) => t.quadrant === c.id),
                main = c.id.startsWith("Q");
              return (
                <div
                  key={c.id}
                  className={mobile === c.id || !main ? "eis-visible" : ""}
                >
                  <section
                    className={`eis-quadrant ${c.tone}${drag?.target?.quadrant === c.id ? " is-drop-zone" : ""}`}
                    aria-label={c.label}
                    data-eis-drop={main ? c.id : undefined}
                  >
                    <header>
                      <div>
                        <h3>
                          <span>
                            {main
                              ? c.id
                              : icon(c.id === "Review" ? "check" : "spark")}
                          </span>
                          {c.label}
                          <strong className="eis-column-count">
                            {rows.length}
                          </strong>
                        </h3>
                        <p>{c.caption}</p>
                      </div>
                      {main && (
                        <button
                          type="button"
                          className="eis-column-add"
                          disabled={busy}
                          aria-label={`Thêm task vào ${c.label}`}
                          onClick={() => open(null, c.id)}
                        >
                          {icon("plus")}
                        </button>
                      )}
                    </header>
                    <div className="eis-items">
                      {rows.map((t) => (
                        <TaskRow
                          key={t.id}
                          task={t}
                          columns={columns}
                          busy={busy}
                          drag={drag}
                          start={start}
                          manual={sort === "Manual"}
                          onReorder={(e, task) => reorder(e, task, rows, c.id)}
                          onOpen={(task) =>
                            canManage(task) ? open(task) : onOpenTeam(task)
                          }
                          onMove={move}
                          onChecklist={checklist}
                          onSuggest={suggest}
                          onComplete={complete}
                        />
                      ))}
                      {!rows.length && (
                        <div className="eis-empty">
                          <span className="eis-empty-symbol">
                            {icon(
                              main
                                ? "check"
                                : c.id === "Review"
                                  ? "users"
                                  : "spark",
                            )}
                          </span>
                          <strong>
                            {query || scope !== "All" || period !== "All"
                              ? "Không có công việc phù hợp"
                              : c.id === "Review"
                                ? "Chưa có kết quả chờ duyệt"
                                : c.id === "Unknown"
                                  ? "Mọi việc đã được phân loại"
                                  : "Chưa có công việc"}
                          </strong>
                          <span>
                            {query || scope !== "All" || period !== "All"
                              ? "Thử đổi hoặc xóa bộ lọc."
                              : main
                                ? advice[c.id]
                                : c.caption}
                          </span>
                          {main && (
                            <button
                              type="button"
                              disabled={busy}
                              onClick={() => open(null, c.id)}
                            >
                              + Thêm task đầu tiên
                            </button>
                          )}
                        </div>
                      )}
                    </div>
                  </section>
                </div>
              );
            })}
          </div>
        </>
      )}
      {editor && (!editor.task || editor.task.kind === "Draft") && (
        <EisenhowerQuickCreate
          key={editor.task?.id || "new"}
          task={editor.task}
          quadrant={editor.quadrant}
          columns={columns}
          busy={busy}
          error={failure}
          onSave={save}
          onClose={() => setEditor(null)}
        />
      )}
      {editor?.task && editor.task.kind !== "Draft" && (
        <Editor
          key={editor.task?.id || "new"}
          task={editor.task}
          quadrant={editor.quadrant}
          columns={columns}
          busy={busy}
          error={failure}
          onSave={save}
          onClose={() => setEditor(null)}
          onTeam={() => {
            const t = editor.task;
            setEditor(null);
            onOpenTeam(t);
          }}
          onComplete={() => complete(editor.task)}
        />
      )}
      {proposal && (
        <Proposal
          {...proposal}
          busy={busy}
          error={failure}
          onClose={() => setProposal(null)}
          onApply={() =>
            run(
              () => api(`/api/plans/${proposal.plan.id}/apply`, "POST"),
              () => {
                setProposal(null);
                setMessage(
                  "Đã áp dụng lịch. Bạn có thể xem trong Lịch làm việc.",
                );
              },
            )
          }
        />
      )}
    </section>
  );
}
