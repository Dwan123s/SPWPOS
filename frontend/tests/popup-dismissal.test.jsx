import { useRef, useState } from "react";
import { beforeEach, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { App } from "../src/App.jsx";
import { ConversationDeleteDialog } from "../src/conversation-delete-dialog.jsx";
import { GettingStarted } from "../src/getting-started.jsx";
import { NoteWritingTools } from "../src/note-writing-tools.jsx";
import { defaultNoteAppearance } from "../src/note-appearance.js";
import { tasksPage } from "../src/page-tasks.jsx";
import { getSnapshot, update, notify } from "../src/store.js";
import { state } from "../src/ui.jsx";

beforeEach(() => {
  state.workspace = { tasks: [], categories: [] };
  state.taskScope = "all";
  update({ screen: null, modal: null, toasts: [], slots: {} });
});

it("toasts can be dismissed and automatically disappear after six seconds", () => {
  vi.useFakeTimers();
  try {
    render(<App initialize={false} />);
    act(() => notify("Đã xóa ghi chú."));
    fireEvent.click(screen.getByRole("button", { name: "Đóng thông báo" }));
    expect(screen.queryByText("Đã xóa ghi chú.")).toBeNull();
    act(() => notify("Đã xóa ghi chú khác."));
    expect(screen.getByText("Đã xóa ghi chú khác.")).toBeTruthy();
    act(() => vi.advanceTimersByTime(6000));
    expect(screen.queryByText("Đã xóa ghi chú khác.")).toBeNull();
  } finally {
    vi.useRealTimers();
  }
});

it("deadline filters keep input values and inline sections when dismissed by an outside click", () => {
  render(
    <>
      {tasksPage()}
      <details open>
        <summary>Inline explanation</summary>Keep reading
      </details>
      <div onClick={(event) => event.stopPropagation()}>Outside</div>
    </>,
  );
  const trigger = screen.getByText("Khoảng deadline");
  fireEvent.click(trigger);
  const date = screen.getByLabelText("Từ ngày");
  fireEvent.change(date, { target: { value: "2026-10-20" } });
  fireEvent.click(date);
  expect(trigger.closest("details").open).toBe(true);
  fireEvent.click(screen.getByText("Outside"));
  expect(trigger.closest("details").open).toBe(false);
  expect(screen.getByText("Inline explanation").closest("details").open).toBe(
    true,
  );
  fireEvent.click(trigger);
  expect(date.value).toBe("2026-10-20");
});

it("note formatting retains a changed setting after outside dismissal and Escape restores its trigger", () => {
  function Editor() {
    const [appearance, setAppearance] = useState(defaultNoteAppearance);
    const ref = useRef(null);
    return (
      <>
        <NoteWritingTools
          appearance={appearance}
          setAppearance={setAppearance}
          typeMenu={ref}
          insertList={() => {}}
        />
        <p>Editor surface</p>
      </>
    );
  }
  render(<Editor />);
  const trigger = screen.getByLabelText("Kiểu chữ và màu giấy");
  fireEvent.click(trigger);
  fireEvent.change(screen.getByLabelText("Cỡ chữ"), {
    target: { value: "24" },
  });
  fireEvent.click(screen.getByText("Phong cách ghi chú"));
  expect(trigger.closest("details").open).toBe(true);
  fireEvent.click(screen.getByText("Editor surface"));
  expect(trigger.closest("details").open).toBe(false);
  fireEvent.click(trigger);
  expect(screen.getByLabelText("Cỡ chữ").value).toBe("24");
  fireEvent.keyDown(screen.getByLabelText("Cỡ chữ"), { key: "Escape" });
  expect(trigger.closest("details").open).toBe(false);
  expect(document.activeElement).toBe(trigger);
});

it("closing the foreground confirmation leaves the underlying app dialog intact and never confirms deletion", () => {
  const cancel = vi.fn(),
    confirm = vi.fn();
  update({
    modal: { title: "Outer editor", subtitle: "", body: <p>Draft content</p> },
  });
  render(
    <>
      <App initialize={false} />
      <ConversationDeleteDialog
        title="Chat"
        onCancel={cancel}
        onConfirm={confirm}
      />
    </>,
  );
  const outer = screen.getByRole("dialog", { name: "Outer editor" });
  const child = screen.getByRole("alertdialog");
  vi.spyOn(child, "getBoundingClientRect").mockReturnValue({
    left: 100,
    right: 500,
    top: 100,
    bottom: 400,
  });
  fireEvent.click(child, { clientX: 20, clientY: 20 });
  expect(cancel).toHaveBeenCalledOnce();
  expect(confirm).not.toHaveBeenCalled();
  expect(outer.open).toBe(true);
  expect(getSnapshot().modal.title).toBe("Outer editor");
});

it("the shared app dialog ignores padding clicks and closes from its backdrop", () => {
  update({
    modal: {
      title: "Edit task",
      subtitle: "",
      body: <input aria-label="Task draft" defaultValue="Retain" />,
    },
  });
  render(<App initialize={false} />);
  const dialog = screen.getByRole("dialog", { name: "Edit task" });
  vi.spyOn(dialog, "getBoundingClientRect").mockReturnValue({
    left: 100,
    right: 500,
    top: 100,
    bottom: 400,
  });
  fireEvent.click(dialog, { clientX: 150, clientY: 150 });
  expect(screen.getByLabelText("Task draft").value).toBe("Retain");
  expect(dialog.open).toBe(true);
  fireEvent.click(dialog, { clientX: 20, clientY: 20 });
  expect(dialog.open).toBe(false);
  expect(getSnapshot().modal).toBeNull();
});

it("a busy delete confirmation stays open when its backdrop is clicked", () => {
  const cancel = vi.fn(),
    confirm = vi.fn();
  render(
    <ConversationDeleteDialog
      title="Chat"
      busy
      onCancel={cancel}
      onConfirm={confirm}
    />,
  );
  fireEvent.click(screen.getByRole("alertdialog"), {
    clientX: -20,
    clientY: -20,
  });
  expect(cancel).not.toHaveBeenCalled();
  expect(confirm).not.toHaveBeenCalled();
});

it("the introductory mobile navigation closes outside and still toggles normally", () => {
  render(<GettingStarted />);
  fireEvent.click(screen.getByRole("button", { name: "Mở menu giới thiệu" }));
  expect(
    screen
      .getByRole("button", { name: "Đóng menu giới thiệu" })
      .getAttribute("aria-expanded"),
  ).toBe("true");
  fireEvent.click(screen.getByRole("heading", { name: "PLANORA", level: 1 }));
  const trigger = screen.getByRole("button", { name: "Mở menu giới thiệu" });
  expect(trigger.getAttribute("aria-expanded")).toBe("false");
  fireEvent.click(trigger);
  fireEvent.keyDown(
    screen.getByRole("button", { name: "Cách hoạt động", exact: true }),
    { key: "Escape" },
  );
  expect(document.activeElement).toBe(trigger);
});
