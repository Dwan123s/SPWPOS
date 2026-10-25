import { expect, it, vi } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
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
    expect(screen.getAllByRole("button")).toHaveLength(4);
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

function customTimeForm(onReply, source = turn, busy = false) {
  render(
    <NoraScheduleAlternatives
      turn={source}
      options={scheduleAlternativeView(source).options}
      onReply={onReply}
      busy={busy}
    />,
  );
  return document.querySelector(".nora-custom-time");
}
function fillPrompt(value) {
  fireEvent.change(screen.getByRole("textbox", { name: "Thời gian khác" }), {
    target: { value },
  });
}

it.each(["Mai 22h, giữ nguyên thời lượng giúp tôi", "thu sau sau 20h toois"])(
  "sends a natural-language time prompt unchanged with the original request: %s",
  async (prompt) => {
    const reply = vi.fn();
    const form = customTimeForm(reply);
    fillPrompt(prompt);
    expect(reply).not.toHaveBeenCalled();
    await act(async () => fireEvent.submit(form));
    expect(reply).toHaveBeenCalledExactlyOnceWith(
      expect.stringContaining(prompt),
    );
    expect(reply.mock.calls[0][0]).toContain(turn.text);
  },
);

it("rejects empty or overlong prompts without clearing entered values", async () => {
  const reply = vi.fn();
  const form = customTimeForm(reply);
  fillPrompt("   ");
  await act(async () => fireEvent.submit(form));
  expect(screen.getByRole("alert").textContent).toContain("Hãy nhập");
  fillPrompt("a".repeat(1501));
  await act(async () => fireEvent.submit(form));
  expect(screen.getByRole("alert").textContent).toContain("1.500");
  expect(
    screen.getByRole("textbox", { name: "Thời gian khác" }).value,
  ).toHaveLength(1501);
  expect(reply).not.toHaveBeenCalled();
});

it("keeps a long prompt and conversation reference within the API message limit", async () => {
  const reply = vi.fn();
  const form = customTimeForm(reply, { ...turn, text: "a".repeat(2000) });
  fillPrompt("b".repeat(1500));
  await act(async () => fireEvent.submit(form));
  expect(reply).toHaveBeenCalledTimes(1);
  expect(reply.mock.calls[0][0].length).toBeLessThanOrEqual(2000);
  expect(reply.mock.calls[0][0]).toContain("b".repeat(1500));
  expect(reply.mock.calls[0][0]).toContain("yêu cầu tôi vừa gửi");
});

it("allows custom time for a calendar conflict even when no suggestions fit", () => {
  customTimeForm(vi.fn(), {
    ...turn,
    reply: "Chưa tìm được giờ trống.",
    errorCode: "BUSY_TIME_CONFLICT",
  });
  expect(screen.getByRole("textbox", { name: "Thời gian khác" })).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Gửi thời gian khác" }),
  ).toBeTruthy();
});

it.each(["AI_UNAVAILABLE", "VALIDATION_ERROR"])(
  "does not show scheduling fields for unrelated %s failures",
  (errorCode) => {
    customTimeForm(vi.fn(), { ...turn, reply: "Không thể xử lý.", errorCode });
    expect(
      screen.queryByRole("textbox", { name: "Thời gian khác" }),
    ).toBeNull();
  },
);

it("blocks duplicate submissions and retains a failed custom request for retry", async () => {
  let reject;
  const reply = vi.fn(
    () =>
      new Promise((resolve, fail) => {
        reject = fail;
      }),
  );
  const form = customTimeForm(reply);
  fillPrompt("Mai sau 19h");
  fireEvent.submit(form);
  fireEvent.submit(form);
  expect(reply).toHaveBeenCalledTimes(1);
  expect(screen.getByRole("button", { name: "Đang gửi…" }).disabled).toBe(true);
  expect(screen.getByRole("textbox", { name: "Thời gian khác" }).disabled).toBe(
    true,
  );
  await act(async () => reject(new Error("Mất kết nối, thử lại.")));
  expect(screen.getByRole("alert").textContent).toContain("Mất kết nối");
  expect(screen.getByRole("textbox", { name: "Thời gian khác" }).value).toBe(
    "Mai sau 19h",
  );
  expect(
    screen.getByRole("button", { name: "Gửi thời gian khác" }).disabled,
  ).toBe(false);
});
