import { expect, it } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PasswordField } from "../src/ui.jsx";

function mount(passwordName = "password", confirmName = "confirmPassword") {
  render(
    <form>
      <PasswordField label="Mật khẩu mới" name={passwordName} />
      <PasswordField
        label="Nhập lại mật khẩu"
        name={confirmName}
        confirmFor={passwordName}
      />
    </form>,
  );
}

it.each([
  ["password", "confirmPassword"],
  ["newPassword", "confirmNewPassword"],
])(
  "marks confirmation in %s forms and rechecks when the password changes",
  (passwordName, confirmName) => {
    mount(passwordName, confirmName);
    const password = screen.getByLabelText("Mật khẩu mới");
    const confirm = screen.getByLabelText("Nhập lại mật khẩu");
    expect(confirm.closest("label").hasAttribute("data-match")).toBe(false);
    expect(screen.getByRole("status").textContent).toBe("");
    fireEvent.change(password, { target: { value: "Abcdef1!" } });
    fireEvent.change(confirm, { target: { value: "Abcdef2!" } });
    expect(confirm.closest("label").dataset.match).toBe("mismatch");
    expect(confirm.getAttribute("aria-invalid")).toBe("true");
    expect(confirm.getAttribute("aria-describedby")).toBe(
      `${confirmName}-match`,
    );
    expect(screen.getByRole("status").textContent).toBe("Mật khẩu chưa khớp");
    fireEvent.input(confirm, { target: { value: "Abcdef1!" } });
    expect(confirm.closest("label").dataset.match).toBe("matched");
    expect(confirm.hasAttribute("aria-invalid")).toBe(false);
    expect(screen.getByRole("status").textContent).toBe("Mật khẩu khớp");
    fireEvent.change(password, { target: { value: "Abcdef3!" } });
    expect(confirm.closest("label").dataset.match).toBe("mismatch");
    fireEvent.change(confirm, { target: { value: "" } });
    expect(confirm.closest("label").hasAttribute("data-match")).toBe(false);
    expect(screen.getByRole("status").textContent).toBe("");
  },
);

it("preserves feedback and password text when toggling confirmation visibility", async () => {
  mount();
  const user = userEvent.setup();
  const confirm = screen.getByLabelText("Nhập lại mật khẩu");
  await user.type(screen.getByLabelText("Mật khẩu mới"), "Abcdef1!");
  await user.type(confirm, "Abcdef1!");
  await user.click(
    within(confirm.closest("label")).getByRole("button", {
      name: "Hiện mật khẩu",
    }),
  );
  expect(confirm.type).toBe("text");
  expect(confirm.value).toBe("Abcdef1!");
  expect(confirm.closest("label").dataset.match).toBe("matched");
});
