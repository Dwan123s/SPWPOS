import { Select, Slot, nodeList } from "./react-ui.jsx";
import { setSlot } from "./store.js";
import {
  state,
  icon,
  button,
  badge,
  statusLabels,
  typeLabels,
  dateFmt,
  timeFmt,
  minutes,
  localInput,
  toUtc,
  field,
  modal,
  empty,
  dayKey,
} from "./ui.jsx";
import { api } from "./api.js";
export const taskById = (id) => state.workspace.tasks.find((t) => t.id === id);
export function taskRows(tasks) {
  return tasks.length ? (
    <>
      <div className="task-row task-heading">
        <span>Công việc</span>
        <span>Deadline</span>
        <span className="task-category">Danh mục</span>
        <span className="progress-wrap">Tiến độ</span>
        <span>Ưu tiên</span>
        <span className="sr-only">Thao tác</span>
      </div>
      {nodeList(
        tasks.map((t) => (
          <div className="task-row" key={t.id}>
            <div>
              <button
                className="title-button"
                data-action="task-detail"
                data-id={t.id}
              >
                <div className="task-name">
                  {t.parentId ? "↳ " : ""}
                  {t.name}
                </div>
              </button>
              <div className="task-meta">
                {badge(
                  statusLabels[t.status],
                  t.status === "Completed"
                    ? "green"
                    : t.status === "InProgress"
                      ? "purple"
                      : "",
                )}
                {" · "}
                {minutes(t.remainingMinutes)}
                {" còn lại"}
              </div>
            </div>
            <div className="task-meta">
              {dateFmt(t.deadline)}
              <br />
              {timeFmt(t.deadline)}
            </div>
            <div className="task-category">
              {badge(
                state.workspace.categories.find((c) => c.id === t.categoryId)
                  ?.name || "Chưa phân loại",
              )}
            </div>
            <div className="progress-wrap">
              <progress
                className="progress"
                aria-label={`Tiến độ ${t.name}`}
                value={t.progress}
                max="100"
              />
              <small>{t.progress}%</small>
            </div>
            <div>
              {badge(
                ["", "Thấp", "Vừa", "Cao", "Khẩn"][t.priority],
                t.priority >= 3 ? "amber" : "",
              )}
            </div>
            <div className="task-row-actions">
              <button
                type="button"
                className="btn ghost icon-only task-delete-button"
                data-action="delete-task"
                data-id={t.id}
                aria-label={`Xóa công việc ${t.name}`}
                title={`Xóa công việc ${t.name}`}
              >
                {icon("trash")}
              </button>
            </div>
          </div>
        )),
      )}
    </>
  ) : (
    empty(
      "Chưa có công việc nào",
      "Thêm một công việc và để PLANORA giúp bạn dành thời gian cho nó.",
    )
  );
}
const scopes = {
  all: ["Tất cả", () => true],
  open: ["Đang mở", (t) => !["Completed", "Cancelled"].includes(t.status)],
  progress: ["Đang làm", (t) => t.status === "InProgress"],
  completed: ["Hoàn thành", (t) => t.status === "Completed"],
};
export function tasksPage() {
  const scope = state.taskScope || "all";
  return (
    <>
      <div className="task-tabs" role="group" aria-label="Chế độ xem công việc">
        {nodeList(
          Object.entries(scopes).map(([key, [label, match]]) => (
            <button
              type="button"
              data-action="task-scope"
              data-id={key}
              className={key === scope ? "active" : ""}
              aria-pressed={key === scope}
            >
              {label}
              <span>{state.workspace.tasks.filter(match).length}</span>
            </button>
          )),
        )}
      </div>
      <section className="card task-workspace">
        <div className="filters task-filters">
          <label className="search-field">
            {icon("search")}
            <input
              className="filter search"
              id="task-search"
              type="search"
              placeholder="Tìm công việc…"
              aria-label="Tìm công việc"
            />
          </label>
          <Select
            className="filter"
            id="task-status"
            aria-label="Lọc trạng thái"
          >
            <option value={``}>Trạng thái</option>
            {nodeList(
              Object.entries(statusLabels)
                .filter(([key]) =>
                  ["Todo", "InProgress", "Completed", "Cancelled"].includes(
                    key,
                  ),
                )
                .map(([key, label]) => <option value={key}>{label}</option>),
            )}
          </Select>
          <Select
            className="filter"
            id="task-category"
            aria-label="Lọc danh mục"
          >
            <option value={``}>Danh mục công việc</option>
            {nodeList(
              state.workspace.categories.map((c) => (
                <option value={c.id}>{c.name}</option>
              )),
            )}
          </Select>
          <Select
            className="filter"
            id="task-priority"
            aria-label="Lọc mức ưu tiên"
          >
            <option value={``}>Ưu tiên</option>
            <option value="4">Khẩn</option>
            <option value="3">Cao</option>
            <option value="2">Vừa</option>
            <option value="1">Thấp</option>
          </Select>
          <details className="date-filters">
            <summary>Khoảng deadline</summary>
            <div>
              <label className="field">
                Từ ngày
                <input className="filter" id="task-from" type="date" />
              </label>
              <label className="field">
                Đến ngày
                <input className="filter" id="task-to" type="date" />
              </label>
            </div>
          </details>
        </div>
        <Slot id="task-list" as="div">
          {taskRows(
            state.workspace.tasks.filter(scopes[scope]?.[1] || scopes.all[1]),
          )}
        </Slot>
        <div className="task-list-footer">
          <Slot id="task-result-count" aria-live="polite" as="span">
            {
              state.workspace.tasks.filter(scopes[scope]?.[1] || scopes.all[1])
                .length
            }
            {" công việc"}
          </Slot>
          <span>Bấm tên công việc để xem chi tiết</span>
        </div>
      </section>
    </>
  );
}
export function filterTasks() {
  const search =
      document.querySelector("#task-search")?.value.toLowerCase() || "",
    status = document.querySelector("#task-status")?.value,
    category = document.querySelector("#task-category")?.value,
    priority = Number(document.querySelector("#task-priority")?.value || 0);
  const tasks = state.workspace.tasks.filter(
    (t) =>
      (scopes[state.taskScope || "all"]?.[1] || scopes.all[1])(t) &&
      (!search ||
        `${t.name} ${t.description || ""}`.toLowerCase().includes(search)) &&
      (!status || t.status === status) &&
      (!category || t.categoryId === category) &&
      (!priority || t.priority === priority) &&
      (!document.querySelector("#task-from")?.value ||
        dayKey(t.deadline) >= document.querySelector("#task-from").value) &&
      (!document.querySelector("#task-to")?.value ||
        dayKey(t.deadline) <= document.querySelector("#task-to").value),
  );
  setSlot(
    "task-list",
    tasks.length
      ? taskRows(tasks)
      : empty(
          "Không có công việc phù hợp",
          "Thử thay đổi từ khóa hoặc bộ lọc.",
          "",
          "",
        ),
  );
  setSlot("task-result-count", `${tasks.length} công việc`);
}
export function taskForm(task = {}, parentId = null) {
  const deadline =
    task.deadline || new Date(Date.now() + 86400000 * 2).toISOString();
  return (
    <form data-form="task" data-id={task.id || ""}>
      <input
        type="hidden"
        name="creationSource"
        defaultValue={task.creationSource || "MANUAL"}
      />
      <div className="form-grid">
        <div className="wide">
          {field(
            "Tên công việc",
            "name",
            "text",
            task.name || "",
            'required maxlength="200" placeholder="Ví dụ: Hoàn thiện báo cáo dự án"',
          )}
        </div>
        {field(
          "Deadline",
          "deadline",
          "datetime-local",
          localInput(deadline),
          "required",
        )}
        {field(
          "Thời gian dự kiến (phút)",
          "estimatedMinutes",
          "number",
          task.estimatedMinutes || 60,
          'required min="1" max="43200"',
        )}
        <label className="field">
          Mức ưu tiên
          <Select name="priority">
            {nodeList(
              [1, 2, 3, 4].map((p) => (
                <option value={p} selected={p === (task.priority || 2)}>
                  {["", "Thấp", "Vừa", "Cao", "Khẩn"][p]}
                </option>
              )),
            )}
          </Select>
        </label>
        <label className="field">
          Danh mục
          <Select name="categoryId">
            <option value={``}>Chưa phân loại</option>
            {nodeList(
              state.workspace.categories.map((c) => (
                <option value={c.id} selected={c.id === task.categoryId}>
                  {c.name}
                </option>
              )),
            )}
          </Select>
        </label>
      </div>
      <details className="form-section" open={task.id}>
        <summary>Tùy chọn thêm · mô tả, loại việc, cách chia thời gian</summary>
        <div className="form-grid">
          <label className="field wide">
            Mô tả
            <textarea
              name="description"
              maxLength="4000"
              defaultValue={task.description || ""}
            />
          </label>
          <label className="field">
            Loại công việc
            <Select name="taskType">
              {nodeList(
                Object.entries(typeLabels).map(([key, label]) => (
                  <option
                    value={key}
                    selected={key === (task.taskType || "OTHER")}
                  >
                    {label}
                  </option>
                )),
              )}
              {task.taskType && !typeLabels[task.taskType] ? (
                <option selected={true}>{task.taskType}</option>
              ) : (
                ""
              )}
            </Select>
          </label>
          {field(
            "Thời lượng phiên tối thiểu (phút)",
            "minimumSessionMinutes",
            "number",
            task.minimumSessionMinutes || 25,
            'min="5" max="480" required',
          )}
          <label className="checkbox wide">
            <input
              type="checkbox"
              name="isHardDeadline"
              defaultChecked={task.isHardDeadline}
            />
            Deadline cứng: không cho xếp lịch sau hạn chót
          </label>
          <label className="checkbox wide">
            <input
              type="checkbox"
              name="splittable"
              defaultChecked={task.splittable ?? true}
            />
            Cho phép chia thành nhiều phiên làm việc
          </label>
          <label className="field wide">
            Công việc cha
            <Select name="parentId">
              <option value={``}>Công việc độc lập</option>
              {nodeList(
                state.workspace.tasks
                  .filter((t) => !t.parentId && t.id !== task.id)
                  .map((t) => (
                    <option
                      value={t.id}
                      selected={t.id === (task.parentId || parentId)}
                    >
                      {t.name}
                    </option>
                  )),
              )}
            </Select>
          </label>
        </div>
      </details>
      <div className="form-error" />
      <div className="form-actions">
        {button("Hủy", "close-modal", "ghost")}
        <button type="submit" className="btn primary">
          {task.id ? "Lưu thay đổi" : "Tạo công việc"}
        </button>
      </div>
    </form>
  );
}
export function openTask(task = {}, parentId = null) {
  modal(
    task.id ? "Chỉnh sửa công việc" : "Thêm công việc",
    "Bắt đầu với tên, deadline và thời gian dự kiến.",
    taskForm(task, parentId),
  );
}
export function taskInput(data) {
  return {
    name: String(data.get("name")),
    description: String(data.get("description") || ""),
    deadline: toUtc(String(data.get("deadline"))),
    estimatedMinutes: Number(data.get("estimatedMinutes")),
    priority: Number(data.get("priority")),
    categoryId: data.get("categoryId") || null,
    parentId: data.get("parentId") || null,
    taskType: String(data.get("taskType") || "OTHER"),
    isHardDeadline: data.has("isHardDeadline"),
    creationSource: String(data.get("creationSource") || "MANUAL"),
    splittable: data.has("splittable"),
    minimumSessionMinutes: Number(data.get("minimumSessionMinutes") || 25),
  };
}
export function openDetail(id, afterSession = false) {
  const t = taskById(id);
  if (!t) return;
  const children = state.workspace.tasks.filter((x) => x.parentId === id);
  const deps = t.dependencies.map(taskById).filter(Boolean);
  modal(
    t.name,
    afterSession
      ? "Lượt làm vừa kết thúc. Cập nhật phần công việc bạn đã hoàn thành."
      : "Chi tiết công việc",
    <>
      {afterSession && (
        <div className="info post-session-help section-gap">
          <h3>Bạn đã làm xong cả công việc chưa?</h3>
          <p>
            Xong phiên chỉ kết thúc lượt làm và dừng đồng hồ. Nếu còn việc, cập
            nhật tiến độ bên dưới để tiếp tục vào lần sau.
          </p>
          {!["Completed", "Cancelled"].includes(t.status) &&
            button(
              "Đã hoàn thành cả công việc",
              "task-finish",
              "primary small",
              id,
              "check",
            )}
        </div>
      )}
      <div className="actions">
        {badge(
          statusLabels[t.status],
          t.status === "Completed" ? "green" : "purple",
        )}
        {badge(typeLabels[t.taskType] || t.taskType)}
        {badge(`Ưu tiên ${t.priority}`)}
        {badge(t.isHardDeadline ? "Deadline cứng" : "Deadline mềm")}
      </div>
      <dl className="detail-list">
        <div>
          <dt>Deadline</dt>
          <dd>
            {dateFmt(t.deadline)}
            {" · "}
            {timeFmt(t.deadline)}
          </dd>
        </div>
        <div>
          <dt>Thời gian dự kiến / điều chỉnh</dt>
          <dd>
            {minutes(t.estimatedMinutes)}
            {" / "}
            {minutes(t.adjustedMinutes)}
          </dd>
        </div>
        <div>
          <dt>Đã thực hiện / đang lên lịch</dt>
          <dd>
            {minutes(t.actualMinutes)}
            {" / "}
            {minutes(t.plannedMinutes)}
          </dd>
        </div>
        <div>
          <dt>Tiến độ</dt>
          <dd>{t.progress}%</dd>
        </div>
      </dl>
      {t.description ? (
        <p className="detail-description">{t.description}</p>
      ) : (
        ""
      )}
      <form data-form="task-progress" data-id={id}>
        <div className="form-grid">
          {field(
            "Tiến độ (%)",
            "progress",
            "number",
            t.progress,
            'min="0" max="100" required',
          )}
          <label className="field">
            Trạng thái
            <Select name="status">
              {nodeList(
                ["Todo", "InProgress", "Completed", "Cancelled"].map((key) => (
                  <option value={key} selected={key === t.status}>
                    {statusLabels[key]}
                  </option>
                )),
              )}
            </Select>
          </label>
        </div>
        <div className="form-error" />
        <div className="form-actions">
          <button type="submit" className="btn secondary">
            Cập nhật tiến độ
          </button>
        </div>
      </form>
      <div className="spaced">
        <h3>Công việc con</h3>
        {children.length ? (
          nodeList(
            children.map((c) => (
              <div className="list-line">
                <span>{c.name}</span>
                {badge(statusLabels[c.status])}
              </div>
            )),
          )
        ) : (
          <p className="muted">
            <small>Chưa có công việc con.</small>
          </p>
        )}
        <div className="actions">
          {button("Thêm việc con", "subtask", "small secondary", id, "plus")}
          {button("Gợi ý chia nhỏ", "breakdown", "small ghost", id, "spark")}
        </div>
      </div>
      <div className="spaced">
        <h3>Phải hoàn thành trước</h3>
        {nodeList(
          deps.map((d) => (
            <div className="list-line">
              <span>{d.name}</span>
              {button(
                "Bỏ phụ thuộc",
                "remove-dependency",
                "small ghost",
                `${id}|${d.id}`,
                "close",
              )}
            </div>
          )),
        )}
        <form data-form="dependency" data-id={id}>
          <div className="actions">
            <Select name="predecessor" className="filter">
              {nodeList(
                state.workspace.tasks
                  .filter((x) => x.id !== id && !t.dependencies.includes(x.id))
                  .map((x) => <option value={x.id}>{x.name}</option>),
              )}
            </Select>
            <button type="submit" className="btn small secondary">
              Thêm phụ thuộc
            </button>
          </div>
          <div className="form-error" />
        </form>
      </div>
      <div className="form-actions">
        {button("Xóa công việc", "delete-task", "danger small", id, "trash")}
        {button(
          "Lập lại lịch liên quan",
          "replan-task",
          "secondary small",
          id,
          "refresh",
        )}
        {button("Chỉnh sửa", "edit-task", "primary small", id, "edit")}
      </div>
    </>,
  );
}
export function captureModal() {
  modal(
    "Ghi nhanh công việc",
    "Bạn kiểm tra gợi ý trước khi lưu.",
    <form data-form="capture">
      <label className="field">
        Mô tả bằng câu tự nhiên
        <textarea
          name="text"
          required={true}
          maxLength="2000"
          placeholder="Làm báo cáo SWP trước thứ Sáu, khoảng 3 tiếng"
          defaultValue={[]}
        />
      </label>
      <label className="checkbox spaced">
        <input type="checkbox" name="useAi" />
        Cho phép gửi nội dung này đến AI nếu đã cấu hình
      </label>
      <div className="form-error" />
      <div className="form-actions">
        <button className="btn primary" type="submit">
          {icon("spark")}
          {" Tạo gợi ý"}
        </button>
      </div>
    </form>,
  );
}
export async function breakdownModal(id) {
  const t = taskById(id);
  const result = await api(`/api/tasks/${id}/breakdown`, "POST");
  state.breakdown = result;
  modal(
    "Chia nhỏ công việc",
    `Gợi ý từ ${["OpenAI", "Gemini"].includes(result.source) ? result.source : "quy tắc mẫu"} · chỉ lưu những mục bạn chọn.`,
    <>
      <form data-form="breakdown" data-id={id}>
        <div className="info">
          Công việc cha trở thành nhóm; chỉ các việc con được tính vào workload
          để tránh cộng trùng.
        </div>
        <div className="spaced">
          {nodeList(
            result.subtasks.map((s, index) => (
              <label className="list-line">
                <span className="checkbox">
                  <input
                    type="checkbox"
                    name="part"
                    defaultValue={index}
                    defaultChecked={true}
                  />
                  {s.name}
                </span>
                <small>{minutes(s.estimatedMinutes)}</small>
              </label>
            )),
          )}
        </div>
        <div className="form-error" />
        <div className="form-actions">
          <button type="submit" className="btn primary">
            Lưu các việc con đã chọn
          </button>
        </div>
      </form>
      <div className="spaced">
        <label className="checkbox">
          <input type="checkbox" id="breakdown-ai" />
          Cho phép gửi tên và mô tả công việc đến AI
        </label>
        {button("Thử gợi ý AI", "ai-breakdown", "small ghost", t.id, "spark")}
      </div>
    </>,
  );
}
