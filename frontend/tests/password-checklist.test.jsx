import { expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PasswordChecklist } from "../src/password-checklist.jsx";
import { PasswordField } from "../src/ui.jsx";

function mount({
  passwordName = "password",
  email = "member@example.test",
} = {}) {
  return render(
    <form>
      <label>
        Email
        <input name="email" defaultValue={email} />
      </label>
      <PasswordField label="Mật khẩu mới" name={passwordName} />
      <PasswordChecklist passwordName={passwordName} />
    </form>,
  );
}

function met(id) {
  return (
    document.querySelector(`[data-requirement="${id}"]`).dataset.met === "true"
  );
}

it.each(["password", "newPassword"])(
  "shows exactly four checks for %s",
  (passwordName) => {
    mount({ passwordName });
    expect(document.querySelectorAll(".password-checklist li")).toHaveLength(4);
  },
);

it("starts unchecked and ticks requirements individually while typing and deleting", async () => {
  mount();
  expect(document.querySelectorAll('[data-met="true"]')).toHaveLength(0);
  const user = userEvent.setup();
  const password = screen.getByLabelText("Mật khẩu mới");
  await user.type(password, "a");
  expect(met("length")).toBe(false);
  expect(met("letters")).toBe(false);
  await user.clear(password);
  await user.type(password, "Abcdef1!");
  expect(document.querySelectorAll('[data-met="true"]')).toHaveLength(4);
  await user.type(password, "{Backspace}");
  expect(met("special")).toBe(false);
  expect(met("length")).toBe(false);
  expect(met("letters")).toBe(true);
  await user.clear(password);
  expect(document.querySelectorAll('[data-met="true"]')).toHaveLength(0);
});

it("keeps username and forbidden text out of the live checklist", () => {
  mount();
  const password = screen.getByLabelText("Mật khẩu mới");
  fireEvent.change(password, { target: { value: "SafePASSWORD1!" } });
  expect(document.querySelectorAll('[data-met="true"]')).toHaveLength(4);
  fireEvent.change(screen.getByLabelText("Email"), {
    target: { value: "SAFE@example.test" },
  });
  expect(document.querySelectorAll('[data-met="true"]')).toHaveLength(4);
  fireEvent.change(screen.getByLabelText("Email"), {
    target: { value: "other@example.test" },
  });
  expect(document.querySelectorAll('[data-met="true"]')).toHaveLength(4);
  fireEvent.change(screen.getByLabelText("Email"), { target: { value: "" } });
  expect(document.querySelectorAll('[data-met="true"]')).toHaveLength(4);
  expect(document.querySelector('[data-requirement="account"]')).toBeNull();
  expect(document.querySelector('[data-requirement="different"]')).toBeNull();
});

it("does not count whitespace as a special character or accept more than 32 characters", () => {
  mount();
  const password = screen.getByLabelText("Mật khẩu mới");
  fireEvent.change(password, { target: { value: "Abcdef1 " } });
  expect(met("special")).toBe(false);
  fireEvent.change(password, { target: { value: "Ab1!" + "a".repeat(29) } });
  expect(met("length")).toBe(false);
});

it.each([
  ["abcdefgh1!", "letters"],
  ["ABCDEFGH1!", "letters"],
  ["Abcdefgh!", "number"],
  ["Abcdefgh1", "special"],
])("leaves the relevant check unticked for %s", (value, requirement) => {
  mount();
  fireEvent.change(screen.getByLabelText("Mật khẩu mới"), {
    target: { value },
  });
  expect(met(requirement)).toBe(false);
});

it("announces the four live checks without a pending server check", () => {
  mount();
  fireEvent.change(screen.getByLabelText("Mật khẩu mới"), {
    target: { value: "Abcdef1!" },
  });
  expect(document.querySelectorAll('[data-met="true"]')).toHaveLength(4);
  expect(screen.queryByText("Kiểm tra khi lưu")).toBeNull();
  expect(screen.getByRole("status").textContent).toContain("4/4");
});

it("keeps ticks and the entered value when password visibility is toggled", async () => {
  mount();
  const user = userEvent.setup();
  await user.type(screen.getByLabelText("Mật khẩu mới"), "Abcdef1!");
  await user.click(screen.getByRole("button", { name: "Hiện mật khẩu" }));
  expect(screen.getByLabelText("Mật khẩu mới").type).toBe("text");
  expect(screen.getByLabelText("Mật khẩu mới").value).toBe("Abcdef1!");
  expect(document.querySelectorAll('[data-met="true"]')).toHaveLength(4);
});
