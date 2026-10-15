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
          <th>Thao tác</th>
        </tr>
      </thead>
      <tbody>
        {nodeList(
          users.map((u) => (
            <tr>
              <td>
                {u.firstName} {u.lastName}
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
        `${s.tasks} công việc trong hệ thống`,
        "users",
      )}
      {stat(
        "Phiên đang hoạt động",
        s.activeSessions,
        `${s.failedLoginsLast24Hours} đăng nhập thất bại / 24h`,
        "clock",
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
