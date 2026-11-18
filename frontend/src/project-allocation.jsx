import { dateFmt, icon, timeFmt } from "./ui.jsx";
import "./project-allocation.css";

export function projectAllocation(
  workspace,
  parentId,
  { replacing, estimates = [] } = {},
) {
  const parent = workspace?.tasks.find(
    (task) => task.id === parentId && !task.isDeleted,
  );
  if (!parent || parent.isTeamTask) return null;

  const children = workspace.tasks.filter(
    (task) => !task.isDeleted && task.parentId === parentId,
  );
  const allocations = [
    ...children
      .filter((task) => task.id !== replacing && task.status !== "Cancelled")
      .map((task) => task.estimatedMinutes),
    ...estimates,
  ].map(Number);
  const assigned = allocations.reduce(
    (sum, value) => sum + (value > 0 ? value : 0),
    0,
  );
  const withoutEstimate = allocations.filter((value) => !(value > 0)).length;
  const original = Number(parent.estimatedMinutes) || 0;
  const recorded = Number(parent.actualMinutes) || 0;
  const sessions = (workspace.sessions ?? []).filter(
    (session) => session.taskId === parentId,
  );
  const replaceable = sessions
    .filter((session) => session.status === "Scheduled" && !session.isLocked)
    .sort((a, b) => Date.parse(a.start) - Date.parse(b.start));

  return {
    parent,
    converts: children.length === 0,
    original,
    recorded,
    assigned,
    withoutEstimate,
    unallocated:
      original > 0 ? Math.max(0, original - recorded - assigned) : null,
    replaceable,
    blocked: sessions.some(
      (session) =>
        ["Running", "Paused"].includes(session.status) ||
        (session.status === "Scheduled" && session.isLocked),
    ),
  };
}

export function ProjectAllocation({ impact, preview = false }) {
  if (
    !impact ||
    (!preview && impact.unallocated === 0 && !impact.withoutEstimate)
  )
    return null;
  const {
    parent,
    converts,
    original,
    recorded,
    assigned,
    withoutEstimate,
    unallocated,
    replaceable,
    blocked,
  } = impact;
  return (
    <section
      className="project-allocation"
      aria-label={
        preview ? "Ảnh hưởng khi lưu việc con" : "Phân bổ thời lượng dự án"
      }
    >
      <div className="project-allocation-title">
        {icon(preview ? "calendar" : "clock")}
        <strong>
          {preview ? "Ảnh hưởng khi lưu việc con" : "Phân bổ thời lượng dự án"}
        </strong>
      </div>
      {preview && converts && (
        <p>
          <strong>{parent.name}</strong> sẽ chuyển từ Inbox sang Projects. Kế
          hoạch sau đó tính theo từng việc con.
        </p>
      )}
      {preview && blocked && (
        <p className="warning">
          Kết thúc phiên đang làm hoặc bỏ khóa phiên của việc cha trước khi lưu
          việc con.
        </p>
      )}
      {preview && !blocked && replaceable.length > 0 && (
        <>
          <p>
            <strong>
              {replaceable.length} phiên chưa bắt đầu của việc cha sẽ bị hủy.
            </strong>{" "}
            Lịch sử đã thực hiện vẫn được giữ.
          </p>
          <ul>
            {replaceable.slice(0, 3).map((session) => (
              <li key={session.id}>
                {dateFmt(session.start)} · {timeFmt(session.start)}–
                {timeFmt(session.end)}
              </li>
            ))}
            {replaceable.length > 3 && (
              <li>Và {replaceable.length - 3} phiên khác</li>
            )}
          </ul>
        </>
      )}
      {unallocated !== null ? (
        <>
          <dl>
            <div>
              <dt>Ước lượng việc cha</dt>
              <dd>{original} phút</dd>
            </div>
            {recorded > 0 && (
              <div>
                <dt>Đã ghi giờ ở việc cha</dt>
                <dd>{recorded} phút</dd>
              </div>
            )}
            <div>
              <dt>Ước lượng các việc con</dt>
              <dd>{assigned} phút</dd>
            </div>
          </dl>
          <p
            aria-live={preview ? "polite" : undefined}
            className={unallocated > 0 ? "project-allocation-gap" : undefined}
          >
            <strong>
              {unallocated > 0
                ? `${unallocated} phút chưa được phân bổ`
                : "Đã phân bổ đủ ước lượng"}
            </strong>
          </p>
          {unallocated > 0 && (
            <p>
              Thêm việc con hoặc điều chỉnh thời lượng của chúng để đưa phần còn
              lại vào kế hoạch.
            </p>
          )}
        </>
      ) : (
        <p>
          Việc cha chưa có ước lượng để đối chiếu với thời lượng các việc con.
        </p>
      )}
      {withoutEstimate > 0 && (
        <p>
          {withoutEstimate} việc con chưa có thời lượng, chưa được tính vào phần
          đã phân bổ.
        </p>
      )}
    </section>
  );
}
