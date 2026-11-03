import { useId } from "react";
import "./nora-calendar-kind.css";

export function NoraCalendarKind({ value, onChange, disabled, hasImage }) {
  const hint = useId();
  return (
    <div className="nora-calendar-kind">
      <label>
        Loại lịch
        <select
          aria-label="Loại lịch"
          aria-describedby={hint}
          value={hasImage ? "Event" : value}
          disabled={disabled || hasImage}
          onChange={(event) => onChange(event.target.value)}
        >
          <option value="Auto">NORA tự nhận diện</option>
          <option value="Event">Sự kiện cố định</option>
          <option value="WorkSession">Phiên công việc</option>
        </select>
      </label>
      <p id={hint}>
        {hasImage
          ? "Thời khóa biểu được đọc thành sự kiện cố định để giữ các giờ bận."
          : value === "Event"
            ? "Lịch học, họp, cuộc hẹn: có ngày và giờ bắt đầu/kết thúc. Không đưa vào ma trận Eisenhower."
            : value === "WorkSession"
              ? "Học bài, làm báo cáo: gắn với task, thời lượng và deadline. NORA tìm giờ phù hợp; bạn có thể chỉnh mức quan trọng trong task."
              : "NORA phân biệt lịch hẹn có giờ cố định với thời gian làm task. Bạn có thể chọn loại trước khi gửi."}
      </p>
    </div>
  );
}
