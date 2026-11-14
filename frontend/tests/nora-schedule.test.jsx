import { beforeEach, expect, it, vi } from "vitest";
import { useState } from "react";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NoraSchedulePanel } from "../src/nora-schedule-panel.jsx";
import { NoraResultCard } from "../src/nora-result-card.jsx";
import {
  compactScheduleText,
  timelineForDay,
  conversationLabel,
} from "../src/nora-schedule.js";
import { state } from "../src/ui.jsx";
let workspace, failRefresh;
beforeEach(() => {
  vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-10-19T01:00:00Z"));
  failRefresh = false;
  workspace = {
    preference: { timeZoneId: "Asia/Ho_Chi_Minh" },
    tasks: [{ id: "task", name: "Hồ sơ" }],
    events: [
      {
        id: "class",
        name: "Đi học",
        start: "2026-10-20T00:00:00Z",
        end: "2026-10-20T05:00:00Z",
        eventType: "CLASS",
      },
      {
        id: "work",
        name: "Đi làm",
        start: "2026-10-20T06:00:00Z",
        end: "2026-10-20T10:00:00Z",
        eventType: "WORK",
      },
    ],
    sessions: [
      {
        id: "session",
        taskId: "task",
        start: "2026-10-20T10:00:00Z",
        end: "2026-10-20T10:30:00Z",
        status: "Scheduled",
      },
    ],
  };
  state.workspace = workspace;
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => ({
      ok: !failRefresh,
      status: failRefresh ? 503 : 200,
      json: async () =>
        failRefresh ? { error: { message: "Offline" } } : { data: workspace },
    })),
  );
});
it("finds saved personal tasks and drafts a preview for the selected ID without sending it", async () => {
  workspace.tasks.push(
    { id: "project", name: "Đồ án" },
    { id: "write", parentId: "project", name: "Viết báo cáo", estimatedMinutes: 90 },
    { id: "team", name: "Việc nhóm", isTeamTask: true },
    { id: "done", name: "Việc đã xong", status: "Completed" },
  );
  state.workspace = { ...workspace, tasks: [] };
  const suggest = vi.fn();
  const openTask = vi.fn();
  function Panel() {
    const [view, setView] = useState("day");
    return <NoraSchedulePanel view={view} onViewChange={setView} date="2026-10-20"
      onSuggest={suggest} onOpenTask={openTask} />;
  }
  const user = userEvent.setup();
  render(<Panel />);
  await screen.findByText(/Vừa cập nhật/);
  await user.click(screen.getByRole("button", { name: "Công việc", exact: true }));
  const list = screen.getByRole("region", { name: "Công việc cá nhân" });
  expect(within(list).queryByRole("button", { name: "Đồ án" })).toBeNull();
  expect(within(list).queryByText("Việc nhóm")).toBeNull();
  expect(within(list).queryByText("Việc đã xong")).toBeNull();
  expect(within(list).queryByText("Hồ sơ")).toBeNull();
  await user.click(within(list).getByRole("checkbox"));
  expect(within(list).getByText("Hồ sơ")).toBeTruthy();
  await user.type(within(list).getByRole("searchbox"), "Đồ án");
  expect(within(list).queryByText("Hồ sơ")).toBeNull();
  await user.click(within(list).getByRole("button", { name: "Viết báo cáo" }));
  expect(openTask).toHaveBeenCalledWith("write");
  expect(state.workspace.tasks).toContainEqual(expect.objectContaining({ id: "write" }));
  await user.click(within(list).getByRole("button", { name: /Soạn yêu cầu xếp lịch/ }));
  expect(suggest).toHaveBeenCalledWith(expect.stringContaining("(ID: write). Chỉ xem trước"));
  expect(fetch).toHaveBeenCalledTimes(1);
});
it("keeps fixed events and work sessions together in the saved day, excluding preview-only entries", async () => {
  const preview = {
    id: "preview",
    status: "PREVIEW",
    text: "Việc chưa lưu",
    changes: [],
    plan: {
      sessions: [
        {
          id: "unsaved",
          taskId: "task",
          start: "2026-10-20T11:00:00Z",
          end: "2026-10-20T12:00:00Z",
        },
      ],
    },
  };
  render(
    <NoraSchedulePanel
      turn={preview}
      view="day"
      date="2026-10-20"
      onDateChange={() => {}}
      onViewChange={() => {}}
      onSuggest={() => {}}
    />,
  );
  const day = await screen.findByRole("region", {
    name: "Lịch đã lưu ngày 20/10/2026",
  });
  await screen.findByText(/Vừa cập nhật/);
  expect(within(day).getByText("Đi học")).toBeTruthy();
  expect(within(day).getByText("Đi làm")).toBeTruthy();
  expect(within(day).getByText("Hồ sơ")).toBeTruthy();
  expect(day.textContent).not.toContain("18:00");
  expect(day.textContent).toContain("12:00–13:00");
  expect(day.textContent).toContain("Chưa đối chiếu giờ rảnh");
});
it("discloses refresh failure and does not present cached calendar data as newly verified", async () => {
  failRefresh = true;
  render(
    <NoraSchedulePanel
      view="day"
      date="2026-10-20"
      onDateChange={() => {}}
      onViewChange={() => {}}
      onSuggest={() => {}}
    />,
  );
  await screen.findByRole("alert");
  expect(screen.getByText(/Dữ liệu lần tải trước/)).toBeTruthy();
  expect(screen.queryByText(/Vừa cập nhật/)).toBeNull();
  failRefresh = false;
  await userEvent.click(screen.getByRole("button", { name: "Cập nhật lịch" }));
  await screen.findByText(/Vừa cập nhật/);
});
it("offers optional lunch and travel drafts without writing to the calendar", async () => {
  let draft = "";
  render(
    <NoraSchedulePanel
      view="day"
      date="2026-10-20"
      onDateChange={() => {}}
      onViewChange={() => {}}
      onSuggest={(value) => {
        draft = value;
      }}
    />,
  );
  await screen.findByText(/Vừa cập nhật/);
  await userEvent.click(
    screen.getByRole("button", { name: "Gợi ý nghỉ trưa 30 phút" }),
  );
  expect(draft).toContain("20/10/2026");
  expect(draft).toContain("12:00");
  expect(
    globalThis.fetch.mock.calls.every(
      ([, options]) => !options?.method || options.method === "GET",
    ),
  ).toBe(true);
});
it("renders one compact result card with recovery actions and hides duplicated logs behind details", async () => {
  const turn = {
    id: "saved",
    status: "APPLIED",
    canUndo: true,
    reply: "Tôi đã lên lịch đi học.",
    outcome: "Đã lưu Đi học [CLASS]",
    changes: [
      {
        kind: "EVENT",
        name: "Đi học",
        before: "",
        after: "20/10/2026 07:00 → 20/10/2026 12:00",
      },
    ],
  };
  let undo = 0;
  render(
    <NoraResultCard
      turn={turn}
      onAction={() => undo++}
      onViewResult={() => {}}
      onEditEvent={() => {}}
      busy={false}
    />,
  );
  const card = screen.getByRole("region", { name: "Kết quả yêu cầu" });
  expect(card.textContent).toContain("20/10/2026 · 07:00–12:00");
  expect(card.textContent).not.toContain("[CLASS]");
  await userEvent.click(within(card).getByRole("button", { name: "Hoàn tác" }));
  expect(undo).toBe(1);
  expect(within(card).getByRole("button", { name: "Sửa Đi học" })).toBeTruthy();
  expect(
    screen.getByText("Giải thích và chi tiết").closest("details").open,
  ).toBe(false);
});
it("clips overnight entries to the account day and detects overlaps rather than calling them free time", () => {
  workspace.events.push({
    id: "overnight",
    name: "Ca đêm",
    start: "2026-10-19T15:00:00Z",
    end: "2026-10-20T01:00:00Z",
  });
  const entries = timelineForDay(workspace, "2026-10-20");
  expect(entries[0].startLabel).toBe("00:00");
  expect(entries.find((x) => x.id === "class").overlap).toBe(true);
  expect(entries.find((x) => x.kind === "gap").startLabel).toBe("12:00");
});
it("localizes category codes and compresses same-day ranges without hiding cross-day dates", () => {
  expect(
    compactScheduleText("Đi làm [WORK]: 20/10/2026 13:00 → 20/10/2026 17:00"),
  ).toBe("Đi làm · Công việc: 20/10/2026 · 13:00–17:00");
  expect(compactScheduleText("20/10/2026 23:00 → 21/10/2026 01:00")).toContain(
    "21/10/2026",
  );
});
it("gives generated prompt titles a distinct topic/date label while preserving manual renames", () => {
  const turn = {
    conversationId: "chat",
    text: "Ngày mai lên lịch cho tôi đi học từ 7h đến 12h sáng.",
    changes: [
      {
        kind: "EVENT",
        name: "Đi học",
        after: "20/10/2026 07:00 → 20/10/2026 12:00",
      },
    ],
  };
  expect(conversationLabel({ id: "chat", title: turn.text }, [turn])).toBe(
    "20/10 · Đi học",
  );
  expect(
    conversationLabel({ id: "chat", title: "Lịch học của tôi" }, [turn]),
  ).toBe("Lịch học của tôi");
});

