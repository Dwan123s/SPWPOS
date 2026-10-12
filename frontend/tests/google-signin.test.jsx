import { beforeEach, expect, it, vi } from "vitest";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { api } from "../src/api.js";
import { prepareGoogleClient } from "../src/firebase-google.js";
import { GoogleSignIn } from "../src/google-signin.jsx";

vi.mock("../src/api.js", () => ({ api: vi.fn() }));
vi.mock("../src/firebase-google.js", async (importOriginal) => ({
  ...(await importOriginal()),
  prepareGoogleClient: vi.fn(),
}));
const client = { signIn: vi.fn(), signOut: vi.fn() };
const config = {
  enabled: true,
  linked: false,
  hasPassword: true,
  config: { projectId: "planora-test" },
};
beforeEach(() => {
  vi.clearAllMocks();
  api.mockResolvedValue(config);
  prepareGoogleClient.mockResolvedValue(client);
  client.signIn.mockResolvedValue("verified-firebase-token");
  client.signOut.mockResolvedValue();
});
async function ready(link = false, onSuccess = vi.fn()) {
  render(<GoogleSignIn link={link} onSuccess={onSuccess} />);
  const button = screen.getByRole("button", {
    name: link ? "Liên kết Google" : "Đăng nhập với Google",
  });
  await waitFor(() => expect(button.disabled).toBe(false));
  return { button, onSuccess };
}

it("stays unavailable when the server has not enabled Firebase", async () => {
  api.mockResolvedValue({ enabled: false });
  render(<GoogleSignIn />);
  expect(
    await screen.findByText("Đăng nhập Google hiện chưa khả dụng."),
  ).toBeTruthy();
  expect(screen.getByRole("button").disabled).toBe(true);
  expect(prepareGoogleClient).not.toHaveBeenCalled();
});
it("exchanges only the Firebase ID token then clears the temporary Firebase session", async () => {
  const { button, onSuccess } = await ready();
  fireEvent.click(button);
  await waitFor(() => expect(onSuccess).toHaveBeenCalledOnce());
  expect(api).toHaveBeenLastCalledWith(
    "/api/browser-session/firebase/login",
    "POST",
    { idToken: "verified-firebase-token" },
    false,
  );
  await waitFor(() => expect(client.signOut).toHaveBeenCalledOnce());
});
it("closing the popup does not exchange a token and allows retry", async () => {
  client.signIn.mockRejectedValue({ code: "auth/popup-closed-by-user" });
  const { button, onSuccess } = await ready();
  fireEvent.click(button);
  expect((await screen.findByRole("alert")).textContent).toContain(
    "Bạn đã đóng cửa sổ Google",
  );
  await waitFor(() => expect(button.disabled).toBe(false));
  expect(api).toHaveBeenCalledTimes(1);
  expect(onSuccess).not.toHaveBeenCalled();
  expect(client.signOut).toHaveBeenCalledOnce();
});
it("shows the server instruction for an unverified local account without completing login", async () => {
  const { button, onSuccess } = await ready();
  api.mockRejectedValue({
    status: 409,
    message:
      "Email này đã đăng ký nhưng chưa xác minh. Hãy xác minh email của tài khoản trước khi đăng nhập Google.",
  });
  fireEvent.click(button);
  expect((await screen.findByRole("alert")).textContent).toContain(
    "Hãy xác minh email của tài khoản",
  );
  expect(onSuccess).not.toHaveBeenCalled();
  await waitFor(() => expect(button.disabled).toBe(false));
});
it("links to the current account and displays confirmation", async () => {
  const { button } = await ready(true);
  fireEvent.click(button);
  expect((await screen.findByRole("status")).textContent).toContain(
    "cùng tài khoản và dữ liệu này",
  );
  expect(api).toHaveBeenLastCalledWith(
    "/api/browser-session/firebase/link",
    "POST",
    { idToken: "verified-firebase-token" },
    false,
  );
  expect(
    screen.getByRole("button", { name: "Đã liên kết Google" }).disabled,
  ).toBe(true);
});
it("shows a linked account without opening a second Firebase client", async () => {
  api.mockResolvedValue({ ...config, linked: true, hasPassword: false });
  render(<GoogleSignIn link />);
  expect(await screen.findByRole("status")).toBeTruthy();
  expect(screen.getByText(/Để tạo mật khẩu/)).toBeTruthy();
  expect(prepareGoogleClient).not.toHaveBeenCalled();
});
it("prevents another popup while authentication is pending", async () => {
  let finish;
  client.signIn.mockReturnValue(
    new Promise((resolve) => {
      finish = resolve;
    }),
  );
  const { button, onSuccess } = await ready();
  fireEvent.click(button);
  fireEvent.click(button);
  expect(client.signIn).toHaveBeenCalledOnce();
  expect(button.disabled).toBe(true);
  await act(async () => {
    finish("verified-firebase-token");
  });
  await waitFor(() => expect(onSuccess).toHaveBeenCalledOnce());
});
