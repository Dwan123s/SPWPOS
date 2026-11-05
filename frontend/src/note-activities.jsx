import { useEffect, useId, useRef, useState } from "react";
import { dateFmt, icon, localInput, minutes, toUtc } from "./ui.jsx";
import { NotePlanPreview } from "./note-plan-preview.jsx";
import "./styles/note-activities.css";

const taskTypes = {
  WORK: "Công việc",
  WRITING: "Viết nội dung",
  STUDY: "Học tập",
  PROGRAMMING: "Lập trình",
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
function explainWarning(warning, proposal, workspace) {
  if (warning.code !== "SOFT_DEADLINE_EXCEEDED") return warning.message;
  const context = proposal.noteContext;
  const tasks = (context?.activities || []).filter(
    (activity) => activity.kind === "TASK",
  );
  const index = (context?.taskIds || []).indexOf(warning.taskId);
  const activity =
    tasks[index] || tasks.find((task) => task.name === warning.taskName);
  const id = warning.taskId || context?.taskIds?.[tasks.indexOf(activity)];
  const existing = (workspace?.tasks || []).find((task) => task.id === id);
  const deadline =
    activity?.deadline ||
    activity?.end ||
    existing?.deadline ||
    (!invalidDate(activity?.start) &&
    Number.isFinite(activity?.estimatedMinutes)
      ? new Date(
          new Date(activity.start).getTime() +
            activity.estimatedMinutes * 60000,
        ).toISOString()
      : null);
  const finishes = proposal.sessions
    .filter((session) => session.taskId === id)
    .map((session) => new Date(session.end).getTime())
    .filter(Number.isFinite);
  const finish = finishes.length ? Math.max(...finishes) : null;
  if (
    !invalidDate(deadline) &&
    finish !== null &&
    finish > new Date(deadline).getTime()
  ) {
    const format = (value) =>
      dateFmt(value, {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
      });
    return `Hạn chót: ${format(deadline)}. Lịch đề xuất làm xong lúc ${format(finish)}, trễ ${minutes(Math.ceil((finish - new Date(deadline).getTime()) / 60000))}.`;
  }
  return "Lịch đề xuất có phiên làm sau hạn chót của công việc này.";
}
function fieldIssues(activity, now = Date.now()) {
  const errors = {};
  if (!activity.name?.trim()) errors.name = "Nhập tên hoạt động.";
  if (activity.kind === "EVENT") {
    if (invalidDate(activity.start)) errors.start = "Bổ sung ngày giờ bắt đầu.";
    else if (Date.parse(activity.start) <= now)
      errors.start = "Giờ bắt đầu đã qua. Chọn giờ sau thời điểm hiện tại.";
    if (invalidDate(activity.end)) errors.end = "Bổ sung ngày giờ kết thúc.";
    else if (
      !invalidDate(activity.start) &&
      new Date(activity.end) <= new Date(activity.start)
    )
      errors.end = "Giờ kết thúc phải sau giờ bắt đầu.";
  } else {
    if (activity.estimatedMinutes == null)
      errors.estimatedMinutes = "Bổ sung thời lượng công việc.";
    else if (!Number.isInteger(activity.estimatedMinutes))
      errors.estimatedMinutes = "Nhập thời lượng bằng số phút nguyên.";
    else if (activity.estimatedMinutes < 5)
      errors.estimatedMinutes = "Thời lượng cần ít nhất 5 phút.";
    else if (activity.estimatedMinutes > 43200)
      errors.estimatedMinutes = "Thời lượng quá dài (tối đa 720 giờ).";
    if (!activity.start && invalidDate(activity.deadline))
      errors.deadline = "Bổ sung hạn chót hoặc giờ bắt đầu.";
    if (activity.start && invalidDate(activity.start))
      errors.start = "Kiểm tra giờ bắt đầu.";
    else if (activity.start && Date.parse(activity.start) <= now)
      errors.start = "Giờ bắt đầu đã qua. Chọn lại giờ hoặc bỏ chọn mục này.";
    else if (activity.start && Date.parse(activity.start) < now + 5 * 60000)
      errors.start = "Giờ bắt đầu công việc phải cách hiện tại ít nhất 5 phút.";
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
    else if (activity.deadline && Date.parse(activity.deadline) <= now)
      errors.deadline =
        "Hạn chót đã qua. Chọn lại hạn chót hoặc bỏ chọn mục này.";
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
const issue = (activity, now) =>
  Object.values(fieldIssues(activity, now))[0] || "";
// Legacy analysis messages are unstructured. Only retire a missing-field remark
// when it names a now-valid activity and contains no other scheduling concern.
function resolvedRemark(message, drafts, originals) {
  const text = message.toLocaleLowerCase();
  if (!/missing|no estimated|no duration|thiếu|chưa có/.test(text))
    return false;
  if (!/duration|deadline|end time|thời lượng|hạn chót|kết thúc/.test(text))
    return false;
  if (
    /conflict|overlap|availability|depend|xung đột|trùng|giờ rảnh|phụ thuộc/.test(
      text,
    )
  )
    return false;
  const matches = originals.flatMap((activity, index) =>
    activity.name?.trim() &&
    text.includes(activity.name.trim().toLocaleLowerCase())
      ? [index]
      : [],
  );
  return matches.length > 0 && matches.every((index) => !issue(drafts[index]));
}
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
  const [now, setNow] = useState(Date.now);
  useEffect(() => {
    const refresh = () => setNow(Date.now());
    const timer = setInterval(refresh, 10000);
    window.addEventListener("focus", refresh);
    return () => {
      clearInterval(timer);
      window.removeEventListener("focus", refresh);
    };
  }, []);
  const [drafts, setDrafts] = useState(() =>
    note.analysis.activities.map((activity) => {
      const types = activity.kind === "EVENT" ? eventTypes : taskTypes;
      const type = (activity.type || "OTHER").trim().toUpperCase();
      return {
        ...activity,
        type: types[type] ? type : "OTHER",
        selected: true,
      };
    }),
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
  const [allowRequestedHours, setAllowRequestedHours] = useState(false);
  const previewHeading = useRef(null);
  const activityNodes = useRef([]);
  const selectionHintId = useId();
  const saveHintId = useId();
  const selected = drafts.filter((x) => x.selected);
  const incomplete = selected.filter((activity) => issue(activity, now));
  const analysisMessages = (note.analysis.messages || []).filter(
    (message) => !resolvedRemark(message, drafts, note.analysis.activities),
  );
  const countEvents = selected.filter((x) => x.kind === "EVENT").length;
  const countTasks = selected.length - countEvents;
  const fixedTasks = selected.filter((x) => x.kind === "TASK" && x.start);
  const needsGeneralHours = selected.some((x) => x.kind === "TASK" && !x.start);
  const blocked = !!busy || dirty || !hasAiAnalysis;
  const blockingWarnings = (proposal?.warnings || []).filter(
    (warning) =>
      warning.unscheduledMinutes > 0 ||
      warning.code === "SOFT_DEADLINE_EXCEEDED",
  );
  const hasLateTask = blockingWarnings.some(
    (warning) => warning.code === "SOFT_DEADLINE_EXCEEDED",
  );
  const update = (index, field, value) => {
    setNow(Date.now());
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
  function showIssue(index) {
    setExpanded((items) => items.map((open, i) => open || i === index));
    requestAnimationFrame(() => {
      const node = activityNodes.current[index];
      node?.scrollIntoView?.({ block: "center", behavior: "smooth" });
      node
        ?.querySelector('[aria-invalid="true"]')
        ?.focus({ preventScroll: true });
    });
  }
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
            {selected.length}/{drafts.length} mục được chọn để tạo
          </h3>
          <span>
            {countEvents} sự kiện · {countTasks} công việc
          </span>
        </div>
        <p id={selectionHintId} className="note-review-help">
          Các ô tick này dùng để chọn mục muốn thêm, chưa phải đánh dấu hoàn
          thành.
          {proposal && " Muốn đổi lựa chọn, bấm Quay lại chỉnh sửa."}
        </p>
        {!drafts.length && (
          <p>Chưa tìm thấy hoạt động. Bổ sung nội dung rồi phân tích lại.</p>
        )}
        <fieldset className="note-fields" disabled={blocked || !!proposal}>
          {drafts.map((activity, index) => {
            const errors = fieldIssues(activity, now);
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
                ref={(node) => {
                  activityNodes.current[index] = node;
                }}
                className={`note-activity ${activity.selected && problem ? "needs-detail" : ""}`}
              >
                <div className="note-activity-heading">
                  <input
                    type="checkbox"
                    aria-label={`Chọn ${activity.name}`}
                    aria-describedby={selectionHintId}
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
                    : fixed
                      ? "Cần bổ sung giờ bắt đầu"
                      : "AI sẽ chọn giờ rảnh để xếp việc"}
                  {activity.end
                    ? ` – ${dateFmt(activity.end, { hour: "2-digit", minute: "2-digit" })}`
                    : !fixed && activity.estimatedMinutes
                      ? ` · ${minutes(activity.estimatedMinutes)}`
                      : ""}
                  {activity.repeatWeeks > 1
                    ? ` · Lặp ${activity.repeatWeeks} tuần`
                    : ""}
                </p>
                {!fixed && !invalidDate(activity.deadline) && (
                  <p className="note-activity-time">
                    Hạn hoàn thành:{" "}
                    {dateFmt(activity.deadline, {
                      day: "2-digit",
                      month: "2-digit",
                      year: "numeric",
                      hour: "2-digit",
                      minute: "2-digit",
                    })}
                  </p>
                )}
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
        {!!analysisMessages.length && (
          <details className="note-analysis-explanation">
            <summary>Nhận xét từ lần phân tích ban đầu</summary>
            <p className="reason">
              Các nhận xét này dựa trên ghi chú ban đầu, chưa phản ánh chỉnh sửa
              của bạn. Thông tin còn thiếu hoặc chưa hợp lệ được hiển thị ngay
              dưới từng ô nhập.
            </p>
            <ul>
              {analysisMessages.map((text, i) => (
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
        {!proposal && fixedTasks.length > 0 && (
          <label className="note-activity-ack">
            <input
              type="checkbox"
              checked={allowRequestedHours}
              disabled={blocked}
              onChange={(e) => {
                setAllowRequestedHours(e.target.checked);
                onReviewDirty(true);
              }}
            />
            Cho phép dùng giờ bắt đầu đã chọn cho lần này, kể cả ngoài giờ rảnh
            thường lệ.
          </label>
        )}
        {allowRequestedHours && (
          <p className="reason">
            Giờ rảnh bổ sung chỉ được lưu cho ngày đã chọn khi bạn xác nhận
            lịch.
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
            {incomplete.length > 0 ? (
              <div className="note-review-blockers" role="status">
                <strong>
                  {incomplete.length} mục cần kiểm tra trước khi xem lịch
                </strong>
                <ul>
                  {drafts.map((activity, index) =>
                    activity.selected && issue(activity, now) ? (
                      <li key={index}>
                        <button type="button" onClick={() => showIssue(index)}>
                          {activity.name || `Hoạt động ${index + 1}`}
                        </button>
                        <span>
                          Cần sửa:{" "}
                          {Object.values(fieldIssues(activity, now)).join(" ")}
                        </span>
                      </li>
                    ) : null,
                  )}
                </ul>
                {selected.length > incomplete.length && (
                  <button
                    type="button"
                    className="btn secondary small"
                    onClick={() => {
                      setDrafts((items) =>
                        items.map((item) =>
                          item.selected && issue(item, now)
                            ? { ...item, selected: false }
                            : item,
                        ),
                      );
                      setProposal(null);
                      setEditError("");
                      onReviewDirty(true);
                    }}
                  >
                    Chỉ giữ các mục hợp lệ đã chọn
                  </button>
                )}
                <p>
                  Các mục bỏ chọn vẫn nằm trong ghi chú và chưa được thêm vào
                  lịch.
                </p>
              </div>
            ) : (
              <p className="reason" role="status">
                Chưa tạo sự kiện, công việc hoặc lưu lịch. Bạn xác nhận sau khi
                xem đề xuất.
              </p>
            )}
            {!selected.length && (
              <p className="reason" role="status">
                Chọn ít nhất một hoạt động để xem lịch đề xuất.
              </p>
            )}
            {note.analysis.needsReview && !reviewed && (
              <p className="note-review-help">
                Đánh dấu “Tôi đã kiểm tra các lưu ý” sau khi xem các mục để tiếp
                tục.
              </p>
            )}
            {!hasTime && fixedTasks.length > 0 && (
              <p className="note-review-help">
                Các công việc đã có giờ bắt đầu. Bạn có thể xem đề xuất để kiểm
                tra khung giờ; chọn “Cho phép dùng giờ bắt đầu đã chọn” nếu muốn
                dùng các giờ này ngoài giờ rảnh thường lệ.
              </p>
            )}
            {!hasTime && needsGeneralHours && (
              <div className="note-hours-help">
                <p>
                  Chưa có giờ rảnh cho công việc chưa chọn giờ bắt đầu. Bạn vẫn
                  có thể xem kiểm tra lịch; bổ sung giờ làm khi cần xếp các việc
                  này.
                </p>
                <button
                  className="btn secondary"
                  disabled={blocked}
                  onClick={onSetTime}
                >
                  Thiết lập giờ rảnh
                </button>
              </div>
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
                (note.analysis.needsReview && !reviewed)
              }
              onClick={async () => {
                const result = await prepare({
                  activities: selected.map(
                    ({ selected, ...activity }) => activity,
                  ),
                  horizonDays: horizon,
                  reviewedConditions: reviewed,
                  allowRequestedTimeOutsideAvailability: allowRequestedHours,
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
              saveBlocked={blockingWarnings.length > 0}
            />
            {!!proposal.warnings.length && (
              <div role="alert" className="warning spaced">
                <strong>
                  {blockingWarnings.length
                    ? "Chưa thể lưu các việc mới và lịch đề xuất"
                    : "Lưu ý về lịch đề xuất"}
                </strong>
                <ul>
                  {proposal.warnings.map((warning, i) => (
                    <li key={i}>
                      <strong>{warning.taskName}</strong>:{" "}
                      {explainWarning(warning, proposal, workspace)}
                    </li>
                  ))}
                </ul>
                {blockingWarnings.length > 0 && (
                  <p className="note-warning-next-step">
                    {hasLateTask
                      ? "Bấm Quay lại chỉnh sửa, mở Chỉnh sửa ở việc bị quá hạn để đổi hạn chót hoặc thời lượng. Bạn cũng có thể bổ sung giờ rảnh trước hạn rồi xem lại lịch đề xuất."
                      : "Bấm Quay lại chỉnh sửa để điều chỉnh thời lượng, giờ thực hiện hoặc giờ rảnh, rồi xem lại lịch đề xuất."}
                  </p>
                )}
                {proposal.warnings.some(
                  (x) => x.code === "NOTE_OUTSIDE_AVAILABILITY",
                ) &&
                  !allowRequestedHours && (
                    <button
                      className="btn secondary"
                      disabled={blocked}
                      onClick={() => {
                        setAllowRequestedHours(true);
                        setProposal(null);
                        onReviewDirty(true);
                      }}
                    >
                      Dùng giờ đã chọn cho lần này
                    </button>
                  )}
              </div>
            )}
            <div className="card note-activity-confirm">
              <strong>
                {countEvents} sự kiện cố định · {countTasks} công việc
              </strong>
              <p id={saveHintId}>
                {blockingWarnings.length
                  ? hasLateTask
                    ? "Nút lưu đang tắt vì có việc được xếp sau hạn chót. Các việc mới và lịch trong đề xuất này chưa được lưu."
                    : "Nút lưu đang tắt vì còn việc chưa được xếp đủ thời gian. Các việc mới và lịch trong đề xuất này chưa được lưu."
                  : countTasks
                    ? "Các mục mới vẫn chưa được lưu. Bấm Lưu sự kiện & công việc để tạo việc và lưu lịch; sau đó bạn có thể tick hoàn thành các công việc trong Note."
                    : "Các sự kiện mới vẫn chưa được lưu. Bấm Lưu sự kiện & công việc để xác nhận; sau đó các sự kiện sẽ xuất hiện trên Lịch."}
              </p>
              <button
                className="btn primary"
                aria-describedby={saveHintId}
                disabled={
                  blocked ||
                  blockingWarnings.length > 0 ||
                  incomplete.length > 0
                }
                onClick={() => apply(proposal.id)}
              >
                Lưu sự kiện & công việc
              </button>
              <button
                className="btn ghost"
                disabled={!!busy}
                onClick={() => {
                  setExpanded((items) =>
                    items.map(
                      (open, index) =>
                        open ||
                        blockingWarnings.some(
                          (warning) => warning.taskName === drafts[index].name,
                        ),
                    ),
                  );
                  setProposal(null);
                }}
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
