import { useEffect, useState } from "react";
import { api } from "./api.js";
import { Select, nodeList } from "./react-ui.jsx";
import { WorkHoursEditor } from "./work-hours.jsx";
import { AvailabilityPolicyCard } from "./availability-policy.jsx";
import { availabilityWindows, nextDay } from "./dashboard-model.js";
import { askNora } from "./agent-handoff.js";
import {
  state,
  button,
  dateFmt,
  timeFmt,
  dayKey,
  field,
  timeValue,
} from "./ui.jsx";
const mondayOf = (date) =>
  nextDay(date, -((new Date(`${date}T12:00:00Z`).getUTCDay() + 6) % 7));
const displayDate = (date) => dateFmt(`${date}T12:00:00Z`, { timeZone: "UTC" });

function WorkHoursCard({ workspace: w, onWorkspaceChanged }) {
  const currentWeek = mondayOf(dayKey(Date.now()));
  const timetableWeeks = w.availabilityPolicy?.timetableWeeks ?? [];
  const timetableWeek = timetableWeeks.find((week) => week >= currentWeek);
  const [scope, setScope] = useState(timetableWeek ? "week" : "recurring");
  const byWeek = scope === "week";
  const byTimetable = scope === "timetable";
  const [weekStart, setWeekStart] = useState(timetableWeek || currentWeek);
  const [effective, setEffective] = useState(null);
  const [retry, setRetry] = useState(0);
  const usesTimetable = byWeek && timetableWeeks.includes(weekStart);
  const effectiveKey = usesTimetable
    ? `${weekStart}:${w.preference.revision}:${retry}`
    : null;
  useEffect(() => {
    if (!effectiveKey) return;
    let live = true;
    api(`/api/time/availability/weeks/${weekStart}`)
      .then(({ slots }) => {
        if (live) setEffective({ key: effectiveKey, slots });
      })
      .catch((error) => {
        if (live) setEffective({ key: effectiveKey, error: error.message });
      });
    return () => {
      live = false;
    };
  }, [effectiveKey, weekStart]);
  const weekResult = effective?.key === effectiveKey ? effective : null;
  const weekEnd = nextDay(weekStart, 6);
  const dates = [
    weekEnd,
    weekStart,
    ...[1, 2, 3, 4, 5].map((offset) => nextDay(weekStart, offset)),
  ];
  const hasOverrides =
    byWeek &&
    w.exceptions.some(
      (item) =>
        item.date >= weekStart &&
        item.date <= weekEnd &&
        item.isAvailable &&
        item.overridesWeeklyAvailability,
    );
  const slots = usesTimetable
    ? weekResult?.slots
    : byWeek
      ? dates.flatMap((date, dayOfWeek) =>
          availabilityWindows(
            w.availability,
            w.exceptions.filter((item) => item.date === date),
            dayOfWeek,
          ).map(([startMinute, endMinute]) => ({
            dayOfWeek,
            startMinute,
            endMinute,
          })),
        )
      : w.availability;

  return (
    <section className="card work-hours-card">
      <div className="card-header">
        <div>
          <h2>Khung giờ làm việc của tôi</h2>
          <p>
            Xem giờ rảnh quanh lịch học và ca làm đã lưu, hoặc nhập các khung
            bạn có thể làm việc.
          </p>
        </div>
        {state.noraAvailable && (
          <button
            type="button"
            className="btn secondary small"
            onClick={() =>
              askNora(
                "Tôi muốn gửi ảnh thời khóa biểu. Hãy đọc giờ học và lưu các buổi học thành lịch cố định sau khi đủ thông tin. Sau đó, thiết lập giờ làm việc theo thời khóa biểu cho các tuần đã nhập, dựa vào lịch học, ca làm và lịch bận đã lưu.",
              )
            }
          >
            Gửi thời khóa biểu
          </button>
        )}
      </div>
      <div
        className="work-hours-scope"
        role="group"
        aria-label="Phạm vi giờ làm việc"
      >
        <button
          type="button"
          className={`btn small ${scope === "recurring" ? "primary" : "secondary"}`}
          aria-pressed={scope === "recurring"}
          onClick={() => setScope("recurring")}
        >
          Giờ làm việc thường lệ
        </button>
        <button
          type="button"
          className={`btn small ${byWeek ? "primary" : "secondary"}`}
          aria-pressed={byWeek}
          onClick={() => setScope("week")}
        >
          Theo từng tuần
        </button>
        <button
          type="button"
          className={`btn small ${byTimetable ? "primary" : "secondary"}`}
          aria-pressed={byTimetable}
          onClick={() => setScope("timetable")}
        >
          Theo thời khóa biểu
        </button>
      </div>
      {byTimetable ? (
        w.events?.length ? (
          <div className="work-hours-timetable">
            <AvailabilityPolicyCard
              autoPreview={!w.availabilityPolicy?.confirmed}
              useTimetableHours
              onWorkspaceChanged={onWorkspaceChanged}
              onConfirmed={(week) => {
                if (week) setWeekStart(week);
                setScope("week");
              }}
            />
          </div>
        ) : (
          <p className="work-hours-empty-timetable">
            Thêm buổi học hoặc ca làm vào Lịch làm việc để Planora tính các
            khoảng rảnh từng ngày.
          </p>
        )
      ) : (
        <>
          {byWeek && (
            <div className="work-hours-week">
              <label className="field">
                Chọn ngày trong tuần
                <input
                  type="date"
                  min="2000-01-03"
                  max="2100-12-31"
                  value={weekStart}
                  onChange={(event) => {
                    const { value, validity } = event.target;
                    if (value && validity.valid) setWeekStart(mondayOf(value));
                  }}
                />
              </label>
              <div>
                <strong>
                  {displayDate(weekStart)} – {displayDate(weekEnd)}/
                  {weekEnd.slice(0, 4)}
                </strong>
                <p>
                  {usesTimetable
                    ? "Các khung tuần này được tính từ thời khóa biểu và lịch bận đã lưu. Chỉnh và lưu nếu bạn muốn đặt giờ riêng cho tuần này."
                    : "Giờ làm việc bạn lưu ở đây chỉ áp dụng cho tuần này. Tắt ngày bạn không rảnh."}
                </p>
                {w.availabilityPolicy?.confirmed &&
                  !w.availability.length &&
                  !hasOverrides && (
                    <p>Tuần này đang dùng lịch sinh hoạt đã xác nhận.</p>
                  )}
              </div>
            </div>
          )}
          {slots ? (
            <form
              data-form="availability"
              key={byWeek ? weekStart : "recurring"}
            >
              {byWeek && (
                <input type="hidden" name="weekStart" value={weekStart} />
              )}
              <WorkHoursEditor
                key={JSON.stringify([hasOverrides, slots])}
                slots={slots}
                dates={byWeek ? dates.map(displayDate) : undefined}
              />
              <div className="form-error" role="alert" />
              <p className="reason spaced">
                {w.preference.autoReschedule !== false
                  ? "Khi lưu, các phiên tương lai linh hoạt bị xung đột sẽ được xếp lại. Phiên đã khóa và đang thực hiện được giữ nguyên."
                  : "Tự điều chỉnh đang tắt. Lưu giờ làm việc rồi xem lịch đề xuất để xử lý các phiên bị ảnh hưởng."}
              </p>
              <div className="form-actions work-hours-save">
                <button type="submit" className="btn primary">
                  {byWeek ? "Lưu giờ làm việc tuần này" : "Lưu giờ làm việc"}
                </button>
                {hasOverrides && (
                  <button
                    type="submit"
                    className="btn secondary"
                    formNoValidate
                    data-next-action="reset"
                  >
                    {usesTimetable
                      ? "Dùng lại giờ theo thời khóa biểu"
                      : "Dùng lại giờ làm việc thường lệ"}
                  </button>
                )}
                {(byWeek ||
                  w.tasks.some(
                    (task) => !["Completed", "Cancelled"].includes(task.status),
                  )) && (
                  <button
                    type="submit"
                    className="btn secondary"
                    data-next-action="plan"
                  >
                    Lưu và xem lịch
                  </button>
                )}
              </div>
            </form>
          ) : weekResult?.error ? (
            <p role="alert">
              {weekResult.error}{" "}
              <button
                type="button"
                className="btn ghost small"
                onClick={() => setRetry(retry + 1)}
              >
                Thử lại
              </button>
            </p>
          ) : (
            <p role="status">Đang tính giờ làm việc theo thời khóa biểu…</p>
          )}
        </>
      )}
    </section>
  );
}

export function timePage({ onWorkspaceChanged } = {}) {
  const w = state.workspace,
    p = w.preference;
  return (
    <div className="equal-columns">
      <div className="stack">
        <WorkHoursCard workspace={w} onWorkspaceChanged={onWorkspaceChanged} />
        <section className="card">
          <div className="card-header">
            <div>
              <h2>Ngoại lệ theo ngày</h2>
              <p>Ngày nghỉ, lịch bận hoặc giờ rảnh thêm.</p>
            </div>
            {button("Thêm", "new-exception", "small secondary", "", "plus")}
          </div>
          {nodeList(
            w.exceptions
              .filter((e) => e.startMinute < e.endMinute)
              .map((e) => (
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
