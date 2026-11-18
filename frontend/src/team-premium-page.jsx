import { TeamPlans } from "./team-plans.jsx";
import "./team-workspace.css";

export function TeamPremiumPage() {
  return (
    <div className="team-premium-page">
      <p>
        Nâng cấp tài khoản để tăng giới hạn thành viên cho tất cả nhóm do bạn
        tạo. Chọn gói sử dụng trong 1 tháng.
      </p>
      <TeamPlans />
    </div>
  );
}
