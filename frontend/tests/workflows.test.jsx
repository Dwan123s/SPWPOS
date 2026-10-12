import { beforeEach, it, expect, vi } from "vitest";
import {
  act,
  render,
  screen,
  waitFor,
  fireEvent,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { App } from "../src/App.jsx";
import { update } from "../src/store.js";
import { state } from "../src/ui.jsx";
import { renderAuth, authSetup } from "../src/auth.jsx";
import { planModal } from "../src/calendar.jsx";
import {
  workspace,
  insights,
  plan,
  resetFixtures,
  profile,
} from "./fixtures.js";

let calls, currentWorkspace;
beforeEach(() => {
  resetFixtures();
  currentWorkspace = workspace();
  calls = [];
  Object.assign(state, {
    workspace: null,
    insights: null,
    user: null,
    roles: [],
    route: "today",
    taskScope: "all",
    settingsTab: "profile",
    scenario: null,
    calendarStart: null,
  });
  update({
    screen: null,
    screenKey: "loading",
    modal: null,
    toasts: [],
    slots: {},
  });
  window.history.replaceState(null, "", "/");
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url, options = {}) => {
      const body = options.body ? JSON.parse(options.body) : undefined;
      calls.push({ url, options, body });
      let data = {};
      if (url === "/api/browser-session/me")
        data = { user: profile, roles: ["Admin"] };
      if (url === "/api/workspace") data = currentWorkspace;
      if (url === "/api/auth/login-methods") data = { hasPassword: true, googleLinked: false };
      if (url === "/api/intelligence") data = insights;
      if (url === "/api/plans/conflicts") data = [];
      if (url === "/api/tasks" && options.method === "POST") {
        const task = {
          ...currentWorkspace.tasks[0],
          ...body,
          id: "created-task",
          dependencies: [],
        };
        currentWorkspace.tasks.push(task);
        data = task;
      }
      if (url === "/api/workspace/profile" && options.method === "PUT")
        Object.assign(currentWorkspace.profile, body);
      if (url === "/api/plans") data = plan;
      if (url === "/api/tasks/capture")
        data = {
          source: "Rules",
          notes: [
            "Kiểm tra deadline trước khi lưu.",
            "<script>untrusted note</script>",
          ],
          task: {
            ...currentWorkspace.tasks[1],
            id: undefined,
            name: body.text,
          },
        };
      if (url === "/api/auth/verify-forgot-password-otp")
        data = { resetToken: "one-use-reset-token" };
      if (url.startsWith("/api/admin/users") || url === "/api/admin/jobs")
        data = [];
      if (url === "/api/admin/status")
        data = {
          databaseHealthy: true,
          serverTime: new Date().toISOString(),
          users: 1,
          tasks: 6,
          activeSessions: 0,
          failedLoginsLast24Hours: 0,
          failedNotificationRuns: 0,
        };
      return { ok: true, status: 200, json: async () => ({ data }) };
    }),
  );
});
function mount(initialize = true) {
  const host = document.createElement("div");
  host.id = "app";
  document.body.append(host);
  return render(<App initialize={initialize} />, { container: host });
}
async function navigate(route, title) {
  await act(async () => {
    location.hash = route;
    window.dispatchEvent(new Event("hashchange"));
  });
  await screen.findByRole("heading", { level: 1, name: title });
}

