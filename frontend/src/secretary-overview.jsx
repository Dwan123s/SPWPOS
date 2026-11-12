import { askNora } from "./agent-handoff.js";
import { state, dateFmt, timeFmt, timeValue } from "./ui.jsx";
import { AvailabilityPolicyCard } from "./availability-policy.jsx";

export function SecretaryTime({ onWorkspaceChanged }) {
  const w = state.workspace;
  return (
    <div className="stack">
      <section className="card">
        <h2>Thời gian biểu của bạn</h2>
        <p>
          NORA đọc lịch cố định và giờ sinh hoạt bạn cung cấp để tìm thời gian
          cho công việc. Bạn chỉ cần gửi ảnh, nói hoặc nhắn yêu cầu.
        </p>
        <div className="form-actions">
          <button
            className="btn primary"
            onClick={() =>
              askNora(
                "Tôi muốn gửi hoặc cập nhật thời gian biểu cố định. Hãy phân tích lịch trước và hỏi những thông tin còn thiếu, không tự tạo giờ rảnh.",
              )
            }
          >
            Gửi thời gian biểu
          </button>
          <button
            className="btn secondary"
            onClick={() =>
              askNora(
                "Tôi muốn báo thay đổi giờ bận hoặc giờ có thể làm việc. Hãy hỏi tôi và cập nhật quanh thời gian biểu cố định.",
              )
            }
          >
            Báo thay đổi với NORA
          </button>
        </div>
      </section>
      <AvailabilityPolicyCard
        autoPreview={!w.availabilityPolicy?.hasDailyRoutine}
        onWorkspaceChanged={onWorkspaceChanged}
      />
      <section className="card">
        <h2>Lịch cố định đã ghi nhận</h2>
        {(w.events || []).length ? (
          w.events.map((event) => (
            <div className="list-line" key={event.id}>
              <div>
                <strong>{event.name}</strong>
                <p>
                  {dateFmt(event.start)} · {timeFmt(event.start)}–
                  {timeFmt(event.end)}
                </p>
              </div>
            </div>
          ))
        ) : (
          <p>Chưa có thời gian biểu. Gửi lịch cho NORA để bắt đầu.</p>
        )}
      </section>
      <section className="card">
        <h2>Giờ có thể làm</h2>
        {(w.availability || []).length ? (
          w.availability.map((slot, i) => (
            <p key={i}>
              {slot.dayOfWeek === 0 ? "Chủ nhật" : `Thứ ${slot.dayOfWeek + 1}`}{" "}
              · {timeValue(slot.startMinute)}–{timeValue(slot.endMinute)}
            </p>
          ))
        ) : w.availabilityPolicy?.confirmed ? (
          <p>
            Đang tính tự động từ chính sách đã xác nhận và lịch cố định theo
            ngày.
          </p>
        ) : (
          <p>
            NORA đã tự đề xuất lịch sinh hoạt và giờ rảnh ở trên. Xem và áp dụng
            để dùng các khoảng này khi xếp công việc.
          </p>
        )}
        {(w.exceptions || []).map((item) => (
          <p key={item.id}>
            {item.date} · {item.startMinute === item.endMinute ? "Không có giờ rảnh" :
              `${item.isAvailable ? "Có thể làm việc" : "Bận"} ${timeValue(item.startMinute)}–${timeValue(item.endMinute)}`}{" "}
            {item.note}
          </p>
        ))}
      </section>
    </div>
  );
}

export function SecretaryScenario() {
  return (
    <section className="card">
      <h2>Trao đổi với NORA trước khi nhận thêm việc</h2>
      <p>
        Mô tả công việc bạn đang cân nhắc. NORA đối chiếu lịch cố định, giờ rảnh
        và deadline; thời lượng ước lượng cần bạn đồng ý trước khi dùng.
      </p>
      <button
        className="btn primary"
        onClick={() =>
          askNora(
            "Tôi đang cân nhắc nhận thêm một việc. Hỏi tôi nội dung và hạn chót, đề xuất thời lượng nếu thiếu rồi giúp tôi đánh giá lịch hiện tại. Chỉ tư vấn, chưa tạo công việc hoặc thay đổi lịch.",
          )
        }
      >
        Nhờ NORA đánh giá
      </button>
    </section>
  );
}
