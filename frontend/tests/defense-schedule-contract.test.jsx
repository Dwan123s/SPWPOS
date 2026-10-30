import { expect, it } from "vitest";
import { scheduleAlternativeView } from "../src/nora-schedule-alternatives.jsx";

// Actual response shape captured from the Windows backend in live D11 on
// 2026-10-30. Component-only fixtures previously hid this server/client mismatch.
it("offers the three verified alternatives emitted by the Windows backend", () => {
  const turn = {
    status: "FAILED",
    errorCode: "SESSION_OVERLAP",
    text: "Thêm cuộc hẹn ngày 26/10/2026 19:00–20:00 và dời phiên bị trùng.",
    reply:
      "Sự kiện trùng với phiên làm việc đã lưu (26-10-2026 19:00 → 26-10-2026 20:00). Chưa lưu thay đổi. Đề xuất giữ nguyên thời lượng: 26-10-2026 21:00–22:00; 27-10-2026 20:00–21:00; 28-10-2026 19:00–20:00. Bạn chọn một khung giờ để thử lại.",
  };
  const view = scheduleAlternativeView(turn);
  expect(view.options).toHaveLength(3);
  expect(view.options.map((option) => option.label)).toEqual([
    "26/10/2026 · 21:00–22:00",
    "27/10/2026 · 20:00–21:00",
    "28/10/2026 · 19:00–20:00",
  ]);
});
