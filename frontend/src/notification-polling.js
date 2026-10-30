import { api } from "./api.js";
import { syncNotifications } from "./notification-feed.js";

export function startNotificationPolling(readWorkspace, request = api) {
  let disposed = false,
    busy = false,
    lastScan = 0,
    scanUser = null;
  async function refresh() {
    const workspace = readWorkspace();
    const uid = workspace?.profile?.id;
    if (disposed || busy || document.hidden || !uid) return;
    busy = true;
    try {
      if (scanUser !== uid) {
        lastScan = 0;
        scanUser = uid;
      }
      const scan =
        workspace.features?.includes("notification-refresh") &&
        (!lastScan || Date.now() - lastScan >= 60000);
      let rows;
      if (scan) {
        lastScan = Date.now();
        try {
          rows = await request("/api/notifications/refresh", "POST");
        } catch {
          lastScan = 0;
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
    }
  }
  const visible = () => {
    if (!document.hidden) void refresh();
  };
  const timer = setInterval(() => void refresh(), 20000);
  window.addEventListener("focus", visible);
  window.addEventListener("workspace-updated", visible);
  document.addEventListener("visibilitychange", visible);
  void refresh();
  return () => {
    disposed = true;
    clearInterval(timer);
    window.removeEventListener("focus", visible);
    window.removeEventListener("workspace-updated", visible);
    document.removeEventListener("visibilitychange", visible);
  };
}
