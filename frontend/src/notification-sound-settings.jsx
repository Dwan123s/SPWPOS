import { useSyncExternalStore } from "react";
import {
  notificationSoundEnabled,
  setNotificationSoundEnabled,
  subscribeNotificationSound,
  playNotificationSound,
} from "./notification-sound.js";

export function NotificationSoundSettings({ userId, disabled = false }) {
  const enabled = useSyncExternalStore(
    subscribeNotificationSound,
    () => notificationSoundEnabled(userId),
    () => true,
  );
  return (
    <div className="notice-sound-settings">
      <label>
        <input
          type="checkbox"
          checked={enabled}
          disabled={!userId || disabled}
          onChange={(event) =>
            setNotificationSoundEnabled(userId, event.target.checked)
          }
        />
        Âm thanh thông báo
      </label>
      <p>
        Áp dụng trên trình duyệt này. Âm báo hoạt động sau khi bạn tương tác với
        trang.
      </p>
      <button
        type="button"
        className="btn secondary small"
        disabled={!userId || disabled || !enabled}
        onClick={() => playNotificationSound(userId)}
      >
        Nghe thử âm báo
      </button>
    </div>
  );
}
