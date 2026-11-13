import { useCallback, useEffect, useRef, useState } from "react";
import { state, dateFmt, icon, statusLabels } from "./ui.jsx";
import { api } from "./api.js";
import { taskColumns, taskRows } from "./tasks.jsx";
import { TaskInbox } from "./task-inbox.jsx";
import { ProjectList, projectColumns } from "./task-projects.jsx";
import { ProjectWorkspace } from "./project-workspace.jsx";
import { activeTask, isInboxNote, smartTaskLists } from "./task-smart-lists.js";
import { PopupDetails } from "./popup-dismissal.jsx";
import "./task-smart-list.css";

const lists = {
  personal: { label: "Việc của tôi" },
  projects: { label: "Projects" },
  all: { label: "Tất cả công việc" },
  inbox: {
    label: "Ghi nháp",
  },
  today: {
    label: "Hôm nay",
  },
  week: {
    label: "7 ngày tới",
  },
};
const emptyFilters = {
  status: "",
  category: "",
  priority: "",
  from: "",
  to: "",
};

function TodayTaskPane({ model }) {
  const overdueCount = model.todayTasks.filter(model.isOverdue).length;
  const total = model.todayCompleted + model.todayTasks.length;
  const progressLabel = `Đã hoàn thành ${model.todayCompleted}/${total} việc hôm nay`;
  return (
    <aside className="task-today-pane" aria-label="Việc hôm nay">
      <header>
        <button type="button" className="task-today-tool" data-action="new-task"
          aria-label="Mở biểu mẫu công việc từ Hôm nay" title="Thêm công việc" aria-haspopup="dialog">
          {icon("plus")}
        </button>
        <div className="task-today-date">
          <h2>Hôm nay <small>{dateFmt(`${model.today}T12:00:00Z`, { timeZone: "UTC" })}</small></h2>
          <progress className="task-today-progress" max={total || 1} value={model.todayCompleted}
            aria-label={progressLabel} title={progressLabel} />
        </div>
        <button type="button" className="task-today-tool" data-action="nav" data-id="calendar"
          aria-label="Mở lịch" title="Mở lịch">
          {icon("clock")}
        </button>
        <span className="task-today-warning" role="status" title={overdueCount ? `${overdueCount} việc quá hạn` : undefined}>
          {overdueCount > 0 && <>{icon("alertTriangle")}<span>{overdueCount} việc quá hạn</span></>}
        </span>
        <button type="button" className="task-today-tool" data-action="nav" data-id="today"
          aria-label="Mở trang Hôm nay" title="Mở trang Hôm nay">
          {icon("expand")}
        </button>
      </header>
      {model.todayTasks.length ? (
        <ul className="task-today-list">
          {model.todayTasks.map((task) => {
            const dueId = model.dayOf(task.deadline) ? `task-today-due-${task.id}` : undefined;
            const overdue = model.isOverdue(task);
            return (
              <li key={task.id}>
                {task.isTeamTask ? <span className="task-today-team" title="Công việc nhóm">{icon("users")}</span> : (
                  <button type="button" className="task-complete-toggle" role="checkbox" aria-checked={false}
                    aria-label={`Đánh dấu xong ${task.name} trong Hôm nay`}
                    data-action="toggle-task-completion" data-id={task.id} />
                )}
                <button type="button" className="task-today-title" data-action="task-detail" data-id={task.id}
                  aria-label={`Xem ${task.name} trong Hôm nay`} aria-describedby={dueId}>
                  <span>{task.name}</span>
                  {dueId && (
                    <time id={dueId} className="task-today-due" dateTime={task.deadline} data-overdue={overdue}>
                      {overdue ? "Quá hạn" : "Hạn"} {dateFmt(task.deadline)}
                    </time>
                  )}
                </button>
              </li>
            );
          })}
        </ul>
      ) : <p>Chưa có công việc cần làm hôm nay.</p>}
    </aside>
  );
}

