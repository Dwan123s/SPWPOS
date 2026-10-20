import { useOutsideDismiss } from "./popup-dismissal.jsx";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { ConversationDeleteDialog } from "./conversation-delete-dialog.jsx";
import { api } from "./api.js";
import { icon, state } from "./ui.jsx";
import { conversationLabel } from "./nora-schedule.js";
import { groupConversations } from "./nora-chat-ui.jsx";

export function LegacyConversationLibrary({
  turns,
  selected,
  disabled,
  onChoose,
}) {
  const [query, setQuery] = useState("");
  const groups = groupConversations(turns, query);
  return (
    <>
      <label className="nora-history-search">
        {icon("search")}
        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Tìm hội thoại…"
          aria-label="Tìm hội thoại"
        />
      </label>
      <div className="nora-conversation-list">
        {groups.map((group) => (
          <section className="nora-history-group" key={group.name}>
            <h2>{group.name}</h2>
            {group.items.map((row) => (
              <button
                key={row.id}
                type="button"
                disabled={disabled}
                aria-current={selected === row.id ? "true" : undefined}
                className={selected === row.id ? "is-selected" : ""}
                aria-label={row.title}
                onClick={() => onChoose(row.id)}
              >
                {icon("chat")}
                <span>{row.title}</span>
                <small>{row.count}</small>
              </button>
            ))}
          </section>
        ))}
        {!groups.length && (
          <p className="nora-history-empty">
            {query
              ? "Không tìm thấy hội thoại phù hợp."
              : "Hội thoại của bạn sẽ xuất hiện ở đây."}
          </p>
        )}
      </div>
    </>
  );
}

function groupRows(items) {
  const now = new Date();
  const z = state.workspace?.preference?.timeZoneId || "Asia/Ho_Chi_Minh";
  const dayFmt = (d) => {
    try {
      return new Intl.DateTimeFormat("en-CA", {
        timeZone: z,
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
      }).format(new Date(d));
    } catch {
      return "";
    }
  };
  const today = dayFmt(now);
  const yesterday = dayFmt(now.getTime() - 86400000);
  const groups = new Map();
  for (const row of items) {
    const rawDate = row.updatedAt || row.createdAt;
    const label = !rawDate
      ? "Gần đây"
      : dayFmt(rawDate) === today
        ? "Hôm nay"
        : dayFmt(rawDate) === yesterday
          ? "Hôm qua"
          : now.getTime() - new Date(rawDate).getTime() < 7 * 86400000
            ? "7 ngày qua"
            : now.getTime() - new Date(rawDate).getTime() < 30 * 86400000
              ? "30 ngày qua"
              : "Trước đó";
    if (!groups.has(label)) groups.set(label, []);
    groups.get(label).push(row);
  }
  return [...groups.entries()].map(([name, rows]) => ({ name, rows }));
}

