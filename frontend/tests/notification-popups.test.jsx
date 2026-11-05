import { afterEach, beforeEach, expect, it, vi } from "vitest";
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
} from "@testing-library/react";
import {
  NotificationBell,
  NotificationPopups,
  NotificationUnreadCount,
} from "../src/notification-popups.jsx";
import {
  dismissNotificationPopup,
  notificationSnapshot,
  previewNotificationPopup,
  resetNotificationFeed,
  syncNotifications,
} from "../src/notification-feed.js";
import { startNotificationPolling } from "../src/notification-polling.js";
import { state } from "../src/ui.jsx";
import {
  pendingTeamJoinReview,
  finishTeamJoinReview,
} from "../src/team-notification-link.js";
const reminder = (id, isRead = false) => ({
  id,
  kind: "SESSION_REMINDER",
  title: `Nhắc ${id}`,
  message: "Sắp bắt đầu phiên học",
  createdAt: "2026-10-30T07:00:00Z",
  isRead,
});
function setup(items = []) {
  state.workspace = {
    profile: { id: "member" },
    notifications: items,
    notificationSettings: { inAppEnabled: true },
    features: ["notification-refresh"],
  };
  act(() => syncNotifications(state.workspace));
}
beforeEach(() => {
  localStorage.clear();
  resetNotificationFeed();
  state.workspace = null;
});
afterEach(() => {
  finishTeamJoinReview(pendingTeamJoinReview(state.workspace?.profile?.id));
  cleanup();
  resetNotificationFeed();
  vi.useRealTimers();
});

it("opens the requesting group from a new join popup and dismisses the popup", () => {
  setup();
  render(<NotificationPopups />);
  act(() =>
    syncNotifications({
      ...state.workspace,
      notifications: [
        {
          ...reminder("join"),
          kind: "TEAM_JOIN_REQUEST",
          teamId: "team-target",
        },
      ],
    }),
  );
  const link = screen.getByRole("link", { name: "Mở nhóm để duyệt" });
  expect(link.getAttribute("href")).toBe("#teams");
  fireEvent.click(link);
  expect(pendingTeamJoinReview("member")?.teamId).toBe("team-target");
  expect(pendingTeamJoinReview("other")).toBeNull();
  expect(notificationSnapshot().popups).toHaveLength(0);
});

it("removes a resolved approval action from an already visible popup on refresh", () => {
  setup();
  render(<NotificationPopups />);
  const notice = {
    ...reminder("join"),
    kind: "TEAM_JOIN_REQUEST",
    teamId: "team-target",
    joinRequestPending: true,
  };
  act(() => syncNotifications({ ...state.workspace, notifications: [notice] }));
  expect(screen.getByRole("link", { name: "Mở nhóm để duyệt" })).toBeTruthy();
  act(() =>
    syncNotifications({
      ...state.workspace,
      notifications: [{ ...notice, joinRequestPending: false }],
    }),
  );
  expect(screen.queryByRole("link", { name: "Mở nhóm để duyệt" })).toBeNull();
  expect(screen.getByText(notice.title)).toBeTruthy();
  expect(notificationSnapshot().popups[0].joinRequestPending).toBe(false);
});

