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
      title: "Gửi thời gian biểu",
      description:
        "Gửi lịch học, lịch làm hoặc lịch cố định để NORA phân tích các giờ bận trước.",
      done: !!workspace?.events?.length || hasTime || hasPlan,
      route: "agent",
      label: "Gửi thời gian biểu cho NORA",
      symbol: "calendar",
    },
    {
      title: "Xác nhận giờ có thể làm việc",
      description:
        "Nói giờ sinh hoạt, ngủ/nghỉ và giờ có thể làm việc; NORA cấu hình quanh lịch cố định.",
      done: hasTime,
      route: "agent",
      label: "Xác nhận giờ rảnh với NORA",
      symbol: "clock",
    },
    {
      title: "Giao việc cho NORA",
      description:
        "Cho biết bài tập hoặc công việc, thời lượng, deadline và mức độ gấp để NORA tìm chỗ trong lịch.",
      done: hasTasks,
      route: "agent",
      label: "Giao việc cho NORA",
      symbol: "edit",
    },
    {
      title: "Sắp xếp công việc",
      description:
        "NORA xếp việc vào giờ rảnh đã xác nhận, ưu tiên việc gấp và giữ lịch cố định.",
      done: hasPlan,
      route: "agent",
      label: "Nhờ NORA sắp xếp",
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
