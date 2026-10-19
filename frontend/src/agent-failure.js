export function agentFailure(failure = {}) {
  const code = failure.code || "";
  if (failure.name === "AbortError" || code === "AGENT_CANCELLED")
    return {
      category: "cancelled",
      label: "Đã dừng nhận phản hồi",
      saveStatus: "Kiểm tra lịch sử để biết yêu cầu đã được lưu hay chưa.",
    };
  if (/STALE|EXPIRED|BUSY|REUSED/.test(code))
    return {
      category: "stale",
      label: "Dữ liệu hoặc bản xem trước đã thay đổi",
      saveStatus:
        "Yêu cầu chưa được áp dụng. Hãy kiểm tra lịch hiện tại rồi thử lại.",
    };
  if (/TIMEOUT/.test(code))
    return {
      category: "timeout",
      label: "AI phản hồi quá lâu",
      saveStatus: "Chưa nhận được xác nhận lưu từ hệ thống.",
    };
  if (/QUOTA|RATE_LIMIT/.test(code))
    return {
      category: "provider",
      label: "Dịch vụ AI đã đạt hạn mức",
      saveStatus: "Yêu cầu chưa được lưu. Vui lòng thử lại sau.",
    };
  if (/CONFLICT|OVERLAP/.test(code))
    return {
      category: "validation",
      label: "Xung đột lịch",
      saveStatus:
        "Yêu cầu chưa được lưu. Hãy chọn giờ khác hoặc chỉnh sự kiện đang trùng.",
    };
  if (
    /INVALID_INTENT|MISSING_TASK|INVALID_TASK|VALIDATION|CONFLICT|LIMIT|OUTSIDE|OVERLAP|SESSION_LENGTH|TASK_WORK_WINDOW/.test(
      code,
    ) ||
    failure.status === 400 ||
    failure.status === 422
  )
    return {
      category: "validation",
      label: "Yêu cầu chưa hợp lệ",
      saveStatus: "Yêu cầu chưa được lưu. Hãy chỉnh lại thông tin và thử lại.",
    };
  if (/AI_|QUOTA|RATE_LIMIT/.test(code))
    return {
      category: "provider",
      label: "Dịch vụ AI chưa hoàn tất yêu cầu",
      saveStatus: "Chưa có thay đổi lịch được xác nhận.",
    };
  if (code || failure.status)
    return {
      category: "server",
      label: "Hệ thống chưa hoàn tất yêu cầu",
      saveStatus:
        "Chưa nhận được xác nhận lưu. Hãy kiểm tra lịch sử trước khi thử lại.",
    };
  return {
    category: "transport",
    label: "Kết nối bị gián đoạn",
    saveStatus:
      "Chưa nhận được xác nhận lưu. Hãy kiểm tra lịch sử trước khi thử lại.",
  };
}
