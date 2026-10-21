import { icon } from "./ui.jsx";

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
          const selection =
            date === endDate
              ? `Tôi chọn từ ${start} đến ${end} ngày ${date}.`
              : `Tôi chọn từ ${start} ngày ${date} đến ${end} ngày ${endDate}.`;
          const text = `${selection} Thực hiện lại yêu cầu dưới đây ở khung giờ mới, thay cho khung giờ ban đầu; giữ nguyên các thông tin khác:\n${turn.text || ""}`;
          return {
            label,
            text:
              text.length <= 2000
                ? text
                : `${selection} Thực hiện lại yêu cầu tôi vừa gửi ở khung giờ mới này, giữ nguyên các thông tin khác.`,
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

export function NoraScheduleAlternatives({ options, busy, onReply }) {
  if (!options.length) return null;
  return (
    <section className="nora-time-alternatives" aria-label="Khung giờ thay thế">
      <p>Chọn giờ thay thế</p>
      <div className="agent-option-chips">
        {options.map((option) => (
          <button
            key={option.label}
            type="button"
            className="btn secondary small agent-chip-btn"
            disabled={busy || !onReply}
            onClick={() => onReply(option.text)}
          >
            {icon("clock")}
            {option.label}
          </button>
        ))}
      </div>
    </section>
  );
}