it("submits login with same-origin cookies and toggles password without clearing it", async () => {
  mount(false);
  authSetup(vi.fn());
  act(() => renderAuth());
  const user = userEvent.setup();
  await user.type(screen.getByLabelText("Email"), "member@example.test");
  await user.type(screen.getByLabelText("Mật khẩu"), "ValidSecret123!");
  await user.click(screen.getByRole("button", { name: "Hiện mật khẩu" }));
  expect(screen.getByLabelText("Mật khẩu").type).toBe("text");
  expect(screen.getByLabelText("Mật khẩu").value).toBe("ValidSecret123!");
  await user.click(
    screen.getByRole("button", { name: "Đăng nhập", exact: true }),
  );
  await waitFor(() =>
    expect(
      calls.find((c) => c.url === "/api/browser-session/login")?.body,
    ).toEqual({ email: "member@example.test", password: "ValidSecret123!" }),
  );
  expect(calls[0].options.credentials).toBe("same-origin");
  expect(calls[0].options.headers["X-SPWPOS"]).toBe("1");
});
it("shows failed login directly on the password field, repeats feedback, and clears it on edit", async () => {
  mount(false);
  const onLogin = vi.fn();
  authSetup(onLogin);
  act(() => renderAuth());
  const password = screen.getByLabelText("Mật khẩu");
  const email = screen.getByLabelText("Email");
  fireEvent.change(email, { target: { value: "member@example.test" } });
  fireEvent.change(password, { target: { value: "Secret123!" } });
  const shell = password.closest(".input-with-action");
  const animation = { cancel: vi.fn(), finished: Promise.resolve() };
  shell.animate = vi.fn(() => animation);
  const failure = {
    ok: false,
    status: 401,
    json: async () => ({ error: { code: "INVALID_CREDENTIALS", message: "Invalid email or password." } }),
  };
  vi.mocked(fetch).mockResolvedValueOnce(failure).mockResolvedValueOnce(failure);
  const user = userEvent.setup();
  const button = screen.getByRole("button", { name: "Đăng nhập", exact: true });
  await user.click(button);
  await waitFor(() => expect(password.getAttribute("aria-invalid")).toBe("true"));
  const field = password.closest("label");
  expect(field.dataset.rejected).toBe("true");
  expect(password.getAttribute("aria-describedby")).toBe("password-login-error");
  expect(within(field).getByRole("alert").textContent).toBe("Email hoặc mật khẩu chưa đúng. Vui lòng kiểm tra và thử lại.");
  expect(document.querySelector(".form-error").textContent).toBe("");
  expect(password.value).toBe("Secret123!");
  expect(document.activeElement).toBe(password);
  expect(shell.animate).toHaveBeenCalledTimes(1);
  await user.click(screen.getByRole("button", { name: "Hiện mật khẩu" }));
  expect(password.type).toBe("text");
  expect(password.getAttribute("aria-invalid")).toBe("true");
  await user.click(button);
  await waitFor(() => expect(shell.animate).toHaveBeenCalledTimes(2));
  fireEvent.change(email, { target: { value: "other@example.test" } });
  expect(password.hasAttribute("aria-invalid")).toBe(false);
  expect(within(field).queryByRole("alert")).toBeNull();
  expect(animation.cancel).toHaveBeenCalled();
  await user.click(button);
  await waitFor(() => expect(onLogin).toHaveBeenCalledOnce());
  expect(password.hasAttribute("aria-invalid")).toBe(false);
});

it("shows login rejection without shaking when reduced motion is enabled", async () => {
  vi.spyOn(window, "matchMedia").mockImplementation((query) => ({
    matches: query.includes("prefers-reduced-motion"),
    addEventListener() {},
    removeEventListener() {},
  }));
  mount(false);
  authSetup(vi.fn());
  act(() => renderAuth());
  const password = screen.getByLabelText("Mật khẩu");
  fireEvent.change(screen.getByLabelText("Email"), { target: { value: "member@example.test" } });
  fireEvent.change(password, { target: { value: "Secret123!" } });
  const shell = password.closest(".input-with-action");
  shell.animate = vi.fn();
  vi.mocked(fetch).mockResolvedValueOnce({
    ok: false,
    status: 401,
    json: async () => ({ error: { code: "INVALID_CREDENTIALS", message: "Invalid email or password." } }),
  });
  await userEvent.setup().click(screen.getByRole("button", { name: "Đăng nhập", exact: true }));
  await waitFor(() => expect(password.getAttribute("aria-invalid")).toBe("true"));
  expect(shell.animate).not.toHaveBeenCalled();
  fireEvent.input(password, { target: { value: "Secret124!" } });
  expect(password.hasAttribute("aria-invalid")).toBe(false);
});

