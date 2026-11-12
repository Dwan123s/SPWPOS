import { useLayoutEffect, useRef, useState } from "react";
import { Slot, attrs, nodeList } from "./react-ui.jsx";
import { setSlot } from "./store.js";
import { PetProgress } from "./pet-appearance.jsx";
import { FocusLauncher } from "./focus-timer.jsx";
import { PageTransition } from "./page-transition.jsx";
import { PopupDetails } from "./popup-dismissal.jsx";
import {
  WorkspaceAtmosphere,
  WorkspaceMotionControl,
} from "./workspace-motion.jsx";
import { $, state, icon, button, userAvatar, dateFmt, modal } from "./ui.jsx";
import { adminRoutes, isAdminRoute } from "./admin-navigation.js";
import {
  NotificationBell,
  NotificationUnreadCount,
} from "./notification-popups.jsx";
import "./styles/workspace-rail.css";

const menuKey = "planora.menu.compact";
const railLabels = {
  today: "Hôm nay",
  calendar: "Lịch",
  tasks: "Việc",
  teams: "Nhóm",
  insights: "Tiến độ",
  agent: "NORA AI",
};
const workRoutes = ["notes", "tasks"];
const planRoutes = ["calendar", "time"];
function readStorage(key) {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}
function writeStorage(key, value) {
  try {
    localStorage.setItem(key, value);
  } catch {
    /* Storage is optional. */
  }
}
export function MenuLabelsControl() {
  const host = useRef(null);
  const [saved, setSaved] = useState(() => readStorage(menuKey));
  const compact = saved !== "false";
  useLayoutEffect(() => {
    const consoleEl = host.current?.closest(".planning-console");
    if (consoleEl) {
      consoleEl.dataset.navMode = compact ? "compact" : "expanded";
    }
  }, [compact]);
  return (
    <button
      ref={host}
      type="button"
      className="btn ghost icon-only menu-labels-control"
      aria-label={compact ? "Hiện tên menu" : "Thu gọn menu"}
      title={compact ? "Hiện tên menu" : "Thu gọn menu"}
      aria-expanded={!compact}
      aria-controls="workspace-menu"
      onClick={() => {
        const value = !compact;
        setSaved(String(value));
        writeStorage(menuKey, String(value));
      }}
    >
      {icon("menu")}
    </button>
  );
}
function RailAccount({ profile }) {
  const popup = useRef(null);
  const name =
    profile.displayName ||
    [profile.firstName, profile.lastName].filter(Boolean).join(" ") ||
    "Tài khoản";
  const close = () => {
    if (popup.current) popup.current.open = false;
  };
  return (
    <PopupDetails className="rail-account" ref={popup}>
      <summary
        className="rail-account-trigger"
        aria-label="Menu tài khoản"
        title="Tài khoản"
      >
        {userAvatar(profile)}
      </summary>
      <div className="rail-account-panel">
        <button
          className="rail-account-identity"
          type="button"
          data-action="nav"
          data-id="profile"
          onClick={close}
        >
          {userAvatar(profile)}
          <span>
            <strong>{name}</strong>
            {profile.email && <small>{profile.email}</small>}
          </span>
        </button>
        <div className="rail-account-section">
          <button
            type="button"
            data-action="nav"
            data-id="settings"
            onClick={close}
          >
            {icon("settings")} Cài đặt
          </button>
          <button
            type="button"
            onClick={() => {
              close();
              modal(
                "Trợ giúp PLANORA",
                "",
                <ul>
                  <li>Nhấn Ctrl + K để tìm nhanh công việc.</li>
                  <li>Dùng dấu + trong Lịch để thêm sự kiện cho một ngày.</li>
                  <li>Mở Công việc → Inbox để ghi nhanh việc cần làm.</li>
                </ul>,
              );
            }}
          >
            {icon("alertCircle")} Trợ giúp
          </button>
        </div>
        <div className="rail-account-section">
          <button type="button" data-action="logout" onClick={close}>
            {icon("logout")} Đăng xuất
          </button>
        </div>
      </div>
    </PopupDetails>
  );
}

