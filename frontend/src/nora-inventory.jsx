import { useEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { state } from "./ui.jsx";
import { PetArtwork, syncPetProfile } from "./pet-appearance.jsx";
import { PetPaymentDialog } from "./pet-payment-dialog.jsx";
import { safePayOsCheckout } from "./payos-checkout.js";
import "./styles/workspace-upgrade.css";

const stageNames = [
  "Khởi đầu",
  "Đôi cánh trưởng thành",
  "Vương miện thành tựu",
];
export function NoraInventory() {
  const [profile, setProfile] = useState(state.workspace?.gamification);
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const [available, setAvailable] = useState(false),
    [order, setOrder] = useState(null);
  const [checking, setChecking] = useState(false);
  const [paymentOpen, setPaymentOpen] = useState(false);
  const requestId = useRef(null),
    alive = useRef(false);
  function applyProfile(updated) {
    syncPetProfile(updated);
    setProfile(updated);
  }
  useEffect(() => {
    alive.current = true;
    Promise.allSettled([
      api("/api/commerce/payments/capabilities"),
      api("/api/commerce/payments/latest"),
      api("/api/workspace/gamification/profile"),
    ]).then(async ([caps, latest, fresh]) => {
      if (!alive.current) return;
      if (caps.status === "fulfilled")
        setAvailable(caps.value?.available === true);
      if (latest.status === "fulfilled")
        setOrder(latest.value?.id ? latest.value : null);
      if (latest.status === "fulfilled" && latest.value?.status === "PAID") {
        // The latest-order request can reconcile a missed webhook. The parallel
        // profile request may predate that grant, so reload it afterwards.
        try {
          const updated = await api("/api/workspace/gamification/profile");
          if (alive.current) applyProfile(updated);
        } catch (failure) {
          if (alive.current) setError(failure.message);
        }
      } else if (fresh.status === "fulfilled" && fresh.value?.inventory)
        applyProfile(fresh.value);
      if (latest.status === "rejected")
        setError(
          latest.reason?.message ||
            "Chưa kiểm tra được thanh toán. Hãy thử lại.",
        );
    });
    return () => {
      alive.current = false;
    };
  }, []);
  async function checkPayment() {
    if (!order?.id || checking) return;
    setChecking(true);
    setError("");
    try {
      const updated = await api(`/api/commerce/payments/${order.id}`);
      if (!alive.current) return;
      setOrder(updated);
      if (updated.status === "PAID") {
        const fresh = await api("/api/workspace/gamification/profile");
        if (alive.current) applyProfile(fresh);
      }
    } catch (failure) {
      if (alive.current) setError(failure.message);
    } finally {
      if (alive.current) setChecking(false);
    }
  }
  useEffect(() => {
    if (!order?.id || !["CREATING", "PENDING"].includes(order.status)) return;
    const timer = setInterval(() => {
      if (!document.hidden) void checkPayment();
    }, 7000);
    const focus = () => {
      void checkPayment();
    };
    window.addEventListener("focus", focus);
    return () => {
      clearInterval(timer);
      window.removeEventListener("focus", focus);
    };
  }, [order?.id, order?.status, checking]);
  if (!profile) return null;
  async function equip(code) {
    setBusy(true);
    setError("");
    try {
      await api("/api/workspace/gamification/skin", "POST", { code });
      const updated = await api("/api/workspace/gamification/profile");
      if (alive.current) applyProfile(updated);
    } catch (failure) {
      if (alive.current) setError(failure.message);
    } finally {
      if (alive.current) setBusy(false);
    }
  }
  async function buy(code) {
    setBusy(true);
    setError("");
    if (order?.status === "EXPIRED") requestId.current = null;
    requestId.current ||= crypto.randomUUID();
    try {
      const purchase = await api("/api/commerce/payments", "POST", {
        code,
        requestId: requestId.current,
      });
      if (alive.current) {
        setOrder(purchase);
        if (safePayOsCheckout(purchase.checkoutUrl)) setPaymentOpen(true);
      }
    } catch (failure) {
      if (alive.current) setError(failure.message);
    } finally {
      if (alive.current) setBusy(false);
    }
  }
  const stage = profile.userLevel >= 10 ? 3 : profile.userLevel >= 5 ? 2 : 1;
  const active = order && ["CREATING", "PENDING"].includes(order.status);
  const safeCheckout = safePayOsCheckout(order?.checkoutUrl);
  return (
    <section className="card nora-inventory" aria-label="Tủ đồ NORA">
      <div className="pet-profile-hero">
        <PetArtwork
          code={profile.selectedSkin}
          level={profile.userLevel}
          alt={
            profile.selectedSkin === "fox"
              ? "Pet MILO hiện tại"
              : "Pet NORA hiện tại"
          }
        />
        <div>
          <span className="pet-eyebrow">BẠN ĐỒNG HÀNH CỦA BẠN</span>
          <h2>
            {profile.selectedSkin === "fox" ? "MILO" : "NORA"} · Cấp{" "}
            {profile.userLevel}
          </h2>
          <p>{stageNames[stage - 1]}</p>
          <strong>{profile.experiencePoints} XP</strong>
          <progress
            className="pet-xp-progress"
            aria-label="Tiến độ cấp độ"
            aria-valuetext={`${profile.experiencePoints % 100} trên 100 XP đến cấp tiếp theo`}
            value={profile.experiencePoints % 100}
            max="100"
          />
          <small>
            Còn {100 - (profile.experiencePoints % 100)} XP đến cấp{" "}
            {profile.userLevel + 1}
          </small>
        </div>
      </div>
      <p>
        Hoàn thành task đúng hạn:{" "}
        <strong>+{profile.taskRewardXp || 25} XP</strong>, một lần cho mỗi task.
        Mỗi 100 XP lên một cấp. Task nhóm nhận thưởng sau khi trưởng nhóm duyệt.
      </p>
      <p className="pet-rule-note">
        Phiên làm việc đúng hạn vẫn có XP thưởng riêng. NORA mặc định miễn phí;
        mua skin chỉ đổi ngoại hình, không tăng XP.
      </p>
      <h3>Hành trình tiến hóa</h3>
      <div className="pet-evolution-grid">
        {[1, 5, 10].map((level, index) => (
          <article
            key={level}
            className={profile.userLevel >= level ? "is-unlocked" : ""}
          >
            <PetArtwork
              code={profile.selectedSkin}
              level={level}
              alt={`Hình dáng pet ở cấp ${level}`}
            />
            <strong>Cấp {level}</strong>
            <span>{stageNames[index]}</span>
            <small>
              {level === 1 ? "Mặc định" : `${(level - 1) * 100} XP`}
            </small>
          </article>
        ))}
      </div>
      <h3>Tủ đồ và cửa hàng</h3>
      <div className="nora-skin-grid">
        {profile.inventory.map((skin) => (
          <article key={skin.code}>
            <PetArtwork
              className="inventory-owl"
              code={skin.code}
              level={profile.userLevel}
              alt={skin.name}
            />
            <strong>{skin.name}</strong>
            <small>
              {skin.priceVnd > 0
                ? `${skin.priceVnd.toLocaleString("vi-VN")}đ · mua một lần`
                : skin.requiredXp
                  ? `${skin.requiredXp} XP`
                  : "Miễn phí · mặc định"}
            </small>
            {skin.priceVnd > 0 && !skin.unlocked ? (
              <button
                type="button"
                className="btn primary small"
                disabled={busy || (!active && !available)}
                onClick={() =>
                  active && safeCheckout ? setPaymentOpen(true) : buy(skin.code)
                }
              >
                {active ? "Tiếp tục thanh toán" : "Mua skin · 10.000đ"}
              </button>
            ) : (
              <button
                type="button"
                className="btn secondary small"
                disabled={
                  busy || !skin.unlocked || profile.selectedSkin === skin.code
                }
                onClick={() => equip(skin.code)}
              >
                {profile.selectedSkin === skin.code
                  ? "Đang dùng"
                  : skin.unlocked
                    ? "Sử dụng"
                    : "Chưa mở khóa"}
              </button>
            )}
          </article>
        ))}
      </div>
      {!available && (
        <p className="pet-payment-notice">
          Cửa hàng chưa kết nối payOS. Bạn vẫn có thể nhận XP và tiến hóa pet
          miễn phí.
        </p>
      )}
      {order && (
        <section className="pet-checkout" aria-label="Đơn mua skin MILO">
          <h3>MILO · {order.amountVnd?.toLocaleString("vi-VN")}đ</h3>
          <p role="status">
            {order.status === "PAID"
              ? "Đã thanh toán · MILO đã vào tủ đồ. Chọn Sử dụng để đổi pet."
              : order.status === "EXPIRED"
                ? "Liên kết đã hết hạn. Bạn có thể tạo đơn mới."
                : "Đang chờ payOS xác nhận thanh toán. Skin sẽ mở khóa sau khi xác nhận nhận tiền."}
          </p>
          {order.status !== "PAID" && (
            <div className="pet-checkout-actions">
              {active && safeCheckout && (
                <button
                  type="button"
                  className="btn primary"
                  onClick={() => setPaymentOpen(true)}
                >
                  Thanh toán trong ứng dụng
                </button>
              )}
              {active && !safeCheckout && (
                <button
                  type="button"
                  className="btn secondary"
                  disabled={busy}
                  onClick={() => buy("fox")}
                >
                  Thử lại liên kết
                </button>
              )}
              <button
                type="button"
                className="btn secondary"
                disabled={checking}
                onClick={checkPayment}
              >
                {checking ? "Đang kiểm tra…" : "Kiểm tra thanh toán"}
              </button>
            </div>
          )}
        </section>
      )}
      {error && <p role="alert">{error}</p>}
      {paymentOpen && order && (
        <PetPaymentDialog
          order={order}
          onClose={() => setPaymentOpen(false)}
          onCheck={checkPayment}
          checking={checking}
          error={error}
        />
      )}
    </section>
  );
}
