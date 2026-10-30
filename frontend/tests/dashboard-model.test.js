import { expect, it } from "vitest";
import { dashboardModel } from "../src/dashboard-model.js";
const now = new Date("2026-10-12T08:00:00+07:00").getTime();
function workspace() {
  return {
    preference: { timeZoneId: "Asia/Ho_Chi_Minh", breakMinutes: 15 },
    availability: [{ dayOfWeek: 1, startMinute: 480, endMinute: 1080 }],
    exceptions: [],
    events: [],
    sessions: [],
    tasks: [],
  };
}
function session(id, start, end, status = "Scheduled") {
  return {
    id,
    start: `2026-10-12T${start}:00+07:00`,
    end: `2026-10-12T${end}:00+07:00`,
    status,
  };
}
it("merges overlapping availability and subtracts calendar exceptions and fixed events once", () => {
  const w = workspace();
  w.availability.push({ dayOfWeek: 1, startMinute: 540, endMinute: 900 });
  w.exceptions = [
    {
      date: "2026-10-12",
      startMinute: 720,
      endMinute: 780,
      isAvailable: false,
    },
  ];
  w.events = [session("meeting", "12:30", "14:00")];
  const model = dashboardModel(w, { capacityFactor: 0.8 }, now);
  expect(model.today).toBe("2026-10-12");
  expect(model.availableMinutes).toBe(480);
  expect(model.capacityMinutes).toBe(384);
});
it("uses the workspace timezone and clips sessions crossing midnight", () => {
  const w = workspace();
  w.sessions = [
    {
      id: "overnight",
      start: "2026-10-11T23:30:00+07:00",
      end: "2026-10-12T00:30:00+07:00",
      status: "Running",
    },
    session("next", "09:00", "10:00"),
  ];
  const model = dashboardModel(w, { capacityFactor: 0.8 }, now);
  expect(model.focus.id).toBe("overnight");
  expect(model.focusMinutes).toBe(30);
  expect(model.scheduledMinutes).toBe(60);
  expect(model.timeline[0].span).toEqual([0, 30]);
});
it("counts scheduled occupancy once and excludes skipped and missed sessions", () => {
  const w = workspace();
  w.sessions = [
    session("first", "09:00", "10:00"),
    session("overlap", "09:30", "10:30"),
    session("skip", "11:00", "12:00", "Skipped"),
    session("miss", "14:00", "15:00", "Missed"),
  ];
  const model = dashboardModel(w, null, now);
  expect(model.days[0].planned).toBe(90);
  expect(model.daySessions).toHaveLength(2);
  expect(model.capacityMinutes).toBeNull();
  expect(model.accuracy).toBeNull();
});
it("does not invent a current focus from sessions that already ended", () => {
  const w = workspace();
  w.sessions = [session("past", "06:00", "07:00")];
  expect(dashboardModel(w, null, now).focus).toBeUndefined();
});
it("reserves break buffers only where occupied intervals leave enough space", () => {
  const w = workspace();
  w.sessions = [
    session("first", "09:00", "10:00"),
    session("second", "10:10", "11:00"),
    session("third", "12:00", "13:00"),
  ];
  w.events = [session("meeting", "11:00", "11:45")];
  const model = dashboardModel(w, null, now);
  expect(model.breaks).toEqual([{ span: [705, 720] }]);
  expect(model.restMinutes).toBe(15);
});
it("uses actual recorded data for the gauge and leaves unavailable statistics unknown", () => {
  expect(
    dashboardModel(
      workspace(),
      { plannedMinutes: 100, actualMinutes: 125 },
      now,
    ).accuracy,
  ).toBe(80);
  expect(
    dashboardModel(workspace(), { plannedMinutes: 100, actualMinutes: 0 }, now)
      .accuracy,
  ).toBeNull();
});

it("caps the displayed daily capacity by the user's daily limit", () => {
  const w = workspace();
  w.preference.maximumDailyMinutes = 60;
  const model = dashboardModel(w, { capacityFactor: 0.8 }, now);
  expect(model.capacityMinutes).toBe(60);
});

it("uses the server's capacity day instead of reconstructing different business rules", () => {
  const model = dashboardModel(
    workspace(),
    {
      capacityFactor: 0.8,
      days: [
        {
          date: "2026-10-12",
          calendarFreeMinutes: 240,
          dailyBudgetMinutes: 360,
          consumedMinutes: 120,
          reservedMinutes: 60,
          effectiveCapacityMinutes: 180,
          schedulableMinutes: 120,
        },
      ],
    },
    now,
  );
  expect(model.capacityMinutes).toBe(180);
  expect(model.availableMinutes).toBe(240);
  expect(model.days[0].reserved).toBe(60);
});

it("includes fixed calendar entries in occupied time without counting overlaps twice", () => {
  const w = workspace();
  w.events = [
    session("class", "07:00", "09:00"),
    session("meeting", "08:30", "10:00"),
  ];
  w.sessions = [session("work", "09:30", "10:30")];
  const before = JSON.stringify(w);
  const model = dashboardModel(w, null, now);
  expect(model.days[0].planned).toBe(60);
  expect(model.days[0].calendarBusy).toBe(210);
  expect(model.calendarStats.remainingMinutes).toBe(150);
  expect(model.calendarStats.percent).toBe(29);
  expect(model.calendarStats.total).toBe(3);
  expect(model.calendarStats.current).toBe(1);
  expect(model.accuracy).toBeNull();
  expect(JSON.stringify(w)).toBe(before);
});
it("reports calendar data independently of missing free-time settings and estimation history", () => {
  const w = workspace();
  w.availability = [];
  w.events = [{ ...session("class", "07:00", "09:00"), name: "PRN212" }];
  const model = dashboardModel(
    w,
    { capacityFactor: 0.8, plannedMinutes: 0, actualMinutes: 0 },
    now,
  );
  expect(model.hasAvailability).toBe(false);
  expect(model.days[0].calendarBusy).toBe(120);
  expect(model.calendarStats.percent).toBe(50);
  expect(model.calendarStats.horizonCount).toBe(1);
  expect(model.calendarMilestones[0].name).toBe("PRN212");
  expect(model.calendarMilestones[0].ongoing).toBe(true);
  expect(model.accuracy).toBeNull();
});
it("clips overnight calendar entries to each local day and excludes cancelled sessions", () => {
  const w = workspace();
  w.events = [
    {
      id: "overnight",
      name: "Overnight",
      start: "2026-10-11T23:30:00+07:00",
      end: "2026-10-12T00:30:00+07:00",
    },
  ];
  w.sessions = [session("cancel", "08:00", "12:00", "Cancelled")];
  const model = dashboardModel(w, null, now);
  expect(model.days[0].calendarBusy).toBe(30);
  expect(model.calendarStats.percent).toBe(100);
  expect(model.calendarStats.remainingMinutes).toBe(0);
  expect(model.calendarStats.horizonCount).toBe(1);
  expect(model.calendarMilestones).toHaveLength(0);
});
