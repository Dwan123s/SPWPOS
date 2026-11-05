import { api } from "./api.js";
import { syncNotifications } from "./notification-feed.js";

export function startNotificationPolling(readWorkspace, request = api) {
  let disposed = false,
    busy = false,
    dueQueued = false,
    lastScan = 0,
    scanUser = null;
  async function refresh(reminderDue = false) {
    const workspace = readWorkspace();
    const uid = workspace?.profile?.id;
    if (disposed || (!reminderDue && document.hidden) || !uid) return;
    if (busy) {
      if (reminderDue) dueQueued = true;
      return;
    }
    busy = true;
    try {
      if (scanUser !== uid) {
        lastScan = 0;
        scanUser = uid;
      }
      const scan =
        !reminderDue &&
        workspace.features?.includes("notification-refresh") &&
        (!lastScan || Date.now() - lastScan >= 60000);
      let rows;
      if (scan) {
        lastScan = Date.now();
        try {
          rows = await request("/api/notifications/refresh", "POST");
        } catch (error) {
          lastScan = 0;
          if (error.status === 429) throw error;
          rows = await request("/api/notifications");
        }
      } else rows = await request("/api/notifications");
      const current = readWorkspace();
      if (current && current !== workspace && current.profile?.id === uid)
        return;
      if (!disposed && current?.profile?.id === uid && Array.isArray(rows)) {
        current.notifications = rows;
        syncNotifications(current);
      }
    } catch {
      /* Keep the inbox and retry after temporary network failures. */
    } finally {
      busy = false;
      if (dueQueued && !disposed) {
        dueQueued = false;
        void refresh(true);
      }
    }
  }
  const visible = () => {
    if (!document.hidden) void refresh();
  };
  const timer = setInterval(() => void refresh(), 20000);
  const reminderDue = () => void refresh(true);
  window.addEventListener("nora-reminder-due", reminderDue);
  window.addEventListener("focus", visible);
  window.addEventListener("workspace-updated", visible);
  document.addEventListener("visibilitychange", visible);
  void refresh();
  return () => {
    disposed = true;
    clearInterval(timer);
    window.removeEventListener("nora-reminder-due", reminderDue);
    window.removeEventListener("focus", visible);
    window.removeEventListener("workspace-updated", visible);
    document.removeEventListener("visibilitychange", visible);
  };
}
