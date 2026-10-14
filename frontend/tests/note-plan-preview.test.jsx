import { beforeEach, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NotePlanPreview } from "../src/note-plan-preview.jsx";
import { state } from "../src/ui.jsx";

beforeEach(() => {
  state.workspace = { preference: { timeZoneId: "Asia/Ho_Chi_Minh" } };
});
it("shows simulated task names with preserved work and fixed events by local day", async () => {
  const result = {
    after: { tasks: [{ taskId: "new", name: "Viết báo cáo" }] },
    proposedPlan: {
      start: "2026-10-14T02:00:00Z",
      sessions: [
        {
          taskId: "new",
          start: "2026-10-14T03:00:00Z",
          end: "2026-10-14T04:00:00Z",
        },
        {
          taskId: "old",
          preserved: true,
          start: "2026-10-15T03:00:00Z",
          end: "2026-10-15T04:00:00Z",
        },
      ],
    },
  };
  const workspace = {
    tasks: [{ id: "old", name: "Ôn chương 4" }],
    events: [
      {
        id: "fixed",
        name: "Buổi học",
        start: "2026-10-14T06:00:00Z",
        end: "2026-10-14T07:00:00Z",
      },
    ],
  };
  render(
    <NotePlanPreview result={result} workspace={workspace} horizonDays={2} />,
  );
  expect(screen.getByText("Viết báo cáo")).toBeTruthy();
  expect(screen.getByText("Buổi học")).toBeTruthy();
  expect(screen.getByText("Chưa lưu")).toBeTruthy();
  expect(screen.queryByText("Ôn chương 4")).toBeNull();
  await userEvent
    .setup()
    .selectOptions(
      screen.getByRole("combobox", { name: "Ngày xem lịch dự kiến" }),
      "2026-10-15",
    );
  expect(screen.getByText("Ôn chương 4")).toBeTruthy();
  expect(screen.getByText("Lịch đang có · giữ nguyên")).toBeTruthy();
  expect(screen.queryByText("Viết báo cáo")).toBeNull();
});
