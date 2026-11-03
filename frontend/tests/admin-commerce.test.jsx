import { beforeEach, it, expect, vi } from "vitest";
import {
  render,
  screen,
  fireEvent,
  waitFor,
  within,
} from "@testing-library/react";
import { AdminCommerce, revenuePreset } from "../src/admin-commerce.jsx";
import { revenueReport, purchaseHistory } from "./commerce-fixtures.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";
beforeEach(() => {
  vi.resetAllMocks();
  api.mockImplementation(async (path) =>
    path.includes("/revenue?") ? revenueReport : purchaseHistory,
  );
});
it("shows verified revenue, accessible chart and purchase details using only GET requests", async () => {
  render(<AdminCommerce />);
  await screen.findByText("Nguyễn Minh Anh");
  expect(screen.getByText("Doanh thu hôm nay")).toBeTruthy();
  expect(screen.getByLabelText(/03\/11\/2026:.*2 đơn/)).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Xem đơn 123456789" }));
  const dialog = screen.getByRole("dialog");
  expect(within(dialog).getByText("PAYOS-VERIFIED-123")).toBeTruthy();
  expect(within(dialog).getByText("Ngày xác nhận thanh toán")).toBeTruthy();
  fireEvent.click(
    within(dialog).getByRole("button", { name: "Đóng chi tiết đơn hàng" }),
  );
  expect(screen.queryByRole("dialog")).toBeNull();
  expect(api.mock.calls.every((call) => call.length === 1)).toBe(true);
});
it("applies shared date filters and changes chart grouping without changing history filters", async () => {
  render(<AdminCommerce />);
  await screen.findByText("Nguyễn Minh Anh");
  fireEvent.change(screen.getByLabelText("Từ ngày"), {
    target: { value: "2026-10-01" },
  });
  fireEvent.change(screen.getByLabelText("Đến ngày"), {
    target: { value: "2026-10-31" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Áp dụng" }));
  await waitFor(() =>
    expect(
      api.mock.calls.some(([p]) =>
        p.includes("/purchases?from=2026-10-01&to=2026-10-31"),
      ),
    ).toBe(true),
  );
  const count = api.mock.calls.filter(([p]) =>
    p.includes("/purchases?"),
  ).length;
  fireEvent.change(screen.getByLabelText("Biểu đồ theo"), {
    target: { value: "week" },
  });
  await waitFor(() =>
    expect(api.mock.calls.some(([p]) => p.includes("groupBy=week"))).toBe(true),
  );
  expect(api.mock.calls.filter(([p]) => p.includes("/purchases?")).length).toBe(
    count,
  );
});
it("filters status, searches and paginates, then resets the page for a new filter", async () => {
  api.mockImplementation(async (path) =>
    path.includes("/revenue?")
      ? revenueReport
      : {
          ...purchaseHistory,
          total: 41,
          page: Number(new URLSearchParams(path.split("?")[1]).get("page")),
        },
  );
  render(<AdminCommerce />);
  await screen.findByText("Nguyễn Minh Anh");
  fireEvent.click(screen.getByRole("button", { name: "Trang sau" }));
  await screen.findByText("Trang 2 / 3");
  fireEvent.change(screen.getByLabelText("Trạng thái"), {
    target: { value: "EXPIRED" },
  });
  await waitFor(() =>
    expect(
      api.mock.calls.some(
        ([p]) => p.includes("status=EXPIRED") && p.includes("page=1"),
      ),
    ).toBe(true),
  );
  fireEvent.change(screen.getByLabelText("Tìm đơn hàng"), {
    target: { value: "member@example.com" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Tìm kiếm đơn" }));
  await waitFor(() =>
    expect(
      api.mock.calls.some(([p]) => p.includes("search=member%40example.com")),
    ).toBe(true),
  );
});
it("rejects reversed or excessive ranges without requesting another report", async () => {
  render(<AdminCommerce />);
  await screen.findByText("Nguyễn Minh Anh");
  const calls = api.mock.calls.length;
  fireEvent.change(screen.getByLabelText("Từ ngày"), {
    target: { value: "2026-11-10" },
  });
  fireEvent.change(screen.getByLabelText("Đến ngày"), {
    target: { value: "2026-11-01" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Áp dụng" }));
  expect(screen.getByRole("alert").textContent).toContain("tối đa 366 ngày");
  expect(api.mock.calls.length).toBe(calls);
});
it("shows zero state and retries failed requests", async () => {
  api.mockRejectedValue(new Error("Không tải được báo cáo"));
  render(<AdminCommerce />);
  await screen.findAllByRole("alert");
  api.mockImplementation(async (path) =>
    path.includes("/revenue?")
      ? {
          ...revenueReport,
          selected: { amountVnd: 0, paidOrders: 0 },
          points: revenueReport.points.map((p) => ({
            ...p,
            amountVnd: 0,
            paidOrders: 0,
          })),
        }
      : { ...purchaseHistory, total: 0, items: [] },
  );
  fireEvent.click(screen.getByRole("button", { name: "Làm mới doanh thu" }));
  await screen.findByText("Chưa phát sinh doanh thu trong khoảng này");
  expect(screen.getByText("Không tìm thấy đơn hàng")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Trang sau" }).disabled).toBe(true);
});
it("ignores an old response after filters change", async () => {
  let resolveOld;
  api.mockImplementation((path) =>
    path.includes("groupBy=day")
      ? new Promise((resolve) => {
          resolveOld = resolve;
        })
      : Promise.resolve(
          path.includes("/revenue?") ? revenueReport : purchaseHistory,
        ),
  );
  render(<AdminCommerce />);
  await screen.findByText("Nguyễn Minh Anh");
  fireEvent.change(screen.getByLabelText("Biểu đồ theo"), {
    target: { value: "month" },
  });
  await screen.findByText("Doanh thu 01/11/2026 – 03/11/2026");
  resolveOld({ ...revenueReport, from: "2026-09-01" });
  await waitFor(() =>
    expect(screen.queryByText(/Doanh thu 01\/09\/2026/)).toBeNull(),
  );
});
it("uses Vietnam local date and Monday for preset ranges", () => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-11-01T17:05:00Z"));
  try {
    expect(revenuePreset("today")).toEqual({
      from: "2026-11-02",
      to: "2026-11-02",
    });
    expect(revenuePreset("week")).toEqual({
      from: "2026-11-02",
      to: "2026-11-02",
    });
    expect(revenuePreset("month").from).toBe("2026-11-01");
  } finally {
    vi.useRealTimers();
  }
});
