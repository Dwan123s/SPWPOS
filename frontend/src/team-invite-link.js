const key = "planora.pending-team-invite";
const validCode = (code) => /^[a-f0-9]{40}$/i.test(code || "");
let memory = null;

export function parseTeamInvite(value, base = window.location.href) {
  const input = String(value || "").trim();
  if (validCode(input)) return input.toUpperCase();
  try {
    const url = new URL(input, base),
      origin = new URL(base).origin;
    if (url.origin !== origin || !["http:", "https:"].includes(url.protocol))
      throw new Error();
    const code =
      (url.hash.startsWith("#teams?")
        ? new URLSearchParams(url.hash.split("?")[1]).get("join")
        : null) || url.searchParams.get("join");
    if (validCode(code)) return code.toUpperCase();
  } catch {
    /* Show a useful error without navigating to the supplied URL. */
  }
  throw new Error(
    "Mã hoặc link mời không hợp lệ. Hãy dùng link PLANORA được trưởng nhóm chia sẻ.",
  );
}
export function teamInviteLink(code, base = window.location.href) {
  if (!validCode(code)) return "";
  const url = new URL(base);
  url.search = "";
  url.hash = `teams?join=${code.toUpperCase()}`;
  return url.href;
}
export function pendingTeamInvite() {
  let saved = memory;
  try {
    saved = JSON.parse(sessionStorage.getItem(key) || "null") || saved;
  } catch {
    /* Optional storage. */
  }
  if (saved && Date.now() - saved.createdAt < 24 * 60 * 60 * 1000) return saved;
  return null;
}
export function clearTeamInvite() {
  memory = null;
  try {
    sessionStorage.removeItem(key);
  } catch {
    /* Optional storage. */
  }
}
export function captureTeamInvite() {
  const url = new URL(window.location.href);
  const fromHash =
    url.hash.startsWith("#teams?") &&
    new URLSearchParams(url.hash.split("?")[1]).has("join");
  if (!fromHash && !url.searchParams.has("join")) return pendingTeamInvite();
  try {
    memory = { code: parseTeamInvite(url.href), createdAt: Date.now() };
  } catch (e) {
    memory = { code: null, error: e.message, createdAt: Date.now() };
  }
  try {
    sessionStorage.setItem(key, JSON.stringify(memory));
  } catch {
    /* Keep in memory until this tab closes. */
  }
  url.searchParams.delete("join");
  url.hash = "teams";
  history.replaceState(null, "", url);
  return memory;
}
