import { useState } from "react";
import { TeamDialog } from "./team-dialog.jsx";
import { dateFmt, timeFmt, icon } from "./ui.jsx";

export function TeamRenewalNotice({ detail }) {
  const [open, setOpen] = useState(true);
  const renewal = detail.renewal;
  const suspended = renewal.status === "Suspended";
  const title = suspended
    ? "Nhóm đã bị vô hiệu hóa"
    : "Gia hạn Premium trong 3 ngày";
  const message = suspended
    ? "Premium đã hết hạn quá 3 ngày và nhóm đang vượt giới hạn thành viên. Chủ nhóm cần gia hạn gói phù hợp để mở lại nhóm."
    : `Premium đã hết hạn. Chủ nhóm cần gia hạn trước ${timeFmt(renewal.graceUntil)} ngày ${dateFmt(renewal.graceUntil)} để nhóm tiếp tục hoạt động.`;
  const action =
    detail.team.role === "Leader" ? (
      <a className="btn primary" href="#premium">
        Gia hạn Premium
      </a>
    ) : (
      <p>Hãy liên hệ chủ nhóm để gia hạn tài khoản Premium.</p>
    );
  return (
    <>
      <section className="team-panel team-renewal-notice" role="status">
        <h3>
          {icon("alertCircle")} {title}
        </h3>
        <p>{message}</p>
        {action}
      </section>
      {open && (
        <TeamDialog
          title={title}
          subtitle={detail.team.name}
          onClose={() => setOpen(false)}
        >
          <p>{message}</p>
          <div className="team-actions">
            {action}
            <button
              type="button"
              className="btn secondary"
              onClick={() => setOpen(false)}
            >
              Đã hiểu
            </button>
          </div>
        </TeamDialog>
      )}
    </>
  );
}
