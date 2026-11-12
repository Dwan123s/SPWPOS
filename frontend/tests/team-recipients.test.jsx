import { beforeEach, expect, it, vi } from "vitest";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { TaskBoard } from "../src/team-task-board.jsx";
import { TeamsPage } from "../src/teams.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";

const members = [
  { userId: "me", name: "An", role: "Leader" },
  { userId: "other", name: "Bình", role: "Member" },
];
const task = {
  id: "task",
  name: "Proposal SSG105",
  description: "Hoàn thiện proposal",
  status: "Open",
  estimatedMinutes: 60,
  priority: 2,
  deadline: "2026-11-04T14:00:00Z",
  version: "v",
  progress: 0,
};
beforeEach(() => {
  vi.resetAllMocks();
  state.workspace = workspace();
});

it("only the designated person sees claim actions and the task appears in their personal and available filters", () => {
  const detail = {
    team: { id: "team", role: "Member" },
    members,
    tasks: [{ ...task, designatedUserId: "other" }],
  };
  const { rerender } = render(<TaskBoard detail={detail} uid="me" />);
  expect(
    screen.queryByRole("button", { name: "Xem lịch trước khi nhận" }),
  ).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: task.name }));
  const popup = screen.getByRole("dialog", { name: task.name });
  expect(
    within(popup).queryByRole("button", { name: "Xem lịch trước khi nhận" }),
  ).toBeNull();
  expect(
    within(popup).getByText(/Task được giao đích danh cho Bình/),
  ).toBeTruthy();
  fireEvent.click(within(popup).getByRole("button", { name: "Đóng" }));
  fireEvent.click(screen.getByRole("button", { name: "Có thể nhận" }));
  expect(screen.queryByRole("article")).toBeNull();
  rerender(<TaskBoard detail={detail} uid="other" />);
  expect(
    screen.getByRole("button", { name: "Xem lịch trước khi nhận" }),
  ).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Việc của tôi" }));
  expect(screen.getByRole("article", { name: task.name })).toBeTruthy();
});

it("lets a leader release an unclaimed task from its popup while keeping the current version", () => {
  const onRecipientChange = vi.fn();
  const current = { ...task, designatedUserId: "other" };
  render(
    <TaskBoard
      detail={{ team: { role: "Leader" }, members, tasks: [current] }}
      uid="me"
      onRecipientChange={onRecipientChange}
    />,
  );
  fireEvent.click(screen.getByRole("button", { name: task.name }));
  const popup = screen.getByRole("dialog");
  fireEvent.change(within(popup).getByRole("combobox"), {
    target: { value: "" },
  });
  fireEvent.click(
    within(popup).getByRole("button", { name: "Lưu người nhận" }),
  );
  expect(onRecipientChange).toHaveBeenCalledWith(current, null);
});

it("sends a named recipient only after confirming a single AI task draft", async () => {
  const team = { id: "team", name: "Nhóm test", role: "Leader" };
  api.mockImplementation(async (url) => {
    if (url === "/api/teams") return [team];
    if (url === "/api/teams/team")
      return { team, members, tasks: [], activity: [] };
    if (url === "/api/teams/team/drafts")
      return { proposalId: "draft", task, question: "", reply: "Bản nháp" };
    return {};
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Đăng việc mới" }));
  fireEvent.change(
    screen.getByRole("textbox", { name: "Mô tả công việc và hạn chót" }),
    { target: { value: "Proposal hạn thứ Tư" } },
  );
  fireEvent.click(screen.getByRole("button", { name: "NORA soạn bản nháp" }));
  fireEvent.change(
    await screen.findByRole("combobox", { name: "Người nhận task" }),
    { target: { value: "other" } },
  );
  expect(api.mock.calls.some(([url]) => url.endsWith("/confirm"))).toBe(false);
  fireEvent.click(
    screen.getByRole("button", { name: "Đồng ý thời lượng và giao task" }),
  );
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/teams/team/drafts/draft/confirm",
      "POST",
      { userId: "other" },
    ),
  );
  expect(api.mock.calls.some(([url]) => url.includes("/preview"))).toBe(false);
});

