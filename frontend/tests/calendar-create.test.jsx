import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { CalendarWeek } from "../src/calendar-week.jsx";
import { calendarPage } from "../src/schedule.jsx";
import { createCalendarRange, minuteHeight } from "../src/calendar-drag.js";
import { state } from "../src/ui.jsx";
import { getSnapshot, update } from "../src/store.js";
import { takeAgentDraft } from "../src/agent-handoff.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";

const dates = ["2026-11-04", "2026-11-05", "2026-11-06"];
let onCreate;
beforeEach(() => {
  vi.resetAllMocks();
  vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-11-04T01:00:00Z"));
  state.workspace = {
    profile: { id: "owner" },
    preference: { timeZoneId: "Asia/Ho_Chi_Minh" },
    sessions: [],
    events: [],
    tasks: [],
    availability: [],
    exceptions: [],
  };
  state.calendarStart = "2026-11-02";
  state.calendarView = "week";
  state.conflicts = [];
  state.noraAvailable = false;
  onCreate = vi.fn();
  update({ slots: {}, modal: null });
  window.PointerEvent = class extends MouseEvent {
    constructor(type, init = {}) {
      super(type, init);
      this.pointerId = init.pointerId ?? 1;
      this.pointerType = init.pointerType ?? "mouse";
    }
  };
});
function setup() {
  const view = render(
    <CalendarWeek
      dates={dates}
      now={Date.parse("2026-11-03T01:00:00Z")}
      block={(session) => <button>{session.taskName}</button>}
      fixed={(event) => <button>{event.name}</button>}
      onCreateEvent={onCreate}
    />,
  );
  const scroll = screen.getByRole("region", {
    name: "Lịch 7 ngày, cuộn dọc và ngang để xem thêm",
  });
  scroll.getBoundingClientRect = () => ({
    left: 0,
    right: 1000,
    top: 0,
    bottom: 700,
    height: 700,
    width: 1000,
  });
  const tracks = [...view.container.querySelectorAll("[data-calendar-date]")];
  tracks.forEach((track, index) => {
    track.getBoundingClientRect = () => ({
      left: 54 + index * 150,
      right: 204 + index * 150,
      top: -400,
      bottom: 1136,
      height: 1536,
      width: 150,
    });
  });
  const point = (day, minutes, extra = {}) => ({
    pointerId: 1,
    button: 0,
    clientX: 100 + day * 150,
    clientY: -400 + minutes * minuteHeight,
    ...extra,
  });
  return { view, tracks, point, scroll };
}
it("selects a one-hour slot on click and only opens the editor after release", () => {
  const { tracks, point } = setup();
  fireEvent.pointerDown(tracks[0], point(0, 9 * 60 + 8));
  expect(onCreate).not.toHaveBeenCalled();
  expect(api).not.toHaveBeenCalled();
  expect(screen.getByText("Lịch mới")).toBeTruthy();
  fireEvent.pointerUp(window, point(0, 9 * 60 + 8));
  expect(onCreate).toHaveBeenCalledWith({
    start: "2026-11-04T02:00:00.000Z",
    end: "2026-11-04T03:00:00.000Z",
  });
  expect(screen.queryByText("Lịch mới")).toBeNull();
  expect(api).not.toHaveBeenCalled();
});
it.each([false, true])(
  "drags a 15-minute-snapped range downward or upward (%s) without writing",
  (upward) => {
    const { tracks, point } = setup();
    fireEvent.pointerDown(tracks[0], point(0, upward ? 11 * 60 : 9 * 60));
    fireEvent.pointerMove(window, point(0, upward ? 9 * 60 : 11 * 60));
    expect(document.body.classList.contains("calendar-is-selecting")).toBe(
      true,
    );
    expect(onCreate).not.toHaveBeenCalled();
    fireEvent.pointerUp(window, point(0, upward ? 9 * 60 : 11 * 60));
    expect(onCreate).toHaveBeenCalledWith({
      start: "2026-11-04T02:00:00.000Z",
      end: "2026-11-04T04:00:00.000Z",
    });
    expect(document.body.classList.contains("calendar-is-selecting")).toBe(
      false,
    );
    expect(api).not.toHaveBeenCalled();
  },
);
it("supports selecting across dates and ignores another pointer", () => {
  const { tracks, point } = setup();
  fireEvent.pointerDown(tracks[0], point(0, 10 * 60));
  fireEvent.pointerMove(window, point(1, 11 * 60, { pointerId: 2 }));
  fireEvent.pointerUp(window, point(1, 11 * 60, { pointerId: 2 }));
  expect(onCreate).not.toHaveBeenCalled();
  fireEvent.pointerMove(window, point(1, 11 * 60));
  fireEvent.pointerUp(window, point(1, 11 * 60));
  expect(onCreate).toHaveBeenCalledWith({
    start: "2026-11-04T03:00:00.000Z",
    end: "2026-11-05T04:00:00.000Z",
  });
});
it("consumes the compatibility click before outside-dismiss sees it, while allowing the next deliberate click", () => {
  const { tracks, point } = setup();
  const outsideClick = vi.fn();
  document.addEventListener("click", outsideClick, true);
  try {
    fireEvent.pointerDown(tracks[0], point(0, 9 * 60));
    fireEvent.pointerMove(window, point(0, 10 * 60));
    fireEvent.pointerUp(window, point(0, 10 * 60));
    expect(onCreate).toHaveBeenCalledTimes(1);
    // Native dialogs retarget the trailing click to the backdrop after showModal.
    fireEvent.click(document.body, point(0, 10 * 60));
    expect(outsideClick).not.toHaveBeenCalled();
    fireEvent.pointerDown(document.body, point(0, 10 * 60));
    fireEvent.click(document.body, point(0, 10 * 60));
    expect(outsideClick).toHaveBeenCalledTimes(1);
  } finally {
    document.removeEventListener("click", outsideClick, true);
  }
});
it.each(["Escape", "pointercancel", "outside", "unmount", "account", "blur"])(
  "cancels safely on %s",
  (reason) => {
    const { view, tracks, point } = setup();
    fireEvent.pointerDown(tracks[0], point(0, 9 * 60));
    fireEvent.pointerMove(window, point(0, 10 * 60));
    if (reason === "Escape") fireEvent.keyDown(window, { key: "Escape" });
    if (reason === "pointercancel")
      fireEvent.pointerCancel(window, point(0, 10 * 60));
    if (reason === "unmount") view.unmount();
    if (reason === "blur") fireEvent.blur(window);
    if (reason === "account") state.workspace.profile.id = "other";
    fireEvent.pointerUp(
      window,
      point(0, 10 * 60, reason === "outside" ? { clientX: 1100 } : {}),
    );
    expect(onCreate).not.toHaveBeenCalled();
    expect(api).not.toHaveBeenCalled();
    expect(document.body.classList.contains("calendar-is-selecting")).toBe(
      false,
    );
  },
);
it("preserves touch scrolling unless selection mode was explicitly enabled", () => {
  const { tracks, point } = setup();
  fireEvent.pointerDown(tracks[0], point(0, 9 * 60, { pointerType: "touch" }));
  fireEvent.pointerMove(window, point(0, 10 * 60, { pointerType: "touch" }));
  fireEvent.pointerUp(window, point(0, 10 * 60, { pointerType: "touch" }));
  expect(onCreate).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Chọn khoảng giờ" }));
  fireEvent.pointerDown(tracks[0], point(0, 9 * 60, { pointerType: "touch" }));
  fireEvent.pointerMove(window, point(0, 10 * 60, { pointerType: "touch" }));
  fireEvent.pointerUp(window, point(0, 10 * 60, { pointerType: "touch" }));
  expect(onCreate).toHaveBeenCalledTimes(1);
});
it("does not start range creation from an existing event and supports a keyboard accessible day button", () => {
  state.workspace.events = [
    {
      id: "meeting",
      name: "Cuộc họp",
      start: "2026-11-04T02:00:00Z",
      end: "2026-11-04T03:00:00Z",
    },
  ];
  const { point } = setup();
  fireEvent.pointerDown(
    screen.getByRole("button", { name: "Cuộc họp", exact: true }),
    point(0, 9 * 60),
  );
  fireEvent.pointerUp(window, point(0, 9 * 60));
  expect(onCreate).not.toHaveBeenCalled();
  fireEvent.click(
    screen.getByRole("button", { name: "Thêm lịch ngày 2026-11-05" }),
  );
  expect(onCreate).toHaveBeenCalledWith({
    start: "2026-11-05T02:00:00.000Z",
    end: "2026-11-05T03:00:00.000Z",
  });
});
it("opens the manual event form with the selected time even in secretary mode", () => {
  state.workspace.features = ["secretary-v1"];
  render(calendarPage());
  fireEvent.click(
    screen.getByRole("button", { name: "Thêm lịch ngày 2026-11-05" }),
  );
  const modal = getSnapshot().modal;
  expect(modal.title).toBe("Thêm Lịch Trình");
  render(modal.body);
  expect(screen.getByLabelText("Bắt đầu").value).toBe("2026-11-05T09:00");
  expect(screen.getByLabelText("Kết thúc").value).toBe("2026-11-05T10:00");
  expect(screen.getByLabelText("Tên Lịch Trình")).toBe(document.activeElement);
  expect(api).not.toHaveBeenCalled();
});

