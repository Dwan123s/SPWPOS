import { Select, Slot, nodeList } from "./react-ui.jsx";
import {
  state,
  button,
  badge,
  icon,
  riskLabels,
  reasons,
  typeLabels,
  dateFmt,
  timeFmt,
  dayKey,
  minutes,
  stat,
  field,
  empty,
  modal,
  loadLabel,
} from "./ui.jsx";
export function categoryModal(category = {}) {
  modal(
    category.id ? "Chỉnh sửa danh mục" : "Thêm danh mục công việc",
    "Dùng danh mục để lọc và tổ chức công việc.",
    <form data-form="category" data-id={category.id || ""}>
      {field(
        "Tên danh mục",
        "name",
        "text",
        category.name || "",
        'required maxlength="80"',
      )}
      <div className="spaced">
        {field(
          "Màu",
          "color",
          "color",
          category.color || "#6366f1",
          "required",
        )}
      </div>
      <div className="form-error" />
      <div className="form-actions">
        <button type="submit" className="btn primary">
          Lưu danh mục
        </button>
      </div>
    </form>,
  );
}
export function adminUsers(users) {
  return (
    <table>
      <thead>
        <tr>
          <th>Người dùng</th>
          <th>Email</th>
          <th>Trạng thái</th>
          <th>Hoạt động tài khoản</th>
          <th>Thao tác</th>
        </tr>
      </thead>
      <tbody>
        {nodeList(
          users.map((u) => (
            <tr>
              <td>
                {u.firstName} {u.lastName}
                {u.userName && <small className="muted"> @{u.userName}</small>}
              </td>
              <td>{u.email}</td>
              <td>
                {badge(
                  u.isActive ? "Hoạt động" : "Đã khóa",
                  u.isActive ? "green" : "red",
                )}{" "}
                {badge(u.emailConfirmed ? "Đã xác minh" : "Chưa xác minh")}
              </td>
              <td>
                <div>
                  {u.lastLoginAt
                    ? `Đăng nhập: ${dateFmt(u.lastLoginAt)} ${timeFmt(u.lastLoginAt)}`
                    : "Chưa đăng nhập"}
                </div>
                {u.createdAt && (
                  <small className="muted">Tạo: {dateFmt(u.createdAt)}</small>
                )}
                <div>
                  <small>{u.activeLoginSessions ?? 0} phiên đăng nhập</small>
                </div>
              </td>
              <td>
                {button(
                  "Lịch sử",
                  "admin-account-history",
                  "small ghost",
                  u.id,
                )}
                {u.id !== state.workspace.profile.id
                  ? button(
                      u.isActive ? "Khóa" : "Mở khóa",
                      u.isActive ? "admin-lock" : "admin-unlock",
                      "small ghost",
                      u.id,
                      "lock",
                    )
                  : badge("Bạn")}
              </td>
            </tr>
          )),
        )}
        {!users.length && (
          <tr>
            <td colSpan="5">Không tìm thấy tài khoản phù hợp.</td>
          </tr>
        )}
      </tbody>
    </table>
  );
}
export function adminStatus(s) {
  return (
    <>
      {stat(
        "Database",
        s.databaseHealthy ? "Hoạt động" : "Không khả dụng",
        `Máy chủ: ${timeFmt(s.serverTime)}`,
        "shield",
        "green",
      )}
      {stat(
        "Người dùng",
        s.users,
        `${s.activeUsersToday ?? 0} tài khoản đăng nhập hôm nay (UTC)`,
        "users",
      )}
      {stat(
        "Phiên đang hoạt động",
        s.activeSessions,
        `${s.failedLoginsLast24Hours} đăng nhập thất bại / 24h`,
        "clock",
      )}
      {stat(
        "Phiên đã hoàn thành",
        s.completedSessions ?? 0,
        "Số liệu tổng hợp, không xem nội dung công việc",
        "check",
      )}
      {stat(
        "Token AI",
        (s.inputTokens ?? 0) + (s.outputTokens ?? 0),
        s.inputTokens == null && s.outputTokens == null
          ? "Provider chưa cung cấp số token"
          : "Token đầu vào và đầu ra do provider báo cáo",
        "spark",
      )}
      {stat(
        "RAM tiến trình",
        `${Math.round((s.processMemoryBytes ?? 0) / 1048576)} MB`,
        `${s.failedJobs ?? 0} tác vụ lỗi`,
        "sliders",
      )}
      {s.health?.ai && s.health?.email && (
        <p className="muted">
          AI: {s.health.ai.configured ? "Đã cấu hình" : "Chưa cấu hình"}
          {s.health.ai.lastObservedLatencyMilliseconds != null
            ? ` · lần thành công gần nhất ${s.health.ai.lastObservedLatencyMilliseconds} ms`
            : " · chưa có số đo"}
          . SMTP: {s.health.email.configured ? "Đã cấu hình" : "Chưa cấu hình"}.
          Các trạng thái này không phải phép đo kết nối tức thời.
        </p>
      )}
      {stat(
        "Thông báo định kỳ",
        s.lastNotificationRun ? timeFmt(s.lastNotificationRun) : "Chưa chạy",
        `${s.failedNotificationRuns} lần lỗi`,
        "bell",
      )}
    </>
  );
}
