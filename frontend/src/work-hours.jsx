import { useRef, useState } from "react";
import { icon, timeValue } from "./ui.jsx";
import "./work-hours.css";

const weekdays = [
  ["Chủ nhật", "CN"],
  ["Thứ hai", "T2"],
  ["Thứ ba", "T3"],
  ["Thứ tư", "T4"],
  ["Thứ năm", "T5"],
  ["Thứ sáu", "T6"],
  ["Thứ bảy", "T7"],
];
const emptyPeriod = { key: -1, start: "", end: "" };

export function WorkHoursEditor({ slots, dates }) {
  const nextKey = useRef(slots.length);
  const [days, setDays] = useState(() =>
    weekdays.map((_, dayOfWeek) => {
      const periods = slots
        .map((slot, key) => ({ ...slot, key }))
        .filter((slot) => slot.dayOfWeek === dayOfWeek)
        .sort((a, b) => a.startMinute - b.startMinute)
        .map(({ key, startMinute, endMinute }) => ({
          key,
          start: timeValue(startMinute),
          end: timeValue(endMinute % 1440),
        }));
      return { enabled: periods.length > 0, periods };
    }),
  );
  const [notice, setNotice] = useState("");
  const dayOrder = dates ? [1, 2, 3, 4, 5, 6, 0] : [0, 1, 2, 3, 4, 5, 6];
  const enabledDays = dayOrder.filter((index) => days[index].enabled);

  function updateDay(index, update) {
    setDays((current) =>
      current.map((day, dayIndex) => (dayIndex === index ? update(day) : day)),
    );
    setNotice("");
  }

  function toggleDay(index) {
    updateDay(index, (day) => ({
      enabled: !day.enabled,
      periods: day.periods.length ? day.periods : [emptyPeriod],
    }));
  }

  function addPeriod(index) {
    const period = { ...emptyPeriod, key: nextKey.current++ };
    updateDay(index, (day) => ({
      enabled: true,
      periods: day.enabled ? [...day.periods, period] : [period],
    }));
  }

  function removePeriod(index, key) {
    updateDay(index, (day) => {
      const periods = day.periods.filter((item) => item.key !== key);
      return { enabled: periods.length > 0, periods };
    });
  }

  function copyHours(index) {
    setDays((current) =>
      current.map((day) =>
        day.enabled ? { ...day, periods: current[index].periods } : day,
      ),
    );
    setNotice(
      `Đã sao chép các khung giờ của ${weekdays[index][0]} sang các ngày đang bật.`,
    );
  }

  return (
    <>
      <div className="work-hours-days" role="group" aria-label="Bật hoặc tắt ngày có giờ rảnh">
        {dayOrder.map((index) => (
          <button
            key={index}
            type="button"
            aria-label={weekdays[index][0]}
            aria-pressed={days[index].enabled}
            title={weekdays[index][0]}
            onClick={() => toggleDay(index)}
          >
            {weekdays[index][1]}
          </button>
        ))}
      </div>
      <div className="work-hours-grid">
        {dayOrder.map((index) => {
          const { enabled, periods } = days[index];
          const [name] = weekdays[index];
          return (
            <div className="work-hours-day" key={index} role="group" aria-label={name}>
              <span className="work-hours-day-name">
                {name}
                {dates && <small>{dates[index]}</small>}
              </span>
              <div className="work-hours-periods">
                {enabled && periods.map((period, periodIndex) => {
                  const label = periods.length === 1 ? name : `${name}, khung ${periodIndex + 1}`;
                  const updatePeriod = (change) =>
                    updateDay(index, (day) => ({
                      ...day,
                      periods: day.periods.map((item) =>
                        item.key === period.key ? { ...item, ...change } : item,
                      ),
                    }));
                  return (
                    <div className="work-hours-row" key={period.key}>
                      <input name="day" type="hidden" value={index} />
                      <label className="work-hours-time">
                        <span>Từ</span>
                        <input
                          type="time"
                          name="start"
                          required
                          aria-label={`Bắt đầu ${label}`}
                          value={period.start}
                          onChange={(event) => updatePeriod({ start: event.target.value })}
                        />
                      </label>
                      <label className="work-hours-time">
                        <span>Đến</span>
                        <input
                          type="time"
                          name="end"
                          required
                          aria-label={`Kết thúc ${label}`}
                          value={period.end}
                          onChange={(event) => updatePeriod({ end: event.target.value })}
                        />
                      </label>
                      <button
                        type="button"
                        className="work-hours-row-icon"
                        title={`Xóa khung ${periodIndex + 1} của ${name}`}
                        aria-label={`Xóa khung ${periodIndex + 1} của ${name}`}
                        onClick={() => removePeriod(index, period.key)}
                      >
                        {icon("close")}
                      </button>
                      {period.end === "00:00" && (
                        <small className="work-hours-midnight">00:00 là cuối ngày</small>
                      )}
                    </div>
                  );
                })}
                <div className="work-hours-actions">
                  <button
                    type="button"
                    className="work-hours-add"
                    aria-label={`Thêm khung giờ cho ${name}`}
                    onClick={() => addPeriod(index)}
                  >
                    {icon("plus")} Thêm khung giờ
                  </button>
                  {!enabled && <span className="work-hours-day-empty">Chưa có giờ rảnh</span>}
                  {index === enabledDays[0] && enabledDays.length > 1 && (
                    <button
                      type="button"
                      className="work-hours-copy"
                      onClick={() => copyHours(index)}
                    >
                      {icon("copy")} Sao chép sang ngày đang bật
                    </button>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>
      <div className="work-hours-notice" role="status">
        {notice}
      </div>
    </>
  );
}
