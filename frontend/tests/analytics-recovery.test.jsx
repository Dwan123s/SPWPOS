import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { api } from "../src/api.js";
import { AnalyticsHistory } from "../src/analytics.jsx";
import { RecoveryPanel } from "../src/recovery.jsx";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
const totals = {
  focusSeconds: 1800,
  plannedSeconds: 3600,
  openFocusSeconds: 60,
  completedTasks: 1,
  onTimeTasks: 1,
};
beforeEach(() => {
  vi.clearAllMocks();
  api.mockImplementation(async (path) =>
    path.startsWith("/api/intelligence/tasks")
      ? { items: [], total: 0, page: 1, pageSize: 20 }
      : {
          start: "2026-10-09",
          end: "2026-10-15",
          timeZoneId: "Asia/Ho_Chi_Minh",
          days: [],
          current: totals,
          previous: totals,
          focusChangePercent: null,
          notices: ["OPEN_TIMER_PROVISIONAL", "NO_COMPARISON_BASELINE"],
        },
  );
});
it("history changes the server range and identifies provisional timers", async () => {
  render(<AnalyticsHistory />);
  await screen.findByText(/Thời gian phiên đang chạy là tạm tính/);
  expect(screen.getByText(/Kỳ trước chưa có thời gian tập trung/)).toBeTruthy();
  fireEvent.change(screen.getByLabelText("Khoảng thống kê"), {
    target: { value: "3" },
  });
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/intelligence/history?days=3"),
  );
  fireEvent.change(screen.getByLabelText("Khoảng thống kê"), {
    target: { value: "30" },
  });
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/intelligence/history?days=30"),
  );
});
it("malformed analytics response becomes a recoverable error", async () => {
  api.mockResolvedValue({});
  render(<AnalyticsHistory />);
  expect(await screen.findByText(/Chưa tải được thống kê hợp lệ/)).toBeTruthy();
  expect(screen.queryByText("Tập trung đã ghi nhận")).toBeNull();
});
it("recovery loads read-only and applies only after a click", async () => {
  const record = {
    id: "case",
    status: "PROPOSED",
    message: "Hai ngày bị lỡ",
    taskIds: ["task"],
    plan: { sessions: [], warnings: [] },
    createdAt: "2026-10-15T12:00:00Z",
  };
  api.mockImplementation(async (path, method) =>
    method
      ? {}
      : {
          items: [record],
          page: 1,
          total: 1,
          settings: {
            enabled: true,
            autoApply: false,
            missedDays: 2,
            quietStartMinute: 1320,
            quietEndMinute: 420,
            version: "version",
          },
        },
  );
  const changed = vi.fn();
  render(<RecoveryPanel onWorkspaceChanged={changed} />);
  await screen.findByText("Hai ngày bị lỡ");
  expect(api.mock.calls.every(([, method]) => !method)).toBe(true);
  expect(
    screen.getByLabelText("Tự áp dụng khi đủ thời gian và không có cảnh báo")
      .checked,
  ).toBe(false);
  fireEvent.click(screen.getByRole("button", { name: "Áp dụng đề xuất" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/recovery/case/apply",
      "POST",
      undefined,
    ),
  );
  await waitFor(() => expect(changed).toHaveBeenCalled());
});

it("keeps an empty recovery state compact and disables auto-apply when recovery is off", async () => {
  api.mockResolvedValue({
    items: [],
    total: 0,
    settings: {
      enabled: true,
      autoApply: true,
      missedDays: 3,
      quietStartMinute: 1320,
      quietEndMinute: 420,
      version: "v1",
    },
  });
  render(<RecoveryPanel />);
  await screen.findByText("Bạn đang giữ nhịp tốt");
  expect(
    screen.queryByRole("navigation", { name: "Phân trang phục hồi" }),
  ).toBeNull();
  expect(screen.getByText(/Gợi ý lịch mới khi bạn lỡ 3 ngày/)).toBeTruthy();
  fireEvent.click(screen.getByLabelText("Tạo đề xuất phục hồi"));
  expect(
    screen.getByLabelText("Tự áp dụng khi đủ thời gian và không có cảnh báo")
      .disabled,
  ).toBe(true);
  fireEvent.submit(document.querySelector(".recovery-settings-form"));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/recovery/settings",
      "PUT",
      expect.objectContaining({ enabled: false, autoApply: false }),
    ),
  );
});

it("does not show an empty recovery state before its initial request completes", async () => {
  api.mockReturnValue(new Promise(() => {}));
  render(<RecoveryPanel />);
  expect(screen.getByRole("status")).toBeTruthy();
  expect(screen.queryByText("Bạn đang giữ nhịp tốt")).toBeNull();
  expect(
    screen.getByRole("button", { name: "Kiểm tra kế hoạch" }).disabled,
  ).toBe(true);
});

it("hides task pagination for empty analytics results", async () => {
  render(<AnalyticsHistory />);
  await screen.findByText("Chưa có công việc phù hợp.");
  expect(
    screen.queryByRole("navigation", { name: "Phân trang thống kê" }),
  ).toBeNull();
});