it("can select different recipients for separate tasks from a file brief, retaining shared tasks as the default", async () => {
  const team = { id: "team", name: "Nhóm file", role: "Leader" };
  const second = { ...task, id: "second", name: "Tổng hợp tài liệu" };
  api.mockImplementation(async (url) => {
    if (url === "/api/teams") return [team];
    if (url === "/api/teams/team")
      return { team, members, tasks: [], activity: [] };
    if (url === "/api/teams/team/briefs")
      return {
        proposalId: "brief",
        tasks: [task, second],
        question: "",
        memberCount: 2,
        warnings: [],
      };
    return {};
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Đăng việc mới" }));
  fireEvent.click(
    screen.getByRole("button", { name: "Chia việc cho cả nhóm" }),
  );
  fireEvent.change(
    screen.getByRole("textbox", { name: "Mô tả công việc và hạn chót" }),
    { target: { value: "Proposal, hạn thứ Tư" } },
  );
  fireEvent.click(
    screen.getByRole("button", { name: "NORA phân tích & chia việc" }),
  );
  const first = await screen.findByRole("combobox", {
    name: `Người nhận task 1: ${task.name}`,
  });
  expect(first.value).toBe("");
  fireEvent.change(first, { target: { value: "other" } });
  fireEvent.change(
    screen.getByRole("combobox", { name: `Người nhận task 2: ${second.name}` }),
    { target: { value: "me" } },
  );
  fireEvent.click(
    screen.getByRole("button", { name: "Xác nhận & giao 2 task cho nhóm" }),
  );
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/teams/team/briefs/brief/confirm",
      "POST",
      {
        recipients: [
          { taskId: "task", userId: "other" },
          { taskId: "second", userId: "me" },
        ],
      },
    ),
  );
});
it("keeps all work packages visible while the leader clarifies a common deadline", async () => {
  const team = { id: "team", name: "Nhóm proposal", role: "Leader" };
  const second = {
    ...task,
    id: "second",
    name: "Tổng hợp proposal",
    deadline: null,
    deadlineNeedsConfirmation: true,
  };
  let attempt = 0;
  api.mockImplementation(async (url) => {
    if (url === "/api/teams") return [team];
    if (url === "/api/teams/team")
      return { team, members, tasks: [], activity: [] };
    if (url === "/api/teams/team/briefs")
      return ++attempt === 1
        ? {
            proposalId: "00000000-0000-0000-0000-000000000000",
            tasks: [],
            pendingTasks: [
              { ...task, deadline: null, deadlineNeedsConfirmation: true },
              second,
            ],
            question: "Bạn xác nhận giờ nộp ngày 04/11/2026 nhé?",
            memberCount: 2,
            warnings: [],
          }
        : {
            proposalId: "ready",
            tasks: [task, { ...second, deadline: task.deadline }],
            question: "",
            memberCount: 2,
            warnings: [],
          };
    return {};
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Đăng việc mới" }));
  fireEvent.click(
    screen.getByRole("button", { name: "Chia việc cho cả nhóm" }),
  );
  const input = screen.getByRole("textbox", {
    name: "Mô tả công việc và hạn chót",
  });
  fireEvent.change(input, {
    target: { value: "Xây dựng proposal SSG105, nộp 04/11/2026." },
  });
  fireEvent.click(
    screen.getByRole("button", { name: "NORA phân tích & chia việc" }),
  );
  expect(await screen.findByText("01. " + task.name)).toBeTruthy();
  expect(screen.getByText("02. " + second.name)).toBeTruthy();
  expect(screen.getAllByText("Hạn chót chờ xác nhận")).toHaveLength(2);
  expect(
    screen.queryByRole("button", { name: "Xác nhận & giao 2 task cho nhóm" }),
  ).toBeNull();
  fireEvent.change(input, {
    target: {
      value:
        "Xây dựng proposal SSG105. Xác nhận giờ nộp chung 21:00 ngày 04/11/2026.",
    },
  });
  fireEvent.click(
    screen.getByRole("button", { name: "NORA phân tích & chia việc" }),
  );
  expect(
    await screen.findByRole("button", {
      name: "Xác nhận & giao 2 task cho nhóm",
    }),
  ).toBeTruthy();
  expect(api.mock.calls.some(([url]) => url.endsWith("/confirm"))).toBe(false);
});

const editableId = "11111111-1111-1111-1111-111111111111";
const editedRevision = "22222222-2222-2222-2222-222222222222";
async function openEditableBrief({ pending = false } = {}) {
  const team = { id: "team", name: "Nhóm chỉnh bản nháp", role: "Leader" };
  const first = {
    ...task,
    description:
      "TÓM TẮT\nTóm tắt ban đầu\n\nYÊU CẦU CẦN THỰC HIỆN\n• Yêu cầu ban đầu\n\nĐẦU RA CẦN BÀN GIAO\n• Báo cáo\n\nTIÊU CHÍ HOÀN THÀNH\n• Leader duyệt\n\nTÀI LIỆU NGUỒN: proposal.docx",
  };
  const second = { ...first, id: "second", name: "Tổng hợp tài liệu" };
  const base = {
    proposalId: editableId,
    revision: "00000000-0000-0000-0000-000000000000",
    memberCount: 2,
    warnings: [],
    tasks: pending ? [] : [first, second],
    pendingTasks: pending
      ? [first, second].map((t) => ({
          ...t,
          deadline: null,
          deadlineNeedsConfirmation: true,
        }))
      : null,
    question: pending ? "Chốt giờ nộp nhé?" : "",
  };
  api.mockImplementation(async (url, method, body) => {
    if (url === "/api/teams") return [team];
    if (url === "/api/teams/team")
      return { team, members, tasks: [], activity: [] };
    if (url === "/api/teams/team/briefs") return base;
    if (url.endsWith("/edit"))
      return {
        ...base,
        revision: editedRevision,
        tasks: body.tasks,
        pendingTasks: null,
        question: "",
      };
    return {};
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Đăng việc mới" }));
  fireEvent.click(
    screen.getByRole("button", { name: "Chia việc cho cả nhóm" }),
  );
  fireEvent.change(
    screen.getByRole("textbox", { name: "Mô tả công việc và hạn chót" }),
    { target: { value: "Xây dựng proposal" } },
  );
  fireEvent.click(
    screen.getByRole("button", { name: "NORA phân tích & chia việc" }),
  );
  await screen.findByText("01. " + first.name);
  return { first, second };
}
it("lets the leader set a common deadline on pending packages without rerunning AI, then confirms the edited revision", async () => {
  await openEditableBrief({ pending: true });
  fireEvent.change(screen.getByLabelText("Hạn chót chung"), {
    target: { value: "2026-11-04T21:00" },
  });
  fireEvent.click(
    screen.getByRole("button", { name: "Áp dụng hạn chót chung" }),
  );
  await screen.findByRole("button", {
    name: "Xác nhận & giao 2 task cho nhóm",
  });
  const edit = api.mock.calls.find(([url]) => url.endsWith("/edit"));
  expect(edit[2].tasks).toHaveLength(2);
  expect(edit[2].tasks[0].deadline).toBe(edit[2].tasks[1].deadline);
  expect(
    api.mock.calls.filter(([url]) => url === "/api/teams/team/briefs"),
  ).toHaveLength(1);
  expect(api.mock.calls.some(([url]) => url.endsWith("/confirm"))).toBe(false);
  fireEvent.click(
    screen.getByRole("button", { name: "Xác nhận & giao 2 task cho nhóm" }),
  );
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      `/api/teams/team/briefs/${editableId}/confirm`,
      "POST",
      { recipients: [], revision: editedRevision },
    ),
  );
});
it("edits summary, requirements and duration, keeps the other package and source, and blocks publishing unsaved edits", async () => {
  const { first, second } = await openEditableBrief();
  fireEvent.click(
    screen.getAllByRole("button", { name: "Chỉnh sửa gói việc" })[0],
  );
  expect(
    screen.getByRole("button", { name: "Xác nhận & giao 2 task cho nhóm" })
      .disabled,
  ).toBe(true);
  expect(screen.getByLabelText("Tóm tắt").value).toBe("Tóm tắt ban đầu");
  fireEvent.change(screen.getByLabelText("Tóm tắt"), {
    target: { value: "Mục tiêu leader cập nhật" },
  });
  fireEvent.change(screen.getByLabelText("Việc cần thực hiện"), {
    target: { value: "• Yêu cầu đã chỉnh" },
  });
  fireEvent.change(screen.getByLabelText("Thời lượng (phút)"), {
    target: { value: "120" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Lưu chỉnh sửa" }));
  await waitFor(() =>
    expect(api.mock.calls.some(([url]) => url.endsWith("/edit"))).toBe(true),
  );
  const saved = api.mock.calls.find(([url]) => url.endsWith("/edit"))[2].tasks;
  expect(saved[0].id).toBe(first.id);
  expect(saved[0].estimatedMinutes).toBe(120);
  expect(saved[0].description).toContain("Mục tiêu leader cập nhật");
  expect(saved[0].description).toContain("Yêu cầu đã chỉnh");
  expect(saved[0].description).toContain("proposal.docx");
  expect(saved[1].description).toBe(second.description);
  expect(api.mock.calls.some(([url]) => url.endsWith("/confirm"))).toBe(false);
});
it("cancels editing without sending changes", async () => {
  await openEditableBrief();
  fireEvent.click(
    screen.getAllByRole("button", { name: "Chỉnh sửa gói việc" })[0],
  );
  fireEvent.change(screen.getByLabelText("Tóm tắt"), {
    target: { value: "Chưa lưu" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Hủy chỉnh sửa" }));
  expect(api.mock.calls.some(([url]) => url.endsWith("/edit"))).toBe(false);
  expect(
    screen.getByRole("button", { name: "Xác nhận & giao 2 task cho nhóm" })
      .disabled,
  ).toBe(false);
});
