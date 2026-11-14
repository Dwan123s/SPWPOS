import { nodeList } from "./react-ui.jsx";
import { RoutePage } from "./route-pages.jsx";
import { adminRoutes, isAdminRoute } from "./admin-navigation.js";
import { ConnectionState } from "./connection-state.jsx";
import { Loading } from "./loading.jsx";
import { getSnapshot, setScreen, setSlot } from "./store.js";
import { setAvatar, openAvatarCropper } from "./interactive.jsx";
import {
  workspaceShell,
  setMobileMenu,
  openSearch,
  filterSearch,
  syncDecorations,
} from "./shell.jsx";
import {
  $,
  state,
  button,
  badge,
  field,
  modal,
  closeModal,
  toast,
  dateFmt,
  timeFmt,
  minutes,
  reasons,
  dayKey,
  localInput,
  toUtc,
  minuteValue,
} from "./ui.jsx";
import { api, submit } from "./api.js";
import { readTeamBriefFile } from "./team-brief-file.js";
import { syncPetProfile } from "./pet-appearance.jsx";
import { captureTeamInvite, pendingTeamInvite } from "./team-invite-link.js";
import {
  resetNotificationFeed,
  syncNotifications,
} from "./notification-feed.js";
import { startNotificationPolling } from "./notification-polling.js";
import { GettingStarted } from "./getting-started.jsx";
import { completeGuideStep } from "./onboarding.js";
import { EventConflictReview, openSessionOutcome } from "./workload.jsx";
import { openInboxNote } from "./task-inbox-link.js";
import { askNora } from "./agent-handoff.js";
import { validateNewPassword } from "./password-policy.js";
import {
  authSetup,
  renderAuth,
  handleAuthAction,
  handleAuthSubmit,
} from "./auth.jsx";
import {
  filterTasks,
  taskById,
  hasPlanningDetails,
  openTask,
  openDetail,
  taskInput,
  captureModal,
  breakdownModal,
} from "./tasks.jsx";
import {
  sessionById,
  sessionDetail,
  eventDetail,
  eventModal,
  exceptionModal,
  planModal,
} from "./calendar.jsx";
import { categoryModal, adminUsers, adminStatus } from "./page-actions.jsx";
const routes = {
  profile: [
    "Trang cá nhân",
    "Chia sẻ đôi điều về bạn và kết nối với mọi người.",
    "users",
  ],
  friends: [
    "Bạn bè",
    "Tìm người quen, gửi lời mời và quản lý kết nối của bạn.",
    "users",
  ],
  teams: [
    "Nhóm",
    "Cùng nhận việc, giữ lịch cá nhân riêng tư.",
    "users",
  ],
  agent: [
    "NORA · Trợ lý AI",
    "Cùng NORA dành thời gian cho những điều quan trọng.",
    "spark",
  ],
  notes: [
    "Nhập việc",
    "Viết một ghi chú. Kiểm tra công việc và chọn lịch phù hợp với bạn.",
    "edit",
  ],
  today: [
    "Hôm nay",
    "Một góc nhìn rõ ràng về công việc và thời gian của bạn.",
    "today",
  ],
  tasks: [
    "Công việc",
    "Thêm việc, đặt hạn chót và cập nhật tiến độ khi làm xong.",
    "tasks",
  ],
  calendar: [
    "Lịch làm việc",
    "Bấm vào một khoảng trong lịch để bắt đầu, tạm dừng hoặc kết thúc lượt làm.",
    "calendar",
  ],
  time: [
    "Giờ làm việc",
    "Đặt giờ làm việc thường lệ hoặc chọn một tuần cụ thể để chỉnh.",
    "clock",
  ],
  insights: [
    "Tiến độ",
    "Theo dõi công suất, hạn chót và thời gian thực làm của cá nhân và nhóm.",
    "insights",
  ],
  scenario: [
    "Thử nhận thêm việc",
    "Nhập một việc giả định để kiểm tra thời gian trước khi nhận; lịch hiện tại chưa thay đổi.",
    "scenario",
  ],
  notifications: [
    "Thông báo",
    "Hạn chót, phiên làm việc và những thay đổi trong lịch của bạn.",
    "bell",
  ],
  settings: [
    "Cài đặt",
    "Thông tin cá nhân, danh mục công việc và bảo mật.",
    "settings",
  ],
  ...adminRoutes,
};
let loading = false,
  working = false;
