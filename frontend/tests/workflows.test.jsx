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
import { getGuide, startGuide } from "../src/onboarding.js";
import {
  workspace,
  insights,
  plan,
  resetFixtures,
  profile,
} from "./fixtures.js";

let calls, currentWorkspace, failTaskDelete;
beforeEach(() => {
  resetFixtures();
  currentWorkspace = workspace();
  calls = [];
  failTaskDelete = false;
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
    authIdentifier: "",
    authEmail: "",
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
      if (url === "/api/notes") data = [];
      if (url === "/api/notes/agent")
        data = { aiAvailable: true, provider: "Gemini" };
      if (url === "/api/time/availability" && options.method === "PUT")
        currentWorkspace.availability = body.slots;
      if (url === "/api/auth/login-methods")
        data = { hasPassword: true, googleLinked: false };
      if (url === "/api/intelligence") data = insights;
      if (url === "/api/plans/conflicts") data = [];
      if (
        url.startsWith("/api/notifications/") &&
        url.endsWith("/read") &&
        options.method === "POST"
      ) {
        const id = url.split("/")[3];
        currentWorkspace.notifications = currentWorkspace.notifications.map(
          (item) => (item.id === id ? { ...item, isRead: true } : item),
        );
      }
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
      if (url.startsWith("/api/tasks/") && options.method === "DELETE") {
        if (failTaskDelete)
          return {
            ok: false,
            status: 409,
            json: async () => ({
              error: {
                message: "Công việc vừa có phiên đang chạy. Thử lại sau.",
              },
            }),
          };
        const id = url.split("/").at(-1);
        const ids = new Set([
          id,
          ...currentWorkspace.tasks
            .filter((task) => task.parentId === id)
            .map((task) => task.id),
        ]);
        currentWorkspace.tasks = currentWorkspace.tasks.filter(
          (task) => !ids.has(task.id),
        );
        currentWorkspace.sessions = currentWorkspace.sessions.map((session) =>
          ids.has(session.taskId) && session.status === "Scheduled"
            ? { ...session, status: "Cancelled" }
            : session,
        );
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

it("accepts a username in the login field and keeps email recovery separate", async () => {
  mount(false);
  authSetup(vi.fn());
  act(() => renderAuth());
  const user = userEvent.setup();
  const identifier = screen.getByLabelText("Tên đăng nhập hoặc email");
  expect(identifier.type).toBe("text");
  expect(identifier.autocomplete).toBe("username");
  await user.type(identifier, "quan.nhat");
  await user.type(screen.getByLabelText("Mật khẩu"), "ValidSecret123!");
  await user.click(
    screen.getByRole("button", { name: "Đăng nhập", exact: true }),
  );
  await waitFor(() =>
    expect(
      calls.find((call) => call.url === "/api/browser-session/login")?.body,
    ).toEqual({ email: "quan.nhat", password: "ValidSecret123!" }),
  );
  act(() => renderAuth("forgot"));
  expect(screen.getByLabelText("Email").value).toBe("");
});

it("submits login with same-origin cookies and toggles password without clearing it", async () => {
  mount(false);
  authSetup(vi.fn());
  act(() => renderAuth());
  const user = userEvent.setup();
  await user.type(
    screen.getByLabelText(/^(Email|Tên đăng nhập hoặc email)$/),
    "member@example.test",
  );
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
  const email = screen.getByLabelText(/^(Email|Tên đăng nhập hoặc email)$/);
  fireEvent.change(email, { target: { value: "member@example.test" } });
  fireEvent.change(password, { target: { value: "Secret123!" } });
  const shell = password.closest(".input-with-action");
  const animation = { cancel: vi.fn(), finished: Promise.resolve() };
  shell.animate = vi.fn(() => animation);
  const failure = {
    ok: false,
    status: 401,
    json: async () => ({
      error: {
        code: "INVALID_CREDENTIALS",
        message: "Invalid email or password.",
      },
    }),
  };
  vi.mocked(fetch)
    .mockResolvedValueOnce(failure)
    .mockResolvedValueOnce(failure);
  const user = userEvent.setup();
  const button = screen.getByRole("button", { name: "Đăng nhập", exact: true });
  await user.click(button);
  await waitFor(() =>
    expect(password.getAttribute("aria-invalid")).toBe("true"),
  );
  const field = password.closest("label");
  expect(field.dataset.rejected).toBe("true");
  expect(password.getAttribute("aria-describedby")).toBe(
    "password-login-error",
  );
  expect(within(field).getByRole("alert").textContent).toBe(
    "Tên đăng nhập, email hoặc mật khẩu chưa đúng. Vui lòng kiểm tra và thử lại.",
  );
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
  fireEvent.change(
    screen.getByLabelText(/^(Email|Tên đăng nhập hoặc email)$/),
    { target: { value: "member@example.test" } },
  );
  fireEvent.change(password, { target: { value: "Secret123!" } });
  const shell = password.closest(".input-with-action");
  shell.animate = vi.fn();
  vi.mocked(fetch).mockResolvedValueOnce({
    ok: false,
    status: 401,
    json: async () => ({
      error: {
        code: "INVALID_CREDENTIALS",
        message: "Invalid email or password.",
      },
    }),
  });
  await userEvent
    .setup()
    .click(screen.getByRole("button", { name: "Đăng nhập", exact: true }));
  await waitFor(() =>
    expect(password.getAttribute("aria-invalid")).toBe("true"),
  );
  expect(shell.animate).not.toHaveBeenCalled();
  fireEvent.input(password, { target: { value: "Secret124!" } });
  expect(password.hasAttribute("aria-invalid")).toBe(false);
});

it("keeps rate-limit errors separate from password rejection feedback", async () => {
  mount(false);
  authSetup(vi.fn());
  act(() => renderAuth());
  const password = screen.getByLabelText("Mật khẩu");
  fireEvent.change(
    screen.getByLabelText(/^(Email|Tên đăng nhập hoặc email)$/),
    { target: { value: "member@example.test" } },
  );
  fireEvent.change(password, { target: { value: "Secret123!" } });
  vi.mocked(fetch).mockResolvedValueOnce({
    ok: false,
    status: 429,
    json: async () => ({
      error: {
        code: "RATE_LIMITED",
        message: "Please wait before trying again.",
      },
    }),
  });
  await userEvent
    .setup()
    .click(screen.getByRole("button", { name: "Đăng nhập", exact: true }));
  await waitFor(() =>
    expect(document.querySelector(".form-error").textContent).toBe(
      "Please wait before trying again.",
    ),
  );
  expect(password.hasAttribute("aria-invalid")).toBe(false);
  expect(password.closest("label").hasAttribute("data-rejected")).toBe(false);
});

it.each(["register", "reset"])(
  "blocks invalid passwords in the %s form before calling the API",
  async (mode) => {
    mount(false);
    state.authEmail = "member@example.test";
    act(() => renderAuth(mode));
    expect(screen.getByRole("list", { name: "Yêu cầu mật khẩu" })).toBeTruthy();
    const password = screen.getByLabelText(
      mode === "reset" ? "Mật khẩu mới" : "Mật khẩu",
    );
    expect(password.minLength).toBe(8);
    expect(password.maxLength).toBe(32);
    fireEvent.change(password, { target: { value: "SafeMEMBER1!" } });
    fireEvent.change(screen.getByLabelText("Nhập lại mật khẩu"), {
      target: { value: "SafeMEMBER1!" },
    });
    expect(document.querySelector(".form-error").textContent).toBe("");
    fireEvent.submit(document.querySelector('form[data-form="auth"]'));
    expect(
      document.querySelectorAll('.password-checklist [data-met="true"]'),
    ).toHaveLength(4);
    await waitFor(() =>
      expect(document.querySelector(".form-error").textContent).toBe(
        "Mật khẩu không được chứa tên đăng nhập, email hoặc tên tài khoản (phần trước @).",
      ),
    );
    expect(calls).toHaveLength(0);
  },
);

it("notifies about forbidden text only after clicking reset", async () => {
  mount(false);
  state.authEmail = "member@example.test";
  act(() => renderAuth("reset"));
  fireEvent.change(screen.getByLabelText("Mật khẩu mới"), {
    target: { value: "SafePASSWORD1!" },
  });
  fireEvent.change(screen.getByLabelText("Nhập lại mật khẩu"), {
    target: { value: "SafePASSWORD1!" },
  });
  expect(
    document.querySelectorAll('.password-checklist [data-met="true"]'),
  ).toHaveLength(4);
  expect(document.querySelector(".form-error").textContent).toBe("");
  await userEvent
    .setup()
    .click(screen.getByRole("button", { name: "Đặt mật khẩu mới" }));
  await waitFor(() =>
    expect(document.querySelector(".form-error").textContent).toBe(
      "Mật khẩu không được chứa password, websitename hoặc PLANORA.",
    ),
  );
  expect(calls).toHaveLength(0);
});

it("shows the server's password reuse error after clicking reset and keeps the form usable", async () => {
  mount(false);
  state.authEmail = "member@example.test";
  state.resetToken = "saved-reset-token";
  act(() => renderAuth("reset"));
  fireEvent.change(screen.getByLabelText("Mật khẩu mới"), {
    target: { value: "Abcdef1!" },
  });
  fireEvent.change(screen.getByLabelText("Nhập lại mật khẩu"), {
    target: { value: "Abcdef1!" },
  });
  expect(document.querySelector(".form-error").textContent).toBe("");
  vi.mocked(fetch).mockResolvedValueOnce({
    ok: false,
    status: 400,
    json: async () => ({
      error: {
        code: "PASSWORD_POLICY_FAILED",
        message: "New password must differ from your current password.",
      },
    }),
  });
  await userEvent
    .setup()
    .click(screen.getByRole("button", { name: "Đặt mật khẩu mới" }));
  await waitFor(() =>
    expect(document.querySelector(".form-error").textContent).toBe(
      "New password must differ from your current password.",
    ),
  );
  expect(fetch).toHaveBeenCalledWith(
    "/api/auth/reset-password",
    expect.objectContaining({ method: "POST" }),
  );
  expect(state.resetToken).toBe("saved-reset-token");
  expect(screen.getByLabelText("Mật khẩu mới").value).toBe("Abcdef1!");
  expect(
    screen.getByRole("button", { name: "Đặt mật khẩu mới" }).disabled,
  ).toBe(false);
});

it("blocks reuse in the account password form before calling the API", async () => {
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  await navigate("settings", "Cài đặt");
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: "Bảo mật" }));
  expect(screen.getByRole("list", { name: "Yêu cầu mật khẩu" })).toBeTruthy();
  fireEvent.change(screen.getByLabelText("Mật khẩu hiện tại"), {
    target: { value: "Abcdef1!" },
  });
  fireEvent.change(screen.getByLabelText("Mật khẩu mới"), {
    target: { value: "Abcdef1!" },
  });
  fireEvent.change(screen.getByLabelText("Nhập lại mật khẩu mới"), {
    target: { value: "Abcdef1!" },
  });
  fireEvent.submit(document.querySelector('form[data-form="password"]'));
  await waitFor(() =>
    expect(
      document.querySelector('form[data-form="password"] .form-error')
        .textContent,
    ).toBe("Mật khẩu mới phải khác mật khẩu hiện tại."),
  );
  expect(calls.some((call) => call.url === "/api/auth/change-password")).toBe(
    false,
  );
});