it("keeps rate-limit errors separate from password rejection feedback", async () => {
  mount(false);
  authSetup(vi.fn());
  act(() => renderAuth());
  const password = screen.getByLabelText("Mật khẩu");
  fireEvent.change(screen.getByLabelText("Email"), { target: { value: "member@example.test" } });
  fireEvent.change(password, { target: { value: "Secret123!" } });
  vi.mocked(fetch).mockResolvedValueOnce({
    ok: false,
    status: 429,
    json: async () => ({ error: { code: "RATE_LIMITED", message: "Please wait before trying again." } }),
  });
  await userEvent.setup().click(screen.getByRole("button", { name: "Đăng nhập", exact: true }));
  await waitFor(() => expect(document.querySelector(".form-error").textContent).toBe("Please wait before trying again."));
  expect(password.hasAttribute("aria-invalid")).toBe(false);
  expect(password.closest("label").hasAttribute("data-rejected")).toBe(false);
});

it.each(["register", "reset"])("blocks invalid passwords in the %s form before calling the API", async (mode) => {
  mount(false);
  state.authEmail = "member@example.test";
  act(() => renderAuth(mode));
  expect(screen.getByRole("list", { name: "Yêu cầu mật khẩu" })).toBeTruthy();
  const password = screen.getByLabelText(mode === "reset" ? "Mật khẩu mới" : "Mật khẩu");
  expect(password.minLength).toBe(8);
  expect(password.maxLength).toBe(32);
  fireEvent.change(password, { target: { value: "SafeMEMBER1!" } });
  fireEvent.change(screen.getByLabelText("Nhập lại mật khẩu"), { target: { value: "SafeMEMBER1!" } });
  expect(document.querySelector(".form-error").textContent).toBe("");
  fireEvent.submit(document.querySelector('form[data-form="auth"]'));
  expect(document.querySelectorAll('.password-checklist [data-met="true"]')).toHaveLength(4);
  await waitFor(() => expect(document.querySelector(".form-error").textContent).toBe("Mật khẩu không được chứa email hoặc tên tài khoản (phần trước @)."));
  expect(calls).toHaveLength(0);
});

it("notifies about forbidden text only after clicking reset", async () => {
  mount(false);
  state.authEmail = "member@example.test";
  act(() => renderAuth("reset"));
  fireEvent.change(screen.getByLabelText("Mật khẩu mới"), { target: { value: "SafePASSWORD1!" } });
  fireEvent.change(screen.getByLabelText("Nhập lại mật khẩu"), { target: { value: "SafePASSWORD1!" } });
  expect(document.querySelectorAll('.password-checklist [data-met="true"]')).toHaveLength(4);
  expect(document.querySelector(".form-error").textContent).toBe("");
  await userEvent.setup().click(screen.getByRole("button", { name: "Đặt mật khẩu mới" }));
  await waitFor(() => expect(document.querySelector(".form-error").textContent).toBe("Mật khẩu không được chứa password, websitename hoặc PLANORA."));
  expect(calls).toHaveLength(0);
});

it("shows the server's password reuse error after clicking reset and keeps the form usable", async () => {
  mount(false);
  state.authEmail = "member@example.test";
  state.resetToken = "saved-reset-token";
  act(() => renderAuth("reset"));
  fireEvent.change(screen.getByLabelText("Mật khẩu mới"), { target: { value: "Abcdef1!" } });
  fireEvent.change(screen.getByLabelText("Nhập lại mật khẩu"), { target: { value: "Abcdef1!" } });
  expect(document.querySelector(".form-error").textContent).toBe("");
  vi.mocked(fetch).mockResolvedValueOnce({
    ok: false,
    status: 400,
    json: async () => ({ error: { code: "PASSWORD_POLICY_FAILED", message: "New password must differ from your current password." } }),
  });
  await userEvent.setup().click(screen.getByRole("button", { name: "Đặt mật khẩu mới" }));
  await waitFor(() => expect(document.querySelector(".form-error").textContent).toBe("New password must differ from your current password."));
  expect(fetch).toHaveBeenCalledWith("/api/auth/reset-password", expect.objectContaining({ method: "POST" }));
  expect(state.resetToken).toBe("saved-reset-token");
  expect(screen.getByLabelText("Mật khẩu mới").value).toBe("Abcdef1!");
  expect(screen.getByRole("button", { name: "Đặt mật khẩu mới" }).disabled).toBe(false);
});

