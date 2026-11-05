import { expect, it } from "vitest";
import { smartTaskLists, isInboxNote } from "../src/task-smart-lists.js";
const task = (id, deadline, status = "Todo") => ({
  id,
  deadline,
  status,
  priority: 2,
});
const ids = (groups) =>
  groups.flatMap((group) => group.tasks.map((item) => item.id));
it("uses the account day, includes overdue and scheduled work, and excludes finished or cancelled work", () => {
  const workspace = {
    preference: { timeZoneId: "Asia/Ho_Chi_Minh" },
    tasks: [
      task("overdue", "2026-11-03T12:00:00+07:00"),
      task("today", "2026-11-04T23:59:00+07:00"),
      task("tomorrow", "2026-11-05T00:00:00+07:00"),
      task("planned", "2026-11-30T12:00:00+07:00"),
      task("done", "2026-11-04T12:00:00+07:00", "Completed"),
      task("cancelled", "2026-11-04T12:00:00+07:00", "Cancelled"),
    ],
    sessions: [
      {
        taskId: "planned",
        status: "Scheduled",
        start: "2026-11-04T08:00:00+07:00",
        end: "2026-11-04T09:00:00+07:00",
      },
    ],
  };
  const view = smartTaskLists(workspace, Date.parse("2026-11-03T18:00:00Z"));
  expect(view.today).toBe("2026-11-04");
  expect(view.todayGroups[0].key).toBe("overdue");
  expect(ids(view.todayGroups)).toEqual(["overdue", "today", "planned"]);
  expect(ids(view.weekGroups)).toContain("tomorrow");
});
it("covers exactly seven local calendar dates across DST and does not duplicate tasks with multiple sessions", () => {
  const workspace = {
    preference: { timeZoneId: "America/New_York" },
    tasks: [
      task("last", "2026-11-04T23:59:00-05:00"),
      task("outside", "2026-11-05T00:00:00-05:00"),
      task("sessions", "2026-11-30T00:00:00-05:00"),
    ],
    sessions: [
      {
        taskId: "sessions",
        status: "Scheduled",
        start: "2026-10-30T08:00:00-04:00",
        end: "2026-10-30T09:00:00-04:00",
      },
      {
        taskId: "sessions",
        status: "Scheduled",
        start: "2026-11-02T08:00:00-05:00",
        end: "2026-11-02T09:00:00-05:00",
      },
    ],
  };
  const view = smartTaskLists(workspace, Date.parse("2026-10-30T12:00:00Z"));
  expect(view.lastDay).toBe("2026-11-05");
  expect(ids(view.weekGroups).filter((id) => id === "sessions")).toHaveLength(
    1,
  );
  // Oct 30, 31, Nov 1, 2, 3, 4, 5 are the seven calendar dates.
  workspace.tasks.push(task("after-seven", "2026-11-06T00:00:00-05:00"));
  expect(
    ids(
      smartTaskLists(workspace, Date.parse("2026-10-30T12:00:00Z")).weekGroups,
    ),
  ).not.toContain("after-seven");
});
it("treats midnight as the end of the previous day and ignores cancelled sessions", () => {
  const workspace = {
    tasks: [
      task("midnight", "2026-12-01T00:00:00Z"),
      task("cancelled-session", "2026-12-01T00:00:00Z"),
    ],
    sessions: [
      {
        taskId: "midnight",
        status: "Scheduled",
        start: "2026-11-03T23:00:00+07:00",
        end: "2026-11-04T00:00:00+07:00",
      },
      {
        taskId: "cancelled-session",
        status: "Cancelled",
        start: "2026-11-04T10:00:00+07:00",
        end: "2026-11-04T11:00:00+07:00",
      },
    ],
  };
  expect(
    ids(
      smartTaskLists(workspace, Date.parse("2026-11-04T01:00:00+07:00"))
        .todayGroups,
    ),
  ).toEqual([]);
});
it("keeps ideas and unfinished drafts in Inbox and hides imported drafts until content is edited again", () => {
  expect(isInboxNote({ createdTaskIds: [], analysis: null })).toBe(true);
  expect(
    isInboxNote({ analysisImported: true, createdTaskIds: ["task"] }),
  ).toBe(false);
  expect(
    isInboxNote({
      analysisImported: false,
      createdTaskIds: ["task"],
      hasProcessedContent: true,
    }),
  ).toBe(true);
});
