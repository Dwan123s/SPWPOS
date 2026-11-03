import { beforeEach, expect, it, vi } from "vitest";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { TaskRequirements, TaskSourceFiles } from "../src/team-task-notes.jsx";
vi.mock("../src/api.js", () => ({ downloadPrivateFile: vi.fn() }));
import { downloadPrivateFile } from "../src/api.js";
beforeEach(() => vi.resetAllMocks());

it("presents the full AI brief in readable sections with requirements and acceptance bullets", () => {
  const description =
    "TÓM TẮT\nXây dựng **proposal SSG105**.\n\nYÊU CẦU CẦN THỰC HIỆN\n• Tìm vấn đề thực tế\n• Xác định mục tiêu\n\nĐẦU RA CẦN BÀN GIAO\n• Một file proposal.docx\n\nTIÊU CHÍ HOÀN THÀNH\n• Có dẫn chứng đầy đủ\nGHI CHÚ & PHỐI HỢP\nBàn giao cho trưởng nhóm.\nCĂN CỨ DEADLINE: Thứ Tư tuần sau lúc 21:00\nTÀI LIỆU NGUỒN: Đề bài.docx";
  render(<TaskRequirements task={{ description, splittable: true }} />);
  expect(screen.getByRole("heading", { name: "Tóm tắt" })).toBeTruthy();
  expect(
    screen.getByRole("heading", { name: "Việc cần thực hiện" }),
  ).toBeTruthy();
  expect(screen.getAllByRole("listitem")).toHaveLength(4);
  expect(screen.getByText("proposal SSG105").tagName).toBe("STRONG");
  expect(screen.getByText("Thứ Tư tuần sau lúc 21:00")).toBeTruthy();
  expect(screen.getByText("Đề bài.docx")).toBeTruthy();
  expect(screen.getByText("Bàn giao cho trưởng nhóm.")).toBeTruthy();
});

it("keeps plain notes and custom Markdown sections readable without executing attached markup", () => {
  const { container } = render(
    <TaskRequirements
      task={{
        description:
          'Lưu ý trước khi bắt đầu.\n### Phạm vi bổ sung\n1. Đọc đề bài\n2. Soát bản nháp\n<script>alert("x")</script>\n### Chưa có nội dung',
      }}
    />,
  );
  expect(screen.getByText("Lưu ý trước khi bắt đầu.")).toBeTruthy();
  expect(screen.getByRole("heading", { name: "Phạm vi bổ sung" })).toBeTruthy();
  expect(
    screen.getByRole("heading", { name: "Chưa có nội dung" }),
  ).toBeTruthy();
  expect(screen.getAllByRole("listitem")).toHaveLength(2);
  expect(container.querySelector("script")).toBeNull();
  expect(screen.getByText('<script>alert("x")</script>')).toBeTruthy();
});

it("renders a clear empty state for tasks without notes or a source file", () => {
  render(
    <>
      <TaskRequirements task={{}} />
      <TaskSourceFiles task={{ id: "old" }} />
    </>,
  );
  expect(screen.getByText("Chưa có ghi chú cho công việc này.")).toBeTruthy();
  expect(screen.getByText("Chưa có tài liệu đính kèm.")).toBeTruthy();
  expect(screen.queryByRole("button")).toBeNull();
});

it("downloads the original leader attachment only after an explicit click, separate from member completion files", async () => {
  const task = {
    id: "task",
    sourceFiles: [{ id: "source", name: "Đề bài.docx", size: 12500 }],
    completionFiles: [{ id: "report", name: "Bài nộp.pdf" }],
  };
  render(<TaskSourceFiles task={task} />);
  const panel = screen.getByRole("region", { name: "Tài liệu từ trưởng nhóm" });
  expect(within(panel).getByText("Đề bài.docx")).toBeTruthy();
  expect(screen.queryByText("Bài nộp.pdf")).toBeNull();
  expect(downloadPrivateFile).not.toHaveBeenCalled();
  fireEvent.click(
    within(panel).getByRole("button", { name: "Tải Đề bài.docx" }),
  );
  await waitFor(() =>
    expect(downloadPrivateFile).toHaveBeenCalledWith(
      "/api/teams/tasks/task/source/files/source",
      "Đề bài.docx",
    ),
  );
});

it("retains the source document and permits retry after a failed download", async () => {
  downloadPrivateFile.mockRejectedValueOnce(new Error("Kết nối bị gián đoạn"));
  render(
    <TaskSourceFiles
      task={{
        id: "task",
        sourceFiles: [{ id: "file", name: "brief.pdf", size: 1024 }],
      }}
    />,
  );
  fireEvent.click(screen.getByRole("button", { name: "Tải brief.pdf" }));
  expect((await screen.findByRole("alert")).textContent).toContain(
    "Kết nối bị gián đoạn",
  );
  expect(screen.getByText("brief.pdf")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Tải brief.pdf" }).disabled).toBe(
    false,
  );
  fireEvent.click(screen.getByRole("button", { name: "Tải brief.pdf" }));
  await waitFor(() => expect(downloadPrivateFile).toHaveBeenCalledTimes(2));
});

it("lets only leaders attach a missing file to an existing task, retaining the retry request after a network error", async () => {
  const onAttach = vi
    .fn()
    .mockResolvedValueOnce(false)
    .mockResolvedValueOnce(true);
  const { rerender } = render(
    <TaskSourceFiles
      task={{ id: "task" }}
      leader={false}
      onAttach={onAttach}
    />,
  );
  expect(
    screen.queryByRole("button", { name: "Đính kèm tài liệu" }),
  ).toBeNull();
  rerender(
    <TaskSourceFiles task={{ id: "task" }} leader onAttach={onAttach} />,
  );
  fireEvent.change(screen.getByLabelText("Chọn tài liệu của trưởng nhóm"), {
    target: {
      files: [new File(["Yêu cầu dự án"], "brief.txt", { type: "text/plain" })],
    },
  });
  const send = await screen.findByRole("button", { name: "Gửi tài liệu" });
  expect(onAttach).not.toHaveBeenCalled();
  fireEvent.click(send);
  await waitFor(() => expect(onAttach).toHaveBeenCalledTimes(1));
  fireEvent.click(screen.getByRole("button", { name: "Gửi tài liệu" }));
  await waitFor(() => expect(onAttach).toHaveBeenCalledTimes(2));
  expect(onAttach.mock.calls[0][0].requestId).toBe(
    onAttach.mock.calls[1][0].requestId,
  );
  await waitFor(() =>
    expect(screen.queryByRole("button", { name: "Gửi tài liệu" })).toBeNull(),
  );
  rerender(
    <TaskSourceFiles
      task={{
        id: "task",
        sourceFiles: [{ id: "existing", name: "brief.txt", size: 20 }],
      }}
      leader
      onAttach={onAttach}
    />,
  );
  expect(
    screen.queryByRole("button", { name: "Đính kèm tài liệu" }),
  ).toBeNull();
});
