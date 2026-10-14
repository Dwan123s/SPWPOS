import { useEffect, useState } from "react";
import { api } from "./api.js";
import { minutes, reasons } from "./ui.jsx";
import { NotePlanPreview } from "./note-plan-preview.jsx";

const reviewReasons = new Set([
  "DEADLINE_PASSED",
  "DEADLINE_CAPACITY_DEFICIT",
  "NO_CONTIGUOUS_SLOT",
  "UNFINISHED_DEPENDENCY",
  "DEADLINE_OUTSIDE_HORIZON",
]);

export function NoteImpact({
  drafts,
  horizonDays,
  hasTime,
  enabled,
  workspace,
  onSetTime,
  showPreview = false,
}) {
  const [check, setCheck] = useState(null);
  const [retry, setRetry] = useState(0);
  const [previewOpen, setPreviewOpen] = useState(
    () => !globalThis.matchMedia?.("(max-width: 800px)").matches,
  );
  const [previewDate, setPreviewDate] = useState(null);
  const tasks = drafts
    .filter((task) => task.selected)
    .map(({ selected, ...task }) => ({
      ...task,
      categoryId: null,
      parentId: null,
      creationSource: "NATURAL_LANGUAGE",
    }));
  const valid =
    tasks.length > 0 &&
    tasks.length <= 12 &&
    Number.isInteger(Number(horizonDays)) &&
    Number(horizonDays) >= 1 &&
    Number(horizonDays) <= 30 &&
    tasks.every(
      (task) =>
        task.name?.trim() &&
        task.estimatedMinutes >= 1 &&
        task.estimatedMinutes <= 43200 &&
        Number.isInteger(task.estimatedMinutes) &&
        Number.isFinite(new Date(task.deadline).getTime()),
    );
  const key =
    enabled && hasTime && valid
      ? JSON.stringify({ tasks, horizonDays: Number(horizonDays) })
      : null;

  useEffect(() => {
    if (!key) return;
    let cancelled = false;
    const timer = setTimeout(async () => {
      try {
        const input = JSON.parse(key);
        const result = await api("/api/plans/scenario", "POST", {
          plan: {
            start: new Date().toISOString(),
            horizonDays: input.horizonDays,
            isReschedule: true,
            triggerEvent: "NEW_TASK",
          },
          newTask: input.tasks[0],
          additionalTasks: input.tasks.slice(1),
        });
        if (
          !result?.before ||
          !result?.after ||
          !Array.isArray(result.proposedPlan?.warnings)
        )
          throw new Error("Chưa nhận đủ kết quả kiểm tra lịch.");
        if (!cancelled) setCheck({ key, retry, workspace, result });
      } catch (error) {
        if (!cancelled)
          setCheck({ key, retry, workspace, error: error.message });
      }
    }, 450);
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [key, retry, workspace]);

  if (!enabled || !tasks.length) return null;
  const current =
    check?.key === key && check.retry === retry && check.workspace === workspace
      ? check
      : null;
  let content;
  if (!hasTime)
    content = (
      <>
        <p>
          Chưa có giờ rảnh để kiểm tra khả năng nhận thêm việc. Chọn giờ bạn có
          thể làm; các việc đang chỉnh sửa sẽ được giữ nguyên.
        </p>
        <button
          type="button"
          className="btn secondary small"
          onClick={onSetTime}
        >
          Chọn giờ rảnh để kiểm tra
        </button>
      </>
    );
  else if (!valid)
    content = (
      <p>
        Điền tên việc, thời lượng và hạn chót hợp lệ; chọn phạm vi 1–30 ngày để
        kiểm tra. Mỗi lần có thể kiểm tra tối đa 12 việc.
      </p>
    );
  else if (!current)
    content = (
      <p role="status">
        Đang kiểm tra {tasks.length} việc được chọn với lịch hiện tại…
      </p>
    );
  else if (current.error)
    content = (
      <>
        <p role="status">
          Chưa kiểm tra được tác động: {current.error} Bạn vẫn có thể chỉnh công
          việc và xem lịch đề xuất.
        </p>
        <button
          type="button"
          className="btn secondary small"
          onClick={() => setRetry((value) => value + 1)}
        >
          Thử kiểm tra lại
        </button>
      </>
    );
  else
    content = (
      <ImpactResult result={current.result} horizonDays={Number(horizonDays)} />
    );

  return (
    <section className="note-impact spaced" aria-labelledby="note-impact-title">
      <h3 id="note-impact-title">Kiểm tra trước khi nhận việc</h3>
      {content}
      <p className="reason">
        Kiểm tra tự cập nhật khi bạn chọn hoặc sửa công việc. Bước này chưa tạo
        công việc hoặc thay đổi lịch.
      </p>
      {showPreview && current?.result && (
        <details
          className="note-calendar-disclosure"
          open={previewOpen}
          onToggle={(event) => setPreviewOpen(event.currentTarget.open)}
        >
          <summary>
            Xem lịch dự kiến <span>Chưa lưu</span>
          </summary>
          <NotePlanPreview
            result={current.result}
            workspace={workspace}
            horizonDays={Number(horizonDays)}
            chosenDate={previewDate}
            onChooseDate={setPreviewDate}
          />
        </details>
      )}
    </section>
  );
}

function ImpactResult({ result, horizonDays }) {
  const { before, after, proposedPlan } = result;
  const warnings = proposedPlan.warnings;
  const existing = new Map(
    (before.tasks || []).map((task) => [task.taskId, task]),
  );
  const affected = (after.tasks || []).filter(
    (task) =>
      existing.has(task.taskId) && task.score > existing.get(task.taskId).score,
  );
  const newRisks = (after.tasks || []).filter(
    (task) =>
      !existing.has(task.taskId) &&
      task.reasons?.some((reason) => reviewReasons.has(reason)),
  );
  const outsideHorizon = newRisks.some((task) =>
    task.reasons.includes("DEADLINE_OUTSIDE_HORIZON"),
  );
  const missing = Math.max(
    0,
    after.horizonDemandMinutes - after.effectiveCapacityMinutes,
  );
  const needsReview =
    missing > 0 ||
    warnings.length > 0 ||
    affected.length > 0 ||
    newRisks.length > 0 ||
    after.riskLevel === "High" ||
    after.riskLevel === "Overload";
  const headline =
    missing > 0
      ? `Khối lượng sau khi thêm vượt khả năng thực hiện ${minutes(missing)}.`
      : warnings.length > 0
        ? "Có việc chưa thể xếp phù hợp vào lịch."
        : affected.length > 0
          ? "Việc mới làm tăng rủi ro của công việc đang có."
          : outsideHorizon
            ? "Có hạn chót ngoài phạm vi đang kiểm tra."
            : needsReview
              ? "Cần xem lại hạn chót và khối lượng công việc."
              : `Chưa thấy vấn đề khi thêm các việc được chọn trong ${horizonDays} ngày tới.`;

  return (
    <div
      className={
        needsReview ? "note-impact-result needs-review" : "note-impact-result"
      }
    >
      <p className="note-impact-verdict" role="status">
        {headline}
      </p>
      <p>
        Thêm {minutes(result.addedMinutes)} công việc, có tính đến dữ liệu ước
        lượng cá nhân.
      </p>
      <details className="note-impact-comparison">
        <summary>So sánh khối lượng và giờ rảnh</summary>
        <div className="note-impact-stats">
          <div>
            <span>Việc đến hạn hiện tại</span>
            <strong>{minutes(before.horizonDemandMinutes)}</strong>
          </div>
          <div>
            <span>Sau khi thêm</span>
            <strong>{minutes(after.horizonDemandMinutes)}</strong>
          </div>
          <div>
            <span>Khả năng trong {horizonDays} ngày</span>
            <strong>{minutes(after.effectiveCapacityMinutes)}</strong>
          </div>
        </div>
      </details>
      {!!warnings.length && (
        <ul className="note-impact-issues">
          {warnings.map((warning, index) => (
            <li key={`${warning.taskId}-${warning.code}-${index}`}>
              <strong>{warning.taskName}</strong>: {warning.message}
              {warning.unscheduledMinutes > 0 &&
                ` Còn ${minutes(warning.unscheduledMinutes)} chưa xếp được.`}
            </li>
          ))}
        </ul>
      )}
      {!!newRisks.length && (
        <ul className="note-impact-issues">
          {newRisks.map((task) => (
            <li key={task.taskId}>
              <strong>{task.name}</strong>:{" "}
              {task.reasons
                .filter((reason) => reviewReasons.has(reason))
                .map((reason) => reasons[reason] || reason)
                .join("; ")}
              .
            </li>
          ))}
        </ul>
      )}
      {!!affected.length && (
        <div className="spaced">
          <strong>Công việc đang có bị tăng rủi ro</strong>
          <ul className="note-impact-issues">
            {affected.map((task) => (
              <li key={task.taskId}>
                <strong>{task.name}</strong>:{" "}
                {task.reasons
                  .filter((reason) => reviewReasons.has(reason))
                  .map((reason) => reasons[reason] || reason)
                  .join("; ")}
                .
              </li>
            ))}
          </ul>
        </div>
      )}
      <p className="reason spaced">
        {needsReview
          ? outsideHorizon
            ? "Tăng số ngày xếp lịch để kiểm tra các hạn chót xa hơn. Với việc đang có cảnh báo, thử giảm số việc nhận, bổ sung giờ rảnh hoặc điều chỉnh hạn chót."
            : "Thử giảm số việc nhận, bổ sung giờ rảnh hoặc điều chỉnh thời lượng và hạn chót, rồi xem kết quả kiểm tra cập nhật."
          : "Bạn có thể tạo công việc và xem lịch đề xuất trước khi quyết định lưu."}
      </p>
    </div>
  );
}
