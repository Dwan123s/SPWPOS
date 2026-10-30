import { SessionTimer } from "./react-ui.jsx";
import { Select, nodeList } from "./react-ui.jsx";
import { availabilityEditor } from "./interactive.jsx";
import { StartSessionButton } from "./session-start.jsx";
import { CalendarTimeTag } from "./calendar-time.jsx";
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
export function sessionCard(s, calendarTime) {
  const upcoming = s.status === "Scheduled",
    active = ["Running", "Paused"].includes(s.status);
  const expired = active && new Date(s.end).getTime() <= Date.now();
  return (
    <div
      className={`session-card ${s.status === "Completed" ? "completed" : ""} ${calendarTime ? `calendar-timed-card calendar-time-${calendarTime}` : ""}`}
    >
      <div className="session-top">
        <h3>{s.taskName}</h3>
        {calendarTime && <CalendarTimeTag phase={calendarTime} />}
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
      {expired && (
        <p className="warning" role="status">
          Phiên đã hết khung lịch. Hãy kết thúc phiên để giữ thời gian đã làm và
          xếp lại phần còn lại.
        </p>
      )}
      <div className="reason">{reasons[s.reasonCode] || s.reasonCode}</div>
      <div className="actions">
        {upcoming && (
          <StartSessionButton session={s} workspace={state.workspace} />
        )}
        {s.status === "Running"
          ? button(
              "Tạm dừng",
              "session-pause",
              "small secondary",
              s.id,
              "pause",
            )
          : ""}
        {s.status === "Paused" && !expired
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
    "Khoảng này được đánh dấu bận. Khi tạo lịch lặp, mỗi lần xuất hiện có thể sửa hoặc xóa riêng.",
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
              WORK: "Công việc",
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
      <label className="check spaced">
        <input type="checkbox" name="isUrgent" defaultChecked={Boolean(event.isUrgent)} />
        Lịch khẩn
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
      {!event.id &&
        field(
          "Số lần lặp hàng tuần (1 = một lần)",
          "repeatWeeks",
          "number",
          1,
          'required min="1" max="12"',
        )}
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
              {e.overridesWeeklyAvailability
                ? "Giờ rảnh duy nhất trong ngày"
                : "Rảnh thêm"}
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
export function planModal(plan, simulation = false) {
  state.plan = plan;
  const confirmMove = plan.hasUrgentReplan && !plan.isApplied;
  const movedTaskIds = new Set(
    plan.sessions.filter((s) => !s.preserved &&
      ["URGENT_NEAR_DEADLINE_MOVE", "URGENT_FLEXIBLE_MOVE"].includes(s.reasonCode))
      .map((s) => s.taskId),
  );
  const windowText = (s) => `${dateFmt(s.start)} · ${timeFmt(s.start)}–${timeFmt(s.end)}`;
  modal(
    plan.isApplied ? "Lịch đã được lưu" : "Xem trước lịch",
    `${dateFmt(plan.start)} → ${dateFmt(plan.end)} · ${plan.sessions.length} phiên · ${plan.isApplied ? "đã cập nhật lịch" : "chưa thay đổi lịch"}.`,
    <>
      {!simulation && (
        <div role="status" className="info plan-save-status">
          {plan.isApplied
            ? "Lịch này đã được lưu. Mở Lịch làm việc để bắt đầu."
            : confirmMove
              ? "Đề xuất dời lịch để ưu tiên việc khẩn. Lịch hiện tại chưa thay đổi. Kiểm tra giờ cũ và giờ mới rồi bấm Xác nhận dời lịch. Hạn chót giữ nguyên."
            : plan.hasDeadlineProtection
              ? "Đề xuất đã dành thời gian cho việc khẩn hoặc gần hạn chưa có đủ lịch trước khi xếp việc mới. Lịch chưa thay đổi; kiểm tra các phiên rồi xác nhận kế hoạch."
            : "Đây là lịch xem trước, chưa thay đổi lịch hiện tại. Bấm Lưu lịch này để đưa các khoảng làm việc vào lịch."}
        </div>
      )}
      {confirmMove && movedTaskIds.size > 0 && (
        <section aria-label="Các công việc được đề xuất dời lịch">
          <h3>Đề xuất dời lịch</h3>
          {nodeList([...movedTaskIds].map((taskId) => {
            const task = state.workspace.tasks.find((t) => t.id === taskId);
            const kept = new Set(plan.sessions.filter((s) => s.preserved).map((s) => s.id));
            const before = (state.workspace.sessions || []).filter((s) => s.taskId === taskId &&
              s.status === "Scheduled" && new Date(s.start) >= new Date(plan.start) &&
              new Date(s.start) < new Date(plan.end) && !kept.has(s.id));
            const after = plan.sessions.filter((s) => s.taskId === taskId && !s.preserved);
            return <div className="info spaced">
              <strong>{task?.name || "Công việc"}</strong>
              <p>Giờ cũ: {before.map(windowText).join("; ") || "Xem lịch đã lưu"}</p>
              <p>Giờ mới: {after.map(windowText).join("; ")}</p>
              {task?.deadline && <p>Hạn chót giữ nguyên: {dateFmt(task.deadline)} · {timeFmt(task.deadline)}</p>}
            </div>;
          }))}
        </section>
      )}
      {nodeList(
        plan.warnings.map((w) => (
          <div className="warning">
            <strong>{w.taskName}</strong>
            <br />
            {w.message}
            {w.unscheduledMinutes > 0 && (
              <> · {minutes(w.unscheduledMinutes)} chưa xếp</>
            )}
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
          ? button(confirmMove ? "Xác nhận dời lịch" : plan.hasDeadlineProtection ? "Xác nhận kế hoạch" : "Lưu lịch này", "apply-plan", "primary", plan.id, "check")
          : ""}
        {!simulation && plan.isApplied
          ? button("Mở Hôm nay để làm việc", "nav", "primary", "today", "play")
          : ""}
      </div>
    </>,
  );
}