it("shows existing unread once and updates the bell and popup on new arrivals", () => {
  setup([reminder("old")]);
  render(
    <>
      <NotificationBell />
      <NotificationUnreadCount />
      <NotificationPopups />
    </>,
  );
  expect(screen.getByText("Bạn có 1 thông báo chưa đọc")).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Thông báo, 1 chưa đọc" }),
  ).toBeTruthy();
  act(() =>
    syncNotifications({
      ...state.workspace,
      notifications: [reminder("new"), reminder("old")],
    }),
  );
  expect(screen.getByText("Nhắc new")).toBeTruthy();
  expect(screen.getByText("Bạn có 2 thông báo chưa đọc")).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Thông báo, 2 chưa đọc" }),
  ).toBeTruthy();
  fireEvent.click(
    screen.getByRole("button", { name: "Đóng nhắc nhở: Nhắc new" }),
  );
  act(() =>
    syncNotifications({
      ...state.workspace,
      notifications: [reminder("new"), reminder("old")],
    }),
  );
  expect(screen.queryByText("Nhắc new")).toBeNull();
  expect(
    notificationSnapshot().items.filter((item) => !item.isRead),
  ).toHaveLength(2);
});
it("does not repeat dismissed popups after a reload, and respects account isolation", () => {
  setup([reminder("one")]);
  act(() => dismissNotificationPopup("summary:member"));
  resetNotificationFeed();
  setup([reminder("one")]);
  expect(notificationSnapshot().popups).toHaveLength(0);
  act(() =>
    syncNotifications({
      profile: { id: "other" },
      notifications: [],
      notificationSettings: {},
    }),
  );
  expect(notificationSnapshot().items).toHaveLength(0);
  expect(notificationSnapshot().popups).toHaveLength(0);
});
it("removes read reminders, honors opt-out, and previews without fake inbox records", () => {
  setup();
  render(<NotificationPopups />);
  act(() =>
    syncNotifications({ ...state.workspace, notifications: [reminder("new")] }),
  );
  expect(screen.getByText("Nhắc new")).toBeTruthy();
  act(() =>
    syncNotifications({
      ...state.workspace,
      notifications: [reminder("new", true)],
    }),
  );
  expect(screen.queryByText("Nhắc new")).toBeNull();
  act(() =>
    syncNotifications({
      ...state.workspace,
      notificationSettings: { inAppEnabled: false },
      notifications: [reminder("off")],
    }),
  );
  expect(screen.queryByLabelText("Nhắc nhở mới")).toBeNull();
  setup();
  act(() => previewNotificationPopup());
  expect(screen.getByText("Thông báo thử")).toBeTruthy();
  expect(notificationSnapshot().items).toHaveLength(0);
});
it("polls while editing a note, throttles scans, and stops cleanly", async () => {
  vi.useFakeTimers();
  setup();
  state.route = "notes";
  const request = vi.fn(async () => [reminder("due")]);
  let stop;
  await act(async () => {
    stop = startNotificationPolling(() => state.workspace, request);
  });
  expect(request).toHaveBeenCalledWith("/api/notifications/refresh", "POST");
  expect(notificationSnapshot().items[0].id).toBe("due");
  await act(async () => vi.advanceTimersByTimeAsync(20000));
  expect(request).toHaveBeenLastCalledWith("/api/notifications");
  await act(async () => vi.advanceTimersByTimeAsync(40000));
  expect(
    request.mock.calls.filter(([path]) => path.endsWith("/refresh")),
  ).toHaveLength(2);
  stop();
  const calls = request.mock.calls.length;
  await act(async () => vi.advanceTimersByTimeAsync(60000));
  expect(request).toHaveBeenCalledTimes(calls);
});
it("loads a due Inbox reminder immediately without waiting for the polling interval", async () => {
  setup();
  const request = vi
    .fn()
    .mockResolvedValueOnce([])
    .mockResolvedValue([reminder("inbox-due")]);
  let stop;
  await act(async () => {
    stop = startNotificationPolling(() => state.workspace, request);
  });
  await act(async () => {
    window.dispatchEvent(new Event("nora-reminder-due"));
  });
  expect(request).toHaveBeenCalledTimes(2);
  expect(request).toHaveBeenLastCalledWith("/api/notifications");
  expect(notificationSnapshot().items[0].id).toBe("inbox-due");
  stop();
});
it("queues a due reminder while an earlier notification request is still running", async () => {
  setup();
  let finish;
  const request = vi
    .fn()
    .mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    )
    .mockResolvedValue([reminder("queued-due")]);
  const stop = startNotificationPolling(() => state.workspace, request);
  await act(async () => {
    window.dispatchEvent(new Event("nora-reminder-due"));
    finish([]);
  });
  expect(request).toHaveBeenCalledTimes(2);
  expect(notificationSnapshot().items[0].id).toBe("queued-due");
  stop();
});
it("does not issue a fallback GET immediately after a rate-limited notification scan", async () => {
  setup();
  const request = vi
    .fn()
    .mockRejectedValue(Object.assign(new Error("Wait"), { status: 429 }));
  let stop;
  await act(async () => {
    stop = startNotificationPolling(() => state.workspace, request);
  });
  expect(request).toHaveBeenCalledTimes(1);
  expect(request).toHaveBeenCalledWith("/api/notifications/refresh", "POST");
  stop();
});
it("ignores responses that arrive after logout", async () => {
  setup();
  let resolve;
  const request = vi.fn(
    () =>
      new Promise((done) => {
        resolve = done;
      }),
  );
  const stop = startNotificationPolling(() => state.workspace, request);
  state.workspace = null;
  resetNotificationFeed();
  await act(async () => {
    resolve([reminder("private")]);
  });
  expect(notificationSnapshot().items).toHaveLength(0);
  stop();
});
