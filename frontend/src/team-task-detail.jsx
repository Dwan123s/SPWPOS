import { createPortal } from "react-dom";
import { icon, dateFmt, timeFmt } from "./ui.jsx";
import { TeamDialog } from "./team-dialog.jsx";
import { CompletionReport } from "./team-completion-files.jsx";
import { TaskRequirements, TaskSourceFiles } from "./team-task-notes.jsx";
import { TeamRecipientEditor } from "./team-recipient-select.jsx";
import { TaskEisenhowerDetail } from "./eisenhower.jsx";

const statusLabels = {
  Open: "Chờ nhận",
  Assigned: "Đang thực hiện",
  PendingReview: "Chờ trưởng nhóm duyệt",
  Completed: "Đã hoàn thành",
};

export function TeamTaskDetail({
  task,
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
  onImportanceChanged,
}) {
  const mine = task.assigneeId === uid;
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
      <div className="team-task-dialog-content">
        <div className="team-task-dialog-badges">
          <span className={`team-detail-status status-${task.status}`}>
            {statusLabels[task.status] || task.status}
          </span>
          <span className={`team-priority priority-${task.priority}`}>
            {task.priority === 4
              ? "Khẩn cấp"
              : task.priority === 3
                ? "Ưu tiên cao"
                : task.priority === 1
                  ? "Linh hoạt"
                  : "Thông thường"}
          </span>
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
            <span>{icon("clock")} Thời lượng</span>
            <strong>{task.estimatedMinutes} phút</strong>
          </div>
          <div>
            <span>{icon("users")} Người thực hiện</span>
            <strong>
              {task.assigneeId
                ? mine
                  ? "Bạn"
                  : assignee?.name || "Thành viên"
                : task.designatedUserId
                  ? `${task.designatedUserId === uid ? "Bạn" : designated?.name || "Thành viên được chỉ định"} · Chờ nhận`
                  : "Chưa có người nhận"}
            </strong>
          </div>
        </div>
        {task.designatedUserId && !task.assigneeId && (
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
          onRecipientChange && (
            <TeamRecipientEditor
              key={task.version}
              task={task}
              members={members}
              busy={busy}
              onSave={onRecipientChange}
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
        <TaskEisenhowerDetail task={task} team leader={leader} onChanged={onImportanceChanged} />
        <TaskRequirements task={task} />
        <TaskSourceFiles
          task={task}
          leader={leader}
          busy={busy}
          onAttach={
            onAttachSource ? (file) => onAttachSource(task, file) : undefined
          }
        />
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
        <CompletionReport task={task} />
        {!task.assigneeId && (
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
        {error && (
          <p className="team-error" role="alert">
            {icon("alertCircle")}
            {error}
          </p>
        )}
      </div>
      <footer className="team-task-dialog-actions">
        {leader && !["Completed", "PendingReview"].includes(task.status) && (
          <button
            className="team-text-button"
            disabled={busy}
            onClick={() => handoff(onChange, task)}
          >
            Đề xuất đổi yêu cầu
          </button>
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
          {!task.assigneeId &&
            task.status === "Open" &&
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
