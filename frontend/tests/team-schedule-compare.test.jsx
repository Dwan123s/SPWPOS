import { beforeEach, expect, it } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { TeamScheduleCompare } from "../src/team-schedule-compare.jsx";
import { state } from "../src/ui.jsx";

beforeEach(() => {
  state.workspace = { preference: { timeZoneId: "Asia/Ho_Chi_Minh" } };
});
const session = (id, start, end, preserved = false) => ({
  id,
  taskName: `Công việc ${id}`,
  start,
  end,
  preserved,
});
it("aligns both schedules by local day and sorts the original unsorted sessions without mutating them", () => {
  const late = session(
    "late",
    "2026-11-02T09:00:00Z",
    "2026-11-02T10:00:00Z",
    true,
  );
  const early = session(
    "early",
    "2026-11-02T06:00:00Z",
    "2026-11-02T07:00:00Z",
    true,
  );
  const midnight = session(
    "tomorrow",
    "2026-11-02T18:00:00Z",
    "2026-11-02T19:00:00Z",
  );
  const before = [late, early];
  render(
    <TeamScheduleCompare before={before} after={[midnight, late, early]} />,
  );
  const previousDay = screen.getByRole("region", {
    name: /Lịch trước khi nhận ngày 02[-/]11/,
  });
  expect(
    within(previousDay)
      .getAllByRole("listitem")
      .map((el) => el.textContent),
  ).toEqual([
    expect.stringContaining("Công việc early"),
    expect.stringContaining("Công việc late"),
  ]);
  expect(
    screen.getByRole("region", { name: /Lịch sau khi nhận ngày 03[-/]11/ })
      .textContent,
  ).toContain("01:00");
  expect(
    screen.getByRole("region", { name: /Lịch trước khi nhận ngày 03[-/]11/ })
      .textContent,
  ).toContain("Chưa có phiên");
  expect(before).toEqual([late, early]);
});
it("filters unchanged days and restores them without hiding baseline sessions on changed days", () => {
  const unchanged = session(
    "kept",
    "2026-11-02T06:00:00Z",
    "2026-11-02T07:00:00Z",
    true,
  );
  const moved = session("move", "2026-11-03T06:00:00Z", "2026-11-03T07:00:00Z");
  const next = {
    ...moved,
    start: "2026-11-03T07:30:00Z",
    end: "2026-11-03T08:30:00Z",
  };
  render(
    <TeamScheduleCompare
      before={[moved, unchanged]}
      after={[unchanged, next]}
    />,
  );
  const filter = screen.getByRole("button", { name: "Chỉ xem ngày thay đổi" });
  expect(screen.getByText("Đổi giờ")).toBeTruthy();
  expect(screen.getByText(/Trước: 03[-/]11 · 13:00–14:00/)).toBeTruthy();
  fireEvent.click(filter);
  expect(filter.getAttribute("aria-pressed")).toBe("true");
  expect(
    screen.queryByRole("region", { name: /Lịch trước khi nhận ngày 02[-/]11/ }),
  ).toBeNull();
  expect(
    screen.getByRole("region", { name: /Lịch trước khi nhận ngày 03[-/]11/ }),
  ).toBeTruthy();
  fireEvent.click(filter);
  expect(
    screen.getByRole("region", { name: /Lịch trước khi nhận ngày 02[-/]11/ }),
  ).toBeTruthy();
});
it("keeps clear empty states when there is no calendar to compare", () => {
  render(<TeamScheduleCompare />);
  expect(screen.getByText("Chưa có phiên làm việc để so sánh.")).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Chỉ xem ngày thay đổi" }).disabled,
  ).toBe(true);
});
