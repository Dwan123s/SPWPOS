import { useCallback, useEffect, useRef, useState } from "react";
import "./styles/nora-entry.css";
import { ConversationDeleteDialog } from "./conversation-delete-dialog.jsx";
import { api } from "./api.js";
import { state, icon } from "./ui.jsx";
import { NoraAvatar, NoraMotionControl } from "./nora-mascot.jsx";
import {
  ConversationLibrary,
  LegacyConversationLibrary,
} from "./conversations.jsx";
import { sendAgentStream } from "./agent-stream.js";
import { agentFailure } from "./agent-failure.js";
import { NoraProgress } from "./nora-progress.jsx";
import { useVoiceInput } from "./voice-input.js";
import {
  NoraReply,
  groupConversations,
  planSessionName,
  planDay,
  planHours,
} from "./nora-chat-ui.jsx";

const statuses = {
  PROCESSING: "Đang xử lý",
  PREVIEW: "Chờ áp dụng",
  APPLIED: "Đã lưu lịch",
  NEEDS_INPUT: "Cần làm rõ",
  BLOCKED: "Chưa đủ điều kiện",
  ANSWERED: "Đã trả lời",
  FAILED: "Chưa thực hiện",
  UNDONE: "Đã hoàn tác",
};
function formatTime(value) {
  return new Intl.DateTimeFormat("vi-VN", {
    timeZone: state.workspace?.preference?.timeZoneId || "Asia/Ho_Chi_Minh",
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
}
function extractOptions(turn) {
  if (Array.isArray(turn.options) && turn.options.length > 0) {
    return turn.options;
  }
  if (!turn.question) return [];
  const q = turn.question;
  const parenMatch = q.match(/\((?:ví dụ:?\s*)?([^)]+)\)/i);
  if (parenMatch) {
    const parts = parenMatch[1]
      .split(/[,;\n]|(?:\s+hay\s+)|\/|(?:\s+hoặc\s+)/i)
      .map((s) =>
        s
          .trim()
          .replace(/^[-*•\d.]+\s*/, "")
          .replace(/^["']|["']$/g, ""),
      )
      .filter((s) => s.length > 0 && s.length < 50);
    if (parts.length >= 2) return parts;
  }
  const hayMatch = q.match(
    /(?:vào|lúc|chọn)?\s*([^,?.!:]+?)\s+(?:hay|hoặc)\s+([^,?.!:]+)/i,
  );
  if (hayMatch) {
    const o1 = hayMatch[1]
      .trim()
      .replace(/^là\s+/i, "")
      .replace(/^["']|["']$/g, "");
    const o2 = hayMatch[2].trim().replace(/^["']|["']$/g, "");
    if (o1.length > 0 && o1.length < 40 && o2.length > 0 && o2.length < 40) {
      return [o1, o2];
    }
  }
  const lower = q.toLowerCase();
  if (lower.includes("sáng") && lower.includes("tối"))
    return ["Buổi sáng", "Buổi chiều / tối"];
  if (lower.includes("bao nhiêu tuần")) return ["1 tuần", "2 tuần", "4 tuần"];
  return [];
}

function Turn({ turn, busy, onAction, onReply, onEdit, onViewPlan, process }) {
  const [otherText, setOtherText] = useState("");
  const [copied, setCopied] = useState(false);
  const [copyError, setCopyError] = useState(false);
  const sessions = turn.plan?.sessions?.filter((item) => !item.preserved) || [];
  const options = turn.question ? extractOptions(turn) : [];
  return (
    <article className="agent-turn">
      <div className="agent-user-message">
        <span>Bạn</span>
        <p>{turn.text}</p>
        <button
          className="nora-edit-request"
          type="button"
          disabled={busy}
          onClick={() => onEdit(turn.text)}
          aria-label="Sửa và gửi lại yêu cầu"
          title="Chép vào ô nhập để sửa và gửi thành yêu cầu mới"
        >
          {icon("edit")}
        </button>
      </div>
      <div className="agent-response">
        <div className="agent-response-heading">
          <strong>
            <NoraAvatar /> NORA
          </strong>
          <span className={`agent-status is-${turn.status.toLowerCase()}`}>
            {statuses[turn.status] || turn.status}
          </span>
        </div>
        {process && (
          <details className="nora-completed-process">
            <summary>
              {icon("check")}Đã xử lý trong {process.seconds}s ·{" "}
              {process.phases.length} bước
            </summary>
            <ol>
              {process.phases.map((step) => (
                <li key={step.stage}>{step.message}</li>
              ))}
            </ol>
          </details>
        )}
        <NoraReply text={turn.reply} />
        {turn.outcome && (
          <section
            className="agent-verified-outcome"
            aria-label="Kết quả đã kiểm tra"
          >
            <NoraReply text={turn.outcome} />
          </section>
        )}
        {!!turn.warnings?.length && (
          <ul className="agent-warnings">
            {turn.warnings.map((warning, index) => (
              <li key={index}>{warning}</li>
            ))}
          </ul>
        )}
        {turn.status === "FAILED" && (
          <small role="status">
            {agentFailure({ code: turn.errorCode }).label} · Yêu cầu này chưa
            được lưu.
          </small>
        )}
        {turn.question && (
          <div className="agent-clarification-box">
            <p className="agent-question">{turn.question}</p>
            {turn.status === "NEEDS_INPUT" && (
              <div className="agent-options-container">
                {options.length > 0 && (
                  <div className="agent-option-chips">
                    {options.map((opt, index) => (
                      <button
                        key={index}
                        type="button"
                        className="btn secondary small agent-chip-btn"
                        disabled={busy}
                        onClick={() => onReply && onReply(opt)}
                      >
                        {opt}
                      </button>
                    ))}
                  </div>
                )}
                <div className="agent-other-row">
                  <span className="agent-other-label">Khác:</span>
                  <div className="agent-other-input-wrap">
                    <input
                      type="text"
                      className="input small agent-other-field"
                      aria-label="Trả lời câu hỏi của NORA"
                      placeholder="Ghi ý kiến hoặc lựa chọn khác của bạn…"
                      value={otherText}
                      disabled={busy}
                      onChange={(e) => setOtherText(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" && otherText.trim()) {
                          e.preventDefault();
                          onReply && onReply(otherText.trim());
                          setOtherText("");
                        }
                      }}
                    />
                    <button
                      type="button"
                      className="btn primary small"
                      disabled={busy || !otherText.trim()}
                      onClick={() => {
                        onReply && onReply(otherText.trim());
                        setOtherText("");
                      }}
                    >
                      Gửi
                    </button>
                  </div>
                </div>
              </div>
            )}
          </div>
        )}
        {!!turn.changes?.length && (
          <div className="agent-changes">
            {turn.changes.map((change, index) => (
              <div className="agent-change" key={index}>
                <strong>{change.name}</strong>
                {change.before && <small>Trước: {change.before}</small>}
                <span>{change.after || "Không còn khoảng thời gian"}</span>
              </div>
            ))}
          </div>
        )}
        {!!sessions.length && (
          <button
            type="button"
            className="nora-plan-card"
            onClick={(event) => onViewPlan(turn, event.currentTarget)}
          >
            <span className="nora-plan-card-icon">{icon("calendar")}</span>
            <span>
              <strong>
                {turn.status === "UNDONE"
                  ? "Lịch sử kế hoạch"
                  : "Kế hoạch của bạn"}
              </strong>
              <small>
                {sessions.length} phiên ·{" "}
                {sessions.reduce(
                  (sum, item) =>
                    sum +
                    Math.round(
                      (new Date(item.end) - new Date(item.start)) / 60000,
                    ),
                  0,
                )}{" "}
                phút ·{" "}
                {turn.status === "APPLIED" ? "Đã lưu vào lịch" : "Xem chi tiết"}
              </small>
            </span>
            {icon("arrow")}
          </button>
        )}
        {!!sessions.length && (
          <details className="agent-sessions">
            <summary>
              {sessions.length} phiên{" "}
              {turn.status === "APPLIED" ? "đã xếp" : "đề xuất"}
            </summary>
            <ul>
              {sessions.map((item) => (
                <li key={item.id}>
                  <strong>
                    {state.workspace?.tasks?.find(
                      (task) => task.id === item.taskId,
                    )?.name ||
                      turn.changes?.find(
                        (change) => change.taskId === item.taskId,
                      )?.name ||
                      "Phiên làm việc"}
                  </strong>
                  <span>
                    {formatTime(item.start)} → {formatTime(item.end)}
                  </span>
                  <small>
                    {Math.round(
                      (new Date(item.end) - new Date(item.start)) / 60000,
                    )}{" "}
                    phút
                  </small>
                </li>
              ))}
            </ul>
          </details>
        )}
        {turn.status === "BLOCKED" && (
          <div className="agent-blocked-tip">
            {icon("spark")}{" "}
            <span>
              <strong>Gợi ý từ NORA:</strong> Bạn có thể mở thêm giờ rảnh ở mục{" "}
              <strong>Giờ rảnh</strong>, nới lỏng deadline hoặc chia nhỏ thời
              lượng công việc rồi gửi lại yêu cầu.
            </span>
          </div>
        )}
        {turn.status === "PREVIEW" && (
          <div className="actions spaced">
            <button
              type="button"
              className="btn primary"
              disabled={busy}
              onClick={() => onAction(turn, "apply")}
            >
              Áp dụng yêu cầu này
            </button>
            <span className="muted">
              Lịch chưa thay đổi. Bản xem trước có hiệu lực tối đa 30 phút.
            </span>
          </div>
        )}
        {turn.status === "APPLIED" && turn.canUndo && (
          <button
            type="button"
            className="btn ghost small"
            disabled={busy}
            onClick={() => onAction(turn, "undo")}
          >
            Hoàn tác yêu cầu
          </button>
        )}
        {turn.status === "UNDONE" && (
          <small className="muted">
            Các phiên trên là lịch sử của yêu cầu đã hoàn tác.
          </small>
        )}
        <div className="nora-message-footer">
          <time dateTime={turn.createdAt}>
            {turn.createdAt ? formatTime(turn.createdAt) : ""}
          </time>
          <button
            type="button"
            className="nora-copy-button"
            onClick={async () => {
              setCopyError(false);
              try {
                await navigator.clipboard.writeText(
                  [
                    turn.reply,
                    turn.outcome,
                    turn.question,
                    ...(turn.warnings || []),
                  ]
                    .filter(Boolean)
                    .join("\n\n"),
                );
                setCopied(true);
              } catch {
                setCopied(false);
                setCopyError(true);
              }
            }}
            aria-label="Sao chép phản hồi"
          >
            {icon(copied ? "check" : "copy")}
            <span>{copied ? "Đã sao chép" : "Sao chép"}</span>
          </button>
        </div>
        {copyError && (
          <small className="nora-copy-error" role="status">
            Không thể sao chép tự động. Bạn có thể chọn nội dung phản hồi để sao
            chép.
          </small>
        )}
      </div>
    </article>
  );
}

export function AgentPage({ onWorkspaceChanged = async () => {} }) {
  const [turns, setTurns] = useState([]);
  const [capabilities, setCapabilities] = useState(null);
  const [connectionAttempt, setConnectionAttempt] = useState(0);
  const [conversationId, setConversationId] = useState(null);
  const [text, setText] = useState("");
  const [autoApply, setAutoApply] = useState(true);
  const [personalized, setPersonalized] = useState(false);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [pending, setPending] = useState(null);
  const [processes, setProcesses] = useState({});
  const [messageCursor, setMessageCursor] = useState(null);
  const [conversationArchived, setConversationArchived] = useState(false);
  const [conversationDetails, setConversationDetails] = useState(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyCollapsed, setHistoryCollapsed] = useState(false);
  const [wide, setWide] = useState(
    () => window.matchMedia("(min-width: 1081px)").matches,
  );
  const [planOpen, setPlanOpen] = useState(false);
  const [planTurnId, setPlanTurnId] = useState(null);
  const [awayFromBottom, setAwayFromBottom] = useState(false);
  const [confirmDeleteCurrent, setConfirmDeleteCurrent] = useState(false);
  const streamAbort = useRef(null);
  const streamState = useRef(null);
  const followBottom = useRef(true);
  const retry = useRef(null);
  const requestField = useRef(null);
  const drawerTrigger = useRef(null);
  const historyPanel = useRef(null);
  const mounted = useRef(true);
  const updateConversation = useCallback(
    (row) => {
      if (row.id !== conversationId) return;
      setConversationDetails(row);
      setConversationArchived(row.archived);
    },
    [conversationId],
  );
  const accountKey = `planora:agent:${state.workspace?.profile?.id || "current"}`;
  useEffect(() => {
    const media = window.matchMedia("(min-width: 1081px)");
    const sync = () => setWide(media.matches);
    sync();
    media.addEventListener("change", sync);
    return () => media.removeEventListener("change", sync);
  }, []);
  const voice = useVoiceInput({
    text,
    enabled:
      !busy &&
      !loading &&
      !conversationArchived &&
      capabilities?.voiceAvailable,
    onPreview: (value) => setText(value),
    onTranscript: (value) => {
      setText(value);
      retry.current = null;
    },
  });
  useEffect(() => {
    if (voice.completed) requestField.current?.focus();
  }, [voice.completed]);
  useEffect(() => {
    const field = requestField.current;
    if (!field) return;
    field.style.height = "auto";
    field.style.height = `${Math.min(field.scrollHeight, 160)}px`;
  }, [text]);
  useEffect(() => {
    mounted.current = true;
    (async () => {
      try {
        const [caps, history] = await Promise.all([
          api("/api/agent/capabilities"),
          api("/api/agent/turns"),
        ]);
        if (!mounted.current) return;
        setCapabilities(caps);
        setTurns(history);
        let saved;
        try {
          saved = sessionStorage.getItem(accountKey);
        } catch {
          /* Browser storage is optional. */
        }
        setConversationId(
          saved && history.some((turn) => turn.conversationId === saved)
            ? saved
            : history.at(-1)?.conversationId || crypto.randomUUID(),
        );
      } catch (failure) {
        if (mounted.current) setError(failure.message);
      } finally {
        if (mounted.current) setLoading(false);
      }
    })();
    return () => {
      mounted.current = false;
      streamAbort.current?.abort();
    };
  }, [accountKey, connectionAttempt]);
  useEffect(() => {
    if (conversationId)
      try {
        sessionStorage.setItem(accountKey, conversationId);
      } catch {
        /* Optional. */
      }
  }, [conversationId, accountKey]);
  const replaceTurn = (result) =>
    setTurns((previous) =>
      previous.some((turn) => turn.id === result.id)
        ? previous.map((turn) =>
            turn.id === result.id ? result : { ...turn, canUndo: false },
          )
        : [...previous.map((turn) => ({ ...turn, canUndo: false })), result],
    );
  async function send(event, overrideText) {
    if (event?.preventDefault) event.preventDefault();
    const messageText = (
      typeof overrideText === "string" ? overrideText : text
    ).trim();
    if (
      busy ||
      conversationArchived ||
      voice.isActive() ||
      !messageText ||
      !conversationId ||
      !capabilities?.aiAvailable
    )
      return;
    setBusy(true);
    setError("");
    const body =
      retry.current?.text === messageText &&
      retry.current.conversationId === conversationId
        ? retry.current
        : {
            conversationId,
            requestId: crypto.randomUUID(),
            text: messageText,
            autoApply,
            useCapacityFactor: personalized,
            useEstimateBias: personalized,
          };
    retry.current = body;
    const controller = new AbortController();
    streamAbort.current = controller;
    streamState.current = {
      requestId: body.requestId,
      text: messageText,
      reply: "",
      phases: [],
      startedAt: Date.now(),
      cancelled: false,
    };
    setPending(streamState.current);
    followBottom.current = true;
    try {
      const result = await sendAgentStream(
        body,
        (item) => {
          if (!mounted.current) return;
          const next = { ...streamState.current };
          if (item.type === "phase")
            next.phases = [
              ...next.phases.filter((step) => step.stage !== item.stage),
              { stage: item.stage, message: item.message },
            ];
          if (item.type === "delta") next.reply += item.delta || "";
          streamState.current = next;
          setPending(next);
        },
        controller.signal,
      );
      if (!mounted.current) return;
      setProcesses((previous) => ({
        ...previous,
        [result.id]: {
          seconds: Math.max(
            1,
            Math.round((Date.now() - streamState.current.startedAt) / 1000),
          ),
          phases: streamState.current.phases,
        },
      }));
      replaceTurn(result);
      setPending(null);
      if (!overrideText) setText("");
      retry.current = null;
      try {
        if (result.status === "APPLIED") await onWorkspaceChanged();
        const history = await api("/api/agent/turns");
        if (mounted.current) setTurns(history);
      } catch {
        if (mounted.current)
          setError(
            result.status === "APPLIED"
              ? "Yêu cầu đã được lưu. Chưa tải lại được giao diện; hãy làm mới để xem lịch mới."
              : "Đã nhận kết quả. Chưa tải lại được lịch sử hội thoại.",
          );
      }
    } catch (failure) {
      if (mounted.current) {
        setPending((previous) =>
          previous
            ? {
                ...previous,
                cancelled: controller.signal.aborted,
                failure: agentFailure(failure),
                reply:
                  !controller.signal.aborted && failure.code
                    ? ""
                    : previous.reply,
              }
            : null,
        );
        setError(
          controller.signal.aborted
            ? "Đã dừng nhận phản hồi. Kiểm tra lịch sử trước khi gửi lại; yêu cầu đã lưu sẽ vẫn được giữ."
            : failure.message,
        );
        try {
          const history = await api("/api/agent/turns");
          if (mounted.current) {
            setTurns(history);
            if (
              history.some(
                (turn) =>
                  turn.requestId === body.requestId &&
                  turn.status !== "PROCESSING",
              )
            ) {
              setPending(null);
              retry.current = null;
              setError("");
            }
            if (
              history.some(
                (turn) =>
                  turn.requestId === body.requestId &&
                  turn.status === "APPLIED",
              )
            ) {
              setError("");
              await onWorkspaceChanged();
            }
          }
        } catch {
          /* Retry keeps the original request ID when no final turn is available. */
        }
      }
    } finally {
      if (streamAbort.current === controller) streamAbort.current = null;
      if (mounted.current) setBusy(false);
    }
  }
  async function act(turn, action) {
    if (busy || voice.isActive()) return;
    setBusy(true);
    setError("");
    try {
      const result = await api(`/api/agent/turns/${turn.id}/${action}`, "POST");
      if (!mounted.current) return;
      replaceTurn(result);
      await onWorkspaceChanged();
      const history = await api("/api/agent/turns");
      if (mounted.current) setTurns(history);
    } catch (failure) {
      if (mounted.current) setError(failure.message);
    } finally {
      if (mounted.current) setBusy(false);
    }
  }
  const current = turns.filter(
    (turn) => turn.conversationId === conversationId,
  );
  useEffect(() => {
    if (!followBottom.current) return;
    historyPanel.current?.scrollTo?.({
      top: current.length || busy ? historyPanel.current.scrollHeight : 0,
      behavior: "instant",
    });
  }, [
    current.length,
    conversationId,
    busy,
    pending?.reply,
    pending?.phases.length,
  ]);
  function editRequest(value) {
    setText(value);
    retry.current = null;
    requestField.current?.focus();
  }
  async function chooseConversation(
    id,
    archived = false,
    fresh = false,
    details = null,
  ) {
    voice.cancel();
    voice.clearError();
    setConversationId(id);
    setConversationDetails(details);
    setConversationArchived(archived);
    setConfirmDeleteCurrent(false);
    setPlanOpen(false);
    setMessageCursor(null);
    setText("");
    setPending(null);
    retry.current = null;
    setError("");
    setHistoryOpen(false);
    followBottom.current = true;
    if (!fresh && capabilities?.features?.includes("conversation-lifecycle")) {
      setLoading(true);
      try {
        const page = await api(
          `/api/agent/conversations/${id}/turns?pageSize=50`,
        );
        if (mounted.current) {
          setTurns((old) => [
            ...old.filter((turn) => turn.conversationId !== id),
            ...page.items,
          ]);
          setMessageCursor(page.nextCursor);
        }
      } catch (failure) {
        if (mounted.current) setError(failure.message);
      } finally {
        if (mounted.current) setLoading(false);
      }
    }
  }
  function newConversation() {
    chooseConversation(crypto.randomUUID(), false, true);
  }
  async function findCurrentConversation() {
    if (conversationDetails?.id === conversationId) return conversationDetails;
    let cursor = null;
    const visited = new Set();
    do {
      const page = await api(
        `/api/agent/conversations?archived=${conversationArchived}&search=&pageSize=50${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ""}`,
      );
      const found = page.items?.find((row) => row.id === conversationId);
      if (found) return found;
      cursor = page.nextCursor;
      if (cursor && visited.has(cursor)) break;
      visited.add(cursor);
    } while (cursor);
    throw new Error(
      "Chưa xác định được hội thoại. Hãy tải lại danh sách rồi thử lại.",
    );
  }
  async function restoreCurrentConversation() {
    setBusy(true);
    setError("");
    try {
      const found = await findCurrentConversation();
      const updated = await api(`/api/agent/conversations/${found.id}`, "PUT", {
        title: found.title,
        archived: false,
        version: found.version,
      });
      updateConversation(updated);
    } catch (failure) {
      setConversationDetails(null);
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }
  async function deleteCurrentConversation() {
    setBusy(true);
    setError("");
    try {
      const found = await findCurrentConversation();
      await api(
        `/api/agent/conversations/${found.id}?version=${found.version}`,
        "DELETE",
      );
      setTurns((old) =>
        old.filter((turn) => turn.conversationId !== conversationId),
      );
      newConversation();
    } catch (failure) {
      setConversationDetails(null);
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }
  async function olderMessages() {
    setLoading(true);
    setError("");
    followBottom.current = false;
    try {
      const page = await api(
        `/api/agent/conversations/${conversationId}/turns?pageSize=50&cursor=${encodeURIComponent(messageCursor)}`,
      );
      if (mounted.current) {
        setTurns((old) => [
          ...page.items.filter(
            (item) => !old.some((turn) => turn.id === item.id),
          ),
          ...old,
        ]);
        setMessageCursor(page.nextCursor);
      }
    } catch (failure) {
      if (mounted.current) setError(failure.message);
    } finally {
      if (mounted.current) setLoading(false);
    }
  }
  useEffect(() => {
    if (wide || (!historyOpen && !planOpen)) return;
    const panel = document.getElementById(
      historyOpen ? "nora-history-list" : "nora-plan-panel",
    );
    if (!panel) return;
    const previous = drawerTrigger.current || document.activeElement;
    const controls = () =>
      [
        ...panel.querySelectorAll(
          "input:not(:disabled),button:not(:disabled),a[href],summary",
        ),
      ].filter((item) => item.tabIndex !== -1);
    const focusPanel = () => {
      if (!panel.contains(document.activeElement)) controls()[0]?.focus();
    };
    let focusTask;
    const focusFrame = requestAnimationFrame(() => {
      focusTask = setTimeout(focusPanel, 0);
    });
    const entered = (event) => {
      if (event.target === panel) focusPanel();
    };
    panel.addEventListener("transitionend", entered);
    panel.addEventListener("animationend", entered);
    const close = (event) => {
      if (event.key === "Escape") {
        event.preventDefault();
        setHistoryOpen(false);
        setPlanOpen(false);
      }
      if (event.key === "Tab") {
        const items = controls();
        const first = items[0],
          last = items.at(-1);
        if (!panel.contains(document.activeElement)) {
          event.preventDefault();
          (event.shiftKey ? last : first)?.focus();
        } else if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first?.focus();
        }
      }
    };
    window.addEventListener("keydown", close);
    return () => {
      clearTimeout(focusTask);
      cancelAnimationFrame(focusFrame);
      panel.removeEventListener("transitionend", entered);
      panel.removeEventListener("animationend", entered);
      window.removeEventListener("keydown", close);
      if (previous?.isConnected) previous.focus?.();
    };
  }, [historyOpen, planOpen, wide]);
  useEffect(() => {
    if (wide) setHistoryOpen(false);
    else setPlanOpen(false);
  }, [wide]);
  const selectedTitle =
    (conversationDetails?.id === conversationId && conversationDetails.title) ||
    current[0]?.text ||
    "Hội thoại mới";
  const latestPlan = [...current]
    .reverse()
    .find((turn) => turn.plan?.sessions?.some((session) => !session.preserved));
  const visiblePlan =
    current.find(
      (turn) => turn.id === planTurnId && turn.plan?.sessions?.length,
    ) || latestPlan;
  const plannedSessions =
    visiblePlan?.plan?.sessions
      ?.filter((session) => !session.preserved)
      .toSorted((a, b) => new Date(a.start) - new Date(b.start)) || [];
  useEffect(() => {
    setPlanOpen(
      Boolean(latestPlan) && window.matchMedia("(min-width: 1081px)").matches,
    );
    setPlanTurnId(latestPlan?.id || null);
  }, [latestPlan?.id]);

  const examples = [
    {
      title: "Xếp một cuộc họp",
      subtitle: "14:00–16:00 ngày mai",
      icon: "calendar",
      text: "Ngày mai tôi có cuộc họp từ 14h đến 16h, xếp lại lịch giúp tôi.",
    },
    {
      title: "Hoàn thành trước deadline",
      subtitle: "2 giờ tập trung trước hạn chót",
      icon: "check",
      text: "Báo cáo SWP cần làm liên tục 2 tiếng, deadline 18h ngày mai. Xếp vào giờ rảnh cho tôi.",
    },
    {
      title: "Cập nhật giờ rảnh",
      subtitle: "Thiết lập nhịp sinh hoạt của bạn",
      icon: "clock",
      text: "Tôi rảnh thứ Hai đến thứ Sáu từ 19h đến 22h, riêng ngày mai bận từ 20h đến 21h.",
    },
    {
      title: "Xem lịch hôm nay",
      subtitle: "Biết việc nào cần ưu tiên",
      icon: "today",
      text: "Hôm nay lịch của tôi thế nào? Có công việc nào cần ưu tiên không? Chỉ xem và tư vấn, chưa thay đổi lịch.",
    },
  ];
  return (
    <div
      className={`agent-layout nora-studio${visiblePlan && planOpen ? " is-plan-open" : ""}${historyCollapsed ? " is-history-collapsed" : ""}`}
      data-motion-state="running"
    >
      <header
        className="nora-header"
        data-nora-reveal
        inert={!wide && (historyOpen || planOpen)}
      >
        <div className="nora-header-identity">
          <button
            type="button"
            className="nora-app-menu mobile-menu"
            data-action="mobile-menu"
            aria-label="Mở điều hướng"
            aria-controls="workspace-menu"
            aria-expanded="false"
          >
            {icon("menu")}
          </button>
          <button
            type="button"
            className="nora-history-toggle"
            aria-label={
              wide
                ? historyCollapsed
                  ? "Mở lịch sử"
                  : "Thu gọn lịch sử"
                : "Lịch sử"
            }
            aria-expanded={wide ? !historyCollapsed : historyOpen}
            aria-controls="nora-history-list"
            disabled={busy || voice.active}
            onClick={(event) => {
              drawerTrigger.current = event.currentTarget;
              if (wide) setHistoryCollapsed(!historyCollapsed);
              else {
                setPlanOpen(false);
                setHistoryOpen(!historyOpen);
              }
            }}
          >
            {icon("panel")}
          </button>
          <NoraAvatar />
          <div>
            <h1>
              NORA <span>Trợ lý lập lịch</span>
            </h1>
          </div>
        </div>
        <div className="nora-header-controls">
          <span
            className={`nora-presence ${loading ? "is-loading" : capabilities?.aiAvailable ? "is-ready" : "is-offline"}`}
          >
            <i aria-hidden="true" />
            {loading
              ? "Đang kết nối"
              : capabilities?.aiAvailable
                ? voice.active
                  ? voice.phase === "recording"
                    ? "Đang nghe"
                    : "Đang nhận chữ"
                  : busy
                    ? "Đang xử lý"
                    : "Sẵn sàng hỗ trợ"
                : "Chưa kết nối"}
          </span>
          <NoraMotionControl />
          <button
            type="button"
            className="nora-header-new"
            aria-label="Hội thoại mới"
            title="Hội thoại mới"
            disabled={busy || loading || voice.active}
            onClick={newConversation}
          >
            {icon("edit")}
          </button>
          {visiblePlan && (
            <button
              type="button"
              className="nora-plan-toggle"
              aria-expanded={planOpen}
              aria-controls="nora-plan-panel"
              onClick={(event) => {
                drawerTrigger.current = event.currentTarget;
                setHistoryOpen(false);
                setPlanOpen(!planOpen);
              }}
            >
              {icon("calendar")}Kế hoạch
            </button>
          )}
        </div>
      </header>
      {historyOpen && (
        <button
          type="button"
          className="nora-history-backdrop"
          aria-label="Đóng lịch sử"
          onClick={() => setHistoryOpen(false)}
        />
      )}
      <aside
        id="nora-history-list"
        className="nora-history-rail"
        data-open={historyOpen}
        inert={wide ? historyCollapsed : !historyOpen}
        aria-label="Hội thoại NORA"
        role={!wide && historyOpen ? "dialog" : undefined}
        aria-modal={!wide && historyOpen ? true : undefined}
      >
        <div className="nora-rail-title">
          <span>Hội thoại</span>
          <button
            type="button"
            className="nora-rail-close"
            aria-label="Đóng bảng hội thoại"
            onClick={() => {
              if (wide) setHistoryCollapsed(true);
              else setHistoryOpen(false);
              document.querySelector(".nora-history-toggle")?.focus();
            }}
          >
            {icon("close")}
          </button>
        </div>
        <button
          type="button"
          className="nora-new-chat"
          disabled={busy || loading || voice.active}
          onClick={newConversation}
        >
          {icon("plus")}Cuộc trò chuyện mới
        </button>
        {capabilities?.features?.includes("conversation-lifecycle") ? (
          <ConversationLibrary
            selected={conversationId}
            refresh={`${turns.length}:${turns.at(-1)?.id}:${turns.at(-1)?.status}:${conversationDetails?.version}`}
            disabled={busy || loading || voice.active}
            onChoose={chooseConversation}
            onUpdated={updateConversation}
            onRemoved={(deletedId) => {
              const idToRemove = deletedId || conversationId;
              setTurns((old) =>
                old.filter((turn) => turn.conversationId !== idToRemove),
              );
              if (!deletedId || deletedId === conversationId) {
                newConversation();
              }
            }}
          />
        ) : (
          <LegacyConversationLibrary
            turns={turns}
            selected={conversationId}
            disabled={busy || loading || voice.active}
            onChoose={chooseConversation}
          />
        )}
        <div className="nora-rail-note">
          {icon("shield")}
          <p>Lịch và hội thoại thuộc tài khoản của bạn.</p>
        </div>
      </aside>
      <section
        className="card agent-main"
        aria-label="Trò chuyện với NORA"
        data-nora-reveal
        inert={!wide && (historyOpen || planOpen)}
      >
        <div className="agent-toolbar">
          <div className="nora-toolbar-title-wrap">
            <span className="nora-chat-title" title={selectedTitle}>
              {icon("chat")} {selectedTitle}
            </span>
            <span className="nora-chat-meta">
              {current.length
                ? `${current.length} yêu cầu`
                : "Không gian riêng của bạn"}
            </span>
          </div>
          {current.length > 0 &&
            capabilities?.features?.includes("conversation-lifecycle") && (
              <div className="nora-toolbar-actions">
                <button
                  type="button"
                  className="nora-toolbar-delete-btn"
                  title="Xóa toàn bộ cuộc trò chuyện này"
                  aria-label="Xóa cuộc trò chuyện"
                  disabled={busy || loading}
                  onClick={() => {
                    setError("");
                    setConfirmDeleteCurrent(true);
                  }}
                >
                  {icon("trash")}
                </button>
              </div>
            )}
        </div>
        {confirmDeleteCurrent && (
          <ConversationDeleteDialog
            title={selectedTitle}
            busy={busy}
            error={error}
            onCancel={() => {
              setConfirmDeleteCurrent(false);
              setError("");
            }}
            onConfirm={deleteCurrentConversation}
          />
        )}
        {conversationArchived && (
          <div className="nora-archived-notice" role="status">
            <p>Hội thoại đã lưu trữ. Khôi phục để tiếp tục trò chuyện.</p>
            <button
              type="button"
              className="btn secondary small"
              disabled={busy || loading}
              onClick={restoreCurrentConversation}
            >
              Khôi phục hội thoại
            </button>
          </div>
        )}
        {messageCursor && (
          <button
            type="button"
            disabled={busy || loading || voice.active}
            onClick={olderMessages}
          >
            Tải yêu cầu trước đó
          </button>
        )}
        <div
          className="agent-history"
          ref={historyPanel}
          aria-label="Lịch sử hội thoại"
          aria-busy={busy || loading}
          onScroll={(event) => {
            const panel = event.currentTarget;
            const away =
              panel.scrollHeight - panel.scrollTop - panel.clientHeight > 90;
            followBottom.current = !away;
            setAwayFromBottom(away);
          }}
        >
          {loading ? (
            <p role="status">Đang tải trợ lý…</p>
          ) : !current.length && !pending ? (
            <div className="agent-empty">
              <span
                className="nora-welcome-icon"
                role="img"
                aria-label="NORA, linh vật cú xanh ôm cuốn lịch"
              >
                <NoraAvatar />
              </span>
              <p className="nora-greeting">
                Chào
                {state.workspace?.profile?.firstName
                  ? ` ${state.workspace.profile.firstName}`
                  : " bạn"}
                , mình là NORA.
              </p>
              <h2>
                Dành thời gian cho
                <br />
                <span>điều quan trọng.</span>
              </h2>
              <p>
                Mình giúp bạn lên lịch, giữ nhịp và tiến gần hơn đến mục tiêu.
              </p>
              <div className="agent-examples">
                {examples.map((example) => (
                  <button
                    key={example.title}
                    className="nora-prompt"
                    type="button"
                    disabled={busy || voice.active}
                    onClick={() => {
                      setText(example.text);
                      retry.current = null;
                      requestField.current?.focus();
                    }}
                  >
                    <span className="nora-prompt-icon">
                      {icon(example.icon)}
                    </span>
                    <span>
                      <strong>{example.title}</strong>
                      <small>{example.subtitle}</small>
                    </span>
                  </button>
                ))}
              </div>
            </div>
          ) : (
            current
              .filter((turn) => turn.requestId !== pending?.requestId)
              .map((turn) => (
                <Turn
                  key={turn.id}
                  turn={turn}
                  busy={busy || voice.active}
                  onAction={act}
                  onReply={(replyText) => send(null, replyText)}
                  onEdit={editRequest}
                  onViewPlan={(turn, trigger) => {
                    drawerTrigger.current = trigger;
                    setPlanTurnId(turn.id);
                    setPlanOpen(true);
                  }}
                  process={processes[turn.id]}
                />
              ))
          )}
          {pending && <NoraProgress pending={pending} busy={busy} />}
          {busy && !pending && (
            <div className="agent-thinking" role="status">
              <NoraAvatar />
              <span>
                NORA đang tìm thời gian phù hợp
                <span className="nora-thinking-dots" aria-hidden="true">
                  <i />
                  <i />
                  <i />
                </span>
              </span>
            </div>
          )}
        </div>
        {awayFromBottom && (
          <button
            type="button"
            className="nora-scroll-bottom"
            onClick={() => {
              followBottom.current = true;
              setAwayFromBottom(false);
              historyPanel.current?.scrollTo({
                top: historyPanel.current.scrollHeight,
                behavior: "instant",
              });
            }}
          >
            {icon("arrow")}Xuống cuối hội thoại
          </button>
        )}
        {capabilities && !capabilities.aiAvailable && (
          <p className="info">
            Trợ lý AI chưa được cấu hình. Bạn vẫn có thể nhập lịch ở mục Giờ
            rảnh và Công việc.
          </p>
        )}
        {error && !confirmDeleteCurrent && (
          <p className="form-error" role="alert">
            {error}
            {!capabilities && (
              <button
                type="button"
                className="btn secondary small"
                onClick={() => {
                  setError("");
                  setLoading(true);
                  setConnectionAttempt((value) => value + 1);
                }}
              >
                Thử kết nối lại
              </button>
            )}
          </p>
        )}
        <form
          className="agent-composer"
          onSubmit={send}
          aria-label="Gửi yêu cầu cho NORA"
        >
          <label htmlFor="agent-request" className="field">
            <span className="nora-sr-only">Yêu cầu của bạn</span>
            <textarea
              id="agent-request"
              aria-label="Yêu cầu của bạn"
              ref={requestField}
              value={text}
              maxLength={2000}
              rows={1}
              aria-describedby="nora-composer-hint nora-request-count"
              disabled={busy || loading || voice.active || conversationArchived}
              placeholder={
                conversationArchived
                  ? "Khôi phục hội thoại để gửi yêu cầu mới…"
                  : "NORA ơi, giúp mình lên lịch…"
              }
              onChange={(event) => setText(event.target.value)}
              onKeyDown={(event) => {
                if (
                  event.key === "Enter" &&
                  !event.shiftKey &&
                  !event.nativeEvent.isComposing &&
                  event.nativeEvent.keyCode !== 229
                )
                  send(event);
              }}
            />
          </label>
          <div className="agent-composer-actions">
            <label className="agent-toggle">
              <input
                type="checkbox"
                checked={autoApply}
                disabled={busy || voice.active}
                onChange={(event) => {
                  setAutoApply(event.target.checked);
                  if (retry.current)
                    retry.current = {
                      ...retry.current,
                      autoApply: event.target.checked,
                    };
                }}
              />
              Tự lưu khi lịch khả thi
            </label>
            <div className="nora-composer-tools">
              <details
                className="nora-composer-info"
                onKeyDown={(event) => {
                  if (event.key === "Escape") {
                    event.currentTarget.removeAttribute("open");
                    event.stopPropagation();
                  }
                }}
              >
                <summary
                  aria-label="Tùy chọn gửi yêu cầu"
                  title="Tùy chọn gửi yêu cầu"
                >
                  {icon("settings")}
                  <span>Tùy chọn</span>
                </summary>
                <div className="nora-composer-help">
                  {capabilities?.features?.includes("planning-policy") && (
                    <>
                      <label>
                        <input
                          type="checkbox"
                          checked={personalized}
                          disabled={busy || voice.active}
                          onChange={(event) => {
                            setPersonalized(event.target.checked);
                            retry.current = null;
                          }}
                        />
                        Dùng thời lượng cá nhân hóa và chừa thời gian dự phòng
                      </label>
                      <p>
                        {personalized
                          ? "Ước lượng và khả năng thực hiện dùng cùng chính sách với form lập kế hoạch."
                          : "Giữ đúng thời lượng bạn yêu cầu và dùng toàn bộ giờ rảnh trong giới hạn ngày. Deadline, giờ nghỉ và giới hạn phiên vẫn được giữ."}
                      </p>
                    </>
                  )}
                  <p>
                    {autoApply
                      ? "NORA sẽ lưu lịch khi khả thi. Bạn có thể hoàn tác sau đó."
                      : "NORA sẽ đề xuất lịch để bạn xem trước khi lưu."}
                  </p>
                  <p>
                    Nội dung yêu cầu và dữ liệu lịch cần thiết được gửi đến dịch
                    vụ AI khi bạn gửi.
                  </p>
                  <p>
                    Micro ghi tối đa 60 giây. Chữ tạm thời xuất hiện khi nói qua
                    dịch vụ nhận dạng của trình duyệt, hoặc Google Cloud theo
                    từng cụm lời nói. Khi dừng, Google Cloud hoàn thiện bản chép
                    lời. PLANORA không lưu bản ghi; bạn xem lại văn bản rồi gửi.
                  </p>
                  {!capabilities?.voiceAvailable && (
                    <p>
                      Nhập giọng nói chưa được cấu hình. Bạn có thể nhập yêu cầu
                      bằng bàn phím.
                    </p>
                  )}
                </div>
              </details>
              {capabilities?.voiceAvailable && (
                <button
                  type="button"
                  className={`nora-mic-button${voice.phase === "recording" ? " is-recording" : ""}`}
                  aria-label={
                    voice.phase === "recording"
                      ? "Dừng ghi và chuyển thành chữ"
                      : "Nhập bằng giọng nói"
                  }
                  aria-pressed={voice.phase === "recording"}
                  title={
                    !voice.supported
                      ? "Trình duyệt này chưa hỗ trợ ghi âm. Hãy dùng Chrome, Edge hoặc Safari mới trên HTTPS."
                      : !capabilities?.voiceAvailable
                        ? "Chuyển giọng nói cần Gemini hoặc Google Cloud được cấu hình."
                        : "Nhập bằng giọng nói"
                  }
                  disabled={
                    busy ||
                    conversationArchived ||
                    loading ||
                    !capabilities?.voiceAvailable ||
                    !voice.supported ||
                    (voice.active && voice.phase !== "recording")
                  }
                  onClick={() =>
                    voice.phase === "recording" ? voice.stop() : voice.start()
                  }
                >
                  {icon(voice.phase === "recording" ? "stop" : "mic")}
                </button>
              )}
              {busy && pending ? (
                <button
                  type="button"
                  className="nora-stop-button"
                  onClick={() => streamAbort.current?.abort()}
                  disabled={pending.phases.at(-1)?.stage === "SAVING"}
                >
                  {icon("stop")}
                  <span className="nora-sr-only">Dừng</span>
                </button>
              ) : (
                <button
                  type="submit"
                  className="btn primary nora-send-button"
                  aria-label="Gửi yêu cầu"
                  title="Gửi yêu cầu"
                  disabled={
                    busy ||
                    conversationArchived ||
                    voice.active ||
                    loading ||
                    !capabilities?.aiAvailable ||
                    !text.trim()
                  }
                >
                  {icon("arrow")}
                  <span className="nora-sr-only">
                    {busy ? "Đang xử lý…" : "Gửi yêu cầu"}
                  </span>
                </button>
              )}
            </div>
          </div>
          <div className="nora-composer-meta">
            <span id="nora-composer-hint">
              Enter gửi · Shift + Enter xuống dòng
            </span>
            <span
              id="nora-request-count"
              className={text.length >= 1900 ? "is-near-limit" : ""}
            >
              {text.length}/2000 ký tự
            </span>
          </div>
          {voice.active && (
            <div className="nora-voice-status" role="status">
              <span>
                <i
                  className={voice.phase === "recording" ? "is-listening" : ""}
                />
                {voice.phase === "requesting"
                  ? "Đang mở micro…"
                  : voice.phase === "recording"
                    ? `Đang nghe · 00:${String(voice.seconds).padStart(2, "0")}${voice.previewing ? " · Chữ tạm thời" : ""}`
                    : "Đang chuyển giọng nói thành chữ…"}
              </span>
              <button type="button" onClick={voice.cancel}>
                Hủy
              </button>
            </div>
          )}
          {voice.error && (
            <p className="nora-voice-error" role="alert">
              {voice.error}
            </p>
          )}
        </form>
      </section>
      {visiblePlan && planOpen && !wide && (
        <button
          type="button"
          className="nora-plan-backdrop"
          aria-label="Đóng kế hoạch"
          onClick={() => setPlanOpen(false)}
        />
      )}
      {visiblePlan && planOpen && (
        <aside
          id="nora-plan-panel"
          className="agent-context"
          aria-label="Kế hoạch của bạn"
          data-nora-reveal
          role={wide ? undefined : "dialog"}
          aria-modal={wide ? undefined : true}
        >
          <button
            className="nora-plan-close"
            type="button"
            onClick={() => setPlanOpen(false)}
            aria-label="Đóng bảng kế hoạch"
          >
            {icon("close")}
          </button>
          {visiblePlan && (
            <section
              className="nora-plan-artifact"
              aria-label="Chi tiết kế hoạch"
            >
              <div className="nora-context-heading">
                <span>{icon("calendar")}Kế hoạch của bạn</span>
                <span className="agent-status">
                  {statuses[visiblePlan.status]}
                </span>
              </div>
              <p>
                {plannedSessions.length} phiên ·{" "}
                {plannedSessions.reduce(
                  (sum, session) =>
                    sum +
                    Math.round(
                      (new Date(session.end) - new Date(session.start)) / 60000,
                    ),
                  0,
                )}{" "}
                phút
              </p>
              {visiblePlan.status !== "APPLIED" && (
                <p className="nora-plan-state-hint">
                  {visiblePlan.status === "UNDONE"
                    ? "Đây là lịch sử của yêu cầu đã hoàn tác."
                    : "Đây là bản đề xuất; các phiên này chưa được lưu."}
                </p>
              )}
              <ol>
                {plannedSessions.map((session, index) => (
                  <li key={session.id}>
                    {(index === 0 ||
                      planDay(session.start) !==
                        planDay(plannedSessions[index - 1].start)) && (
                      <h3>{planDay(session.start)}</h3>
                    )}
                    <div className="nora-plan-session">
                      <span>
                        {planHours(session.start)} — {planHours(session.end)}
                        {planDay(session.start) !== planDay(session.end)
                          ? ` (${planDay(session.end)})`
                          : ""}
                      </span>
                      <strong>{planSessionName(session, visiblePlan)}</strong>
                      <small>
                        {Math.round(
                          (new Date(session.end) - new Date(session.start)) /
                            60000,
                        )}{" "}
                        phút tập trung
                      </small>
                    </div>
                  </li>
                ))}
              </ol>
              <a href="#calendar">Mở lịch làm việc {icon("arrow")}</a>
            </section>
          )}
        </aside>
      )}
    </div>
  );
}