it("completes forgot password, OTP and reset transitions with the returned token", async () => {
  mount(false);
  act(() => renderAuth("forgot"));
  const artwork = document.querySelector(".login-scene");
  expect(artwork).not.toBeNull();
  const user = userEvent.setup();
  await user.clear(screen.getByLabelText(/^(Email|Tên đăng nhập hoặc email)$/));
  await user.type(
    screen.getByLabelText(/^(Email|Tên đăng nhập hoặc email)$/),
    "member@example.test",
  );
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
  await user.type(screen.getByLabelText("Nhập lại mật khẩu"), "NewSecret123!");
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
  await user.type(screen.getByLabelText("Tên đăng nhập"), "lan.nguyen");
  await user.clear(screen.getByLabelText(/^(Email|Tên đăng nhập hoặc email)$/));
  await user.type(
    screen.getByLabelText(/^(Email|Tên đăng nhập hoặc email)$/),
    "new-member@example.test",
  );
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
    userName: "lan.nguyen",
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
  expect(
    screen.getByLabelText(/^(Email|Tên đăng nhập hoặc email)$/).value,
  ).toBe("new-member@example.test");
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
    if (url === "/api/auth/login-methods")
      return {
        ok: true,
        status: 200,
        json: async () => ({
          data: { hasPassword: false, googleLinked: true },
        }),
      };
    return regularFetch(url, options);
  });
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  await navigate("settings", "Cài đặt");
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: "Bảo mật" }));
  await screen.findByRole("heading", {
    name: "Thiết lập đăng nhập bằng mật khẩu",
  });
  expect(screen.queryByLabelText("Mật khẩu hiện tại")).toBeNull();
  await user.click(screen.getByRole("button", { name: "Gửi mã OTP" }));
  await user.type(screen.getByLabelText("Mã OTP xác nhận từ Email"), "123456");
  await user.type(
    screen.getByLabelText("Mật khẩu mới", { exact: true }),
    "ValidSecret123!",
  );
  await user.type(
    screen.getByLabelText("Nhập lại mật khẩu mới"),
    "ValidSecret123!",
  );
  await user.click(
    screen.getByRole("button", { name: "Thiết lập mật khẩu", exact: true }),
  );
  await screen.findByRole("heading", { name: "Đăng nhập vào PLANORA" });
  expect(calls.find((c) => c.url === "/api/auth/set-password").body).toEqual({
    otp: "123456",
    newPassword: "ValidSecret123!",
    confirmNewPassword: "ValidSecret123!",
  });
  expect(
    calls.some((c) => c.url === "/api/auth/change-password/request-otp"),
  ).toBe(true);
  expect(calls.some((c) => c.url === "/api/browser-session/logout")).toBe(true);
  expect(calls.some((c) => c.url === "/api/auth/change-password")).toBe(false);
});
it("navigates all workspace screens and renders the admin data", async () => {
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  expect(document.querySelector(".console-sync")).toBeNull();
  expect(screen.queryByText("Đã tải dữ liệu")).toBeNull();
  for (const [route, title] of Object.entries({
    tasks: "Công việc",
    calendar: "Lịch làm việc",
    time: "Giờ rảnh",
    insights: "Tiến độ",
    scenario: "Thử nhận thêm việc",
    notifications: "Thông báo",
    settings: "Cài đặt",
    admin: "Quản trị",
  }))
    await navigate(route, title);
  await screen.findByText("Hoạt động");
  expect(calls.some((c) => c.url === "/api/admin/status")).toBe(true);
});
it("marks a notification read while preserving its filters and updating the shell unread count", async () => {
  mount();
  await navigate("notifications", "Thông báo");
  const user = userEvent.setup();
  await user.click(
    screen.getByRole("button", { name: "Chưa đọc 1", exact: true }),
  );
  await user.selectOptions(screen.getByLabelText("Loại thông báo"), "deadline");
  await user.type(
    screen.getByRole("searchbox", { name: "Tìm thông báo" }),
    "thảo luận",
  );
  await user.click(screen.getByRole("button", { name: /^Đánh dấu đã đọc:/ }));
  await waitFor(() => expect(screen.queryByRole("article")).toBeNull());
  expect(screen.getByRole("searchbox", { name: "Tìm thông báo" }).value).toBe(
    "thảo luận",
  );
  expect(screen.getByLabelText("Loại thông báo").value).toBe("deadline");
  const unreadFilter = screen.getByRole("button", {
    name: "Chưa đọc 0",
    exact: true,
  });
  expect(unreadFilter.getAttribute("aria-pressed")).toBe("true");
  await waitFor(() => expect(document.activeElement).toBe(unreadFilter));
  expect(document.querySelector(".notification-number")).toBeNull();
  expect(
    calls.filter(
      (call) => call.url === "/api/notifications/notification-1/read",
    ),
  ).toHaveLength(1);
});
it("retains an unread notification when marking it read fails", async () => {
  const original = vi.mocked(fetch).getMockImplementation();
  vi.mocked(fetch).mockImplementation(async (url, options = {}) => {
    if (url === "/api/notifications/notification-1/read")
      return {
        ok: false,
        status: 503,
        json: async () => ({
          error: { message: "Chưa đánh dấu được thông báo. Thử lại." },
        }),
      };
    return original(url, options);
  });
  mount();
  await navigate("notifications", "Thông báo");
  await userEvent
    .setup()
    .click(screen.getByRole("button", { name: /^Đánh dấu đã đọc:/ }));
  await screen.findByText("Chưa đánh dấu được thông báo. Thử lại.");
  expect(
    screen.getByRole("button", { name: /^Đánh dấu đã đọc:/ }).disabled,
  ).toBe(false);
  expect(currentWorkspace.notifications[0].isRead).toBe(false);
});
it("opens the notification settings tab directly from the inbox", async () => {
  mount();
  await navigate("notifications", "Thông báo");
  await userEvent
    .setup()
    .click(screen.getByRole("button", { name: "Điều chỉnh nhắc nhở" }));
  await screen.findByRole("heading", { name: "Cài đặt thông báo" });
  expect(location.hash).toBe("#settings");
  expect(state.settingsTab).toBe("notifications");
});
it("confirms deletion from a task row and allows cancelling without a request", async () => {
  mount();
  await navigate("tasks", "Công việc");
  const user = userEvent.setup();
  const task = currentWorkspace.tasks[0];
  await user.click(
    screen.getByRole("button", { name: `Xóa công việc ${task.name}` }),
  );
  const dialog = within(await screen.findByRole("dialog"));
  expect(dialog.getByText(new RegExp(task.name))).toBeTruthy();
  await user.click(dialog.getByRole("button", { name: "Hủy", exact: true }));
  expect(calls.some((call) => call.options.method === "DELETE")).toBe(false);
  expect(
    screen.getByRole("button", { name: `Xóa công việc ${task.name}` }),
  ).toBeTruthy();
});
it("deletes a task and its children after explaining the cancelled sessions", async () => {
  const task = currentWorkspace.tasks[0];
  currentWorkspace.tasks[1].parentId = task.id;
  mount();
  await navigate("tasks", "Công việc");
  const user = userEvent.setup();
  await user.click(
    screen.getByRole("button", { name: `Xóa công việc ${task.name}` }),
  );
  const dialog = within(await screen.findByRole("dialog"));
  expect(dialog.getByText(/và 1 việc con/)).toBeTruthy();
  expect(dialog.getByText(/phiên chưa thực hiện sẽ bị hủy/)).toBeTruthy();
  await user.click(
    dialog.getByRole("button", { name: "Xóa công việc", exact: true }),
  );
  await waitFor(() =>
    expect(
      screen.queryByRole("button", { name: `Xóa công việc ${task.name}` }),
    ).toBeNull(),
  );
  expect(
    calls
      .filter((call) => call.options.method === "DELETE")
      .map((call) => call.url),
  ).toEqual(["/api/tasks/task-0"]);
  expect(currentWorkspace.tasks).toHaveLength(4);
  expect(currentWorkspace.sessions[0].status).toBe("Completed");
});
it.each(["Running", "Paused"])(
  "blocks deletion when a child has a %s session",
  async (status) => {
    const task = currentWorkspace.tasks[0];
    currentWorkspace.tasks[1].parentId = task.id;
    currentWorkspace.sessions[1].status = status;
    mount();
    await navigate("tasks", "Công việc");
    await userEvent
      .setup()
      .click(
        screen.getByRole("button", { name: `Xóa công việc ${task.name}` }),
      );
    await screen.findByRole("heading", { name: "Chưa thể xóa công việc" });
    expect(
      within(screen.getByRole("dialog")).queryByRole("button", {
        name: "Xóa công việc",
        exact: true,
      }),
    ).toBeNull();
    expect(calls.some((call) => call.options.method === "DELETE")).toBe(false);
  },
);
it("keeps the task when the server rejects deletion", async () => {
  const task = currentWorkspace.tasks[0];
  failTaskDelete = true;
  mount();
  await navigate("tasks", "Công việc");
  const user = userEvent.setup();
  await user.click(
    screen.getByRole("button", { name: `Xóa công việc ${task.name}` }),
  );
  await user.click(
    within(await screen.findByRole("dialog")).getByRole("button", {
      name: "Xóa công việc",
      exact: true,
    }),
  );
  await screen.findByText("Công việc vừa có phiên đang chạy. Thử lại sau.");
  expect(
    screen.getByRole("button", { name: `Xóa công việc ${task.name}` }),
  ).toBeTruthy();
  expect(currentWorkspace.tasks).toHaveLength(6);
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
  await navigate("time", "Giờ rảnh");
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
  await navigate("settings", "Cài đặt");
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
it("guides an empty daily schedule to input before offering planning", async () => {
  currentWorkspace.tasks = [];
  currentWorkspace.sessions = [];
  currentWorkspace.events = [];
  mount();
  const schedule = within(
    await screen.findByRole("region", { name: "Lịch hôm nay", exact: true }),
  );
  await userEvent
    .setup()
    .click(
      schedule.getByRole("button", { name: "Nhập việc cần làm", exact: true }),
    );
  await screen.findByRole("heading", { level: 1, name: "Nhập việc" });
  expect(calls.some((call) => call.url === "/api/plans")).toBe(false);
});

it("returns to today's work after explicitly saving a proposed schedule", async () => {
  mount();
  await navigate("calendar", "Lịch làm việc");
  const user = userEvent.setup();
  await user.click(
    screen.getByRole("button", { name: "Lập kế hoạch", exact: true }),
  );
  await user.click(
    screen.getByRole("button", { name: "Tạo đề xuất", exact: true }),
  );
  await screen.findByRole("heading", { name: "Xem trước lịch" });
  expect(calls.some((call) => call.url.endsWith("/apply"))).toBe(false);
  await user.click(
    screen.getByRole("button", { name: "Lưu lịch này", exact: true }),
  );
  await screen.findByRole("heading", {
    level: 1,
    name: /Một ngày có chủ đích/,
  });
  expect(location.hash).toBe("#today");
  expect(calls.filter((call) => call.url.endsWith("/apply"))).toHaveLength(1);
  expect(document.querySelector("#modal").open).toBe(false);
});

it("shows apply only for real proposals and keeps simulations read-only", async () => {
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  act(() => planModal(plan));
  expect(screen.getByRole("button", { name: "Lưu lịch này" })).toBeTruthy();
  act(() => planModal(plan, true));
  expect(screen.queryByRole("button", { name: "Lưu lịch này" })).toBeNull();
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

it("guides users to hours before generating a plan without changing saved tasks", async () => {
  currentWorkspace.availability = [];
  currentWorkspace.exceptions = [];
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  await userEvent
    .setup()
    .click(
      within(document.querySelector(".page-header .actions")).getByRole(
        "button",
        { name: "Chọn giờ rảnh", exact: true },
      ),
    );
  await screen.findByRole("heading", { level: 1, name: "Giờ rảnh" });
  expect(calls.some((c) => c.url === "/api/plans")).toBe(false);
  expect(currentWorkspace.tasks).toHaveLength(6);
});
it("saves hours and previews the schedule before any explicit apply", async () => {
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  await navigate("time", "Giờ rảnh");
  const user = userEvent.setup();
  await user.click(
    screen.getByRole("button", { name: "Lưu giờ rảnh & xem lịch" }),
  );
  await screen.findByRole("heading", { name: "Tạo kế hoạch mới" });
  expect(
    calls.some(
      (call) =>
        call.url === "/api/time/availability" && call.options.method === "PUT",
    ),
  ).toBe(true);
  expect(calls.some((call) => call.url === "/api/plans")).toBe(false);
  await user.click(screen.getByRole("button", { name: "Tạo đề xuất" }));
  await screen.findByRole("heading", { name: "Xem trước lịch" });
  expect(calls.some((call) => call.url === "/api/plans")).toBe(true);
  expect(calls.some((call) => call.url.endsWith("/apply"))).toBe(false);
});
it("stays on hours when saving fails instead of proceeding to plan generation", async () => {
  const original = vi.mocked(fetch).getMockImplementation();
  vi.mocked(fetch).mockImplementation(async (url, options = {}) => {
    if (url === "/api/time/availability")
      return {
        ok: false,
        status: 400,
        json: async () => ({ error: { message: "Các khoảng giờ bị trùng." } }),
      };
    return original(url, options);
  });
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  await navigate("time", "Giờ rảnh");
  await userEvent
    .setup()
    .click(screen.getByRole("button", { name: "Lưu giờ rảnh & xem lịch" }));
  await screen.findByText("Các khoảng giờ bị trùng.");
  expect(
    screen.queryByRole("heading", { name: "Tạo kế hoạch mới" }),
  ).toBeNull();
  expect(calls.some((call) => call.url === "/api/plans")).toBe(false);
});
it("completes the first guided step by saving a manual task without requiring AI", async () => {
  profile.id = crypto.randomUUID();
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  act(() => startGuide());
  await navigate("tasks", "Công việc");
  const user = userEvent.setup();
  await user.click(
    screen.getByRole("button", { name: "Thêm công việc", exact: true }),
  );
  const form = document.querySelector('[data-form="task"]');
  const name = form.querySelector('[name="name"]');
  fireEvent.change(name, { target: { value: "Công việc đầu tiên" } });
  fireEvent.submit(form);
  await waitFor(() => expect(getGuide().completed).toContain(0));
  expect(
    calls.some(
      (call) => call.url === "/api/tasks" && call.options.method === "POST",
    ),
  ).toBe(true);
  expect(calls.some((call) => call.url.endsWith("/analyze"))).toBe(false);
  await user.click(
    screen.getByRole("button", { name: "Tiếp tục", exact: true }),
  );
  await screen.findByRole("heading", { level: 1, name: "Giờ rảnh" });
  expect(getGuide().step).toBe(1);
});
it("finishing a session asks for progress and completes the whole task only on explicit click", async () => {
  currentWorkspace.sessions[1].status = "Running";
  currentWorkspace.tasks[1].status = "InProgress";
  const original = vi.mocked(fetch).getMockImplementation();
  vi.mocked(fetch).mockImplementation(async (url, options = {}) => {
    if (url === "/api/sessions/session-1/complete")
      currentWorkspace.sessions[1].status = "Completed";
    if (url === "/api/tasks/task-1/status")
      Object.assign(currentWorkspace.tasks[1], JSON.parse(options.body));
    return original(url, options);
  });
  mount();
  await screen.findByRole("heading", { name: /Một ngày có chủ đích/ });
  const user = userEvent.setup();
  await user.click(
    screen.getByRole("button", { name: "Xong phiên", exact: true }),
  );
  await screen.findByRole("heading", {
    name: "Bạn đã làm xong cả công việc chưa?",
  });
  expect(calls.some((c) => c.url === "/api/tasks/task-1/status")).toBe(false);
  await user.click(
    screen.getByRole("button", { name: "Đã hoàn thành cả công việc" }),
  );
  await waitFor(() =>
    expect(
      calls.find((c) => c.url === "/api/tasks/task-1/status")?.body,
    ).toEqual({ status: "Completed", progress: 100 }),
  );
});
