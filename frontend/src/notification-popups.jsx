import { useEffect, useSyncExternalStore } from "react";
import { icon, state } from "./ui.jsx";
import {
  dismissNotificationPopup,
  hideNotificationPopup,
  notificationSnapshot,
  subscribeNotifications,
} from "./notification-feed.js";
import "./styles/notification-popups.css";
import { requestTeamJoinReview } from "./team-notification-link.js";
import { openTeamTask, teamReviewTarget } from "./team-task-link.js";
import { startNotificationSound } from "./notification-sound.js";

export function useNotificationFeed() {
  return useSyncExternalStore(
    subscribeNotifications,
    notificationSnapshot,
    notificationSnapshot,
  );
}
function useUnreadCount() {
  const feed = useNotificationFeed();
  const rows =
    feed.userId && feed.userId === state.workspace?.profile?.id
      ? feed.items
      : state.workspace?.notifications || [];
  return rows.filter((item) => !item.isRead).length;
}
export function NotificationBell() {
  const count = useUnreadCount();
  return (
    <button
      type="button"
      className="bell"
      data-action="nav"
      data-id="notifications"
      aria-label={`Thông báo${count ? `, ${count} chưa đọc` : ""}`}
    >
      {icon("bell")}
      {count > 0 && <span className="dot" />}
    </button>
  );
}
export function NotificationUnreadCount() {
  const count = useUnreadCount();
  return count > 0 ? (
    <span className="notification-number">{count}</span>
  ) : null;
}
function TimedNotificationPopup({ id, userId, children }) {
  useEffect(() => {
    const timer = setTimeout(() => hideNotificationPopup(id, userId), 5000);
    return () => clearTimeout(timer);
  }, [id, userId]);
  return <article className="reminder-popup">{children}</article>;
}
export function NotificationPopups() {
  useEffect(() => startNotificationSound(() => state.workspace), []);
  const feed = useNotificationFeed();
  if (!feed.userId || !feed.popups.length) return null;
  return (
    <section
      className="reminder-popups"
      aria-label="Nhắc nhở mới"
      aria-live="polite"
    >
      {feed.popups
        .filter(
          (item) => !["NORA_REMINDER", "INBOX_REMINDER"].includes(item.kind),
        )
        .map((item) => {
          const taskId = item.taskId && state.workspace?.tasks?.some(task => task.id === item.taskId) ? item.taskId : null;
          const openTask = Boolean(taskId && !teamReviewTarget(item));
          return (
          <TimedNotificationPopup key={`${feed.userId}:${item.id}`} id={item.id} userId={feed.userId}>
            <span className="reminder-popup-icon" aria-hidden="true">
              {icon("bell")}
            </span>
            <div className="reminder-popup-content">
              <strong>{item.title}</strong>
              <p>{item.message}</p>
              {!(
                item.kind === "TEAM_JOIN_REQUEST" &&
                item.joinRequestPending === false
              ) && (
                <a
                  data-action={openTask ? "task-detail" : undefined}
                  data-id={openTask ? taskId : undefined}
                  data-session-id={openTask ? item.sessionId || undefined : undefined}
                  href={openTask ? "#tasks/" + taskId :
                    item.kind === "FRIEND_REQUEST"
                      ? "#friends/incoming"
                      : item.kind === "FRIEND_ACCEPTED"
                        ? "#friends/friends"
                        : item.kind === "TEAM_JOIN_REQUEST" || item.kind === "TEAM_FRIEND_INVITE" || item.kind?.startsWith("TEAM_PREMIUM_") || teamReviewTarget(item)
                          ? "#teams"
                          : item.kind === "SESSION_REVIEW"
                            ? "#today"
                            : item.kind === "NORA_REMINDER" ||
                                item.kind === "INBOX_REMINDER"
                              ? "#agent"
                              : "#notifications"
                  }
                  onClick={(event) => {
                    if (openTask) event.preventDefault();
                    if (item.kind === "TEAM_JOIN_REQUEST" || item.kind?.startsWith("TEAM_PREMIUM_"))
                      requestTeamJoinReview(item.teamId);
                    if (teamReviewTarget(item)) {
                      event.preventDefault();
                      openTeamTask(item.teamId, item.teamTaskId);
                    }
                    dismissNotificationPopup(item.id);
                  }}
                >
                  {openTask ? "Mở công việc" : item.kind === "TEAM_FRIEND_INVITE"
                    ? "Xem lời mời vào nhóm"
                    : item.kind === "TEAM_JOIN_REQUEST"
                    ? "Mở nhóm để duyệt"
                    : teamReviewTarget(item)
                      ? "Mở công việc cần duyệt"
                    : item.kind === "SESSION_REVIEW"
                      ? "Phản hồi NORA"
                      : item.kind === "NORA_REMINDER" ||
                          item.kind === "INBOX_REMINDER"
                        ? "Mở NORA"
                        : "Xem thông báo"}{" "}
                  {icon("arrow")}
                </a>
              )}
            </div>
            <button
              type="button"
              className="reminder-popup-close"
              aria-label={`Đóng nhắc nhở: ${item.title}`}
              onClick={() => dismissNotificationPopup(item.id)}
            >
              {icon("close")}
            </button>
          </TimedNotificationPopup>
          );
        })}
    </section>
  );
}
