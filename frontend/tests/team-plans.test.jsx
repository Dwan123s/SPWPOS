import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { TeamPlans } from "../src/team-plans.jsx";
import { TeamJoinRequests } from "../src/team-membership.jsx";
import { TeamPremiumPage } from "../src/team-premium-page.jsx";
import { api } from "../src/api.js";

vi.mock("../src/api.js", () => ({ api: vi.fn() }));
const free = { code: "free", memberLimit: 5, amountVnd: 0 };
const premium = { code: "premium20", memberLimit: 20, amountVnd: 50000 };
const unlimited = {
  code: "premiumUnlimited",
  memberLimit: null,
  amountVnd: 110000,
};
const detail = {
  team: { id: "group", role: "Leader" },
  members: Array(5).fill({}),
  plan: free,
};
beforeEach(() => vi.resetAllMocks());

it("shows monthly pricing, the active expiry and renewal action", async () => {
  api.mockResolvedValue({
    available: true,
    plans: [free, premium, unlimited],
    plan: premium,
    validUntil: "2026-12-18T06:00:00Z",
    order: null,
  });
  render(<TeamPlans />);
  expect(
    await screen.findByRole("button", { name: "Gia hạn 1 tháng" }),
  ).toBeTruthy();
  expect(screen.getByText(/Có hiệu lực đến/).textContent).toContain("2026");
  expect(screen.getAllByText("/ tháng")).toHaveLength(2);
});

it("keeps a pending checkout available and refreshes the group only after verification", async () => {
  api.mockResolvedValueOnce({
    available: true,
    plans: [free, premium, unlimited],
    order: null,
  });
  render(<TeamPlans />);
  const choose = await screen.findByRole("button", { name: "Chọn Premium 20" });
  api.mockResolvedValueOnce({
    planCode: "premium20",
    status: "PENDING",
    checkoutUrl: "https://pay.payos.vn/web/team",
  });
  fireEvent.click(choose);
  const pay = await screen.findByRole("link", { name: "Thanh toán qua payOS" });
  expect(pay.getAttribute("href")).toBe("https://pay.payos.vn/web/team");
  expect(
    screen
      .getByRole("link", { name: "Tiếp tục thanh toán" })
      .getAttribute("href"),
  ).toBe("https://pay.payos.vn/web/team");
  expect(screen.queryByRole("button", { name: "Chọn Premium 20" })).toBeNull();
  expect(api).toHaveBeenCalledWith("/api/commerce/premium", "POST", {
    code: "premium20",
  });

  api.mockResolvedValueOnce({
    available: true,
    plans: [free, premium, unlimited],
    plan: premium,
    order: { planCode: "premium20", status: "PAID" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Kiểm tra thanh toán" }));

  expect(
    await screen.findByText(
      "Đã xác nhận thanh toán và kích hoạt Premium cho tài khoản.",
    ),
  ).toBeTruthy();
});

it("disables approval at the free capacity and enables it after upgrading", () => {
  const request = {
    userId: "applicant",
    name: "Lan",
    email: "lan@example.com",
  };
  const onReview = vi.fn();
  const view = render(
    <TeamJoinRequests
      detail={{ ...detail, joinRequests: [request] }}
      onReview={onReview}
    />,
  );
  expect(screen.getByRole("button", { name: "Duyệt Lan" }).disabled).toBe(true);
  expect(screen.getByRole("button", { name: "Từ chối Lan" }).disabled).toBe(
    false,
  );
  view.rerender(
    <TeamJoinRequests
      detail={{ ...detail, plan: premium, joinRequests: [request] }}
      onReview={onReview}
    />,
  );
  fireEvent.click(screen.getByRole("button", { name: "Duyệt Lan" }));
  expect(onReview).toHaveBeenCalledWith(request, true);
});
