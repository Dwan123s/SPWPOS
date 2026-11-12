import { dateFmt, dayKey, timeFmt } from "./ui.jsx";

export const sessionStartOpensAt = (session) => Date.parse(session.start) - 15 * 60000;

export function sessionStartIssue(session, workspace, now = Date.now()) {
  if (session.status !== "Scheduled") return "Phiên không còn chờ bắt đầu.";
  const start = new Date(session.start).getTime(),
    end = new Date(session.end).getTime();
  if (!Number.isFinite(start) || !Number.isFinite(end))
    return "Cần cập nhật thời gian của phiên.";
  const opensAt = sessionStartOpensAt(session);
  if (now < opensAt) {
    const day = dayKey(now) === dayKey(opensAt) ? "" : ` ngày ${dateFmt(opensAt)}`;
    return `Chưa đến giờ. Có thể bắt đầu từ ${timeFmt(opensAt)}${day} (sớm 15 phút).`;
  }
  if (now >= end) return "Phiên đã quá giờ. Bạn có thể ghi kết quả nếu đã làm việc.";
  if (
    (workspace?.sessions || []).some((item) =>
      ["Running", "Paused"].includes(item.status),
    )
  )
    return "Hoàn thành hoặc bỏ qua phiên đang chạy trước.";
  if (workspace?.sessions?.some((item) => item.id !== session.id && item.status === "Scheduled" &&
      Date.parse(item.start) <= now && now < Date.parse(item.end)))
    return "Khung giờ hiện tại dành cho phiên khác. Hãy chờ phiên đó kết thúc.";
  const task = workspace?.tasks?.find((item) => item.id === session.taskId);
  if (task?.isDeleted || ["Completed", "Cancelled"].includes(task?.status))
    return "Công việc đã kết thúc.";
  if (
    task?.dependencies?.some(
      (id) =>
        workspace.tasks.find((item) => item.id === id)?.status !== "Completed",
    )
  )
    return "Hoàn thành công việc phụ thuộc trước.";
  if (
    (workspace?.events || []).some(
      (event) =>
        new Date(event.start).getTime() <= now &&
        new Date(event.end).getTime() > now,
    )
  )
    return "Bạn đang có sự kiện cố định. Bắt đầu sau khi sự kiện kết thúc.";
  return null;
}
