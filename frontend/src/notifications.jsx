import { useState } from "react";
import { dateFmt, dayKey, icon, timeFmt } from "./ui.jsx";

const types = {
  deadline: { label: "Hạn chót", glyph: "clock", tone: "amber" },
  session: { label: "Phiên làm việc", glyph: "calendar", tone: "blue" },
  warning: { label: "Cảnh báo lịch", glyph: "alertCircle", tone: "coral" },
  schedule: { label: "Thay đổi lịch", glyph: "refresh", tone: "teal" },
  other: { label: "Cập nhật khác", glyph: "bell", tone: "blue" },
};
function notificationType(kind) {
  return (
    {
      DEADLINE: "deadline",
      SESSION_REMINDER: "session",
      WORKLOAD_WARNING: "warning",
      SCHEDULE_CHANGE: "schedule",
      SCHEDULE_CHANGED: "schedule",
    }[kind] || "other"
  );
}
const searchable = (value) =>
  String(value || "")
    .toLocaleLowerCase("vi")
    .normalize("NFD")
    .replace(/\p{M}/gu, "")
    .replace(/đ/g, "d");

export function NotificationCenter({ workspace }) {
  const [status, setStatus] = useState("all");
  const [type, setType] = useState("all");
  const [query, setQuery] = useState("");
  const notifications = workspace.notifications || [];
  const tasks = workspace.tasks || [];
  const settings = workspace.notificationSettings || {};
  const unread = notifications.filter((item) => !item.isRead).length;
  const counts = {
    all: notifications.length,
    unread,
    read: notifications.length - unread,
  };
  const term = searchable(query.trim());
  const visible = notifications
    .filter((item) => {
      const key = notificationType(item.kind);
      const task = tasks.find((task) => task.id === item.taskId);
      return (
        (status === "all" ||
          (status === "unread" ? !item.isRead : item.isRead)) &&
        (type === "all" || key === type) &&
        (!term ||
          searchable(
            `${item.title} ${item.message} ${task?.name || ""} ${types[key].label}`,
          ).includes(term))
      );
    })
    .sort(
      (a, b) => (Date.parse(b.createdAt) || 0) - (Date.parse(a.createdAt) || 0),
    );
  const today = dayKey(Date.now());
  const previous = new Date(`${today}T12:00:00Z`);
  previous.setUTCDate(previous.getUTCDate() - 1);
  const yesterday = previous.toISOString().slice(0, 10);
  const groups = [];
  visible.forEach((item) => {
    const valid = Number.isFinite(Date.parse(item.createdAt));
    const key = valid ? dayKey(item.createdAt) : "unknown";
    let group = groups.find((group) => group.key === key);
    if (!group) {
      group = {
        key,
        label:
          key === today
            ? "Hôm nay"
            : key === yesterday
              ? "Hôm qua"
              : valid
                ? dateFmt(item.createdAt, { year: "numeric" })
                : "Trước đó",
        items: [],
      };
      groups.push(group);
    }
    group.items.push(item);
  });
  const resetFilters = () => {
    setStatus("all");
    setType("all");
    setQuery("");
  };
  const filtered = status !== "all" || type !== "all" || !!query.trim();
  return (
    <div className="notification-center">
      <section
        className={`notice-overview ${unread ? "has-unread" : ""}`}
        aria-labelledby="notice-overview-title"
      >
        <span className="notice-overview-icon" aria-hidden="true">
          {icon(unread ? "bell" : "check")}
        </span>
        <div className="notice-overview-copy">
          <span className="notice-eyebrow">NHỊP LÀM VIỆC CỦA BẠN</span>
          <h2 id="notice-overview-title">
            {unread
              ? `${unread} thông báo chưa đọc`
              : notifications.length
                ? "Bạn đã đọc hết thông báo"
                : "Một nơi cho mọi nhắc nhở"}
          </h2>
          <p>
            {unread
              ? "Kiểm tra việc đến hạn, phiên sắp bắt đầu và những thay đổi cần chú ý."
              : "Nhắc nhở về công việc và lịch của bạn sẽ xuất hiện ở đây."}
          </p>
        </div>
        <a className="notice-calendar-link" href="#calendar">
          {icon("calendar")} Mở lịch làm việc {icon("arrow")}
        </a>
      </section>
      <div className="notice-layout">
        <section className="card notice-inbox" aria-label="Hộp thông báo">
          <div className="notice-toolbar">
            <div
              className="notice-status-filters"
              role="group"
              aria-label="Lọc trạng thái thông báo"
            >
              {Object.entries({
                all: "Tất cả",
                unread: "Chưa đọc",
                read: "Đã đọc",
              }).map(([key, label]) => (
                <button
                  key={key}
                  type="button"
                  className={`notice-filter ${status === key ? "active" : ""}`}
                  aria-pressed={status === key}
                  onClick={() => setStatus(key)}
                >
                  {label} <span>{counts[key]}</span>
                </button>
              ))}
            </div>
            <span className="notice-sort">{icon("clock")} Mới nhất trước</span>
          </div>
          <div className="notice-search-row">
            <label className="notice-search">
              {icon("search")}
              <input
                type="search"
                aria-label="Tìm thông báo"
                placeholder="Tìm trong thông báo…"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
              />
            </label>
            <label className="notice-type-filter">
              <span className="sr-only">Loại thông báo</span>
              <select
                value={type}
                onChange={(event) => setType(event.target.value)}
              >
                <option value="all">Tất cả loại thông báo</option>
                {Object.entries(types).map(([key, value]) => (
                  <option key={key} value={key}>
                    {value.label}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <div className="notice-result-bar">
            <p role="status">
              {visible.length} thông báo
              {filtered ? " phù hợp" : " trong hộp thư"}
            </p>
            {filtered && (
              <button
                type="button"
                className="notice-reset"
                onClick={resetFilters}
              >
                Bỏ bộ lọc
              </button>
            )}
          </div>
          {groups.length ? (
            groups.map((group) => (
              <section
                className="notice-day"
                key={group.key}
                aria-label={`Thông báo ${group.label}`}
              >
                <h3 className="notice-day-heading">
                  {group.label} <span>{group.items.length}</span>
                </h3>
                <ul className="notice-list">
                  {group.items.map((item) => (
                    <NotificationItem
                      key={item.id}
                      item={item}
                      task={tasks.find((task) => task.id === item.taskId)}
                    />
                  ))}
                </ul>
              </section>
            ))
          ) : (
            <div className="notice-empty">
              <span className="notice-empty-icon">
                {icon(
                  term || type !== "all"
                    ? "search"
                    : status === "unread"
                      ? "check"
                      : "bell",
                )}
              </span>
              <h3>
                {term || type !== "all"
                  ? "Không tìm thấy thông báo"
                  : status === "unread"
                    ? "Không còn thông báo chưa đọc"
                    : status === "read"
                      ? "Chưa có thông báo đã đọc"
                      : "Chưa có thông báo"}
              </h3>
              <p>
                {filtered
                  ? "Thử đổi từ khóa hoặc bỏ bộ lọc để xem các thông báo khác."
                  : "Khi có việc sắp đến hạn, phiên sắp bắt đầu hoặc lịch thay đổi, bạn sẽ nhận nhắc nhở tại đây."}
              </p>
              {filtered ? (
                <button
                  type="button"
                  className="notice-empty-action"
                  onClick={resetFilters}
                >
                  Xem tất cả thông báo {icon("arrow")}
                </button>
              ) : (
                <a className="notice-empty-action" href="#today">
                  Về Hôm nay {icon("arrow")}
                </a>
              )}
            </div>
          )}
        </section>
        <aside className="notice-sidebar" aria-label="Tổng quan nhắc nhở">
          <section className="card notice-priorities">
            <div className="notice-sidebar-heading">
              <span className="notice-small-icon">{icon("mail")}</span>
              <h2>Chưa đọc theo loại</h2>
            </div>
            <p className="notice-sidebar-description">
              Chọn điều bạn muốn kiểm tra trước.
            </p>
            <div className="notice-type-shortcuts">
              {Object.entries(types)
                .filter(
                  ([key]) =>
                    key !== "other" ||
                    notifications.some(
                      (item) =>
                        !item.isRead && notificationType(item.kind) === key,
                    ),
                )
                .map(([key, value]) => {
                  const count = notifications.filter(
                    (item) =>
                      !item.isRead && notificationType(item.kind) === key,
                  ).length;
                  return (
                    <button
                      key={key}
                      type="button"
                      aria-pressed={status === "unread" && type === key}
                      onClick={() => {
                        setStatus("unread");
                        setType(key);
                        setQuery("");
                      }}
                    >
                      <span className={`notice-type-icon tone-${value.tone}`}>
                        {icon(value.glyph)}
                      </span>
                      <span>{value.label}</span> <strong>{count}</strong>
                    </button>
                  );
                })}
            </div>
          </section>
          <section
            className="card notice-preferences"
            aria-labelledby="notice-preferences-title"
          >
            <div className="notice-sidebar-heading">
              <span className="notice-small-icon">{icon("settings")}</span>
              <h2 id="notice-preferences-title">Nhắc nhở của bạn</h2>
            </div>
            <span
              className={`notice-enabled ${settings.inAppEnabled === false ? "is-off" : ""}`}
            >
              <span aria-hidden="true" />
              {settings.inAppEnabled === false
                ? "Đang tắt trong ứng dụng"
                : "Đang bật trong ứng dụng"}
            </span>
            {settings.inAppEnabled === false && (
              <p className="notice-paused-copy">
                Bật lại để nhận nhắc nhở mới. Các thông báo đã có vẫn được giữ.
              </p>
            )}
            <dl className="notice-settings-list">
              <div>
                <dt>Trước hạn chót</dt>
                <dd>{settings.deadlineLeadHours ?? 24} giờ</dd>
              </div>
              <div>
                <dt>Trước phiên làm việc</dt>
                <dd>{settings.sessionReminderLeadMinutes ?? 10} phút</dd>
              </div>
              <div>
                <dt>Cảnh báo khối lượng</dt>
                <dd>
                  {{
                    MODERATE: "Từ mức vừa phải",
                    HIGH: "Từ mức cao",
                    OVERLOAD: "Khi quá tải",
                  }[
                    String(
                      settings.workloadWarningLevel || "HIGH",
                    ).toUpperCase()
                  ] || "Từ mức cao"}
                </dd>
              </div>
            </dl>
            <button
              type="button"
              className="notice-settings-link"
              data-action="notification-preferences"
            >
              Điều chỉnh nhắc nhở {icon("arrow")}
            </button>
          </section>
          <p className="notice-read-hint">
            {icon("check")} Đã đọc chỉ ghi nhận bạn đã xem thông báo. Tiến độ
            công việc vẫn được cập nhật trong từng công việc.
          </p>
        </aside>
      </div>
    </div>
  );
}
function NotificationItem({ item, task }) {
  const type = notificationType(item.kind);
  const meta = types[type];
  const target =
    type === "session" || type === "schedule"
      ? { label: "Xem lịch làm việc", route: "calendar" }
      : task
        ? { label: "Xem công việc", taskId: task.id }
        : type === "warning"
          ? { label: "Xem tiến độ", route: "insights" }
          : item.taskId
            ? { label: "Mở danh sách công việc", route: "tasks" }
            : null;
  const validDate = Number.isFinite(Date.parse(item.createdAt));
  return (
    <li>
      <article
        className={`notice-item ${item.isRead ? "is-read" : "is-unread"}`}
        data-notification-id={item.id}
        aria-labelledby={`notice-title-${item.id}`}
      >
        <span
          className={`notice-item-icon tone-${meta.tone}`}
          aria-hidden="true"
        >
          {icon(meta.glyph)}
        </span>
        <div className="notice-item-body">
          <div className="notice-item-meta">
            <span className={`notice-kind tone-${meta.tone}`}>
              {meta.label}
            </span>
            {!item.isRead && (
              <span className="notice-unread-label">
                <span aria-hidden="true" />
                Chưa đọc
              </span>
            )}
            {validDate && (
              <time
                dateTime={item.createdAt}
                title={dateFmt(item.createdAt, { year: "numeric" })}
              >
                {timeFmt(item.createdAt)}
              </time>
            )}
          </div>
          <h4 id={`notice-title-${item.id}`}>{item.title}</h4>
          <p className="notice-message">{item.message}</p>
          {task && (
            <p className="notice-task-context">
              {icon("tasks")}
              <span>{task.name}</span>
              {type === "deadline" &&
                Number.isFinite(Date.parse(task.deadline)) && (
                  <span className="notice-deadline">
                    Hạn: {dateFmt(task.deadline)} · {timeFmt(task.deadline)}
                  </span>
                )}
            </p>
          )}
          {!task && item.taskId && (
            <p className="notice-missing-task">
              Công việc liên quan không còn trong danh sách hiện tại.
            </p>
          )}
          <div className="notice-item-actions">
            {target &&
              (target.taskId ? (
                <button
                  type="button"
                  className="notice-open"
                  data-action="task-detail"
                  data-id={target.taskId}
                >
                  {target.label}
                  {icon("arrow")}
                </button>
              ) : (
                <a className="notice-open" href={`#${target.route}`}>
                  {target.label}
                  {icon("arrow")}
                </a>
              ))}
            {!item.isRead ? (
              <button
                type="button"
                className="notice-mark-read"
                data-action="notification-read"
                data-id={item.id}
                aria-label={`Đánh dấu đã đọc: ${item.title}`}
              >
                {icon("check")}Đánh dấu đã đọc
              </button>
            ) : (
              <span className="notice-read-label">{icon("check")}Đã đọc</span>
            )}
          </div>
        </div>
      </article>
    </li>
  );
}
