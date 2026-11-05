import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { dateFmt, timeFmt, dayKey, icon, state } from "./ui.jsx";
import { PetArtwork, usePetAppearance } from "./pet-appearance.jsx";
import "./team-chat.css";

function mergeMessages(previous, incoming) {
  return [
    ...new Map([...previous, ...incoming].map((m) => [m.id, m])).values(),
  ].sort((a, b) => a.id - b.id);
}
function ChatAvatar({ name, src, userId }) {
  const [failedSrc, setFailedSrc] = useState(null);
  const initials = (name || "Thành viên")
    .trim()
    .split(/\s+/)
    .slice(-2)
    .map((part) => part[0])
    .join("")
    .toLocaleUpperCase("vi");
  const tone =
    [...String(userId || name)].reduce(
      (hash, char) => hash + char.charCodeAt(0),
      0,
    ) % 5;
  return (
    <span className={`team-chat-avatar tone-${tone}`} title={name}>
      {src && failedSrc !== src ? (
        <img
          src={src}
          alt={`Ảnh đại diện của ${name}`}
          loading="lazy"
          decoding="async"
          onError={() => setFailedSrc(src)}
        />
      ) : (
        <span aria-label={`Ảnh đại diện của ${name}`}>{initials}</span>
      )}
    </span>
  );
}
function chatDay(at) {
  const key = dayKey(at);
  if (key === dayKey(Date.now())) return "Hôm nay";
  const yesterday = new Date();
  yesterday.setDate(yesterday.getDate() - 1);
  return key === dayKey(yesterday)
    ? "Hôm qua"
    : dateFmt(at, { year: "numeric" });
}
export function TeamChat({ teamId, name }) {
  const myPet = usePetAppearance();
  const [messages, setMessages] = useState([]),
    [text, setText] = useState("");
  const [authors, setAuthors] = useState({});
  const [expanded, setExpanded] = useState(false);
  const [sending, setSending] = useState(false),
    [loading, setLoading] = useState(true),
    [older, setOlder] = useState(false),
    [loadingOlder, setLoadingOlder] = useState(false),
    [error, setError] = useState("");
  const list = useRef(null),
    input = useRef(null),
    cursor = useRef(null),
    oldest = useRef(null),
    follow = useRef(true),
    alive = useRef(false),
    pending = useRef(null),
    dialog = useRef(null),
    sizeButton = useRef(null),
    scrollPosition = useRef(null);
  const uid = state.workspace?.profile?.id;
  const profile = state.workspace?.profile;
  function resizeChat() {
    scrollPosition.current = list.current?.scrollTop ?? 0;
    setExpanded((value) => !value);
  }
  useLayoutEffect(() => {
    if (expanded) {
      const window = dialog.current;
      const previousOverflow = document.body.style.overflow;
      window.showModal();
      document.body.style.overflow = "hidden";
      input.current?.focus({ preventScroll: true });
      if (list.current) list.current.scrollTop = scrollPosition.current;
      return () => {
        window.close();
        document.body.style.overflow = previousOverflow;
      };
    }
    if (list.current && scrollPosition.current !== null)
      list.current.scrollTop = scrollPosition.current;
    // Restore focus after the expanded window is closed.
    if (sizeButton.current && scrollPosition.current !== null)
      sizeButton.current.focus({ preventScroll: true });
  }, [expanded]);
  function rememberAuthors(page) {
    if (page.authors?.length)
      setAuthors((previous) => ({
        ...previous,
        ...Object.fromEntries(
          page.authors.map((author) => [author.userId, author]),
        ),
      }));
  }
  useEffect(() => {
    alive.current = true;
    let fetching = false;
    async function refresh() {
      if (fetching || document.hidden) return;
      fetching = true;
      try {
        let pages = 0,
          hasMore;
        do {
          const initial = cursor.current === null;
          const data = await api(
            `/api/teams/${teamId}/messages${initial ? "" : `?after=${cursor.current}`}`,
          );
          if (!alive.current) return;
          const items = data.items || [];
          rememberAuthors(data);
          setMessages((prev) => mergeMessages(prev, items));
          if (initial) {
            setOlder(data.hasMore);
            oldest.current = items[0]?.id ?? null;
          }
          cursor.current = data.nextAfter ?? cursor.current ?? 0;
          hasMore = !initial && data.hasMore && items.length > 0;
        } while (hasMore && ++pages < 3);
        if (alive.current) setError("");
      } catch (e) {
        if (alive.current) setError(e.message);
      } finally {
        fetching = false;
        if (alive.current) setLoading(false);
      }
    }
    refresh();
    const timer = setInterval(refresh, 7000);
    const visible = () => {
      if (!document.hidden) refresh();
    };
    document.addEventListener("visibilitychange", visible);
    return () => {
      alive.current = false;
      clearInterval(timer);
      document.removeEventListener("visibilitychange", visible);
    };
  }, [teamId]);
  useLayoutEffect(() => {
    if (follow.current && list.current)
      list.current.scrollTop = list.current.scrollHeight;
  }, [messages]);
  useLayoutEffect(() => {
    if (input.current) {
      input.current.style.height = "auto";
      input.current.style.height = `${Math.min(input.current.scrollHeight || 40, 120)}px`;
    }
  }, [text, expanded]);
  async function send(e) {
    e.preventDefault();
    const value = text.trim();
    if (!value || sending) return;
    const request =
      pending.current?.text === value
        ? pending.current
        : { requestId: crypto.randomUUID(), text: value };
    pending.current = request;
    setSending(true);
    setError("");
    try {
      const message = await api(
        `/api/teams/${teamId}/messages`,
        "POST",
        request,
      );
      if (!alive.current) return;
      follow.current = true;
      setMessages((prev) => mergeMessages(prev, [message]));
      setText("");
      pending.current = null;
    } catch (e) {
      if (alive.current) setError(e.message);
    } finally {
      if (alive.current) setSending(false);
    }
  }
  async function loadOlder() {
    if (!oldest.current || loadingOlder) return;
    setLoadingOlder(true);
    const height = list.current?.scrollHeight || 0;
    try {
      const data = await api(
        `/api/teams/${teamId}/messages?before=${oldest.current}`,
      );
      if (!alive.current) return;
      follow.current = false;
      oldest.current = data.items?.[0]?.id ?? oldest.current;
      rememberAuthors(data);
      setOlder(data.hasMore);
      setMessages((prev) => mergeMessages(prev, data.items || []));
      requestAnimationFrame(() => {
        if (list.current)
          list.current.scrollTop += list.current.scrollHeight - height;
      });
    } catch (e) {
      if (alive.current) setError(e.message);
    } finally {
      if (alive.current) setLoadingOlder(false);
    }
  }
  const chat = (
    <section
      className={`card team-chat${expanded ? " team-chat-expanded" : ""}`}
      aria-label={`Chat chung ${name}`}
    >
      <header className="team-chat-header">
        <span className="team-chat-group-icon">{icon("chat")}</span>
        <div>
          <span className="team-chat-eyebrow">KHÔNG GIAN TRÒ CHUYỆN</span>
          <h2>Chat chung của nhóm</h2>
          <p>{name}</p>
        </div>
        <span
          className="team-chat-private"
          title="Chỉ thành viên trong nhóm được xem"
        >
          {icon("lock")}
          <span>Riêng tư</span>
        </span>
        <button
          ref={sizeButton}
          type="button"
          className="team-chat-resize"
          aria-label={expanded ? "Thu nhỏ khung chat" : "Phóng to khung chat"}
          title={expanded ? "Thu nhỏ khung chat (Esc)" : "Phóng to khung chat"}
          aria-expanded={expanded}
          onClick={resizeChat}
        >
          <svg className="icon" viewBox="0 0 24 24" aria-hidden="true">
            <path
              d={
                expanded
                  ? "M4 9h5V4 M20 9h-5V4 M4 15h5v5 M20 15h-5v5"
                  : "M9 4H4v5 M15 4h5v5 M4 15v5h5 M20 15v5h-5"
              }
              fill="none"
              stroke="currentColor"
              strokeWidth="1.8"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </button>
      </header>
      {older && (
        <button
          className="team-chat-older"
          disabled={loadingOlder}
          onClick={loadOlder}
        >
          {loadingOlder ? "Đang tải…" : "Xem tin nhắn cũ hơn"}
        </button>
      )}
      <div
        ref={list}
        className="team-chat-log"
        role="log"
        aria-live="polite"
        aria-relevant="additions"
        aria-label="Tin nhắn nhóm"
        onScroll={() => {
          const el = list.current;
          follow.current =
            el.scrollHeight - el.scrollTop - el.clientHeight < 70;
        }}
      >
        {loading && (
          <div className="team-chat-loading" role="status">
            <span />
            <span />
            <span />
            <p>Đang tải tin nhắn…</p>
          </div>
        )}
        {!loading && !messages.length && (
          <div className="team-chat-empty">
            <span>{icon("chat")}</span>
            <strong>Bắt đầu cuộc trò chuyện</strong>
            <p>
              Chia sẻ ý tưởng, cập nhật công việc
              <br />
              và cùng nhau hoàn thành mục tiêu.
            </p>
          </div>
        )}
        {messages.map((m, index) => {
          const mine = m.userId === uid;
          const account = mine ? profile : authors[m.userId];
          const pet =
            mine && state.workspace?.gamification
              ? { selectedSkin: myPet.code, userLevel: myPet.level }
              : authors[m.userId];
          const petCode = pet?.selectedSkin === "fox" ? "fox" : "classic";
          const petLevel = Math.max(1, Number(pet?.userLevel) || 1);
          const petName = petCode === "fox" ? "MILO" : "NORA";
          const author =
            (mine ? account?.displayName : account?.name) ||
            m.author ||
            "Thành viên";
          const showDay =
            index === 0 ||
            dayKey(messages[index - 1].createdAt) !== dayKey(m.createdAt);
          return (
            <div className="team-chat-entry" key={m.id}>
              {showDay && (
                <div className="team-chat-day">
                  <span>{chatDay(m.createdAt)}</span>
                </div>
              )}
              {m.isSystem ? (
                <article className="team-chat-message system">
                  <span className="team-chat-system-icon">
                    {icon("shield")}
                  </span>
                  <div>
                    <div className="team-chat-meta">
                      <strong>✓ Hệ thống</strong>
                      <time dateTime={m.createdAt}>{timeFmt(m.createdAt)}</time>
                    </div>
                    <p>{m.text}</p>
                  </div>
                </article>
              ) : (
                <div className={`team-chat-row${mine ? " mine" : ""}`}>
                  <ChatAvatar
                    name={author}
                    src={account?.avatarUrl}
                    userId={m.userId}
                  />
                  <div className="team-chat-message-content">
                    <div className="team-chat-author-line">
                      <a className="team-chat-author team-member-profile-link" href={`#profile/${m.userId}`} title={`Xem trang cá nhân của ${author}`}>
                        {mine ? "Bạn" : author}
                      </a>
                      <span
                        className="team-chat-pet"
                        title={`${petName} của ${mine ? "bạn" : author} · Cấp ${petLevel}`}
                      >
                        <PetArtwork
                          code={petCode}
                          level={petLevel}
                          alt={`${petName} của ${mine ? "bạn" : author} · Cấp ${petLevel}`}
                        />
                        <span
                          className="team-chat-pet-level"
                          aria-hidden="true"
                        >
                          Cấp {petLevel}
                        </span>
                      </span>
                    </div>
                    <article
                      className={`team-chat-message${mine ? " mine" : ""}`}
                    >
                      <p>{m.text}</p>
                    </article>
                    <time
                      className="team-chat-time"
                      dateTime={m.createdAt}
                      title={dateFmt(m.createdAt, { year: "numeric" })}
                    >
                      {timeFmt(m.createdAt)}
                    </time>
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>
      {error && (
        <p className="team-chat-error" role="alert">
          {icon("alertCircle")}
          {error}
        </p>
      )}
      <form className="team-chat-composer" onSubmit={send}>
        <label className="sr-only" htmlFor={`team-message-${teamId}`}>
          Tin nhắn cho nhóm
        </label>
        <div className="team-chat-input-wrap">
          <textarea
            ref={input}
            id={`team-message-${teamId}`}
            value={text}
            maxLength={2000}
            rows={1}
            disabled={sending}
            onChange={(e) => setText(e.target.value)}
            placeholder="Viết tin nhắn cho nhóm…"
            onKeyDown={(e) => {
              if (
                e.key === "Enter" &&
                !e.shiftKey &&
                !e.nativeEvent.isComposing &&
                e.nativeEvent.keyCode !== 229
              ) {
                e.preventDefault();
                e.currentTarget.form.requestSubmit();
              }
            }}
          />
          <button
            className="team-chat-send-button"
            aria-label={sending ? "Đang gửi…" : "Gửi tin nhắn"}
            title="Gửi tin nhắn (Enter)"
            disabled={sending || !text.trim()}
          >
            {icon(sending ? "clock" : "arrow")}
          </button>
        </div>
        <div className="team-chat-composer-hint">
          <span>
            <kbd>Enter</kbd> để gửi · <kbd>Shift</kbd> + <kbd>Enter</kbd> xuống
            dòng
          </span>
          <span>{text.length}/2000</span>
        </div>
      </form>
    </section>
  );
  return expanded ? (
    <dialog
      ref={dialog}
      className="team-chat-window"
      aria-label={`Chat chung ${name} mở rộng`}
      onCancel={(event) => {
        event.preventDefault();
        resizeChat();
      }}
    >
      {chat}
    </dialog>
  ) : (
    chat
  );
}
