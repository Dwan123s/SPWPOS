import { act, render, screen } from "@testing-library/react";
import { beforeEach, afterEach, expect, it } from "vitest";
import { sessionDetail } from "../src/calendar.jsx";
import { getSnapshot, hideModal } from "../src/store.js";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";

beforeEach(() => {
  state.workspace = structuredClone(workspace());
  state.workspace.features = [];
  const task = state.workspace.tasks[0];
  task.name = "Họp nhóm";
  state.workspace.sessions = [
    {
      id: "meeting-session",
      taskId: task.id,
      taskName: task.name,
      status: "Scheduled",
      isLocked: false,
      start: new Date(Date.now() + 3600000).toISOString(),
      end: new Date(Date.now() + 10800000).toISOString(),
      actualMinutes: 0,
    },
  ];
});
afterEach(() => hideModal());

it.each([false, true])(
  "does not offer splitting a continuous session when a legacy raw flag is %s",
  (legacyFlag) => {
    Object.assign(state.workspace.tasks[0], {
      splittable: legacyFlag,
      requiresContinuousSession: true,
    });
    act(() => sessionDetail("meeting-session"));
    render(getSnapshot().modal.body);
    expect(
      screen.queryByRole("button", { name: "Chia thành hai phiên" }),
    ).toBeNull();
    expect(
      screen.getByText(
        "Hoạt động này cần một phiên liên tục đủ thời lượng, không thể chia nhỏ.",
      ),
    ).toBeTruthy();
  },
);

it("still offers splitting an ordinary task when permitted", () => {
  Object.assign(state.workspace.tasks[0], {
    name: "Chuẩn bị tài liệu họp",
    splittable: true,
    requiresContinuousSession: false,
  });
  act(() => sessionDetail("meeting-session"));
  render(getSnapshot().modal.body);
  expect(
    screen.getByRole("button", { name: "Chia thành hai phiên" }),
  ).toBeTruthy();
});
