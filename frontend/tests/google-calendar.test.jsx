import { afterEach, beforeEach, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { GoogleCalendarCard } from "../src/google-calendar.jsx";
import { api } from "../src/api.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
const connected = {
  configured: true,
  connected: true,
  status: "Synced",
  email: "calendar@example.com",
  reminderMinutes: 15,
  remindAtStart: false,
  pendingChanges: 0,
  conflicts: 0,
  lastSyncedAt: "2026-10-30T03:00:00Z",
  webhookEnabled: false,
  calendarUrl: "https://calendar.google.com/calendar/u/0/r",
};
beforeEach(() => {
  api.mockReset();
  history.replaceState(null, "", "/#settings");
});
afterEach(cleanup);
it("shows declined Calendar permission as an actionable error instead of a green success notice", async () => {
  history.replaceState(
    null,
    "",
    "/?googleCalendar=CALENDAR_SCOPE_REQUIRED#settings",
  );
  api.mockResolvedValue({
    ...connected,
    connected: false,
    status: "Disconnected",
  });
  render(<GoogleCalendarCard />);
  expect(screen.getByRole("alert").textContent).toContain(
    "tích ô cho phép PLANORA tạo và quản lý lịch",
  );
  expect(document.querySelector(".calendar-notice")).toBeNull();
  await screen.findByText("Chưa kết nối");
  expect(window.location.search).toBe("");
});
it("does not expose arbitrary callback values in the error message", async () => {
  history.replaceState(null, "", "/?googleCalendar=private-canary#settings");
  api.mockResolvedValue({
    ...connected,
    connected: false,
    status: "Disconnected",
  });
  render(<GoogleCalendarCard />);
  expect(screen.getByRole("alert").textContent).toContain(
    "Hãy bấm kết nối để thử lại",
  );
  expect(screen.queryByText(/private-canary/)).toBeNull();
  await screen.findByText("Chưa kết nối");
});
it("shows a disabled integration without pretending it is connected", async () => {
  api.mockResolvedValue({
    ...connected,
    configured: false,
    connected: false,
    status: "NotConfigured",
  });
  render(<GoogleCalendarCard />);
  await screen.findByText("Chưa cấu hình máy chủ");
  expect(
    screen.getByRole("button", { name: "Kết nối Google Calendar" }).disabled,
  ).toBe(true);
  expect(screen.queryByText("calendar@example.com")).toBeNull();
});
it("requests a queued sync and saves future-event reminders independently", async () => {
  api.mockResolvedValue(connected);
  render(<GoogleCalendarCard />);
  await screen.findByText("Đã đồng bộ");
  fireEvent.click(screen.getByRole("button", { name: "Đồng bộ ngay" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/integrations/google-calendar/sync",
      "POST",
    ),
  );
  await screen.findByText(/Đã yêu cầu đồng bộ/);
  fireEvent.change(screen.getByRole("spinbutton"), { target: { value: "30" } });
  fireEvent.click(screen.getByRole("checkbox"));
  fireEvent.click(screen.getByRole("button", { name: "Lưu giờ nhắc" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/integrations/google-calendar/reminders",
      "PUT",
      { reminderMinutes: 30, remindAtStart: true },
    ),
  );
});
it("shows both conflict versions and prevents overwriting execution history", async () => {
  api.mockImplementation((path) =>
    Promise.resolve(
      path.endsWith("/conflicts")
        ? [
            {
              id: "conflict",
              baseline: null,
              local: {
                title: "Báo cáo",
                start: "2026-10-30T03:00:00Z",
                end: "2026-10-30T04:00:00Z",
              },
              google: null,
              readOnly: false,
              canKeepGoogle: false,
            },
          ]
        : { ...connected, conflicts: 1 },
    ),
  );
  render(<GoogleCalendarCard />);
  await screen.findByText("Hai bên đang có thay đổi khác nhau");
  expect(screen.getByRole("button", { name: "Giữ Google" }).disabled).toBe(
    true,
  );
  expect(screen.getByRole("button", { name: "Giữ PLANORA" }).disabled).toBe(
    false,
  );
});
it("keeps Google events when disconnecting and explains the result", async () => {
  api.mockImplementation((path, method) =>
    Promise.resolve(path.endsWith("/disconnect") ? {} : connected),
  );
  render(<GoogleCalendarCard />);
  await screen.findByText("Đã đồng bộ");
  fireEvent.click(screen.getByRole("button", { name: "Ngắt kết nối" }));
  await screen.findByText(
    "Đã ngắt kết nối. Các sự kiện trên Google vẫn được giữ.",
  );
  expect(api).toHaveBeenCalledWith(
    "/api/integrations/google-calendar/disconnect",
    "POST",
  );
});
