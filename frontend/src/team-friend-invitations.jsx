import { useEffect, useId, useState } from "react";
import { api } from "./api.js";
import { dateFmt, icon, userAvatar } from "./ui.jsx";
import { TeamDialog } from "./team-dialog.jsx";
import { teamInviteLink, teamInvitationMessage } from "./team-invite-link.js";
import "./team-friend-invitations.css";

export function TeamInviteDialog({ team, onClose }) {
  const [tab, setTab] = useState("friends");
  const [query, setQuery] = useState("");
  const [search, setSearch] = useState("");
  const [offset, setOffset] = useState(0);
  const [page, setPage] = useState({ items: [], nextOffset: null });
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [invite, setInvite] = useState(null);
  const [copied, setCopied] = useState("");
  const id = useId();
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearch(query.trim());
      setOffset(0);
    }, 300);
    return () => clearTimeout(timer);
  }, [query]);
  useEffect(() => {
    if (tab !== "friends") return;
    const request = new AbortController();
    setLoading(true);
    setError("");
    api(
      `/api/teams/${team.id}/invite-friends?search=${encodeURIComponent(search)}&offset=${offset}`,
      "GET",
      undefined,
      true,
      request.signal,
    )
      .then((data) => {
        if (!request.signal.aborted)
          setPage((old) => ({
            items:
              offset === 0
                ? data.items
                : [
                    ...old.items,
                    ...data.items.filter(
                      (item) =>
                        !old.items.some(
                          (existing) => existing.userId === item.userId,
                        ),
                    ),
                  ],
            nextOffset: data.nextOffset,
          }));
      })
      .catch((failure) => {
        if (!request.signal.aborted) setError(failure.message);
      })
      .finally(() => {
        if (!request.signal.aborted) setLoading(false);
      });
    return () => request.abort();
  }, [team.id, tab, search, offset, revision]);

  async function act(action) {
    if (busy) return;
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await action();
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }
  const refresh = () => {
    setOffset(0);
    setRevision((n) => n + 1);
  };
  const statusLabels = {
    Member: "Đã trong nhóm",
    AwaitingApproval: "Chờ duyệt tham gia",
    Removed: "Đã bị loại khỏi nhóm",
    Declined: "Đã từ chối",
  };
  return (
    <TeamDialog
      title="Mời thành viên vào nhóm"
      subtitle={team.name}
      busy={busy}
      onClose={onClose}
      className="team-invitation-dialog"
    >
      <div className="team-invitation-body">
        <div
          className="team-invitation-tabs"
          role="tablist"
          aria-label="Cách mời thành viên"
          onKeyDown={(event) => {
            const target = {
              ArrowLeft: "friends",
              ArrowRight: "link",
              Home: "friends",
              End: "link",
            }[event.key];
            if (target && !busy) {
              event.preventDefault();
              setTab(target);
              setError("");
              setNotice("");
              document.getElementById(`${id}-${target}`)?.focus();
            }
          }}
        >
          {[
            { key: "friends", label: "Mời bạn bè", glyph: "users" },
            { key: "link", label: "Link / mã mời", glyph: "copy" },
          ].map(({ key, label, glyph }) => (
            <button
              key={key}
              id={`${id}-${key}`}
              type="button"
              role="tab"
              tabIndex={tab === key ? 0 : -1}
              aria-selected={tab === key}
              aria-controls={`${id}-panel`}
              disabled={busy}
              onClick={() => {
                setTab(key);
                setError("");
                setNotice("");
              }}
            >
              {icon(glyph)} {label}
            </button>
          ))}
        </div>
        <section
          id={`${id}-panel`}
          role="tabpanel"
          aria-labelledby={`${id}-${tab}`}
        >
          {tab === "friends" ? (
            <>
              <p className="team-invitation-hint">
                Mời người đã kết bạn với bạn. Họ sẽ nhận thông báo và tự chọn
                tham gia nhóm.
              </p>
              <label className="team-invitation-search">
                <span>Tìm trong danh sách bạn bè</span>
                <input
                  type="search"
                  maxLength={100}
                  placeholder="Nhập tên bạn bè…"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                />
              </label>
              <div className="team-invitation-list" aria-busy={loading}>
                {loading && offset === 0 ? (
                  <p role="status">Đang tải bạn bè…</p>
                ) : page.items.length ? (
                  page.items.map((person) => (
                    <article
                      className="team-invitation-person"
                      key={person.userId}
                      aria-label={person.name}
                    >
                      {userAvatar({
                        displayName: person.name,
                        avatarUrl: person.avatarUrl,
                      })}
                      <div className="team-invitation-name">
                        <strong>{person.name}</strong>
                        <small>
                          {person.status === "Invited"
                            ? "Đã gửi lời mời · Chờ phản hồi"
                            : person.status === "Declined"
                              ? "Có thể mời lại sau 24 giờ từ lần mời trước"
                              : statusLabels[person.status] || "Bạn bè của bạn"}
                        </small>
                      </div>
                      {statusLabels[person.status] ? (
                        <span className="team-invitation-state">
                          {statusLabels[person.status]}
                        </span>
                      ) : person.status === "Invited" ? (
                        <button
                          type="button"
                          className="btn secondary small"
                          disabled={busy || loading}
                          aria-label={`Thu hồi lời mời ${person.name}`}
                          onClick={() =>
                            act(async () => {
                              await api(
                                `/api/teams/${team.id}/friend-invitations/${person.invitationId}/revoke`,
                                "POST",
                                { version: person.version },
                              );
                              refresh();
                              setNotice(`Đã thu hồi lời mời ${person.name}.`);
                            })
                          }
                        >
                          Thu hồi
                        </button>
                      ) : (
                        <button
                          type="button"
                          className="btn primary small"
                          disabled={busy || loading || query.trim() !== search}
                          aria-label={`Mời ${person.name}`}
                          onClick={() =>
                            act(async () => {
                              const result = await api(
                                `/api/teams/${team.id}/friend-invitations`,
                                "POST",
                                { userId: person.userId },
                              );
                              setPage((old) => ({
                                ...old,
                                items: old.items.map((item) =>
                                  item.userId === person.userId
                                    ? {
                                        ...item,
                                        status: result.status,
                                        invitationId: result.invitationId,
                                        version: result.version,
                                      }
                                    : item,
                                ),
                              }));
                              setNotice(
                                result.status === "Invited"
                                  ? `Đã gửi lời mời đến ${person.name}.`
                                  : "Trạng thái thành viên đã thay đổi. Danh sách đã được cập nhật.",
                              );
                            })
                          }
                        >
                          Mời vào nhóm
                        </button>
                      )}
                    </article>
                  ))
                ) : (
                  !error && (
                    <div className="team-invitation-empty">
                      {icon("users")}
                      <strong>
                        {search
                          ? "Không tìm thấy bạn bè phù hợp"
                          : "Bạn chưa có kết nối bạn bè"}
                      </strong>
                      <p>
                        {search
                          ? "Thử tìm bằng tên khác."
                          : "Kết bạn trên PLANORA hoặc dùng link mời để chia sẻ cho người bạn biết."}
                      </p>
                      {!search && (
                        <a
                          className="btn secondary"
                          href="#friends"
                          onClick={onClose}
                        >
                          Tìm bạn bè
                        </a>
                      )}
                    </div>
                  )
                )}
              </div>
              {page.nextOffset != null && (
                <button
                  type="button"
                  className="btn secondary"
                  disabled={loading || busy}
                  onClick={() => setOffset(page.nextOffset)}
                >
                  {loading ? "Đang tải…" : "Xem thêm bạn bè"}
                </button>
              )}
            </>
          ) : (
            <div className="team-invite-content">
              <p>
                Chia sẻ link cho người chưa kết bạn. Họ gửi yêu cầu tham gia và
                chờ trưởng nhóm duyệt.
              </p>
              {!invite ? (
                <button
                  type="button"
                  className="btn primary"
                  disabled={busy}
                  onClick={() =>
                    act(async () => {
                      setInvite(
                        await api(`/api/teams/${team.id}/invites`, "POST"),
                      );
                    })
                  }
                >
                  {busy ? "Đang tạo…" : "Tạo link mời"}
                </button>
              ) : (
                <>
                  <div className="team-invitation-share-preview">
                    <span>LỜI MỜI THAM GIA NHÓM</span>
                    <strong>{team.name}</strong>
                    <p>
                      Mời bạn cùng quản lý công việc và trao đổi với nhóm trên
                      PLANORA.
                    </p>
                    <small>
                      Người nhận mở link, đăng nhập và gửi yêu cầu tham gia.
                    </small>
                  </div>
                  <button
                    type="button"
                    className="btn primary"
                    disabled={busy}
                    onClick={() =>
                      act(async () => {
                        await navigator.clipboard.writeText(
                          teamInvitationMessage(team.name, invite.code),
                        );
                        setCopied("message");
                      })
                    }
                  >
                    {icon("copy")}{" "}
                    {copied === "message"
                      ? "Đã sao chép lời mời"
                      : "Sao chép lời mời"}
                  </button>
                  <p className="team-invitation-share-hint">
                    Bao gồm tên nhóm, link và hướng dẫn. Dán trực tiếp vào tin
                    nhắn để người nhận dễ hiểu.
                  </p>
                  <label className="team-invite-link-field">
                    Link tham gia nhóm
                    <input
                      readOnly
                      value={teamInviteLink(invite.code)}
                      onFocus={(e) => e.target.select()}
                    />
                  </label>
                  <button
                    type="button"
                    className="btn secondary"
                    onClick={() =>
                      act(async () => {
                        await navigator.clipboard.writeText(
                          teamInviteLink(invite.code),
                        );
                        setCopied("link");
                      })
                    }
                  >
                    {icon("copy")}{" "}
                    {copied === "link"
                      ? "Đã sao chép link"
                      : "Sao chép link mời"}
                  </button>
                  <details className="team-invite-code">
                    <summary>Hoặc dùng mã mời</summary>
                    <code>{invite.code}</code>
                    <button
                      type="button"
                      className="btn secondary"
                      onClick={() =>
                        act(async () => {
                          await navigator.clipboard.writeText(invite.code);
                          setCopied("code");
                        })
                      }
                    >
                      {copied === "code" ? "Đã sao chép mã" : "Sao chép mã mời"}
                    </button>
                  </details>
                  <p className="team-muted">
                    Hết hạn {dateFmt(invite.expiresAt)}. Thu hồi sẽ vô hiệu hóa
                    cả link và mã này.
                  </p>
                  <button
                    type="button"
                    className="btn secondary"
                    disabled={busy}
                    onClick={() =>
                      act(async () => {
                        await api(
                          `/api/teams/${team.id}/invites/${invite.id}`,
                          "DELETE",
                        );
                        setInvite(null);
                        setCopied("");
                        setNotice("Đã thu hồi link và mã mời.");
                      })
                    }
                  >
                    Thu hồi link mời
                  </button>
                </>
              )}
            </div>
          )}
          {error && (
            <div role="alert" className="team-error">
              <span>{error}</span>
              {tab === "friends" && (
                <button
                  type="button"
                  className="btn secondary small"
                  disabled={busy || loading}
                  onClick={refresh}
                >
                  Tải lại danh sách
                </button>
              )}
            </div>
          )}
          {notice && (
            <p role="status" className="team-invitation-success">
              {icon("check")} {notice}
            </p>
          )}
        </section>
      </div>
    </TeamDialog>
  );
}

