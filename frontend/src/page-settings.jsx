import { Select, Slot, nodeList } from "./react-ui.jsx";
import { secretaryMode } from "./agent-handoff.js";
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
import { avatarPreview, avatarRemove } from "./interactive.jsx";
import { AccountPassword } from "./account-password.jsx";
import { GoogleAccountCard } from "./google-signin.jsx";
import { Loading } from "./loading.jsx";
import { NoraInventory } from "./nora-inventory.jsx";
import { GoogleCalendarCard } from "./google-calendar.jsx";
import { TelegramCard } from "./telegram.jsx";
import { NotificationSoundSettings } from "./notification-sound-settings.jsx";
export function settingsPage() {
  const p = state.workspace.profile || {},
    n = state.workspace.notificationSettings || {};
  const currentTab = state.settingsTab || "profile";
  const initials = (p.displayName || p.firstName || "U")[0].toUpperCase();
  const fullName =
    (p.displayName || `${p.firstName || ""} ${p.lastName || ""}`).trim() ||
    "Người dùng";
  const roleTitle = state.roles.includes("Admin")
    ? "Quản trị viên"
    : "Thành viên chính thức";
  const catCount = (state.workspace.categories || []).length;
  return (
    <>
      <div className="profile-hero-card">
        <div className="profile-hero-bg" />
        <div className="profile-hero-content">
          <label
            className="avatar profile-hero-avatar clickable-avatar"
            title="Bấm để chọn/đổi ảnh đại diện"
            tabIndex="0"
            role="button"
            aria-label="Đổi ảnh đại diện"
          >
            {avatarPreview(p)}
            <div className="avatar-hover-overlay">
              {icon("edit")}
              <span>Đổi ảnh</span>
            </div>
            <input
              type="file"
              id="avatar-file-input"
              accept="image/png,image/jpeg,image/webp"
              className="hidden"
            />
          </label>
          <div className="profile-hero-info">
            <div className="profile-hero-name-row">
              <h2>{fullName}</h2>
              <span className="badge purple">{roleTitle}</span>
              {avatarRemove(p)}
            </div>
            <p className="profile-hero-email">
              {icon("shield")} {p.email || ""}{" "}
              <span className="verified-tag">✓ Đã xác minh</span>
            </p>
            {p.bio ? <p className="profile-hero-bio">“{p.bio}”</p> : ""}
          </div>
        </div>
      </div>
      <div className="settings-tabs-bar">
        <button type="button" className={`settings-tab-btn ${currentTab === "pets" ? "active" : ""}`} data-action="settings-tab" data-id="pets">
          {icon("spark")} Pet & XP
        </button>
        <button type="button" className={`settings-tab-btn ${currentTab === "connections" ? "active" : ""}`} data-action="settings-tab" data-id="connections">
          {icon("calendar")} Kết nối
        </button>
        <button
          type="button"
          className={`settings-tab-btn ${currentTab === "profile" ? "active" : ""}`}
          data-action="settings-tab"
          data-id="profile"
        >
          {icon("users")}
          {" Hồ sơ"}
        </button>
        <button
          type="button"
          className={`settings-tab-btn ${currentTab === "categories" ? "active" : ""}`}
          data-action="settings-tab"
          data-id="categories"
        >
          {icon("folder")}
          {" Danh mục công việc "}
          <span className="tab-count">{catCount}</span>
        </button>
        <button
          type="button"
          className={`settings-tab-btn ${currentTab === "security" ? "active" : ""}`}
          data-action="settings-tab"
          data-id="security"
        >
          {icon("shield")}
          {" Bảo mật"}
        </button>
        <button
          type="button"
          className={`settings-tab-btn ${currentTab === "notifications" ? "active" : ""}`}
          data-action="settings-tab"
          data-id="notifications"
        >
          {icon("bell")}
          {" Thông báo"}
        </button>
      </div>
      {currentTab === "connections" ? <div className="stack gap-24"><GoogleCalendarCard /><TelegramCard /></div> : null}
      {currentTab === "pets" ? <NoraInventory /> : null}
      {currentTab === "profile" ? (
        <section className="card settings-content-card">
          <div className="card-header">
            <div>
              <h2>Thông tin cá nhân</h2>
              <p>
                Quản lý tên hiển thị, tiểu sử và thông tin cá nhân của bạn. (Bấm
                vào khung ảnh ở trên để đổi ảnh đại diện)
              </p>
            </div>
          </div>
          <form data-form="profile">
            <input
              type="hidden"
              name="avatarUrl"
              id="avatarUrl-hidden"
              defaultValue={p.avatarUrl || ""}
            />
            <div className="form-grid">
              {field(
                "Tên",
                "firstName",
                "text",
                p.firstName || "",
                'required maxlength="100"',
              )}
              {field(
                "Họ",
                "lastName",
                "text",
                p.lastName || "",
                'required maxlength="100"',
              )}
              <div className="wide">
                {field(
                  "Tên hiển thị công khai",
                  "displayName",
                  "text",
                  p.displayName ||
                    `${p.firstName || ""} ${p.lastName || ""}`.trim(),
                  'required maxlength="100"',
                )}
                <label className="field wide">
                  Giới thiệu bản thân / Bio
                  <textarea
                    name="bio"
                    rows="3"
                    maxLength="1000"
                    placeholder="Viết một đoạn ngắn mô tả bản thân hoặc phong cách làm việc của bạn..."
                    defaultValue={p.bio || ""}
                  />
                </label>
                {field(
                  "Địa chỉ Email (Đã xác minh)",
                  "email",
                  "email",
                  p.email || "",
                  "disabled",
                )}
              </div>
            </div>
            <div className="form-error" />
            <div className="form-actions right-align">
              <button type="submit" className="btn primary">
                {icon("check")}
                {" Lưu thông tin hồ sơ"}
              </button>
            </div>
          </form>
        </section>
      ) : (
        ""
      )}
      {currentTab === "categories" ? (
        <section className="card settings-content-card">
          <div className="card-header">
            <div>
              <h2>Quản lý danh mục công việc</h2>
              <p>
                Phân loại công việc theo dự án hoặc chủ đề để dễ dàng sắp xếp kế
                hoạch.
              </p>
            </div>
            {!secretaryMode() && button(
              "Thêm danh mục mới",
              "new-category",
              "primary small",
              "",
              "plus",
            )}
          </div>
          <div className="categories-grid-modern">
            {nodeList(
              (state.workspace.categories || []).map((c) => (
                <div className="category-card-item">
                  <div className="category-color-badge" data-color={c.color} />
                  <div className="category-card-info">
                    <strong>{c.name}</strong>
                    <small>
                      {
                        state.workspace.tasks.filter(
                          (t) => t.categoryId === c.id,
                        ).length
                      }
                      {" công việc"}
                    </small>
                  </div>
                  {!secretaryMode() && <div className="category-card-actions">
                    {button(
                      "",
                      "edit-category",
                      "icon-only ghost small",
                      c.id,
                      "edit",
                    )}
                    {button(
                      "",
                      "delete-category",
                      "icon-only ghost small danger",
                      c.id,
                      "trash",
                    )}
                  </div>}
                </div>
              )),
            ) ||
              (secretaryMode() ? <p>NORA dùng loại công việc phù hợp từ nội dung bạn giao.</p> : empty(
                "Chưa có danh mục công việc nào",
                "Tạo danh mục đầu tiên để sắp xếp công việc tốt hơn.",
                "new-category",
                "Thêm danh mục",
              ))}
          </div>
        </section>
      ) : (
        ""
      )}
      {currentTab === "security" ? (
        <div className="stack">
          {" "}
          <section className="card settings-content-card">
            <GoogleAccountCard />
            <AccountPassword email={p.email || ""} />
          </section>{" "}
          <section className="card danger-card">
            <div className="card-header">
              <div>
                <h2 className="danger-title">{"Quản lý phiên & Đăng xuất"}</h2>
                <p>
                  Đăng xuất tài khoản khỏi tất cả thiết bị khác trên trình
                  duyệt.
                </p>
              </div>
              {button(
                "Đăng xuất tất cả thiết bị",
                "logout-all",
                "danger",
                "",
                "logout",
              )}
            </div>
          </section>
        </div>
      ) : (
        ""
      )}
      {currentTab === "notifications" ? (
        <div className="stack gap-24">
          <TelegramCard />
          <section className="card settings-content-card">
            <div className="card-header">
              <div>
                <h2>Cài đặt thông báo</h2>
                <p>
                  Cấu hình thời gian nhắc nhở deadline và cảnh báo khối lượng
                  công việc.
                </p>
              </div>
            </div>
            <form data-form="notification-settings">
              <NotificationSoundSettings userId={state.workspace.profile?.id} disabled={n.inAppEnabled === false} />
              <label className="checkbox-switch-card">
                <input
                  type="checkbox"
                  name="inAppEnabled"
                  defaultChecked={n.inAppEnabled}
                />
                <div className="switch-content">
                  <strong>Bật thông báo trong ứng dụng</strong>
                  <p>
                    Hiển thị các nhắc nhở quan trọng về phiên sắp chạy và công
                    việc sắp đến hạn.
                  </p>
                </div>
              </label>
              <label className="checkbox-switch-card spaced">
                <input
                  type="checkbox"
                  name="emailEnabled"
                  defaultChecked={n.emailEnabled ?? false}
                />
                <div className="switch-content">
                  <strong>Gửi nhắc nhở qua email</strong>
                  <p>
                    Nhắc deadline và phiên làm việc đến {p.email}, kể cả khi bạn
                    không mở PLANORA. Email xác minh và bảo mật vẫn được gửi
                    riêng.
                  </p>
                </div>
              </label>
              <div className="form-grid spaced">
                {field(
                  "Nhắc deadline trước (giờ)",
                  "deadlineLeadHours",
                  "number",
                  n.deadlineLeadHours || 24,
                  'required min="1" max="168"',
                )}
                {field(
                  "Nhắc phiên làm việc trước (phút)",
                  "sessionReminderLeadMinutes",
                  "number",
                  n.sessionReminderLeadMinutes || 10,
                  'required min="1" max="120"',
                )}
                <label className="field wide">
                  Cảnh báo khi khối lượng công việc đạt mức
                  <Select name="workloadWarningLevel">
                    {nodeList(
                      Object.entries({
                        MODERATE: "Vừa phải (Bắt đầu theo dõi)",
                        HIGH: "Cao (Nên phân bổ lại)",
                        OVERLOAD: "Quá tải (Cần điều chỉnh ngay)",
                      }).map(([code, label]) => (
                        <option
                          value={code}
                          selected={code === (n.workloadWarningLevel || "HIGH")}
                        >
                          {label}
                        </option>
                      )),
                    )}
                  </Select>
                </label>
              </div>
              <div className="form-error" />
              <div className="form-actions right-align">
                <button className="btn primary" type="submit">
                  {icon("check")}
                  {" Lưu cài đặt thông báo"}
                </button>
              </div>
            </form>
          </section>
        </div>
      ) : (
        ""
      )}
    </>
  );
}
