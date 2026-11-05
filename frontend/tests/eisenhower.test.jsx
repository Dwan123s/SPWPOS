import { beforeEach, expect, it, vi } from "vitest";
import { StrictMode } from "react";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import {
  EisenhowerMatrix,
  EisenhowerFocus,
  TaskEisenhowerDetail,
  invalidateEisenhower,
} from "../src/eisenhower.jsx";
import { tasksPage } from "../src/page-tasks.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";
const item = (n, quadrant = "Q1", extra = {}) => ({
  id: `eis-${n}`,
  name: `Công việc ${n}`,
  deadline: new Date(Date.now() + 86400000).toISOString(),
  quadrant,
  importance: "Important",
  urgency: "Urgent",
  remainingMinutes: 60,
  explanation: `Lý do ${n}`,
  kind: "Personal",
  ...extra,
});
let fixtureId = 0;
const response = (tasks) => ({
  tasks,
  evaluatedAt: new Date().toISOString(),
  nextEvaluationAt: new Date(Date.now() + 30000).toISOString(),
  assessmentRevision: "r",
});
beforeEach(() => {
  vi.resetAllMocks();
  state.workspace = workspace();
  state.workspace.features = ["eisenhower-v1"];
  state.workspace.profile.id = `eisenhower-user-${++fixtureId}`;
  invalidateEisenhower();
});
it("keeps the open checklist visible during a background reevaluation of the same revision", async () => {
  const task = item(0, "Q1", {
    version: "v1",
    checklist: [{ id: "step", text: "Bước đang xem", completed: false }],
  });
  let finish;
  api.mockResolvedValueOnce(response([task])).mockImplementationOnce(
    () =>
      new Promise((resolve) => {
        finish = resolve;
      }),
  );
  render(<EisenhowerMatrix />);
  await screen.findByRole("button", { name: "Công việc 0", exact: true });
  fireEvent.click(
    screen.getByRole("button", { name: "Checklist Công việc 0 · 0/1" }),
  );
  act(() => invalidateEisenhower());
  await waitFor(() => expect(api).toHaveBeenCalledTimes(2));
  expect(screen.getByRole("checkbox", { name: "Bước đang xem" })).toBeTruthy();
  expect(
    screen.queryByText("Đang đánh giá công việc từ lịch của bạn…"),
  ).toBeNull();
  await act(async () => finish(response([task])));
  expect(screen.getByRole("checkbox", { name: "Bước đang xem" })).toBeTruthy();
});
it("switches from the existing list to a bounded matrix without requesting AI or mutating tasks", async () => {
  api.mockResolvedValue(
    response(Array.from({ length: 13 }, (_, n) => item(n))),
  );
  render(tasksPage());
  fireEvent.click(
    screen.getByRole("button", { name: "Eisenhower", exact: true }),
  );
  const matrix = await screen.findByRole("region", {
    name: "Ma trận Eisenhower",
  });
  await within(matrix).findByRole("button", { name: "Công việc 0" });
  expect(within(matrix).getAllByRole("article")).toHaveLength(13);
  expect(matrix.querySelectorAll(".eis-grid > div")).toHaveLength(4);
  expect(screen.getByRole("button", { name: "Công việc 4" })).toBeTruthy();
  fireEvent.change(screen.getByRole("textbox", { name: "Tìm trong ma trận" }), {
    target: { value: "Công việc 12" },
  });
  expect(
    await screen.findByRole("button", { name: "Công việc 12" }),
  ).toBeTruthy();
  expect(
    api.mock.calls.every(
      ([url, method]) => ["/api/eisenhower", "/api/notes"].includes(url) && !method,
    ),
  ).toBe(true);
});
it("keeps unknown, pending acceptance and reviews separate, and limits today's recommendations to three", async () => {
  api.mockResolvedValue(
    response([
      item(0, "Unknown"),
      item(1, "Q2", {
        kind: "NeedsAcceptance",
        teamId: "team",
        teamTaskId: "shared",
      }),
      item(2, "Review", {
        kind: "Review",
        teamId: "team",
        teamTaskId: "review",
      }),
      item(3, "Q4"),
      item(4, "Q1", { atRisk: true }),
      item(5, "Q3"),
    ]),
  );
  const { unmount } = render(<EisenhowerMatrix />);
  await screen.findByRole("button", { name: "Công việc 1" });
  expect(screen.queryByRole("region", { name: "Cần làm rõ" })).toBeNull();
  expect(screen.getByText("Chưa nhận")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: /Cần làm rõ/ }));
  await screen.findByRole("button", { name: "Công việc 0" });
  expect(
    within(screen.getByRole("region", { name: "Cần làm rõ" })).getByRole(
      "button",
      { name: "Công việc 0" },
    ),
  ).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: /Chờ bạn duyệt/ }));
  expect(screen.getByRole("region", { name: "Chờ bạn duyệt" })).toBeTruthy();
  expect(screen.getByRole("button", { name: "Công việc 2" })).toBeTruthy();
  unmount();
  render(<EisenhowerFocus />);
  await screen.findByRole("button", { name: "Công việc 4" });
  expect(screen.getAllByRole("article")).toHaveLength(3);
  expect(screen.queryByRole("button", { name: "Công việc 3" })).toBeNull();
  expect(api).toHaveBeenCalledTimes(1);
});
it("does not fetch or display the feature for older clients or disabled accounts", () => {
  state.workspace.features = [];
  render(
    <>
      <EisenhowerFocus />
      <TaskEisenhowerDetail task={{ id: "task" }} />
    </>,
  );
  expect(screen.queryByRole("region")).toBeNull();
  expect(api).not.toHaveBeenCalled();
});
it("shows read errors and supports retry without falling back to fabricated categories", async () => {
  api
    .mockRejectedValueOnce(new Error("Mất kết nối"))
    .mockResolvedValueOnce(response([item(1, "Q2")]));
  render(<EisenhowerMatrix />);
  expect((await screen.findByRole("alert")).textContent).toContain(
    "Mất kết nối",
  );
  fireEvent.click(screen.getByRole("button", { name: "Thử lại" }));
  expect(
    await screen.findByRole("button", { name: "Công việc 1" }),
  ).toBeTruthy();
});
it("saves only an optional personal importance correction with its version and never applies a schedule", async () => {
  api.mockImplementation(async (url) =>
    url.endsWith("/eisenhower") ? item(0, "Unknown") : {},
  );
  const changed = vi.fn();
  render(
    <TaskEisenhowerDetail
      task={{
        id: "task",
        version: "v",
        name: "Proposal",
        importance: { level: "Unknown" },
      }}
      onChanged={changed}
    />,
  );
  fireEvent.click(
    screen.getByRole("button", { name: "Điều chỉnh mức quan trọng" }),
  );
  fireEvent.change(screen.getByRole("combobox", { name: "Mức quan trọng" }), {
    target: { value: "Important" },
  });
  fireEvent.change(screen.getByLabelText("Lý do"), {
    target: { value: "Ảnh hưởng đến kết quả môn học" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Lưu đánh giá" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/tasks/task/importance", "POST", {
      version: "v",
      level: "Important",
      reason: "Ảnh hưởng đến kết quả môn học",
    }),
  );
  expect(changed).toHaveBeenCalled();
  expect(
    api.mock.calls.some(
      ([url]) => url.includes("/apply") || url.includes("/claims/"),
    ),
  ).toBe(false);
});
it("never exposes controls for changing shared importance to a member", () => {
  api.mockResolvedValue(item(0));
  render(
    <TaskEisenhowerDetail
      task={{ id: "shared", name: "Shared" }}
      team
      leader={false}
    />,
  );
  expect(
    screen.queryByRole("button", { name: /Điều chỉnh mức quan trọng/ }),
  ).toBeNull();
  expect(
    api.mock.calls.some(([url]) => url.endsWith("/importance/proposal")),
  ).toBe(false);
});

const proposalTask = {
  id: "proposal-task",
  version: "proposal-v",
  name: "Nghiệm thu proposal",
  importance: { level: "Unknown" },
};
const suggested = {
  version: "proposal-v",
  suggestion: {
    level: "Important",
    reason: "Đầu ra bắt buộc của proposal",
    confidence: 0.7,
    evidence: "Nghiệm thu",
  },
};
function mockProposal(value = suggested) {
  api.mockImplementation(async (url) =>
    url.endsWith("/importance/proposal")
      ? value
      : url.endsWith("/eisenhower")
        ? item(0, "Unknown")
        : {},
  );
}
it("automatically proposes for old unknown tasks but saves only after accepting", async () => {
  mockProposal();
  const changed = vi.fn();
  render(<TaskEisenhowerDetail task={proposalTask} onChanged={changed} />);
  expect(await screen.findByText(suggested.suggestion.reason)).toBeTruthy();
  expect(api).toHaveBeenCalledWith(
    "/api/tasks/proposal-task/importance/proposal",
    "POST",
    { version: "proposal-v" },
  );
  expect(api.mock.calls.some(([url]) => url.endsWith("/importance"))).toBe(
    false,
  );
  fireEvent.click(screen.getByRole("button", { name: "Đồng ý đề xuất" }));
  await waitFor(() => expect(changed).toHaveBeenCalled());
  expect(api).toHaveBeenCalledWith(
    "/api/tasks/proposal-task/importance",
    "POST",
    {
      version: "proposal-v",
      level: "Important",
      reason: suggested.suggestion.reason,
    },
  );
});
it("prefills the AI suggestion for editing and persists the user's correction", async () => {
  mockProposal();
  render(<TaskEisenhowerDetail task={proposalTask} />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Chỉnh sửa", exact: true }),
  );
  expect(screen.getByLabelText("Mức quan trọng").value).toBe("Important");
  expect(screen.getByLabelText("Lý do").value).toBe(
    suggested.suggestion.reason,
  );
  fireEvent.change(screen.getByLabelText("Mức quan trọng"), {
    target: { value: "Lower" },
  });
  fireEvent.change(screen.getByLabelText("Lý do"), {
    target: { value: "Tôi chỉ hỗ trợ tổng hợp" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Lưu đánh giá" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/tasks/proposal-task/importance",
      "POST",
      {
        version: "proposal-v",
        level: "Lower",
        reason: "Tôi chỉ hỗ trợ tổng hợp",
      },
    ),
  );
});
it("keeps confirmed assessments and only reanalyzes on explicit request", async () => {
  mockProposal();
  render(
    <TaskEisenhowerDetail
      task={{
        ...proposalTask,
        importance: {
          level: "Lower",
          reason: "Đã xác nhận",
          source: "User",
          confirmed: true,
        },
      }}
    />,
  );
  expect(
    api.mock.calls.some(([url]) => url.endsWith("/importance/proposal")),
  ).toBe(false);
  fireEvent.click(screen.getByRole("button", { name: "NORA đề xuất lại" }));
  expect(await screen.findByText(suggested.suggestion.reason)).toBeTruthy();
  expect(screen.getByText("Đã xác nhận")).toBeTruthy();
  expect(api.mock.calls.some(([url]) => url.endsWith("/importance"))).toBe(
    false,
  );
});
it("allows retry or manual correction when AI fails", async () => {
  let attempt = 0;
  api.mockImplementation(async (url) => {
    if (!url.endsWith("/importance/proposal")) return item(0);
    if (++attempt === 1) throw new Error("NORA phản hồi quá lâu");
    return suggested;
  });
  render(<TaskEisenhowerDetail task={proposalTask} team leader />);
  expect((await screen.findByRole("alert")).textContent).toContain(
    "NORA phản hồi quá lâu",
  );
  expect(
    screen.getByRole("button", { name: "Điều chỉnh mức quan trọng chung" }),
  ).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Thử lại đề xuất" }));
  expect(await screen.findByText(suggested.suggestion.reason)).toBeTruthy();
  expect(api).toHaveBeenCalledWith(
    "/api/teams/tasks/proposal-task/importance/proposal",
    "POST",
    { version: "proposal-v" },
  );
});
it("ignores a delayed response from a previously opened task", async () => {
  let finish;
  api.mockImplementation(async (url) =>
    url.endsWith("/importance/proposal")
      ? new Promise((resolve) => {
          finish = resolve;
        })
      : item(0),
  );
  const { rerender } = render(<TaskEisenhowerDetail task={proposalTask} />);
  rerender(
    <TaskEisenhowerDetail
      task={{
        id: "other",
        version: "other-v",
        importance: { level: "Lower", confirmed: true },
      }}
    />,
  );
  finish(suggested);
  await waitFor(() =>
    expect(screen.queryByText(suggested.suggestion.reason)).toBeNull(),
  );
  expect(screen.queryByRole("button", { name: "Đồng ý đề xuất" })).toBeNull();
});
it("deduplicates StrictMode AI requests and keeps manual edits when the response arrives", async () => {
  let finish;
  api.mockImplementation(async (url) =>
    url.endsWith("/importance/proposal")
      ? new Promise((resolve) => {
          finish = resolve;
        })
      : item(0),
  );
  render(
    <StrictMode>
      <TaskEisenhowerDetail task={proposalTask} />
    </StrictMode>,
  );
  fireEvent.click(
    screen.getByRole("button", { name: "Điều chỉnh mức quan trọng" }),
  );
  fireEvent.change(screen.getByLabelText("Lý do"), {
    target: { value: "Đánh giá riêng của tôi" },
  });
  finish(suggested);
  await screen.findByText(suggested.suggestion.reason);
  expect(screen.getByLabelText("Lý do").value).toBe("Đánh giá riêng của tôi");
  expect(
    api.mock.calls.filter(([url]) => url.endsWith("/importance/proposal")),
  ).toHaveLength(1);
});
it("does not allow accepting a response with a different task version", async () => {
  mockProposal({ ...suggested, version: "old-v" });
  render(<TaskEisenhowerDetail task={proposalTask} />);
  expect((await screen.findByRole("alert")).textContent).toContain(
    "Hãy tải lại task",
  );
  expect(screen.queryByRole("button", { name: "Đồng ý đề xuất" })).toBeNull();
  expect(api.mock.calls.some(([url]) => url.endsWith("/importance"))).toBe(
    false,
  );
});
