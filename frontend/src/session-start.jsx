import { useEffect, useId, useState } from "react";
import { icon, timeFmt } from "./ui.jsx";

export function sessionStartIssue(session, workspace, now = Date.now()) {
  if (session.status !== "Scheduled") return "Phiên không còn chờ bắt đầu.";
  const start = new Date(session.start).getTime(),
    end = new Date(session.end).getTime();
  if (!Number.isFinite(start) || !Number.isFinite(end))
    return "Cần cập nhật thời gian của phiên.";
  if (now < start - 15 * 60000)
    return `Có thể bắt đầu từ ${timeFmt(start - 15 * 60000)} (sớm 15 phút).`;
  if (now >= end) return "Phiên đã quá giờ. Điều chỉnh lịch trước khi bắt đầu.";
  if (
    (workspace?.sessions || []).some((item) =>
      ["Running", "Paused"].includes(item.status),
    )
  )
    return "Hoàn thành hoặc bỏ qua phiên đang chạy trước.";
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

export function StartSessionButton({
  session,
  workspace,
  label = "Bắt đầu",
  className = "small primary",
}) {
  const [now, setNow] = useState(Date.now);
  const helpId = useId();
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 15000);
    return () => clearInterval(timer);
  }, []);
  const issue = sessionStartIssue(session, workspace, now);
  return (
    <div className="session-start-control">
      <button
        type="button"
        className={`btn ${className}`}
        data-action="session-start"
        data-id={session.id}
        disabled={!!issue}
        aria-describedby={issue ? helpId : undefined}
      >
        {icon("play")}
        {label}
      </button>
      {issue && (
        <p className="session-start-hint" id={helpId}>
          {issue}
        </p>
      )}
    </div>
  );
}
