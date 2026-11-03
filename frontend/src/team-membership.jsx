import { TeamDialog } from "./team-dialog.jsx";

export function MyJoinRequests({ requests = [] }) {
  if (!requests.length) return null;
  const labels = {
    Pending: "Chờ trưởng nhóm duyệt",
    Rejected: "Đã từ chối",
    Removed: "Đã bị loại khỏi nhóm",
  };
  return (
    <section className="team-panel" aria-label="Yêu cầu tham gia của bạn">
      <h3>Yêu cầu tham gia của bạn</h3>
      {requests.map((request) => (
        <p key={request.teamId}>
          <strong>{request.teamName}</strong> ·{" "}
          {labels[request.status] || request.status}
        </p>
      ))}
      <p className="team-private-note">
        Bạn chỉ được xem công việc và chat chung sau khi trưởng nhóm duyệt.
      </p>
    </section>
  );
}

export function TeamJoinRequests({ detail, busy, onReview, sectionRef }) {
  if (detail.team.role !== "Leader") return null;
  const requests = detail.joinRequests || [];
  return (
    <section
      ref={sectionRef}
      tabIndex={-1}
      className="team-panel"
      aria-label="Duyệt yêu cầu tham gia"
    >
      <div className="team-panel-heading">
        <h3>Chờ duyệt tham gia</h3>
        <span className="team-count">{requests.length}</span>
      </div>
      {requests.length ? (
        requests.map((request) => (
          <div className="team-join-request" key={request.userId}>
            <strong>{request.name}</strong>
            <small>{request.email}</small>
            <div className="team-actions">
              <button
                className="btn primary"
                disabled={busy}
                onClick={() => onReview(request, true)}
                aria-label={`Duyệt ${request.name}`}
              >
                Duyệt
              </button>
              <button
                className="btn secondary"
                disabled={busy}
                onClick={() => onReview(request, false)}
                aria-label={`Từ chối ${request.name}`}
              >
                Từ chối
              </button>
            </div>
          </div>
        ))
      ) : (
        <p>Chưa có yêu cầu chờ duyệt.</p>
      )}
    </section>
  );
}

export function RemoveTeamMemberDialog({
  member,
  self,
  busy,
  error,
  onClose,
  onConfirm,
}) {
  return (
    <TeamDialog
      title={self ? "Rời nhóm" : "Loại thành viên khỏi nhóm"}
      busy={busy}
      onClose={onClose}
    >
      <p>
        {self
          ? "Bạn muốn rời nhóm này?"
          : `Bạn muốn loại ${member.name} khỏi nhóm? Người này sẽ mất quyền xem công việc, chat và tài liệu nhóm, đồng thời không thể dùng link mời để quay lại.`}
      </p>
      {!self && (
        <p>
          Công việc chưa hoàn tất sẽ trở về nhóm để giao lại. Lịch và bản sao
          công việc cá nhân của người đó được giữ nguyên.
        </p>
      )}
      {error && (
        <p className="team-error" role="alert">
          {error}
        </p>
      )}
      <div className="team-actions end">
        <button className="btn secondary" disabled={busy} onClick={onClose}>
          Hủy
        </button>
        <button className="btn primary" disabled={busy} onClick={onConfirm}>
          {self ? "Xác nhận rời nhóm" : "Xác nhận loại thành viên"}
        </button>
      </div>
    </TeamDialog>
  );
}
