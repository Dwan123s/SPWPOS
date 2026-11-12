import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { TeamsPage } from "../src/teams.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
vi.mock("../src/api.js", () => ({ api: vi.fn() }));
import { api } from "../src/api.js";

const team = { id: "brief-team", name: "Nhóm đề tài", role: "Leader" };
const task = {
  id: "task-one",
  name: "Thiết kế hệ thống",
  description:
    "TÓM TẮT\nThiết kế dữ liệu\nĐẦU RA CẦN BÀN GIAO\nERD và mô tả API",
  estimatedMinutes: 120,
  deadline: "2026-11-04T14:00:00Z",
  status: "Open",
  version: "v",
};
const draft = {
  proposalId: "brief",
  summary: "Phát triển ứng dụng",
  splitReason: "Hai gói việc cho hai thành viên",
  memberCount: 2,
  tasks: [
    task,
    {
      ...task,
      id: "task-two",
      name: "Kiểm thử hệ thống",
      description:
        "TÓM TẮT\nKiểm thử\nTIÊU CHÍ HOÀN THÀNH\nBáo cáo kiểm thử đầy đủ",
    },
  ],
  question: "",
  warnings: ["Thời lượng cần trưởng nhóm xác nhận."],
};
beforeEach(() => {
  vi.resetAllMocks();
  state.workspace = workspace();
  api.mockImplementation(async (path) => {
    if (path === "/api/teams") return [team];
    if (path === "/api/teams/brief-team")
      return {
        team,
        members: [
          { userId: "one", name: "An", role: "Leader" },
          { userId: "two", name: "Bình", role: "Member" },
        ],
        tasks: [],
        activity: [],
      };
    if (path === "/api/teams/brief-team/briefs") return draft;
    return {};
  });
});
async function open() {
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: team.name }));
  fireEvent.click(await screen.findByRole("button", { name: "Giao việc mới" }));
}
it("attaches a document, automatically splits for the current team and requires leader confirmation before publishing", async () => {
  await open();
  const file = new File(["Yêu cầu dự án hạn ngày mai"], "project.txt", {
    type: "text/plain",
  });
  fireEvent.change(screen.getByLabelText("Tải tài liệu công việc"), {
    target: { files: [file] },
  });
  expect(await screen.findByText("project.txt")).toBeTruthy();
  expect(screen.getByText(/Nhóm hiện có 2 thành viên/)).toBeTruthy();
  fireEvent.click(
    screen.getByRole("button", { name: "NORA phân tích & chia việc" }),
  );
  expect(await screen.findByText("01. Thiết kế hệ thống")).toBeTruthy();
  expect(screen.getByText("02. Kiểm thử hệ thống")).toBeTruthy();
  expect(screen.getByText(/ERD và mô tả API/)).toBeTruthy();
  expect(api.mock.calls.some(([p]) => p.includes("/confirm"))).toBe(false);
  const request = api.mock.calls.find(([p]) => p.endsWith("/briefs"));
  expect(request[1]).toBe("POST");
  expect(request[2].file.name).toBe("project.txt");
  expect(request[2].file.data.length).toBeGreaterThan(0);
  fireEvent.click(
    screen.getByRole("button", { name: "Xác nhận & giao 2 task cho nhóm" }),
  );
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith(
      "/api/teams/brief-team/briefs/brief/confirm",
      "POST",
      undefined,
    ),
  );
});
it("retains the document when NORA needs a deadline and accepts clarification in the text box", async () => {
  api.mockImplementation(async (path) => {
    if (path === "/api/teams") return [team];
    if (path === "/api/teams/brief-team")
      return {
        team,
        members: [
          { userId: "one", name: "An", role: "Leader" },
          { userId: "two", name: "Bình", role: "Member" },
        ],
        tasks: [],
        activity: [],
      };
    if (path.endsWith("/briefs"))
      return {
        ...draft,
        proposalId: "",
        question: "Deadline cuối cùng là khi nào?",
        tasks: [],
      };
    return {};
  });
  await open();
  fireEvent.change(screen.getByLabelText("Tải tài liệu công việc"), {
    target: { files: [new File(["Yêu cầu"], "brief.md")] },
  });
  await screen.findByText("brief.md");
  fireEvent.click(
    screen.getByRole("button", { name: "NORA phân tích & chia việc" }),
  );
  expect(await screen.findByText(/Deadline cuối cùng là khi nào/)).toBeTruthy();
  expect(screen.queryByRole("button", { name: /Xác nhận & giao/ })).toBeNull();
  fireEvent.change(screen.getByRole("textbox", { name: /Bổ sung yêu cầu/ }), {
    target: { value: "Hạn 21h ngày mai" },
  });
  expect(screen.getByText("brief.md")).toBeTruthy();
  fireEvent.click(
    screen.getByRole("button", { name: "NORA phân tích & chia việc" }),
  );
  await waitFor(() =>
    expect(
      api.mock.calls.filter(([p]) => p.endsWith("/briefs")).at(-1)[2].text,
    ).toBe("Hạn 21h ngày mai"),
  );
});
it("rejects unsupported files locally without calling AI", async () => {
  await open();
  fireEvent.change(screen.getByLabelText("Tải tài liệu công việc"), {
    target: { files: [new File(["bad"], "program.exe")] },
  });
  expect(await screen.findByRole("alert")).toHaveProperty(
    "textContent",
    "Hỗ trợ PDF, Word (.docx), TXT và Markdown (.md).",
  );
  expect(api.mock.calls.some(([p]) => p.endsWith("/briefs"))).toBe(false);
});
it("lets the leader choose 1 through the member count and sends that choice with the brief", async () => {
  await open();
  fireEvent.click(
    screen.getByRole("button", { name: "Chia việc cho cả nhóm" }),
  );
  const select = screen.getByRole("combobox", {
    name: "Số gói việc NORA sẽ chia",
  });
  expect([...select.options].map((option) => option.value)).toEqual(["1", "2"]);
  expect(select.value).toBe("2");
  fireEvent.change(select, { target: { value: "1" } });
  expect(screen.getByText(/chia thành 1 gói việc/)).toBeTruthy();
  fireEvent.change(
    screen.getByRole("textbox", { name: "Mô tả công việc và hạn chót" }),
    { target: { value: "Toàn bộ dự án, hạn ngày mai" } },
  );
  fireEvent.click(
    screen.getByRole("button", { name: "NORA phân tích & chia việc" }),
  );
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/teams/brief-team/briefs", "POST", {
      text: "Toàn bộ dự án, hạn ngày mai",
      taskCount: 1,
      file: null,
    }),
  );
  fireEvent.change(select, { target: { value: "2" } });
  expect(screen.queryByText("01. Thiết kế hệ thống")).toBeNull();
  expect(
    screen.getByRole("textbox", { name: "Mô tả công việc và hạn chót" }).value,
  ).toBe("Toàn bộ dự án, hạn ngày mai");
});
it("keeps single-task text entry available", async () => {
  await open();
  fireEvent.change(
    screen.getByRole("textbox", { name: "Mô tả công việc và hạn chót" }),
    { target: { value: "Viết báo cáo hạn ngày mai" } },
  );
  fireEvent.click(screen.getByRole("button", { name: "NORA soạn bản nháp" }));
  await waitFor(() =>
    expect(api).toHaveBeenCalledWith("/api/teams/brief-team/drafts", "POST", {
      text: "Viết báo cáo hạn ngày mai",
    }),
  );
});
