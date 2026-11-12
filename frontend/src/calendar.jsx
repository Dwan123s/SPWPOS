import { useId, useState } from "react";
import { SessionTimer } from "./react-ui.jsx";
import { Select, nodeList } from "./react-ui.jsx";
import { sessionStartIssue, sessionStartOpensAt } from "./session-start.jsx";
import { CalendarClock, CalendarTimeTag, calendarPhase } from "./calendar-time.jsx";
import { askNora, secretaryMode } from "./agent-handoff.js";
import {
  state,
  button,
  badge,
  icon,
  statusLabels,
  reasons,
  dateFmt,
  timeFmt,
  dayKey,
  localInput,
  toUtc,
  toast,
  field,
  modal,
  minutes,
  timeValue,
} from "./ui.jsx";
import "./calendar-event-form.css";
import "./calendar-detail.css";

const eventTypes = {
  MEETING: "Cuộc họp",
  CLASS: "Lịch học",
  APPOINTMENT: "Cuộc hẹn",
  WORK: "Ca làm",
  OTHER: "Khác",
};
export const sessionById = (id) =>
  state.workspace.sessions.find((s) => s.id === id);
export const canDeleteSession = (session) => session?.status === "Scheduled" &&
  !session.actualSeconds && !session.actualMinutes && !session.segmentStartedAt;
