import { createPortal } from "react-dom";
import { useState } from "react";
import { icon, dateFmt, timeFmt, minutes, state } from "./ui.jsx";
import { TeamDialog } from "./team-dialog.jsx";
import { CompletionReport } from "./team-completion-files.jsx";
import { TaskNoteSection, TaskSourceFiles, teamTaskNoteSections } from "./team-task-notes.jsx";
import { TeamRecipientEditor } from "./team-recipient-select.jsx";
import { TaskEisenhowerDetail } from "./eisenhower.jsx";
import { EisenhowerPriorityBadge } from "./eisenhower-priority.jsx";
import { TeamChecklist } from "./team-task-checklist.jsx";
import { TaskPlannedSessions, TaskPlanningPanel, TaskPriorityIndicators, TaskSessionControls } from "./task-workflow.jsx";

const statusLabels = {
  Open: "Chờ nhận",
  Assigned: "Đang thực hiện",
  PendingReview: "Chờ trưởng nhóm duyệt",
  Completed: "Đã hoàn thành",
  Split: "Đã chia thành việc con",
};

export function TeamTaskDetail({
  task,
  teamId,
  onExecutionChanged,
  selectedSessionId,
  initialSection = "overview",
  parent,
  childrenTasks = [],
  uid,
  busy,
  leader,
  assignee,
  designated,
  members = [],
  reviewer,
  error,
  onClose,
  onClaim,
  onProgress,
  onReviewCompletion,
  onReturn,
  onChange,
  onReject,
  onAttachSource,
  onRecipientChange,
  onRequestAvailability,
  onImportanceChanged,
  onOpenTask,
  onSplit,
  onChecklistToggle,
  awaitingDeadline,
  onExtendDeadline,
}) {
  const mine = task.assigneeId === uid;
  const personal = mine ? state.workspace?.tasks?.find(item =>
    item.teamId === teamId && item.teamTaskId === task.id) : null;
  const [section, setSection] = useState(initialSection);
  const notes = teamTaskNoteSections(task);
  const noteCategories = new Map(notes.filter((note) => note.title !== "Tài liệu nguồn")
    .map((note) => [note.title, note.glyph]));
  const categories = [
    ["overview", "Tổng quan", "tasks"],
    ...(personal ? [["sessions", "Lịch làm", "calendar"]] : []),
    ...Array.from(noteCategories, ([title, glyph]) => [`note:${title}`, title, glyph]),
    ...((mine || task.status === "Open") && task.checklist?.length ? [["checklist", "Checklist", "check"]] : []),
    ...(childrenTasks.length ? [["children", "Việc con", "tasks"]] : []),
    ["source", "Tài liệu nguồn", "folder"],
    ...(task.completionSubmissionNote || task.completionFiles?.length || task.completionReportUrl
      ? [["report", "Kết quả đã gửi", "folder"]] : []),
  ];
  const activeSection = categories.some(([id]) => id === section) ? section : "overview";
  // Close the task viewer before opening the scheduling, submission or review dialog.
  const handoff = (action, ...args) => {
    onClose();
    return action(...args);
  };
  return createPortal(
    <TeamDialog
      title={task.name}
      subtitle="Chi tiết công việc nhóm"
      onClose={onClose}
      busy={busy}
      className="team-task-dialog"
    >
      <nav className="team-task-detail-nav" aria-label="Danh mục chi tiết công việc">
        {categories.map(([id, title, glyph]) => (
          <button type="button" key={id} aria-pressed={activeSection === id} onClick={() => setSection(id)}>
            {icon(glyph)} {title}
          </button>
        ))}
      </nav>
      <div className="team-task-dialog-content" key={activeSection}>
        {activeSection === "overview" && <>
        <TaskPriorityIndicators task={personal || task} sessions={state.workspace?.sessions} project={task.status === "Split"}
          link={Boolean(personal)} onOpenPlan={() => setSection("sessions")} />
        {personal && task.status === "Assigned" && (
          <TaskSessionControls task={personal} onWorkspaceChanged={onExecutionChanged} selectedSessionId={selectedSessionId} />
        )}
        {parent && <button type="button" className="team-text-button" onClick={() => onOpenTask(parent)}>
          Việc gốc · {parent.name}
        </button>}
        <div className="team-task-dialog-badges">
          <span className={`team-detail-status status-${task.status}`}>
            {awaitingDeadline ? "Quá hạn · Chưa có người nhận" : statusLabels[task.status] || task.status}
          </span>
          <EisenhowerPriorityBadge task={task} />
          {task.hasPendingChange && (
            <span className="team-change-badge">Có yêu cầu mới</span>
          )}
        </div>
        <div className="team-task-dialog-facts">
          <div>
            <span>{icon("calendar")} Hạn hoàn thành</span>
            <strong>
              {dateFmt(task.deadline)} · {timeFmt(task.deadline)}
            </strong>
          </div>
          <div>
            <span>{icon("clock")} {task.status === "Split" ? "Việc con" : "Thời lượng"}</span>
            <strong>{task.status === "Split" ? `${task.subtasksCompleted}/${task.subtaskCount} đã hoàn thành` : minutes(task.estimatedMinutes, "long")}</strong>
          </div>
          <div>
            <span>{icon("users")} Người thực hiện</span>
            <strong>
              {task.assigneeId
                ? mine
                  ? "Bạn"
                  : assignee?.name || "Thành viên"
                : task.status === "Split" ? "Theo từng việc con" : task.designatedUserId
                  ? `${task.designatedUserId === uid ? "Bạn" : designated?.name || "Thành viên được chỉ định"} · Chờ nhận`
                  : "Chưa có người nhận"}
            </strong>
          </div>
        </div>
        {task.status !== "Split" && <div className="team-task-work-mode">
          {icon("clock")} {task.splittable ? "Có thể chia thành nhiều phiên làm việc" : "Thực hiện trong một phiên liên tục"}
        </div>}
        {awaitingDeadline && <p className="team-task-overdue-note">
          {icon("alertCircle")} {leader ? "Đặt hạn mới để thành viên có thể xem lịch và nhận việc." : "Chờ trưởng nhóm gia hạn."}
        </p>}
        {task.designatedUserId && !task.assigneeId && !awaitingDeadline && (
          <p className="team-designated-note">
            {icon("users")}
            {task.designatedUserId === uid
              ? "Task được giao đích danh cho bạn. Nhận task để NORA tìm lịch phù hợp."
              : `Task được giao đích danh cho ${designated?.name || "thành viên được chỉ định"}. Chỉ người này có thể nhận task.`}
          </p>
        )}
        {leader &&
          task.status === "Open" &&
          !task.assigneeId &&
          !awaitingDeadline &&
          onRecipientChange && (
            <TeamRecipientEditor
              key={task.version}
              task={task}
              members={members}
              busy={busy}
              onSave={onRecipientChange}
              onRequestAvailability={onRequestAvailability}
            />
          )}
        {task.assigneeId && (
          <div className="team-task-dialog-progress">
            <span>
              Tiến độ <strong>{task.progress}%</strong>
            </span>
            <div className="team-progress-track">
              <i style={{ width: `${task.progress}%` }} />
            </div>
          </div>
        )}
        {task.status !== "Split" && <TaskEisenhowerDetail task={task} team leader={leader} onChanged={onImportanceChanged} />}
        </>}
        {activeSection === "sessions" && personal && (
          <>
            <TaskPlanningPanel task={personal} onWorkspaceChanged={onExecutionChanged} />
            <TaskPlannedSessions sessions={(state.workspace.sessions || [])
              .filter(session => session.taskId === personal.id && session.status !== "Cancelled")
              .sort((a, b) => Date.parse(a.start) - Date.parse(b.start))} />
          </>
        )}
        {activeSection === "children" && (
          <section className="team-checklist" aria-label="Việc con">
            <h3>Việc con</h3>
            <ul>{childrenTasks.map((child) => (
              <li key={child.id}>
                <button type="button" className="team-text-button" onClick={() => onOpenTask(child)}>{child.name}</button>
                <small>{statusLabels[child.status]}</small>
              </li>
            ))}</ul>
          </section>
        )}
        {activeSection === "checklist" && (
          <TeamChecklist items={task.checklist} busy={busy}
            onToggle={mine && task.status === "Assigned" && onChecklistToggle
              ? (itemId, completed) => onChecklistToggle(task, itemId, completed) : undefined} />
        )}
        {notes.filter((note) => activeSection === `note:${note.title}` ||
          (activeSection === "source" && note.title === "Tài liệu nguồn"))
          .map((note, index) => <TaskNoteSection key={index} section={note} />)}
        {activeSection === "source" && <TaskSourceFiles
          task={task}
          leader={leader}
          busy={busy}
          onAttach={
            onAttachSource ? (file) => onAttachSource(task, file) : undefined
          }
        />}
        {activeSection === "report" && <CompletionReport task={task} />}
        {activeSection === "overview" && <>
        {task.status === "PendingReview" && (
          <p className="team-row-review-note">
            {assignee?.name || "Thành viên"} đã báo hoàn thành
            {task.completionSubmittedAt
              ? ` lúc ${timeFmt(task.completionSubmittedAt)} · ${dateFmt(task.completionSubmittedAt)}`
              : ""}
            . Đang chờ trưởng nhóm xác nhận.
          </p>
        )}
        {task.status === "Assigned" && task.completionApproved === false && (
          <div className="team-row-review-note">
            <strong>Cần bổ sung trước khi hoàn thành</strong>
            <p>
              {task.completionReviewNote ||
                "Trưởng nhóm yêu cầu kiểm tra lại công việc. Trao đổi trong chat và gửi duyệt lại khi xong."}
            </p>
          </div>
        )}
        {task.status === "Completed" && task.completionApproved === true && (
          <p className="team-approved-caption">
            {icon("shield")} Đã được {reviewer?.name || "trưởng nhóm"} duyệt
          </p>
        )}
        {!task.assigneeId && task.status === "Open" && !awaitingDeadline && (
          <p className="team-task-dialog-privacy">
            {icon("lock")} Khi nhận task, NORA sẽ xếp theo lịch riêng của bạn.
          </p>
        )}
        {mine && task.status === "Assigned" && (
          <div className="team-row-secondary-actions">
            <span>Cập nhật tiến độ</span>
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
              onClick={() => handoff(onReturn, task)}
            >
              Trả việc về nhóm
            </button>
          </div>
        )}
        {mine && task.hasPendingChange && (
          <div className="team-change-actions">
            <p>
              Trưởng nhóm đề xuất thay đổi yêu cầu. Lịch hiện tại vẫn giữ
              nguyên.
            </p>
            <button
              className="btn secondary"
              disabled={busy}
              onClick={() => handoff(onClaim, task, true)}
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
        </>}
        {error && (
          <p className="team-error" role="alert">
            {icon("alertCircle")}
            {error}
          </p>
        )}
      </div>
      <footer className="team-task-dialog-actions">
        {leader && ["Open", "Assigned"].includes(task.status) && (
          <div>
            <button
              className="team-text-button"
              disabled={busy}
              onClick={() => handoff(onChange, task)}
            >
              Đề xuất đổi yêu cầu
            </button>
            {task.status === "Open" && !awaitingDeadline && !task.parentTaskId && onSplit && (
              <button type="button" className="btn secondary" disabled={busy} onClick={() => handoff(onSplit, task)}>
                Chia thành 2 việc con
              </button>
            )}
          </div>
        )}
        <div>
          <button
            type="button"
            className="btn secondary"
            disabled={busy}
            onClick={onClose}
          >
            Đóng
          </button>
          {leader && awaitingDeadline && onExtendDeadline && (
            <button type="button" className="btn primary" disabled={busy} onClick={() => handoff(onExtendDeadline, task)}>
              {icon("calendar")} Gia hạn
            </button>
          )}
          {!task.assigneeId &&
            task.status === "Open" &&
            !awaitingDeadline &&
            (!task.designatedUserId || task.designatedUserId === uid) && (
              <button
                className="btn primary"
                disabled={busy}
                onClick={() => handoff(onClaim, task)}
              >
                {icon("spark")} Xem lịch trước khi nhận
              </button>
            )}
          {mine && task.status === "Assigned" && (
            <button
              className="btn primary"
              disabled={busy}
              onClick={() => handoff(onProgress, task, 100)}
            >
              {icon("check")} Báo hoàn thành · Gửi duyệt
            </button>
          )}
          {leader && task.status === "PendingReview" && (
            <button
              className="btn primary"
              disabled={busy}
              onClick={() => handoff(onReviewCompletion, task)}
            >
              Kiểm tra & duyệt
            </button>
          )}
        </div>
      </footer>
    </TeamDialog>,
    document.body,
  );
}
