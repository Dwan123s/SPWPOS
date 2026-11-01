import { Select, Slot, nodeList } from "./react-ui.jsx";
import { secretaryMode } from "./agent-handoff.js";
import { SecretaryScenario } from "./secretary-overview.jsx";
import {
  state,
  button,
  badge,
  icon,
  riskLabels,
  reasons,
  typeLabels,
  dateFmt,
  timeFmt,
  dayKey,
  minutes,
  stat,
  field,
  empty,
  modal,
  loadLabel,
} from "./ui.jsx";
export function scenarioPage() {
  if (secretaryMode()) return <SecretaryScenario />;
  const result = state.scenario;
  return (
    <div className="equal-columns">
      <section className="card">
        <div className="card-header">
          <div>
            <h2>Nếu có thêm một công việc…</h2>
            <p>Xem tác động trước khi nhận thêm việc.</p>
          </div>
        </div>
        <form data-form="scenario">
          <div className="form-grid">
            <div className="wide">
              {field(
                "Tên công việc giả định",
                "name",
                "text",
                "",
                'required maxlength="200" placeholder="Ví dụ: Chuẩn bị buổi thuyết trình"',
              )}
            </div>
            {field(
              "Thời lượng (phút)",
              "estimatedMinutes",
              "number",
              120,
              'required min="1" max="43200"',
            )}
            {field("Deadline", "deadline", "datetime-local", "", "required")}
            <label className="field">
              Ưu tiên
              <Select name="priority">
                <option value="2">Vừa</option>
                <option value="3">Cao</option>
                <option value="4">Khẩn</option>
                <option value="1">Thấp</option>
              </Select>
            </label>
            <label className="field">
              Loại công việc
              <Select name="taskType">
                {nodeList(
                  Object.entries(typeLabels).map(([key, label]) => (
                    <option value={key}>{label}</option>
                  )),
                )}
              </Select>
            </label>
          </div>
          <div className="form-error" />
          <div className="form-actions">
            <button type="submit" className="btn primary">
              {"Phân tích kịch bản "}
              {icon("scenario")}
            </button>
          </div>
        </form>
        <div className="info spaced">
          Mô phỏng không lưu công việc và không thay đổi lịch hiện tại.
        </div>
      </section>
      <section className="card">
        <div className="card-header">
          <h2>Trước và sau</h2>
        </div>
        {result ? (
          <>
            <div className="equal-columns">
              {nodeList(
                [result.before, result.after].map((i, n) => (
                  <div>
                    <div className="eyebrow">
                      {n ? "Sau khi thêm" : "Hiện tại"}
                    </div>
                    <div className="stat-value">
                      {minutes(i.remainingMinutes)}
                    </div>
                    {badge(
                      riskLabels[i.riskLevel],
                      i.riskLevel === "High" || i.riskLevel === "Overload"
                        ? "red"
                        : "green",
                    )}
                    <p className="reason spaced">{loadLabel(i)}</p>
                  </div>
                )),
              )}
            </div>
            <div className="warning spaced">
              {"Thêm "}
              {minutes(result.addedMinutes)}
              {" workload. "}
              {result.proposedPlan.warnings.length}
              {" cảnh báo trong kế hoạch giả định."}
            </div>
            {nodeList(
              result.proposedPlan.warnings.map((w) => (
                <p className="reason">
                  {w.taskName}
                  {": "}
                  {w.message}
                </p>
              )),
            )}
            {button(
              "Xem lịch giả định",
              "scenario-plan",
              "secondary",
              "",
              "calendar",
            )}
            {button(
              "Tạo công việc từ kịch bản",
              "scenario-create",
              "ghost",
              "",
              "plus",
            )}
          </>
        ) : (
          empty(
            "Thử trước khi quyết định",
            "Nhập một công việc ở bên trái để so sánh.",
            "",
            "",
          )
        )}
      </section>
    </div>
  );
}
