export function needsTimetableSetup(workspace) {
  return (
    !!workspace &&
    !(
      workspace.events?.length ||
      workspace.availability?.length ||
      workspace.exceptions?.length ||
      workspace.tasks?.length ||
      workspace.sessions?.length
    )
  );
}

export function workflowSteps(workspace, now = Date.now()) {
  const hasTasks = (workspace?.tasks || []).some(
    (task) => !["Completed", "Cancelled"].includes(task.status),
  );
  const hasTime =
    !!workspace?.availabilityPolicy?.confirmed ||
    !!workspace?.availability?.length ||
    !!workspace?.exceptions?.some((item) => item.isAvailable);
  const hasPlan = (workspace?.sessions || []).some(
    (session) =>
      ["Running", "Paused"].includes(session.status) ||
      (session.status === "Scheduled" && new Date(session.end).getTime() > now),
  );
  return [
    {
      title: "Thêm sự kiện cố định",
      description: "Thêm lịch học, lịch làm hoặc các sự kiện đã có.",
      done: true,
      route: "calendar",
      label: "Thêm sự kiện cố định",
      symbol: "calendar",
    },
    {
      title: "Xác nhận giờ có thể làm việc",
      description: "Chọn giờ có thể làm việc trong tuần.",
      done: hasTime,
      route: "time",
      label: "Thiết lập giờ làm",
      symbol: "clock",
    },
    {
      title: "Thêm công việc",
      description: "Lưu tên, thời lượng và hạn chót của công việc.",
      done: hasTasks,
      route: "tasks",
      label: "Thêm công việc",
      symbol: "edit",
    },
    {
      title: "Sắp xếp công việc",
      description: "Xem trước các phiên theo giờ rảnh rồi xác nhận.",
      done: hasPlan,
      route: "calendar",
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