it("blocks reuse in the account password form before calling the API", async () => {
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  await navigate("settings", "Tài khoản & nhóm");
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: "Bảo mật" }));
  expect(screen.getByRole("list", { name: "Yêu cầu mật khẩu" })).toBeTruthy();
  fireEvent.change(screen.getByLabelText("Mật khẩu hiện tại"), { target: { value: "Abcdef1!" } });
  fireEvent.change(screen.getByLabelText("Mật khẩu mới"), { target: { value: "Abcdef1!" } });
  fireEvent.change(screen.getByLabelText("Nhập lại mật khẩu mới"), { target: { value: "Abcdef1!" } });
  fireEvent.submit(document.querySelector('form[data-form="password"]'));
  await waitFor(() => expect(document.querySelector('form[data-form="password"] .form-error').textContent).toBe("Mật khẩu mới phải khác mật khẩu hiện tại."));
  expect(calls.some((call) => call.url === "/api/auth/change-password")).toBe(false);
});

it("completes forgot password, OTP and reset transitions with the returned token", async () => {
  mount(false);
  act(() => renderAuth("forgot"));
  const artwork = document.querySelector(".login-scene");
  expect(artwork).not.toBeNull();
  const user = userEvent.setup();
  await user.clear(screen.getByLabelText("Email"));
  await user.type(screen.getByLabelText("Email"), "member@example.test");
  await user.click(screen.getByRole("button", { name: "Gửi mã xác minh" }));
  await screen.findByRole("heading", { name: "Xác minh mã OTP" });
  expect(document.querySelector(".login-scene")).toBe(artwork);
  await user.type(screen.getByLabelText("Mã OTP"), "123456");
  await user.click(
    screen.getByRole("button", { name: "Xác minh mã", exact: true }),
  );
  await screen.findByRole("heading", { name: "Đặt mật khẩu mới" });
  expect(document.querySelector(".login-scene")).toBe(artwork);
  await user.type(screen.getByLabelText("Mật khẩu mới"), "NewSecret123!");
  await user.type(
    screen.getByLabelText("Nhập lại mật khẩu"),
    "NewSecret123!",
  );
  await user.click(screen.getByRole("button", { name: "Đặt mật khẩu mới" }));
  await screen.findByRole("heading", { name: "Đăng nhập vào PLANORA" });
  expect(document.querySelector(".login-scene")).toBe(artwork);
  expect(
    calls.find((c) => c.url === "/api/auth/reset-password").body.resetToken,
  ).toBe("one-use-reset-token");
  expect(state.resetToken).toBeNull();
});
it("keeps the animated auth page and pause setting through registration and verification", async () => {
  mount(false);
  act(() => renderAuth());
  const artwork = document.querySelector(".login-scene");
  const card = document.querySelector(".auth-card");
  const user = userEvent.setup();
  await user.type(screen.getByLabelText("Mật khẩu"), "DoNotCarryThis123!");
  await user.click(screen.getByRole("button", { name: "Hiện mật khẩu" }));
  await user.click(
    screen.getByRole("button", { name: "Tạm dừng chuyển động" }),
  );
  await user.click(screen.getByRole("button", { name: "Tạo tài khoản mới" }));
  await screen.findByRole("heading", { name: "Tạo tài khoản PLANORA" });
  expect(document.querySelector(".login-scene")).toBe(artwork);
  expect(document.querySelector(".auth-card")).toBe(card);
  expect(document.querySelector(".auth-hero-art")).toBeNull();
  expect(screen.getByLabelText("Mật khẩu").value).toBe("");
  expect(screen.getByLabelText("Mật khẩu").type).toBe("password");
  expect(
    screen
      .getByRole("button", { name: "Tiếp tục chuyển động" })
      .getAttribute("aria-pressed"),
  ).toBe("true");
  await user.type(screen.getByLabelText("Tên"), "Lan");
  await user.type(screen.getByLabelText("Họ"), "Nguyen");
  await user.clear(screen.getByLabelText("Email"));
  await user.type(screen.getByLabelText("Email"), "new-member@example.test");
  await user.type(screen.getByLabelText("Mật khẩu"), "ValidSecret123!");
  await user.type(
    screen.getByLabelText("Nhập lại mật khẩu"),
    "ValidSecret123!",
  );
  await user.click(
    screen.getByRole("button", { name: "Tạo tài khoản", exact: true }),
  );
  await screen.findByRole("heading", { name: "Xác minh email" });
  expect(document.querySelector(".login-scene")).toBe(artwork);
  expect(calls.find((c) => c.url === "/api/auth/register").body).toEqual({
    email: "new-member@example.test",
    password: "ValidSecret123!",
    confirmPassword: "ValidSecret123!",
    firstName: "Lan",
    lastName: "Nguyen",
  });
  await user.type(screen.getByLabelText("Mã OTP"), "123456");
  await user.click(
    screen.getByRole("button", { name: "Xác minh email", exact: true }),
  );
  await screen.findByRole("heading", { name: "Đăng nhập vào PLANORA" });
  expect(document.querySelector(".login-scene")).toBe(artwork);
  expect(screen.getByLabelText("Email").value).toBe("new-member@example.test");
  expect(screen.getByLabelText("Mật khẩu").value).toBe("");
  expect(
    screen
      .getByRole("button", { name: "Tiếp tục chuyển động" })
      .getAttribute("aria-pressed"),
  ).toBe("true");
});
it("sets a first password for a Google account from Security and returns to login", async () => {
  const regularFetch = fetch.getMockImplementation();
  fetch.mockImplementation(async (url, options) => {
    if (url === "/api/auth/login-methods") return { ok: true, status: 200, json: async () => ({ data: { hasPassword: false, googleLinked: true } }) };
    return regularFetch(url, options);
  });
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  await navigate("settings", "Tài khoản & nhóm");
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: "Bảo mật" }));
  await screen.findByRole("heading", { name: "Thiết lập đăng nhập bằng mật khẩu" });
  expect(screen.queryByLabelText("Mật khẩu hiện tại")).toBeNull();
  await user.click(screen.getByRole("button", { name: "Gửi mã OTP" }));
  await user.type(screen.getByLabelText("Mã OTP xác nhận từ Email"), "123456");
  await user.type(screen.getByLabelText("Mật khẩu mới", { exact: true }), "ValidSecret123!");
  await user.type(screen.getByLabelText("Nhập lại mật khẩu mới"), "ValidSecret123!");
  await user.click(screen.getByRole("button", { name: "Thiết lập mật khẩu", exact: true }));
  await screen.findByRole("heading", { name: "Đăng nhập vào PLANORA" });
  expect(calls.find((c) => c.url === "/api/auth/set-password").body).toEqual({ otp: "123456", newPassword: "ValidSecret123!", confirmNewPassword: "ValidSecret123!" });
  expect(calls.some((c) => c.url === "/api/auth/change-password/request-otp")).toBe(true);
  expect(calls.some((c) => c.url === "/api/browser-session/logout")).toBe(true);
  expect(calls.some((c) => c.url === "/api/auth/change-password")).toBe(false);
});
it("navigates all workspace screens and renders the admin data", async () => {
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  for (const [route, title] of Object.entries({
    tasks: "Công việc",
    calendar: "Lịch làm việc",
    time: "Thời gian & tùy chọn",
    insights: "Phân tích cá nhân",
    scenario: "Thử kịch bản",
    notifications: "Thông báo",
    settings: "Tài khoản & nhóm",
    admin: "Quản trị",
  }))
    await navigate(route, title);
  await screen.findByText("Hoạt động");
  expect(calls.some((c) => c.url === "/api/admin/status")).toBe(true);
});
it("creates a task and retains filters while updating the React result list", async () => {
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  await navigate("tasks", "Công việc");
  const user = userEvent.setup();
  await user.type(screen.getByLabelText("Tìm công việc"), "Không tồn tại");
  expect(screen.getByText("Không có công việc phù hợp")).toBeTruthy();
  await user.clear(screen.getByLabelText("Tìm công việc"));
  await user.click(screen.getByRole("button", { name: "Thêm công việc" }));
  const name = screen.getByLabelText("Tên công việc");
  await user.type(name, "Kiểm tra React workflow");
  await user.click(
    screen.getByRole("button", { name: "Tạo công việc", exact: true }),
  );
  await screen.findByText("Kiểm tra React workflow");
  const request = calls.find((c) => c.url === "/api/tasks");
  expect(request.body.name).toBe("Kiểm tra React workflow");
  expect(request.body.splittable).toBe(true);
  expect(request.body.priority).toBe(2);
});
it("adds and removes availability rows without losing unsaved time values", async () => {
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  await navigate("time", "Thời gian & tùy chọn");
  const user = userEvent.setup();
  fireEvent.change(screen.getAllByLabelText("Bắt đầu")[1], {
    target: { value: "09:30" },
  });
  await user.click(screen.getByRole("button", { name: "Thêm khoảng" }));
  expect(screen.getAllByLabelText("Ngày trong tuần")).toHaveLength(6);
  await user.click(
    screen.getAllByRole("button", { name: "Xóa khoảng thời gian" })[0],
  );
  expect(screen.getAllByLabelText("Bắt đầu")[0].value).toBe("09:30");
  await user.click(screen.getByRole("button", { name: "Lưu lịch rảnh" }));
  await waitFor(() =>
    expect(
      calls.find((c) => c.url === "/api/time/availability")?.body.slots,
    ).toHaveLength(5),
  );
  expect(
    calls.find((c) => c.url === "/api/time/availability").body.slots[0]
      .startMinute,
  ).toBe(570);
});
it("removes an avatar while preserving unsaved profile fields before save", async () => {
  currentWorkspace.profile.avatarUrl = "data:image/png;base64,AAA";
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  await navigate("settings", "Tài khoản & nhóm");
  const user = userEvent.setup();
  await user.clear(screen.getByLabelText("Tên", { exact: true }));
  await user.type(screen.getByLabelText("Tên", { exact: true }), "Tên mới");
  await user.click(screen.getByRole("button", { name: "Xóa ảnh" }));
  expect(screen.getByLabelText("Tên", { exact: true }).value).toBe("Tên mới");
  expect(document.querySelector("#avatarUrl-hidden").value).toBe("");
  expect(document.querySelector("#hero-avatar-img")).toBeNull();
  await user.click(screen.getByRole("button", { name: "Lưu thông tin hồ sơ" }));
  await waitFor(() =>
    expect(
      calls.find((c) => c.url === "/api/workspace/profile")?.body.firstName,
    ).toBe("Tên mới"),
  );
});
it("shows apply only for real proposals and keeps simulations read-only", async () => {
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  act(() => planModal(plan));
  expect(screen.getByRole("button", { name: "Áp dụng kế hoạch" })).toBeTruthy();
  act(() => planModal(plan, true));
  expect(screen.queryByRole("button", { name: "Áp dụng kế hoạch" })).toBeNull();
  expect(screen.queryByRole("button", { name: "Bỏ đề xuất" })).toBeNull();
});
it("reviews a captured task with safe text notes before saving", async () => {
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  await navigate("tasks", "Công việc");
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: "Ghi nhanh" }));
  await user.type(
    screen.getByLabelText("Mô tả bằng câu tự nhiên"),
    "Chuẩn bị báo cáo React",
  );
  await user.click(screen.getByRole("button", { name: /Tạo gợi ý/ }));
  await screen.findByRole("heading", { name: "Kiểm tra công việc gợi ý" });
  expect(document.querySelector("#modal .info").textContent).toContain(
    "<script>untrusted note</script>",
  );
  expect(document.querySelector("#modal script")).toBeNull();
  expect(screen.getByLabelText("Tên công việc").value).toBe(
    "Chuẩn bị báo cáo React",
  );
  expect(calls.some((c) => c.url === "/api/tasks")).toBe(false);
  await user.click(
    screen.getByRole("button", { name: "Tạo công việc", exact: true }),
  );
  await screen.findByText("Chuẩn bị báo cáo React");
});
it("opens and closes the mobile drawer with inert content and restored focus", async () => {
  window.matchMedia = vi.fn((query) => ({
    matches: query === "(max-width: 800px)",
    addEventListener() {},
    removeEventListener() {},
  }));
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  const user = userEvent.setup();
  expect(document.querySelector(".sidebar").inert).toBe(true);
  const button = screen.getByRole("button", { name: "Mở menu" });
  await user.click(button);
  expect(button.getAttribute("aria-expanded")).toBe("true");
  expect(document.querySelector(".main").inert).toBe(true);
  fireEvent.keyDown(document.activeElement, { key: "Escape" });
  await waitFor(() => expect(document.activeElement).toBe(button));
  expect(document.querySelector(".main").inert).toBe(false);
});
