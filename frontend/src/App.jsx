import {
  Fragment,
  useEffect,
  useLayoutEffect,
  useRef,
  useSyncExternalStore,
} from "react";
import { getSnapshot, subscribe } from "./store.js";
import { closeModal, button } from "./ui.jsx";
import {
  startController,
  onClick,
  onInput,
  onChange,
  onSubmit,
} from "./controller.jsx";
import { setupShell } from "./shell.jsx";
export function App({ initialize = true }) {
  const snapshot = useSyncExternalStore(subscribe, getSnapshot, getSnapshot),
    dialog = useRef(null);
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
  return (
    <div
      onClick={onClick}
      onSubmit={onSubmit}
      onInput={onInput}
      onChange={onChange}
      className="min-h-dvh text-slate-900 antialiased"
    >
      <Fragment key={snapshot.screenKey}>
        {snapshot.screen || (
          <div className="initial-loading flex min-h-dvh flex-col items-center justify-center gap-4">
            <img src="/assets/planora-mark.png" alt="PLANORA" />
            <p>Đang mở không gian làm việc…</p>
          </div>
        )}
      </Fragment>
      <div id="toasts" aria-live="polite">
        {snapshot.toasts.map((toast) => (
          <div key={toast.id} className={`toast ${toast.type}`}>
            {toast.message}
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
        onClick={(event) => {
          if (event.target === dialog.current) closeModal();
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
