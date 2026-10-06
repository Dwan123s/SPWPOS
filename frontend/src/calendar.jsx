import { SessionTimer } from "./react-ui.jsx";
import { Select, nodeList } from "./react-ui.jsx";
import { availabilityEditor } from "./interactive.jsx";
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
export const sessionById = (id) =>
  state.workspace.sessions.find((s) => s.id === id);
export function sessionCard(s) {
  const upcoming = s.status === "Scheduled",
    active = ["Running", "Paused"].includes(s.status);
  return (
    <div
      className={`session-card ${s.status === "Completed" ? "completed" : ""}`}
    >
      <div className="session-top">
        <h3>{s.taskName}</h3>
        {badge(
          statusLabels[s.status],
          active ? "purple" : s.status === "Completed" ? "green" : "",
        )}
      </div>
      <div className="session-time">
        {timeFmt(s.start)}
        {" – "}
        {timeFmt(s.end)}
        {" · "}
        {minutes(Math.round((new Date(s.end) - new Date(s.start)) / 60000))}
        {s.isLocked ? " · Đã khóa" : ""}
      </div>
      {active ? <SessionTimer id={s.id} className="timer" /> : ""}
      <div className="reason">{reasons[s.reasonCode] || s.reasonCode}</div>
      <div className="actions">
        {upcoming
          ? button("Bắt đầu", "session-start", "small secondary", s.id, "play")
          : ""}
        {s.status === "Running"
          ? button(
              "Tạm dừng",
              "session-pause",
              "small secondary",
              s.id,
              "pause",
            )
          : ""}
        {s.status === "Paused"
          ? button(
              "Tiếp tục",
              "session-resume",
              "small secondary",
              s.id,
              "play",
            )
          : ""}
        {active
          ? button(
              "Xong phiên",
              "session-complete",
              "small primary",
              s.id,
              "check",
            )
          : ""}
        {upcoming || active
          ? button("Bỏ qua", "session-skip", "small ghost", s.id)
          : ""}
        {button("Chi tiết", "session-detail", "small ghost", s.id, "edit")}
      </div>
    </div>
  );
}
export { calendarPage } from "./schedule.jsx";
export function sessionDetail(id) {
  const s = sessionById(id);
  if (!s) return;
  modal(
    s.taskName,
    "Điều chỉnh lịch và xem thời gian thực tế.",
    <>
      {sessionCard(s)}
      <dl className="detail-list">
        <div>
          <dt>Ngày</dt>
          <dd>
            {dateFmt(s.start, {
              year: "numeric",
            })}
          </dd>
        </div>
        <div>
          <dt>Thực tế</dt>
          <dd>{minutes(s.actualMinutes)}</dd>
        </div>
      </dl>
      {s.status === "Scheduled" ? (
        <>
          <form data-form="session-edit" data-id={id}>
            <div className="form-grid">
              {field(
                "Bắt đầu",
                "start",
                "datetime-local",
                localInput(s.start),
                "required",
              )}
              {field(
                "Kết thúc",
                "end",
                "datetime-local",
                localInput(s.end),
                "required",
              )}
            </div>
            <div className="form-error" />
            <div className="form-actions">
              {button(
                s.isLocked ? "Mở khóa" : "Khóa phiên",
                s.isLocked ? "session-unlock" : "session-lock",
                "secondary",
                id,
                "lock",
              )}
              <button
                type="submit"
                className="btn primary"
                disabled={s.isLocked}
              >
                Lưu thời gian
              </button>
            </div>
          </form>
          {!s.isLocked ? (
            <form data-form="session-split" data-id={id} className="spaced">
              {field(
                "Chia phiên: phần đầu (phút)",
                "firstMinutes",
                "number",
                25,
                'required min="5" max="480"',
              )}
              <div className="form-error" />
              <div className="form-actions">
                <button className="btn secondary" type="submit">
                  Chia thành hai phiên
                </button>
              </div>
            </form>
          ) : (
            ""
          )}
        </>
      ) : (
        ""
      )}
      {s.status === "Completed" ? (
        <form data-form="actual-duration" data-id={id}>
          {field(
            "Sửa thời gian thực tế (phút)",
            "actualMinutes",
            "number",
            s.actualMinutes,
            'required min="0" max="10080"',
          )}
          <div className="form-error" />
          <div className="form-actions">
            <button type="submit" className="btn secondary">
              Lưu thời gian thực tế
            </button>
          </div>
        </form>
      ) : (
        ""
      )}
      {button("Lịch sử timer", "execution-history", "ghost", s.id)}
      {button("Xem công việc", "task-detail", "ghost", s.taskId)}
    </>,
  );
}
export function eventModal(event = {}) {
  const start = event.start || new Date(Date.now() + 3600000).toISOString(),
    end = event.end || new Date(Date.now() + 7200000).toISOString();
  modal(
    event.id ? "Sửa sự kiện" : "Thêm sự kiện cố định",
    "Khoảng thời gian này sẽ không được dùng để lập lịch.",
    <form data-form="event" data-id={event.id || ""}>
      {field(
        "Tên sự kiện",
        "name",
        "text",
        event.name || "",
        'required maxlength="200"',
      )}
      <label className="field spaced">
        Loại sự kiện
        <Select name="eventType">
          {nodeList(
            Object.entries({
              MEETING: "Cuộc họp",
              CLASS: "Lớp học",
              APPOINTMENT: "Cuộc hẹn",
              OTHER: "Khác",
            }).map(([code, label]) => (
              <option
                value={code}
                selected={code === (event.eventType || "OTHER")}
              >
                {label}
              </option>
            )),
          )}
        </Select>
      </label>
      <label className="field spaced">
        Mô tả
        <textarea
          name="description"
          maxLength="1000"
          defaultValue={event.description || ""}
        />
      </label>
      <div className="form-grid spaced">
        {field(
          "Bắt đầu",
          "start",
          "datetime-local",
          localInput(start),
          "required",
        )}
        {field(
          "Kết thúc",
          "end",
          "datetime-local",
          localInput(end),
          "required",
        )}
      </div>
      <div className="form-error" />
      <div className="form-actions">
        {event.id
          ? button("Xóa sự kiện", "delete-event", "danger", event.id, "trash")
          : ""}
        <button type="submit" className="btn primary">
          Lưu sự kiện
        </button>
      </div>
    </form>,
  );
}
export function exceptionModal(e = {}) {
  modal(
    e.id ? "Sửa ngoại lệ" : "Thêm ngoại lệ thời gian",
    "Điều chỉnh một ngày riêng, bổ sung thời gian rảnh hoặc đánh dấu bận.",
    <form data-form="exception" data-id={e.id || ""}>
      <div className="form-grid">
        {field(
          "Ngày",
          "date",
          "date",
          e.date || dayKey(Date.now()),
          "required",
        )}
        <label className="field">
          Loại
          <Select name="isAvailable">
            <option value="false" selected={!e.isAvailable}>
              Bận
            </option>
            <option value="true" selected={e.isAvailable}>
              Rảnh thêm
            </option>
          </Select>
        </label>
        {field(
          "Từ",
          "start",
          "time",
          timeValue(e.startMinute ?? 480),
          "required",
        )}
        {field(
          "Đến (HH:mm)",
          "end",
          "text",
          timeValue(e.endMinute ?? 1080),
          'required pattern="(?:[01][0-9]|2[0-3]):[0-5][0-9]|24:00"',
        )}
        <div className="wide">
          {field("Ghi chú", "note", "text", e.note || "", 'maxlength="300"')}
        </div>
      </div>
      <div className="form-error" />
      <div className="form-actions">
        {e.id ? button("Xóa", "delete-exception", "danger", e.id, "trash") : ""}
        <button className="btn primary" type="submit">
          Lưu ngoại lệ
        </button>
      </div>
    </form>,
  );
}
export function availabilityRow(
  slot = {
    dayOfWeek: 1,
    startMinute: 480,
    endMinute: 1080,
  },
) {
  return (
    <div className="availability-row">
      <Select className="filter" name="day" aria-label="Ngày trong tuần">
        <option value="1" selected={slot.dayOfWeek === 1}>
          Thứ 2
        </option>
        {nodeList(
          [2, 3, 4, 5, 6, 0].map((d) => (
            <option value={d} selected={d === slot.dayOfWeek}>
              {d === 0 ? "CN" : `Thứ ${d + 1}`}
            </option>
          )),
        )}
      </Select>
      <input
        className="filter"
        name="start"
        type="time"
        defaultValue={timeValue(slot.startMinute)}
        required={true}
        aria-label="Bắt đầu"
      />
      <input
        className="filter"
        name="end"
        type="text"
        defaultValue={timeValue(slot.endMinute)}
        required={true}
        pattern="(?:[01][0-9]|2[0-3]):[0-5][0-9]|24:00"
        aria-label="Kết thúc (HH:mm)"
      />
      {button("", "remove-slot", "icon-only ghost", "", "close")}
    </div>
  );
}
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
              <p>Chỉ những khoảng này được dùng để lên lịch.</p>
            </div>
          </div>
          <form data-form="availability">
            {availabilityEditor(w.availability)}
            <div className="form-error" />
            <div className="form-actions">
              {button("Thêm khoảng", "add-slot", "secondary", "", "plus")}
              <button type="submit" className="btn primary">
                Lưu lịch rảnh
              </button>
            </div>
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
                    {e.isAvailable ? "Rảnh" : "Bận"}
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
            <div />
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
export function planModal(plan, simulation = false) {
  state.plan = plan;
  modal(
    "Xem trước kế hoạch",
    `${dateFmt(plan.start)} → ${dateFmt(plan.end)} · ${plan.sessions.length} phiên · chưa thay đổi lịch.`,
    <>
      {nodeList(
        plan.warnings.map((w) => (
          <div className="warning">
            <strong>{w.taskName}</strong>
            <br />
            {w.message}
            {" · "}
            {minutes(w.unscheduledMinutes)}
            {" chưa xếp"}
          </div>
        )),
      )}
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Công việc</th>
              <th>Ngày / giờ</th>
              <th>Lý do</th>
            </tr>
          </thead>
          <tbody>
            {nodeList(
              plan.sessions.map((s) => (
                <tr>
                  <td>
                    {state.workspace.tasks.find((t) => t.id === s.taskId)
                      ?.name || "Công việc giả định"}
                    {s.preserved ? (
                      <>
                        <br />
                        {badge("Giữ nguyên")}
                      </>
                    ) : (
                      ""
                    )}
                  </td>
                  <td>
                    {dateFmt(s.start)}
                    {" · "}
                    {timeFmt(s.start)}
                    {" – "}
                    {timeFmt(s.end)}
                  </td>
                  <td>{reasons[s.reasonCode] || s.reasonCode}</td>
                </tr>
              )),
            )}
          </tbody>
        </table>
      </div>
      {!plan.sessions.length ? (
        <div className="info spaced">
          Chưa có phiên phù hợp. Kiểm tra thời gian rảnh, deadline và thời lượng
          công việc.
        </div>
      ) : (
        ""
      )}
      <ul className="explanation-list">
        {nodeList(plan.explanations.map((x) => <li>{x}</li>))}
      </ul>
      <div className="form-actions">
        {button("Đóng", "close-modal", "ghost")}
        {!simulation && plan.id !== "00000000-0000-0000-0000-000000000000" ? (
          <>
            {button("Các quyết định", "plan-decisions", "secondary", plan.id)}
            {!plan.isApplied && plan.status !== "DISCARDED"
              ? button("Bỏ đề xuất", "discard-plan", "ghost", plan.id)
              : ""}
          </>
        ) : (
          ""
        )}
        {!simulation && !plan.isApplied && plan.status !== "DISCARDED"
          ? button(
              "Áp dụng kế hoạch",
              "apply-plan",
              "primary",
              plan.id,
              "check",
            )
          : ""}
      </div>
    </>,
  );
}
