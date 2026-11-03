const listeners = new Set();
const preferences = new Map();
const key = (uid) => `planora.notification-sound.${uid}`;
let context = null;

export function notificationSoundEnabled(uid) {
  if (!uid) return false;
  if (!preferences.has(uid)) {
    let enabled = true;
    try {
      enabled = localStorage.getItem(key(uid)) !== "false";
    } catch {
      /* Optional storage. */
    }
    preferences.set(uid, enabled);
  }
  return preferences.get(uid);
}
export function setNotificationSoundEnabled(uid, enabled) {
  if (!uid) return;
  preferences.set(uid, enabled);
  try {
    localStorage.setItem(key(uid), String(enabled));
  } catch {
    /* Keep the session preference. */
  }
  listeners.forEach((listener) => listener());
}
export function subscribeNotificationSound(listener) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}
function unlockSound() {
  try {
    const AudioContext = window.AudioContext || window.webkitAudioContext;
    if (!AudioContext) return;
    if (!context || context.state === "closed") context = new AudioContext();
    if (context.state === "suspended") void context.resume().catch(() => {});
  } catch {
    /* Browser audio may be unavailable or blocked. */
  }
}
export function startNotificationSound(readWorkspace) {
  const unlock = () => {
    const workspace = readWorkspace();
    if (
      workspace?.notificationSettings?.inAppEnabled !== false &&
      notificationSoundEnabled(workspace?.profile?.id)
    )
      unlockSound();
  };
  document.addEventListener("pointerdown", unlock, true);
  document.addEventListener("keydown", unlock, true);
  return () => {
    document.removeEventListener("pointerdown", unlock, true);
    document.removeEventListener("keydown", unlock, true);
    const previous = context;
    context = null;
    try {
      void previous?.close().catch(() => {});
    } catch {
      /* Already closed. */
    }
  };
}
export function playNotificationSound(uid) {
  if (!notificationSoundEnabled(uid) || context?.state !== "running") return;
  try {
    const now = context.currentTime;
    // A short, quiet two-note chime generated locally, without media downloads.
    [659.25, 880].forEach((frequency, index) => {
      const oscillator = context.createOscillator();
      const gain = context.createGain();
      const start = now + index * 0.14;
      oscillator.type = "sine";
      oscillator.frequency.setValueAtTime(frequency, start);
      gain.gain.setValueAtTime(0, start);
      gain.gain.linearRampToValueAtTime(0.12, start + 0.015);
      gain.gain.exponentialRampToValueAtTime(0.001, start + 0.22);
      oscillator.connect(gain);
      gain.connect(context.destination);
      oscillator.onended = () => {
        oscillator.disconnect();
        gain.disconnect();
      };
      oscillator.start(start);
      oscillator.stop(start + 0.24);
    });
  } catch {
    /* A sound failure must never interrupt notifications. */
  }
}
