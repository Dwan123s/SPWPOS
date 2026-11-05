import { useEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { icon, state } from "./ui.jsx";
import { Loading } from "./loading.jsx";
import "./styles/social.css";

export const profileLink = (id) => `#profile/${encodeURIComponent(id)}`;

function PersonAvatar({ person, large = false }) {
  const [failed, setFailed] = useState("");
  return (
    <span className={`social-avatar${large ? " social-avatar-large" : ""}`}>
      {person.avatarUrl && failed !== person.avatarUrl ? (
        <img
          src={person.avatarUrl}
          alt={`Ảnh đại diện của ${person.displayName}`}
          onError={() => setFailed(person.avatarUrl)}
        />
      ) : (
        <span aria-hidden="true">
          {(person.displayName || "U")
            .trim()
            .split(/\s+/)
            .slice(-2)
            .map((word) => word[0])
            .join("")}
        </span>
      )}
    </span>
  );
}

export function FriendshipActions({ person, onChanged }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [confirm, setConfirm] = useState(false);
  const pending = useRef(false);
  async function act(action) {
    if (pending.current) return;
    pending.current = true;
    setBusy(true);
    setError("");
    try {
      const base = `/api/social/friends/${encodeURIComponent(person.id)}`;
      const result = await api(
        action === "accept" ? `${base}/accept` : base,
        action === "remove" ? "DELETE" : "POST",
        action === "remove" ? undefined : {},
      );
      setConfirm(false);
      onChanged?.(result.relationship);
    } catch (failure) {
      setError(failure.message);
    } finally {
      pending.current = false;
      setBusy(false);
    }
  }
  const relationship = person.relationship;
  if (relationship === "Self") return null;
  return (
    <div className="friendship-actions">
      {relationship === "None" && (
        <button
          type="button"
          className="btn primary"
          disabled={busy}
          onClick={() => act("request")}
        >
          {icon("plus")} {busy ? "Đang gửi…" : "Thêm bạn bè"}
        </button>
      )}
      {relationship === "Outgoing" && (
        <>
          <span className="social-relationship-label">
            {icon("check")} Đã gửi lời mời
          </span>
          <button
            type="button"
            className="btn secondary"
            disabled={busy}
            onClick={() => act("remove")}
          >
            {busy ? "Đang thu hồi…" : "Thu hồi lời mời"}
          </button>
        </>
      )}
      {relationship === "Incoming" && (
        <>
          <button
            type="button"
            className="btn primary"
            disabled={busy}
            onClick={() => act("accept")}
          >
            {icon("check")} {busy ? "Đang xử lý…" : "Chấp nhận"}
          </button>
          <button
            type="button"
            className="btn secondary"
            disabled={busy}
            onClick={() => act("remove")}
          >
            Từ chối
          </button>
        </>
      )}
      {relationship === "Friends" &&
        (!confirm ? (
          <button
            type="button"
            className="btn secondary"
            onClick={() => setConfirm(true)}
          >
            {icon("users")} Bạn bè
          </button>
        ) : (
          <div className="social-unfriend-confirm">
            <span>Hủy kết bạn với {person.displayName}?</span>
            <div>
              <button
                type="button"
                className="btn danger small"
                disabled={busy}
                onClick={() => act("remove")}
              >
                Hủy kết bạn
              </button>
              <button
                type="button"
                className="btn ghost small"
                disabled={busy}
                onClick={() => setConfirm(false)}
              >
                Giữ lại
              </button>
            </div>
          </div>
        ))}
      {error && (
        <p className="social-error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

function PersonCard({ person, onChanged }) {
  return (
    <article className="social-person-card">
      <a className="social-person-link" href={profileLink(person.id)}>
        <PersonAvatar person={person} />
        <span>
          <strong>{person.displayName}</strong>
          <small>{person.bio || "Thành viên PLANORA"}</small>
        </span>
      </a>
      <FriendshipActions person={person} onChanged={onChanged} />
      <a className="social-profile-link" href={profileLink(person.id)}>
        Xem trang cá nhân {icon("arrow")}
      </a>
    </article>
  );
}

export function PeopleList({
  endpoint,
  emptyTitle,
  emptyDescription,
  onChanged,
}) {
  const [items, setItems] = useState([]);
  const [offset, setOffset] = useState(0);
  const [next, setNext] = useState(null);
  const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    setLoading(true);
    setError("");
    api(`${endpoint}${endpoint.includes("?") ? "&" : "?"}offset=${offset}`)
      .then((page) => {
        if (active) {
          setItems((old) =>
            offset === 0
              ? page.items
              : [
                  ...old,
                  ...page.items.filter(
                    (item) => !old.some((p) => p.id === item.id),
                  ),
                ],
          );
          setNext(page.nextOffset);
        }
      })
      .catch((failure) => {
        if (active) setError(failure.message);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [endpoint, offset, revision]);
  const refresh = () => {
    setOffset(0);
    setRevision((n) => n + 1);
    onChanged?.();
  };
  return (
    <div className="social-people-list">
      {error && (
        <div className="social-load-error" role="alert">
          <p>{error}</p>
          <button
            type="button"
            className="btn secondary small"
            onClick={() => setRevision((n) => n + 1)}
          >
            Thử lại
          </button>
        </div>
      )}
      {loading && offset === 0 ? (
        <Loading label="Đang tải danh sách…" />
      ) : !items.length && !error ? (
        <div className="social-empty">
          {icon("users")}
          <h3>{emptyTitle}</h3>
          <p>{emptyDescription}</p>
        </div>
      ) : (
        <div className="social-people-grid">
          {items.map((person) => (
            <PersonCard key={person.id} person={person} onChanged={refresh} />
          ))}
        </div>
      )}
      {next !== null && !error && (
        <button
          type="button"
          className="btn secondary social-load-more"
          disabled={loading}
          onClick={() => setOffset(next)}
        >
          {loading ? "Đang tải…" : "Xem thêm"}
        </button>
      )}
    </div>
  );
}

const friendTabs = [
  {
    id: "people",
    label: "Tìm bạn",
    hint: "Kết nối với mọi người",
    glyph: "search",
  },
  {
    id: "friends",
    label: "Bạn bè của tôi",
    hint: "Những người đã kết nối",
    glyph: "users",
  },
  {
    id: "incoming",
    label: "Lời mời đã nhận",
    hint: "Chờ bạn phản hồi",
    glyph: "bell",
  },
  {
    id: "outgoing",
    label: "Lời mời đã gửi",
    hint: "Theo dõi lời mời của bạn",
    glyph: "arrow",
  },
];
const emptyMessages = {
  people: ["Chưa tìm thấy người phù hợp", "Thử tìm bằng tên hiển thị khác."],
  friends: [
    "Bắt đầu kết nối đầu tiên",
    "Tìm người bạn biết rồi gửi lời mời kết bạn.",
  ],
  incoming: [
    "Chưa có lời mời mới",
    "Lời mời kết bạn gửi đến bạn sẽ xuất hiện ở đây.",
  ],
  outgoing: [
    "Chưa có lời mời đang chờ",
    "Bạn có thể tìm và gửi lời mời cho người mình biết.",
  ],
};
export function FriendsPage({ friendsTab }) {
  const [tab, setTab] = useState(
    friendTabs.some((item) => item.id === friendsTab) ? friendsTab : "people",
  );
  const [query, setQuery] = useState("");
  const [search, setSearch] = useState("");
  const [listVersion, setListVersion] = useState(0);
  useEffect(() => {
    const refresh = () => setListVersion((n) => n + 1);
    window.addEventListener("focus", refresh);
    return () => window.removeEventListener("focus", refresh);
  }, []);
  useEffect(() => {
    const timer = setTimeout(() => setSearch(query.trim()), 350);
    return () => clearTimeout(timer);
  }, [query]);
  const endpoint =
    tab === "people"
      ? `/api/social/people?search=${encodeURIComponent(search)}`
      : `/api/social/connections?kind=${tab}`;
  const [emptyTitle, emptyDescription] = emptyMessages[tab];
  return (
    <div className="social-workspace social-friends-page">
      <aside className="social-friends-nav">
        <div className="social-nav-heading">
          {icon("users")} Kết nối của bạn
        </div>
        <nav aria-label="Danh sách bạn bè">
          {friendTabs.map((item) => (
            <button
              type="button"
              key={item.id}
              aria-current={tab === item.id ? "page" : undefined}
              className={tab === item.id ? "is-active" : ""}
              onClick={() => {
                setTab(item.id);
                history.replaceState(null, "", `#friends/${item.id}`);
              }}
            >
              {icon(item.glyph)}
              <span>
                <strong>{item.label}</strong>
                <small>{item.hint}</small>
              </span>
            </button>
          ))}
        </nav>
        <a href="#profile" className="social-own-profile">
          Mở trang cá nhân của tôi {icon("arrow")}
        </a>
      </aside>
      <section
        className="social-friends-content"
        aria-labelledby="social-friends-title"
      >
        <header>
          <div>
            <span className="social-eyebrow">CÙNG NHAU, MỖI NGÀY</span>
            <h2 id="social-friends-title">
              {friendTabs.find((item) => item.id === tab).label}
            </h2>
            <p>Gặp gỡ, kết nối và tìm hiểu về nhau trong PLANORA.</p>
          </div>
          <button
            type="button"
            className="btn secondary small"
            onClick={() => setListVersion((n) => n + 1)}
          >
            {icon("refresh")} Làm mới
          </button>
        </header>
        {tab === "people" && (
          <form
            className="social-search"
            role="search"
            onSubmit={(event) => {
              event.preventDefault();
              event.stopPropagation();
              setSearch(query.trim());
            }}
          >
            {icon("search")}
            <input
              aria-label="Tìm người theo tên"
              placeholder="Tìm người theo tên hiển thị…"
              maxLength="100"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
            />
            <button className="btn primary small" type="submit">
              Tìm kiếm
            </button>
          </form>
        )}
        <PeopleList
          key={`${endpoint}:${listVersion}`}
          endpoint={endpoint}
          emptyTitle={emptyTitle}
          emptyDescription={emptyDescription}
        />
      </section>
    </div>
  );
}

async function coverFromFile(file) {
  if (
    !["image/jpeg", "image/png", "image/webp"].includes(file.type) ||
    file.size > 8 * 1024 * 1024
  )
    throw new Error("Chọn ảnh PNG, JPG hoặc WebP, tối đa 8 MB.");
  const url = URL.createObjectURL(file);
  try {
    const img = new Image();
    img.src = url;
    await img.decode();
    if (!img.width || !img.height) throw new Error("Không đọc được ảnh này.");
    const canvas = document.createElement("canvas");
    canvas.width = 1600;
    canvas.height = 600;
    const scale = Math.max(
      canvas.width / img.width,
      canvas.height / img.height,
    );
    canvas
      .getContext("2d")
      .drawImage(
        img,
        (canvas.width - img.width * scale) / 2,
        (canvas.height - img.height * scale) / 2,
        img.width * scale,
        img.height * scale,
      );
    return canvas.toDataURL("image/jpeg", 0.88);
  } finally {
    URL.revokeObjectURL(url);
  }
}
export function ProfilePage({ profileId }) {
  const id = profileId || state.workspace?.profile?.id;
  const [profile, setProfile] = useState(null);
  const [error, setError] = useState("");
  const [version, setVersion] = useState(0);
  const [tab, setTab] = useState("about");
  const [coverBusy, setCoverBusy] = useState(false);
  const [coverError, setCoverError] = useState("");
  const [notice, setNotice] = useState("");
  const coverInput = useRef(null);
  const changingCover = useRef(false);
  useEffect(() => {
    let active = true;
    setError("");
    api(`/api/social/profiles/${encodeURIComponent(id)}`)
      .then((data) => {
        if (active) setProfile(data);
      })
      .catch((failure) => {
        if (active) setError(failure.message);
      });
    return () => {
      active = false;
    };
  }, [id, version]);
  const refresh = () => setVersion((n) => n + 1);
  async function updateCover(input) {
    if (changingCover.current) return;
    changingCover.current = true;
    setCoverBusy(true);
    setCoverError("");
    setNotice("");
    try {
      const url =
        typeof input === "string" ? input : await coverFromFile(input);
      await api("/api/social/profile/cover", "PUT", { coverUrl: url });
      setProfile((old) => ({ ...old, coverUrl: url }));
      setNotice(url ? "Đã cập nhật ảnh bìa." : "Đã gỡ ảnh bìa.");
    } catch (failure) {
      setCoverError(failure.message);
    } finally {
      changingCover.current = false;
      setCoverBusy(false);
    }
  }
  async function chooseCover(event) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file || changingCover.current) return;
    setCoverError("");
    await updateCover(file);
  }
  if (error)
    return (
      <div className="social-workspace social-load-error" role="alert">
        <h2>Chưa mở được trang cá nhân</h2>
        <p>{error}</p>
        <button className="btn secondary" type="button" onClick={refresh}>
          Thử lại
        </button>
        <a className="btn ghost" href="#friends">
          Tìm bạn bè
        </a>
      </div>
    );
  if (!profile) return <Loading label="Đang tải trang cá nhân…" />;
  const own = profile.relationship === "Self";
  return (
    <div className="social-workspace social-profile-page">
      <section
        className="social-profile-hero"
        aria-labelledby="social-profile-name"
      >
        <div className={`social-cover${profile.coverUrl ? " has-image" : ""}`}>
          {profile.coverUrl ? (
            <img
              src={profile.coverUrl}
              alt={`Ảnh bìa của ${profile.displayName}`}
            />
          ) : (
            <div className="social-cover-pattern" aria-hidden="true">
              <span />
              <span />
              <span />
            </div>
          )}
          {own && (
            <div className="social-cover-actions">
              <button
                type="button"
                className="btn secondary small"
                disabled={coverBusy}
                onClick={() => coverInput.current?.click()}
              >
                {icon("image")}{" "}
                {coverBusy
                  ? "Đang cập nhật…"
                  : profile.coverUrl
                    ? "Đổi ảnh bìa"
                    : "Thêm ảnh bìa"}
              </button>
              {profile.coverUrl && (
                <button
                  type="button"
                  className="btn secondary small"
                  disabled={coverBusy}
                  onClick={() => updateCover("")}
                  aria-label="Gỡ ảnh bìa"
                  title="Gỡ ảnh bìa"
                >
                  {icon("trash")}
                </button>
              )}
              <input
                ref={coverInput}
                type="file"
                accept="image/png,image/jpeg,image/webp"
                aria-label="Chọn ảnh bìa"
                hidden
                onChange={chooseCover}
              />
            </div>
          )}
        </div>
        <div className="social-profile-heading">
          <PersonAvatar person={profile} large />
          <div className="social-profile-identity">
            <span className="social-eyebrow">TRANG CÁ NHÂN</span>
            <h2 id="social-profile-name">{profile.displayName}</h2>
            <button
              type="button"
              className="social-friend-count"
              onClick={() => setTab("friends")}
            >
              {profile.friendCount} bạn bè
              {!own && profile.mutualFriendCount > 0
                ? ` · ${profile.mutualFriendCount} bạn chung`
                : ""}
            </button>
          </div>
          <div className="social-profile-actions">
            {own ? (
              <>
                <a href="#settings" className="btn primary">
                  {icon("edit")} Chỉnh sửa hồ sơ
                </a>
                <a href="#friends" className="btn secondary">
                  {icon("users")} Tìm bạn bè
                </a>
              </>
            ) : (
              <FriendshipActions
                person={profile}
                onChanged={(relationship) => {
                  setProfile((old) => ({ ...old, relationship }));
                  refresh();
                }}
              />
            )}
          </div>
        </div>
        <nav
          className="social-profile-tabs"
          aria-label="Nội dung trang cá nhân"
        >
          <button
            type="button"
            aria-current={tab === "about" ? "page" : undefined}
            className={tab === "about" ? "is-active" : ""}
            onClick={() => setTab("about")}
          >
            Giới thiệu
          </button>
          <button
            type="button"
            aria-current={tab === "friends" ? "page" : undefined}
            className={tab === "friends" ? "is-active" : ""}
            onClick={() => setTab("friends")}
          >
            Bạn bè <span>{profile.friendCount}</span>
          </button>
          <button
            type="button"
            className="social-share-profile"
            onClick={async () => {
              try {
                await navigator.clipboard.writeText(
                  `${location.origin}${location.pathname}${profileLink(profile.id)}`,
                );
                setNotice("Đã sao chép liên kết trang cá nhân.");
              } catch {
                setNotice(
                  "Bạn có thể sao chép địa chỉ trang này trên thanh địa chỉ.",
                );
              }
            }}
          >
            {icon("copy")} Sao chép liên kết
          </button>
        </nav>
      </section>
      {coverError && (
        <p className="social-error" role="alert">
          {coverError}
        </p>
      )}
      {notice && (
        <p className="social-notice" role="status">
          {notice}
        </p>
      )}
      {tab === "about" ? (
        <div className="social-profile-columns">
          <section className="social-panel social-about">
            <h3>{icon("users")} Giới thiệu</h3>
            <p className="social-bio">
              {profile.bio ||
                (own
                  ? "Thêm vài dòng giới thiệu để mọi người hiểu bạn hơn."
                  : "Chưa có giới thiệu.")}
            </p>
            <p className="social-joined">
              {icon("calendar")} Tham gia từ{" "}
              {new Date(profile.joinedAt).toLocaleDateString("vi-VN", {
                month: "long",
                year: "numeric",
              })}
            </p>
            {own && (
              <a className="btn secondary" href="#settings">
                Chỉnh sửa thông tin
              </a>
            )}
          </section>
          <section className="social-panel social-profile-welcome">
            <span className="social-welcome-icon">{icon("spark")}</span>
            <h3>
              {own
                ? "Một góc nhỏ về bạn"
                : `Kết nối với ${profile.displayName}`}
            </h3>
            <p>
              {own
                ? "Chia sẻ đôi điều về mình, tìm bạn bè và cùng nhau tạo động lực mỗi ngày."
                : profile.relationship === "Friends"
                  ? "Hai bạn đã kết nối. Khám phá thêm bạn bè và tìm hiểu về nhau qua trang cá nhân."
                  : "Gửi lời mời kết bạn để bắt đầu kết nối trong PLANORA."}
            </p>
            <p className="social-privacy-note">
              {icon("lock")} Trang cá nhân chỉ chia sẻ thông tin giới thiệu và
              bạn bè. Lịch, công việc và giờ rảnh của mỗi người được giữ riêng.
            </p>
          </section>
        </div>
      ) : (
        <section className="social-panel">
          <div className="social-panel-heading">
            <h3>
              Bạn bè <span>{profile.friendCount}</span>
            </h3>
            {own && <a href="#friends">Tìm bạn mới {icon("arrow")}</a>}
          </div>
          <PeopleList
            key={`${profile.id}:${version}`}
            endpoint={`/api/social/profiles/${encodeURIComponent(profile.id)}/friends`}
            emptyTitle="Chưa có bạn bè"
            emptyDescription={
              own
                ? "Tìm người bạn biết và gửi lời mời kết bạn đầu tiên."
                : "Danh sách bạn bè sẽ xuất hiện khi có kết nối."
            }
            onChanged={refresh}
          />
        </section>
      )}
    </div>
  );
}
