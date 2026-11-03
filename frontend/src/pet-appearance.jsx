import { useSyncExternalStore } from "react";
import { state } from "./ui.jsx";
import "./pet-appearance.css";

export function syncPetProfile(profile) {
  if (state.workspace) state.workspace.gamification = profile;
  document.documentElement.dataset.noraSkin =
    profile?.selectedSkin || "classic";
  window.dispatchEvent(new Event("pet-profile-changed"));
}
function subscribe(listener) {
  window.addEventListener("pet-profile-changed", listener);
  return () => window.removeEventListener("pet-profile-changed", listener);
}
export function usePetAppearance() {
  const value = useSyncExternalStore(subscribe, () => {
    const profile = state.workspace?.gamification;
    return `${profile?.selectedSkin || "classic"}|${profile?.userLevel || 1}`;
  });
  const [code, level] = value.split("|");
  return { code, level: Number(level) };
}
export function PetArtwork({
  code = "classic",
  level = 1,
  alt = "",
  className = "",
}) {
  const stage = level >= 10 ? 3 : level >= 5 ? 2 : 1;
  return (
    <span
      className={`pet-artwork pet-stage-${stage} skin-${code} ${className}`}
      role={alt ? "img" : undefined}
      aria-label={alt || undefined}
    >
      <img
        src={
          code === "fox"
            ? "/assets/pets/milo-fox-v1.png"
            : "/assets/start/planora-owl-v1.png"
        }
        alt=""
        decoding="async"
      />
      {stage >= 2 && (
        <svg
          className="pet-evolution-wings"
          viewBox="0 0 160 160"
          aria-hidden="true"
        >
          <path
            d="M30 96C5 94 1 65 8 54c5 21 19 25 31 31M130 96c25-2 29-31 22-42-5 21-19 25-31 31"
            fill={stage === 3 ? "#ffe5a1" : "#c7f4ff"}
            stroke={stage === 3 ? "#e8ac35" : "#66cce7"}
            strokeWidth="3"
          />
          <path
            d="m20 31 3 7 8 2-8 3-3 7-2-7-8-3 8-2zm120 5 2 5 6 2-6 2-2 6-2-6-5-2 5-2z"
            fill={stage === 3 ? "#e8ac35" : "#66cce7"}
          />
        </svg>
      )}
      {stage === 3 && (
        <svg
          className="pet-evolution-crown"
          viewBox="0 0 80 50"
          aria-hidden="true"
        >
          <path
            d="m10 15 17 12L40 5l13 22 17-12-7 29H17z"
            fill="#ffcf5a"
            stroke="#c18a22"
            strokeWidth="3"
            strokeLinejoin="round"
          />
          <path d="m40 25 6 7-6 7-6-7z" fill="#39b7df" />
        </svg>
      )}
    </span>
  );
}
export function PetProgress({ compact = false }) {
  const profile = useSyncExternalStore(
    subscribe,
    () => state.workspace?.gamification,
  );
  if (!profile) return null;
  return (
    <a
      className={`pet-progress-card${compact ? " pet-progress-header" : ""}`}
      href="#settings"
      onClick={() => {
        state.settingsTab = "pets";
      }}
      aria-label="Mở Pet và XP"
      title={`${profile.selectedSkin === "fox" ? "MILO" : "NORA"} · Cấp ${profile.userLevel} · ${profile.experiencePoints} XP · +${profile.taskRewardXp || 25} XP / task đúng hạn`}
    >
      <PetArtwork code={profile.selectedSkin} level={profile.userLevel} />
      <span>
        <span className="pet-progress-heading">
          <strong>
            {profile.selectedSkin === "fox" ? "MILO" : "NORA"} · Cấp{" "}
            {profile.userLevel}
          </strong>
          <small>
            {profile.experiencePoints} XP
            {!compact && ` · +${profile.taskRewardXp || 25} XP / task đúng hạn`}
          </small>
        </span>
        <progress
          className="pet-xp-progress"
          aria-label="XP đến cấp tiếp theo"
          aria-valuetext={`${profile.experiencePoints % 100} trên 100 XP đến cấp tiếp theo`}
          value={profile.experiencePoints % 100}
          max="100"
        />
      </span>
      {!compact && <span aria-hidden="true">→</span>}
    </a>
  );
}
