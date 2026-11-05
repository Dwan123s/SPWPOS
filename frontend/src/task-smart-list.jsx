import { useCallback, useEffect, useRef, useState } from "react";
import { state, icon, statusLabels } from "./ui.jsx";
import { api } from "./api.js";
import { taskRows } from "./tasks.jsx";
import { TaskInbox } from "./task-inbox.jsx";
import { isInboxNote, smartTaskLists } from "./task-smart-lists.js";
import { PopupDetails } from "./popup-dismissal.jsx";
import "./task-smart-list.css";

const lists = {
  inbox: {
    label: "Inbox",
    glyph: "mail",
    description:
      "Ghi nhanh việc cần làm, tick khi hoàn thành. Bỏ tick để mở lại bất cứ lúc nào.",
  },
  today: {
    label: "Today",
    glyph: "calendar",
    description:
      "Công việc đến hạn hoặc đã xếp lịch hôm nay, cùng những việc còn quá hạn.",
  },
  week: {
    label: "Next 7 Days",
    glyph: "clock",
    description: "Bảy ngày tính từ hôm nay, theo múi giờ tài khoản của bạn.",
  },
  all: {
    label: "Tất cả công việc",
    glyph: "tasks",
    description:
      "Tra cứu công việc đã lên kế hoạch, bao gồm cả việc hoàn thành và đã hủy.",
  },
};
const emptyFilters = {
  status: "",
  category: "",
  priority: "",
  from: "",
  to: "",
};
export function TaskSmartList() {
  const [scope, setScope] = useState(() =>
    lists[state.taskScope] ? state.taskScope : "inbox",
  );
  const [query, setQuery] = useState("");
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
    window.addEventListener("workspace-updated", visible);
    document.addEventListener("visibilitychange", visible);
    return () => {
      alive.current = false;
      epoch.current++;
      clearInterval(timer);
      window.removeEventListener("focus", visible);
      window.removeEventListener("workspace-updated", visible);
      document.removeEventListener("visibilitychange", visible);
    };
  }, [refresh]);
  const model = smartTaskLists(workspace, now);
  const inbox = notes.filter(isInboxNote);
  const counts = {
    inbox: inbox.filter((note) => !note.inboxCompleted).length,
    today: model.todayGroups.reduce((sum, g) => sum + g.tasks.length, 0),
    week: model.weekGroups.reduce((sum, g) => sum + g.tasks.length, 0),
    all: model.all.length,
  };
  const selected = lists[scope];
  function choose(key) {
    state.taskScope = key;
    setScope(key);
    setNow(Date.now());
    if (key !== "all" && ["Completed", "Cancelled"].includes(filters.status))
      setFilters((current) => ({ ...current, status: "" }));
  }
  function matches(task) {
    return (
      `${task.name} ${task.description || ""}`
        .toLocaleLowerCase("vi")
        .includes(query.trim().toLocaleLowerCase("vi")) &&
      (!filters.status || task.status === filters.status) &&
      (!filters.category || task.categoryId === filters.category) &&
      (!filters.priority || task.priority === Number(filters.priority)) &&
      (!filters.from || model.dayOf(task.deadline) >= filters.from) &&
      (!filters.to || model.dayOf(task.deadline) <= filters.to)
    );
  }
  const sourceGroups =
    scope === "today"
      ? model.todayGroups
      : scope === "week"
        ? model.weekGroups
        : [{ key: "all", tasks: model.all }];
  const groups = sourceGroups
    .map((g) => ({ ...g, tasks: g.tasks.filter(matches) }))
    .filter((g) => g.tasks.length);
  const count = groups.reduce((sum, g) => sum + g.tasks.length, 0);
  const filtered =
    !!query || (scope !== "inbox" && Object.values(filters).some(Boolean));
  const setFilter = (key, value) =>
    setFilters((current) => ({ ...current, [key]: value }));
  return (
    <div className="task-smart-layout">
      <nav className="task-smart-nav" aria-label="Danh sách thông minh">
        <span className="task-smart-eyebrow">DANH SÁCH CỦA BẠN</span>
        {Object.entries(lists).map(([key, item]) => (
          <button
            key={key}
            type="button"
            className={`task-smart-nav-item${scope === key ? " is-active" : ""}${key === "all" ? " task-smart-all" : ""}`}
            aria-pressed={scope === key}
            onClick={() => choose(key)}
          >
            {icon(item.glyph)}
            <span>{item.label}</span>
            <small aria-label={`${counts[key]} mục`}>
              {key === "inbox" && loading ? "…" : counts[key]}
            </small>
          </button>
        ))}
        <p>
          Ghi lại trước.
          <br />
          Tập trung vào điều cần làm tiếp theo.
        </p>
      </nav>
      <section
        className="card task-smart-main"
        aria-label={`Nội dung ${selected.label}`}
      >
        <header className="task-smart-header">
          <div>
            <span className="task-smart-eyebrow">
              {scope === "inbox" ? "NƠI BẮT ĐẦU Ý TƯỞNG" : "CÔNG VIỆC CỦA BẠN"}
            </span>
            <h2>
              {selected.label}
              <span>{counts[scope]}</span>
            </h2>
            <p>{selected.description}</p>
          </div>
          {scope !== "inbox" && (
            <span className="task-smart-range">
              {formatTaskDate(`${model.today}T12:00:00Z`)}
              {scope === "week" &&
                ` – ${formatTaskDate(`${model.lastDay}T12:00:00Z`)}`}
            </span>
          )}
        </header>
        <div className="task-smart-toolbar">
          <label className="search-field">
            {icon("search")}
            <input
              className="filter search"
              type="search"
              aria-label={
                scope === "inbox" ? "Tìm trong Inbox" : "Tìm công việc"
              }
              placeholder={
                scope === "inbox"
                  ? "Tìm ý tưởng, nhiệm vụ tạm thời…"
                  : "Tìm công việc…"
              }
              value={query}
              onChange={(event) => setQuery(event.target.value)}
            />
          </label>
          {scope !== "inbox" && (
            <>
              <select
                aria-label="Lọc trạng thái"
                className="filter"
                value={filters.status}
                onChange={(event) => setFilter("status", event.target.value)}
              >
                <option value="">Trạng thái</option>
                {Object.entries(statusLabels)
                  .filter(([key]) =>
                    (scope === "all"
                      ? ["Todo", "InProgress", "Completed", "Cancelled"]
                      : ["Todo", "InProgress"]
                    ).includes(key),
                  )
                  .map(([key, label]) => (
                    <option key={key} value={key}>
                      {label}
                    </option>
                  ))}
              </select>
              <select
                aria-label="Lọc danh mục"
                className="filter"
                value={filters.category}
                onChange={(event) => setFilter("category", event.target.value)}
              >
                <option value="">Danh mục công việc</option>
                {workspace.categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
              <select
                aria-label="Lọc mức ưu tiên"
                className="filter"
                value={filters.priority}
                onChange={(event) => setFilter("priority", event.target.value)}
              >
                <option value="">Ưu tiên</option>
                {[4, 3, 2, 1].map((priority) => (
                  <option key={priority} value={priority}>
                    {["", "Thấp", "Vừa", "Cao", "Khẩn"][priority]}
                  </option>
                ))}
              </select>
              <PopupDetails className="date-filters">
                <summary>Khoảng deadline</summary>
                <div>
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
            </>
          )}
          {filtered && (
            <button
              type="button"
              className="btn ghost small"
              onClick={() => {
                setQuery("");
                setFilters(emptyFilters);
              }}
            >
              Bỏ bộ lọc
            </button>
          )}
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
        {scope !== "inbox" && (
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
                    {taskRows(group.tasks)}
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
            <footer className="task-list-footer">
              <span aria-live="polite">{count} công việc</span>
              <span>Bấm tên công việc để xem chi tiết</span>
            </footer>
          </>
        )}
      </section>
    </div>
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

function formatTaskDate(value) {
  return new Intl.DateTimeFormat("vi-VN", {
    timeZone: "UTC",
    day: "2-digit",
    month: "2-digit",
  }).format(new Date(value));
}
