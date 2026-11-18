import { beforeEach, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { initTeamInviteLanding } from "../../src/SPWPOS.Api/wwwroot/assets/team-invite-landing.js";

it("serves invitation-specific metadata to chat crawlers without running JavaScript", () => {
  const html = readFileSync("../src/SPWPOS.Api/wwwroot/join.html", "utf8");
  const page = new DOMParser().parseFromString(html, "text/html");
  expect(page.querySelector('meta[property="og:title"]').content).toBe("Bạn được mời tham gia nhóm trên PLANORA");
  expect(page.querySelector('meta[property="og:description"]').content).toContain("gửi yêu cầu tham gia nhóm");
  expect(page.querySelector('meta[property="og:image"]').content).toMatch(/\/assets\/team-invitation-share\.png$/);
  expect(page.querySelector('meta[name="referrer"]').content).toBe("no-referrer");
  expect(page.querySelector("script:not([src])")).toBeNull();
});

beforeEach(() => {
  document.body.innerHTML = `<h1 id="invite-title">Lời mời</h1><p id="invite-description"></p><ol class="invite-steps"></ol><a id="invite-open" hidden></a><p id="invite-help"></p><p id="invite-invalid" hidden></p>`;
});

it("opens the existing invitation review without automatically joining", () => {
  initTeamInviteLanding(
    document,
    `https://planora.example/join.html?code=${"ab".repeat(20)}`,
  );
  const button = document.getElementById("invite-open");
  expect(button.hidden).toBe(false);
  expect(button.getAttribute("href")).toBe(`/#teams?join=${"AB".repeat(20)}`);
  expect(document.getElementById("invite-invalid").hidden).toBe(true);
});

it("shows an understandable error for invalid codes without reflecting them", () => {
  initTeamInviteLanding(
    document,
    "https://planora.example/join.html?code=%3Cscript%3Ebad%3C/script%3E",
  );
  expect(document.getElementById("invite-open").hidden).toBe(true);
  expect(document.getElementById("invite-invalid").hidden).toBe(false);
  expect(document.getElementById("invite-title").textContent).toBe(
    "Không thể mở lời mời này",
  );
  expect(document.body.innerHTML).not.toContain("<script>");
});
