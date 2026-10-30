import { useEffect, useRef, useState } from "react";
import { attrs } from "./react-ui.jsx";
import { notify, showModal, hideModal } from "./store.js";
import {
  INVALID_LOGIN_MESSAGE,
  useLoginPasswordFeedback,
} from "./password-login-feedback.js";
export const state = {
  workspace: null,
  insights: null,
  user: null,
  roles: [],
  route: "today",
  loadedAt: 0,
  calendarStart: null,
  plan: null,
  scenario: null,
  authEmail: "",
  authIdentifier: "",
  resetToken: null,
};
export const $ = (selector, root = document) => root.querySelector(selector);
const paths = {
  undo: "M9 5 4 10l5 5 M4 10h9a6 6 0 0 1 6 6v3",
  sliders: "M4 6h16 M4 12h16 M4 18h16 M8 3v6 M16 9v6 M10 15v6",
  refresh:
    "M20 7v5h-5 M4 17v-5h5 M6 7a7 7 0 0 1 12-1l2 6 M4 12l2 6a7 7 0 0 0 12-1",
  copy: "M9 9h12v12H9z M5 15H3V3h12v2",
  chat: "M4 4h16v12H9l-5 4z M8 8h8 M8 12h5",
  stop: "M6 6h12v12H6z",
  mic: "M9 5a3 3 0 0 1 6 0v6a3 3 0 0 1-6 0z M6 10v1a6 6 0 0 0 12 0v-1 M12 17v4 M9 21h6",
  image: "M3 4h18v16H3z M3 16l6-6 5 5 3-3 4 4 M16 8h.01",
  panel: "M3 4h18v16H3z M9 4v16 M5 8h2 M5 12h2",
  today: "M3 10l9-7 9 7v10H3z M9 20v-7h6v7",
  tasks: "M9 6h11 M9 12h11 M9 18h11 M3 6h1 M3 12h1 M3 18h1",
  calendar: "M4 5h16v16H4z M8 3v4 M16 3v4 M4 10h16 M8 14h2 M14 14h2",
  insights: "M4 20V10 M10 20V4 M16 20v-7 M22 20H2",
  scenario: "M4 4h6v6H4z M14 14h6v6h-6z M10 7h7v7 M7 10v7h7",
  settings:
    "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8 M12 2v3 M12 19v3 M2 12h3 M19 12h3 M5 5l2 2 M17 17l2 2 M5 19l2-2 M17 7l2-2",
  bell: "M5 17h14l-2-3V9a5 5 0 0 0-10 0v5z M10 20h4",
  plus: "M12 5v14 M5 12h14",
  arrow: "M5 12h14 M13 6l6 6-6 6",
  close: "M6 6l12 12 M18 6L6 18",
  alertCircle: "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18 M12 7v6 M12 16h.01",
  check: "M5 12l4 4L19 6",
  clock: "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18 M12 7v5l4 2",
  play: "M7 4l13 8-13 8z",
  pause: "M8 5v14 M16 5v14",
  edit: "M15 4l5 5 M4 20l2-6L17 3l4 4-11 11z",
  trash: "M4 7h16 M8 4h8 M6 7l1 14h10l1-14 M10 11v6 M14 11v6",
  lock: "M6 10h12v11H6z M8 10V6a4 4 0 0 1 8 0v4",
  mail: "M3 5h18v14H3z M3 5l9 8 9-8",
  userPlus:
    "M9 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8 M2 21v-3a7 7 0 0 1 14 0v3 M19 8v8 M15 12h8",
  globe:
    "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18 M3 12h18 M5 7h14 M5 17h14 M12 3c-5 5-5 13 0 18 M12 3c5 5 5 13 0 18",
  logout: "M9 4H4v16h5 M10 12h11 M17 8l4 4-4 4",
  users:
    "M9 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8 M2 21v-3a7 7 0 0 1 14 0v3 M17 4a4 4 0 0 1 0 7 M18 14a5 5 0 0 1 4 5v2",
  menu: "M4 6h16 M4 12h16 M4 18h16",
  spark: "M12 3l2 6 7 3-7 3-2 6-2-6-7-3 7-3z",
  back: "M19 12H5 M11 6l-6 6 6 6",
  refresh:
    "M20 7v5h-5 M4 17v-5h5 M5 8a7 7 0 0 1 12-3l3 3 M19 16a7 7 0 0 1-12 3l-3-3",
  folder: "M3 6h7l2 3h9v12H3z",
  shield: "M12 3l8 3v6c0 5-8 9-8 9s-8-4-8-9V6z M8 12l3 3 5-6",
  eye: "M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6",
  eyeOff:
    "M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94 M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19 M1 1l22 22",
};
paths.search = "M10.5 3a7.5 7.5 0 1 0 0 15 7.5 7.5 0 0 0 0-15 M16 16l5 5";
export const statusLabels = {
  Todo: "Cần làm",
  InProgress: "Đang làm",
  Completed: "Hoàn thành",
  Cancelled: "Đã hủy",
  Scheduled: "Đã lên lịch",
  Running: "Đang chạy",
  Paused: "Tạm dừng",
  Skipped: "Đã bỏ qua",
  Missed: "Đã lỡ",
  NeedsReview: "Chờ ghi nhận kết quả",
};
export const riskLabels = {
  Unknown: "Chưa xác định",
  Low: "Thấp",
  Moderate: "Vừa phải",
  Medium: "Trung bình",
  High: "Cao",
  Overload: "Quá tải",
};
export const typeLabels = {
  WORK: "Công việc",
  OTHER: "Chung",
  PROGRAMMING: "Lập trình",
  WRITING: "Viết / báo cáo",
  STUDY: "Học tập",
  MEETING_PREP: "Chuẩn bị cuộc họp",
  ADMIN: "Hành chính / cá nhân",
};
export const reasons = {
  DEADLINE_OUTSIDE_HORIZON:
    "Deadline ngoài phạm vi phân tích; chưa kết luận rủi ro thời gian",
  SOFT_DEADLINE_EXCEEDED: "Có phiên sau deadline mềm",
  DEADLINE_PASSED: "Đã quá hạn",
  DEADLINE_CAPACITY_DEFICIT:
    "Khối lượng trước deadline vượt khả năng thực hiện",
  DEADLINE_WITHIN_48_HOURS: "Deadline trong 48 giờ",
  UNFINISHED_DEPENDENCY: "Còn công việc tiền nhiệm chưa xong",
  NO_CONTIGUOUS_SLOT: "Không có khoảng thời gian liền mạch đủ dài",
  WORKLOAD_EXCEEDS_EFFECTIVE_CAPACITY: "Công việc vượt khả năng thực hiện",
  DEADLINE_CLUSTER: "Nhiều deadline tập trung trong 48 giờ",
  EARLIEST_FEASIBLE_SLOT: "Khoảng thời gian phù hợp đầu tiên",
  DEADLINE_PRIORITY_CAPACITY: "Theo deadline, ưu tiên và khả năng thực hiện",
  URGENT_TASK_FIRST: "Ưu tiên công việc khẩn",
  URGENT_NEAR_DEADLINE_MOVE:
    "Nhường giờ cho việc khẩn, dời gần nhất và vẫn kịp hạn",
  URGENT_FLEXIBLE_MOVE: "Nhường giờ cho việc khẩn, lùi trong tối đa hai ngày",
  PRESERVED_SESSION: "Giữ nguyên phiên đã khóa hoặc không bị ảnh hưởng",
  USER_ADJUSTMENT: "Thời gian do bạn điều chỉnh",
  USER_SPLIT: "Phiên được chia nhỏ",
  AUTO_HEAL: "Phiên bù tự động, giữ nguyên các lịch khác",
};
export const zone = () =>
  state.workspace?.preference.timeZoneId || "Asia/Ho_Chi_Minh";
