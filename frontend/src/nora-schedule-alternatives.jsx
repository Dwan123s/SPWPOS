import { useId, useRef, useState } from "react";
import { icon } from "./ui.jsx";

function alternativeReply(turn, date, start, endDate, end) {
  const selection =
    date === endDate
      ? `Tôi chọn từ ${start} đến ${end} ngày ${date}.`
      : `Tôi chọn từ ${start} ngày ${date} đến ${end} ngày ${endDate}.`;
  const text = `${selection} Thực hiện lại yêu cầu dưới đây ở khung giờ mới, thay cho khung giờ ban đầu; giữ nguyên các thông tin khác:\n${turn?.text || ""}`;
  return text.length <= 2000
    ? text
    : `${selection} Thực hiện lại yêu cầu tôi vừa gửi ở khung giờ mới này, giữ nguyên các thông tin khác.`;
}

function isTimeConflict(turn) {
  return (
    ["FAILED", "BLOCKED"].includes(turn?.status) &&
    [
      "FIXED_EVENT_OVERLAP",
      "SESSION_OVERLAP",
      "LOCKED_SESSION_CONFLICT",
      "BUSY_TIME_CONFLICT",
    ].includes(turn.errorCode)
  );
}

// Stored conflict replies already contain server-checked suggestions. Read only
// that labelled section, never infer choices from arbitrary times in a reply.
export function scheduleAlternativeView(turn) {
  const options = [];
  if (!["FAILED", "BLOCKED"].includes(turn.status))
    return { reply: turn.reply, options };
  const reply = (turn.reply || "")
    .replace(
      /Đề xuất giữ nguyên thời lượng:\s*([^\n]+?)\.\s*Bạn chọn một khung giờ để thử lại\./g,
      (original, list) => {
        const parsed = list.split(/;\s*/).map((item) => {
          const match = item
            .trim()
            .match(
              /^(\d{2}\/\d{2}\/\d{4})\s+([0-2]\d:[0-5]\d)\s*[–→-]\s*(?:(\d{2}\/\d{2}\/\d{4})\s+)?([0-2]\d:[0-5]\d)$/,
            );
          if (!match) return null;
          const [, date, start, endDate = date, end] = match;
          const validDate = (value) => {
            const [day, month, year] = value.split("/").map(Number);
            const check = new Date(Date.UTC(year, month - 1, day));
            return (
              year >= 2000 &&
              year <= 2100 &&
              check.getUTCFullYear() === year &&
              check.getUTCMonth() === month - 1 &&
              check.getUTCDate() === day
            );
          };
          if (
            !validDate(date) ||
            !validDate(endDate) ||
            start > "23:59" ||
            end > "24:00" ||
            (date === endDate && start >= end)
          )
            return null;
          const label = `${date} · ${start}–${endDate === date ? "" : `${endDate} `}${end}`;
          return {
            label,
            text: alternativeReply(turn, date, start, endDate, end),
          };
        });
        if (
          !parsed.length ||
          parsed.length > 3 ||
          parsed.some((option) => !option)
        )
          return original;
        options.push(...parsed);
        return "";
      },
    )
    .trim();
  return {
    reply,
    options: options.filter(
      (option, index) =>
        options.findIndex((other) => other.label === option.label) === index,
    ),
  };
}

export function NoraScheduleAlternatives({ options, busy, onReply, turn }) {
  const [prompt, setPrompt] = useState("");
  const [error, setError] = useState("");
  const [sending, setSending] = useState(false);
  const pending = useRef(false);
  const hintId = useId();
  const disabled = busy || sending || !onReply;
  if (!options.length && !isTimeConflict(turn)) return null;

  async function submitCustom(event) {
    event.preventDefault();
    event.stopPropagation();
    if (disabled || pending.current) return;
    setError("");
    try {
      const value = prompt.trim();
      if (!value) throw new Error("Hãy nhập thời gian khác bạn muốn.");
      if (value.length > 1500) throw new Error("Nhập tối đa 1.500 ký tự.");
      const selection = `Tôi muốn đổi thời gian cho yêu cầu này: ${value}\n`;
      const full = `${selection}Thực hiện lại yêu cầu dưới đây theo thời gian vừa nhập, thay cho khung giờ ban đầu; giữ nguyên các thông tin khác:\n${turn?.text || ""}`;
      const text =
        full.length <= 2000
          ? full
          : `${selection}Thực hiện lại yêu cầu tôi vừa gửi theo thời gian vừa nhập, giữ nguyên các thông tin khác.`;
      pending.current = true;
      setSending(true);
      await onReply(text);
    } catch (failure) {
      setError(failure.message);
    } finally {
      pending.current = false;
      setSending(false);
    }
  }
  return (
    <section className="nora-time-alternatives" aria-label="Khung giờ thay thế">
      <p>Chọn giờ khác cho yêu cầu mới</p>
      <div className="agent-option-chips">
        {options.map((option) => (
          <button
            key={option.label}
            type="button"
            className="btn secondary small agent-chip-btn"
            disabled={disabled}
            onClick={() => onReply(option.text)}
          >
            {icon("clock")}
            {option.label}
          </button>
        ))}
      </div>
      <form className="nora-custom-time" onSubmit={submitCustom}>
        <fieldset disabled={disabled}>
          <legend>Hoặc nhập thời gian khác</legend>
          <textarea
            name="timePrompt"
            aria-label="Thời gian khác"
            placeholder="Ví dụ: Mai 9h sáng, hoặc thứ Sáu sau 20h…"
            rows={2}
            maxLength={1500}
            required
            disabled={disabled}
            value={prompt}
            aria-describedby={hintId}
            onChange={(event) => {
              setPrompt(event.target.value);
              setError("");
            }}
          />
          <p id={hintId} className="nora-custom-time-hint">
            Ghi thời gian bạn muốn bằng lời. NORA sẽ hiểu yêu cầu và kiểm tra
            lại lịch.
          </p>
          {error && (
            <p className="nora-custom-time-error" role="alert">
              {error}
            </p>
          )}
          <button
            type="submit"
            className="btn primary small"
            disabled={disabled}
          >
            {icon("arrow")}
            {sending ? "Đang gửi…" : "Gửi thời gian khác"}
          </button>
        </fieldset>
      </form>
    </section>
  );
}