export function ConversationLibrary({
  turns = [],
  selected,
  refresh,
  disabled,
  onChoose,
  onRemoved,
  onUpdated,
}) {
  const [query, setQuery] = useState("");
  const [archived, setArchived] = useState(false);
  const [items, setItems] = useState([]);
  const [cursor, setCursor] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [revision, setRevision] = useState(0);
  const [menuOpenId, setMenuOpenId] = useState(null);
  const [editingId, setEditingId] = useState(null);
  const [editingTitle, setEditingTitle] = useState("");
  const [confirmDeleteId, setConfirmDeleteId] = useState(null);
  const [action, setAction] = useState("");
  const library = useRef(null);
  const menu = useRef(null);
  const menuTrigger = useRef(null);
  const [menuPosition, setMenuPosition] = useState({});
  const focusLast = useRef(false);
  const previousEdit = useRef(null);
  function focusTrigger(id) {
    [...(library.current?.querySelectorAll("[data-conversation-menu]") || [])]
      .find((button) => button.dataset.conversationMenu === id)
      ?.focus();
  }
  function closeMenu(id) {
    setMenuOpenId(null);
    focusTrigger(id);
  }
  useEffect(() => {
    const current = items.find((row) => row.id === selected);
    if (current) onUpdated?.(current);
  }, [items, selected, onUpdated]);
  useLayoutEffect(() => {
    if (!menuOpenId || !menu.current) return;
    const node = menu.current;
    const trigger = [
      ...library.current.querySelectorAll("[data-conversation-menu]"),
    ].find((button) => button.dataset.conversationMenu === menuOpenId);
    if (!trigger) return;
    menuTrigger.current = trigger;
    node.showPopover?.();
    const anchor = trigger.getBoundingClientRect();
    const width = Math.min(220, window.innerWidth - 16);
    const height = node.offsetHeight;
    const above = anchor.bottom + 6 + height > window.innerHeight - 8;
    setMenuPosition({
      left: Math.max(
        8,
        Math.min(window.innerWidth - width - 8, anchor.right - width),
      ),
      top: Math.max(8, above ? anchor.top - height - 6 : anchor.bottom + 6),
      width,
      transformOrigin: above ? "bottom right" : "top right",
    });
    const close = (event) => {
      if (!node.contains(event.target)) setMenuOpenId(null);
    };
    window.addEventListener("resize", close);
    window.addEventListener("scroll", close, true);
    const controls = node.querySelectorAll('[role="menuitem"]');
    (focusLast.current
      ? controls?.[controls.length - 1]
      : controls?.[0]
    )?.focus();
    return () => {
      window.removeEventListener("resize", close);
      window.removeEventListener("scroll", close, true);
    };
  }, [menuOpenId]);
  useEffect(() => {
    if (previousEdit.current && !editingId) focusTrigger(previousEdit.current);
    previousEdit.current = editingId;
  }, [editingId]);
  function switchTab(archive) {
    setArchived(archive);
    setCursor(null);
    setMenuOpenId(null);
    setConfirmDeleteId(null);
    setEditingId(null);
  }

  useEffect(() => {
    let active = true;
    const timer = setTimeout(() => {
      setLoading(true);
      setError("");
      api(
        `/api/agent/conversations?archived=${archived}&search=${encodeURIComponent(query)}&pageSize=30`,
      )
        .then((page) => {
          if (!Array.isArray(page?.items))
            throw new Error("Chưa tải được danh sách hội thoại. Hãy thử lại.");
          if (active) {
            setItems(page.items);
            setCursor(page.nextCursor);
          }
        })
        .catch((e) => {
          if (active) setError(e.message);
        })
        .finally(() => {
          if (active) setLoading(false);
        });
    }, 250);
    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [query, archived, revision, refresh]);

  useOutsideDismiss(
    [menu, menuTrigger],
    () => setMenuOpenId(null),
    !!menuOpenId,
  );

  async function more() {
    setLoading(true);
    setError("");
    try {
      const page = await api(
        `/api/agent/conversations?archived=${archived}&search=${encodeURIComponent(query)}&pageSize=30&cursor=${encodeURIComponent(cursor)}`,
      );
      setItems((old) => [
        ...old,
        ...page.items.filter((row) => !old.some((x) => x.id === row.id)),
      ]);
      setCursor(page.nextCursor);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }

  async function edit(row, title, archive) {
    setLoading(true);
    setAction(
      row.archived === archive
        ? "Đang lưu tên…"
        : archive
          ? "Đang lưu trữ…"
          : "Đang khôi phục…",
    );
    setError("");
    try {
      const updated = await api(`/api/agent/conversations/${row.id}`, "PUT", {
        title,
        archived: archive,
        version: row.version,
      });
      setItems((old) =>
        old.flatMap((item) =>
          item.id !== row.id
            ? [item]
            : updated.archived === archived
              ? [updated]
              : [],
        ),
      );
      onUpdated?.(updated);
      setRevision((x) => x + 1);
      if (archive && row.id === selected) onRemoved?.(row.id);
      return true;
    } catch (e) {
      setError(e.message);
      return false;
    } finally {
      setLoading(false);
      setAction("");
    }
  }

  async function remove(row) {
    setLoading(true);
    setAction("Đang xóa hội thoại…");
    setError("");
    try {
      await api(
        `/api/agent/conversations/${row.id}?version=${row.version}`,
        "DELETE",
      );
      setRevision((x) => x + 1);
      setItems((old) => old.filter((x) => x.id !== row.id));
      onRemoved?.(row.id);
      return true;
    } catch (e) {
      setError(e.message);
      return false;
    } finally {
      setLoading(false);
      setAction("");
    }
  }

  async function download(row) {
    setLoading(true);
    setAction("Đang xuất hội thoại…");
    setError("");
    try {
      const data = await api(`/api/agent/conversations/${row.id}/export`);
      if (data.truncated) {
        setError(
          "Hội thoại vượt 10.000 yêu cầu; bản xuất hiện tại chỉ chứa 10.000 yêu cầu đầu tiên.",
        );
      }
      const href = URL.createObjectURL(
        new Blob([JSON.stringify(data, null, 2)], {
          type: "application/json",
        }),
      );
      const link = document.createElement("a");
      link.href = href;
      link.download = `nora-${row.id}.json`;
      link.click();
      setTimeout(() => URL.revokeObjectURL(href), 1000);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
      setAction("");
    }
  }

  const grouped =
    archived || query.trim()
      ? [{ name: archived ? "Đã lưu trữ" : "Kết quả tìm kiếm", rows: items }]
      : groupRows(items);

  return (
    <div className="nora-conversations-wrapper" ref={library}>
      <label className="nora-history-search">
        {icon("search")}
        <input
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setLoading(true);
            setCursor(null);
            setMenuOpenId(null);
          }}
          maxLength={100}
          placeholder="Tìm hội thoại…"
          aria-label="Tìm hội thoại"
        />
      </label>

      <div
        className="nora-history-tabs"
        role="tablist"
        aria-label="Danh sách hội thoại"
        onKeyDown={(event) => {
          if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key))
            return;
          event.preventDefault();
          const next =
            event.key === "Home"
              ? false
              : event.key === "End"
                ? true
                : !archived;
          switchTab(next);
          event.currentTarget
            .querySelectorAll('[role="tab"]')
            [next ? 1 : 0]?.focus();
        }}
      >
        <button
          type="button"
          role="tab"
          id="nora-recent-tab"
          aria-controls="nora-conversation-results"
          tabIndex={archived ? -1 : 0}
          aria-selected={!archived}
          className={`nora-tab-pill${!archived ? " is-active" : ""}`}
          onClick={() => switchTab(false)}
          disabled={disabled}
        >
          Gần đây
        </button>
        <button
          type="button"
          role="tab"
          id="nora-archived-tab"
          aria-controls="nora-conversation-results"
          tabIndex={archived ? 0 : -1}
          aria-selected={archived}
          className={`nora-tab-pill${archived ? " is-active" : ""}`}
          onClick={() => switchTab(true)}
          disabled={disabled}
        >
          Đã lưu trữ
        </button>
      </div>

      {error && !confirmDeleteId && (
        <div className="nora-history-error" role="alert">
          <span>{error}</span>
          <button type="button" onClick={() => setRevision((x) => x + 1)}>
            Thử lại
          </button>
        </div>
      )}

      <div
        className="nora-conversation-list"
        id="nora-conversation-results"
        role="tabpanel"
        aria-labelledby={archived ? "nora-archived-tab" : "nora-recent-tab"}
        aria-busy={loading}
      >
        {grouped.map((group) => (
          <section key={group.name} className="nora-history-group">
            <h2>{group.name}</h2>
            {group.rows.map((row) => (
              <div
                key={row.id}
                className={`nora-chat-item${row.id === selected ? " is-selected" : ""}${menuOpenId === row.id ? " has-menu-open" : ""}${confirmDeleteId === row.id ? " is-deleting" : ""}`}
              >
                {editingId === row.id ? (
                  <form
                    className="nora-inline-rename"
                    onSubmit={async (e) => {
                      e.preventDefault();
                      const val = editingTitle.trim();
                      if (val) {
                        if (await edit(row, val, row.archived))
                          setEditingId(null);
                      }
                    }}
                  >
                    <input
                      type="text"
                      aria-label="Tên hội thoại"
                      disabled={disabled || loading}
                      value={editingTitle}
                      onChange={(e) => setEditingTitle(e.target.value)}
                      autoFocus
                      maxLength={120}
                      onKeyDown={(e) => {
                        if (e.key === "Escape") {
                          e.preventDefault();
                          e.stopPropagation();
                          setEditingId(null);
                        }
                      }}
                    />
                    <button
                      type="submit"
                      aria-label="Lưu tên"
                      disabled={disabled || loading || !editingTitle.trim()}
                    >
                      {icon("check")}
                    </button>
                    <button
                      type="button"
                      aria-label="Hủy"
                      disabled={loading}
                      onClick={() => setEditingId(null)}
                    >
                      {icon("close")}
                    </button>
                  </form>
                ) : (
                  <>
                    <button
                      type="button"
                      disabled={disabled || loading}
                      aria-current={row.id === selected ? "true" : undefined}
                      className="nora-chat-item-btn"
                      onClick={() => onChoose(row.id, row.archived, false, row)}
                      aria-label={conversationLabel(row, turns)}
                      title={row.title}
                    >
                      <span className="nora-chat-item-icon">
                        {icon("chat")}
                      </span>
                      <span className="nora-chat-item-title">
                        {conversationLabel(row, turns)}
                        {row.createdAt && (
                          <small>
                            {new Intl.DateTimeFormat("vi-VN", {
                              day: "2-digit",
                              month: "2-digit",
                              hour: "2-digit",
                              minute: "2-digit",
                              timeZone:
                                state.workspace?.preference?.timeZoneId ||
                                "Asia/Ho_Chi_Minh",
                            }).format(new Date(row.createdAt))}
                          </small>
                        )}
                      </span>
                    </button>
                    <button
                      type="button"
                      className="nora-chat-item-more"
                      data-conversation-menu={row.id}
                      aria-haspopup="menu"
                      aria-controls={
                        menuOpenId === row.id
                          ? `nora-menu-${row.id}`
                          : undefined
                      }
                      aria-label={`Tùy chọn ${row.title}`}
                      aria-expanded={menuOpenId === row.id}
                      onClick={(e) => {
                        e.stopPropagation();
                        setConfirmDeleteId(null);
                        focusLast.current = false;
                        setMenuOpenId(menuOpenId === row.id ? null : row.id);
                      }}
                      onKeyDown={(event) => {
                        if (
                          event.key === "ArrowDown" ||
                          event.key === "ArrowUp"
                        ) {
                          event.preventDefault();
                          focusLast.current = event.key === "ArrowUp";
                          setMenuOpenId(row.id);
                        }
                      }}
                      disabled={disabled || loading}
                    >
                      <svg
                        viewBox="0 0 24 24"
                        width="16"
                        height="16"
                        fill="currentColor"
                        aria-hidden="true"
                      >
                        <circle cx="5" cy="12" r="2" />
                        <circle cx="12" cy="12" r="2" />
                        <circle cx="19" cy="12" r="2" />
                      </svg>
                    </button>
                  </>
                )}

                {menuOpenId === row.id && confirmDeleteId !== row.id && (
                  <div
                    ref={menu}
                    popover={
                      typeof HTMLElement.prototype.showPopover === "function"
                        ? "manual"
                        : undefined
                    }
                    className="nora-menu-popover"
                    style={menuPosition}
                    id={`nora-menu-${row.id}`}
                    role="menu"
                    aria-label={`Tùy chọn hội thoại ${row.title}`}
                    onClick={(e) => e.stopPropagation()}
                    onBlur={(event) => {
                      if (
                        event.relatedTarget &&
                        !event.currentTarget.contains(event.relatedTarget)
                      )
                        setMenuOpenId(null);
                    }}
                    onKeyDown={(event) => {
                      if (event.key === "Escape") {
                        event.preventDefault();
                        event.stopPropagation();
                        closeMenu(row.id);
                      }
                      if (
                        !["ArrowDown", "ArrowUp", "Home", "End"].includes(
                          event.key,
                        )
                      )
                        return;
                      event.preventDefault();
                      const controls = [
                        ...event.currentTarget.querySelectorAll(
                          '[role="menuitem"]',
                        ),
                      ];
                      const index = controls.indexOf(document.activeElement);
                      const next =
                        event.key === "Home"
                          ? 0
                          : event.key === "End"
                            ? controls.length - 1
                            : (index +
                                (event.key === "ArrowUp" ? -1 : 1) +
                                controls.length) %
                              controls.length;
                      controls[next]?.focus();
                    }}
                  >
                    <button
                      type="button"
                      role="menuitem"
                      disabled={disabled || loading}
                      onClick={() => {
                        setEditingId(row.id);
                        setEditingTitle(row.title);
                        setMenuOpenId(null);
                      }}
                    >
                      {icon("edit")} Đổi tên
                    </button>
                    <button
                      type="button"
                      role="menuitem"
                      disabled={disabled || loading}
                      onClick={() => {
                        edit(row, row.title, !row.archived);
                        setMenuOpenId(null);
                      }}
                    >
                      {icon("folder")} {row.archived ? "Khôi phục" : "Lưu trữ"}
                    </button>
                    <button
                      type="button"
                      role="menuitem"
                      disabled={disabled || loading}
                      onClick={() => {
                        download(row);
                        setMenuOpenId(null);
                      }}
                    >
                      {icon("copy")} Xuất JSON
                    </button>
                    <button
                      type="button"
                      role="menuitem"
                      disabled={disabled || loading}
                      className="is-danger"
                      onClick={(e) => {
                        e.stopPropagation();
                        setError("");
                        focusTrigger(row.id);
                        setConfirmDeleteId(row.id);
                        setMenuOpenId(null);
                      }}
                    >
                      {icon("trash")} Xóa
                    </button>
                  </div>
                )}
              </div>
            ))}
          </section>
        ))}

        {!loading && items.length === 0 && (
          <p className="nora-history-empty">
            {query
              ? "Không tìm thấy hội thoại phù hợp."
              : archived
                ? "Không có hội thoại đã lưu trữ."
                : "Hội thoại của bạn sẽ xuất hiện ở đây."}
          </p>
        )}
      </div>

      {confirmDeleteId && items.find((row) => row.id === confirmDeleteId) && (
        <ConversationDeleteDialog
          title={items.find((row) => row.id === confirmDeleteId).title}
          busy={loading}
          error={error}
          onCancel={() => {
            setConfirmDeleteId(null);
            setError("");
          }}
          onConfirm={async () => {
            const row = items.find((row) => row.id === confirmDeleteId);
            if (row && (await remove(row))) setConfirmDeleteId(null);
          }}
        />
      )}
      {loading && (
        <p className="nora-history-loading" role="status">
          {action || "Đang tải hội thoại…"}
        </p>
      )}
      {cursor && (
        <button
          type="button"
          className="nora-history-more"
          disabled={disabled || loading}
          onClick={more}
        >
          Tải thêm hội thoại
        </button>
      )}
    </div>
  );
}
