import { state } from "./ui.jsx";
import { NotificationCenter } from "./notifications.jsx";
export function notificationsPage() {
  return <NotificationCenter workspace={state.workspace} />;
}
