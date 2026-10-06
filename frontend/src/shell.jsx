import { Slot, attrs, nodeList } from "./react-ui.jsx";
import { setSlot } from "./store.js";
import { brand } from "./brand.jsx";
import { $, state, icon, button, userAvatar, dateFmt, modal } from "./ui.jsx";
export function workspaceShell(routes, title, description, body) {
  const p = state.workspace.profile || {},
    unread = (state.workspace.notifications || []).filter(
      (n) => !n.isRead,
    ).length;
  const nav = (keys) =>
    nodeList(
      keys
        .filter((key) => key !== "admin" || state.roles.includes("Admin"))
        .map((key) => (
          <button
            type="button"
            className={state.route === key ? "active" : ""}
            data-action="nav"
            data-id={key}
            {...attrs(state.route === key ? 'aria-current="page"' : "")}
          >
            {icon(routes[key][2])}
            <span>{routes[key][0]}</span>
            {key === "notifications" && unread ? (
              <span className="notification-number">{unread}</span>
            ) : (
              ""
            )}
          </button>
        )),
    );
  const actions =
    state.route === "tasks" ? (
      <>
        {button("Ghi nhanh", "capture", "secondary", "", "spark")}
        {button("Thêm công việc", "new-task", "primary", "", "plus")}
      </>
    ) : state.route === "today" ? (
      button("Thêm công việc", "new-task", "primary", "", "plus")
    ) : state.route === "calendar" ? (
      button("Lập kế hoạch", "generate-plan", "primary", "", "spark")
    ) : (
      ""
    );
  const headline =
    state.route === "today"
      ? `Một ngày có chủ đích${p.firstName ? `, ${p.firstName}` : ""}.`
      : title;
  return (
    <>
      <a className="skip-link" href="#main-content">
        Đến nội dung chính
      </a>
      <div className="shell glass-workspace">
        <button
          type="button"
          className="sidebar-overlay"
          data-action="close-menu"
          aria-label="Đóng menu"
          tabIndex="-1"
          hidden={true}
        />
        <aside className="sidebar" id="workspace-menu">
          <div className="sidebar-brand-header">
            {brand("#today", "", "PLANORA — Hôm nay")}
            <button
              type="button"
              className="btn ghost icon-only sidebar-close"
              data-action="close-menu"
              aria-label="Đóng menu"
            >
              {icon("close")}
            </button>
          </div>
          <div className="workspace-label">
            {icon("folder")}
            <span>Không gian cá nhân</span>
            <span className="workspace-dot" />
          </div>
          <button type="button" className="quick-search" data-action="search">
            {icon("search")}
            <span>Tìm công việc</span>
            <kbd>Ctrl K</kbd>
          </button>
          <nav className="nav" aria-label="Điều hướng chính">
            <div className="nav-label">LÀM VIỆC</div>
            {nav(["today", "tasks", "calendar"])}
            <div className="nav-label">LÊN KẾ HOẠCH</div>
            {nav(["time", "insights", "scenario"])}
            <div className="nav-label">QUẢN LÝ</div>
            {nav(["notifications", "settings", "admin"])}
          </nav>
          <div className="sidebar-bottom">
            <div className="side-note">
              <span className="side-note-icon">{icon("spark")}</span>
              <strong>Dành chỗ cho việc quan trọng</strong>
              <p>Một kế hoạch vừa sức bắt đầu từ thời gian bạn có.</p>
              {button("Xem giờ rảnh", "nav", "small ghost", "time", "arrow")}
            </div>
            <div className="user-mini">
              {userAvatar(p)}
              <button
                className="user-link"
                type="button"
                data-action="nav"
                data-id="settings"
              >
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
              </button>
              {button("", "logout", "ghost icon-only", "", "logout")}
            </div>
          </div>
        </aside>
        <div className="main">
          <header className="topbar">
            <div className="actions">
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
              <div className="breadcrumb">
                Không gian cá nhân
                <span>/</span>
                <strong>{title}</strong>
              </div>
            </div>
            <div className="topbar-right">
              <span className="top-date">
                {dateFmt(Date.now(), {
                  weekday: "long",
                  year: "numeric",
                })}
              </span>
              <button
                type="button"
                className="bell"
                data-action="nav"
                data-id="notifications"
                aria-label={`Thông báo${unread ? `, ${unread} chưa đọc` : ""}`}
              >
                {icon("bell")}
                {unread ? <span className="dot" /> : ""}
              </button>
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
          <main className="content" id="main-content" tabIndex="-1">
            <div className="page-header">
              <div>
                <div className="eyebrow">
                  {state.route === "today"
                    ? dateFmt(Date.now(), {
                        weekday: "long",
                        month: "long",
                      })
                    : "KHÔNG GIAN CỦA BẠN"}
                </div>
                <h1>{headline}</h1>
                <p>
                  {state.route === "today"
                    ? "Nhìn rõ việc cần làm. Dành thời gian để hoàn thành."
                    : description}
                </p>
              </div>
              <div className="actions">{actions}</div>
            </div>
            {state.analysisError ? (
              <div className="warning analysis-warning" role="status">
                <div>
                  <strong>Phân tích chưa sẵn sàng</strong>
                  <p>
                    Chưa tải được đầy đủ phân tích hoặc kiểm tra xung đột. Không
                    thể xác nhận lịch đang an toàn.
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
          </main>
        </div>
      </div>
    </>
  );
}
export function setMobileMenu(open = false) {
  const sidebar = $(".sidebar");
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
  $(".mobile-menu")?.setAttribute("aria-expanded", String(mobile && open));
  if (mobile && open) {
    $(".main").inert = true;
    sidebar.querySelector("a,button")?.focus();
  } else if (mobile && (hadFocus || wasOpen)) {
    queueMicrotask(() => $(".mobile-menu")?.focus());
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
              {"Deadline "}
              {dateFmt(t.deadline)}
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
  if (!window.matchMedia("(max-width: 800px)").matches) setMobileMenu(false);
  else if (!$(".sidebar")?.classList.contains("open")) setMobileMenu(false);
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
