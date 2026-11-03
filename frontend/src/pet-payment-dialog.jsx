import { useId, useLayoutEffect, useRef, useState } from "react";
import { PetArtwork } from "./pet-appearance.jsx";
import {
  loadPayOsCheckout,
  safePayOsCheckout,
  payOsReturnParameter,
} from "./payos-checkout.js";
import "./pet-payment-dialog.css";

export function PetPaymentDialog({ order, onClose, onCheck, checking, error }) {
  const dialog = useRef(null);
  const callbacks = useRef({ onClose, onCheck });
  callbacks.current = { onClose, onCheck };
  const id = `pet-payment-${useId().replace(/:/g, "")}`;
  const [attempt, setAttempt] = useState(0);
  const [phase, setPhase] = useState("loading");
  const [loadError, setLoadError] = useState("");
  const checkoutUrl = safePayOsCheckout(order.checkoutUrl);
  const returnParameter = payOsReturnParameter(order.returnUrl);
  const pending = ["CREATING", "PENDING"].includes(order.status);

  useLayoutEffect(() => {
    const opener = document.activeElement;
    const previousOverflow = document.body.style.overflow;
    const node = dialog.current;
    node.showModal();
    document.body.style.overflow = "hidden";
    return () => {
      node.close();
      document.body.style.overflow = previousOverflow;
      if (opener?.isConnected) opener.focus();
    };
  }, []);

  useLayoutEffect(() => {
    if (!pending) return;
    let disposed = false;
    let checkout;
    setPhase("loading");
    setLoadError("");
    if (!checkoutUrl) {
      setLoadError(
        "Liên kết thanh toán chưa sẵn sàng. Đóng popup và thử lại liên kết.",
      );
      return;
    }
    if (!returnParameter) {
      setLoadError(
        "Chưa nhận được URL thanh toán hợp lệ từ hệ thống. Đóng popup và tải lại trang để thử lại.",
      );
      return;
    }
    const receive = () => {
      if (disposed) return;
      setPhase("confirming");
      // SDK messages only request a refresh. The server must verify provider
      // evidence before granting a skin; the iframe never changes ownership.
      void callbacks.current.onCheck();
    };
    loadPayOsCheckout()
      .then((sdk) => {
        if (disposed) return;
        checkout = sdk.usePayOS({
          // Must match the returnUrl used by the backend when creating the
          // payment, even when this screen is opened on localhost. Server
          // polling confirms payment independently of cross-origin callbacks.
          RETURN_URL: returnParameter,
          ELEMENT_ID: id,
          CHECKOUT_URL: checkoutUrl,
          embedded: true,
          onSuccess: receive,
          onCancel: () => {
            if (disposed) return;
            setPhase("cancelled");
            void callbacks.current.onCheck();
          },
          onExit: () => {
            if (disposed) return;
            setPhase("closed");
            void callbacks.current.onCheck();
          },
        });
        checkout.open();
        const frame = document.getElementById(id)?.querySelector("iframe");
        if (frame) frame.title = "Thanh toán MILO qua payOS";
        setPhase("ready");
      })
      .catch(() => {
        if (!disposed)
          setLoadError(
            "Chưa tải được thanh toán payOS. Kiểm tra kết nối và thử lại.",
          );
      });
    return () => {
      disposed = true;
      // The SDK removes its message listener when it removes the iframe.
      if (document.getElementById(id)?.querySelector("iframe"))
        checkout?.exit();
    };
  }, [checkoutUrl, returnParameter, id, pending, attempt]);

  return (
    <dialog
      ref={dialog}
      className="pet-payment-dialog"
      aria-labelledby={`${id}-title`}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClose={onClose}
    >
      <header className="pet-payment-header">
        <div>
          <span className="pet-eyebrow">CỬA HÀNG PLANORA</span>
          <h2 id={`${id}-title`}>Thanh toán skin MILO</h2>
        </div>
        <button
          type="button"
          className="pet-payment-close"
          onClick={onClose}
          aria-label="Đóng thanh toán"
          autoFocus
        >
          ×
        </button>
      </header>
      <div className="pet-payment-body">
        <div className="pet-payment-summary">
          <PetArtwork code="fox" level={1} alt="Skin cáo MILO" />
          <div>
            <strong>MILO · Bạn đồng hành mới</strong>
            <p>Mua một lần · Sử dụng lâu dài</p>
          </div>
          <strong className="pet-payment-price">
            {order.amountVnd?.toLocaleString("vi-VN")}đ
          </strong>
        </div>
        <div
          id={id}
          className="pet-payment-frame"
          aria-label="Cổng thanh toán payOS"
          hidden={
            !pending || !!loadError || !["loading", "ready"].includes(phase)
          }
        />
        {pending ? (
          <>
            {loadError ? (
              <div className="pet-payment-message">
                <p role="alert">{loadError}</p>
                <button
                  type="button"
                  className="btn secondary"
                  onClick={() => setAttempt((value) => value + 1)}
                >
                  Tải lại thanh toán
                </button>
              </div>
            ) : (
              <>
                {phase === "loading" && (
                  <p role="status" className="pet-payment-message">
                    Đang tải thanh toán an toàn…
                  </p>
                )}
                {!["loading", "ready"].includes(phase) && (
                  <div className="pet-payment-message" role="status">
                    <p>
                      {phase === "confirming"
                        ? "Đang chờ xác nhận giao dịch. MILO sẽ tự mở khóa khi hệ thống nhận được thanh toán."
                        : phase === "cancelled"
                          ? "Bạn đã dừng thanh toán. Skin chưa được mở khóa."
                          : "Cổng thanh toán đã đóng. Bạn có thể mở lại để tiếp tục."}
                    </p>
                    {phase !== "confirming" && (
                      <button
                        type="button"
                        className="btn secondary"
                        onClick={() => setAttempt((value) => value + 1)}
                      >
                        Mở lại thanh toán
                      </button>
                    )}
                  </div>
                )}
              </>
            )}
            <p className="pet-payment-note">
              Quét mã QR bằng ứng dụng ngân hàng. Bạn có thể đóng popup và mở
              lại đơn đang chờ trong tủ đồ.
            </p>
            {error && <p role="alert">{error}</p>}
          </>
        ) : (
          <div className="pet-payment-result" role="status">
            <span aria-hidden="true">
              {order.status === "PAID" ? "✓" : "⌛"}
            </span>
            <h3>
              {order.status === "PAID"
                ? "Thanh toán thành công!"
                : "Đơn thanh toán đã hết hạn"}
            </h3>
            <p>
              {order.status === "PAID"
                ? "Bạn đã sở hữu MILO. Đóng popup và chọn Sử dụng trong tủ đồ."
                : "Đóng popup để tạo đơn thanh toán mới."}
            </p>
          </div>
        )}
      </div>
      <footer className="pet-payment-footer">
        <span>Thanh toán qua payOS</span>
        {pending ? (
          <button
            type="button"
            className="btn secondary"
            disabled={checking}
            onClick={onCheck}
          >
            {checking ? "Đang kiểm tra…" : "Tôi đã thanh toán"}
          </button>
        ) : (
          <button type="button" className="btn primary" onClick={onClose}>
            Về tủ đồ
          </button>
        )}
      </footer>
    </dialog>
  );
}
