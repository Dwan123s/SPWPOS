const listeners = new Set();
let snapshot = { userId: null, enabled: false, items: [], popups: [] };
let announced = new Set();
let dismissed = new Set();
const key = (uid) => `planora.notification-popups.dismissed.${uid}`;
export const subscribeNotifications = (listener) => {
  listeners.add(listener);
  return () => listeners.delete(listener);
};
export const notificationSnapshot = () => snapshot;
function publish(next) {
  snapshot = next;
  listeners.forEach((listener) => listener());
}
export function resetNotificationFeed() {
  announced = new Set();
  dismissed = new Set();
  publish({ userId: null, enabled: false, items: [], popups: [] });
}
export function syncNotifications(workspace) {
  const userId = workspace?.profile?.id;
  if (!userId) {
    resetNotificationFeed();
    return;
  }
  const initial = snapshot.userId !== userId;
  if (initial) {
    announced = new Set();
    try {
      const saved = JSON.parse(localStorage.getItem(key(userId)) || "[]");
      dismissed = new Set(
        Array.isArray(saved)
          ? saved.filter((id) => typeof id === "string").slice(-250)
          : [],
      );
    } catch {
      dismissed = new Set();
    }
  }
  const items = Array.isArray(workspace.notifications)
    ? workspace.notifications
    : [];
  const unread = items.filter((item) => !item.isRead);
  const unreadIds = new Set(unread.map((item) => item.id));
  const summaryId = `summary:${userId}`;
  const fresh = unread
    .filter((item) => !announced.has(item.id) && !dismissed.has(item.id))
    .sort(
      (a, b) => (Date.parse(b.createdAt) || 0) - (Date.parse(a.createdAt) || 0),
    );
  const previous = initial
    ? []
    : snapshot.popups
        .filter(
          (item) =>
            item.preview ||
            (item.summary ? unread.length > 0 : unreadIds.has(item.id)),
        )
        .map((item) =>
          item.summary
            ? { ...item, title: `Bạn có ${unread.length} thông báo chưa đọc` }
            : item,
        );
  let popups = [];
  if (workspace.notificationSettings?.inAppEnabled !== false) {
    // Summarize the existing inbox once on sign-in. New arrivals get their own
    // title and message, without flooding the screen with old reminders.
    const incoming = initial
      ? unread.length && !dismissed.has(summaryId)
        ? [
            {
              id: summaryId,
              summary: true,
              title: `Bạn có ${unread.length} thông báo chưa đọc`,
              message:
                "Mở hộp thông báo để xem các lời nhắc và cập nhật lịch của bạn.",
            },
          ]
        : []
      : fresh;
    popups = [...incoming, ...previous].slice(0, 3);
    unread.forEach((item) => announced.add(item.id));
  }
  publish({
    userId,
    enabled: workspace.notificationSettings?.inAppEnabled !== false,
    items,
    popups,
  });
}
export function dismissNotificationPopup(id) {
  dismissed.add(id);
  try {
    localStorage.setItem(
      key(snapshot.userId),
      JSON.stringify([...dismissed].slice(-250)),
    );
  } catch {
    /* Optional browser storage. */
  }
  publish({
    ...snapshot,
    popups: snapshot.popups.filter((item) => item.id !== id),
  });
}
export function previewNotificationPopup() {
  if (!snapshot.userId || !snapshot.enabled) return;
  const item = {
    id: "popup-preview",
    preview: true,
    title: "Thông báo thử",
    message:
      "Popup đang hoạt động. Lời nhắc công việc và lịch hẹn sẽ xuất hiện tại đây.",
  };
  publish({
    ...snapshot,
    popups: [item, ...snapshot.popups.filter((row) => !row.preview)].slice(
      0,
      3,
    ),
  });
}
