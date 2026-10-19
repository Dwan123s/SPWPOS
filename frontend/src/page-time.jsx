import { SessionTimer } from "./react-ui.jsx";
import { Select, nodeList } from "./react-ui.jsx";
import { availabilityEditor } from "./interactive.jsx";
import { StartSessionButton } from "./session-start.jsx";
import {
  state,
  button,
  badge,
  statusLabels,
  reasons,
  dateFmt,
  timeFmt,
  dayKey,
  localInput,
  field,
  modal,
  minutes,
  timeValue,
} from "./ui.jsx";
export function timePage() {
  const w = state.workspace,
    p = w.preference;
  return (
    <div className="equal-columns">
      <div className="stack">
        <section className="card">
          <div className="card-header">
            <div>
              <h2>Thời gian rảnh hàng tuần</h2>
              <p>
                Chỉ những khoảng này được dùng để lên lịch. Mỗi ngày có thể thêm
                nhiều khoảng, ví dụ 07:00–09:00 và 15:00–17:00.
              </p>
            </div>
          </div>
          <form data-form="availability">
            <p className="reason">
              Giờ được nhập theo múi giờ {p.timeZoneId}. Sau khi lưu, bấm biểu
              tượng lịch cạnh công việc để xếp vào giờ rảnh.
            </p>
            {availabilityEditor(w.availability)}
            <div className="form-error" />
            <div className="form-actions">
              {button("Thêm khoảng", "add-slot", "secondary", "", "plus")}
              <button type="submit" className="btn secondary">
                Lưu lịch rảnh
              </button>
              {w.tasks.some(
                (task) => !["Completed", "Cancelled"].includes(task.status),
              ) && (
                <button
                  type="submit"
                  className="btn primary"
                  data-next-action="plan"
                >
                  Lưu giờ rảnh & xem lịch
                </button>
              )}
            </div>
            <p className="reason spaced">
              {p.autoReschedule !== false
                ? "Khi lưu, các phiên tương lai linh hoạt bị xung đột sẽ được xếp lại. Phiên đã khóa và đang thực hiện được giữ nguyên."
                : "Tự điều chỉnh đang tắt. Lưu giờ rảnh rồi xem lịch đề xuất để xử lý các phiên bị ảnh hưởng."}
            </p>
          </form>
        </section>
        <section className="card">
          <div className="card-header">
            <div>
              <h2>Ngoại lệ theo ngày</h2>
              <p>Ngày nghỉ, lịch bận hoặc giờ rảnh thêm.</p>
            </div>
            {button("Thêm", "new-exception", "small secondary", "", "plus")}
          </div>
          {nodeList(
            w.exceptions.map((e) => (
              <div className="list-line">
                <div>
                  <strong>
                    {e.date}
                    {" · "}
                    {e.isAvailable
                      ? e.overridesWeeklyAvailability
                        ? "Chỉ rảnh trong khoảng này"
                        : "Rảnh"
                      : "Bận"}
                  </strong>
                  <p>
                    {timeValue(e.startMinute)}
                    {" – "}
                    {timeValue(e.endMinute)}
                    {" · "}
                    {e.note}
                  </p>
                </div>
                {button("Sửa", "edit-exception", "small ghost", e.id)}
              </div>
            )),
          ) || (
            <p className="muted">
              <small>Chưa có ngoại lệ</small>
            </p>
          )}
        </section>
        <section className="card">
          <div className="card-header">
            <h2>Sự kiện cố định</h2>
            {button("Thêm", "new-event", "small secondary", "", "plus")}
          </div>
          {nodeList(
            w.events.map((e) => (
              <div className="list-line">
                <div>
                  <strong>{e.name}</strong>
                  <p>
                    {dateFmt(e.start)} {timeFmt(e.start)}
                    {" – "}
                    {dateFmt(e.end)} {timeFmt(e.end)}
                  </p>
                </div>
                {button("Sửa", "edit-event", "small ghost", e.id)}
              </div>
            )),
          ) || (
            <p className="muted">
              <small>Chưa có sự kiện</small>
            </p>
          )}
        </section>
      </div>
      <section className="card">
        <div className="card-header">
          <div>
            <h2>Cách bạn muốn làm việc</h2>
            <p>Giới hạn phiên và thời gian nghỉ.</p>
          </div>
        </div>
        <form data-form="preferences">
          <div className="form-grid">
            <label className="field wide">
              Múi giờ
              <Select name="timeZoneId">
                {nodeList(
                  [
                    ...new Set([
                      "Asia/Ho_Chi_Minh",
                      "Asia/Bangkok",
                      "Asia/Tokyo",
                      "UTC",
                      "Europe/London",
                      "America/New_York",
                      p.timeZoneId,
                    ]),
                  ].map((z) => (
                    <option value={z} selected={z === p.timeZoneId}>
                      {z}
                    </option>
                  )),
                )}
              </Select>
            </label>
            {field(
              "Phiên ngắn nhất (phút)",
              "minimumSessionMinutes",
              "number",
              p.minimumSessionMinutes,
              'required min="5" max="480"',
            )}
            {field(
              "Phiên dài nhất (phút)",
              "maximumSessionMinutes",
              "number",
              p.maximumSessionMinutes,
              'required min="5" max="480"',
            )}
            {field(
              "Số ngày lập kế hoạch",
              "planningHorizonDays",
              "number",
              p.planningHorizonDays || 14,
              'required min="1" max="30"',
            )}
            <label className="field">
              Buổi làm việc ưu tiên
              <Select name="preferredPeriod">
                {nodeList(
                  Object.entries({
                    NONE: "Theo giờ bên dưới",
                    MORNING: "Sáng",
                    AFTERNOON: "Chiều",
                    EVENING: "Tối",
                  }).map(([code, label]) => (
                    <option
                      value={code}
                      selected={code === (p.preferredPeriod || "NONE")}
                    >
                      {label}
                    </option>
                  )),
                )}
              </Select>
            </label>
            {field(
              "Nghỉ giữa phiên (phút)",
              "breakMinutes",
              "number",
              p.breakMinutes,
              'required min="0" max="120"',
            )}
            {field(
              "Làm việc tối đa mỗi ngày (phút)",
              "maximumDailyMinutes",
              "number",
              p.maximumDailyMinutes ?? 480,
              'required min="5" max="1440"',
            )}
            <label className="field wide">
              Khi thời gian thay đổi
              <Select name="autoReschedule">
                <option value="true" selected={p.autoReschedule !== false}>
                  Tự xếp lại phiên bị ảnh hưởng
                </option>
                <option value="false" selected={p.autoReschedule === false}>
                  Thông báo để tôi tự xem lại lịch
                </option>
              </Select>
            </label>
            {field(
              "Giờ làm việc ưu tiên từ",
              "preferredStart",
              "time",
              timeValue(p.preferredStartMinute),
              "required",
            )}
            {field(
              "Đến (HH:mm)",
              "preferredEnd",
              "text",
              timeValue(p.preferredEndMinute),
              'required pattern="(?:[01][0-9]|2[0-3]):[0-5][0-9]|24:00"',
            )}
          </div>
          <p className="reason spaced">
            Giờ ưu tiên giúp sắp xếp lịch. Những phiên đã khóa được bảo vệ khi
            bạn thay đổi lịch rảnh.
          </p>
          <div className="form-error" />
          <div className="form-actions">
            <button type="submit" className="btn primary">
              Lưu tùy chọn
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}