export function TaskSmartList({ onShowMatrix, onWorkspaceChanged, projectId = null }) {
  const [openedProject, setOpenedProject] = useState(projectId);
  useEffect(() => { setOpenedProject(projectId); }, [projectId]);
  const [scope, setScope] = useState(() =>
    lists[state.taskScope] ? state.taskScope : "personal",
  );
  const [columns, setColumns] = useState(() => Object.keys(taskColumns));
  const [visibleProjectColumns, setProjectColumns] = useState(["progress", "owner"]);
  const [selectedTaskId, setSelectedTaskId] = useState(null);
  const [toolbarHost, setToolbarHost] = useState(null);
  const [query, setQuery] = useState("");
  const [searchOpen, setSearchOpen] = useState(false);
  const searchTrigger = useRef(null);
  const [filters, setFilters] = useState(emptyFilters);
  const [notes, setNotes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [now, setNow] = useState(Date.now);
  const epoch = useRef(0),
    alive = useRef(false);
  const workspace = state.workspace,
    uid = workspace?.profile?.id;
  const refresh = useCallback(async () => {
    const request = ++epoch.current;
    try {
      const items = await api("/api/notes");
      if (
        alive.current &&
        request === epoch.current &&
        state.workspace?.profile?.id === uid
      ) {
        setNotes(Array.isArray(items) ? items : []);
        setError("");
      }
    } catch (failure) {
      if (alive.current && request === epoch.current) setError(failure.message);
    } finally {
      if (alive.current && request === epoch.current) setLoading(false);
    }
  }, [uid]);
  useEffect(() => {
    alive.current = true;
    setLoading(true);
    refresh();
    const visible = () => {
      if (!document.hidden) {
        setNow(Date.now());
        refresh();
      }
    };
    const timer = setInterval(visible, 60000);
    window.addEventListener("focus", visible);
    const updated = () => {
      if (lists[state.taskScope]) setScope(state.taskScope);
      visible();
    };
    window.addEventListener("workspace-updated", updated);
    document.addEventListener("visibilitychange", visible);
    return () => {
      alive.current = false;
      epoch.current++;
      clearInterval(timer);
      window.removeEventListener("focus", visible);
      window.removeEventListener("workspace-updated", updated);
      document.removeEventListener("visibilitychange", visible);
    };
  }, [refresh]);
  const nextDeadline = (workspace.tasks || []).reduce((next, task) => {
    const deadline = Date.parse(task.deadline);
    return activeTask(task) && deadline > now ? Math.min(next, deadline) : next;
  }, Infinity);
  useEffect(() => {
    if (nextDeadline - now > 60000) return;
    const timer = setTimeout(() => setNow(Date.now()), Math.max(1, nextDeadline - Date.now()));
    return () => clearTimeout(timer);
  }, [nextDeadline, now]);
  const model = smartTaskLists(workspace, now);
  const inbox = notes.filter(isInboxNote);
  const selected = lists[scope];
  const isDrafts = scope === "inbox";
  const isProjects = scope === "projects";
  const isFlat = scope === "personal" || scope === "all";
  const incomplete = filters.status === "active";
  function choose(key) {
    state.taskScope = key;
    setScope(key);
    setSelectedTaskId(null);
    setNow(Date.now());
    if ((key === "today" || key === "week") && ["Completed", "Cancelled"].includes(filters.status))
      setFilters((current) => ({ ...current, status: "" }));
  }
  function matches(task) {
    return (
      `${task.name} ${task.description || ""}`
        .toLocaleLowerCase("vi")
        .includes(query.trim().toLocaleLowerCase("vi")) &&
      (!filters.status || (filters.status === "active"
        ? !["Completed", "Cancelled"].includes(task.status)
        : task.status === filters.status)) &&
      (!filters.category || task.categoryId === filters.category) &&
      (!filters.priority || task.priority === Number(filters.priority)) &&
      (!filters.from || (task.deadline && model.dayOf(task.deadline) >= filters.from)) &&
      (!filters.to || (task.deadline && model.dayOf(task.deadline) <= filters.to))
    );
  }
  const sourceGroups =
    scope === "today"
      ? model.todayGroups
      : scope === "week"
        ? model.weekGroups
        : [{ key: "all", tasks: scope === "personal" ? model.personalInbox : isProjects ? model.projects : model.all }];
  const groups = sourceGroups
    .map((g) => ({ ...g, projectContext: isFlat ? g.tasks : undefined, tasks: g.tasks.filter(matches) }))
    .filter((g) => isFlat || g.tasks.length);
  const count = groups.reduce((sum, g) => sum + g.tasks.length, 0);
  const filtered =
    !!query || (!isDrafts && Object.values(filters).some(Boolean));
  const setFilter = (key, value) =>
    setFilters((current) => ({ ...current, [key]: value }));
  const addTask = (
    <button type="button" className="task-quick-add-button" data-action="new-task" aria-haspopup="dialog">
      {icon("plus")} Thêm công việc…
    </button>
  );
  return (
    <>
    {openedProject && <ProjectWorkspace key={openedProject} projectId={openedProject} workspace={workspace} onWorkspaceChanged={onWorkspaceChanged} onBack={() => {
      choose("projects"); setOpenedProject(null); location.hash = "tasks";
    }} />}
    <div hidden={!!openedProject}>
    <div className="task-smart-layout">
      <TodayTaskPane model={model} />
      <nav className="task-smart-nav" aria-label="Danh sách thông minh">
        <button type="button" className={`task-smart-nav-item${scope === "personal" ? " is-active" : ""}`}
          aria-pressed={scope === "personal"} onClick={() => choose("personal")}>
          {icon("mail")} Inbox
        </button>
        <button type="button" className={`task-smart-nav-item${isProjects ? " is-active" : ""}`}
          aria-pressed={isProjects} onClick={() => choose("projects")}>
          {icon("folder")} Projects
        </button>
        <button type="button" className={`task-smart-nav-item${["all", "today", "week"].includes(scope) ? " is-active" : ""}`}
          aria-pressed={["all", "today", "week"].includes(scope)} onClick={() => choose("all")}>
          {icon("tasks")} Công việc
        </button>
        <button type="button" className={`task-smart-nav-item${isDrafts ? " is-active" : ""}`}
          aria-pressed={isDrafts} onClick={() => choose("inbox")}>
          {icon("edit")} Ghi nháp
        </button>
        {onShowMatrix && (
          <button type="button" className="task-smart-nav-item" onClick={onShowMatrix}>
            {icon("spark")} Eisenhower
          </button>
        )}
      </nav>
      <section
        className="task-smart-main"
        aria-label={`Nội dung ${scope === "personal" ? "Inbox" : selected.label}`}
      >
        <div className="task-smart-toolbar" ref={setToolbarHost}>
          {!isDrafts && (
            <PopupDetails className="task-work-popup task-work-scope">
              <summary aria-label={`Lọc danh sách · ${selected.label}`}>{selected.label}</summary>
              <div>
                {!isProjects && <div className="task-work-scope-options" aria-label="Phạm vi công việc">
                  {Object.entries(lists).filter(([key]) => !["inbox", "projects"].includes(key)).map(([key, list]) => (
                    <button key={key} type="button" aria-pressed={scope === key}
                      onClick={(event) => {
                        const disclosure = event.currentTarget.closest("details");
                        choose(key);
                        disclosure.open = false;
                        disclosure.querySelector("summary").focus();
                      }}>
                      {list.label}
                    </button>
                  ))}
                </div>}
                <select aria-label="Lọc trạng thái" className="filter" value={filters.status}
                  onChange={(event) => setFilter("status", event.target.value)}>
                  <option value="">Tất cả trạng thái</option>
                  <option value="active">Chưa hoàn thành</option>
                  {(isFlat || isProjects ? ["Todo", "InProgress", "Completed", "Cancelled"] : ["Todo", "InProgress"])
                    .map((key) => <option value={key} key={key}>{statusLabels[key]}</option>)}
                </select>
                  <select
                    aria-label="Lọc danh mục"
                    className="filter"
                    value={filters.category}
                    onChange={(event) =>
                      setFilter("category", event.target.value)
                    }
                  >
                    <option value="">Danh mục công việc</option>
                    {(workspace.categories || []).map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                  <select
                    aria-label="Lọc mức ưu tiên"
                    className="filter"
                    value={filters.priority}
                    onChange={(event) =>
                      setFilter("priority", event.target.value)
                    }
                  >
                    <option value="">Ưu tiên</option>
                    {[4, 3, 2, 1].map((priority) => (
                      <option key={priority} value={priority}>
                        {["", "Thấp", "Vừa", "Cao", "Khẩn"][priority]}
                      </option>
                    ))}
                  </select>
                  <label className="field">
                    Từ ngày
                    <input
                      className="filter"
                      type="date"
                      value={filters.from}
                      onChange={(event) =>
                        setFilter("from", event.target.value)
                      }
                    />
                  </label>
                  <label className="field">
                    Đến ngày
                    <input
                      className="filter"
                      type="date"
                      value={filters.to}
                      onChange={(event) => setFilter("to", event.target.value)}
                    />
                  </label>
                </div>
            </PopupDetails>
          )}
          <button ref={searchTrigger} type="button" className="task-work-tool task-work-search-toggle"
            aria-label="Tìm kiếm" aria-expanded={searchOpen} aria-controls="work-task-search"
            onClick={() => {
              setSearchOpen((current) => !current);
              if (searchOpen) setQuery("");
            }}>
            {icon("search")}
          </button>
          {searchOpen && (
            <input id="work-task-search" type="search" className="task-work-search-input" autoFocus
              aria-label={isDrafts ? "Tìm trong ghi nháp" : isProjects ? "Tìm project" : "Tìm công việc"}
              placeholder={isDrafts ? "Tìm ghi nháp…" : isProjects ? "Tìm project…" : "Tìm công việc…"}
              value={query} onChange={(event) => setQuery(event.target.value)}
              onKeyDown={(event) => {
                if (event.key !== "Escape") return;
                setQuery("");
                setSearchOpen(false);
                searchTrigger.current?.focus();
              }} />
          )}
          <div className="task-work-toolbar-end">
            {filtered && (
              <button type="button" className="task-work-tool" onClick={() => {
                setQuery("");
                setFilters(emptyFilters);
              }}>
                Bỏ bộ lọc
              </button>
            )}
            {isDrafts ? (
              <button type="button" className="task-work-tool" data-action="capture">
                {icon("spark")} Ghi nhanh
              </button>
            ) : (
              <>
                <button type="button" className="task-work-tool task-work-incomplete" aria-pressed={incomplete}
                  onClick={() => setFilter("status", incomplete ? "" : "active")}>
                  <span className="task-work-incomplete-icon">{icon("check")}</span>
                  <span>{isProjects ? "Project đang hoạt động" : "Chưa hoàn thành"}</span>
                </button>
                <PopupDetails className="task-work-popup task-column-picker">
                  <summary><span className="task-work-columns-icon" aria-hidden="true" /> Cột</summary>
                  <div>
                    {Object.entries(isProjects ? projectColumns : taskColumns).map(([key, label]) => (
                      <label className="checkbox" key={key}>
                        <input type="checkbox" checked={(isProjects ? visibleProjectColumns : columns).includes(key)} onChange={(event) => {
                          const checked = event.target.checked;
                          const changeColumns = isProjects ? setProjectColumns : setColumns;
                          changeColumns((current) => checked ? [...current, key] : current.filter((item) => item !== key));
                        }} />
                        {label}
                      </label>
                    ))}
                  </div>
                </PopupDetails>
              </>
            )}
          </div>
        </div>
        <div hidden={scope !== "inbox"}>
          <TaskInbox
            notes={inbox}
            loading={loading}
            error={error}
            query={query}
            onRefresh={refresh}
            onSaved={(note) => {
              if (state.workspace?.profile?.id !== uid) return;
              epoch.current++;
              setNotes((items) => [
                note,
                ...items.filter((item) => item.id !== note.id),
              ]);
              setLoading(false);
              setError("");
              refresh();
            }}
            onDeleted={(id) => {
              if (state.workspace?.profile?.id !== uid) return;
              epoch.current++;
              setNotes((items) => items.filter((item) => item.id !== id));
              refresh();
            }}
          />
        </div>
        {isProjects && <ProjectList projects={model.projects.filter(matches)} columns={visibleProjectColumns} filtered={filtered}
          onOpenProject={(id) => { setOpenedProject(id); location.hash = `tasks/project/${encodeURIComponent(id)}`; }} />}
        {scope !== "inbox" && !isProjects && (
          <>
            <div className="task-smart-groups">
              {groups.length ? (
                groups.map((group) => (
                  <section
                    className={`task-smart-group${group.key === "overdue" ? " is-overdue" : ""}`}
                    key={group.key}
                    aria-label={groupLabel(group.key, model.today)}
                  >
                    {group.key !== "all" && (
                      <h3>
                        {groupLabel(group.key, model.today)}
                        <span>{group.tasks.length}</span>
                      </h3>
                    )}
                    {taskRows(group.tasks, model.plannedDays, {
                      columns,
                      selectedId: selectedTaskId,
                      onSelect: setSelectedTaskId,
                      toolbarHost,
                      onWorkspaceChanged,
                      projectContext: group.projectContext,
                      plannedStarts: model.plannedStarts,
                      revealMatchingChildren: filtered,
                      addTask: isFlat ? addTask : null,
                      emptyMessage: filtered ? "Không có công việc phù hợp" : null,
                    })}
                  </section>
                ))
              ) : (
                <div className="task-smart-empty">
                  {icon("check")}
                  <h3>
                    {filtered
                      ? "Không có công việc phù hợp"
                      : scope === "today"
                        ? "Hôm nay đã gọn gàng"
                        : "Chưa có công việc trong danh sách này"}
                  </h3>
                  <p>
                    {filtered
                      ? "Thử thay đổi từ khóa hoặc bộ lọc."
                      : "Bạn có thể thêm công việc hoặc hoàn thiện một ý tưởng trong Inbox."}
                  </p>
                </div>
              )}
            </div>
            {!isFlat && <div className="work-task-board">{addTask}</div>}
            <span className="sr-only" aria-live="polite">{count} công việc</span>
          </>
        )}
      </section>
    </div>
    </div>
    </>
  );
}
function groupLabel(day, today) {
  if (day === "all") return "Tất cả công việc";
  if (day === "overdue") return "Quá hạn";
  if (day === today) return "Hôm nay";
  return new Intl.DateTimeFormat("vi-VN", {
    timeZone: "UTC",
    weekday: "long",
    day: "2-digit",
    month: "2-digit",
  }).format(new Date(`${day}T12:00:00Z`));
}
