import { beforeEach, expect, it, vi } from "vitest";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { TeamsPage } from "../src/teams.jsx";
import { state } from "../src/ui.jsx";
import { workspace } from "./fixtures.js";
vi.mock("../src/api.js", () => ({
  api: vi.fn(),
  downloadPrivateFile: vi.fn().mockResolvedValue(undefined),
}));
import { api, downloadPrivateFile } from "../src/api.js";
const group = { id: "team", name: "Nhóm báo cáo", role: "Member" };
const shared = {
  id: "shared",
  name: "Proposal SSG105",
  estimatedMinutes: 120,
  deadline: "2026-11-04T14:00:00Z",
  status: "Assigned",
  version: "v",
  progress: 50,
};
beforeEach(() => {
  vi.clearAllMocks();
  state.workspace = workspace();
  api.mockImplementation(async (path) => {
    if (path === "/api/teams") return [group];
    if (path === "/api/teams/team")
      return {
        team: group,
        members: [],
        tasks: [{ ...shared, assigneeId: state.workspace.profile.id }],
        activity: [],
      };
    return {};
  });
});
async function submitDialog() {
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: group.name }));
  fireEvent.click(
    await screen.findByRole("button", { name: "Báo hoàn thành · Gửi duyệt" }),
  );
  return screen.getByRole("dialog", { name: "Gửi xác nhận hoàn thành" });
}
it("sends optional report files and notes only when member confirms, and keeps request id on retry", async () => {
  let attempts = 0;
  const routes = api.getMockImplementation();
  api.mockImplementation(async (path, ...args) => {
    if (path.endsWith("/completion/submit") && ++attempts === 1)
      throw new Error("Mất kết nối. Thử lại.");
    return routes(path, ...args);
  });
  const dialog = await submitDialog();
  fireEvent.change(within(dialog).getByLabelText("Chọn file báo cáo"), {
    target: {
      files: [
        new File(["Nội dung báo cáo"], "proposal.txt", { type: "text/plain" }),
      ],
    },
  });
  expect(await within(dialog).findByText("proposal.txt")).toBeTruthy();
  fireEvent.change(
    within(dialog).getByRole("textbox", {
      name: "Ghi chú kết quả (không bắt buộc)",
    }),
    { target: { value: "Đã bổ sung mục tiêu và kế hoạch" } },
  );
  expect(api.mock.calls.some(([p]) => p.endsWith("/completion/submit"))).toBe(
    false,
  );
  fireEvent.click(
    within(dialog).getByRole("button", { name: "Xác nhận & gửi duyệt" }),
  );
  expect(await within(dialog).findByRole("alert")).toHaveProperty(
    "textContent",
    "Mất kết nối. Thử lại.",
  );
  expect(within(dialog).getByText("proposal.txt")).toBeTruthy();
  fireEvent.click(
    within(dialog).getByRole("button", { name: "Xác nhận & gửi duyệt" }),
  );
  await waitFor(() =>
    expect(
      api.mock.calls.filter(([p]) => p.endsWith("/completion/submit")),
    ).toHaveLength(2),
  );
  const [first, second] = api.mock.calls.filter(([p]) =>
    p.endsWith("/completion/submit"),
  );
  expect(first[2]).toEqual(second[2]);
  expect(first[2].note).toBe("Đã bổ sung mục tiêu và kế hoạch");
  expect(first[2].files).toEqual([
    { name: "proposal.txt", data: expect.any(String) },
  ]);
});
it("allows removing an attachment and rejects unsupported files before upload", async () => {
  const dialog = await submitDialog();
  const upload = within(dialog).getByLabelText("Chọn file báo cáo");
  fireEvent.change(upload, {
    target: { files: [new File(["test"], "bad.exe")] },
  });
  expect(await within(dialog).findByRole("alert")).toHaveProperty(
    "textContent",
    "Hỗ trợ PDF, Word, Excel, PowerPoint, TXT, Markdown, CSV, ZIP và ảnh PNG, JPG, WebP.",
  );
  fireEvent.change(upload, {
    target: { files: [new File(["test"], "report.txt")] },
  });
  expect(await within(dialog).findByText("report.txt")).toBeTruthy();
  fireEvent.click(
    within(dialog).getByRole("button", { name: "Bỏ file report.txt" }),
  );
  expect(within(dialog).queryByText("report.txt")).toBeNull();
});
it("leader sees the report note and can download the submitted attachment from the review dialog", async () => {
  api.mockImplementation(async (path) => {
    if (path === "/api/teams") return [{ ...group, role: "Leader" }];
    if (path === "/api/teams/team")
      return {
        team: { ...group, role: "Leader" },
        members: [{ userId: "member", name: "An", role: "Member" }],
        activity: [],
        tasks: [
          {
            ...shared,
            assigneeId: "member",
            status: "PendingReview",
            completionRequestId: "request",
            completionSubmissionNote: "Bản cuối có đầy đủ nguồn",
            completionFiles: [
              {
                id: "file",
                name: "Bao-cao.docx",
                mimeType:
                  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                size: 2000,
              },
            ],
          },
        ],
      };
    return {};
  });
  render(<TeamsPage />);
  fireEvent.click(await screen.findByRole("button", { name: group.name }));
  fireEvent.click(
    await screen.findByRole("button", { name: "Kiểm tra & duyệt" }),
  );
  const dialog = screen.getByRole("dialog", { name: "Duyệt hoàn thành task" });
  expect(within(dialog).getByText("Bản cuối có đầy đủ nguồn")).toBeTruthy();
  expect(within(dialog).getByText("Bao-cao.docx")).toBeTruthy();
  fireEvent.click(within(dialog).getByRole("button", { name: "Tải file" }));
  await waitFor(() =>
    expect(downloadPrivateFile).toHaveBeenCalledWith(
      "/api/teams/tasks/shared/completion/files/file",
      "Bao-cao.docx",
    ),
  );
  expect(api.mock.calls.some(([p]) => p.endsWith("/completion/review"))).toBe(
    false,
  );
});
