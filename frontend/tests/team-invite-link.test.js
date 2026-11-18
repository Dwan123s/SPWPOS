import { beforeEach, expect, it } from "vitest";
import {
  captureTeamInvite,
  clearTeamInvite,
  parseTeamInvite,
  pendingTeamInvite,
  teamInviteLink,
  teamInvitationMessage,
} from "../src/team-invite-link.js";
const code = "AB".repeat(20);
beforeEach(() => {
  clearTeamInvite();
  history.replaceState(null, "", "/app/#today");
});
it("builds dedicated invitation links without leaking existing query parameters", () => {
  const link = teamInviteLink(
    code,
    "https://planora.example/app/?private=1#calendar",
  );
  expect(link).toBe(`https://planora.example/join.html?code=${code}`);
  expect(parseTeamInvite(link, "https://planora.example/app/")).toBe(code);
  expect(parseTeamInvite(` ${code.toLowerCase()} `)).toBe(code);
});
it("still accepts old links and explains the invitation in copied messages", () => {
  expect(
    parseTeamInvite(
      `https://planora.example/#teams?join=${code}`,
      "https://planora.example/",
    ),
  ).toBe(code);
  expect(
    parseTeamInvite(
      `https://planora.example/?join=${code}`,
      "https://planora.example/",
    ),
  ).toBe(code);
  expect(
    teamInvitationMessage("SWP391", code, "https://planora.example/"),
  ).toBe(
    `Mời bạn tham gia nhóm “SWP391” trên PLANORA.\n\nhttps://planora.example/join.html?code=${code}\n\nMở link để xem lời mời, đăng nhập và gửi yêu cầu tham gia nhóm.`,
  );
});
it("rejects malformed and foreign links instead of navigating elsewhere", () => {
  for (const input of [
    "bad",
    "javascript:alert(1)",
    `https://outside.example/#teams?join=${code}`,
    `#teams?join=bad`,
  ])
    expect(() => parseTeamInvite(input)).toThrow(/không hợp lệ/);
});
it("captures an invitation across login and refresh and clears it after completion or cancellation", () => {
  history.replaceState(null, "", `/app/#teams?join=${code}`);
  expect(captureTeamInvite().code).toBe(code);
  expect(location.hash).toBe("#teams");
  expect(location.search).toBe("");
  history.replaceState(null, "", "#login");
  expect(pendingTeamInvite().code).toBe(code);
  expect(
    JSON.parse(sessionStorage.getItem("planora.pending-team-invite")).code,
  ).toBe(code);
  clearTeamInvite();
  expect(pendingTeamInvite()).toBeNull();
});
it("turns an invalid link into a readable pending error without preserving untrusted content", () => {
  history.replaceState(null, "", "/app/#teams?join=malformed");
  const invite = captureTeamInvite();
  expect(invite.code).toBeNull();
  expect(invite.error).toContain("không hợp lệ");
  expect(JSON.stringify(invite)).not.toContain("malformed");
});
