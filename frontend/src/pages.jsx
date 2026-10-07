import { Select, Slot, nodeList } from "./react-ui.jsx";
import { avatarPreview, avatarRemove } from "./interactive.jsx";
import { PasswordChecklist } from "./password-checklist.jsx";
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
  PasswordField,
  empty,
  modal,
  loadLabel,
} from "./ui.jsx";
export function performanceChart() {
  const base = dayKey(Date.now()),
    rows = Array.from(
      {
        length: 7,
      },
      (_, index) => {
        const date = new Date(`${base}T12:00:00Z`);
        date.setUTCDate(date.getUTCDate() + index - 6);
        const key = date.toISOString().slice(0, 10),
          sessions = state.workspace.sessions.filter(
            (s) => s.status === "Completed" && dayKey(s.end) === key,
          );
        return {
          label: key.slice(8) + "/" + key.slice(5, 7),
          planned: sessions.reduce(
            (sum, s) =>
              sum + Math.round((new Date(s.end) - new Date(s.start)) / 60000),
            0,
          ),
          actual: sessions.reduce((sum, s) => sum + s.actualMinutes, 0),
        };
      },
    );
  const maximum = Math.max(60, ...rows.flatMap((r) => [r.planned, r.actual]));
  return (
    <>
      <svg
        viewBox="0 0 700 180"
        className="chart"
        role="img"
        aria-label="Biểu đồ dự kiến và thực tế của phiên hoàn thành trong 7 ngày"
      >
        <line x1="20" y1="145" x2="690" y2="145" />
        {nodeList(
          rows.map((r, index) => {
            const x = 30 + index * 95,
              p = (r.planned / maximum) * 110,
              a = (r.actual / maximum) * 110;
            return (
              <g>
                <rect x={x} y={145 - p} width="22" height={p} rx="4">
                  <title>
                    {"Dự kiến: "}
                    {r.planned}
                    {" phút"}
                  </title>
                </rect>
                <rect
                  className="actual"
                  x={x + 27}
                  y={145 - a}
                  width="22"
                  height={a}
                  rx="4"
                >
                  <title>
                    {"Thực tế: "}
                    {r.actual}
                    {" phút"}
                  </title>
                </rect>
                <text x={x + 25} y="165" textAnchor="middle">
                  {r.label}
                </text>
                <text x={x + 25} y={132 - Math.max(p, a)} textAnchor="middle">
                  {r.planned}
                  {" / "}
                  {r.actual}
                </text>
              </g>
            );
          }),
        )}
      </svg>
      <p className="reason">
        Xanh đậm: dự kiến · Xanh nhạt: thực tế (phút). Theo ngày kết thúc dự
        kiến của phiên hoàn thành.
      </p>
    </>
  );
}
export { todayPage } from "./dashboard.jsx";
export function insightsPage() {
  if (!state.insights)
    return (
      <section className="card">
        <h2>Chưa có kết quả phân tích</h2>
        <p>
          Không thể lấy dữ liệu lúc này. Hãy thử tải lại để xem đánh giá
          workload và rủi ro.
        </p>
      </section>
    );
  const i = state.insights;
  const active = state.workspace.tasks.filter(
    (t) => !["Cancelled"].includes(t.status),
  );
  const rows = active.slice(0, 8);
  return (
    <>
      <div className="stats-grid">
        {stat(
          "Workload còn lại",
          minutes(i.remainingMinutes),
          "Chỉ tính công việc độc lập và việc con",
          "tasks",
        )}
        {stat(
          "Thời gian rảnh",
          minutes(i.calendarFreeMinutes),
          "Sau khi trừ sự kiện và ngoại lệ",
          "calendar",
          "blue",
        )}
        {stat(
          "Khả năng hiệu quả",
          minutes(i.effectiveCapacityMinutes),
          `${i.capacitySamples} phiên quan sát`,
          "clock",
          "green",
        )}
        {stat(
          "Nguy cơ tổng thể",
          riskLabels[i.riskLevel],
          loadLabel(i),
          "insights",
          "amber",
        )}
      </div>
      <div className="equal-columns section-gap">
        <section className="card">
          <div className="card-header">
            <div>
              <h2>Hệ số ước lượng cá nhân</h2>
              <p>Thời gian thực tế ÷ dự kiến, từ công việc đã hoàn thành.</p>
            </div>
          </div>
          {nodeList(
            i.biases.map((b) => (
              <div className="bias-item">
                <div>
                  <strong>
                    {b.taskType === "All"
                      ? "Toàn bộ công việc"
                      : typeLabels[b.taskType] || b.taskType}
                  </strong>
                  <p>
                    {b.samples}
                    {" mẫu · "}
                    {b.personalized
                      ? "Đã cá nhân hóa"
                      : "Chưa đủ 3 mẫu, dùng hệ số trung tính"}
                  </p>
                </div>
                <strong>{b.ratio.toFixed(2)}×</strong>
              </div>
            )),
          ) || (
            <p className="reason">
              Hoàn thành ít nhất 3 công việc có thời gian thực tế để bắt đầu cá
              nhân hóa.
            </p>
          )}
          <p className="reason spaced">
            Ví dụ: hệ số 1.25× điều chỉnh 60 phút dự kiến thành 75 phút khi lập
            lịch.
          </p>
        </section>
        <section className="card">
          <div className="card-header">
            <div>
              <h2>Khả năng thực hiện</h2>
              <p>Tỷ lệ thời gian được hoàn thành trong các phiên đã đến hạn.</p>
            </div>
          </div>
          <div className="capacity-visual">
            <svg viewBox="0 0 120 120" aria-hidden="true">
              <circle
                cx="60"
                cy="60"
                r="48"
                stroke="#edeafa"
                strokeWidth="12"
              />
              <circle
                cx="60"
                cy="60"
                r="48"
                stroke="#8780e5"
                strokeWidth="12"
                strokeDasharray={`${i.capacityFactor * 302} 302`}
                transform="rotate(-90 60 60)"
              />
            </svg>
            <strong>{Math.round(i.capacityFactor * 100)}%</strong>
          </div>
          <p className="reason">
            {i.capacitySamples >= 5
              ? "Đã sử dụng lịch sử cá nhân để tính khả năng thực hiện."
              : "Chưa đủ 5 phiên quan sát. Tạm dùng 80% để dành một phần dự phòng."}
          </p>
          {nodeList(
            i.reasons.map((r) => (
              <div className="warning">{reasons[r] || r}</div>
            )),
          )}
        </section>
      </div>
      <section className="card section-gap">
        <div className="card-header">
          <div>
            <h2>Dự kiến và thực tế</h2>
            <p>Thời gian theo từng công việc. Đơn vị: phút.</p>
          </div>
        </div>
        {performanceChart()}
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Công việc</th>
                <th>Dự kiến</th>
                <th>Thực tế</th>
                <th>Tiến độ</th>
              </tr>
            </thead>
            <tbody>
              {nodeList(
                rows.map((t) => (
                  <tr>
                    <td>{t.name}</td>
                    <td>{t.estimatedMinutes}</td>
                    <td>{t.actualMinutes}</td>
                    <td>
                      <progress
                        className="progress"
                        value={t.progress}
                        max="100"
                      />{" "}
                      {t.progress}%
                    </td>
                  </tr>
                )),
              )}
            </tbody>
          </table>
        </div>
      </section>
      <section className="card">
        <div className="card-header">
          <h2>Rủi ro theo công việc</h2>
        </div>
        {i.tasks.length
          ? nodeList(
              i.tasks.map((t) => (
                <div className="list-line">
                  <div>
                    <button
                      className="text-btn"
                      data-action="task-detail"
                      data-id={t.taskId}
                    >
                      {t.name}
                    </button>
                    <p>
                      {t.reasons.map((r) => reasons[r] || r).join(" · ") ||
                        "Chưa phát hiện rủi ro đáng kể"}
                    </p>
                  </div>
                  {badge(
                    riskLabels[t.level],
                    t.level === "High"
                      ? "red"
                      : t.level === "Medium"
                        ? "amber"
                        : "green",
                  )}
                </div>
              )),
            )
          : empty("Chưa có dữ liệu", "Thêm công việc để xem phân tích.")}
      </section>
    </>
  );
}
export function scenarioPage() {
  const result = state.scenario;
  return (
    <div className="equal-columns">
      <section className="card">
        <div className="card-header">
          <div>
            <h2>Nếu có thêm một công việc…</h2>
            <p>Xem tác động trước khi nhận thêm việc.</p>
          </div>
        </div>
        <form data-form="scenario">
          <div className="form-grid">
            <div className="wide">
              {field(
                "Tên công việc giả định",
                "name",
                "text",
                "",
                'required maxlength="200" placeholder="Ví dụ: Chuẩn bị buổi thuyết trình"',
              )}
            </div>
            {field(
              "Thời lượng (phút)",
              "estimatedMinutes",
              "number",
              120,
              'required min="1" max="43200"',
            )}
            {field("Deadline", "deadline", "datetime-local", "", "required")}
            <label className="field">
              Ưu tiên
              <Select name="priority">
                <option value="2">Vừa</option>
                <option value="3">Cao</option>
                <option value="4">Khẩn</option>
                <option value="1">Thấp</option>
              </Select>
            </label>
            <label className="field">
              Loại công việc
              <Select name="taskType">
                {nodeList(
                  Object.entries(typeLabels).map(([key, label]) => (
                    <option value={key}>{label}</option>
                  )),
                )}
              </Select>
            </label>
          </div>
          <div className="form-error" />
          <div className="form-actions">
            <button type="submit" className="btn primary">
              {"Phân tích kịch bản "}
              {icon("scenario")}
            </button>
          </div>
        </form>
        <div className="info spaced">
          Mô phỏng không lưu công việc và không thay đổi lịch hiện tại.
        </div>
      </section>
      <section className="card">
        <div className="card-header">
          <h2>Trước và sau</h2>
        </div>
        {result ? (
          <>
            <div className="equal-columns">
              {nodeList(
                [result.before, result.after].map((i, n) => (
                  <div>
                    <div className="eyebrow">
                      {n ? "Sau khi thêm" : "Hiện tại"}
                    </div>
                    <div className="stat-value">
                      {minutes(i.remainingMinutes)}
                    </div>
                    {badge(
                      riskLabels[i.riskLevel],
                      i.riskLevel === "High" || i.riskLevel === "Overload"
                        ? "red"
                        : "green",
                    )}
                    <p className="reason spaced">{loadLabel(i)}</p>
                  </div>
                )),
              )}
            </div>
            <div className="warning spaced">
              {"Thêm "}
              {minutes(result.addedMinutes)}
              {" workload. "}
              {result.proposedPlan.warnings.length}
              {" cảnh báo trong kế hoạch giả định."}
            </div>
            {nodeList(
              result.proposedPlan.warnings.map((w) => (
                <p className="reason">
                  {w.taskName}
                  {": "}
                  {w.message}
                </p>
              )),
            )}
            {button(
              "Xem lịch giả định",
              "scenario-plan",
              "secondary",
              "",
              "calendar",
            )}
            {button(
              "Tạo công việc từ kịch bản",
              "scenario-create",
              "ghost",
              "",
              "plus",
            )}
          </>
        ) : (
          empty(
            "Thử trước khi quyết định",
            "Nhập một công việc ở bên trái để so sánh.",
            "",
            "",
          )
        )}
      </section>
    </div>
  );
}
export function notificationsPage() {
  return (
    <section className="card">
      <div className="card-header">
        <div>
          <h2>Hộp thông báo</h2>
          <p>Deadline, phiên sắp đến, workload và thay đổi lịch.</p>
        </div>
      </div>
      {nodeList(
        state.workspace.notifications.map((n) => (
          <div className={`notification ${n.isRead ? "read" : ""}`}>
            <span className="notification-dot" />
            <div>
              <h3>{n.title}</h3>
              <p>{n.message}</p>
              <small>
                {dateFmt(n.createdAt)}
                {" · "}
                {timeFmt(n.createdAt)}
              </small>
              <div className="actions spaced">
                {n.taskId
                  ? button(
                      "Xem công việc",
                      "task-detail",
                      "small ghost",
                      n.taskId,
                    )
                  : ""}
                {!n.isRead
                  ? button(
                      "Đánh dấu đã đọc",
                      "notification-read",
                      "small ghost",
                      n.id,
                      "check",
                    )
                  : ""}
              </div>
            </div>
          </div>
        )),
      ) ||
        empty(
          "Bạn đã cập nhật mọi thứ",
          "Thông báo sẽ xuất hiện ở đây khi có điều cần chú ý.",
          "",
          "",
        )}
    </section>
  );
}
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
          {" Nhóm công việc "}
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
              <h2>Quản lý nhóm công việc</h2>
              <p>
                Phân loại công việc theo dự án hoặc chủ đề để dễ dàng sắp xếp kế
                hoạch.
              </p>
            </div>
            {button(
              "Thêm nhóm mới",
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
                </div>
              )),
            ) ||
              empty(
                "Chưa có nhóm công việc nào",
                "Tạo nhóm đầu tiên để sắp xếp công việc tốt hơn.",
                "new-category",
                "Thêm nhóm",
              )}
          </div>
        </section>
      ) : (
        ""
      )}
      {currentTab === "security" ? (
        <section className="card settings-content-card">
          <div className="card-header">
            <div>
              <h2>Đổi mật khẩu tài khoản</h2>
              <p>
                Yêu cầu mã xác thực OTP gửi về Email để bảo vệ tài khoản của
                bạn.
              </p>
            </div>
          </div>
          <div className="security-steps-banner">
            <div className="step-box">
              <span className="step-num">1</span>
              <div>
                <strong>Bước 1: Yêu cầu mã OTP</strong>
                <p>
                  {"Mã xác minh 6 chữ số sẽ được gửi tới Email: "}
                  <b>{p.email || ""}</b>
                </p>
              </div>
              {button(
                "Gửi mã OTP",
                "change-otp",
                "secondary small",
                "",
                "shield",
              )}
            </div>
          </div>
          <form data-form="password">
            <div className="form-grid">
              <div className="wide">
                {field(
                  "Mật khẩu hiện tại",
                  "currentPassword",
                  "password",
                  "",
                  'required maxlength="128" autocomplete="current-password"',
                )}
              </div>
              <div className="wide">
                {field(
                  "Mã OTP xác nhận từ Email",
                  "otp",
                  "text",
                  "",
                  'required pattern="[0-9]{6,8}" maxlength="8" inputmode="numeric" autocomplete="one-time-code" placeholder="Nhập 6 chữ số OTP"',
                )}
              </div>
              {field(
                "Mật khẩu mới",
                "newPassword",
                "password",
                "",
                'required minlength="8" maxlength="32" aria-describedby="password-requirements" autocomplete="new-password"',
              )}{" "}
              <PasswordField
                label="Nhập lại mật khẩu mới"
                name="confirmNewPassword"
                confirmFor="newPassword"
                extra='required minlength="8" maxlength="32" autocomplete="new-password"'
              />
            </div>
            <PasswordChecklist passwordName="newPassword" />
            <div className="form-error" />
            <div className="form-actions right-align">
              <button type="submit" className="btn primary">
                {icon("lock")}
                {" Đổi mật khẩu ngay"}
              </button>
            </div>
          </form>
        </section>
      ) : (
        ""
      )}
      {currentTab === "notifications" ? (
        <div className="stack gap-24">
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
                  Cảnh báo khi Workload đạt mức
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
    </>
  );
}
export function categoryModal(category = {}) {
  modal(
    category.id ? "Chỉnh sửa nhóm" : "Thêm nhóm công việc",
    "Dùng nhóm để lọc và tổ chức công việc.",
    <form data-form="category" data-id={category.id || ""}>
      {field(
        "Tên nhóm",
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
          Lưu nhóm
        </button>
      </div>
    </form>,
  );
}
export function adminPage() {
  return (
    <>
      <section className="card section-gap">
        <div className="card-header">
          <div>
            <h2>Trạng thái hệ thống</h2>
            <p>Database, hoạt động và tác vụ thông báo.</p>
          </div>
          {button(
            "Cập nhật",
            "admin-refresh",
            "small secondary",
            "",
            "refresh",
          )}
        </div>
        <Slot id="system-status" className="admin-grid" as="div">
          <p className="muted">Đang tải…</p>
        </Slot>
      </section>
      <section className="card">
        <div className="card-header">
          <h2>Quản lý người dùng</h2>
        </div>
        <form data-form="admin-search" className="filters">
          <input
            className="filter search"
            name="search"
            placeholder="Tìm theo email hoặc tên"
            maxLength="100"
            aria-label="Tìm người dùng"
          />
          <button type="submit" className="btn secondary">
            Tìm kiếm
          </button>
        </form>
        <Slot className="table-scroll" id="admin-users" as="div">
          Đang tải…
        </Slot>
      </section>
      <section className="card spaced">
        <h2>Lịch sử xử lý hệ thống</h2>
        <Slot className="table-scroll" id="admin-jobs" as="div">
          Đang tải…
        </Slot>
      </section>
    </>
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
