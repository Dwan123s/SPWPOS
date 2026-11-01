const processingStops = new Set([
  "AGENT_BUDGET_EXCEEDED",
  "TOOL_BUDGET_EXCEEDED",
  "AGENT_TIME_LIMIT",
]);

export const isAgentProcessingStop = (turn) =>
  turn.status === "BLOCKED" && processingStops.has(turn.errorCode);

export function agentBlockedHint(turn) {
  if (isAgentProcessingStop(turn))
    return "NORA đã dừng ở giới hạn xử lý, lịch chưa thay đổi. Bạn có thể nhắn “Tiếp tục xử lý” ngay trong hội thoại này để dùng lại thông tin đã gửi.";
  if (turn.plan?.warnings?.some((warning) => warning.unscheduledMinutes > 0))
    return "Phương án đã kiểm tra còn thiếu thời gian. Bạn có thể trao đổi với NORA về cách ưu tiên hoặc chia việc; mọi điều chỉnh cần phù hợp với lịch cố định và thông tin bạn xác nhận.";
  return "Yêu cầu chưa được hoàn tất. Bạn có thể bổ sung hoặc yêu cầu NORA kiểm tra lại ngay trong hội thoại này.";
}
