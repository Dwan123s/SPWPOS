import { useOutsideDismiss } from "./popup-dismissal.jsx";
import {
  Fragment,
  useEffect,
  useLayoutEffect,
  useRef,
  useSyncExternalStore,
} from "react";
import { getSnapshot, subscribe, dismissToast } from "./store.js";
import { closeModal, button } from "./ui.jsx";
import {
  startController,
  onClick,
  onInput,
  onChange,
  onSubmit,
} from "./controller.jsx";
import { setupShell } from "./shell.jsx";
import { Loading } from "./loading.jsx";
import { revealModal } from "./workspace-motion.jsx";
import { prefetchPage } from "./route-pages.jsx";
import { NotificationPopups } from "./notification-popups.jsx";
function prefetchNavigation(event) {
  const link = event.target.closest?.('[data-action="nav"][data-id]');
  if (link) prefetchPage(link.dataset.id);
}
export function App({ initialize = true }) {
  const snapshot = useSyncExternalStore(subscribe, getSnapshot, getSnapshot),
    dialog = useRef(null);
  useOutsideDismiss([dialog], closeModal, !!snapshot.modal);
  useEffect(() => {
    if (!initialize) return;
    const stopController = startController(),
      stopShell = setupShell();
    return () => {
      stopController();
      stopShell();
    };
  }, [initialize]);
  useLayoutEffect(() => {
    if (snapshot.modal && !dialog.current.open) dialog.current.showModal();
    if (!snapshot.modal && dialog.current.open) dialog.current.close();
  }, [snapshot.modal]);
  useEffect(() => {
    if (!snapshot.modal) return;
    const animation = revealModal(dialog.current.querySelector(".modal-body"));
    return () => animation?.cancel();
  }, [snapshot.modalKey, !!snapshot.modal]);
  return (
    <div
      onClick={onClick}
      onSubmit={onSubmit}
      onInput={onInput}
      onChange={onChange}
      onPointerOver={prefetchNavigation}
      onFocusCapture={prefetchNavigation}
      className="min-h-dvh text-slate-900 antialiased"
    >
      <Fragment
        key={
          snapshot.screenGroup ||
          (snapshot.screenKey.startsWith("auth-")
            ? "auth"
            : snapshot.screenKey.startsWith("notifications-")
              ? "notifications"
              : snapshot.screenKey)
        }
      >
        {snapshot.screen || <Loading fullscreen />}
      </Fragment>
      <NotificationPopups />
      <div id="toasts" aria-live="polite">
        {snapshot.toasts.map((toast) => (
          <div key={toast.id} className={`toast ${toast.type}`}>
            {toast.message}
            <button
              type="button"
              aria-label="Đóng thông báo"
              onClick={() => dismissToast(toast.id)}
              style={{
                background: "none",
                border: 0,
                color: "inherit",
                cursor: "pointer",
                marginLeft: 12,
              }}
            >
              ×
            </button>
          </div>
        ))}
      </div>
      <dialog
        ref={dialog}
        id="modal"
        aria-labelledby="modal-title"
        onCancel={(event) => {
          event.preventDefault();
          closeModal();
        }}
      >
        <div id="modal-content">
          {snapshot.modal && (
            <div key={snapshot.modalKey} className="modal-body">
              <div className="modal-head">
                <div>
                  <h2 id="modal-title">{snapshot.modal.title}</h2>
                  <p>{snapshot.modal.subtitle}</p>
                </div>
                {button("", "close-modal", "icon-only ghost", "", "close")}
              </div>
              {snapshot.modal.body}
            </div>
          )}
        </div>
      </dialog>
    </div>
  );
}
