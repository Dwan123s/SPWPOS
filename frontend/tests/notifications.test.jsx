import { beforeEach, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NotificationCenter } from "../src/notifications.jsx";
import { state } from "../src/ui.jsx";

let workspace;
beforeEach(() => {
  vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-10-14T03:00:00Z"));
  workspace = {
    preference: { timeZoneId: "Asia/Ho_Chi_Minh" },
    tasks: [
      {
        id: "task",
        name: "Chuẩn bị báo cáo",
        deadline: "2026-10-14T10:00:00Z",
      },
    ],
    notifications: [
      {
        id: "old",
        kind: "SCHEDULE_CHANGED",
        title: "Đã chỉnh sửa phiên",
        message: "Lịch mới đã được lưu.",
        createdAt: "2026-10-13T15:00:00Z",
        isRead: true,
      },
      {
        id: "deadline",
        kind: "DEADLINE",
        title: "Công việc sắp đến hạn",
        message: "Báo cáo cần hoàn thành hôm nay.",
        taskId: "task",
        createdAt: "2026-10-13T17:30:00Z",
        isRead: false,
      },
      {
        id: "session",
        kind: "SESSION_REMINDER",
        title: "Sắp đến phiên làm việc",
        message: "Bắt đầu phiên học.",
        createdAt: "2026-10-14T02:00:00Z",
        isRead: false,
      },
      {
        id: "warning",
        kind: "WORKLOAD_WARNING",
        title: "Kế hoạch cần xem lại",
        message: "Khối lượng công việc vượt khả năng.",
        createdAt: "2026-10-14T01:00:00Z",
        isRead: false,
      },
    ],
    notificationSettings: {
      inAppEnabled: true,
      deadlineLeadHours: 36,
      sessionReminderLeadMinutes: 15,
      workloadWarningLevel: "OVERLOAD",
    },
  };
  state.workspace = workspace;
});
it("groups dates in the account time zone and routes each reminder to its relevant screen", () => {
  render(<NotificationCenter workspace={workspace} />);
  const today = within(
    screen.getByRole("region", { name: "Thông báo Hôm nay" }),
  );
  expect(
    today
      .getAllByRole("article")
      .map((item) => item.querySelector("h4").textContent),
  ).toEqual([
    "Sắp đến phiên làm việc",
    "Kế hoạch cần xem lại",
    "Công việc sắp đến hạn",
  ]);
  expect(
    screen.getByRole("region", { name: "Thông báo Hôm qua" }),
  ).toBeTruthy();
  expect(
    within(today.getByRole("article", { name: "Sắp đến phiên làm việc" }))
      .getByRole("link", { name: "Xem lịch làm việc" })
      .getAttribute("href"),
  ).toBe("#calendar");
  expect(
    within(today.getByRole("article", { name: "Kế hoạch cần xem lại" }))
      .getByRole("link", { name: "Xem tiến độ" })
      .getAttribute("href"),
  ).toBe("#insights");
  expect(today.getByRole("button", { name: "Xem công việc" }).dataset.id).toBe(
    "task",
  );
});
it("combines status, type and accent-insensitive search and can reset an empty result", async () => {
  render(<NotificationCenter workspace={workspace} />);
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: /^Chưa đọc 3$/ }));
  await user.selectOptions(screen.getByLabelText("Loại thông báo"), "deadline");
  await user.type(
    screen.getByRole("searchbox", { name: "Tìm thông báo" }),
    "bao cao",
  );
  expect(screen.getAllByRole("article")).toHaveLength(1);
  await user.type(
    screen.getByRole("searchbox", { name: "Tìm thông báo" }),
    " không tồn tại",
  );
  expect(screen.queryByRole("article")).toBeNull();
  expect(
    screen.getByRole("heading", { name: "Không tìm thấy thông báo" }),
  ).toBeTruthy();
  await user.click(
    screen.getByRole("button", { name: "Xem tất cả thông báo" }),
  );
  expect(screen.getAllByRole("article")).toHaveLength(4);
  expect(screen.getByRole("searchbox", { name: "Tìm thông báo" }).value).toBe(
    "",
  );
  expect(screen.getByLabelText("Loại thông báo").value).toBe("all");
});
it("filters unread reminders by type from the overview shortcuts", async () => {
  render(<NotificationCenter workspace={workspace} />);
  await userEvent
    .setup()
    .click(
      within(screen.getByRole("complementary")).getByRole("button", {
        name: "Cảnh báo lịch 1",
      }),
    );
  expect(screen.getAllByRole("article")).toHaveLength(1);
  expect(
    screen.getByRole("article", { name: "Kế hoạch cần xem lại" }),
  ).toBeTruthy();
  expect(screen.getByLabelText("Loại thông báo").value).toBe("warning");
});
it("keeps deleted-task notifications readable and never opens a missing task", () => {
  workspace.tasks = [];
  workspace.notifications = [
    {
      ...workspace.notifications[1],
      title: "<script>unsafe</script>",
      message: "<img src=x onerror=alert(1)>",
    },
  ];
  render(<NotificationCenter workspace={workspace} />);
  expect(
    screen
      .getByRole("link", { name: "Mở danh sách công việc" })
      .getAttribute("href"),
  ).toBe("#tasks");
  expect(screen.queryByRole("button", { name: "Xem công việc" })).toBeNull();
  expect(screen.getByText(/Công việc liên quan không còn/)).toBeTruthy();
  expect(document.querySelector("script, img")).toBeNull();
});
it("shows saved reminder preferences and distinguishes disabled notifications from an empty inbox", () => {
  workspace.notifications = [];
  workspace.notificationSettings.inAppEnabled = false;
  render(<NotificationCenter workspace={workspace} />);
  expect(
    screen.getByRole("heading", { name: "Chưa có thông báo" }),
  ).toBeTruthy();
  expect(screen.getByText("Đang tắt trong ứng dụng")).toBeTruthy();
  expect(screen.getByText("36 giờ")).toBeTruthy();
  expect(screen.getByText("15 phút")).toBeTruthy();
  expect(screen.getByText("Khi quá tải")).toBeTruthy();
  expect(
    screen.getByRole("button", { name: "Điều chỉnh nhắc nhở" }).dataset.action,
  ).toBe("notification-preferences");
});
