import { state } from "./ui.jsx";

const zone = () =>
  state.workspace?.preference?.timeZoneId || "Asia/Ho_Chi_Minh";
export function planDay(value) {
  return new Intl.DateTimeFormat("vi-VN", {
    timeZone: zone(),
    weekday: "short",
    day: "2-digit",
    month: "2-digit",
  }).format(new Date(value));
}
export function planHours(value) {
  return new Intl.DateTimeFormat("vi-VN", {
    timeZone: zone(),
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).format(new Date(value));
}
export function planSessionName(session, turn) {
  return (
    state.workspace?.tasks?.find((task) => task.id === session.taskId)?.name ||
    turn.changes?.find((change) => change.taskId === session.taskId)?.name ||
    "Phiên làm việc"
  );
}
function dayKey(value) {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: zone(),
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date(value));
}
export function groupConversations(turns, query = "", now = new Date()) {
  const items = new Map();
  for (const turn of turns) {
    const item = items.get(turn.conversationId) || {
      id: turn.conversationId,
      title: turn.text || "Hội thoại",
      count: 0,
      updated: null,
      search: "",
    };
    item.count++;
    item.search += ` ${turn.text || ""} ${turn.reply || ""}`;
    if (
      turn.createdAt &&
      Number.isFinite(Date.parse(turn.createdAt)) &&
      (!item.updated || Date.parse(turn.createdAt) > Date.parse(item.updated))
    )
      item.updated = turn.createdAt;
    items.set(turn.conversationId, item);
  }
  const today = dayKey(now),
    yesterday = dayKey(new Date(now.getTime() - 86400000));
  const groups = new Map();
  for (const item of [...items.values()]
    .reverse()
    .sort(
      (a, b) => (Date.parse(b.updated) || 0) - (Date.parse(a.updated) || 0),
    )) {
    if (
      !item.search
        .toLocaleLowerCase("vi")
        .includes(query.toLocaleLowerCase("vi").trim())
    )
      continue;
    const name = !item.updated
      ? "Gần đây"
      : dayKey(item.updated) === today
        ? "Hôm nay"
        : dayKey(item.updated) === yesterday
          ? "Hôm qua"
          : now - new Date(item.updated) < 7 * 86400000
            ? "7 ngày qua"
            : "Trước đó";
    if (!groups.has(name)) groups.set(name, []);
    groups.get(name).push(item);
  }
  return [...groups].map(([name, items]) => ({ name, items }));
}

function safeLink(value) {
  if (
    /^#(?:today|calendar|tasks|time|notes|insights|settings|agent)$/.test(value)
  )
    return value;
  try {
    const url = new URL(value);
    return ["https:", "http:"].includes(url.protocol) ? url.href : null;
  } catch {
    return null;
  }
}
function inline(text) {
  return text
    .split(/(\*\*[^*\n]+\*\*|`[^`\n]+`|\[[^\]\n]+\]\([^\s)]+\))/g)
    .map((part, index) => {
      if (part.startsWith("**") && part.endsWith("**"))
        return <strong key={index}>{part.slice(2, -2)}</strong>;
      if (part.startsWith("`") && part.endsWith("`"))
        return <code key={index}>{part.slice(1, -1)}</code>;
      const link = part.match(/^\[([^\]]+)\]\((.+)\)$/);
      if (link) {
        const href = safeLink(link[2]);
        return href ? (
          <a
            key={index}
            href={href}
            {...(href.startsWith("#")
              ? {}
              : { target: "_blank", rel: "noopener noreferrer" })}
          >
            {link[1]}
          </a>
        ) : (
          link[1]
        );
      }
      return part;
    });
}
// Render a small, safe subset of Markdown as React nodes; raw HTML is always literal text.
export function NoraReply({ text = "", streaming = false }) {
  const lines = text.replace(/\r\n/g, "\n").split("\n"),
    blocks = [];
  for (let i = 0; i < lines.length;) {
    if (!lines[i].trim()) {
      i++;
      continue;
    }
    const start = i;
    if (lines[i].startsWith("```")) {
      const code = [];
      i++;
      while (i < lines.length && !lines[i].startsWith("```"))
        code.push(lines[i++]);
      if (i < lines.length) i++;
      blocks.push(
        <pre key={start}>
          <code>{code.join("\n")}</code>
        </pre>,
      );
      continue;
    }
    const heading = lines[i].match(/^#{1,3}\s+(.+)$/);
    if (heading) {
      blocks.push(<h3 key={start}>{inline(heading[1])}</h3>);
      i++;
      continue;
    }
    const ordered = /^\s*\d+\.\s+/.test(lines[i]),
      unordered = /^\s*[-*]\s+/.test(lines[i]);
    if (ordered || unordered) {
      const pattern = ordered ? /^\s*\d+\.\s+/ : /^\s*[-*]\s+/,
        items = [];
      while (i < lines.length && pattern.test(lines[i])) {
        items.push(<li key={i}>{inline(lines[i].replace(pattern, ""))}</li>);
        i++;
      }
      blocks.push(
        ordered ? <ol key={start}>{items}</ol> : <ul key={start}>{items}</ul>,
      );
      continue;
    }
    const paragraph = [lines[i++]];
    while (
      i < lines.length &&
      lines[i].trim() &&
      !/^(?:```|#{1,3}\s|\s*[-*]\s|\s*\d+\.\s)/.test(lines[i])
    )
      paragraph.push(lines[i++]);
    blocks.push(<p key={start}>{inline(paragraph.join("\n"))}</p>);
  }
  return (
    <div
      className={`nora-reply${streaming ? " is-streaming" : ""}`}
      aria-live="off"
    >
      {blocks}
      {streaming && <span className="nora-stream-caret" aria-hidden="true" />}
    </div>
  );
}
