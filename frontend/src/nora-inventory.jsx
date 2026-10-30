import { useState } from "react";
import { api } from "./api.js";
import { state } from "./ui.jsx";
import "./styles/workspace-upgrade.css";

export function NoraInventory() {
  const [profile, setProfile] = useState(state.workspace?.gamification);
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  if (!profile) return null;
  async function equip(code) {
    setBusy(true);
    setError("");
    try {
      await api("/api/workspace/gamification/skin", "POST", { code });
      const updated = await api("/api/workspace/gamification/profile");
      state.workspace.gamification = updated;
      setProfile(updated);
      document.documentElement.dataset.noraSkin = updated.selectedSkin;
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }
  const withinLevel = profile.experiencePoints % 100;
  return (
    <section className="card nora-inventory" aria-label="Tủ đồ NORA">
      <h2>NORA đồng hành · Cấp {profile.userLevel}</h2>
      <p>
        {profile.experiencePoints} XP · Hoàn thành phiên đúng hạn để nhận XP,
        không cần thanh toán.
      </p>
      <progress aria-label="Tiến độ cấp độ" value={withinLevel} max="100" />
      <div className="nora-skin-grid">
        {profile.inventory.map((skin) => (
          <article key={skin.code}>
            <span className={`inventory-owl skin-${skin.code}`}>
              <img src="/assets/start/planora-owl-v1.png" alt={skin.name} />
            </span>
            <strong>{skin.name}</strong>
            <small>{skin.requiredXp} XP</small>
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
          </article>
        ))}
      </div>
      {error && <p role="alert">{error}</p>}
    </section>
  );
}
