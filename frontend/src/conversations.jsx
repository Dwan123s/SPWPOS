import { useEffect, useState } from "react";
import { api } from "./api.js";
import { icon, state } from "./ui.jsx";
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
  selected,
  refresh,
  disabled,
  onChoose,
  onRemoved,
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

  // Close floating popovers when clicking outside
  useEffect(() => {
    if (!menuOpenId && !confirmDeleteId) return;
    const closeAll = (e) => {
      if (
        e.target.closest?.(
          ".nora-menu-popover, .nora-inline-delete-box, .nora-chat-item-more",
        )
      ) {
        return;
      }
      setMenuOpenId(null);
      setConfirmDeleteId(null);
    };
    document.addEventListener("click", closeAll);
    return () => document.removeEventListener("click", closeAll);
  }, [menuOpenId, confirmDeleteId]);

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
    setError("");
    try {
      await api(`/api/agent/conversations/${row.id}`, "PUT", {
        title,
        archived: archive,
        version: row.version,
      });
      setRevision((x) => x + 1);
      if (archive && row.id === selected) onRemoved();
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }

  async function remove(row) {
    setLoading(true);
    setError("");
    try {
      await api(
        `/api/agent/conversations/${row.id}?version=${row.version}`,
        "DELETE",
      );
      setRevision((x) => x + 1);
      setItems((old) => old.filter((x) => x.id !== row.id));
      onRemoved?.(row.id);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }

  async function download(row) {
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
    }
  }

  const grouped =
    archived || query.trim()
      ? [{ name: archived ? "Đã lưu trữ" : "Kết quả tìm kiếm", rows: items }]
      : groupRows(items);

  return (
    <div className="nora-conversations-wrapper">
      <label className="nora-history-search">
        {icon("search")}
        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          maxLength={100}
          placeholder="Tìm hội thoại…"
          aria-label="Tìm hội thoại"
        />
      </label>

      {/* ChatGPT-style tabs: Gần đây / Đã lưu trữ */}
      <div className="nora-history-tabs" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={!archived}
          className={`nora-tab-pill${!archived ? " is-active" : ""}`}
          onClick={() => {
            setArchived(false);
            setMenuOpenId(null);
            setConfirmDeleteId(null);
          }}
          disabled={disabled}
        >
          Gần đây
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={archived}
          className={`nora-tab-pill${archived ? " is-active" : ""}`}
          onClick={() => {
            setArchived(true);
            setMenuOpenId(null);
            setConfirmDeleteId(null);
          }}
          disabled={disabled}
        >
          Đã lưu trữ
        </button>
      </div>

      {error && (
        <div className="nora-history-error" role="alert">
          <span>{error}</span>
          <button type="button" onClick={() => setRevision((x) => x + 1)}>
            Thử lại
          </button>
        </div>
      )}

      <div className="nora-conversation-list" aria-busy={loading}>
        {grouped.map((group) => (
          <section key={group.name} className="nora-history-group">
            <h2>{group.name}</h2>
            {group.rows.map((row) => (
              <div
                key={row.id}
                className={`nora-chat-item${row.id === selected ? " is-selected" : ""}${menuOpenId === row.id ? " has-menu-open" : ""}${confirmDeleteId === row.id ? " is-deleting" : ""}`}
              >
                {confirmDeleteId === row.id ? (
                  <div
                    className="nora-inline-delete-box"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <span className="nora-inline-delete-text">
                      Xóa cuộc trò chuyện?
                    </span>
                    <div className="nora-inline-delete-actions">
                      <button
                        type="button"
                        className="nora-confirm-btn"
                        onClick={(e) => {
                          e.stopPropagation();
                          setConfirmDeleteId(null);
                        }}
                      >
                        Hủy
                      </button>
                      <button
                        type="button"
                        className="nora-confirm-btn is-danger"
                        disabled={disabled || loading}
                        onClick={async (e) => {
                          e.stopPropagation();
                          await remove(row);
                          setConfirmDeleteId(null);
                        }}
                      >
                        Xóa
                      </button>
                    </div>
                  </div>
                ) : editingId === row.id ? (
                  <form
                    className="nora-inline-rename"
                    onSubmit={(e) => {
                      e.preventDefault();
                      const val = editingTitle.trim();
                      if (val) {
                        edit(row, val, row.archived);
                        setEditingId(null);
                      }
                    }}
                  >
                    <input
                      type="text"
                      value={editingTitle}
                      onChange={(e) => setEditingTitle(e.target.value)}
                      autoFocus
                      maxLength={120}
                      onKeyDown={(e) => {
                        if (e.key === "Escape") setEditingId(null);
                      }}
                    />
                    <button
                      type="submit"
                      aria-label="Lưu tên"
                      disabled={disabled || loading}
                    >
                      {icon("check")}
                    </button>
                    <button
                      type="button"
                      aria-label="Hủy"
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
                      onClick={() => onChoose(row.id, row.archived)}
                      aria-label={row.title}
                      title={row.title}
                    >
                      <span className="nora-chat-item-icon">
                        {icon("chat")}
                      </span>
                      <span className="nora-chat-item-title">{row.title}</span>
                    </button>
                    <button
                      type="button"
                      className="nora-chat-item-more"
                      aria-label={`Tùy chọn ${row.title}`}
                      aria-expanded={menuOpenId === row.id}
                      onClick={(e) => {
                        e.stopPropagation();
                        setConfirmDeleteId(null);
                        setMenuOpenId(menuOpenId === row.id ? null : row.id);
                      }}
                      disabled={disabled || loading}
                    >
                      <svg
                        viewBox="0 0 24 24"
                        width="16"
                        height="16"
                        fill="currentColor"
                      >
                        <circle cx="5" cy="12" r="2" />
                        <circle cx="12" cy="12" r="2" />
                        <circle cx="19" cy="12" r="2" />
                      </svg>
                    </button>
                  </>
                )}

                {/* ChatGPT-style Floating Menu */}
                {menuOpenId === row.id && confirmDeleteId !== row.id && (
                  <div
                    className="nora-menu-popover"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <button
                      type="button"
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
                      onClick={() => {
                        edit(row, row.title, !row.archived);
                        setMenuOpenId(null);
                      }}
                    >
                      {icon("folder")} {row.archived ? "Khôi phục" : "Lưu trữ"}
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        download(row);
                        setMenuOpenId(null);
                      }}
                    >
                      {icon("copy")} Xuất JSON
                    </button>
                    <button
                      type="button"
                      className="is-danger"
                      onClick={(e) => {
                        e.stopPropagation();
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

      {loading && (
        <p className="nora-history-loading" role="status">
          Đang tải hội thoại…
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
