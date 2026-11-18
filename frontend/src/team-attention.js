const CLAIM_WINDOW_MS = 48 * 60 * 60 * 1000;

export const awaitsTeamDeadline = (task, now) =>
  task.status === "Open" && !task.assigneeId && Date.parse(task.deadline) <= now;

export const teamAttentionMetrics = [
  {
    id: "overdue",
    label: "Việc quá hạn",
    glyph: "alertCircle",
    description: "Chờ nhận hoặc đang làm",
  },
  {
    id: "review",
    label: "Báo cáo chờ duyệt",
    glyph: "eye",
    description: "Thành viên đã gửi kết quả",
  },
  {
    id: "unclaimed",
    label: "Sắp đến hạn, chưa nhận",
    glyph: "clock",
    description: "Trong 48 giờ tới",
  },
];

export function teamTaskAttention(task, now) {
  if (task.status === "PendingReview") return { id: "review", rank: 1 };
  if (task.status !== "Open" && task.status !== "Assigned") return null;

  const remaining = Date.parse(task.deadline) - now;
  if (remaining <= 0) return { id: "overdue", rank: 2 };
  if (
    task.status === "Open" &&
    !task.assigneeId &&
    remaining <= CLAIM_WINDOW_MS
  )
    return { id: "unclaimed", rank: 3, label: "Sắp đến hạn · Chưa ai nhận" };
  if (task.availabilityCheck?.canTake != null)
    return { id: "availability", rank: 4, label: "Có phản hồi kiểm tra lịch" };
  return null;
}