it("shows duration, asks for a count only for weekly repeats, and carries the draft to NORA", () => {
  state.noraAvailable = true;
  render(calendarPage());
  fireEvent.click(screen.getByRole("button", { name: "Thêm lịch ngày 2026-11-05" }));
  const { container } = render(getSnapshot().modal.body);
  const form = container.querySelector("form");
  expect(new FormData(form).has("repeatWeeks")).toBe(false);
  fireEvent.change(screen.getByLabelText("Tên Lịch Trình"), { target: { value: "Ca làm chiều" } });
  fireEvent.change(screen.getByLabelText("Loại lịch"), { target: { value: "WORK" } });
  fireEvent.change(screen.getByLabelText("Kết thúc"), { target: { value: "2026-11-05T10:30" } });
  expect(screen.getByText("Thời lượng 1 giờ 30 phút")).toBeTruthy();
  fireEvent.change(screen.getByLabelText("Lặp lại"), { target: { value: "weekly" } });
  fireEvent.change(screen.getByLabelText("Tổng số lần"), { target: { value: "4" } });
  expect(new FormData(form).get("repeatWeeks")).toBe("4");
  fireEvent.click(screen.getByRole("button", { name: "Nhờ NORA" }));
  expect(takeAgentDraft()).toMatch(/Chỉ xem trước[\s\S]*Ca làm chiều[\s\S]*2026-11-05 10:30[\s\S]*tổng 4 lần/);
  expect(api).not.toHaveBeenCalled();
});
it("clips a click near midnight and computes elapsed time correctly across DST", () => {
  expect(createCalendarRange({ date: "2026-11-04", minute: 1439 })).toEqual({
    start: "2026-11-04T16:45:00.000Z",
    end: "2026-11-04T17:00:00.000Z",
  });
  state.workspace.preference.timeZoneId = "America/New_York";
  const range = createCalendarRange(
    { date: "2026-10-31", minute: 23 * 60 },
    { date: "2026-11-01", minute: 3 * 60 },
    true,
  );
  expect((Date.parse(range.end) - Date.parse(range.start)) / 3600000).toBe(5);
});
it("rejects clicking a past hour and dragging a future range backwards into the past", () => {
  const { tracks, point } = setup();
  fireEvent.pointerDown(tracks[0], point(0, 7 * 60));
  fireEvent.pointerUp(window, point(0, 7 * 60));
  expect(onCreate).not.toHaveBeenCalled();
  expect(screen.getByRole("alert").textContent).toContain("quá khứ");
  fireEvent.pointerDown(tracks[0], point(0, 9 * 60));
  fireEvent.pointerMove(window, point(0, 7 * 60 + 45));
  fireEvent.pointerUp(window, point(0, 7 * 60 + 45));
  expect(onCreate).not.toHaveBeenCalled();
  expect(screen.queryByText("Lịch mới")).toBeNull();
});
it("checks the clock again on release rather than allowing a slot that expired while selecting", () => {
  const { tracks, point } = setup();
  fireEvent.pointerDown(tracks[0], point(0, 9 * 60));
  vi.mocked(Date.now).mockReturnValue(Date.parse("2026-11-04T02:01:00Z"));
  fireEvent.pointerUp(window, point(0, 9 * 60));
  expect(onCreate).not.toHaveBeenCalled();
  expect(api).not.toHaveBeenCalled();
});
it("disables creation on past days and preselects the next quarter-hour when today's 09:00 has passed", () => {
  vi.mocked(Date.now).mockReturnValue(Date.parse("2026-11-04T08:17:00Z"));
  render(calendarPage());
  expect(
    screen.getByRole("button", { name: "Thêm lịch ngày 2026-11-03" }).disabled,
  ).toBe(true);
  fireEvent.click(
    screen.getByRole("button", { name: "Thêm lịch ngày 2026-11-04" }),
  );
  render(getSnapshot().modal.body);
  expect(screen.getByLabelText("Bắt đầu").value).toBe("2026-11-04T15:30");
  expect(screen.getByLabelText("Bắt đầu").min).toBe("2026-11-04T15:18");
});
