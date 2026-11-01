import { useEffect, useRef, useState } from "react";
import { icon, dateFmt, timeFmt } from "./ui.jsx";
import { readTeamBriefFile, TEAM_BRIEF_ACCEPT } from "./team-brief-file.js";
import { CompletionReport } from "./team-completion-files.jsx";

export function TeamDialog({
  title,
  subtitle,
  children,
  onClose,
  busy,
  wide = false,
}) {
  const host = useRef(null);
  useEffect(() => {
    const el = host.current;
    el.showModal();
    return () => {
      if (el.open) el.close();
    };
  }, []);
  return (
    <dialog
      ref={host}
      className={`team-dialog${wide ? " team-dialog-wide" : ""}`}
      aria-label={title}
      onCancel={(e) => {
        e.preventDefault();
        if (!busy) onClose();
      }}
    >
      <header className="team-dialog-header">
        <div>
          <span className="team-eyebrow">PLANORA WORKSPACE</span>
          <h2>{title}</h2>
          {subtitle && <p>{subtitle}</p>}
        </div>
        <button
          type="button"
          className="team-icon-button"
          aria-label="Đóng cửa sổ"
          disabled={busy}
          onClick={onClose}
        >
          {icon("close")}
        </button>
      </header>
      {children}
    </dialog>
  );
}
export function GroupHub({
  teams,
  selected,
  busy,
  loading,
  error,
  onDialogChange,
  onSelect,
  onCreate,
  onJoin,
  onCreateLink,
}) {
  const [mode, setMode] = useState(null),
    [value, setValue] = useState("");
  useEffect(() => {
    onDialogChange?.(!!mode);
  }, [mode, onDialogChange]);
  const open = (next) => {
    setValue(
      next === "invite" ? teams.find((t) => t.role === "Leader")?.id || "" : "",
    );
    setMode(next);
  };
  return (
    <>
      {!selected && (
        <section className="team-hub-hero">
          <div className="team-hub-intro">
            <span className="team-eyebrow">CỘNG TÁC CÙNG NORA</span>
            <h2>
              Cùng một mục tiêu.
              <br />
              <span>Mỗi người một lịch phù hợp.</span>
            </h2>
            <p>
              Giao việc cho cả nhóm. Để NORA tìm thời gian thực hiện phù hợp với
              lịch học, công việc và deadline của từng thành viên.
            </p>
            <div className="team-actions">
              <button
                className="btn primary"
                disabled={busy}
                onClick={() => open("create")}
              >
                {icon("plus")} Tạo nhóm mới
              </button>
              <button
                className="btn secondary"
                disabled={busy}
                onClick={() => open("join")}
              >
                {icon("users")} Tham gia bằng link hoặc mã
              </button>
              {teams.some((t) => t.role === "Leader") && (
                <button
                  className="btn secondary"
                  disabled={busy}
                  onClick={() => open("invite")}
                >
                  {icon("copy")} Tạo link mời
                </button>
              )}
            </div>
            <span className="team-privacy-caption">
              {icon("shield")} Nhóm thấy tiến độ. Lịch cá nhân chỉ bạn thấy.
            </span>
          </div>
          <div
            className="team-flow-visual"
            aria-label="Leader giao việc, thành viên nhận task, NORA xếp lịch riêng"
          >
            <div className="team-flow-orbit" />
            <div className="team-flow-card flow-task">
              <span className="team-tile-icon">{icon("tasks")}</span>
              <div>
                <strong>Task từ nhóm</strong>
                <small>Một mục tiêu, một deadline</small>
              </div>
            </div>
            <div className="team-flow-card flow-ai">
              <span className="team-tile-icon">{icon("spark")}</span>
              <div>
                <strong>NORA phân tích</strong>
                <small>Dựa trên lịch của chính bạn</small>
              </div>
              <i>AI</i>
            </div>
            <div className="team-flow-card flow-calendar">
              <span className="team-tile-icon">{icon("calendar")}</span>
              <div>
                <strong>Lịch làm việc riêng</strong>
                <small>Bạn xem và xác nhận</small>
              </div>
              <span className="team-flow-check">{icon("check")}</span>
            </div>
            <span className="team-flow-dot dot-one" />
            <span className="team-flow-dot dot-two" />
          </div>
        </section>
      )}
      <section className={`team-group-section${selected ? " compact" : ""}`}>
        <div className="team-section-heading">
          <div>
            <h2>
              Nhóm của bạn <span className="team-count">{teams.length}</span>
            </h2>
            <p>
              {teams.length
                ? "Chọn không gian làm việc để theo dõi và nhận task."
                : "Bắt đầu bằng một nhóm mới hoặc mã mời từ trưởng nhóm."}
            </p>
          </div>
          {selected && (
            <div className="team-actions">
              <button
                className="btn secondary"
                disabled={busy}
                onClick={() => open("join")}
              >
                Nhập mã mời
              </button>
              <button
                className="btn primary"
                disabled={busy}
                onClick={() => open("create")}
              >
                {icon("plus")} Tạo nhóm
              </button>
            </div>
          )}
        </div>
        {loading ? (
          <p role="status" className="team-loading">
            Đang tải các nhóm của bạn…
          </p>
        ) : teams.length ? (
          <div className="team-group-grid">
            {teams.map((t, i) => (
              <button
                key={t.id}
                className={`team-group-card${selected === t.id ? " selected" : ""}`}
                aria-label={t.name}
                aria-pressed={selected === t.id}
                disabled={busy}
                onClick={() => onSelect(t.id)}
              >
                <span className={`team-group-symbol tone-${i % 3}`}>
                  {icon("folder")}
                </span>
                <span>
                  <strong>{t.name}</strong>
                  <small>
                    {t.role === "Leader"
                      ? "Bạn là trưởng nhóm"
                      : "Không gian thành viên"}
                  </small>
                </span>
                <span className="team-group-arrow">
                  {icon(selected === t.id ? "check" : "arrow")}
                </span>
              </button>
            ))}
          </div>
        ) : (
          <div className="team-start-guide">
            <article>
              <span className="team-step-number">01</span>
              <div>
                <h3>Tạo hoặc tham gia nhóm</h3>
                <p>Kết nối các thành viên trong cùng một không gian đồ án.</p>
              </div>
            </article>
            <article>
              <span className="team-step-number">02</span>
              <div>
                <h3>Giao việc, cùng nhận task</h3>
                <p>Task có nội dung, thời lượng và deadline rõ ràng.</p>
              </div>
            </article>
            <article>
              <span className="team-step-number">03</span>
              <div>
                <h3>NORA sắp xếp lịch riêng</h3>
                <p>Mỗi người nhận một phương án theo lịch cá nhân của mình.</p>
              </div>
            </article>
          </div>
        )}
      </section>
      {mode && (
        <TeamDialog
          title={
            mode === "create"
              ? "Tạo nhóm đồ án"
              : mode === "invite"
                ? "Tạo link mời vào nhóm"
                : "Tham gia nhóm"
          }
          subtitle={
            mode === "create"
              ? "Một không gian chung cho task, thành viên và trao đổi."
              : mode === "invite"
                ? "Chọn nhóm bạn quản lý để tạo link chia sẻ."
                : "Dán mã hoặc link mời được trưởng nhóm chia sẻ."
          }
          busy={busy}
          onClose={() => setMode(null)}
        >
          {error && (
            <p className="team-error" role="alert">
              {error}
            </p>
          )}
          <form
            className="team-form"
            onSubmit={async (e) => {
              e.preventDefault();
              const ok = await (mode === "create"
                ? onCreate(value.trim())
                : mode === "invite"
                  ? onCreateLink(value)
                  : onJoin(value.trim()));
              if (ok) setMode(null);
            }}
          >
            <label className="field">
              {mode === "create"
                ? "Tên nhóm mới"
                : mode === "invite"
                  ? "Nhóm cần mời thành viên"
                  : "Mã mời hoặc link tham gia"}
              {mode === "invite" ? (
                <select
                  value={value}
                  required
                  disabled={busy}
                  onChange={(e) => setValue(e.target.value)}
                >
                  {teams
                    .filter((t) => t.role === "Leader")
                    .map((t) => (
                      <option key={t.id} value={t.id}>
                        {t.name}
                      </option>
                    ))}
                </select>
              ) : (
                <input
                  autoFocus
                  required
                  maxLength={mode === "create" ? 120 : 2000}
                  value={value}
                  onChange={(e) => setValue(e.target.value)}
                  placeholder={
                    mode === "create"
                      ? "Ví dụ: SWP391 · Nhóm 4"
                      : "Dán link hoặc mã mời của nhóm"
                  }
                />
              )}
            </label>
            <div className="team-dialog-note">
              {icon(mode === "create" ? "users" : "shield")}
              <p>
                {mode === "create"
                  ? "Bạn sẽ là trưởng nhóm và có thể mời thành viên, giao task, quản lý deadline."
                  : mode === "invite"
                    ? "Link mời có hạn dùng và có thể thu hồi. Chỉ trưởng nhóm được tạo link cho nhóm mình quản lý."
                    : "Bạn sẽ thấy task và chat chung. Lịch riêng của các thành viên luôn được giữ riêng."}
              </p>
            </div>
            <div className="team-actions end">
              <button
                type="button"
                className="btn secondary"
                disabled={busy}
                onClick={() => setMode(null)}
              >
                Để sau
              </button>
              <button className="btn primary" disabled={busy || !value.trim()}>
                {busy
                  ? "Đang xử lý…"
                  : mode === "create"
                    ? "Tạo nhóm"
                    : mode === "invite"
                      ? "Tạo link mời"
                      : "Tham gia nhóm"}
              </button>
            </div>
          </form>
        </TeamDialog>
      )}
    </>
  );
}
export function TeamHeader({ detail, busy, onInvite, onRefresh, onCompose }) {
  const { team, members, tasks } = detail;
  const complete = tasks.filter((t) => t.status === "Completed").length;
  const metrics = [
    {
      label: "Tổng công việc",
      value: tasks.length,
      glyph: "tasks",
      tone: "blue",
    },
    {
      label: "Đang chờ nhận",
      value: tasks.filter((t) => !t.assigneeId).length,
      glyph: "clock",
      tone: "amber",
    },
    {
      label: "Đang thực hiện",
      value: tasks.filter((t) => t.status === "Assigned").length,
      glyph: "play",
      tone: "violet",
    },
    {
      label: "Chờ trưởng nhóm duyệt",
      value: tasks.filter((t) => t.status === "PendingReview").length,
      glyph: "clock",
      tone: "amber",
    },
    { label: "Đã hoàn thành", value: complete, glyph: "check", tone: "green" },
  ];
  return (
    <>
      <header className="team-workspace-header">
        <div className="team-title-block">
          <span className="team-group-symbol">{icon("folder")}</span>
          <div>
            <div className="team-eyebrow">
              KHÔNG GIAN NHÓM{" "}
              <span className="team-role-pill">
                {team.role === "Leader" ? "Trưởng nhóm" : "Thành viên"}
              </span>
            </div>
            <h2>{team.name}</h2>
            <p>
              {members.length} thành viên · {complete}/{tasks.length} công việc
              hoàn thành
            </p>
          </div>
        </div>
        <div className="team-actions">
          <button
            className="team-icon-button"
            title="Tải lại nhóm"
            aria-label="Tải lại nhóm"
            disabled={busy}
            onClick={onRefresh}
          >
            {icon("refresh")}
          </button>
          {team.role === "Leader" && (
            <button
              className="btn secondary"
              disabled={busy}
              onClick={onInvite}
            >
              {icon("users")} Mời thành viên
            </button>
          )}
          {team.role === "Leader" && (
            <button className="btn primary" disabled={busy} onClick={onCompose}>
              {icon("plus")} Giao task mới
            </button>
          )}
        </div>
      </header>
      <div className="team-metrics">
        {metrics.map((m) => (
          <div className={`team-metric ${m.tone}`} key={m.label}>
            <span className="team-metric-icon">{icon(m.glyph)}</span>
            <div>
              <strong>{m.value}</strong>
              <span>{m.label}</span>
            </div>
          </div>
        ))}
      </div>
    </>
  );
}
function MemberAvatar({ member, tone }) {
  const [failedSrc, setFailedSrc] = useState(null);
  return (
    <span className={`team-member-avatar tone-${tone}`}>
      {member.avatarUrl && failedSrc !== member.avatarUrl ? (
        <img
          src={member.avatarUrl}
          alt={`Ảnh đại diện của ${member.name}`}
          loading="lazy"
          decoding="async"
          onError={() => setFailedSrc(member.avatarUrl)}
        />
      ) : (
        <span aria-label={`Ảnh đại diện của ${member.name}`}>
          {(member.name || "Thành viên")
            .trim()
            .split(/\s+/)
            .slice(-2)
            .map((n) => n[0])
            .join("")
            .toLocaleUpperCase("vi")}
        </span>
      )}
    </span>
  );
}
export function TeamMembers({ detail, uid, busy, onRemove }) {
  return (
    <section className="team-panel team-members">
      <div className="team-panel-heading">
        <h3>Thành viên</h3>
        <span className="team-count">{detail.members.length}</span>
      </div>
      <div className="team-member-list">
        {detail.members.map((m, i) => (
          <div className="team-member" key={m.userId}>
            <MemberAvatar member={m} tone={i % 3} />
            <div>
              <strong>
                {m.name}
                {m.userId === uid ? " (bạn)" : ""}
              </strong>
              <small>
                {m.role === "Leader" ? "Trưởng nhóm" : "Thành viên"}
              </small>
            </div>
            {m.role !== "Leader" &&
              (detail.team.role === "Leader" || m.userId === uid) && (
                <button
                  className="team-text-button"
                  disabled={busy}
                  onClick={() => onRemove(m)}
                >
                  {m.userId === uid ? "Rời nhóm" : "Gỡ"}
                </button>
              )}
          </div>
        ))}
      </div>
      <p className="team-private-note">
        {icon("lock")} Lịch và giờ rảnh của thành viên được giữ riêng.
      </p>
    </section>
  );
}
export function TaskBoard({
  detail,
  uid,
  busy,
  onClaim,
  onProgress,
  onReviewCompletion,
  onReturn,
  onChange,
  onReject,
  onCompose,
}) {
  const [filter, setFilter] = useState("all"),
    [query, setQuery] = useState("");
  const tasks = detail.tasks.filter(
    (t) =>
      (filter !== "mine" || t.assigneeId === uid) &&
      (filter !== "available" || !t.assigneeId) &&
      t.name.toLocaleLowerCase("vi").includes(query.toLocaleLowerCase("vi")),
  );
  const columns = [
    { key: "Open", label: "Chờ nhận", tone: "open", glyph: "clock" },
    { key: "Assigned", label: "Đang thực hiện", tone: "active", glyph: "play" },
    {
      key: "PendingReview",
      label: "Chờ duyệt",
      tone: "review",
      glyph: "clock",
    },
    { key: "Completed", label: "Hoàn thành", tone: "done", glyph: "check" },
  ];
  return (
    <section className="team-board" aria-label="Công việc nhóm">
      <div className="team-board-heading">
        <div>
          <span className="team-eyebrow">CÔNG VIỆC CHUNG</span>
          <h3>Bảng công việc</h3>
        </div>
        <label className="team-search">
          {icon("search")}
          <input
            aria-label="Tìm task trong nhóm"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Tìm công việc…"
          />
        </label>
      </div>
      <div className="team-board-tabs" aria-label="Lọc công việc">
        {[
          { id: "all", name: "Tất cả" },
          { id: "available", name: "Có thể nhận" },
          { id: "mine", name: "Việc của tôi" },
        ].map((t) => (
          <button
            key={t.id}
            type="button"
            className={filter === t.id ? "active" : ""}
            aria-pressed={filter === t.id}
            onClick={() => setFilter(t.id)}
          >
            {t.name}
            {t.id === "mine" && (
              <span>
                {detail.tasks.filter((x) => x.assigneeId === uid).length}
              </span>
            )}
          </button>
        ))}
      </div>
      {!detail.tasks.length ? (
        <div className="team-board-empty">
          <span>{icon("tasks")}</span>
          <h3>Công việc đầu tiên đang chờ bạn</h3>
          <p>
            {detail.team.role === "Leader"
              ? "Mô tả việc cần làm và hạn chót. NORA sẽ soạn task để bạn xác nhận trước khi giao cho nhóm."
              : "Trưởng nhóm sẽ giao công việc tại đây. Bạn có thể nhận task và để NORA sắp xếp theo lịch riêng."}
          </p>
          {detail.team.role === "Leader" && (
            <button className="btn primary" disabled={busy} onClick={onCompose}>
              {icon("spark")} Tạo task cùng NORA
            </button>
          )}
        </div>
      ) : (
        <div className="team-board-columns">
          {columns.map((column) => (
            <section
              className={`team-board-column ${column.tone}`}
              key={column.key}
            >
              <div className="team-column-heading">
                <span className="team-status-dot" />
                <h4>{column.label}</h4>
                <span>
                  {tasks.filter((t) => t.status === column.key).length}
                </span>
              </div>
              <div className="team-column-cards">
                {tasks
                  .filter((t) => t.status === column.key)
                  .map((task) => {
                    const mine = task.assigneeId === uid,
                      assignee = detail.members.find(
                        (m) => m.userId === task.assigneeId,
                      );
                    return (
                      <article className="team-task-card" key={task.id}>
                        <div className="team-task-topline">
                          <span
                            className={`team-priority priority-${task.priority}`}
                          >
                            {task.priority === 4
                              ? "Khẩn cấp"
                              : task.priority === 3
                                ? "Ưu tiên cao"
                                : task.priority === 1
                                  ? "Linh hoạt"
                                  : "Thông thường"}
                          </span>
                          {task.hasPendingChange && (
                            <span className="team-change-badge">
                              Có yêu cầu mới
                            </span>
                          )}
                        </div>
                        <h4>{task.name}</h4>
                        <p className="team-task-description">
                          {task.description}
                        </p>
                        {task.description && (
                          <details className="team-task-brief-details">
                            <summary>Xem đầy đủ yêu cầu & ghi chú</summary>
                            <div>{task.description}</div>
                          </details>
                        )}
                        <div className="team-task-facts">
                          <span>
                            {icon("calendar")}
                            {dateFmt(task.deadline)} · {timeFmt(task.deadline)}
                          </span>
                          <span>
                            {icon("clock")}
                            {task.estimatedMinutes} phút
                            {task.splittable
                              ? " · Có thể chia phiên"
                              : " · Làm liên tục"}
                          </span>
                        </div>
                        {task.assigneeId && (
                          <div className="team-task-progress">
                            <div>
                              <span>
                                {mine
                                  ? "Tiến độ của bạn"
                                  : assignee?.name || "Thành viên"}
                              </span>
                              <strong>{task.progress}%</strong>
                            </div>
                            <div className="team-progress-track">
                              <i style={{ width: `${task.progress}%` }} />
                            </div>
                          </div>
                        )}
                        {task.status === "PendingReview" && (
                          <div className="team-review-state">
                            <strong>
                              {icon("clock")} Chờ trưởng nhóm xác nhận
                            </strong>
                            <p>
                              {assignee?.name || "Thành viên"} đã báo hoàn thành
                              {task.completionSubmittedAt
                                ? ` lúc ${timeFmt(task.completionSubmittedAt)} · ${dateFmt(task.completionSubmittedAt)}`
                                : ""}
                              .
                            </p>
                            {detail.team.role === "Leader" && (
                              <button
                                className="btn primary"
                                disabled={busy}
                                onClick={() => onReviewCompletion(task)}
                              >
                                Kiểm tra & duyệt
                              </button>
                            )}
                          </div>
                        )}
                        {task.status === "Assigned" &&
                          task.completionApproved === false && (
                            <div className="team-review-state changes-requested">
                              <strong>Cần bổ sung trước khi hoàn thành</strong>
                              <p>
                                {task.completionReviewNote ||
                                  "Trưởng nhóm yêu cầu kiểm tra lại công việc. Trao đổi trong chat và gửi duyệt lại khi xong."}
                              </p>
                            </div>
                          )}
                        {task.status === "Completed" &&
                          task.completionApproved === true && (
                            <p className="team-approved-caption">
                              {icon("shield")} Đã được{" "}
                              {detail.members.find(
                                (m) => m.userId === task.completionReviewedBy,
                              )?.name || "trưởng nhóm"}{" "}
                              duyệt
                            </p>
                          )}
                        <CompletionReport task={task} />
                        <div className="team-task-actions">
                          {!task.assigneeId && (
                            <>
                              <p>
                                {icon("lock")} NORA xếp theo lịch riêng của bạn
                              </p>
                              <button
                                className="btn primary"
                                disabled={busy}
                                onClick={() => onClaim(task)}
                              >
                                {icon("spark")} Nhận task · NORA xếp lịch
                              </button>
                            </>
                          )}
                          {mine && task.status === "Assigned" && (
                            <>
                              <button
                                className="btn primary"
                                disabled={busy}
                                onClick={() => onProgress(task, 100)}
                              >
                                {icon("check")} Báo hoàn thành · Gửi duyệt
                              </button>
                              <details className="team-task-options">
                                <summary>Cập nhật tiến độ / trả việc</summary>
                                <div>
                                  {[25, 50, 75]
                                    .filter((p) => p > task.progress)
                                    .map((p) => (
                                      <button
                                        className="btn secondary"
                                        disabled={busy}
                                        key={p}
                                        onClick={() => onProgress(task, p)}
                                      >
                                        Xác nhận {p}%
                                      </button>
                                    ))}
                                  <button
                                    className="team-text-button"
                                    disabled={busy}
                                    onClick={() => onReturn(task)}
                                  >
                                    Trả việc về nhóm
                                  </button>
                                </div>
                              </details>
                            </>
                          )}
                          {task.assigneeId &&
                            !mine &&
                            task.status === "Assigned" && (
                              <span className="team-assigned-caption">
                                {icon("users")} {assignee?.name || "Thành viên"}{" "}
                                đã nhận việc
                              </span>
                            )}
                          {mine && task.hasPendingChange && (
                            <div className="team-change-actions">
                              <p>
                                Trưởng nhóm đề xuất thay đổi yêu cầu. Lịch hiện
                                tại vẫn giữ nguyên.
                              </p>
                              <button
                                className="btn secondary"
                                disabled={busy}
                                onClick={() => onClaim(task, true)}
                              >
                                Xem lịch cho yêu cầu mới
                              </button>
                              <button
                                className="team-text-button"
                                disabled={busy}
                                onClick={() => onReject(task)}
                              >
                                Giữ yêu cầu hiện tại
                              </button>
                            </div>
                          )}
                          {detail.team.role === "Leader" &&
                            !["Completed", "PendingReview"].includes(
                              task.status,
                            ) && (
                              <button
                                className="team-text-button team-edit-task"
                                disabled={busy}
                                onClick={() => onChange(task)}
                              >
                                Đề xuất đổi yêu cầu {icon("arrow")}
                              </button>
                            )}
                        </div>
                      </article>
                    );
                  })}
                {!tasks.some((t) => t.status === column.key) && (
                  <div className="team-column-empty">
                    {icon(column.glyph)}
                    <p>
                      {column.key === "Open"
                        ? "Chưa có task chờ nhận"
                        : column.key === "Assigned"
                          ? "Task được nhận sẽ xuất hiện ở đây"
                          : column.key === "PendingReview"
                            ? "Task được báo hoàn thành sẽ chờ duyệt ở đây"
                            : "Hoàn thành từng việc, tiến gần mục tiêu"}
                    </p>
                  </div>
                )}
              </div>
            </section>
          ))}
        </div>
      )}
    </section>
  );
}
export function TeamTaskComposer({
  text,
  setText,
  changeTask,
  draft,
  setDraft,
  busy,
  error,
  onDraft,
  onConfirm,
  onClose,
  file,
  setFile,
  splitBrief,
  setSplitBrief,
  memberCount = 1,
}) {
  const upload = useRef(null);
  const [fileError, setFileError] = useState("");
  const [reading, setReading] = useState(false);
  async function attachFile(selected) {
    if (!selected || busy || reading) return;
    setFileError("");
    setReading(true);
    try {
      setFile(await readTeamBriefFile(selected));
      setSplitBrief(true);
      setDraft(null);
    } catch (e) {
      setFileError(e.message);
    } finally {
      setReading(false);
      if (upload.current) upload.current.value = "";
    }
  }
  const previewTasks = draft?.tasks || (draft?.task ? [draft.task] : []);
  return (
    <TeamDialog
      title={changeTask ? "Đề xuất thay đổi công việc" : "Tạo task cùng NORA"}
      subtitle={
        changeTask
          ? changeTask.name
          : "Bạn mô tả công việc. NORA chuẩn bị phần còn lại."
      }
      busy={busy}
      onClose={onClose}
    >
      <div className="team-composer-content">
        {(error || fileError) && (
          <p className="team-error" role="alert">
            {fileError || error}
          </p>
        )}
        <div className="team-nora-caption">
          <span>{icon("spark")}</span>
          <p>
            {splitBrief
              ? `Nhóm hiện có ${memberCount} thành viên, gồm trưởng nhóm. NORA sẽ đọc yêu cầu và chia thành ${memberCount} gói việc có đầu ra rõ ràng để bạn xem lại.`
              : "Nêu việc cần làm và hạn chót. Nếu chưa có thời lượng, NORA sẽ ước lượng để bạn xác nhận."}
          </p>
        </div>
        <form
          className="team-form"
          onSubmit={(e) => {
            e.preventDefault();
            onDraft();
          }}
        >
          {!changeTask && (
            <>
              <div className="team-brief-modes" aria-label="Cách tạo công việc">
                <button
                  type="button"
                  className={!splitBrief ? "active" : ""}
                  aria-pressed={!splitBrief}
                  disabled={busy || reading}
                  onClick={() => {
                    setSplitBrief(false);
                    setFile(null);
                    setDraft(null);
                  }}
                >
                  Một task
                </button>
                <button
                  type="button"
                  className={splitBrief ? "active" : ""}
                  aria-pressed={!!splitBrief}
                  disabled={busy || reading}
                  onClick={() => {
                    setSplitBrief(true);
                    setDraft(null);
                  }}
                >
                  {icon("users")} Chia việc cho cả nhóm
                </button>
              </div>
              <input
                ref={upload}
                type="file"
                accept={TEAM_BRIEF_ACCEPT}
                className="sr-only"
                aria-label="Tải tài liệu công việc"
                disabled={busy || reading}
                onChange={(e) => attachFile(e.target.files?.[0])}
              />
              <div
                className={`team-brief-upload${file ? " has-file" : ""}`}
                onDragOver={(e) => e.preventDefault()}
                onDrop={(e) => {
                  e.preventDefault();
                  attachFile(e.dataTransfer.files?.[0]);
                }}
              >
                <span className="team-brief-file-icon">{icon("folder")}</span>
                <div>
                  <strong>
                    {file ? file.name : "Đính kèm tài liệu công việc"}
                  </strong>
                  <p>
                    {file
                      ? `${Math.max(1, Math.ceil(file.size / 1024))} KB · NORA sẽ đọc và chia việc theo nhóm`
                      : "Kéo thả PDF, Word, TXT hoặc Markdown · tối đa 8 MB"}
                  </p>
                </div>
                <button
                  type="button"
                  className="btn secondary"
                  disabled={busy || reading}
                  onClick={() => upload.current?.click()}
                >
                  {reading ? "Đang đọc…" : file ? "Đổi file" : "Chọn file"}
                </button>
                {file && (
                  <button
                    type="button"
                    className="team-icon-button"
                    aria-label="Bỏ tài liệu đính kèm"
                    disabled={busy || reading}
                    onClick={() => {
                      setFile(null);
                      setDraft(null);
                    }}
                  >
                    {icon("close")}
                  </button>
                )}
              </div>
            </>
          )}
          <label className="field">
            {file
              ? "Bổ sung yêu cầu hoặc làm rõ deadline (nếu cần)"
              : "Mô tả công việc và hạn chót"}
            <textarea
              autoFocus
              required={!file}
              rows={4}
              maxLength={splitBrief ? 12000 : 2000}
              disabled={busy || reading}
              value={text}
              onChange={(e) => {
                setText(e.target.value);
                setDraft(null);
              }}
              placeholder="Ví dụ: Viết báo cáo kiểm thử cho đồ án, hạn 21h thứ Sáu. Hãy đề xuất thời lượng giúp tôi."
              onPaste={(e) => {
                if (!changeTask && e.clipboardData.files?.length) {
                  e.preventDefault();
                  attachFile(e.clipboardData.files[0]);
                }
              }}
            />
          </label>
          <div className="team-actions end">
            <button
              className="btn primary"
              disabled={busy || reading || (!text.trim() && !file)}
            >
              {icon("spark")}
              {busy
                ? splitBrief
                  ? "NORA đang phân tích & chia việc…"
                  : "NORA đang soạn…"
                : splitBrief
                  ? "NORA phân tích & chia việc"
                  : "NORA soạn bản nháp"}
            </button>
          </div>
        </form>
        {draft && (
          <div className="team-draft-preview">
            <span className="team-eyebrow">BẢN NHÁP · CHƯA GIAO VIỆC</span>
            <p>
              {draft.summary || draft.reply} {draft.question}
            </p>
            {draft.splitReason && <p>{draft.splitReason}</p>}
            {draft.memberCount && (
              <span className="team-status-pill">
                {icon("users")} {draft.memberCount} thành viên ·{" "}
                {previewTasks.length} gói việc
              </span>
            )}
            {draft.warnings?.length > 0 && (
              <ul className="team-brief-warnings">
                {draft.warnings.map((warning, index) => (
                  <li key={index}>{warning}</li>
                ))}
              </ul>
            )}
            {previewTasks.map((task, index) => (
              <article className="team-brief-task" key={task.id}>
                <h3>
                  {draft.tasks ? `${String(index + 1).padStart(2, "0")}. ` : ""}
                  {task.name}
                </h3>
                <div className="team-task-facts">
                  <span>
                    {icon("clock")}Thời lượng đề xuất: {task.estimatedMinutes}{" "}
                    phút
                  </span>
                  <span>
                    {icon("calendar")}Hạn {dateFmt(task.deadline)}{" "}
                    {timeFmt(task.deadline)}
                  </span>
                </div>
                <p className="team-brief-task-notes">{task.description}</p>
              </article>
            ))}
            {previewTasks.length > 0 && !draft.question && (
              <button
                className="btn primary"
                disabled={busy}
                onClick={onConfirm}
              >
                {changeTask
                  ? "Xác nhận gửi yêu cầu mới"
                  : draft.tasks
                    ? `Xác nhận & giao ${previewTasks.length} task cho nhóm`
                    : "Đồng ý thời lượng và tạo task chung"}
              </button>
            )}
          </div>
        )}
      </div>
    </TeamDialog>
  );
}
export function TeamActivity({ detail }) {
  const labels = {
    TEAM_CREATED: "đã tạo nhóm",
    MEMBER_JOINED: "đã tham gia nhóm",
    MEMBER_LEFT: "đã rời nhóm",
    TASK_CREATED: "đã tạo task",
    TASK_CLAIMED: "đã nhận task",
    TASK_RETURNED: "đã trả task",
    TASK_COMPLETED: "đã hoàn thành task",
    TASK_COMPLETION_SUBMITTED: "đã báo hoàn thành và gửi trưởng nhóm duyệt",
    TASK_COMPLETION_APPROVED: "đã duyệt hoàn thành task",
    TASK_COMPLETION_REJECTED: "đã yêu cầu bổ sung task",
    PROGRESS_CONFIRMED: "đã xác nhận tiến độ",
    CLAIM_UNDONE: "đã hoàn tác nhận task",
    TASK_CHANGE_PROPOSED: "đã đề xuất thay đổi",
    TASK_CHANGE_ACCEPTED: "đã chấp nhận thay đổi",
    TASK_CHANGE_REJECTED: "đã giữ yêu cầu hiện tại",
  };
  return (
    <section className="team-panel team-activity">
      <div className="team-panel-heading">
        <h3>Hoạt động gần đây</h3>
        {icon("clock")}
      </div>
      {detail.activity.length ? (
        <ol>
          {detail.activity.slice(0, 8).map((a, i) => (
            <li key={i}>
              <span className="team-activity-dot" />
              <div>
                <p>
                  <strong>
                    {detail.members.find((m) => m.userId === a.actorId)?.name ||
                      "Thành viên"}
                  </strong>{" "}
                  {labels[a.kind] || "đã cập nhật công việc"}
                </p>
                <time>
                  {dateFmt(a.createdAt)} · {timeFmt(a.createdAt)}
                </time>
              </div>
            </li>
          ))}
        </ol>
      ) : (
        <p className="team-muted">
          Các hoạt động giao việc và tiến độ sẽ xuất hiện ở đây.
        </p>
      )}
    </section>
  );
}
