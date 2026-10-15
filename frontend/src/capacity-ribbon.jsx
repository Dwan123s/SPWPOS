import { state, minutes } from "./ui.jsx";
import { dashboardModel } from "./dashboard-model.js";
export function CapacityRibbon() {
  const model = dashboardModel(state.workspace, state.insights);
  const used = model.scheduledMinutes + model.focusMinutes + model.restMinutes;
  const ceiling = Math.max(
    used,
    model.capacityMinutes || model.availableMinutes,
    1,
  );
  return (
    <section className="capacity-ribbon" aria-label="Phân bổ thời gian hôm nay">
      <div className="capacity-ribbon-track">
        {[
          {
            name: "Đã xếp lịch",
            value: model.scheduledMinutes,
            tone: "scheduled",
          },
          { name: "Tập trung", value: model.focusMinutes, tone: "focus" },
          { name: "Đệm nghỉ", value: model.restMinutes, tone: "rest" },
        ].map((segment) => (
          <div
            key={segment.tone}
            className={`capacity-segment capacity-segment--${segment.tone}`}
            style={{ flexGrow: Math.max(segment.value / ceiling, 0.14) }}
            title={`${minutes(segment.value)} ${segment.name.toLowerCase()}`}
          >
            <strong>{minutes(segment.value)}</strong>
            <span>{segment.name}</span>
          </div>
        ))}
        <div
          className={`capacity-ceiling ${model.capacityMinutes !== null && used > model.capacityMinutes ? "is-overloaded" : ""}`}
        >
          <strong>{minutes(model.capacityMinutes)}</strong>
          <span>Khả năng hôm nay</span>
        </div>
      </div>
    </section>
  );
}