export function ReceivedTeamInvitations({ onAccepted }) {
  const [items, setItems] = useState([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(null);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let live = true;
    const load = async () => {
      if (document.hidden) return;
      try {
        const data = await api("/api/teams/friend-invitations");
        if (live) {
          setItems(Array.isArray(data) ? data : []);
          setError("");
        }
      } catch (failure) {
        if (live) setError(failure.message);
      }
    };
    load();
    const timer = setInterval(load, 15000);
    window.addEventListener("focus", load);
    return () => {
      live = false;
      clearInterval(timer);
      window.removeEventListener("focus", load);
    };
  }, [revision]);
  async function respond(item, accepted) {
    if (busy) return;
    setBusy(item.id);
    setError("");
    try {
      const result = await api(
        `/api/teams/friend-invitations/${item.id}/respond`,
        "POST",
        { version: item.version, accepted },
      );
      setItems((old) => old.filter((row) => row.id !== item.id));
      if (accepted) await onAccepted(result.id);
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(null);
    }
  }
  if (!items.length && !error) return null;
  return (
    <section
      className="team-panel team-received-invitations"
      aria-label="Lời mời vào nhóm"
    >
      <div className="team-panel-heading">
        <h3>Lời mời vào nhóm</h3>
        <span className="team-count">{items.length}</span>
      </div>
      {error && (
        <div role="alert" className="team-error">
          {error}
          <button
            type="button"
            className="btn secondary small"
            onClick={() => setRevision((n) => n + 1)}
          >
            Thử lại
          </button>
        </div>
      )}
      {items.map((item) => (
        <article
          className="team-received-invitation"
          key={item.id}
          aria-label={`Lời mời ${item.teamName}`}
        >
          <div>
            <strong>{item.teamName}</strong>
            <p>
              {item.senderName} mời bạn tham gia · Hết hạn{" "}
              {dateFmt(item.expiresAt)}
            </p>
          </div>
          <div className="team-actions">
            <button
              type="button"
              className="btn secondary"
              disabled={!!busy}
              onClick={() => respond(item, false)}
            >
              Từ chối
            </button>
            <button
              type="button"
              className="btn primary"
              disabled={!!busy}
              onClick={() => respond(item, true)}
            >
              {busy === item.id ? "Đang xử lý…" : "Tham gia nhóm"}
            </button>
          </div>
        </article>
      ))}
    </section>
  );
}
