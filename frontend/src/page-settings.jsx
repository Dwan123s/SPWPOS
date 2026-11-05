import { useState } from "react";
import { nodeList } from "./react-ui.jsx";
import { secretaryMode } from "./agent-handoff.js";
import { state, button, icon, field, empty } from "./ui.jsx";
import { avatarPreview, avatarRemove, setAvatar } from "./interactive.jsx";
import { AccountPassword } from "./account-password.jsx";
import { GoogleAccountCard } from "./google-signin.jsx";
import { NoraInventory } from "./nora-inventory.jsx";
import { GoogleCalendarCard } from "./google-calendar.jsx";
import { TelegramCard } from "./telegram.jsx";
import { NotificationSoundSettings } from "./notification-sound-settings.jsx";
import { SettingsForm } from "./settings-form.jsx";
import "./styles/settings.css";

const sections = [
  { id: "profile", label: "Hồ sơ", hint: "Thông tin cá nhân", glyph: "users" },
  {
    id: "security",
    label: "Bảo mật",
    hint: "Mật khẩu & đăng nhập",
    glyph: "shield",
  },
  {
    id: "notifications",
    label: "Thông báo",
    hint: "Kênh nhận & giờ nhắc",
    glyph: "bell",
  },
  {
    id: "connections",
    label: "Kết nối",
    hint: "Google Calendar & Telegram",
    glyph: "calendar",
  },
  {
    id: "categories",
    label: "Danh mục công việc",
    hint: "Sắp xếp theo chủ đề",
    glyph: "folder",
  },
  {
    id: "pets",
    label: "Pet & XP",
    hint: "Bạn đồng hành của bạn",
    glyph: "spark",
  },
];

// Keep the route's callable export compatible with the existing page registry.
export function settingsPage() {
  return <SettingsPage />;
}

