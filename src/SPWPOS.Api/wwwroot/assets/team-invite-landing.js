export function initTeamInviteLanding(doc = document, href = window.location.href) {
  const button = doc.getElementById("invite-open");
  if (!button) return;
  const code = new URL(href).searchParams.get("code") || "";
  if (/^[a-f0-9]{40}$/i.test(code)) {
    button.setAttribute("href", `/#teams?join=${code.toUpperCase()}`);
    button.hidden = false;
  } else {
    doc.getElementById("invite-title").textContent = "Không thể mở lời mời này";
    doc.getElementById("invite-description").textContent = "Link chưa có mã mời hợp lệ. Bạn cần link đầy đủ được trưởng nhóm chia sẻ.";
    doc.getElementById("invite-invalid").hidden = false;
    doc.getElementById("invite-help").hidden = true;
    doc.querySelector(".invite-steps").hidden = true;
    button.hidden = true;
  }
}

initTeamInviteLanding();
