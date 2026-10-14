import { expect, it } from "vitest";
import { sessionStartIssue } from "../src/session-start.jsx";

const start = Date.parse("2026-10-14T03:00:00Z");
const session = {
  id: "s1",
  taskId: "t1",
  status: "Scheduled",
  start: new Date(start).toISOString(),
  end: new Date(start + 3600000).toISOString(),
};
const workspace = {
  tasks: [{ id: "t1", status: "Todo", dependencies: [] }],
  sessions: [session],
  events: [],
};

it("allows starting only from fifteen minutes before a scheduled session through its end", () => {
  expect(
    sessionStartIssue(session, workspace, start - 15 * 60000 - 1),
  ).toContain("sớm 15 phút");
  expect(sessionStartIssue(session, workspace, start - 15 * 60000)).toBeNull();
  expect(sessionStartIssue(session, workspace, start + 3600000)).toBeNull();
  expect(sessionStartIssue(session, workspace, start + 3600001)).toContain(
    "quá giờ",
  );
});
it("explains active sessions, unfinished prerequisites and current fixed events", () => {
  expect(
    sessionStartIssue(
      session,
      { ...workspace, sessions: [{ status: "Paused" }] },
      start,
    ),
  ).toContain("phiên đang chạy");
  const dependencies = {
    ...workspace,
    tasks: [
      { ...workspace.tasks[0], dependencies: ["t2"] },
      { id: "t2", status: "Todo" },
    ],
  };
  expect(sessionStartIssue(session, dependencies, start)).toContain(
    "phụ thuộc",
  );
  dependencies.tasks[1].status = "Completed";
  expect(sessionStartIssue(session, dependencies, start)).toBeNull();
  expect(
    sessionStartIssue(
      session,
      { ...workspace, events: [{ start: session.start, end: session.end }] },
      start,
    ),
  ).toContain("sự kiện cố định");
});
