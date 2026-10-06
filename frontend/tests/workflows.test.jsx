import { beforeEach, it, expect, vi } from "vitest";
import {
  act,
  render,
  screen,
  waitFor,
  fireEvent,
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
  await user.type(screen.getByLabelText("Mật khẩu"), "ValidPassword123!");
  await user.click(screen.getByRole("button", { name: "Hiện mật khẩu" }));
  expect(screen.getByLabelText("Mật khẩu").type).toBe("text");
  expect(screen.getByLabelText("Mật khẩu").value).toBe("ValidPassword123!");
  await user.click(
    screen.getByRole("button", { name: "Đăng nhập", exact: true }),
  );
  await waitFor(() =>
    expect(
      calls.find((c) => c.url === "/api/browser-session/login")?.body,
    ).toEqual({ email: "member@example.test", password: "ValidPassword123!" }),
  );
  expect(calls[0].options.credentials).toBe("same-origin");
  expect(calls[0].options.headers["X-SPWPOS"]).toBe("1");
});
it("completes forgot password, OTP and reset transitions with the returned token", async () => {
  mount(false);
  act(() => renderAuth("forgot"));
  const user = userEvent.setup();
  await user.clear(screen.getByLabelText("Email"));
  await user.type(screen.getByLabelText("Email"), "member@example.test");
  await user.click(screen.getByRole("button", { name: "Gửi mã xác minh" }));
  await screen.findByRole("heading", { name: "Xác minh mã OTP" });
  await user.type(screen.getByLabelText("Mã OTP"), "123456");
  await user.click(
    screen.getByRole("button", { name: "Xác minh mã", exact: true }),
  );
  await screen.findByRole("heading", { name: "Đặt mật khẩu mới" });
  await user.type(screen.getByLabelText("Mật khẩu mới"), "NewPassword123!");
  await user.type(
    screen.getByLabelText("Nhập lại mật khẩu"),
    "NewPassword123!",
  );
  await user.click(screen.getByRole("button", { name: "Đặt mật khẩu mới" }));
  await screen.findByRole("heading", { name: "Đăng nhập vào PLANORA" });
  expect(
    calls.find((c) => c.url === "/api/auth/reset-password").body.resetToken,
  ).toBe("one-use-reset-token");
  expect(state.resetToken).toBeNull();
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
  expect(document.querySelector("#modal .info").textContent).toContain("<script>untrusted note</script>");
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
  window.matchMedia = vi.fn(() => ({ matches: true }));
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