export function workspaceShell(routes, title, description, body) {
  const p = state.workspace.profile || {};
  const home = state.route === "today";
  const work = workRoutes.includes(state.route);
  const plan = planRoutes.includes(state.route);
  const sectionRoutes = work && state.route !== "tasks" ? workRoutes : plan ? planRoutes : [];
  const railCurrent = work ? "tasks" : plan ? "calendar" : state.route;
  const nav = (keys) =>
    nodeList(
      keys
        .filter((key) => !isAdminRoute(key) || state.roles.includes("Admin"))
        .map((key) => (
          <button
            type="button"
            className={`nav-link${state.route === key ? " active" : ""}`}
            data-action="nav"
            data-id={key}
            data-rail-label={railLabels[key]}
            data-rail-current={railCurrent === key ? "true" : undefined}
            title={routes[key][0]}
            aria-label={routes[key][0]}
            {...attrs(state.route === key ? 'aria-current="page"' : "")}
          >
            {icon(routes[key][2])}
            <span className="nav-text">{routes[key][0]}</span>
            {key === "notifications" ? <NotificationUnreadCount /> : ""}
          </button>
        )),
    );
  return (
    <>
      <a
        className="skip-link"
        href="#main-content"
        onClick={(event) => {
          event.preventDefault();
          document.getElementById("main-content")?.focus();
        }}
      >
        Đến nội dung chính
      </a>
      <div
        className="shell glass-workspace planning-console"
        data-route={state.route}
      >
        <WorkspaceAtmosphere />
        <button
          type="button"
          className="sidebar-overlay"
          data-action="close-menu"
          aria-label="Đóng menu"
          tabIndex="-1"
          hidden={true}
        />
        <aside className="sidebar" id="workspace-menu">
          <div className="sidebar-top">
            <div className="sidebar-brand-header">
              <a
                className="brand brand-lockup console-brand-mark"
                href="#today"
                aria-label="PLANORA — Hôm nay"
              >
                <img
                  src="/assets/planora-mark.png"
                  width="36"
                  height="36"
                  alt="PLANORA"
                />
              </a>
              <button
                type="button"
                className="btn ghost icon-only sidebar-close"
                data-action="close-menu"
                aria-label="Đóng menu"
              >
                {icon("close")}
              </button>
            </div>
          </div>
          <nav className="nav" aria-label="Điều hướng chính">
            <button
              type="button"
              className="btn primary icon-only workspace-rail-add"
              data-action="new-task"
              aria-label="Tạo nhanh công việc"
              title="Tạo nhanh công việc"
            >
              {icon("plus")}
            </button>
            <section className="nav-group" aria-label="Không gian cá nhân">
              <div className="nav-label">Không gian cá nhân</div>
              {nav([
                "today",
                "calendar",
                "tasks",
                "insights",
                "agent",
                "notes",
              ])}
            </section>
            <section className="nav-group" aria-label="Cộng tác">
              <div className="nav-label">Cộng tác</div>
              {nav(["teams"])}
            </section>
            <section className="nav-group" aria-label="Kết nối bạn bè">
              <div className="nav-label">Kết nối</div>
              {nav(["profile", "friends"])}
            </section>
            <section className="nav-group" aria-label="Theo dõi và thiết lập">
              <div className="nav-label">Theo dõi & thiết lập</div>
              {nav(["time"])}
            </section>
            <section className="nav-group" aria-label="Quản lý">
              <div className="nav-label">Quản lý</div>
              {nav(["notifications", "settings"])}
            </section>
            {state.roles.includes("Admin") && (
              <section className="nav-group" aria-label="Quản trị viên">
                <div className="nav-label">Quản trị viên</div>
                {nav(Object.keys(adminRoutes))}
              </section>
            )}
          </nav>
          <div className="sidebar-bottom">
            <div
              className="workspace-rail-tools"
              role="group"
              aria-label="Lối tắt"
            >
              <button
                type="button"
                data-action="search"
                aria-label="Tìm kiếm từ thanh bên"
                title="Tìm kiếm"
              >
                {icon("search")}
              </button>
              <button
                type="button"
                data-action="nav"
                data-id="notifications"
                aria-label="Mở thông báo từ thanh bên"
                title="Thông báo"
              >
                {icon("bell")}
                <NotificationUnreadCount />
              </button>
              <RailAccount profile={p} />
            </div>
            <div className="user-mini">
              <button
                className="user-link"
                type="button"
                aria-label="Hồ sơ của bạn"
                data-action="nav"
                data-id="profile"
                title={
                  p.displayName ||
                  `${p.firstName || ""} ${p.lastName || ""}`.trim() ||
                  "Tài khoản"
                }
              >
                {userAvatar(p)}
                <span className="user-mini-info">
                  <strong>
                    {p.displayName ||
                      `${p.firstName || ""} ${p.lastName || ""}`.trim() ||
                      "Tài khoản"}
                  </strong>
                  <small>
                    {state.roles.includes("Admin")
                      ? "Quản trị viên"
                      : "Không gian của bạn"}
                  </small>
                </span>
              </button>
              <button
                className="btn ghost icon-only sidebar-logout"
                type="button"
                data-action="logout"
                aria-label="Đăng xuất"
                title="Đăng xuất"
              >
                {icon("logout")}
              </button>
            </div>
          </div>
        </aside>
        <div className="main">
          <header className="topbar">
            <div className="actions">
              <MenuLabelsControl />
              <button
                type="button"
                className="btn ghost icon-only mobile-menu"
                data-action="mobile-menu"
                aria-controls="workspace-menu"
                aria-expanded="false"
                aria-label="Mở menu"
              >
                {icon("menu")}
              </button>
              <button
                className="console-brand-name"
                type="button"
                data-action="nav"
                data-id="today"
              >
                PLANORA
              </button>
              <button
                type="button"
                className="console-command-search"
                data-action="search"
                aria-label="Mở tìm kiếm nhanh"
              >
                {icon("search")}
                <span>Tìm công việc…</span>
                <kbd>Ctrl K</kbd>
              </button>
              <div className="breadcrumb">
                Không gian cá nhân
                <span>/</span>
                <strong>{title}</strong>
              </div>
            </div>
            <div className="topbar-right">
              <FocusLauncher compact />
              <PetProgress compact />
              <WorkspaceMotionControl />
              <NotificationBell />
              <button
                type="button"
                className="avatar-button"
                data-action="nav"
                data-id="settings"
                aria-label="Tài khoản"
              >
                {userAvatar(p)}
              </button>
            </div>
          </header>
          {sectionRoutes.length > 0 && (
            <nav
              className={`workspace-section-links${plan ? " workspace-section-links--plan" : ""}`}
              aria-label={work ? "Khu vực công việc" : "Khu vực lập lịch"}
            >
              {sectionRoutes.map((key) => (
                <a
                  key={key}
                  href={`#${key}`}
                  aria-current={state.route === key ? "page" : undefined}
                >
                  {plan && icon(routes[key][2])}
                  {routes[key][0]}
                </a>
              ))}
            </nav>
          )}
          <main className="content" id="main-content" tabIndex="-1">
            <PageTransition route={state.route}>
              {home ? null : ["calendar", "teams", "profile", "admin-revenue"].includes(
                  state.route,
                ) ? (
                <h1 className="sr-only" tabIndex="-1">
                  {title}
                </h1>
              ) : (
                <div className="page-header">
                  <div>
                    <h1 tabIndex="-1">{title}</h1>
                    <p>{description}</p>
                  </div>
                  {state.route === "tasks" &&
                    button("Thêm công việc", "new-task", "primary", "", "plus")}
                </div>
              )}
              {state.analysisError ? (
                <div className="warning analysis-warning" role="status">
                  <div>
                    <strong>Phân tích chưa sẵn sàng</strong>
                    <p>
                      Chưa tải được đầy đủ phân tích hoặc kiểm tra xung đột.
                      Không thể xác nhận lịch đang an toàn.
                    </p>
                  </div>
                  {button(
                    "Thử lại",
                    "reload-workspace",
                    "small secondary",
                    "",
                    "refresh",
                  )}
                </div>
              ) : (
                ""
              )}
              {body}
              <footer className="workspace-footer">
                <span>PLANORA · Làm việc theo nhịp của bạn.</span>
                <span>{state.workspace.preference.timeZoneId}</span>
              </footer>
            </PageTransition>
          </main>
        </div>
      </div>
    </>
  );
}
export function setMobileMenu(open = false) {
  const sidebar = $(".sidebar");
  const trigger = $(".nora-app-menu") || $(".mobile-menu");
  if (!sidebar) return;
  const mobile = window.matchMedia("(max-width: 800px)").matches,
    hadFocus = sidebar.contains(document.activeElement),
    wasOpen = sidebar.classList.contains("open");
  if (!(mobile && open) && $(".main")) $(".main").inert = false;
  sidebar.classList.toggle("open", mobile && open);
  sidebar.inert = mobile && !open;
  const overlay = $(".sidebar-overlay");
  if (overlay) overlay.hidden = !(mobile && open);
  document.body.classList.toggle("menu-open", mobile && open);
  trigger?.setAttribute("aria-expanded", String(mobile && open));
  if (mobile && open) {
    $(".main").inert = true;
    sidebar.querySelector("a,button")?.focus();
  } else if (mobile && (hadFocus || wasOpen)) {
    queueMicrotask(() => trigger?.focus());
  }
}
export function syncDecorations() {
  document.querySelectorAll("[data-color]").forEach((el) => {
    if (/^#[0-9a-f]{6}$/i.test(el.dataset.color))
      el.style.backgroundColor = el.dataset.color;
  });
}
export function openSearch() {
  modal(
    "Tìm trong không gian của bạn",
    "Tìm theo tên hoặc nội dung công việc.",
    <>
      <label className="field">
        Tìm công việc
        <input
          type="search"
          id="workspace-search"
          placeholder="Nhập để tìm kiếm…"
          autoComplete="off"
        />
      </label>
      <Slot
        id="search-results"
        className="search-results"
        aria-live="polite"
        as="div"
      />
    </>,
  );
  filterSearch();
  $("#workspace-search").focus();
}
export function filterSearch() {
  const query =
    $("#workspace-search")?.value.trim().toLocaleLowerCase("vi") || "";
  const results = state.workspace.tasks.filter(
    (t) =>
      !query ||
      `${t.name} ${t.description || ""}`
        .toLocaleLowerCase("vi")
        .includes(query),
  );
  setSlot(
    "search-results",
    nodeList(
      results.slice(0, 20).map((t) => (
        <button
          type="button"
          className="search-result"
          data-action="task-detail"
          data-id={t.id}
        >
          {icon("tasks")}
          <span>
            <strong>{t.name}</strong>
            <small>
              {t.deadline ? `Hạn ${dateFmt(t.deadline)}` : "Chưa có hạn"}
              {" · "}
              {t.progress}% hoàn thành
            </small>
          </span>
          {icon("arrow")}
        </button>
      )),
    ) || <p className="search-empty">Không tìm thấy công việc phù hợp.</p>,
  );
}
const onResize = () => {
  setMobileMenu(
    window.matchMedia("(max-width: 800px)").matches && !!$(".sidebar.open"),
  );
};
const onKeyDown = (event) => {
  if (
    event.target.matches(".clickable-avatar") &&
    ["Enter", " "].includes(event.key)
  ) {
    event.preventDefault();
    event.target.querySelector('input[type="file"]')?.click();
  }
  if (
    (event.ctrlKey || event.metaKey) &&
    event.key.toLowerCase() === "k" &&
    state.workspace
  ) {
    event.preventDefault();
    setMobileMenu(false);
    openSearch();
  }
  if (event.key === "Escape" && $(".sidebar.open")) {
    event.preventDefault();
    setMobileMenu(false);
  }
  if (event.key === "Tab" && $(".sidebar.open") && !$("#modal").open) {
    const controls = [
        ...$(".sidebar").querySelectorAll("a,button:not(:disabled)"),
      ],
      first = controls[0],
      last = controls.at(-1);
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }
};
export function setupShell() {
  window.addEventListener("resize", onResize);
  document.addEventListener("keydown", onKeyDown);
  return () => {
    window.removeEventListener("resize", onResize);
    document.removeEventListener("keydown", onKeyDown);
  };
}