function SettingsPage() {
  const p = state.workspace.profile || {};
  const [currentTab, setCurrentTab] = useState(() =>
    sections.some((section) => section.id === state.settingsTab)
      ? state.settingsTab
      : "profile",
  );
  const fullName =
    (p.displayName || `${p.firstName || ""} ${p.lastName || ""}`).trim() ||
    "Người dùng";
  const roleTitle = state.roles.includes("Admin")
    ? "Quản trị viên"
    : "Thành viên";
  const catCount = (state.workspace.categories || []).length;
  return (
    <div className="settings-workspace">
      <aside className="settings-sidebar">
        <div className="settings-identity">
          <span className="settings-identity-avatar" aria-hidden="true">
            {p.avatarUrl ? (
              <img src={p.avatarUrl} alt="" />
            ) : (
              fullName[0].toUpperCase()
            )}
          </span>
          <div>
            <strong>{fullName}</strong>
            <span>{p.email}</span>
            <small>{roleTitle}</small>
          </div>
        </div>
        <nav className="settings-navigation" aria-label="Mục cài đặt">
          {sections.map(({ id, label, hint, glyph }) => (
            <button
              key={id}
              type="button"
              className={`settings-nav-item${currentTab === id ? " is-active" : ""}`}
              id={`settings-nav-${id}`}
              aria-current={currentTab === id ? "page" : undefined}
              aria-controls={`settings-panel-${id}`}
              onClick={() => {
                state.settingsTab = id;
                setCurrentTab(id);
              }}
            >
              <span className="settings-nav-icon" aria-hidden="true">
                {icon(glyph)}
              </span>
              <span className="settings-nav-copy">
                <strong>{label}</strong>
                <small>{hint}</small>
              </span>
              {id === "categories" && (
                <span
                  className="settings-nav-count"
                  aria-label={`${catCount} danh mục`}
                >
                  {catCount}
                </span>
              )}
            </button>
          ))}
        </nav>
        <p className="settings-sidebar-note">
          Tùy chỉnh PLANORA theo cách bạn làm việc mỗi ngày.
        </p>
      </aside>
      <div className="settings-main">
        <div
          id="settings-panel-profile"
          role="region"
          aria-labelledby="settings-nav-profile"
          hidden={currentTab !== "profile"}
        >
          <section className="card settings-content-card">
            <div className="card-header">
              <div>
                <h2>Thông tin cá nhân</h2>
                <p>
                  Chọn cách bạn xuất hiện trong PLANORA và các nhóm của mình.
                </p>
              </div>
              <a className="btn secondary small" href="#profile">
                Xem trang cá nhân {icon("arrow")}
              </a>
            </div>
            <SettingsForm
              name="profile"
              saveLabel="Lưu thông tin hồ sơ"
              onRestore={() => setAvatar(p.avatarUrl || "")}
            >
              <input
                type="hidden"
                name="avatarUrl"
                id="avatarUrl-hidden"
                defaultValue={p.avatarUrl || ""}
              />
              <div className="settings-avatar-row">
                <div className="settings-avatar-picker">
                  <button
                    type="button"
                    className="settings-edit-avatar"
                    aria-label="Đổi ảnh đại diện"
                    aria-describedby="settings-avatar-help"
                    title="Bấm để đổi ảnh đại diện"
                    onClick={(event) =>
                      event.currentTarget
                        .closest("form")
                        .querySelector("#avatar-file-input")
                        .click()
                    }
                  >
                    {avatarPreview(p)}
                    <span
                      className="settings-avatar-overlay"
                      aria-hidden="true"
                    >
                      {icon("edit")} Đổi ảnh
                    </span>
                  </button>
                  {avatarRemove(p)}
                  <input
                    type="file"
                    id="avatar-file-input"
                    accept="image/png,image/jpeg,image/webp"
                    className="hidden"
                    aria-label="Chọn ảnh đại diện"
                  />
                </div>
                <div className="settings-avatar-controls">
                  <strong>Ảnh đại diện</strong>
                  <p id="settings-avatar-help">
                    Bấm vào ảnh để thay đổi. Nút thùng rác ở góc ảnh để gỡ ảnh.
                  </p>
                  <small>Hỗ trợ PNG, JPG và WebP.</small>
                </div>
              </div>
              <div className="form-grid">
                {field(
                  "Họ",
                  "lastName",
                  "text",
                  p.lastName || "",
                  'required maxlength="100" autocomplete="family-name"',
                )}
                {field(
                  "Tên",
                  "firstName",
                  "text",
                  p.firstName || "",
                  'required maxlength="100" autocomplete="given-name"',
                )}
                <label className="field wide">
                  Tên hiển thị công khai
                  <input
                    name="displayName"
                    required
                    maxLength="100"
                    defaultValue={
                      p.displayName ||
                      `${p.firstName || ""} ${p.lastName || ""}`.trim()
                    }
                    aria-describedby="settings-display-name-help"
                  />
                  <small id="settings-display-name-help">
                    Tên hiển thị trong nhóm và cuộc trò chuyện.
                  </small>
                </label>
                <label className="field wide">
                  Giới thiệu bản thân / Bio
                  <textarea
                    name="bio"
                    rows="3"
                    maxLength="1000"
                    placeholder="Một vài dòng về bạn hoặc cách bạn làm việc…"
                    defaultValue={p.bio || ""}
                  />
                </label>
                <label className="field wide">
                  Email đăng nhập
                  <input
                    type="email"
                    name="email"
                    value={p.email || ""}
                    disabled
                    aria-describedby="settings-email-help"
                  />
                  <small id="settings-email-help">
                    Email gắn với tài khoản, không thay đổi tại đây.
                  </small>
                </label>
              </div>
            </SettingsForm>
          </section>
        </div>
        <div
          id="settings-panel-notifications"
          role="region"
          aria-labelledby="settings-nav-notifications"
          hidden={currentTab !== "notifications"}
        >
          <NotificationPreferences />
        </div>
        {!["profile", "notifications"].includes(currentTab) && (
          <div
            id={`settings-panel-${currentTab}`}
            role="region"
            aria-labelledby={`settings-nav-${currentTab}`}
          >
            {currentTab === "connections" && (
              <div className="stack gap-24">
                <div className="settings-section-intro">
                  <h2>Kết nối ứng dụng</h2>
                  <p>
                    Đồng bộ lịch và nhận nhắc việc ngay cả khi bạn không mở
                    PLANORA.
                  </p>
                </div>
                <GoogleCalendarCard />
                <TelegramCard />
              </div>
            )}
            {currentTab === "pets" && <NoraInventory />}
            {currentTab === "categories" ? (
              <section className="card settings-content-card">
                <div className="card-header">
                  <div>
                    <h2>Quản lý danh mục công việc</h2>
                    <p>
                      Phân loại công việc theo dự án hoặc chủ đề để dễ dàng sắp
                      xếp kế hoạch.
                    </p>
                  </div>
                  {!secretaryMode() &&
                    button(
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
                        <div
                          className="category-color-badge"
                          data-color={c.color}
                        />
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
                        {!secretaryMode() && (
                          <div className="category-card-actions">
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
                          </div>
                        )}
                      </div>
                    )),
                  ) ||
                    (secretaryMode() ? (
                      <p>
                        NORA dùng loại công việc phù hợp từ nội dung bạn giao.
                      </p>
                    ) : (
                      empty(
                        "Chưa có danh mục công việc nào",
                        "Tạo danh mục đầu tiên để sắp xếp công việc tốt hơn.",
                        "new-category",
                        "Thêm danh mục",
                      )
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
                <section className="card danger-card settings-sessions">
                  <div className="card-header">
                    <div>
                      <h2 className="danger-title">{"Phiên đăng nhập"}</h2>
                      <p>
                        Đăng xuất khỏi tất cả thiết bị, bao gồm cả phiên đang
                        dùng. Bạn sẽ cần đăng nhập lại.
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
          </div>
        )}
      </div>
    </div>
  );
}

function NotificationPreferences() {
  const p = state.workspace.profile || {};
  const n = state.workspace.notificationSettings || {};
  const [inAppEnabled, setInAppEnabled] = useState(n.inAppEnabled ?? true);
  return (
    <section className="card settings-content-card">
      <div className="card-header">
        <div>
          <h2>Cài đặt thông báo</h2>
          <p>Chọn nơi nhận thông báo và thời điểm nhắc phù hợp với bạn.</p>
        </div>
      </div>
      <SettingsForm
        name="notification-settings"
        saveLabel="Lưu cài đặt thông báo"
        onRestore={() => setInAppEnabled(n.inAppEnabled ?? true)}
      >
        <fieldset className="settings-fieldset">
          <legend>Kênh nhận thông báo</legend>
          <label className="settings-switch-row">
            <span>
              <strong>Trong ứng dụng</strong>
              <small>
                Nhắc phiên làm việc và công việc sắp đến hạn khi mở PLANORA.
              </small>
            </span>
            <input
              type="checkbox"
              name="inAppEnabled"
              role="switch"
              checked={inAppEnabled}
              onChange={(event) => setInAppEnabled(event.target.checked)}
              aria-label="Bật thông báo trong ứng dụng"
            />
          </label>
          <label className="settings-switch-row">
            <span>
              <strong>Qua email</strong>
              <small>
                Gửi nhắc việc đến {p.email || "email tài khoản"}, kể cả khi bạn
                không mở ứng dụng.
              </small>
            </span>
            <input
              type="checkbox"
              name="emailEnabled"
              role="switch"
              defaultChecked={n.emailEnabled ?? false}
              aria-label="Gửi nhắc nhở qua email"
            />
          </label>
          <p className="settings-field-help">
            Email xác minh và bảo mật luôn được gửi khi cần thiết.
          </p>
          <div className="settings-channel-link">
            <span>{icon("chat")} Muốn nhận nhắc việc qua Telegram?</span>
            <button
              type="button"
              className="btn ghost small"
              onClick={() =>
                document.getElementById("settings-nav-connections")?.click()
              }
            >
              Thiết lập kết nối {icon("arrow")}
            </button>
          </div>
        </fieldset>
        <fieldset className="settings-fieldset">
          <legend>Thời điểm nhắc</legend>
          <p className="settings-field-help">
            Dùng chung cho các kênh nhắc việc đã bật.
          </p>
          <div className="form-grid">
            <label className="field">
              Nhắc deadline trước (giờ)
              <input
                name="deadlineLeadHours"
                type="number"
                required
                min="1"
                max="168"
                defaultValue={n.deadlineLeadHours ?? 24}
                aria-describedby="settings-deadline-help"
              />
              <small id="settings-deadline-help">
                Từ 1 đến 168 giờ (7 ngày).
              </small>
            </label>
            <label className="field">
              Nhắc phiên làm việc trước (phút)
              <input
                name="sessionReminderLeadMinutes"
                type="number"
                required
                min="1"
                max="120"
                defaultValue={n.sessionReminderLeadMinutes ?? 10}
                aria-describedby="settings-session-help"
              />
              <small id="settings-session-help">Từ 1 đến 120 phút.</small>
            </label>
            <label className="field wide">
              Cảnh báo khối lượng công việc
              <select
                name="workloadWarningLevel"
                defaultValue={n.workloadWarningLevel || "HIGH"}
              >
                <option value="MODERATE">Vừa phải — Bắt đầu theo dõi</option>
                <option value="HIGH">Cao — Nên phân bổ lại</option>
                <option value="OVERLOAD">Quá tải — Cần điều chỉnh ngay</option>
              </select>
              <small>Nhắc khi khối lượng công việc đạt mức bạn chọn.</small>
            </label>
          </div>
        </fieldset>
      </SettingsForm>
      <div className="settings-local-sound">
        <h3>Âm thanh trên trình duyệt này</h3>
        <p className="settings-field-help">
          Thay đổi âm thanh được áp dụng ngay, không cần bấm lưu.
        </p>
        <NotificationSoundSettings userId={p.id} disabled={!inAppEnabled} />
      </div>
    </section>
  );
}
