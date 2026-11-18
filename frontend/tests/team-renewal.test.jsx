import { expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { TeamRenewalNotice } from "../src/team-renewal-notice.jsx";

const detail = {
  team: { id: "team", name: "Nhóm thiết kế", role: "Leader" },
  renewal: {
    status: "GracePeriod",
    expiredAt: "2026-11-18T05:00:00Z",
    graceUntil: "2026-11-21T05:00:00Z",
  },
};

it("opens the renewal popup and keeps its banner after acknowledgement", () => {
  render(<TeamRenewalNotice detail={detail} />);
  expect(
    screen.getByRole("dialog", { name: "Gia hạn Premium trong 3 ngày" }).open,
  ).toBe(true);
  expect(
    screen
      .getAllByRole("link", { name: "Gia hạn Premium" })[0]
      .getAttribute("href"),
  ).toBe("#premium");
  fireEvent.click(screen.getByRole("button", { name: "Đã hiểu" }));
  expect(screen.queryByRole("dialog")).toBeNull();
  expect(screen.getByRole("status").textContent).toContain("21");
});

it("explains suspension to a member and directs them to the owner", () => {
  render(
    <TeamRenewalNotice
      detail={{
        ...detail,
        team: { ...detail.team, role: "Member" },
        renewal: { ...detail.renewal, status: "Suspended" },
      }}
    />,
  );
  expect(
    screen.getByRole("dialog", { name: "Nhóm đã bị vô hiệu hóa" }),
  ).toBeTruthy();
  expect(screen.queryByRole("link", { name: "Gia hạn Premium" })).toBeNull();
  expect(
    screen.getAllByText("Hãy liên hệ chủ nhóm để gia hạn tài khoản Premium."),
  ).toHaveLength(2);
});
