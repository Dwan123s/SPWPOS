import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { NoraReminderClock, NoraReminders } from "../src/nora-reminders.jsx";
import { state } from "../src/ui.jsx";
import { NoraResultCard } from "../src/nora-result-card.jsx";
import { TaskInbox } from "../src/task-inbox.jsx";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
vi.mock("../src/notification-sound.js", () => ({
  startNoraReminderAlarm: vi.fn(),
}));
import { api } from "../src/api.js";
import { startNoraReminderAlarm } from "../src/notification-sound.js";
let rows, stopAlarm;
beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  state.workspace = {
    profile: { id: "owner" },
    preference: { timeZoneId: "Asia/Ho_Chi_Minh" },
    notificationSettings: { inAppEnabled: true },
  };
  rows = [
    {
      id: "alarm",
      message: "Trả lời email khách hàng",
      at: new Date(Date.now() + 60000).toISOString(),
      due: false,
    },
  ];
  stopAlarm = vi.fn();
  startNoraReminderAlarm.mockReturnValue(stopAlarm);
  api.mockImplementation(async (path, method) => {
    if (method === "DELETE" || path.endsWith("/read")) {
      rows = [];
      return {};
    }
    return rows;
  });
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

it("opens reminder management inside NORA, cancels and drafts a reminder prompt", async () => {
  const compose = vi.fn();
  render(<NoraReminders onCompose={compose} />);
  fireEvent.click(
    screen.getByRole("button", { name: "Lời nhắc", exact: true }),
  );
  const dialog = within(
    screen.getByRole("dialog", { name: "Lời nhắc của bạn" }),
  );
  await waitFor(() =>
    expect(dialog.getByText("Trả lời email khách hàng")).toBeTruthy(),
  );
  fireEvent.click(
    dialog.getByRole("button", { name: "Hủy nhắc: Trả lời email khách hàng" }),
  );
  await waitFor(() =>
    expect(dialog.getByText("Chưa có lời nhắc nào.")).toBeTruthy(),
  );
  expect(api).toHaveBeenCalledWith("/api/agent/reminders/alarm", "DELETE");
  fireEvent.click(
    dialog.getByRole("button", { name: "Đặt lời nhắc với NORA" }),
  );
  expect(compose).toHaveBeenCalledWith("Nhắc tôi ");
  expect(screen.queryByRole("dialog")).toBeNull();
});

it("rings at the deadline and keeps the popup until explicit dismissal, even across page updates", async () => {
  vi.useFakeTimers();
  const now = new Date("2026-11-04T03:00:00Z");
  vi.setSystemTime(now);
  rows = [
    {
      id: "alarm",
      message: "Trả lời email khách hàng",
      at: new Date(now.getTime() + 60000).toISOString(),
      due: false,
    },
  ];
  api.mockImplementation(async (path, method) => {
    if (path.endsWith("/read")) {
      rows = [];
      return {};
    }
    return rows.map((row) => ({
      ...row,
      due: Date.parse(row.at) <= Date.now(),
    }));
  });
  const view = render(<NoraReminderClock userId="owner" />);
  await act(async () => {});
  expect(screen.queryByRole("alertdialog")).toBeNull();
  await act(async () => vi.advanceTimersByTimeAsync(60100));
  expect(screen.getByRole("alertdialog")).toBeTruthy();
  expect(startNoraReminderAlarm).toHaveBeenCalledWith("owner");
  fireEvent.keyDown(document, { key: "Escape" });
  fireEvent.click(document.body);
  view.rerender(
    <>
      <h1>Lịch làm việc</h1>
      <NoraReminderClock userId="owner" />
    </>,
  );
  await act(async () => {});
  expect(screen.getByRole("alertdialog")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Tắt chuông" }));
  await act(async () => {});
  expect(screen.queryByRole("alertdialog")).toBeNull();
  expect(stopAlarm).toHaveBeenCalled();
  expect(api).toHaveBeenCalledWith("/api/notifications/alarm/read", "POST");
  view.unmount();
  render(<NoraReminderClock userId="owner" />);
  await act(async () => {});
  expect(screen.queryByRole("alertdialog")).toBeNull();
});

it("groups due reminders and keeps a local dismissal if acknowledgement is temporarily offline", async () => {
  rows = [
    { id: "a", message: "Gọi khách", at: new Date().toISOString(), due: true },
    { id: "b", message: "Uống nước", at: new Date().toISOString(), due: true },
  ];
  api.mockImplementation(async (path) => {
    if (path.endsWith("/read")) throw new Error("Offline");
    return rows;
  });
  const view = render(<NoraReminderClock userId="owner" />);
  await waitFor(() => expect(screen.getByRole("alertdialog")).toBeTruthy());
  expect(screen.getByText("NORA nhắc bạn · 2 lời nhắc")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Tắt chuông" }));
  expect(screen.queryByRole("alertdialog")).toBeNull();
  view.unmount();
  render(<NoraReminderClock userId="owner" />);
  await act(async () => {});
  expect(screen.queryByRole("alertdialog")).toBeNull();
});

it("does not reintroduce countdown or reminder creation in Inbox", () => {
  render(
    <TaskInbox
      notes={[
        {
          id: "note",
          title: "Mail",
          content: "1 phút nữa rep mail",
          createdAt: new Date().toISOString(),
          inboxReminderAt: rows[0].at,
        },
      ]}
      query=""
      onRefresh={vi.fn()}
      onSaved={vi.fn()}
    />,
  );
  expect(
    screen.queryByText(/Bắt đầu đếm ngược|Nhắc sau|Nhắc lại|để đặt nhắc/),
  ).toBeNull();
  expect(
    screen.getByText("Enter để lưu · Shift + Enter để xuống dòng"),
  ).toBeTruthy();
});

it("shows a reminder result instead of suggesting calendar controls", () => {
  render(
    <NoraResultCard
      turn={{
        status: "APPLIED",
        reply: "Đã đặt nhắc gọi khách.",
        changes: [{ kind: "REMINDER", name: "Gọi khách", after: "10:00" }],
      }}
      busy={false}
      onAction={vi.fn()}
    />,
  );
  expect(
    screen.getByRole("heading", { name: "Lời nhắc đã cập nhật" }),
  ).toBeTruthy();
  expect(screen.queryByText("Mở lịch")).toBeNull();
  expect(screen.queryByText("Xem kết quả này")).toBeNull();
});
