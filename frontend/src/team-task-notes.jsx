import { useRef, useState } from "react";
import { downloadPrivateFile } from "./api.js";
import { icon } from "./ui.jsx";
import { TEAM_BRIEF_ACCEPT, readTeamBriefFile } from "./team-brief-file.js";

const sectionNames = [
  ["Tóm tắt", "spark", ["tom tat", "tong quan", "summary"]],
  [
    "Việc cần thực hiện",
    "tasks",
    ["yeu cau can thuc hien", "yeu cau", "cong viec can lam", "requirements"],
  ],
  [
    "Đầu ra cần bàn giao",
    "folder",
    ["dau ra can ban giao", "dau ra", "deliverables"],
  ],
  [
    "Tiêu chí hoàn thành",
    "check",
    ["tieu chi hoan thanh", "acceptance criteria"],
  ],
  [
    "Ghi chú & phối hợp",
    "users",
    ["ghi chu & phoi hop", "ghi chu va phoi hop", "ghi chu", "notes"],
  ],
  [
    "Tham chiếu tài liệu",
    "folder",
    ["tham chieu tai lieu", "source references"],
  ],
  ["Căn cứ deadline", "calendar", ["can cu deadline"]],
  ["Tài liệu nguồn", "folder", ["tai lieu nguon"]],
];
const normalize = (text) =>
  text
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase()
    .trim();

