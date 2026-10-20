import { useRef, useState } from "react";
import { dateFmt, icon, localInput, minutes, toUtc } from "./ui.jsx";
import { NotePlanPreview } from "./note-plan-preview.jsx";
import "./styles/note-activities.css";

const taskTypes = {
  WORK: "Công việc",
  WRITING: "Viết nội dung",
  STUDY: "Học tập",
  PROGRAMMING: "Lập trình",
  DESIGN: "Thiết kế",
  MEETING_PREP: "Chuẩn bị họp",
  ADMIN: "Hành chính",
  OTHER: "Khác",
};
const eventTypes = {
  CLASS: "Đi học",
  WORK: "Ca làm",
  MEETING: "Cuộc họp",
  APPOINTMENT: "Cuộc hẹn",
  OTHER: "Khác",
};
const invalidDate = (value) =>
  !value || !Number.isFinite(new Date(value).getTime());
function fieldIssues(activity) {
  const errors = {};
  if (!activity.name?.trim()) errors.name = "Nhập tên hoạt động.";
  if (activity.kind === "EVENT") {
    if (invalidDate(activity.start)) errors.start = "Bổ sung ngày giờ bắt đầu.";
    if (invalidDate(activity.end)) errors.end = "Bổ sung ngày giờ kết thúc.";
    else if (
      !errors.start &&
      new Date(activity.end) <= new Date(activity.start)
    )
      errors.end = "Giờ kết thúc phải sau giờ bắt đầu.";
  } else {
    if (
      !Number.isInteger(activity.estimatedMinutes) ||
      activity.estimatedMinutes < 5 ||
      activity.estimatedMinutes > 43200
    )
      errors.estimatedMinutes = "Nhập thời lượng từ 5 đến 43200 phút.";
    if (!activity.start && invalidDate(activity.deadline))
      errors.deadline = "Bổ sung hạn chót hoặc giờ bắt đầu.";
    if (activity.start && invalidDate(activity.start))
      errors.start = "Kiểm tra giờ bắt đầu.";
    if (
      activity.end &&
      (invalidDate(activity.end) ||
        invalidDate(activity.start) ||
        new Date(activity.end) - new Date(activity.start) <
          activity.estimatedMinutes * 60000)
    )
      errors.end = "Khung giờ chưa đủ thời lượng công việc.";
    if (activity.deadline && invalidDate(activity.deadline))
      errors.deadline = "Kiểm tra hạn chót.";
    if (
      activity.deadline &&
      !errors.deadline &&
      !errors.start &&
      !errors.end &&
      !errors.estimatedMinutes &&
      activity.start &&
      new Date(activity.deadline) <
        new Date(
          activity.end ||
            new Date(activity.start).getTime() +
              activity.estimatedMinutes * 60000,
        )
    )
      errors.deadline = "Hạn chót không được trước giờ kết thúc công việc.";
  }
  return errors;
}
const issue = (activity) => Object.values(fieldIssues(activity))[0] || "";
export function NoteActivitiesReview({
  note,
  busy,
  dirty,
  hasAiAnalysis,
  workspace,
  hasTime,
  onSetTime,
  prepare,
  apply,
  onReviewDirty,
}) {
  const [drafts, setDrafts] = useState(() =>
    note.analysis.activities.map((activity) => ({
      ...activity,
      selected: true,
    })),
  );
  // Validation can open an incomplete activity initially; only the user closes it.
  const [expanded, setExpanded] = useState(() =>
    note.analysis.activities.map((activity) => !!issue(activity)),
  );
  const [reviewed, setReviewed] = useState(false);
  const [proposal, setProposal] = useState(null);
  const [horizon, setHorizon] = useState(
    workspace?.preference?.planningHorizonDays || 14,
  );
  const [editError, setEditError] = useState("");
  const previewHeading = useRef(null);
  const selected = drafts.filter((x) => x.selected);
  const incomplete = selected.filter(issue);
  const countEvents = selected.filter((x) => x.kind === "EVENT").length;
  const countTasks = selected.length - countEvents;
  const blocked = !!busy || dirty || !hasAiAnalysis;
  const update = (index, field, value) => {
    setDrafts((items) =>
      items.map((item, i) =>
        i === index ? { ...item, [field]: value } : item,
      ),
    );
    setProposal(null);
    setEditError("");
    onReviewDirty(true);
  };
  const changeDate = (index, field, value) => {
    try {
      update(index, field, value ? toUtc(value) : null);
    } catch (error) {
      setEditError(error.message);
    }
  };
  if (note.planId)
    return (
      <div className="card note-activities-saved" role="status">
        {icon("check")}
        <h3>Đã lưu {drafts.length} hoạt động vào lịch.</h3>
        <p>Các sự kiện cố định và phiên công việc đã sẵn sàng.</p>
        <a className="btn primary" href="#calendar">
          Mở lịch
        </a>
      </div>
    );
  return (
    <div className="note-activities-grid">
      <section
        className="card note-activities-list"
        aria-label="Hoạt động từ ghi chú"
      >
        <div className="note-task-list-heading">
          <h3>
            {selected.length}/{drafts.length} hoạt động được chọn
          </h3>
          <span>
            {countEvents} sự kiện · {countTasks} công việc
          </span>
        </div>
        {!drafts.length && (
          <p>Chưa tìm thấy hoạt động. Bổ sung nội dung rồi phân tích lại.</p>
        )}
        <fieldset className="note-fields" disabled={blocked || !!proposal}>
          {drafts.map((activity, index) => {
            const errors = fieldIssues(activity);
            const problem = Object.values(errors)[0];
            const fixed = activity.kind === "EVENT";
            const errorProps = (field) => ({
              "aria-invalid":
                activity.selected && errors[field] ? true : undefined,
              "aria-describedby":
                activity.selected && errors[field]
                  ? `activity-error-${index}-${field}`
                  : undefined,
            });
            const fieldError = (field) =>
              activity.selected && errors[field] ? (
                <span
                  className="note-activity-field-error"
                  id={`activity-error-${index}-${field}`}
                >
                  {errors[field]}
                </span>
              ) : null;
            return (
              <article
                key={index}
                className={`note-activity ${activity.selected && problem ? "needs-detail" : ""}`}
              >
                <div className="note-activity-heading">
                  <input
                    type="checkbox"
                    aria-label={`Chọn ${activity.name}`}
                    checked={activity.selected}
                    onChange={(e) =>
                      update(index, "selected", e.target.checked)
                    }
                  />
                  <div>
                    <strong>{activity.name}</strong>
                    <span>{fixed ? "Sự kiện cố định" : "Công việc"}</span>
                  </div>
                  {icon(fixed ? "calendar" : "tasks")}
                </div>
                <p className="note-activity-time">
                  {activity.start
                    ? dateFmt(activity.start, {
                        day: "2-digit",
                        month: "2-digit",
                        hour: "2-digit",
                        minute: "2-digit",
                      })
                    : "Chưa có giờ bắt đầu"}
                  {activity.end
                    ? ` – ${dateFmt(activity.end, { hour: "2-digit", minute: "2-digit" })}`
                    : !fixed && activity.estimatedMinutes
                      ? ` · ${minutes(activity.estimatedMinutes)}`
                      : ""}
                  {activity.repeatWeeks > 1
                    ? ` · Lặp ${activity.repeatWeeks} tuần`
                    : ""}
                </p>
                <details
                  open={expanded[index]}
                  onToggle={(event) => {
                    const isOpen = event.currentTarget.open;
                    setExpanded((items) =>
                      items[index] === isOpen
                        ? items
                        : items.map((value, i) =>
                            i === index ? isOpen : value,
                          ),
                    );
                  }}
                >
                  <summary>
                    {problem && activity.selected
                      ? "Bổ sung thông tin"
                      : "Chỉnh sửa"}
                  </summary>
                  <div className="note-activity-fields">
                    <label className="wide">
                      Tên hoạt động
                      <input
                        maxLength={200}
                        value={activity.name}
                        onChange={(e) => update(index, "name", e.target.value)}
                        aria-label={`Tên hoạt động ${index + 1}`}
                        {...errorProps("name")}
                      />
                      {fieldError("name")}
                    </label>
                    <label>
                      Loại
                      <select
                        value={activity.type || "OTHER"}
                        onChange={(e) => update(index, "type", e.target.value)}
                        aria-label={`Loại ${activity.name}`}
                      >
                        {Object.entries(fixed ? eventTypes : taskTypes).map(
                          ([value, label]) => (
                            <option key={value} value={value}>
                              {label}
                            </option>
                          ),
                        )}
                      </select>
                    </label>
                    <label>
                      {fixed ? "Bắt đầu" : "Bắt đầu (nếu có)"}
                      <input
                        type="datetime-local"
                        value={activity.start ? localInput(activity.start) : ""}
                        onChange={(e) =>
                          changeDate(index, "start", e.target.value)
                        }
                        aria-label={`Bắt đầu ${activity.name}`}
                        {...errorProps("start")}
                      />
                      {fieldError("start")}
                    </label>
                    {fixed ? (
                      <label>
                        Kết thúc
                        <input
                          type="datetime-local"
                          value={activity.end ? localInput(activity.end) : ""}
                          onChange={(e) =>
                            changeDate(index, "end", e.target.value)
                          }
                          aria-label={`Kết thúc ${activity.name}`}
                          {...errorProps("end")}
                        />
                        {fieldError("end")}
                      </label>
                    ) : (
                      <>
                        <label>
                          Thời lượng (phút)
                          <input
                            type="number"
                            min={5}
                            max={43200}
                            value={activity.estimatedMinutes ?? ""}
                            onChange={(e) =>
                              update(
                                index,
                                "estimatedMinutes",
                                e.target.value === ""
                                  ? null
                                  : Number(e.target.value),
                              )
                            }
                            aria-label={`Thời lượng ${activity.name} (phút)`}
                            {...errorProps("estimatedMinutes")}
                          />
                          {fieldError("estimatedMinutes")}
                        </label>
                        {activity.end && (
                          <label>
                            Kết thúc khung giờ
                            <input
                              type="datetime-local"
                              value={localInput(activity.end)}
                              onChange={(e) =>
                                changeDate(index, "end", e.target.value)
                              }
                              aria-label={`Kết thúc khung giờ ${activity.name}`}
                              {...errorProps("end")}
                            />
                            {fieldError("end")}
                          </label>
                        )}
                        <label>
                          Hạn chót {activity.start ? "(nếu có)" : ""}
                          <input
                            type="datetime-local"
                            value={
                              activity.deadline
                                ? localInput(activity.deadline)
                                : ""
                            }
                            onChange={(e) =>
                              changeDate(index, "deadline", e.target.value)
                            }
                            aria-label={`Hạn chót ${activity.name}`}
                            {...errorProps("deadline")}
                          />
                          {fieldError("deadline")}
                        </label>
                      </>
                    )}
                  </div>
                  {!fixed && activity.start && (
                    <p className="reason">
                      Giữ giờ bắt đầu đã yêu cầu. Lịch sẽ dùng đúng thời lượng
                      bạn nhập.
                    </p>
                  )}
                </details>
              </article>
            );
          })}
        </fieldset>
        {!!note.analysis.messages?.length && (
          <details className="note-analysis-explanation">
            <summary>Lưu ý từ ghi chú</summary>
            <ul>
              {note.analysis.messages.map((text, i) => (
                <li key={i}>{text}</li>
              ))}
            </ul>
          </details>
        )}
        {note.analysis.needsReview && (
          <label className="note-activity-ack">
            <input
              type="checkbox"
              checked={reviewed}
              disabled={blocked || !!proposal}
              onChange={(e) => {
                setReviewed(e.target.checked);
                onReviewDirty(true);
              }}
            />
            Tôi đã kiểm tra các lưu ý, giờ rảnh và điều kiện trong ghi chú.
          </label>
        )}
        {editError && (
          <p role="alert" className="warning">
            {editError}
          </p>
        )}
        {dirty && (
          <p className="warning">
            Nội dung đã thay đổi. Phân tích lại ghi chú trước khi xem lịch.
          </p>
        )}
        {!proposal && (
          <div className="note-proposal-footer">
            <label className="note-horizon">
              Xếp trong{" "}
              <input
                type="number"
                min={1}
                max={30}
                value={horizon}
                disabled={blocked}
                onChange={(e) => {
                  setHorizon(Number(e.target.value));
                  onReviewDirty(true);
                }}
                aria-label="Số ngày xếp lịch"
              />{" "}
              ngày tới
            </label>
            <p className="reason" role="status">
              {incomplete.length
                ? `${incomplete.length} hoạt động cần bổ sung thông tin.`
                : "Chưa tạo sự kiện, công việc hoặc lưu lịch. Bạn xác nhận sau khi xem đề xuất."}
            </p>
            {!hasTime && countTasks > 0 && (
              <button
                className="btn secondary"
                disabled={blocked}
                onClick={onSetTime}
              >
                Thiết lập giờ rảnh
              </button>
            )}
            <button
              className="btn primary"
              disabled={
                blocked ||
                !selected.length ||
                !!incomplete.length ||
                !Number.isInteger(horizon) ||
                horizon < 1 ||
                horizon > 30 ||
                (countTasks > 0 && !hasTime) ||
                (note.analysis.needsReview && !reviewed)
              }
              onClick={async () => {
                const result = await prepare({
                  activities: selected.map(
                    ({ selected, ...activity }) => activity,
                  ),
                  horizonDays: horizon,
                  reviewedConditions: reviewed,
                });
                if (result) {
                  setProposal(result);
                  requestAnimationFrame(() => previewHeading.current?.focus());
                }
              }}
            >
              Xem lịch đề xuất {icon("arrow")}
            </button>
          </div>
        )}
      </section>
      <div className="note-activities-context">
        {proposal ? (
          <>
            <NotePlanPreview
              headingRef={previewHeading}
              result={{
                proposedPlan: proposal,
                after: {
                  tasks: selected
                    .filter((x) => x.kind === "TASK")
                    .map((x, i) => ({
                      taskId: proposal.noteContext.taskIds[i],
                      name: x.name,
                    })),
                },
              }}
              workspace={{
                ...workspace,
                events: [
                  ...(workspace?.events || []).filter(
                    (x) =>
                      !proposal.noteContext.events.some((e) => e.id === x.id),
                  ),
                  ...proposal.noteContext.events,
                ],
              }}
              horizonDays={horizon}
              confirmation
            />
            {!!proposal.warnings.length && (
              <div role="alert" className="warning spaced">
                <ul>
                  {proposal.warnings.map((warning, i) => (
                    <li key={i}>
                      {warning.taskName}: {warning.message}
                    </li>
                  ))}
                </ul>
              </div>
            )}
            <div className="card note-activity-confirm">
              <strong>
                {countEvents} sự kiện cố định · {countTasks} công việc
              </strong>
              <p>
                Kiểm tra ngày giờ trước khi lưu. Các phiên hiện có cần điều
                chỉnh được thể hiện trong lịch đề xuất.
              </p>
              <button
                className="btn primary"
                disabled={
                  blocked ||
                  proposal.warnings.some(
                    (x) =>
                      x.unscheduledMinutes > 0 ||
                      x.code === "SOFT_DEADLINE_EXCEEDED",
                  )
                }
                onClick={() => apply(proposal.id)}
              >
                Lưu sự kiện & công việc
              </button>
              <button
                className="btn ghost"
                disabled={!!busy}
                onClick={() => setProposal(null)}
              >
                Quay lại chỉnh sửa
              </button>
            </div>
          </>
        ) : (
          <div className="card note-preview-placeholder">
            {icon("calendar")}
            <h3>Đủ hoạt động, đúng giờ</h3>
            <p>
              Sự kiện cố định giữ nguyên giờ. Công việc có giờ bắt đầu sẽ được
              xếp trong khung giờ bạn xác nhận.
            </p>
            <p>
              Hoàn thiện thông tin bên trái rồi xem lịch đề xuất. Chưa có thay
              đổi nào được lưu vào lịch.
            </p>
          </div>
        )}
      </div>
    </div>
  );
}