it.each([
  ["America/New_York", "2026-10-19", "21:00"],
  ["Pacific/Auckland", "2026-10-20", "14:00"],
])(
  "uses the refreshed calendar timezone %s instead of the old global timezone",
  (zone, date, start) => {
    const calendar = {
      preference: { timeZoneId: zone },
      tasks: [],
      sessions: [],
      events: [
        {
          id: "cross-zone",
          name: "Cuộc họp",
          start: "2026-10-20T01:00:00Z",
          end: "2026-10-20T02:00:00Z",
        },
      ],
    };
    const entries = timelineForDay(calendar, date);
    expect(entries).toHaveLength(1);
    expect(entries[0].startLabel).toBe(start);
  },
);

it("compares an older request with its own calendar date instead of the last selected day", async () => {
  let selectedDate = "2026-10-20",
    mode = "result";
  const old = {
    id: "old",
    status: "APPLIED",
    text: "Lịch học ngày 15/10",
    changes: [
      {
        kind: "EVENT",
        name: "Đi học",
        after: "15/10/2026 07:00 → 15/10/2026 12:00",
      },
    ],
  };
  render(
    <NoraSchedulePanel
      turn={old}
      view="result"
      date={selectedDate}
      onDateChange={(value) => {
        selectedDate = value;
      }}
      onViewChange={(value) => {
        mode = value;
      }}
      onSuggest={() => {}}
    />,
  );
  await userEvent.click(
    screen.getByRole("button", { name: /Đối chiếu với lịch đã lưu/ }),
  );
  expect(selectedDate).toBe("2026-10-15");
  expect(mode).toBe("day");
});

it("does not suggest lunch or travel intervals that have already passed on the selected day", async () => {
  vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-10-20T13:00:00Z"));
  render(
    <NoraSchedulePanel
      view="day"
      date="2026-10-20"
      onDateChange={() => {}}
      onViewChange={() => {}}
      onSuggest={() => {}}
    />,
  );
  await screen.findByText(/Vừa cập nhật/);
  expect(
    screen.queryByRole("button", { name: "Gợi ý nghỉ trưa 30 phút" }),
  ).toBeNull();
  expect(
    screen.queryByRole("button", { name: /Di chuyển · 30 phút/ }),
  ).toBeNull();
});
