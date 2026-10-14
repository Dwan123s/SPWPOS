export function workflowSteps(workspace, now = Date.now()) {
  const hasTasks = (workspace?.tasks || []).some(
    (task) => !["Completed", "Cancelled"].includes(task.status),
  );
  const hasTime =
    !!workspace?.availability?.length ||
    !!workspace?.exceptions?.some((item) => item.isAvailable);
  const hasPlan = (workspace?.sessions || []).some(
    (session) =>
      ["Running", "Paused"].includes(session.status) ||
      (session.status === "Scheduled" && new Date(session.end).getTime() > now),
  );
  return [
    {
      title: "Nhập việc",
      description:
        "Viết việc cần làm để xem trước kết quả AI, hoặc thêm công việc thủ công.",
      done: hasTasks,
      route: "notes",
      label: "Nhập việc cần làm",
      symbol: "edit",
    },
    {
      title: "Chọn giờ rảnh",
      description:
        "Đã có công việc. Chọn những khoảng bạn có thể làm trước khi tạo lịch.",
      done: hasTime,
      route: "time",
      label: "Chọn giờ rảnh",
      symbol: "clock",
    },
    {
      title: "Xem lịch đề xuất",
      description:
        "Đã có việc và giờ rảnh. Xem lịch đề xuất rồi chọn lưu khi phù hợp.",
      done: hasPlan,
      action: "generate-plan",
      label: "Xem lịch đề xuất",
      symbol: "calendar",
    },
    {
      title: "Bắt đầu làm",
      description:
        "Lịch đã có phiên sắp tới. Mở lịch để xem giờ và bắt đầu khi đến lượt.",
      route: "calendar",
      label: "Mở lịch để làm việc",
      symbol: "play",
    },
  ];
}

export function nextWorkflowStep(workspace, now = Date.now()) {
  return workflowSteps(workspace, now).find((step) => !step.done);
}