function navigation(key) {
  location.hash = key;
  if (state.route === key) render();
}
function expiry() {
  if (!state.workspace) return;
  state.workspace = null;
  resetNotificationFeed();
  state.insights = null;
  state.user = null;
  state.roles = [];
  closeModal();
  history.replaceState(null, "", "#login");
  renderAuth();
  toast("Phiên đăng nhập đã kết thúc. Hãy đăng nhập lại.");
}
async function load(renderPage = true) {
  if (loading) return;
  loading = true;
  try {
    state.workspace = await api("/api/workspace");
    syncNotifications(state.workspace);
    syncPetProfile(state.workspace.gamification);
    state.loadedAt = Date.now();
    const input = {
      start: new Date().toISOString(),
      horizonDays: 7,
    };
    const [insights, conflicts, agent] = await Promise.allSettled([
      api("/api/intelligence", "POST", input),
      api("/api/plans/conflicts", "POST", input),
      api("/api/agent/capabilities"),
    ]);
    state.noraAvailable =
      agent.status === "fulfilled" && agent.value?.aiAvailable === true;
    state.insights = insights.status === "fulfilled" ? insights.value : null;
    state.conflicts = conflicts.status === "fulfilled" ? conflicts.value : null;
    state.analysisError =
      insights.status === "rejected" || conflicts.status === "rejected";
    window.dispatchEvent(new Event("workspace-updated"));
    if (renderPage) render();
  } finally {
    loading = false;
  }
}
async function loggedIn() {
  const data = await api("/api/browser-session/me");
  if (new URLSearchParams(location.search).has("googleCalendar"))
    state.settingsTab = "connections";
  state.user = data.user;
  state.roles = data.roles || [];
  if (pendingTeamInvite()) history.replaceState(null, "", "#teams");
  await load();
}
function render() {
  if (!state.workspace) return;
  const profileMatch = location.hash
    .slice(1)
    .match(
      /^profile\/([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})$/i,
    );
  const friendsMatch = location.hash
    .slice(1)
    .match(/^friends\/(people|friends|incoming|outgoing)$/);
  const taskMatch = location.hash.slice(1)
    .match(/^tasks\/([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})$/i);
  const projectMatch = location.hash.slice(1).match(/^tasks\/project\/([a-zA-Z0-9-]+)$/);
  state.socialFriendsTab = friendsMatch?.[1] || null;
  state.profileUserId = profileMatch?.[1] || null;
  state.route = profileMatch
    ? "profile"
    : friendsMatch
      ? "friends"
      : taskMatch || projectMatch ? "tasks" : location.hash.slice(1) || "today";
  if (projectMatch) state.taskScope = "projects";
  if (state.route === "admin") {
    state.route = "admin-users";
    history.replaceState(null, "", "#admin-users");
  }
  if (state.route === "getting-started") {
    setScreen(<GettingStarted signedIn />, "getting-started");
    return;
  }
  if (
    !routes[state.route] ||
    (isAdminRoute(state.route) && !state.roles.includes("Admin"))
  )
    state.route = "today";
  const [title, description] = routes[state.route];
  setScreen(
    workspaceShell(
      routes,
      title,
      description,
      <RoutePage
        key={state.route}
        route={state.route}
        projectId={projectMatch?.[1] || null}
        profileId={state.profileUserId}
        friendsTab={state.socialFriendsTab}
        onWorkspaceChanged={() =>
          load(state.route !== "agent" && state.route !== "notes")
        }
        onReady={
          ["admin-users", "admin-system"].includes(state.route)
            ? () => loadAdmin().catch((e) => toast(e.message, "error"))
            : undefined
        }
      />,
    ),
    state.route,
    { group: "workspace" },
  );
  setMobileMenu(false);
  syncDecorations();
  if (taskMatch) {
    history.replaceState(null, "", "#tasks");
    if (taskById(taskMatch[1])) openDetail(taskMatch[1]);
    else toast("Không tìm thấy công việc trong tài khoản này.", "error");
  }
}
async function loadAdmin(search = "") {
  if (state.route === "admin-users") {
    const users = await api(
      `/api/admin/users?search=${encodeURIComponent(search)}`,
    );
    if (state.route !== "admin-users") return;
    state.adminUsers = users;
    if ($("#admin-users")) setSlot("admin-users", adminUsers(users));
    return;
  }
  if (state.route !== "admin-system") return;
  const [status, jobs, metrics, health] = await Promise.all([
    api("/api/admin/status"),
    api("/api/admin/jobs"),
    api("/api/admin/metrics"),
    api("/api/admin/health"),
  ]);
  if (state.route !== "admin-system") return;
  if ($("#admin-jobs"))
    setSlot(
      "admin-jobs",
      <table>
        <thead>
          <tr>
            <th>Job</th>
            <th>Kết quả</th>
            <th>Bắt đầu</th>
            <th>Chi tiết</th>
          </tr>
        </thead>
        <tbody>
          {nodeList(
            jobs.map((j) => (
              <tr>
                <td>{j.jobType}</td>
                <td>
                  {badge(j.status, j.status === "FAILED" ? "red" : "green")}
                </td>
                <td>
                  {dateFmt(j.startedAt)} {timeFmt(j.startedAt)}
                </td>
                <td>{j.errorMessage || "—"}</td>
              </tr>
            )),
          )}
        </tbody>
      </table>,
    );
  if ($("#system-status"))
    setSlot("system-status", adminStatus({ ...status, ...metrics, health }));
}
async function scheduleTask(id, triggerEvent = "MANUAL") {
  const task = taskById(id);
  if (task && !hasPlanningDetails(task)) {
    openTask(task);
    return;
  }
  if (
    !state.workspace.availabilityPolicy?.confirmed &&
    !state.workspace.availability.length &&
    !state.workspace.exceptions.some((item) => item.isAvailable)
  ) {
    planOptions([id]);
    return;
  }
  const plan = await api("/api/plans", "POST", {
    start: new Date().toISOString(),
    horizonDays: state.workspace.preference.planningHorizonDays || 14,
    affectedTaskIds: [id],
    isReschedule: true,
    triggerEvent,
    strict: true,
  });
  planModal(plan);
}
function planOptions(affected = []) {
  const go = (route) => {
    closeModal();
    navigation(route);
  };
  if (affected.length === 1) {
    const task = taskById(affected[0]);
    if (task && !hasPlanningDetails(task)) {
      openTask(task);
      return;
    }
  }
  if (
    !state.workspace.tasks.some(
      (task) => hasPlanningDetails(task) && !["Completed", "Cancelled"].includes(task.status),
    )
  ) {
    modal(
      "Nhập việc trước khi tạo lịch",
      "Thêm công việc hoặc bổ sung hạn chót và thời gian dự kiến cho một việc đã lưu.",
      <div className="form-actions">
        <button
          type="button"
          className="btn primary"
          onClick={() => go("notes")}
        >
          Mở Ghi chú
        </button>
        <button
          type="button"
          className="btn secondary"
          onClick={() => {
            closeModal();
            openTask();
          }}
        >
          Tự thêm công việc
        </button>
      </div>,
    );
    return;
  }
  if (
    !state.workspace.availabilityPolicy?.confirmed &&
    !state.workspace.availability.length &&
    !state.workspace.exceptions.some((item) => item.isAvailable)
  ) {
    modal(
      "Thiết lập giờ làm",
      "Xác nhận giờ bạn có thể làm việc trước khi xếp lịch.",
      <button type="button" className="btn primary" onClick={() => go("time")}>
        Mở Giờ làm việc
      </button>,
    );
    return;
  }
  state.affected = affected;
  modal(
    affected.length ? "Lập lại lịch liên quan" : "Tạo kế hoạch mới",
    "Bạn sẽ được xem lại đề xuất trước khi áp dụng.",
    <form data-form="plan">
      <div className="form-grid">
        {field(
          "Bắt đầu",
          "start",
          "datetime-local",
          localInput(Date.now()),
          "required",
        )}
        {field(
          "Số ngày",
          "horizonDays",
          "number",
          state.workspace.preference.planningHorizonDays || 14,
          'required min="1" max="30"',
        )}
      </div>
      <label>
        <input name="useCapacityFactor" type="checkbox" defaultChecked /> Chừa
        thời gian dự phòng theo khả năng thực hiện
      </label>
      <label>
        <input name="useEstimateBias" type="checkbox" defaultChecked /> Dùng
        thời lượng đã cá nhân hóa
      </label>
      <p className="reason">
        Bỏ hai lựa chọn để giữ thời lượng đã nhập và dùng toàn bộ giờ rảnh như
        chế độ mặc định của NORA. Các giới hạn ngày, phiên, giờ nghỉ và deadline
        vẫn được giữ.
      </p>
      <p className="reason spaced">
        {affected.length
          ? "Giữ nguyên phiên không bị ảnh hưởng, phiên đã khóa và lịch sử thực hiện."
          : "Giữ nguyên phiên đã khóa hoặc đang chạy. Các phiên chưa khóa trong khoảng này sẽ được lập lại."}
      </p>
      <div className="form-error" />
      <div className="form-actions">
        <button className="btn primary" type="submit">
          Tạo đề xuất
        </button>
        {state.noraAvailable && (
          <button
            type="button"
            className="btn ghost"
            onClick={() =>
              askNora(
                "Giúp tôi xem phương án sắp xếp công việc. Chưa thay đổi lịch.",
              )
            }
          >
            Nhờ NORA
          </button>
        )}
      </div>
    </form>,
  );
}
function confirmAction(
  title,
  description,
  operation,
  confirmLabel = "Xác nhận",
) {
  state.confirm = operation;
  modal(
    title,
    description,
    <div className="form-actions">
      {button("Hủy", "close-modal", "ghost")}
      {button(confirmLabel, "confirm-action", "danger")}
    </div>,
  );
}
async function refreshAfter(message = "Đã lưu.") {
  closeModal();
  await load();
  toast(message, "success");
}
function completeTask(id) {
  return api(`/api/tasks/${id}/status`, "PATCH", { status: "Completed", progress: 100 });
}
async function action(name, id, element) {
  if (await handleAuthAction(name)) return;
  switch (name) {
    case "nav":
      navigation(id);
      break;
    case "calendar-date":
      if (/^\d{4}-\d{2}-\d{2}$/.test(id) && Number.isFinite(Date.parse(id))) {
        state.calendarStart = id;
        navigation("calendar");
      }
      break;
    case "work-hours-week":
      if (/^\d{4}-\d{2}-\d{2}$/.test(id) && Number.isFinite(Date.parse(id))) {
        state.workHoursWeek = id;
        navigation("time");
      }
      break;
    case "open-task-list":
      state.taskScope = id;
      navigation("tasks");
      break;
    case "reload-workspace":
      await load();
      break;
    case "settings-tab":
      state.settingsTab = id;
      render();
      break;
    case "calendar-connections":
      state.settingsTab = "connections";
      navigation("settings");
      break;
    case "mobile-menu":
      setMobileMenu(!$(".sidebar")?.classList.contains("open"));
      break;
    case "close-menu":
      setMobileMenu(false);
      break;
    case "search":
      setMobileMenu(false);
      openSearch();
      break;
    case "task-scope":
      state.taskScope = id;
      filterTasks();
      document.querySelectorAll('[data-action="task-scope"]').forEach((b) => {
        b.classList.toggle("active", b.dataset.id === id);
        b.setAttribute("aria-pressed", String(b.dataset.id === id));
      });
      break;
    case "calendar-view":
      state.calendarView = id;
      render();
      break;
    case "close-modal":
      closeModal();
      break;
    case "logout":
      await api("/api/browser-session/logout", "POST");
      expiry();
      break;
    case "logout-all":
      confirmAction(
        "Đăng xuất tất cả thiết bị?",
        "Tất cả phiên sẽ kết thúc ngay.",
        async () => {
          await api("/api/auth/logout-all", "POST");
          expiry();
        },
      );
      break;
    case "new-task":
      openTask({ startDate: element?.dataset.startDate });
      break;
    case "subtask":
      openTask({}, id);
      break;
    case "task-detail":
      openDetail(id);
      break;
    case "task-finish":
      await completeTask(id);
      await refreshAfter("Đã đánh dấu hoàn thành cả công việc.");
      break;
    case "toggle-task-completion": {
      const task = taskById(id);
      if (!task || task.isTeamTask || task.status === "Cancelled") break;
      const completed = task.status === "Completed";
      await api(`/api/tasks/${id}/status`, "PATCH", {
        status: completed ? "Todo" : "Completed",
        progress: completed ? 0 : 100,
      });
      const message = completed ? "Đã mở lại công việc." : "Đã hoàn thành công việc.";
      if (element?.dataset.keepTaskDetail) {
        await load(false);
        openDetail(id);
        toast(message, "success");
      } else await refreshAfter(message);
      if (element?.dataset.returnProject) openDetail(element.dataset.returnProject);
      break;
    }
    case "edit-task": {
      const task = taskById(id);
      if (!task) throw new Error("Công việc này không còn trong danh sách. Hãy tải lại dữ liệu.");
      openTask(task);
      break;
    }
    case "duplicate-task": {
      const task = taskById(id);
      if (!task) break;
      openTask({
        name: `${task.name.slice(0, 189)} (bản sao)`,
        description: task.description,
        estimatedMinutes: task.estimatedMinutes,
        deadline: Date.parse(task.deadline) >= Date.now() ? task.deadline : null,
        priority: task.priority,
        taskType: task.taskType,
        splittable: task.splittable,
        minimumSessionMinutes: task.minimumSessionMinutes,
        isHardDeadline: task.isHardDeadline,
      });
      break;
    }
    case "copy-task-link": {
      if (!taskById(id)) break;
      const link = new URL(location.href);
      link.hash = `tasks/${id}`;
      await navigator.clipboard.writeText(link.href);
      toast("Đã sao chép liên kết công việc.", "success");
      break;
    }
    case "reload-task":
      await load(false);
      if (taskById(id)) openTask(taskById(id));
      else {
        closeModal();
        render();
        toast("Công việc không còn tồn tại. Danh sách đã được cập nhật.");
      }
      break;
    case "delete-task": {
      const task = taskById(id);
      if (!task) break;
      const children = state.workspace.tasks.filter(
        (item) => item.parentId === id,
      );
      const ids = new Set([id, ...children.map((item) => item.id)]);
      const sessions = state.workspace.sessions.filter((session) =>
        ids.has(session.taskId),
      );
      if (
        sessions.some((session) =>
          ["Running", "Paused"].includes(session.status),
        )
      ) {
        modal(
          "Chưa thể xóa công việc",
          `“${task.name}” hoặc việc con đang có phiên chạy/tạm dừng. Hoàn thành hoặc bỏ qua phiên trước khi xóa.`,
          <div className="form-actions">
            {button("Đóng", "close-modal", "secondary")}
          </div>,
        );
        break;
      }
      const scheduled = sessions.filter(
        (session) => session.status === "Scheduled",
      ).length;
      confirmAction(
        "Xóa công việc?",
        `Xóa “${task.name}”${children.length ? ` và ${children.length} việc con` : ""}? ${scheduled ? `${scheduled} phiên chưa thực hiện sẽ bị hủy. ` : ""}Liên kết phụ thuộc với các việc này sẽ được gỡ. Lịch sử phiên đã thực hiện được giữ.`,
        async () => {
          await api(`/api/tasks/${id}`, "DELETE");
          await refreshAfter("Đã xóa công việc.");
        },
        "Xóa công việc",
      );
      break;
    }
    case "remove-dependency": {
      const [task, predecessor] = id.split("|");
      await api(`/api/tasks/${task}/dependencies/${predecessor}`, "DELETE");
      await load();
      openDetail(task);
      break;
    }
    case "capture":
      captureModal();
      break;
    case "breakdown":
      await breakdownModal(id);
      break;
    case "ai-breakdown":
      if (!$("#breakdown-ai")?.checked)
        throw new Error("Chọn cho phép gửi nội dung trước khi sử dụng AI.");
      {
        const result = await api(
          `/api/tasks/${id}/breakdown?useAi=true`,
          "POST",
        );
        state.breakdown = result;
        modal(
          "Gợi ý chia nhỏ",
          `Nguồn: ${["OpenAI", "Gemini", "VertexAI"].includes(result.source) ? result.source : "quy tắc (AI chưa khả dụng)"}`,
          <form data-form="breakdown" data-id={id}>
            {nodeList(
              result.subtasks.map((s, n) => (
                <label className="list-line checkbox">
                  <input
                    type="checkbox"
                    name="part"
                    defaultValue={n}
                    defaultChecked={true}
                  />
                  {s.name}
                  {" · "}
                  {s.estimatedMinutes}p
                </label>
              )),
            )}
            <div className="form-error" />
            <div className="form-actions">
              <button className="btn primary" type="submit">
                Lưu các việc con đã chọn
              </button>
            </div>
          </form>,
        );
      }
      break;
    case "new-category":
      categoryModal();
      break;
    case "edit-category":
      categoryModal(state.workspace.categories.find((c) => c.id === id));
      break;
    case "delete-category":
      confirmAction(
        "Xóa danh mục?",
        "Công việc trong danh mục sẽ chuyển thành chưa phân loại.",
        async () => {
          await api(`/api/categories/${id}`, "DELETE");
          await refreshAfter();
        },
      );
      break;
    case "new-event":
      eventModal({}, { manual: true });
      break;
    case "edit-event":
      eventModal(
        state.workspace.events.find((e) => e.id === id),
        { manual: true },
      );
      break;
    case "event-detail":
      eventDetail(id);
      break;
    case "delete-event": {
      const entry = state.workspace.events.find((e) => e.id === id);
      if (!entry) break;
      confirmAction(
        "Xóa Lịch Trình?",
        `Xóa “${entry.name}” ngày ${dateFmt(entry.start)} lúc ${timeFmt(entry.start)}? Chỉ xóa lần xuất hiện này và dừng các nhắc nhở của lịch này.`,
        async () => {
          await api(`/api/time/events/${id}`, "DELETE");
          await refreshAfter("Đã xóa Lịch Trình.");
        },
        "Xóa Lịch Trình",
      );
      break;
    }
    case "delete-session": {
      const session = sessionById(id);
      if (!session) break;
      confirmAction(
        "Xóa phiên khỏi lịch?",
        `Xóa phiên “${session.taskName}” ngày ${dateFmt(session.start)} lúc ${timeFmt(session.start)}? Công việc và deadline vẫn được giữ để xếp lại sau.`,
        async () => {
          await api(`/api/sessions/${id}`, "DELETE");
          await refreshAfter(
            "Đã xóa phiên khỏi lịch. Công việc và deadline vẫn được giữ.",
          );
        },
        "Xóa phiên",
      );
      break;
    }
    case "new-exception":
      exceptionModal();
      break;
    case "edit-exception":
      exceptionModal(state.workspace.exceptions.find((e) => e.id === id));
      break;
    case "delete-exception":
      await api(`/api/time/exceptions/${id}`, "DELETE");
      await refreshAfter();
      break;
    case "week-prev":
    case "week-next": {
      const d = new Date(`${state.calendarStart}T12:00:00Z`);
      d.setUTCDate(d.getUTCDate() + (name === "week-prev" ? -7 : 7));
      state.calendarStart = d.toISOString().slice(0, 10);
      render();
      break;
    }
    case "week-today":
      state.calendarStart = dayKey(Date.now());
      render();
      break;
    case "generate-plan":
      planOptions();
      break;
    case "replan-conflicts":
      planOptions([...new Set(state.conflicts.map((c) => c.taskId))]);
      break;
    case "replan-task":
      planOptions([id]);
      break;
    case "schedule-task":
      await scheduleTask(id);
      break;
    case "apply-plan":
      try {
        await api(`/api/plans/${id}/apply`, "POST");
      } catch (error) {
        if (!["PLAN_STALE", "PLAN_EXPIRED"].includes(error.code)) throw error;
        const request = state.plan?.id === id ? state.plan.request : null;
        await load(false);
        modal(
          "Cần xem lại lịch",
          error.message,
          <>
            <div className="form-error" />
            <div className="form-actions">
              {button("Đóng", "close-modal", "ghost")}
              {request ? (
                <form data-form="regenerate-plan">
                  <button type="submit" className="btn primary">
                    Tạo đề xuất mới
                  </button>
                  <div className="form-error" />
                </form>
              ) : (
                button("Mở tùy chọn lập lịch", "generate-plan", "primary")
              )}
            </div>
          </>,
        );
        break;
      }
      await refreshAfter("Đã áp dụng kế hoạch.");
      if (
        state.plan?.id === id &&
        state.plan.sessions.some((session) => !session.preserved)
      )
        completeGuideStep(2);
      navigation("today");
      break;
    case "session-outcome": {
      const session = state.workspace.sessions.find((item) => item.id === id);
      if (session) openSessionOutcome(session, () => load(false));
      break;
    }
    case "session-detail":
      sessionDetail(id);
      break;
    case "session-lock":
    case "session-unlock":
      await api(`/api/sessions/${id}/${name.slice(8)}`, "POST");
      await load();
      sessionDetail(id);
      break;
    case "session-start":
    case "session-pause":
    case "session-resume":
    case "session-skip":
    case "session-complete": {
      const s = sessionById(id);
      await api(`/api/sessions/${id}/${name.slice(8)}`, "POST");
      if (name === "session-start") completeGuideStep(3);
      await refreshAfter(
        name === "session-complete"
          ? "Đã ghi nhận phiên. Cập nhật tiến độ công việc để kế hoạch tiếp theo chính xác."
          : "Đã cập nhật phiên.",
      );
      if (name === "session-complete") openDetail(s.taskId, { afterSession: true });
      break;
    }
    case "discard-plan":
      await api(`/api/plans/${id}/discard`, "POST");
      closeModal();
      toast("Đã bỏ đề xuất.");
      break;
    case "plan-decisions":
      {
        const items = await api(`/api/plans/${id}/items`);
        modal(
          "Các quyết định trong đề xuất",
          "Lịch hiện tại chỉ thay đổi khi bạn áp dụng.",
          <>
            <div className="table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Công việc</th>
                    <th>Quyết định</th>
                    <th>Thời lượng</th>
                    <th>Lý do</th>
                  </tr>
                </thead>
                <tbody>
                  {nodeList(
                    items.map((x) => (
                      <tr>
                        <td>{taskById(x.taskId)?.name || "Công việc"}</td>
                        <td>
                          {{
                            ADD: "Thêm",
                            KEEP: "Giữ",
                            MOVE: "Dời",
                            RESIZE: "Đổi thời lượng",
                            REMOVE: "Bỏ",
                            UNSCHEDULED: "Chưa xếp",
                          }[x.action] || x.action}
                        </td>
                        <td>{x.plannedMinutes ?? "—"}</td>
                        <td>
                          {x.reasonCodes
                            .map((c) => reasons[c] || c)
                            .join(" · ")}
                        </td>
                      </tr>
                    )),
                  )}
                </tbody>
              </table>
            </div>
            {button("Quay lại đề xuất", "review-plan", "secondary", id)}
          </>,
        );
      }
      break;
    case "review-plan":
      planModal(await api(`/api/plans/${id}`));
      break;
    case "execution-history":
      {
        const x = await api(`/api/sessions/${id}/execution`);
        modal(
          "Lịch sử thực hiện",
          "Timer giữ nguyên khi bạn chỉnh thời lượng thực tế.",
          x ? (
            <>
              <p>
                {"Timer: "}
                {minutes(Math.floor(x.timerSeconds / 60))}
                {" · Thực tế: "}
                {minutes(Math.floor(x.actualSeconds / 60))}
                {x.isUserAdjusted ? " · Đã chỉnh sửa" : ""}
              </p>
              {nodeList(
                x.segments.map((s) => (
                  <div className="list-line">
                    {dateFmt(s.start)} {timeFmt(s.start)}
                    {" → "}
                    {s.end ? timeFmt(s.end) : "Đang chạy"}
                  </div>
                )),
              )}
            </>
          ) : (
            "Chưa bắt đầu phiên."
          ),
        );
      }
      break;
    case "notification-read":
      await api(`/api/notifications/${id}/read`, "POST");
      await load();
      if (state.route === "notifications") {
        const item = [
          ...document.querySelectorAll("[data-notification-id]"),
        ].find((item) => item.dataset.notificationId === id);
        const next =
          item?.querySelector(".notice-open") ||
          document.querySelector(
            '.notice-status-filters button[aria-pressed="true"]',
          );
        next?.focus();
      }
      break;
    case "notification-preferences":
      state.settingsTab = "notifications";
      navigation("settings");
      break;
    case "change-otp":
      await api("/api/auth/change-password/request-otp", "POST");
      toast("Mã xác minh đã được gửi đến email của bạn.");
      break;
    case "scenario-plan": {
      const p = state.scenario.proposedPlan;
      planModal(p, true);
      break;
    }
    case "scenario-create":
      openTask(state.scenarioTask);
      break;
    case "admin-account-history":
      {
        const logs = await api(`/api/admin/users/${id}/status-logs`);
        modal(
          "Lịch sử trạng thái tài khoản",
          "Các thao tác quản trị được lưu trong database.",
          nodeList(
            logs.map((l) => (
              <div className="list-line">
                <div>
                  <strong>{l.action === "LOCK" ? "Khóa" : "Mở khóa"}</strong>
                  <p>{l.reason || "Không có ghi chú"}</p>
                  <small>
                    {dateFmt(l.createdAt)} {timeFmt(l.createdAt)}
                  </small>
                </div>
              </div>
            )),
          ) || "Chưa có thao tác.",
        );
      }
      break;
    case "remove-avatar":
      setAvatar("");
      toast("Đã xóa ảnh. Bấm Lưu thông tin hồ sơ để hoàn tất.");
      break;
    case "admin-refresh":
      await loadAdmin();
      break;
    case "admin-lock": {
      const targetUser = state.adminUsers?.find((x) => x.id === id);
      const userDisplay = targetUser
        ? `${targetUser.firstName} ${targetUser.lastName} (${targetUser.email})`
        : "người dùng";
      modal(
        "Khóa tài khoản",
        `Xác nhận khóa tài khoản ${userDisplay}. Mọi phiên đăng nhập hiện tại sẽ bị thu hồi.`,
        <form data-form="admin-lock" data-id={id}>
          <div className="field">
            <label htmlFor="admin-lock-reason" style={{ fontWeight: 600 }}>
              Lý do khóa tài khoản <span style={{ color: "#ef4444" }}>*</span>
            </label>
            <textarea
              id="admin-lock-reason"
              name="reason"
              rows="3"
              className="filter"
              style={{
                width: "100%",
                padding: "0.6rem",
                borderRadius: "8px",
                resize: "vertical",
                marginTop: "0.25rem",
              }}
              placeholder="Nhập lý do khóa tài khoản (lý do này sẽ hiển thị cho người dùng khi họ đăng nhập)..."
              maxLength="500"
              required
              autoFocus
            />
          </div>
          <div
            className="actions"
            style={{
              display: "flex",
              gap: "0.5rem",
              justifyContent: "flex-end",
              marginTop: "1rem",
            }}
          >
            <button
              type="button"
              className="btn ghost"
              data-action="close-modal"
            >
              Hủy
            </button>
            <button type="submit" className="btn danger">
              Khóa tài khoản
            </button>
          </div>
        </form>,
      );
      break;
    }
    case "admin-unlock":
      confirmAction(
        "Mở khóa tài khoản?",
        "Cho phép tài khoản hoạt động lại bình thường.",
        async () => {
          await api(`/api/admin/users/${id}/unlock`, "POST", {
            reason: "Mở khóa bởi quản trị viên",
          });
          closeModal();
          await loadAdmin();
          toast("Đã mở khóa tài khoản thành công.");
        },
      );
      break;
    case "confirm-action": {
      const op = state.confirm;
      state.confirm = null;
      closeModal();
      if (op) await op();
      break;
    }
  }
}
async function formAction(form, data, nextAction = null) {
  const id = form.dataset.id;
  switch (form.dataset.form) {
    case "regenerate-plan": {
      const request = state.plan.request;
      const start = new Date(
        Math.max(Date.parse(request.start), Date.now()),
      ).toISOString();
      planModal(
        await api(
          request.isReschedule ? "/api/plans/reschedule" : "/api/plans",
          "POST",
          { ...request, start },
        ),
      );
      break;
    }
    case "admin-lock": {
      const reason = String(data.get("reason") || "").trim();
      if (!reason) throw new Error("Vui lòng nhập lý do khóa tài khoản.");
      await api(`/api/admin/users/${id}/lock`, "POST", { reason });
      closeModal();
      await loadAdmin();
      toast("Đã khóa tài khoản thành công.", "success");
      break;
    }
    case "task-batch": {
      const raw = data.get("batch");
      if (!raw) throw new Error("Phân tích tài liệu để có danh sách công việc.");
      const batch = JSON.parse(raw);
      const deadlineValue = (value) => value && !value.includes("T") ? toUtc(`${value}T23:59`) : value || null;
      const projectDeadline = deadlineValue(batch.projectDeadline);
      const tasks = batch.tasks.map((task) => ({
        ...task,
        deadline: deadlineValue(task.deadline),
      }));
      if (nextAction === "preview" && tasks.some((task) => !hasPlanningDetails({ ...task, deadline: task.deadline || projectDeadline })))
        throw new Error("Nhập hạn chót của project và thời lượng từng việc trước khi xếp lịch, hoặc chọn Lưu project.");
      const source = form.elements.namedItem("source")?.files?.[0];
      const sourceFile = source?.size ? await readTeamBriefFile(source, { allowCsv: true }) : null;
      const saved = await api("/api/tasks/batch", "POST", { ...batch, projectDeadline, tasks, sourceFile });
      completeGuideStep(0);
      await refreshAfter("Đã lưu project và các công việc.");
      if (nextAction === "preview") {
        try {
          const plan = await api("/api/plans", "POST", {
            start: new Date().toISOString(),
            horizonDays: state.workspace.preference.planningHorizonDays || 14,
            affectedTaskIds: saved.taskIds,
            triggerEvent: "NEW_TASK",
            strict: true,
            preserveExisting: true,
          });
          planModal(plan);
        } catch (error) {
          toast(`Project đã lưu. Chưa tạo được lịch: ${error.message}`, "error");
        }
      }
      break;
    }
    case "complete-checklist-task": {
      const task = taskById(id);
      if (!task || task.isTeamTask || task.status === "Cancelled" ||
          !task.checklist?.length || !task.checklist.every((item) => item.completed)) {
        throw new Error("Checklist đã thay đổi. Hãy kiểm tra lại các mục trước khi hoàn thành công việc.");
      }
      const detailKey = form.closest("#modal") ? getSnapshot().modalKey : null;
      const confirmation = form.closest("dialog");
      if (task.status !== "Completed") await completeTask(id);
      await load(false);
      if (confirmation?.open) confirmation.close();
      const { modal: currentModal, modalKey } = getSnapshot();
      if (currentModal && detailKey === modalKey && taskById(id)) openDetail(id, { initialTab: "checklist" });
      toast("Đã hoàn thành công việc.", "success");
      break;
    }
    case "task": {
      if (form.dataset.stale === "true")
        throw new Error("Tải lại bản mới trước khi chỉnh sửa công việc.");
      const input = taskInput(data);
      if (nextAction === "preview" && !hasPlanningDetails(input))
        throw new Error("Bổ sung hạn chót và thời gian dự kiến để xếp lịch, hoặc chọn Lưu công việc.");
      const markComplete = !id && data.has("markComplete");
      let saved;
      try {
        saved = await api(
          `/api/tasks${id ? `/${id}` : ""}`,
          id ? "PUT" : "POST",
          input,
        );
      } catch (error) {
        if (id && ["TASK_STALE", "DATA_STALE"].includes(error.code)) {
          form.dataset.stale = "true";
          setSlot(
            "task-conflict",
            <div className="info section-gap">
              <p>
                Công việc đã được cập nhật ở nơi khác. Bạn có thể giữ nội dung
                đang nhập để đối chiếu, hoặc tải bản mới để chỉnh sửa lại.
              </p>
              <p>Khi tải bản mới, nội dung trong form sẽ được thay thế.</p>
              <button
                type="button"
                className="btn ghost"
                data-action="reload-task"
                data-id={id}
              >
                Tải bản mới
              </button>
            </div>,
          );
        }
        throw error;
      }
      if (!id) {
        completeGuideStep(0);
        if (state.route === "tasks" && state.taskScope !== "all") state.taskScope = "personal";
      }
      let completionError;
      if (markComplete) {
        try {
          await api(`/api/tasks/${saved.id}/status`, "PATCH", {
            status: "Completed",
            progress: 100,
          });
        } catch (error) {
          completionError = error;
        }
      }
      await refreshAfter(markComplete && !completionError
        ? "Đã lưu và đánh dấu hoàn thành."
        : "Đã lưu công việc.");
      if (completionError) {
        toast(`Công việc đã lưu. Chưa đánh dấu hoàn thành: ${completionError.message}`, "error");
      }
      if (!id && (nextAction === "open" || completionError)) openDetail(saved.id);
      if (!id && nextAction === "preview") {
        try {
          await scheduleTask(saved.id, "NEW_TASK");
        } catch (error) {
          toast(`Công việc đã lưu. Chưa xếp được lịch: ${error.message}`);
        }
      }
      break;
    }
    case "task-progress":
      await api(`/api/tasks/${id}/status`, "PATCH", {
        status:
          Number(data.get("progress")) === 100 &&
          data.get("status") !== "Cancelled"
            ? "Completed"
            : String(data.get("status")),
        progress: Number(data.get("progress")),
      });
      await refreshAfter("Đã cập nhật tiến độ.");
      break;
    case "dependency":
      if (!data.get("predecessor"))
        throw new Error("Chọn một công việc tiền nhiệm.");
      await api(
        `/api/tasks/${id}/dependencies/${data.get("predecessor")}`,
        "POST",
      );
      await load();
      openDetail(id);
      break;
    case "capture": {
      const content = String(data.get("text")).trim();
      if (!content) throw new Error("Nhập nội dung công việc.");
      const note = await api("/api/notes", "POST", {
        title: content.split("\n")[0].slice(0, 120),
        content,
      });
      closeModal();
      openInboxNote(note.id);
      break;
    }
    case "breakdown": {
      const selected = data.getAll("part");
      if (!selected.length) throw new Error("Chọn ít nhất một việc con.");
      let saved = 0;
      try {
        for (const index of selected) {
          await api("/api/tasks", "POST", {
            ...state.breakdown.subtasks[Number(index)],
            parentId: id,
            categoryId: taskById(id).categoryId,
            creationSource: "AI_BREAKDOWN",
          });
          saved++;
          const checkbox = form.querySelector(`input[value="${index}"]`);
          checkbox.checked = false;
          checkbox.disabled = true;
        }
      } catch (e) {
        await load(false);
        throw new Error(
          `Đã lưu ${saved} việc con. ${e.message} Bạn có thể thử lại các mục chưa lưu.`,
        );
      }
      await refreshAfter(`Đã thêm ${saved} công việc con.`);
      break;
    }
    case "category":
      await api(`/api/categories${id ? `/${id}` : ""}`, id ? "PUT" : "POST", {
        name: String(data.get("name")),
        color: String(data.get("color")),
      });
      await refreshAfter();
      break;
    case "availability": {
      const weekStart = data.get("weekStart");
      const url = weekStart
        ? `/api/time/availability/weeks/${encodeURIComponent(weekStart)}`
        : "/api/time/availability";
      if (weekStart && nextAction === "reset") {
        await api(url, "DELETE");
        await refreshAfter(
          state.workspace.availabilityPolicy?.timetableWeeks?.includes(weekStart)
            ? "Đã dùng lại giờ làm việc theo thời khóa biểu."
            : "Tuần đã chọn đã quay về giờ làm việc thường lệ.",
        );
        break;
      }
      const starts = data.getAll("start");
      const ends = data.getAll("end");
      const slots = data.getAll("day").map((day, index) => {
        if (!starts[index] || !ends[index])
          throw new Error("Nhập giờ bắt đầu và giờ kết thúc cho mỗi khung.");
        const startMinute = minuteValue(String(starts[index]));
        const endMinute = ends[index] === "00:00" ? 1440 : minuteValue(String(ends[index]));
        if (!(startMinute < endMinute && endMinute <= 1440))
          throw new Error("Giờ kết thúc phải sau giờ bắt đầu trong cùng ngày.");
        return { dayOfWeek: Number(day), startMinute, endMinute };
      });
      if (!weekStart && slots.length > 28)
        throw new Error("Bạn có thể lưu tối đa 28 khoảng giờ làm việc thường lệ.");
      await api(url, "PUT", {
        slots,
        ...(data.get("weekRevision") ? { revision: data.get("weekRevision") } : {}),
      });
      if (slots.length) completeGuideStep(1);
      await refreshAfter(weekStart ? "Đã lưu giờ làm việc cho tuần đã chọn." : "Đã lưu giờ làm việc.");
      if (nextAction === "plan") {
        if (weekStart) await action("calendar-date", weekStart);
        else if (slots.length) planOptions();
      }
      break;
    }
    case "preferences":
      await api("/api/time/preferences", "PUT", {
        timeZoneId: String(data.get("timeZoneId")),
        minimumSessionMinutes: Number(data.get("minimumSessionMinutes")),
        maximumSessionMinutes: Number(data.get("maximumSessionMinutes")),
        breakMinutes: Number(data.get("breakMinutes")),
        preferredStartMinute: minuteValue(String(data.get("preferredStart"))),
        preferredEndMinute: minuteValue(String(data.get("preferredEnd"))),
        planningHorizonDays: Number(data.get("planningHorizonDays")),
        preferredPeriod: String(data.get("preferredPeriod")),
        maximumDailyMinutes: Number(data.get("maximumDailyMinutes")),
        autoReschedule: data.get("autoReschedule") === "true",
      });
      await refreshAfter();
      break;
    case "exception":
      await api(
        `/api/time/exceptions${id ? `/${id}` : ""}`,
        id ? "PUT" : "POST",
        {
          date: String(data.get("date")),
          startMinute: minuteValue(String(data.get("start"))),
          endMinute: minuteValue(String(data.get("end"))),
          isAvailable: data.get("isAvailable") === "true",
          note: String(data.get("note")),
        },
      );
      await refreshAfter();
      break;
    case "event": {
      const start = toUtc(String(data.get("start")));
      const end = toUtc(String(data.get("end")));
      if (!id && Date.parse(start) <= Date.now())
        throw new Error(
          "Không thể tạo lịch trong quá khứ. Hãy chọn giờ bắt đầu sau thời điểm hiện tại.",
        );
      if (Date.parse(end) <= Date.parse(start))
        throw new Error("Giờ kết thúc phải sau giờ bắt đầu.");
      const input = {
        name: String(data.get("name")),
        start,
        end,
        description: String(data.get("description") || ""),
        eventType: String(data.get("eventType") || "OTHER"),
        repeatWeeks: Number(data.get("repeatWeeks") || 1),
        isUrgent: data.get("isUrgent") === "on",
      };
      try {
        await api(
          `/api/time/events${id ? `/${id}` : ""}`,
          id ? "PUT" : "POST",
          input,
        );
      } catch (error) {
        if (id || input.repeatWeeks !== 1 || error.code !== "SESSION_OVERLAP")
          throw error;
        modal(
          "Lịch Trình trùng phiên đã xếp",
          error.message,
          <EventConflictReview
            input={input}
            onBack={() => eventModal(input, { manual: true })}
            onSaved={() => refreshAfter("Đã lưu Lịch Trình và cập nhật phiên đã xếp.")}
          />,
        );
        return;
      }
      await refreshAfter();
      break;
    }
    case "plan":
      planModal(
        await api(
          state.affected?.length ? "/api/plans/reschedule" : "/api/plans",
          "POST",
          {
            start: toUtc(String(data.get("start"))),
            horizonDays: Number(data.get("horizonDays")),
            useCapacityFactor: data.get("useCapacityFactor") === "on",
            useEstimateBias: data.get("useEstimateBias") === "on",
            affectedTaskIds: state.affected?.length ? state.affected : null,
            strict: true,
          },
        ),
      );
      break;
    case "session-edit":
      await api(`/api/sessions/${id}`, "PUT", {
        start: toUtc(String(data.get("start"))),
        end: toUtc(String(data.get("end"))),
      });
      await refreshAfter();
      break;
    case "session-split":
      await api(`/api/sessions/${id}/split`, "POST", {
        firstMinutes: Number(data.get("firstMinutes")),
      });
      await refreshAfter();
      break;
    case "actual-duration":
      await api(`/api/sessions/${id}/actual-duration`, "PUT", {
        actualMinutes: Number(data.get("actualMinutes")),
      });
      await refreshAfter();
      break;
    case "notification-settings":
      await api("/api/notifications/settings", "PUT", {
        inAppEnabled: data.has("inAppEnabled"),
        emailEnabled: data.has("emailEnabled"),
        deadlineLeadHours: Number(data.get("deadlineLeadHours")),
        sessionReminderLeadMinutes: Number(
          data.get("sessionReminderLeadMinutes"),
        ),
        workloadWarningLevel: String(data.get("workloadWarningLevel")),
      });
      await refreshAfter();
      break;
    case "profile":
      await api("/api/workspace/profile", "PUT", {
        firstName: String(data.get("firstName")),
        lastName: String(data.get("lastName")),
        displayName: String(data.get("displayName")),
        bio: String(data.get("bio") || ""),
        avatarUrl: String(data.get("avatarUrl") || ""),
      });
      await refreshAfter("Đã cập nhật thông tin hồ sơ.");
      break;
    case "password":
      validateNewPassword(
        String(data.get("newPassword") || ""),
        state.user?.email || "",
        String(data.get("currentPassword") || ""),
      );
      if (data.get("newPassword") !== data.get("confirmNewPassword")) {
        throw new Error("Mật khẩu xác nhận không khớp.");
      }
      await api(
        form.dataset.passwordMode === "setup"
          ? "/api/auth/set-password"
          : "/api/auth/change-password",
        "POST",
        Object.fromEntries(data),
      );
      await api("/api/browser-session/logout", "POST");
      expiry();
      toast(
        form.dataset.passwordMode === "setup"
          ? "Đã thiết lập mật khẩu. Bạn có thể đăng nhập bằng email hoặc Google."
          : "Mật khẩu đã đổi. Hãy đăng nhập lại.",
        "success",
      );
      break;
    case "scenario": {
      state.scenarioTask = {
        ...taskInput(data),
        splittable: true,
        minimumSessionMinutes: 25,
      };
      state.scenario = await api("/api/plans/scenario", "POST", {
        plan: {
          start: new Date().toISOString(),
          horizonDays: 7,
        },
        newTask: state.scenarioTask,
      });
      render();
      break;
    }
    case "admin-search":
      await loadAdmin(String(data.get("search")));
      break;
  }
}
export async function onClick(event) {
  const el = event.target.closest("[data-action]");
  if (!el || working) return;
  working = true;
  el.disabled = true;
  try {
    await action(el.dataset.action, el.dataset.id, el);
  } catch (e) {
    toast(e.message, "error");
  } finally {
    working = false;
    if (el.isConnected) el.disabled = false;
  }
}
export async function onSubmit(event) {
  const form = event.target;
  if (!form.dataset.form) return;
  event.preventDefault();
  if (working) return;
  working = true;
  try {
    if (!(await handleAuthSubmit(form)))
      await submit(form, (data) =>
        formAction(
          form,
          data,
          event.nativeEvent?.submitter?.dataset.nextAction ??
            event.submitter?.dataset.nextAction,
        ),
      );
  } catch {
    /* submit renders the error beside the form. */
  } finally {
    working = false;
  }
}
export function onInput(event) {
  if (event.target.id === "task-search") filterTasks();
  if (event.target.id === "workspace-search") filterSearch();
}
export function onChange(event) {
  if (event.target.id === "calendar-jump-date") {
    if (event.target.value) void action("calendar-date", event.target.value);
    return;
  }
  if (
    event.target.id === "avatar-file-input" ||
    event.target.classList.contains("avatar-file-trigger")
  ) {
    const file = event.target.files[0];
    if (!file) return;
    if (file.size > 8 * 1024 * 1024) {
      toast("Kích thước ảnh tối đa 8MB.", "error");
      return;
    }
    const reader = new FileReader();
    reader.onload = (e) => {
      openAvatarCropper(e.target.result);
      event.target.value = "";
    };
    reader.readAsDataURL(file);
  }
  if (
    [
      "task-status",
      "task-category",
      "task-priority",
      "task-from",
      "task-to",
    ].includes(event.target.id)
  )
    filterTasks();
}
export function startController() {
  if (
    ["returned", "cancelled"].includes(
      new URLSearchParams(location.search).get("payment"),
    )
  ) {
    state.settingsTab = "pets";
    history.replaceState(null, "", "#settings");
  }
  captureTeamInvite();
  resetNotificationFeed();
  const stopNotificationPolling = startNotificationPolling(
    () => state.workspace,
  );
  let disposed = false,
    connecting = false;
  const entryLogin = () => {
    history.pushState(null, "", "#login");
    renderAuth("login");
  };
  const publicPage = () => {
    if (disposed) return;
    const route = location.hash.slice(1);
    if (
      ["login", "register", "forgot", "verify", "forgotOtp", "reset"].includes(
        route,
      )
    )
      renderAuth(route);
    else if (pendingTeamInvite()) renderAuth("login");
    else setScreen(<GettingStarted onLogin={entryLogin} />, "getting-started");
  };
  const connect = async () => {
    if (connecting || disposed) return;
    connecting = true;
    setScreen(<Loading fullscreen />, "loading");
    try {
      await loggedIn();
    } catch (error) {
      if (disposed) return;
      if (error.status === 401) publicPage();
      else
        setScreen(
          <ConnectionState onRetry={connect} onLogin={entryLogin} />,
          "connection-error",
        );
    } finally {
      connecting = false;
    }
  };
  const hash = () => {
    captureTeamInvite();
    if (state.workspace) render();
    else publicPage();
  };
  window.addEventListener("hashchange", hash);
  window.addEventListener("session-expired", expiry);
  authSetup(loggedIn);
  const poll = setInterval(() => {
    if (
      state.workspace &&
      !document.hidden &&
      !document.querySelector("#modal")?.open &&
      !working &&
      !isAdminRoute(state.route) &&
      ![
        "time",
        "settings",
        "scenario",
        "tasks",
        "notes",
        "agent",
        "getting-started",
      ].includes(state.route)
    )
      load().catch((e) => {
        if (state.workspace) toast(e.message, "error");
      });
  }, 30000);
  void connect();
  return () => {
    disposed = true;
    stopNotificationPolling();
    clearInterval(poll);
    window.removeEventListener("hashchange", hash);
    window.removeEventListener("session-expired", expiry);
  };
}
