import { describe, expect, it } from "vitest";
import { validateNewPassword } from "../src/password-policy.js";

describe("new password policy", () => {
  it.each([
    "Abcdef1!",
    "Ab1!" + "a".repeat(28),
    "Rainy morning with coffee 42!",
    " Abcdef1! ",
  ])("accepts a valid password: %s", (password) => {
    expect(() =>
      validateNewPassword(password, "member@example.test"),
    ).not.toThrow();
  });

  it.each([
    "Abcde1!",
    "Ab1!" + "a".repeat(29),
    "abcdefgh1!",
    "ABCDEFGH1!",
    "Abcdefgh!",
    "Abcdefgh1",
    "Abcdef1 ",
    "Abcdef1\n",
    "Abcdef١!",
    "SafePASSWORD1!",
    "SafeWebSiteName1!",
    "SafePlAnOrA1!",
    "SafeMEMBER1!",
    "Member@example.test1!",
    "Ab1!" + "a".repeat(29) + "\n",
  ])("rejects an invalid password: %s", (password) => {
    expect(() =>
      validateNewPassword(password, "member@example.test"),
    ).toThrow();
  });

  it("checks the current password exactly", () => {
    expect(() => validateNewPassword("Abcdef1!", "", "Abcdef1!")).toThrow(
      "Mật khẩu mới phải khác mật khẩu hiện tại.",
    );
    expect(() => validateNewPassword("ABcdef1!", "", "Abcdef1!")).not.toThrow();
  });

  it("treats username regex characters as literal text", () => {
    expect(() =>
      validateNewPassword("SafeA.B+1!", "a.b+@example.test"),
    ).toThrow();
    expect(() =>
      validateNewPassword("SafeAxB+1!", "a.b+@example.test"),
    ).not.toThrow();
  });
  it("rejects a password containing the separate registration username", () => {
    expect(() =>
      validateNewPassword(
        "SafeLAN.NGUYEN1!",
        "member@example.test",
        undefined,
        "lan.nguyen",
      ),
    ).toThrow();
    expect(() =>
      validateNewPassword(
        "SafeMember1!",
        "member@example.test",
        undefined,
        "lan.nguyen",
      ),
    ).toThrow();
    expect(() =>
      validateNewPassword(
        "SafeSunrise1!",
        "member@example.test",
        undefined,
        "lan.nguyen",
      ),
    ).not.toThrow();
  });
});