function SessionSummary({ session: s, task, duration, now }) {
  const upcoming = s.status === "Scheduled",
    active = ["Running", "Paused"].includes(s.status);
  const ended = Date.parse(s.end) <= now;
  const issue = upcoming ? sessionStartIssue(s, state.workspace, now) : null;
  const hintId = useId();
  return (
    <section className="calendar-entry-summary calendar-entry-task" aria-label="Phiên làm việc">
      <div className="calendar-entry-summary-header">
        <strong>{dateFmt(s.start, { year: "numeric" })} · {timeFmt(s.start)}–{timeFmt(s.end)}</strong>
        {badge(statusLabels[s.status], active ? "purple" : s.status === "Completed" ? "green" : "")}
      </div>
      <dl className="calendar-entry-metrics">
        <div><dt>Thời lượng phiên</dt><dd>{minutes(duration)}</dd></div>
        {task?.remainingMinutes != null && (
          <div><dt>Công việc còn lại</dt><dd>{minutes(task.remainingMinutes)}</dd></div>
        )}
        <div><dt>Đã thực hiện</dt><dd>{minutes(s.actualMinutes ?? 0)}</dd></div>
      </dl>
      {s.isLocked && <p className="calendar-entry-note">Đã khóa phiên</p>}
      {active && <SessionTimer id={s.id} className="timer" />}
      {(reasons[s.reasonCode] || s.reasonCode) && <p className="calendar-entry-note">{reasons[s.reasonCode] || s.reasonCode}</p>}
      <div className="calendar-entry-actions">
        {upcoming && !ended && <button type="button" className="btn small primary"
          data-action="session-start" data-id={s.id} disabled={Boolean(issue)} aria-describedby={issue ? hintId : undefined}>
          {icon("play")} Bắt đầu
        </button>}
        {s.status === "Running" && button("Tạm dừng", "session-pause", "small secondary", s.id, "pause")}
        {s.status === "Paused" && !ended && button("Tiếp tục", "session-resume", "small secondary", s.id, "play")}
        {active && button("Xong phiên", "session-complete", "small primary", s.id, "check")}
        {ended && ["Scheduled", "NeedsReview", "Running", "Paused", "Completed", "Missed"].includes(s.status) &&
          button("Ghi kết quả phiên", "session-outcome", "small secondary", s.id)}
        {(upcoming || active) && button("Bỏ qua", "session-skip", "small ghost", s.id)}
      </div>
      {issue && <p id={hintId} className="calendar-entry-note" role="status">{issue}</p>}
      {active && ended && <p className="calendar-entry-note" role="status">Phiên đã hết giờ. Bấm Xong phiên để lưu thời gian đã làm.</p>}
    </section>
  );
}
export function sessionDetail(id) {
  const s = sessionById(id);
  if (!s) return;
  const task = state.workspace.tasks.find((item) => item.id === s.taskId);
  const canSplit = task?.splittable === true && !task.requiresContinuousSession;
  const duration = Math.round((Date.parse(s.end) - Date.parse(s.start)) / 60000);
  const minimumSplit = Math.max(task?.minimumSessionMinutes ?? 25, state.workspace.preference?.minimumSessionMinutes ?? 25);
  const boundaries = [sessionStartOpensAt(s), ...[...(state.workspace.events || []), ...state.workspace.sessions]
    .flatMap((entry) => [Date.parse(entry.start), Date.parse(entry.end)])];
  modal(
    s.taskName,
    "Phiên làm việc",
    <>
      <CalendarClock boundaries={boundaries}>
        {(now) => <SessionSummary session={s} task={task} duration={duration} now={now} />}
      </CalendarClock>
      {s.status === "Scheduled" && !secretaryMode() ? (
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
          {!s.isLocked && canSplit && duration >= 2 * minimumSplit && Date.parse(s.start) >= Date.now() ? (
            <form data-form="session-split" data-id={id} className="spaced">
              {field(
                "Chia phiên: phần đầu (phút)",
                "firstMinutes",
                "number",
                minimumSplit,
                `required min="${minimumSplit}" max="${Math.min(480, duration - minimumSplit)}"`,
              )}
              <div className="form-error" />
              <div className="form-actions">
                <button className="btn secondary" type="submit">
                  Chia thành hai phiên
                </button>
              </div>
            </form>
          ) : !canSplit ? (
            <p className="reason spaced">
              Hoạt động này cần một phiên liên tục đủ thời lượng, không thể chia nhỏ.
            </p>
          ) : null}
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
      <footer className="calendar-detail-footer">
        <div className="calendar-entry-actions">
          {button("Lịch sử phiên", "execution-history", "ghost", s.id)}
          {button("Xem công việc", "task-detail", "secondary", s.taskId)}
        </div>
        {canDeleteSession(s) && <div className="calendar-entry-actions">
          {s.isLocked && secretaryMode() && button("Mở khóa phiên", "session-unlock", "secondary", s.id, "lock")}
          <button type="button" className="btn danger" data-action="delete-session" data-id={s.id}
            disabled={s.isLocked} title={s.isLocked ? "Mở khóa phiên trước khi xóa" : "Xóa phiên này, giữ công việc và deadline"}>
            Xóa phiên khỏi lịch
          </button>
        </div>}
      </footer>
    </>,
  );
}
export function eventDetail(id) {
  const event = state.workspace.events.find((entry) => entry.id === id);
  if (!event) return;
  modal(event.name, "Lịch Trình · Thao tác chỉ áp dụng cho lần xuất hiện này.", <>
    <CalendarClock boundaries={[Date.parse(event.start), Date.parse(event.end)]}>
      {(now) => {
        const phase = calendarPhase(event.start, event.end, now);
        return (
          <section className={`calendar-entry-summary calendar-entry-event calendar-time-${phase}`} aria-label="Thông tin Lịch Trình">
            <div className="calendar-entry-summary-header">
              <strong>{eventTypes[event.eventType] || eventTypes.OTHER}</strong>
              <CalendarTimeTag phase={phase} />
            </div>
            <dl className="calendar-entry-metrics">
              <div><dt>Bắt đầu</dt><dd>{dateFmt(event.start, { year: "numeric" })} · {timeFmt(event.start)}</dd></div>
              <div><dt>Kết thúc</dt><dd>{dateFmt(event.end, { year: "numeric" })} · {timeFmt(event.end)}</dd></div>
              <div><dt>Thời lượng</dt><dd>{minutes(Math.round((Date.parse(event.end) - Date.parse(event.start)) / 60000))}</dd></div>
            </dl>
          </section>
        );
      }}
    </CalendarClock>
    {event.description && <p className="info" style={{ whiteSpace: "pre-wrap" }}>{event.description}</p>}
    <div className="form-actions">
      {button("Xóa lịch", "delete-event", "danger", event.id, "trash")}
      {button("Chỉnh sửa lịch", "edit-event", "secondary", event.id, "edit")}
      {button("Đóng", "close-modal", "ghost")}
    </div>
  </>);
}
export function eventModal(event = {}, { manual = true } = {}) {
  const start = event.start || new Date(Date.now() + 3600000).toISOString(),
    end = event.end || new Date(Date.now() + 7200000).toISOString();
  if (!event.id && Date.parse(start) <= Date.now()) {
    toast("Không thể tạo lịch trong quá khứ. Hãy chọn khoảng giờ sắp tới.", "error");
    return;
  }
  modal(
    event.id ? "Chỉnh sửa Lịch Trình" : "Thêm Lịch Trình",
    event.id ? "Thay đổi áp dụng cho lần xuất hiện này." : "Giữ khung giờ này cho lịch học, ca làm hoặc cuộc hẹn. Planora sẽ xếp công việc quanh giờ bận.",
    <BusyEventForm event={event} start={start} end={end} manual={manual} />,
  );
}

function BusyEventForm({ event, start, end, manual }) {
  const [repeat, setRepeat] = useState(event.repeatWeeks > 1);
  const [range, setRange] = useState({ start: localInput(start), end: localInput(end) });
  let duration = 0;
  try {
    duration = Math.round((Date.parse(toUtc(range.end)) - Date.parse(toUtc(range.start))) / 60000);
  } catch {
    // A partially entered time has no duration yet.
  }
  function handoff(form) {
    const data = new FormData(form);
    const type = eventTypes[data.get("eventType")];
    const name = String(data.get("name") || "").trim();
    const description = String(data.get("description") || "").trim();
    const localDateTime = (key) => String(data.get(key) || "").replace("T", " ");
    const from = localDateTime("start");
    const until = localDateTime("end");
    askNora([
      "Chỉ xem trước một sự kiện cố định để giữ khung giờ này. Chờ tôi xác nhận trước khi lưu.",
      name && `Tên lịch: ${name}.`,
      type && type !== eventTypes.OTHER && `Loại lịch: ${type}.`,
      from && `Bắt đầu: ${from}.`,
      until && `Kết thúc: ${until}.`,
      repeat && data.get("repeatWeeks") && `Lặp hằng tuần, tổng ${data.get("repeatWeeks")} lần tính cả lần đầu.`,
      data.has("isUrgent") && "Đây là lịch khẩn.",
      description && `Ghi chú: ${description}`,
    ].filter(Boolean).join("\n"));
  }
  return (
    <form className="busy-event-form" data-form="event" data-id={event.id || ""}>
      {field("Tên Lịch Trình", "name", "text", event.name || "",
        { required: true, maxLength: 200, autoFocus: manual, placeholder: "Ví dụ: Ca làm chiều" })}
      <div className="form-grid">
        <label className="field">
          Bắt đầu
          <input type="datetime-local" name="start" required value={range.start}
            min={event.id ? undefined : localInput(Math.ceil((Date.now() + 1) / 60000) * 60000)}
            onChange={(e) => setRange({ ...range, start: e.target.value })} />
        </label>
        <label className="field">
          Kết thúc
          <input type="datetime-local" name="end" required value={range.end}
            onChange={(e) => setRange({ ...range, end: e.target.value })} />
        </label>
      </div>
      {duration > 0 && <p className="busy-event-hint">Thời lượng {minutes(duration, "long")}</p>}
      <div className="form-grid">
        <label className="field">
          Loại lịch
          <Select name="eventType" defaultValue={event.eventType || "OTHER"}>
            {Object.entries(eventTypes).map(([code, label]) => <option value={code} key={code}>{label}</option>)}
          </Select>
        </label>
        {!event.id && (
          <label className="field">
            Lặp lại
            <select value={repeat ? "weekly" : "once"} onChange={(e) => setRepeat(e.target.value === "weekly")}>
              <option value="once">Không lặp</option>
              <option value="weekly">Hằng tuần</option>
            </select>
          </label>
        )}
      </div>
      {!event.id && repeat && (
        <div className="busy-event-repeat">
          {field("Tổng số lần", "repeatWeeks", "number", event.repeatWeeks > 1 ? event.repeatWeeks : 2,
            { required: true, min: 2, max: 12 })}
          <p className="busy-event-hint">Tính cả lần đầu. Mỗi lần có thể sửa hoặc xóa riêng.</p>
        </div>
      )}
      <label className="field">
        Ghi chú (tùy chọn)
        <textarea name="description" maxLength={1000} rows={3} defaultValue={event.description || ""} />
      </label>
      <label className="busy-event-urgent">
        <input type="checkbox" name="isUrgent" defaultChecked={Boolean(event.isUrgent)} aria-describedby="busy-event-urgent-hint" />
        <span>Lịch khẩn</span>
      </label>
      <p id="busy-event-urgent-hint" className="busy-event-hint">Đánh dấu ưu tiên khi xem phương án xử lý trùng lịch.</p>
      <div className="form-error" role="alert" />
      <div className="form-actions">
        {event.id && button("Xóa lịch", "delete-event", "danger", event.id, "trash")}
        {state.noraAvailable && !event.id && <button type="button" className="btn ghost" onClick={(e) => handoff(e.currentTarget.form)}>Nhờ NORA</button>}
        <button type="submit" className="btn primary">Lưu Lịch Trình</button>
      </div>
    </form>
  );
}
export function exceptionModal(e = {}) {
  if (secretaryMode()) {
    askNora(e.id ? `Tôi muốn điều chỉnh khoảng ${e.isAvailable ? "rảnh" : "bận"} ngày ${e.date} (mã ${e.id}). Hỏi tôi thay đổi cần thực hiện.`
      : "Tôi muốn cập nhật giờ bận hoặc giờ có thể làm việc. Hỏi tôi thông tin còn thiếu rồi cấu hình quanh thời gian biểu.");
    return;
  }
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
export function planModal(plan, simulation = false) {
  state.plan = plan;
  const blocked = plan.strict && plan.warnings.some((w) => w.unscheduledMinutes > 0 || ["SOFT_DEADLINE_EXCEEDED", "DEADLINE_CONFLICT"].includes(w.code));
  const confirmMove = plan.hasUrgentReplan && !plan.isApplied && !blocked;
  const movedTaskIds = new Set(
    plan.sessions.filter((s) => !s.preserved &&
      ["URGENT_NEAR_DEADLINE_MOVE", "URGENT_FLEXIBLE_MOVE"].includes(s.reasonCode))
      .map((s) => s.taskId),
  );
  const kept = new Set(plan.sessions.filter((s) => s.preserved).map((s) => s.id));
  const previous = (state.workspace.sessions || []).filter((s) =>
    s.status === "Scheduled" && new Date(s.start) >= new Date(plan.start) &&
    new Date(s.start) < new Date(plan.end) && !kept.has(s.id));
  if (plan.strict) for (const row of previous) movedTaskIds.add(row.taskId);
  const windowText = (s) => `${dateFmt(s.start)} · ${timeFmt(s.start)}–${timeFmt(s.end)}`;
  modal(
    plan.isApplied ? "Lịch đã được lưu" : "Xem trước lịch",
    `${dateFmt(plan.start)} → ${dateFmt(plan.end)} · ${plan.sessions.length} phiên · ${plan.isApplied ? "đã cập nhật lịch" : "chưa thay đổi lịch"}.`,
    <>
      {!simulation && (
        <div role="status" className="info plan-save-status">
          {plan.isApplied
            ? "Lịch này đã được lưu. Mở Lịch làm việc để bắt đầu."
            : blocked ? "Chưa thể xếp đủ thời gian trước hạn. Lịch hiện tại được giữ nguyên."
            : confirmMove
              ? "Đề xuất dời lịch để ưu tiên việc khẩn. Lịch hiện tại chưa thay đổi. Kiểm tra giờ cũ và giờ mới rồi bấm Xác nhận dời lịch. Hạn chót giữ nguyên."
            : plan.hasDeadlineProtection
              ? "Đề xuất đã dành thời gian cho việc khẩn hoặc gần hạn chưa có đủ lịch trước khi xếp việc mới. Lịch chưa thay đổi; kiểm tra các phiên rồi xác nhận kế hoạch."
            : "Đây là lịch xem trước, chưa thay đổi lịch hiện tại. Bấm Lưu lịch này để đưa các khoảng làm việc vào lịch."}
        </div>
      )}
      {(confirmMove || plan.strict && !plan.isApplied) && movedTaskIds.size > 0 && (
        <section aria-label="Các công việc được đề xuất dời lịch">
          <h3>Đề xuất dời lịch</h3>
          {nodeList([...movedTaskIds].map((taskId) => {
            const task = state.workspace.tasks.find((t) => t.id === taskId);
            const before = previous.filter((s) => s.taskId === taskId);
            const after = plan.sessions.filter((s) => s.taskId === taskId && !s.preserved);
            return <div className="info spaced">
              <strong>{task?.name || "Công việc"}</strong>
              <p>Giờ cũ: {before.map(windowText).join("; ") || "Xem lịch đã lưu"}</p>
              <p>Giờ mới: {after.map(windowText).join("; ") || "—"}</p>
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
        {!simulation && !blocked && !plan.isApplied && plan.status !== "DISCARDED"
          ? button(confirmMove ? "Xác nhận dời lịch" : plan.hasDeadlineProtection ? "Xác nhận kế hoạch" : "Lưu lịch này", "apply-plan", "primary", plan.id, "check")
          : ""}
        {!simulation && plan.isApplied
          ? button("Mở Hôm nay để làm việc", "nav", "primary", "today", "play")
          : ""}
      </div>
    </>,
  );
}
