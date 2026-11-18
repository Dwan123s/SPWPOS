import { expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { TeamManualTaskForm } from "../src/team-manual-task-form.jsx";
import { manualTaskContent, manualTaskDescription } from "../src/team-manual-task-content.js";

const task = {
  name: "Kiểm tra báo cáo", estimatedMinutes: 60,
  deadline: "2027-01-10T12:30:00Z", priority: 2, splittable: true,
};

it("adds, edits and removes checklist rows, and Enter adds a step without submitting", () => {
  const onPreview = vi.fn(), onEdit = vi.fn();
  render(<TeamManualTaskForm task={task} onEdit={onEdit} onPreview={onPreview} />);
  fireEvent.change(screen.getByLabelText(/Mô tả/), { target: { value: "Bàn giao bản cuối." } });
  fireEvent.click(screen.getByRole("button", { name: "Thêm mục checklist" }));
  const first = screen.getByRole("textbox", { name: "Nội dung checklist 1" });
  expect(document.activeElement).toBe(first);
  fireEvent.change(first, { target: { value: "  Đọc yêu cầu  " } });
  fireEvent.keyDown(first, { key: "Enter" });
  expect(onPreview).not.toHaveBeenCalled();
  fireEvent.change(screen.getByRole("textbox", { name: "Nội dung checklist 2" }), { target: { value: "Mục bỏ đi" } });
  fireEvent.click(screen.getByRole("button", { name: "Xóa mục checklist 2" }));
  fireEvent.click(screen.getByRole("button", { name: "Thêm mục checklist" }));
  fireEvent.change(screen.getByRole("textbox", { name: "Nội dung checklist 2" }), { target: { value: "Soát số liệu" } });
  fireEvent.click(screen.getByRole("button", { name: "Thêm mục checklist" }));
  fireEvent.click(screen.getByRole("button", { name: "Xem trước thay đổi" }));
  expect(onPreview).toHaveBeenCalledWith(expect.objectContaining({
    description: "TÓM TẮT\nBàn giao bản cuối.\n\nYÊU CẦU CẦN THỰC HIỆN\n- Đọc yêu cầu\n- Soát số liệu",
  }));
  expect(onEdit).toHaveBeenCalled();
});

it("restores an existing checklist and preserves the other brief sections when edited", () => {
  const onPreview = vi.fn();
  render(<TeamManualTaskForm task={{ ...task, description:
    "TÓM TẮT\nBáo cáo tháng.\n\nVIỆC CẦN THỰC HIỆN\n- Đọc yêu cầu\n- Soát số liệu\n\nĐẦU RA\nreport.pdf\n\n### Tham khảo riêng\nGiữ nguyên thông tin này.",
    checklist: [{ id: "a", text: "Đọc yêu cầu", completed: true }, { id: "b", text: "Soát số liệu" }],
  }} onEdit={vi.fn()} onPreview={onPreview} />);
  expect(screen.getByLabelText(/Mô tả/).value).not.toContain("Soát số liệu");
  expect(screen.getByRole("textbox", { name: "Nội dung checklist 1" }).value).toBe("Đọc yêu cầu");
  fireEvent.click(screen.getByRole("button", { name: "Xóa mục checklist 2" }));
  fireEvent.click(screen.getByRole("button", { name: "Xem trước thay đổi" }));
  const { description } = onPreview.mock.calls[0][0];
  expect(description).toContain("Báo cáo tháng.");
  expect(description).toContain("report.pdf");
  expect(description).toContain("## Tham khảo riêng\nGiữ nguyên thông tin này.");
  expect(description.match(/Đọc yêu cầu/g)).toHaveLength(1);
  expect(description).not.toContain("Soát số liệu");
});

it("keeps an empty explicit checklist after deleting all steps and leaves ordinary descriptions unchanged", () => {
  const content = manualTaskContent({ description: "Tóm tắt\nGiữ mô tả\nYêu cầu\n- Bỏ mục này" });
  expect(manualTaskDescription(content.description, [], content.hasChecklist)).toBe("## Tóm tắt\nGiữ mô tả\n\nYÊU CẦU CẦN THỰC HIỆN\n");
  expect(manualTaskDescription("Mô tả nguyên bản", [], false)).toBe("Mô tả nguyên bản");
});

it("reports the combined content limit before sending the draft and locks rows while busy", () => {
  const onPreview = vi.fn();
  const props = { task, onEdit: vi.fn(), onPreview };
  const { rerender } = render(<TeamManualTaskForm {...props} />);
  fireEvent.change(screen.getByLabelText(/Mô tả/), { target: { value: "a".repeat(3990) } });
  fireEvent.click(screen.getByRole("button", { name: "Thêm mục checklist" }));
  fireEvent.change(screen.getByRole("textbox", { name: "Nội dung checklist 1" }), { target: { value: "Soát số liệu" } });
  fireEvent.click(screen.getByRole("button", { name: "Xem trước thay đổi" }));
  expect(screen.getByRole("alert").textContent).toContain("4.000 ký tự");
  expect(onPreview).not.toHaveBeenCalled();
  rerender(<TeamManualTaskForm {...props} busy />);
  expect(screen.getByRole("textbox", { name: "Nội dung checklist 1" }).matches(":disabled")).toBe(true);
  expect(screen.getByRole("button", { name: "Thêm mục checklist" }).matches(":disabled")).toBe(true);
});
