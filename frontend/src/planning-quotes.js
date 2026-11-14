// Original encouragement written for PLANORA; selected by the user's local day.
export const planningQuotes = [
  "Một kế hoạch vừa sức hôm nay là nền tảng cho ngày mai nhẹ nhàng hơn.",
  "Bạn không cần làm mọi việc cùng lúc. Hãy bắt đầu với một việc quan trọng.",
  "Mỗi khoảng thời gian được dành đúng chỗ đều giúp bạn tiến thêm một bước.",
  "Kế hoạch tốt luôn có chỗ cho nghỉ ngơi và những điều bất ngờ.",
  "Một bước nhỏ có chủ đích vẫn là một bước tiến đáng ghi nhận.",
  "Dành thời gian cho điều quan trọng cũng là cách chăm sóc chính mình.",
  "Điều chỉnh kế hoạch là một phần của hành trình tiến bộ.",
  "Bạn có thể bắt đầu lại từ khoảng thời gian tiếp theo.",
  "Làm đều đặn từng chút một sẽ đưa bạn đến gần mục tiêu hơn.",
  "Hôm nay, hãy chọn nhịp độ giúp bạn có thể tiếp tục vào ngày mai.",
  "Một lịch trình có khoảng thở giúp bạn tập trung khi thật sự cần.",
  "Ghi việc ra, chọn ưu tiên, rồi dành sự chú ý cho bước đầu tiên.",
  "Tiến độ của bạn có giá trị, kể cả khi kế hoạch cần thay đổi.",
  "Hoàn thành một việc vừa sức cũng là một thành công của hôm nay.",
];

export function planningQuoteOfDay(date = new Date()) {
  const day = Math.floor(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()) / 86400000);
  return planningQuotes[((day % planningQuotes.length) + planningQuotes.length) % planningQuotes.length];
}
