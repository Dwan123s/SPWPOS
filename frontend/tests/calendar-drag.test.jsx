import { beforeEach, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { state } from "../src/ui.jsx";
import { calendarPage } from "../src/schedule.jsx";
import {
  atMinute,
  moveRange,
  shiftRange,
  daySegments,
  canDragCalendarItem,
} from "../src/calendar-drag.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";
const session = {
  id: "session",
  taskId: "task",
  taskName: "Làm báo cáo",
  start: "2026-11-05T03:00:00Z",
  end: "2026-11-05T04:00:00Z",
  status: "Scheduled",
  isLocked: false,
  actualSeconds: 0,
};
const entry = { session };
beforeEach(() => {
  vi.resetAllMocks();
  vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-11-03T01:00:00Z"));
  state.calendarStart = "2026-11-02";
  state.calendarView = "week";
  state.conflicts = [];
  state.workspace = {
    profile: { id: "user" },
    preference: { timeZoneId: "Asia/Ho_Chi_Minh", revision: "original" },
    tasks: [],
    sessions: [{ ...session }],
    events: [],
    availability: [],
    exceptions: [],
  };
  api.mockImplementation(async (path, method, body) => ({
    ...body,
    previousStart: body.expectedStart,
    previousEnd: body.expectedEnd,
    revision: "new",
  }));
});
it("snaps to 15-minute wall time in the account zone, preserving elapsed duration across dates", () => {
  expect(atMinute("2026-11-06", 0)).toBe("2026-11-05T17:00:00.000Z");
  expect(moveRange(session, "2026-11-06", 10 * 60 + 8)).toEqual({
    start: "2026-11-06T03:15:00.000Z",
    end: "2026-11-06T04:15:00.000Z",
  });
  expect(shiftRange(session, 1)).toEqual({
    start: "2026-11-06T03:00:00.000Z",
    end: "2026-11-06T04:00:00.000Z",
  });
  expect(moveRange(session, "2026-11-05", 11 * 60 + 30, true)).toEqual({
    start: session.start,
    end: "2026-11-05T04:30:00.000Z",
  });
});
it("clips overnight events to each local day and gives overlapping items separate lanes", () => {
  const event = {
    id: "night",
    start: "2026-11-05T16:00:00Z",
    end: "2026-11-05T18:00:00Z",
  };
  const first = daySegments([{ event }], "2026-11-05")[0],
    last = daySegments([{ event }], "2026-11-06")[0];
  expect(first.top).toBe(23 * 64);
  expect(first.height).toBe(64);
  expect(last.top).toBe(0);
  expect(last.height).toBe(64);
  const overlap = daySegments(
    [
      entry,
      { event: { id: "meeting", start: session.start, end: session.end } },
    ],
    "2026-11-05",
  );
  expect(overlap.map((x) => x.lane)).toEqual([0, 1]);
  expect(overlap.every((x) => x.lanes === 2)).toBe(true);
});
it("does not expose drag handles for locked, running, completed or past sessions", () => {
  state.workspace.sessions = [{ ...session, isLocked: true }];
  render(calendarPage());
  expect(screen.queryByRole("button", { name: "Kéo Làm báo cáo" })).toBeNull();
  for (const status of ["Running", "Paused", "Completed", "Cancelled"])
    expect(
      canDragCalendarItem({ session: { ...session, status } }),
    ).toBeFalsy();
  expect(
    canDragCalendarItem({
      session: { ...session, start: "2026-11-02T01:00:00Z" },
    }),
  ).toBeFalsy();
});
it("keyboard move saves only after explicit action and undo uses committed revision", async () => {
  render(calendarPage());
  expect(api).not.toHaveBeenCalled();
  fireEvent.keyDown(screen.getByRole("button", { name: "Kéo Làm báo cáo" }), {
    key: "ArrowRight",
    altKey: true,
  });
  await screen.findByText("Đã cập nhật lịch.");
  expect(api).toHaveBeenCalledWith("/api/calendar/move", "POST", {
    kind: "Session",
    id: "session",
    start: "2026-11-06T03:00:00.000Z",
    end: "2026-11-06T04:00:00.000Z",
    revision: "original",
    expectedStart: session.start,
    expectedEnd: session.end,
  });
  fireEvent.click(screen.getByRole("button", { name: "Hoàn tác" }));
  await screen.findByText("Đã hoàn tác thay đổi giờ.");
  expect(api.mock.calls[1][2]).toMatchObject({
    revision: "new",
    start: session.start,
    end: session.end,
  });
});
it("failed save retains the original item and explains the conflict", async () => {
  api.mockRejectedValue(new Error("Sự kiện trùng với phiên làm việc đã lưu."));
  render(calendarPage());
  fireEvent.keyDown(screen.getByRole("button", { name: "Kéo Làm báo cáo" }), {
    key: "ArrowDown",
    altKey: true,
  });
  expect((await screen.findByRole("alert")).textContent).toContain("trùng");
  expect(state.workspace.sessions[0].start).toBe(session.start);
  expect(screen.queryByRole("button", { name: "Hoàn tác" })).toBeNull();
});
it("keyboard resize sends the new end and preserves the start", async () => {
  render(calendarPage());
  fireEvent.keyDown(
    screen.getByRole("button", { name: "Đổi thời lượng Làm báo cáo" }),
    { key: "ArrowDown", altKey: true },
  );
  await waitFor(() => expect(api).toHaveBeenCalled());
  expect(api.mock.calls[0][2]).toMatchObject({
    start: session.start,
    end: "2026-11-05T04:15:00.000Z",
  });
});
it("cancels pending pointer gestures on Escape and unmount without writes", () => {
  const View = render(calendarPage());
  fireEvent.pointerDown(
    screen.getByRole("button", { name: "Kéo Làm báo cáo" }),
    { button: 0, pointerId: 1, clientX: 100, clientY: 100 },
  );
  fireEvent.keyDown(window, { key: "Escape" });
  View.unmount();
  expect(api).not.toHaveBeenCalled();
  expect(document.body.classList.contains("calendar-is-dragging")).toBe(false);
});
