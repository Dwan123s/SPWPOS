import { beforeEach, expect, it, vi } from "vitest";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { api } from "../src/api.js";
import {
  EventConflictReview,
  TodayWorkload,
  WorkloadWeek,
} from "../src/workload.jsx";
import { state } from "../src/ui.jsx";
import { App } from "../src/App.jsx";
import { update } from "../src/store.js";
import { openSessionOutcome } from "../src/workload.jsx";
import { workspace, resetFixtures } from "./fixtures.js";

vi.mock("../src/api.js", () => ({ api: vi.fn() }));

const view = () => ({
  start: "2026-11-09",
  end: "2026-11-15",
  completedTasks: 0,
  next: { kind: "SETUP", reason: "Xác nhận giờ làm." },
  tasks: [],
  days: [
    {
      date: "2026-11-11",
      status: "UNCONFIGURED",
      workingMinutes: null,
      reservedMinutes: null,
      schedulableMinutes: null,
      plannedSeconds: 0,
      recordedActualSeconds: null,
      sessions: [],
    },
  ],
});

beforeEach(() => {
  resetFixtures();
  state.workspace = workspace();
  api.mockReset();
  update({
    screen: null,
    screenKey: "loading",
    modal: null,
    toasts: [],
    slots: {},
  });
});

it("records total remaining work without generating a recovery plan", async () => {
  api.mockResolvedValue({
    sessionId: "ended",
    taskId: "task",
    outcome: "PARTIAL",
    actualSeconds: 3600,
    remainingMinutes: 90,
  });
  const changed = vi.fn();
  render(<App initialize={false} />);
  act(() =>
    openSessionOutcome(
      { id: "ended", name: "Report", timerSeconds: 3600 },
      changed,
    ),
  );
  fireEvent.change(
    screen.getByLabelText("Tổng số phút còn cần, gồm cả các phiên đã đặt"),
    { target: { value: "90" } },
  );
  fireEvent.submit(
    screen.getByRole("button", { name: "Lưu kết quả" }).closest("form"),
  );
  await screen.findByRole("button", { name: "Xem phương án" });
  expect(api).toHaveBeenCalledWith("/api/sessions/ended/outcome", "POST", {
    outcome: "PARTIAL",
    actualMinutes: null,
    remainingMinutes: 90,
  });
  expect(api.mock.calls.some(([path]) => path.includes("/api/plans"))).toBe(
    false,
  );
  expect(changed).toHaveBeenCalledOnce();
});

it("offers setup and capture with AI disabled and creates no proposal on opening", async () => {
  api.mockResolvedValue(view());
  render(<TodayWorkload />);
  expect(
    await screen.findByRole("button", { name: "Thiết lập giờ làm" }),
  ).toBeTruthy();
  expect(screen.getByRole("button", { name: "Ghi việc trước" })).toBeTruthy();
  expect(api.mock.calls).toEqual([["/api/intelligence/workload"]]);
  fireEvent.click(screen.getByRole("button", { name: "Bỏ qua" }));
  expect(screen.queryByRole("region", { name: "Bước tiếp theo" })).toBeNull();
});

it("discards an older response after a newer workspace refresh", async () => {
  let finishOld;
  api.mockImplementationOnce(
    () =>
      new Promise((resolve) => {
        finishOld = resolve;
      }),
  );
  const fresh = view();
  fresh.next = { kind: "CAPTURE", reason: "Giờ làm đã lưu." };
  api.mockResolvedValueOnce(fresh);
  render(<TodayWorkload />);
  act(() => window.dispatchEvent(new Event("workspace-updated")));
  await screen.findByText("Giờ làm đã lưu.");
  await act(async () => finishOld(view()));
  expect(
    screen.queryByRole("button", { name: "Thiết lập giờ làm" }),
  ).toBeNull();
});

it("shows missing actual separately from a recorded zero and uses day details", async () => {
  const data = view();
  data.days.push({
    ...data.days[0],
    date: "2026-11-12",
    status: "PAST",
    recordedActualSeconds: 0,
  });
  api.mockResolvedValue(data);
  render(<WorkloadWeek />);
  await screen.findByRole("heading", { name: "Tải công việc trong tuần" });
  const actual = Array.from(document.querySelectorAll("details > p")).map(
    (node) => node.textContent,
  );
  expect(actual[0]).toContain("Thực làm —");
  expect(actual[1]).not.toContain("Thực làm —");
  expect(
    document.querySelectorAll(".workload-day details summary"),
  ).toHaveLength(2);
});

it("previews a conflicting event only on request and confirms only on a second action", async () => {
  const input = {
    name: "Meeting",
    start: "2026-11-12T08:00:00Z",
    end: "2026-11-12T09:00:00Z",
  };
  const onSaved = vi.fn();
  api
    .mockResolvedValueOnce({
      id: "proposal",
      event: input,
      before: [],
      after: [],
      warnings: [],
    })
    .mockResolvedValueOnce({ id: "event" });
  render(
    <EventConflictReview input={input} onSaved={onSaved} onBack={vi.fn()} />,
  );
  expect(api).not.toHaveBeenCalled();
  fireEvent.click(
    screen.getByRole("button", { name: "Giữ giờ sự kiện và xem lịch đề xuất" }),
  );
  const confirm = await screen.findByRole("button", {
    name: "Xác nhận sự kiện và lịch mới",
  });
  expect(api.mock.calls).toEqual([
    ["/api/time/events/replan/preview", "POST", input],
  ]);
  fireEvent.click(confirm);
  await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
  expect(api).toHaveBeenLastCalledWith(
    "/api/time/events/replan/proposal/confirm",
    "POST",
  );
});
