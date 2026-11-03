import { useEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { state, icon, dateFmt, timeFmt } from "./ui.jsx";
import { askNora } from "./agent-handoff.js";
import "./eisenhower.css";

export const eisenhowerEnabled = () =>
  state.workspace?.features?.includes("eisenhower-v1") === true;
export const importanceLabels = {
  Important: "Quan trọng",
  Lower: "Mức ảnh hưởng thấp hơn",
  Unknown: "Chưa xác định",
};
const quadrants = [
  {
    id: "Q1",
    label: "Làm trước",
    caption: "Quan trọng · Khẩn cấp",
    tone: "urgent",
  },
  {
    id: "Q2",
    label: "Chủ động lên lịch",
    caption: "Quan trọng · Chưa khẩn cấp",
    tone: "schedule",
  },
  {
    id: "Q3",
    label: "Xử lý / phân công",
    caption: "Ảnh hưởng thấp hơn · Khẩn cấp",
    tone: "delegate",
  },
  {
    id: "Q4",
    label: "Có thể để sau",
    caption: "Ảnh hưởng thấp hơn · Chưa khẩn cấp",
    tone: "later",
  },
  {
    id: "Unknown",
    label: "Cần làm rõ",
    caption: "Chưa đủ căn cứ phân loại",
    tone: "unknown",
  },
  {
    id: "Review",
    label: "Chờ bạn duyệt",
    caption: "Kết quả thành viên đã gửi",
    tone: "review",
  },
];
const requests = new Map();
let cached = null;
export function invalidateEisenhower() {
  cached = null;
  window.dispatchEvent(new Event("eisenhower-refresh"));
}
function fetchView(key) {
  if (
    cached?.key === key &&
    Date.parse(cached.value.nextEvaluationAt) > Date.now()
  )
    return Promise.resolve(cached.value);
  if (!requests.has(key))
    requests.set(
      key,
      api("/api/eisenhower")
        .then((value) => {
          cached = { key, value };
          return value;
        })
        .finally(() => requests.delete(key)),
    );
  return requests.get(key);
}
function useEisenhower() {
  const [view, setView] = useState(null),
    [error, setError] = useState(""),
    [refresh, setRefresh] = useState(0);
  const enabled = eisenhowerEnabled();
  const key = `${state.workspace?.profile?.id}:${state.workspace?.preference?.revision}`;
  useEffect(() => {
    if (!enabled) return;
    let live = true,
      timer;
    setView(null);
    setError("");
    fetchView(key)
      .then((result) => {
        if (!live) return;
        setView({ ...result, ownerKey: key });
        timer = setTimeout(
          () => setRefresh((value) => value + 1),
          Math.max(
            1000,
            Math.min(30000, Date.parse(result.nextEvaluationAt) - Date.now()),
          ),
        );
      })
      .catch((failure) => {
        if (live) setError(failure.message);
      });
    const listener = () => setRefresh((value) => value + 1);
    window.addEventListener("eisenhower-refresh", listener);
    return () => {
      live = false;
      clearTimeout(timer);
      window.removeEventListener("eisenhower-refresh", listener);
    };
  }, [enabled, key, refresh]);
  return {
    view: view?.ownerKey === key ? view : null,
    error,
    retry: () => {
      cached = null;
      setRefresh((value) => value + 1);
    },
  };
}
let pendingTeamTask = null;
export function takeEisenhowerTeamTask() {
  const task = pendingTeamTask;
  pendingTeamTask = null;
  return task;
}
function OpenTask({ task }) {
  if (!task.teamTaskId)
    return (
      <button type="button" data-action="task-detail" data-id={task.id}>
        {task.name}
      </button>
    );
  return (
    <button
      type="button"
      onClick={() => {
        pendingTeamTask = { teamId: task.teamId, taskId: task.teamTaskId };
        location.hash = "teams";
        window.dispatchEvent(new Event("eisenhower-open-team"));
      }}
    >
      {task.name}
    </button>
  );
}
function TaskItem({ task }) {
  return (
    <article className="eis-task">
      <h4>
        <OpenTask task={task} />
      </h4>
      <div className="eis-facts">
        <span>
          {icon("calendar")}
          {dateFmt(task.deadline)} · {timeFmt(task.deadline)}
        </span>
        {task.kind !== "Review" && <span>{task.remainingMinutes} phút</span>}
      </div>
      {(task.atRisk || task.kind === "NeedsAcceptance" || task.blocked) && (
        <div className="eis-signals">
          {task.atRisk && (
            <span className="risk">
              {task.overdue ? "Đã quá hạn" : "Nguy cơ trễ hạn"}
            </span>
          )}
          {task.kind === "NeedsAcceptance" && (
            <span>Cần nhận task · Đánh giá dự kiến</span>
          )}
          {task.blocked && <span>Có việc phụ thuộc</span>}
        </div>
      )}
      <p>{task.explanation}</p>
    </article>
  );
}
function Quadrant({ column, tasks }) {
  const [page, setPage] = useState(0);
  const last = Math.max(0, Math.ceil(tasks.length / 4) - 1),
    current = Math.min(page, last);
  useEffect(() => setPage(0), [tasks.map((task) => task.id).join("|")]);
  return (
    <section
      className={`eis-quadrant ${column.tone}`}
      aria-label={column.label}
    >
      <header>
        <div>
          <h3>
            <span>
              {column.id.startsWith("Q")
                ? column.id
                : icon(column.id === "Review" ? "check" : "spark")}
            </span>
            {column.label}
          </h3>
          <p>{column.caption}</p>
        </div>
        <strong>{tasks.length}</strong>
      </header>
      <div className="eis-items">
        {tasks.slice(current * 4, current * 4 + 4).map((task) => (
          <TaskItem key={task.id} task={task} />
        ))}
        {!tasks.length && (
          <p className="eis-empty">Chưa có công việc trong nhóm này.</p>
        )}
      </div>
      {last > 0 && (
        <nav
          className="eis-pagination"
          aria-label={`Phân trang ${column.label}`}
        >
          <button
            type="button"
            aria-label={`Trang trước ${column.label}`}
            disabled={current === 0}
            onClick={() => setPage(current - 1)}
          >
            ←
          </button>
          <span>
            {current + 1} / {last + 1}
          </span>
          <button
            type="button"
            aria-label={`Trang tiếp ${column.label}`}
            disabled={current === last}
            onClick={() => setPage(current + 1)}
          >
            →
          </button>
        </nav>
      )}
    </section>
  );
}
export function EisenhowerMatrix() {
  const { view, error, retry } = useEisenhower();
  const [query, setQuery] = useState(""),
    [mobile, setMobile] = useState("Q1");
  const all = view?.tasks || [],
    tasks = all.filter((task) =>
      task.name.toLocaleLowerCase("vi").includes(query.toLocaleLowerCase("vi")),
    );
  return (
    <section className="eis-workspace" aria-label="Ma trận Eisenhower">
      <div className="eis-heading">
        <div>
          <span className="team-eyebrow">TẬP TRUNG ĐÚNG VIỆC</span>
          <h2>Ma trận Eisenhower</h2>
          <p>Độ gấp được tính theo lịch riêng của bạn, trong 30 ngày tới.</p>
        </div>
        <label className="team-search">
          {icon("search")}
          <input
            aria-label="Tìm trong ma trận"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Tìm công việc…"
          />
        </label>
      </div>
      {error ? (
        <p className="eis-feedback" role="alert">
          {error}{" "}
          <button type="button" onClick={retry}>
            Thử lại
          </button>
        </p>
      ) : !view ? (
        <p className="eis-feedback" role="status">
          Đang đánh giá công việc từ lịch của bạn…
        </p>
      ) : (
        <>
          <div
            className="eis-mobile-tabs"
            role="group"
            aria-label="Chọn nhóm Eisenhower"
          >
            {quadrants.map((column) => (
              <button
                key={column.id}
                type="button"
                aria-pressed={mobile === column.id}
                onClick={() => setMobile(column.id)}
              >
                {column.label}
                <span>
                  {tasks.filter((task) => task.quadrant === column.id).length}
                </span>
              </button>
            ))}
          </div>
          <div className="eis-grid">
            {quadrants.map((column) => (
              <div
                key={column.id}
                className={mobile === column.id ? "eis-visible" : ""}
              >
                <Quadrant
                  column={column}
                  tasks={tasks.filter((task) => task.quadrant === column.id)}
                />
              </div>
            ))}
          </div>
          <p className="eis-footnote">
            {icon("lock")} Xem và phân loại không thay đổi lịch. “Có thể để sau”
            vẫn giữ các deadline đã cam kết.
          </p>
        </>
      )}
    </section>
  );
}
export function EisenhowerFocus() {
  const { view, error, retry } = useEisenhower();
  if (!eisenhowerEnabled()) return null;
  const rank = (task) =>
    task.kind === "Review"
      ? 1
      : task.overdue
        ? 0
        : task.atRisk
          ? 1
          : task.quadrant === "Q1"
            ? 2
            : task.quadrant === "Q3"
              ? 3
              : task.quadrant === "Q2"
                ? 4
                : 5;
  const tasks = [...(view?.tasks || [])]
    .filter((task) => task.quadrant !== "Q4")
    .sort(
      (a, b) =>
        rank(a) - rank(b) || Date.parse(a.deadline) - Date.parse(b.deadline),
    )
    .slice(0, 3);
  return (
    <section className="eis-focus" aria-label="Nên tập trung hôm nay">
      <header>
        <div>
          <h2>{icon("spark")} Nên tập trung hôm nay</h2>
          <p>Gợi ý từ deadline, mức quan trọng và quỹ thời gian cá nhân.</p>
        </div>
        <button type="button" data-action="nav" data-id="tasks">
          Xem công việc ↗
        </button>
      </header>
      {error ? (
        <p role="alert">
          {error}{" "}
          <button type="button" onClick={retry}>
            Thử lại
          </button>
        </p>
      ) : !view ? (
        <p role="status">Đang đánh giá…</p>
      ) : tasks.length ? (
        <div className="eis-focus-items">
          {tasks.map((task) => (
            <TaskItem key={task.id} task={task} />
          ))}
        </div>
      ) : (
        <p className="eis-empty">
          Chưa có công việc cần ưu tiên. Bạn có thể thêm công việc hoặc xem lịch
          hôm nay.
        </p>
      )}
    </section>
  );
}
export function ImportancePreview({ value }) {
  if (!value) return null;
  return (
    <div className="eis-importance-preview">
      <strong>
        {importanceLabels[value.level] || importanceLabels.Unknown}
      </strong>
      <p>
        {value.reason || "NORA cần thêm căn cứ về ảnh hưởng của công việc."}
      </p>
    </div>
  );
}
const importanceRequests = new Map();
function requestImportance(url, version) {
  const key = `${state.workspace?.profile?.id}:${url}:${version}`;
  if (!importanceRequests.has(key)) {
    importanceRequests.set(
      key,
      api(url, "POST", { version }).finally(() =>
        importanceRequests.delete(key),
      ),
    );
  }
  return importanceRequests.get(key);
}
export function TaskEisenhowerDetail(props) {
  if (!eisenhowerEnabled()) return null;
  return (
    <ImportanceDetail
      key={`${props.team}:${props.leader}:${props.task.id}:${props.task.version}`}
      {...props}
    />
  );
}
function ImportanceDetail({ task, team = false, leader = false, onChanged }) {
  const [assessment, setAssessment] = useState(null),
    [error, setError] = useState("");
  const [editing, setEditing] = useState(false),
    [level, setLevel] = useState(task.importance?.level || "Unknown"),
    [reason, setReason] = useState(""),
    [busy, setBusy] = useState(false);
  const canEdit = !team || leader;
  const [current, setCurrent] = useState(
    task.importance || { level: "Unknown" },
  );
  const [proposal, setProposal] = useState(
    task.importance?.source === "AI" &&
      !task.importance.confirmed &&
      task.importance.level !== "Unknown"
      ? { version: task.version, suggestion: task.importance }
      : null,
  );
  const [proposing, setProposing] = useState(false);
  const [proposalError, setProposalError] = useState("");
  const [editVersion, setEditVersion] = useState(task.version);
  const mounted = useRef(false);
  const proposalEpoch = useRef(0);
  const base = `/api/${team ? "teams/tasks" : "tasks"}/${task.id}`;
  async function propose() {
    const epoch = ++proposalEpoch.current;
    setProposing(true);
    setProposalError("");
    try {
      const value = await requestImportance(
        `${base}/importance/proposal`,
        task.version,
      );
      if (!value?.suggestion || value.version !== task.version)
        throw new Error("Đề xuất không còn phù hợp. Hãy tải lại task.");
      if (mounted.current && epoch === proposalEpoch.current)
        setProposal(value);
    } catch (failure) {
      if (mounted.current && epoch === proposalEpoch.current)
        setProposalError(failure.message);
    } finally {
      if (mounted.current && epoch === proposalEpoch.current)
        setProposing(false);
    }
  }
  useEffect(() => {
    mounted.current = true;
    if (
      canEdit &&
      task.version &&
      (!task.importance || task.importance.level === "Unknown")
    )
      propose();
    return () => {
      mounted.current = false;
    };
  }, []);
  function edit(
    value = proposal?.suggestion || current,
    version = proposal?.version || task.version,
  ) {
    setLevel(value.level);
    setReason(value.reason || "");
    setEditVersion(version);
    setError("");
    setEditing(true);
  }
  async function save(value, version) {
    proposalEpoch.current++;
    setProposing(false);
    setProposalError("");
    setBusy(true);
    setError("");
    try {
      await api(`${base}/importance`, "POST", {
        version,
        level: value.level,
        reason: value.reason.trim(),
      });
      invalidateEisenhower();
      if (mounted.current) {
        setCurrent({
          ...value,
          source: team ? "Leader" : "User",
          confirmed: true,
        });
        setProposal(null);
        setEditing(false);
      }
      await onChanged?.();
    } catch (failure) {
      if (mounted.current) setError(failure.message);
    } finally {
      if (mounted.current) setBusy(false);
    }
  }
  useEffect(() => {
    if (!eisenhowerEnabled()) return;
    setAssessment(null);
    setLevel(task.importance?.level || "Unknown");
    let live = true;
    api(`/api/${team ? "teams/tasks" : "tasks"}/${task.id}/eisenhower`)
      .then((value) => {
        if (live) setAssessment(value);
      })
      .catch(() => {});
    return () => {
      live = false;
    };
  }, [task.id, task.version, state.workspace?.preference?.revision, team]);
  if (!eisenhowerEnabled()) return null;
  return (
    <section className="eis-detail" aria-label="Đánh giá ưu tiên">
      <h3>
        {icon("spark")} Đánh giá ưu tiên {team ? "của bạn" : ""}
      </h3>
      {!(current.source === "AI" && !current.confirmed && proposal) && (
        <ImportancePreview
          value={
            current.level === "Unknown" && proposal
              ? {
                  ...current,
                  reason: "Chờ bạn xác nhận hoặc chỉnh sửa đề xuất bên dưới.",
                }
              : current
          }
        />
      )}
      {canEdit && (
        <div
          className="eis-proposal"
          aria-label="Đề xuất của NORA"
          aria-busy={proposing}
        >
          <strong>
            {icon("spark")} NORA đề xuất{team ? " cho nhóm" : ""}
          </strong>
          {proposing ? (
            <p role="status">NORA đang phân tích ảnh hưởng của công việc…</p>
          ) : proposal ? (
            <>
              <ImportancePreview value={proposal.suggestion} />
              <p className="eis-proposal-hint">
                Đây là đề xuất, chưa được lưu. Bạn có thể chỉnh sửa trước khi
                xác nhận.
              </p>
              {!editing && (
                <div className="eis-detail-actions">
                  <button
                    type="button"
                    className="btn secondary"
                    disabled={busy || proposal.suggestion.level === "Unknown"}
                    onClick={() => save(proposal.suggestion, proposal.version)}
                  >
                    Đồng ý đề xuất
                  </button>
                  <button
                    type="button"
                    className="team-text-button"
                    disabled={busy}
                    onClick={() => edit()}
                  >
                    Chỉnh sửa
                  </button>
                </div>
              )}
            </>
          ) : (
            <p>
              NORA dựa vào mục tiêu, đầu ra bắt buộc và ảnh hưởng đến các công
              việc khác để đề xuất.
            </p>
          )}
          {proposalError && <p role="alert">{proposalError}</p>}
          {!proposing && !editing && (
            <button
              type="button"
              className="team-text-button"
              disabled={busy}
              onClick={propose}
            >
              {proposalError
                ? "Thử lại đề xuất"
                : proposal
                  ? "Phân tích lại"
                  : "NORA đề xuất lại"}
            </button>
          )}
        </div>
      )}
      {assessment && (
        <div>
          <strong>
            {
              quadrants.find((column) => column.id === assessment.quadrant)
                ?.label
            }
          </strong>
          <p>{assessment.explanation}</p>
        </div>
      )}
      <div className="eis-detail-actions">
        <button
          type="button"
          className="team-text-button"
          onClick={() =>
            askNora(
              `Tôi muốn làm rõ mức quan trọng của công việc "${task.name}"${team ? " trong nhóm. Chỉ tư vấn, không sửa task nhóm." : ". Hãy giữ nguyên deadline, thời lượng và các yêu cầu khác."}`,
            )
          }
        >
          Trao đổi với NORA
        </button>
        {(!team || leader) && (
          <button
            type="button"
            className="team-text-button"
            disabled={busy}
            onClick={() => (editing ? setEditing(false) : edit())}
          >
            Điều chỉnh mức quan trọng{team ? " chung" : ""}
          </button>
        )}
      </div>
      {editing && (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            save({ level, reason }, editVersion);
          }}
        >
          <label>
            Mức quan trọng
            <select
              aria-label="Mức quan trọng"
              value={level}
              onChange={(event) => setLevel(event.target.value)}
              disabled={busy}
            >
              {Object.entries(importanceLabels).map(([key, label]) => (
                <option key={key} value={key}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Lý do
            <textarea
              aria-label="Lý do"
              required
              maxLength={1000}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              disabled={busy}
            />
          </label>
          <button
            type="submit"
            className="btn secondary"
            disabled={busy || !reason.trim()}
          >
            {busy ? "Đang lưu…" : "Lưu đánh giá"}
          </button>
        </form>
      )}
      {error && <p role="alert">{error}</p>}
    </section>
  );
}