export function parseTaskNotes(description = "") {
  const sections = [];
  let current = { title: "Nội dung công việc", glyph: "tasks", lines: [] };
  for (const line of description.replace(/\r\n?/g, "\n").split("\n")) {
    const clean = line
      .trim()
      .replace(/^(?:#{1,6}\s+|\*\*|__)/, "")
      .replace(/(?:\*\*|__)\s*$/, "")
      .replace(/(?:&#x20;|&nbsp;)$/i, "")
      .trim();
    const [heading, ...tail] = clean.split(/[:：]/);
    const known = sectionNames.find(([, , aliases]) =>
      aliases.includes(normalize(heading)),
    );
    const markdownHeading = /^#{1,6}\s+/.test(line.trim());
    if (known || markdownHeading) {
      if (current.explicit || current.lines.some((text) => text.trim()))
        sections.push(current);
      current = {
        title: known?.[0] || clean,
        glyph: known?.[1] || "tasks",
        explicit: true,
        lines: known && tail.length ? [tail.join(":").trim()] : [],
      };
    } else current.lines.push(line);
  }
  if (current.explicit || current.lines.some((text) => text.trim()))
    sections.push(current);
  return sections;
}

function InlineText({ text }) {
  return text
    .split(/(\*\*[^*\n]+\*\*)/g)
    .map((part, i) =>
      /^\*\*.+\*\*$/.test(part) ? (
        <strong key={i}>{part.slice(2, -2)}</strong>
      ) : (
        part
      ),
    );
}
export function TaskNoteContent({ lines }) {
  const blocks = [];
  let current;
  for (const line of lines) {
    if (!line.trim()) {
      current = null;
      continue;
    }
    const item = /^\s*(?:[•●▪*-]\s+|\d+[.)]\s+)(.*)$/.exec(line);
    const kind = item ? "list" : "text";
    if (!current || current.kind !== kind) {
      current = { kind, lines: [] };
      blocks.push(current);
    }
    current.lines.push(item ? item[1] : line);
  }
  return blocks.map((block, i) =>
    block.kind === "list" ? (
      <ul key={i}>
        {block.lines.map((text, n) => (
          <li key={n}>
            <InlineText text={text} />
          </li>
        ))}
      </ul>
    ) : (
      <p key={i}>
        <InlineText text={block.lines.join("\n")} />
      </p>
    ),
  );
}
export function TaskRequirements({ task }) {
  const sections = parseTaskNotes(task.description || "");
  return (
    <section className="team-task-notes" aria-label="Yêu cầu & ghi chú">
      <header>
        <span>{icon("tasks")}</span>
        <div>
          <h3>Yêu cầu & ghi chú</h3>
          <p>Nội dung cần nắm trước khi bắt đầu công việc.</p>
        </div>
      </header>
      {sections.length ? (
        <div className="team-task-note-sections">
          {sections.map((section, i) => (
            <section
              className={`team-task-note-section${section.title === "Tóm tắt" ? " summary" : ""}`}
              key={i}
            >
              <h4>
                <span>{icon(section.glyph)}</span>
                {section.title}
              </h4>
              <TaskNoteContent lines={section.lines} />
            </section>
          ))}
        </div>
      ) : (
        <p className="team-task-notes-empty">
          Chưa có ghi chú cho công việc này.
        </p>
      )}
      <div className="team-task-work-mode">
        {icon("clock")}{" "}
        {task.splittable
          ? "Có thể chia thành nhiều phiên làm việc"
          : "Thực hiện trong một phiên liên tục"}
      </div>
    </section>
  );
}

const sizeLabel = (size) =>
  size >= 1024 * 1024
    ? `${(size / (1024 * 1024)).toFixed(1)} MB`
    : `${Math.max(1, Math.ceil(size / 1024))} KB`;
export function TaskSourceFiles({ task, leader, busy, onAttach }) {
  const [downloading, setDownloading] = useState(null);
  const [error, setError] = useState("");
  const [pending, setPending] = useState(null);
  const [reading, setReading] = useState(false);
  const picker = useRef(null);
  return (
    <section
      className="team-task-source-files"
      aria-label="Tài liệu từ trưởng nhóm"
    >
      <header>
        <h3>{icon("folder")} Tài liệu từ trưởng nhóm</h3>
        <span>{task.sourceFiles?.length || 0} file</span>
      </header>
      {task.sourceFiles?.length ? (
        <ul>
          {task.sourceFiles.map((file) => (
            <li key={file.id}>
              <span className="team-source-file-type">
                {file.name.split(".").pop()?.toUpperCase()}
              </span>
              <div>
                <strong>{file.name}</strong>
                <small>{sizeLabel(file.size)} · Tài liệu gốc</small>
              </div>
              <button
                type="button"
                className="btn secondary"
                aria-label={`Tải ${file.name}`}
                disabled={!!downloading}
                onClick={async () => {
                  setDownloading(file.id);
                  setError("");
                  try {
                    await downloadPrivateFile(
                      `/api/teams/tasks/${task.id}/source/files/${file.id}`,
                      file.name,
                    );
                  } catch (failure) {
                    setError(failure.message);
                  } finally {
                    setDownloading(null);
                  }
                }}
              >
                {downloading === file.id ? "Đang tải…" : "Tải xuống"}{" "}
                <span aria-hidden="true">↓</span>
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p>Chưa có tài liệu đính kèm.</p>
      )}
      {leader && onAttach && !task.sourceFiles?.length && (
        <div className="team-source-attach">
          <input
            ref={picker}
            type="file"
            accept={TEAM_BRIEF_ACCEPT}
            hidden
            aria-label="Chọn tài liệu của trưởng nhóm"
            disabled={busy || reading}
            onChange={async (event) => {
              const file = event.target.files?.[0];
              event.target.value = "";
              if (!file) return;
              setError("");
              setReading(true);
              try {
                setPending({
                  ...(await readTeamBriefFile(file)),
                  requestId: crypto.randomUUID(),
                });
              } catch (failure) {
                setError(failure.message);
              } finally {
                setReading(false);
              }
            }}
          />
          {pending ? (
            <>
              <strong>{pending.name}</strong>
              <small>
                {sizeLabel(pending.size)} · Thành viên sẽ tải được sau khi gửi.
              </small>
              <div>
                <button
                  type="button"
                  className="btn primary"
                  disabled={busy || reading}
                  onClick={async () => {
                    setError("");
                    try {
                      if (await onAttach(pending)) setPending(null);
                    } catch (failure) {
                      setError(failure.message);
                    }
                  }}
                >
                  {busy ? "Đang gửi…" : "Gửi tài liệu"}
                </button>
                <button
                  type="button"
                  className="team-text-button"
                  disabled={busy || reading}
                  onClick={() => {
                    setPending(null);
                    setError("");
                  }}
                >
                  Bỏ chọn
                </button>
              </div>
            </>
          ) : (
            <>
              <button
                type="button"
                className="btn secondary"
                disabled={busy || reading}
                onClick={() => picker.current?.click()}
              >
                {icon("plus")}
                {reading ? "Đang đọc…" : "Đính kèm tài liệu"}
              </button>
              <small>
                PDF, Word, TXT, Markdown · tối đa 8 MB · Chia sẻ cho thành viên
                nhóm.
              </small>
            </>
          )}
        </div>
      )}
      {error && (
        <p className="team-error" role="alert">
          {error}
        </p>
      )}
    </section>
  );
}
