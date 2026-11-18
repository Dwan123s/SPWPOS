import { expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QuickCreateMenu } from "../src/quick-create-menu.jsx";

it("opens separate task and event choices and dismisses after selection", async () => {
  render(<QuickCreateMenu />);
  const user = userEvent.setup();
  const trigger = screen.getByRole("button", { name: "Tạo mới" });
  expect(trigger.getAttribute("aria-expanded")).toBe("false");
  expect(trigger.hasAttribute("data-action")).toBe(false);
  await user.click(trigger);
  const task = screen.getByRole("menuitem", { name: "Công việc" });
  const event = screen.getByRole("menuitem", { name: "Sự kiện" });
  expect(task.getAttribute("data-action")).toBe("new-task");
  expect(event.getAttribute("data-action")).toBe("new-event");
  expect(document.activeElement).toBe(task);
  await user.click(event);
  expect(screen.queryByRole("menu")).toBeNull();
  expect(document.activeElement).toBe(trigger);
});

it("supports arrow keys and Escape without trapping focus", async () => {
  render(<QuickCreateMenu />);
  const user = userEvent.setup();
  const trigger = screen.getByRole("button", { name: "Tạo mới" });
  trigger.focus();
  await user.keyboard("{ArrowUp}");
  expect(document.activeElement).toBe(
    screen.getByRole("menuitem", { name: "Sự kiện" }),
  );
  await user.keyboard("{ArrowDown}");
  expect(document.activeElement).toBe(
    screen.getByRole("menuitem", { name: "Công việc" }),
  );
  await user.keyboard("{Escape}");
  expect(screen.queryByRole("menu")).toBeNull();
  expect(document.activeElement).toBe(trigger);
});

it("closes on an outside click and uses letter shortcuts only inside the open menu", async () => {
  render(
    <>
      <QuickCreateMenu />
      <input aria-label="Tên đang nhập" />
    </>,
  );
  const user = userEvent.setup();
  const trigger = screen.getByRole("button", { name: "Tạo mới" });
  await user.type(screen.getByLabelText("Tên đang nhập"), "te");
  expect(screen.queryByRole("menu")).toBeNull();
  await user.click(trigger);
  await user.click(screen.getByLabelText("Tên đang nhập"));
  expect(screen.queryByRole("menu")).toBeNull();
  await user.click(trigger);
  await user.keyboard("e");
  expect(screen.queryByRole("menu")).toBeNull();
  expect(document.activeElement).toBe(trigger);
  await user.click(trigger);
  fireEvent.resize(window);
  expect(screen.queryByRole("menu")).toBeNull();
});
