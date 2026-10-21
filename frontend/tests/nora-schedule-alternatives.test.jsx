import { expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import {
  NoraScheduleAlternatives,
  scheduleAlternativeView,
} from "../src/nora-schedule-alternatives.jsx";

const turn = {
  status: "FAILED",
  text: "lên lịch mai tôi đi học vào lúc 18h đến 19h",
  reply:
    "Sự kiện nằm trong giờ đã đánh dấu bận (22/10/2026 17:00–20:00). Chưa lưu thay đổi. Đề xuất giữ nguyên thời lượng: 22/10/2026 21:00–22:00; 23/10/2026 19:00–20:00; 26/10/2026 19:00–20:00. Bạn chọn một khung giờ để thử lại.",
};

it.each(["FAILED", "BLOCKED"])(
  "shows each verified alternative as a separate selectable chip for %s",
  (status) => {
    const view = scheduleAlternativeView({ ...turn, status });
    const reply = vi.fn();
    render(<NoraScheduleAlternatives options={view.options} onReply={reply} />);
    expect(screen.getAllByRole("button")).toHaveLength(3);
    expect(
      screen.getByRole("button", { name: "22/10/2026 · 21:00–22:00" }),
    ).toBeTruthy();
    expect(view.reply).toContain("17:00–20:00");
    expect(view.reply).not.toContain("Đề xuất giữ nguyên thời lượng:");
    expect(reply).not.toHaveBeenCalled();
    fireEvent.click(
      screen.getByRole("button", { name: "23/10/2026 · 19:00–20:00" }),
    );
    expect(reply).toHaveBeenCalledTimes(1);
    const selected = reply.mock.calls[0][0];
    expect(selected).toContain("Tôi chọn từ 19:00 đến 20:00 ngày 23/10/2026.");
    expect(selected).toContain(turn.text);
    expect(selected).not.toContain("26/10/2026");
  },
);

it("disables all time chips while NORA is processing", () => {
  const reply = vi.fn();
  render(
    <NoraScheduleAlternatives
      options={scheduleAlternativeView(turn).options}
      busy
      onReply={reply}
    />,
  );
  for (const button of screen.getAllByRole("button")) {
    expect(button.disabled).toBe(true);
    fireEvent.click(button);
  }
  expect(reply).not.toHaveBeenCalled();
});

it.each(["ANSWERED", "APPLIED", "PREVIEW", "UNDONE"])(
  "does not turn ordinary response times into suggested actions for %s",
  (status) => {
    const view = scheduleAlternativeView({ ...turn, status });
    expect(view.options).toEqual([]);
    expect(view.reply).toBe(turn.reply);
  },
);

it("keeps malformed suggestions visible as prose without creating an invalid chip", () => {
  const invalid = {
    ...turn,
    reply: turn.reply.replace("23/10/2026 19:00", "31/02/2026 19:00"),
  };
  const view = scheduleAlternativeView(invalid);
  expect(view.options).toEqual([]);
  expect(view.reply).toBe(invalid.reply);
});

it("also renders older stored suggestions that include a full date at each end", () => {
  const view = scheduleAlternativeView({
    ...turn,
    reply:
      "Trùng lịch. Đề xuất giữ nguyên thời lượng: 22/10/2026 21:00 → 22/10/2026 22:00. Bạn chọn một khung giờ để thử lại.",
  });
  expect(view.options[0].label).toBe("22/10/2026 · 21:00–22:00");
});

it("keeps a chip reply within the existing message limit for a maximum-length request", () => {
  const view = scheduleAlternativeView({ ...turn, text: "a".repeat(2000) });
  expect(view.options[0].text.length).toBeLessThanOrEqual(2000);
  expect(view.options[0].text).toContain("21:00 đến 22:00 ngày 22/10/2026");
});