export const dateFmt = (value, options = {}) =>
  new Intl.DateTimeFormat("vi-VN", {
    timeZone: zone(),
    day: "2-digit",
    month: "2-digit",
    ...options,
  }).format(new Date(value));
export const timeFmt = (value) =>
  dateFmt(value, {
    day: undefined,
    month: undefined,
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  });
export const dayKey = (value) =>
  new Intl.DateTimeFormat("en-CA", {
    timeZone: zone(),
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date(value));
export const localInput = (value) => {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat("sv-SE", {
      timeZone: zone(),
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      hourCycle: "h23",
    })
      .formatToParts(new Date(value))
      .filter((x) => x.type !== "literal")
      .map((x) => [x.type, x.value]),
  );
  return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}`;
};
export const toUtc = (value) => {
  const [date, time] = value.split("T");
  if (!date || !time) throw new Error("Hãy nhập ngày giờ hợp lệ.");
  const [y, m, d] = date.split("-").map(Number),
    [h, min] = time.split(":").map(Number);
  const nominal = Date.UTC(y, m - 1, d, h, min);
  let candidate = nominal;
  for (let i = 0; i < 3; i++) {
    const local = localInput(candidate);
    const [ld, lt] = local.split("T");
    const [ly, lm, lday] = ld.split("-").map(Number),
      [lh, lmin] = lt.split(":").map(Number);
    candidate += nominal - Date.UTC(ly, lm - 1, lday, lh, lmin);
  }
  if (localInput(candidate) !== value)
    throw new Error("Giờ này không hợp lệ trong múi giờ đã chọn.");
  return new Date(candidate).toISOString();
};
export const minutes = (value) =>
  value === null || value === undefined
    ? "—"
    : value >= 60
      ? `${Math.floor(value / 60)}h${value % 60 ? ` ${value % 60}p` : ""}`
      : `${value}p`;
export const timeValue = (value) =>
  `${String(Math.floor(value / 60)).padStart(2, "0")}:${String(value % 60).padStart(2, "0")}`;
export const minuteValue = (value) =>
  value === "24:00"
    ? 1440
    : value
        .split(":")
        .reduce(
          (total, n, index) => total + Number(n) * (index === 0 ? 60 : 1),
          0,
        );
export const loadLabel = (i) =>
  i.riskLevel === "Unknown"
    ? "Chưa lấy được dữ liệu phân tích"
    : i.effectiveCapacityMinutes === 0
      ? "Chưa có thời gian rảnh"
      : `${Math.round(i.loadRatio * 100)}% khả năng thực hiện · ${i.horizonDemandMinutes ?? i.remainingMinutes} phút đến hạn trong phạm vi phân tích`;
export const icon = (name, extra = "") => (
  <svg aria-hidden="true" viewBox="0 0 24 24" className={`icon ${extra}`}>
    <path d={paths[name] || paths.tasks} />
  </svg>
);
export function button(text, action, extra = "", id = "", glyph = "") {
  const labels = {
    "week-prev": "Tuần trước",
    "week-next": "Tuần sau",
    "remove-slot": "Xóa khoảng thời gian",
    "edit-category": "Chỉnh sửa danh mục",
    "delete-category": "Xóa danh mục",
  };
  const glyphLabels = {
    close: "Đóng",
    logout: "Đăng xuất",
    menu: "Mở menu",
    back: "Quay lại",
    arrow: "Tiếp theo",
    edit: "Chỉnh sửa",
    trash: "Xóa",
  };
  return (
    <button
      type="button"
      className={`btn ${extra}`}
      data-action={action}
      data-id={id || undefined}
      aria-label={
        text ? undefined : labels[action] || glyphLabels[glyph] || "Thao tác"
      }
    >
      {glyph && icon(glyph)}
      {text}
    </button>
  );
}
export const badge = (text, color = "") => (
  <span className={`badge ${color}`}>{text}</span>
);
export function userAvatar(p, extra = "") {
  return (
    <div className={`avatar ${extra}`}>
      {p?.avatarUrl ? (
        <img src={p.avatarUrl} alt="Avatar" className="avatar-img" />
      ) : (
        (p?.displayName || p?.firstName || "U")[0].toUpperCase()
      )}
    </div>
  );
}
export const toast = notify;
let modalTrigger = null;
export function modal(title, subtitle, body) {
  if (!document.querySelector("#modal")?.open)
    modalTrigger = document.activeElement;
  showModal(title, subtitle, body);
}
export function closeModal() {
  hideModal();
  if (modalTrigger?.isConnected) modalTrigger.focus();
}
export const empty = (
  title,
  message,
  action = "new-task",
  label = "Tạo công việc",
) => (
  <div className="empty">
    {icon("folder")}
    <h3>{title}</h3>
    <p>{message}</p>
    {action && button(label, action, "secondary", "", "plus")}
  </div>
);
export function PasswordField({
  label,
  name,
  value = "",
  extra = "",
  hiddenLabel = false,
  leadingIcon,
  confirmFor,
  loginFeedback = false,
}) {
  const [visible, setVisible] = useState(false);
  const input = useRef(null);
  const rejected = useLoginPasswordFeedback(input, loginFeedback);
  const [match, setMatch] = useState(null);
  useEffect(() => {
    if (!confirmFor) return;
    const form = input.current?.closest("form");
    if (!form) return;
    const sync = () => {
      const confirmation = input.current.value;
      const password = form.elements.namedItem(confirmFor)?.value || "";
      setMatch(
        confirmation
          ? confirmation === password
            ? "matched"
            : "mismatch"
          : null,
      );
    };
    sync();
    form.addEventListener("input", sync);
    form.addEventListener("change", sync);
    return () => {
      form.removeEventListener("input", sync);
      form.removeEventListener("change", sync);
    };
  }, [confirmFor]);
  const inputAttributes = attrs(extra);
  const feedbackId = `${name}-match`;
  const errorId = `${name}-login-error`;
  return (
    <label
      className="field password-field"
      data-match={match || undefined}
      data-rejected={rejected || undefined}
    >
      <span
        id={confirmFor || loginFeedback ? `${name}-label` : undefined}
        className={hiddenLabel ? "auth-visually-hidden" : undefined}
      >
        {label}
      </span>
      <div className="input-with-action">
        {leadingIcon && (
          <span className="login-input-icon" aria-hidden="true">
            {icon(leadingIcon)}
          </span>
        )}
        <input
          ref={input}
          type={visible ? "text" : "password"}
          name={name}
          defaultValue={value}
          {...inputAttributes}
          aria-labelledby={
            confirmFor || loginFeedback
              ? `${name}-label`
              : inputAttributes["aria-labelledby"]
          }
          aria-invalid={rejected || match === "mismatch" ? true : undefined}
          aria-describedby={
            [
              inputAttributes["aria-describedby"],
              match ? feedbackId : "",
              rejected ? errorId : "",
            ]
              .filter(Boolean)
              .join(" ") || undefined
          }
        />
        {rejected && (
          <span className="password-error-indicator" aria-hidden="true">
            {icon("alertCircle")}
          </span>
        )}
        {match && (
          <span className="password-confirm-indicator" aria-hidden="true">
            {icon(match === "matched" ? "check" : "close")}
          </span>
        )}
        <button
          type="button"
          className="btn-toggle-pwd"
          aria-label={visible ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
          onClick={() => setVisible((v) => !v)}
        >
          {icon(visible ? "eyeOff" : "eye")}
        </button>
      </div>
      {rejected && (
        <span id={errorId} className="password-login-error" role="alert">
          {INVALID_LOGIN_MESSAGE}
        </span>
      )}
      {confirmFor && (
        <span
          id={feedbackId}
          className="password-confirm-message"
          role="status"
          aria-live="polite"
        >
          {match === "matched"
            ? "Mật khẩu khớp"
            : match === "mismatch"
              ? "Mật khẩu chưa khớp"
              : ""}
        </span>
      )}
    </label>
  );
}
export function field(label, name, type = "text", value = "", extra = "") {
  if (type === "password")
    return (
      <PasswordField
        key={name}
        label={label}
        name={name}
        value={value}
        extra={extra}
      />
    );
  return (
    <label className="field" key={name}>
      {label}
      <input type={type} name={name} defaultValue={value} {...attrs(extra)} />
    </label>
  );
}
export const stat = (label, value, note, glyph = "clock", color = "") => (
  <div className="card">
    <span className={`stat-icon ${color}`}>{icon(glyph)}</span>
    <div className="stat-label">{label}</div>
    <div className="stat-value">{value}</div>
    <div className="stat-note">{note}</div>
  </div>
);
