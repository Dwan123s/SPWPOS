import { useRef, useState } from "react";
import { dateFmt, icon, localInput, minutes, toUtc } from "./ui.jsx";
import { NoteImpact } from "./note-impact.jsx";
import { NoteLibrary } from "./note-library.jsx";
import { NoteWritingTools } from "./note-writing-tools.jsx";
import { noteAppearanceStyle } from "./note-appearance.js";
import { NoteActivitiesReview } from "./note-activities.jsx";

const priorities = ["", "Thấp", "Vừa", "Cao", "Khẩn"];
const types = {
  WORK: "Công việc",
  OTHER: "Khác",
  PROGRAMMING: "Lập trình",
  WRITING: "Viết nội dung",
  STUDY: "Học tập",
  MEETING_PREP: "Chuẩn bị họp",
  ADMIN: "Hành chính",
};

export function NoteStudio({
  notes,
  note,
  drafts,
  agent,
  imported,
  hasHistory = imported,
  autosaveStatus = "",
  busy,
  dirty,
  hasAiAnalysis,
  hasTime,
  horizonDays,
  setHorizonDays,
  libraryOpen,
  setLibraryOpen,
  select,
  setNote,
  analyze,
  saveDraft,
  requestSchedule,
  updateDraft,
  proposalIssue,
  workspace,
  checklist,
  stage,
  setStage,
  feedback,
  availability,
  onSetTime,
  onDelete,
  navigationPrompt,
  appearance,
  setAppearance,
  prepareActivities,
  applyActivities,
  onActivityReviewDirty,
}) {
  const [focusMode, setFocusMode] = useState(false);
  const editor = useRef(null);
  const typeMenu = useRef(null);
  const openedAt = useRef(new Date().toISOString());
  const reviewHeading = useRef(null);
  const reviewing = stage === "review" && !!note.analysis;
  const goWrite = () => {
    setStage("write");
    requestAnimationFrame(() => editor.current?.focus());
  };
  const goReview = () => {
    setStage("review");
    requestAnimationFrame(() => reviewHeading.current?.focus());
  };
  const insertList = (marker) => {
    const element = editor.current;
    const start = element?.selectionStart ?? note.content.length;
    const end = element?.selectionEnd ?? start;
    const prefix = start > 0 && note.content[start - 1] !== "\n" ? "\n" : "";
    const insertion = prefix + marker;
    setNote({
      ...note,
      content: (
        note.content.slice(0, start) +
        insertion +
        note.content.slice(end)
      ).slice(0, 8000),
    });
    requestAnimationFrame(() => {
      element?.focus();
      element?.setSelectionRange(
        start + insertion.length,
        start + insertion.length,
      );
    });
  };
  return (
    <div
      className={`notes-workspace note-studio ${reviewing ? "is-review" : "is-writing"}${note.createdTaskIds?.length ? " has-checklist" : ""}${focusMode && !reviewing ? " is-focus" : ""}${appearance.ruled ? " is-ruled" : ""}`}
      style={noteAppearanceStyle(appearance)}
    >
      <NoteLibrary
        notes={notes}
        currentId={note.id}
        busy={busy}
        open={libraryOpen}
        setOpen={setLibraryOpen}
        select={select}
        onDelete={onDelete}
        hidden={focusMode && !reviewing}
      />
      <div className="note-studio-main">
        {navigationPrompt}
        <div className="note-flow-bar">
          <ol className="note-flow" aria-label="Các bước từ ghi chú đến lịch">
            <li className={!reviewing ? "current" : "done"}>
              <button
                type="button"
                aria-current={!reviewing ? "step" : undefined}
                disabled={!!busy}
                onClick={goWrite}
              >
                <span>1</span> Nhập việc
              </button>
            </li>
            <li className={reviewing ? "current" : ""}>
              <button
                type="button"
                aria-current={reviewing ? "step" : undefined}
                disabled={!!busy || !note.analysis}
                onClick={goReview}
              >
                <span>2</span> Kiểm tra
              </button>
            </li>
            <li className={note.planId ? "done" : ""}>
              <span className="note-flow-final">
                <span>3</span> Lưu lịch
              </span>
            </li>
          </ol>
          <NoteWritingTools
            busy={busy}
            hidden={reviewing}
            imported={imported}
            typeMenu={typeMenu}
            appearance={appearance}
            setAppearance={setAppearance}
            insertList={insertList}
          />
          <div className="note-desk-actions">
            {note.id && (
              <button
                type="button"
                className="note-delete-button"
                aria-label="Xóa ghi chú"
                title="Xóa ghi chú"
                disabled={!!busy}
                onClick={() => onDelete(note)}
              >
                {icon("trash")}
              </button>
            )}
            {!reviewing && (
              <button
                type="button"
                className="note-focus-toggle"
                aria-label={focusMode ? "Thoát tập trung" : "Tập trung viết"}
                title={focusMode ? "Thoát tập trung" : "Tập trung viết"}
                aria-pressed={focusMode}
                onClick={() => setFocusMode(!focusMode)}
              >
                <NoteGlyph name={focusMode ? "collapse" : "focus"} />
                <span>{focusMode ? "Thoát tập trung" : "Tập trung viết"}</span>
              </button>
            )}
          </div>
        </div>
        <section
          className="card note-paper"
          aria-labelledby="note-editor-title"
          hidden={reviewing}
        >
          <form
            onSubmit={(event) => {
              event.preventDefault();
              saveDraft();
            }}
          >
            <fieldset className="note-fields" disabled={!!busy}>
              <label className="note-title-field">
                <span className="sr-only">Tiêu đề ghi chú</span>
                <input
                  aria-label="Tiêu đề ghi chú"
                  maxLength={120}
                  value={note.title}
                  onChange={(event) =>
                    setNote({ ...note, title: event.target.value })
                  }
                  placeholder="Ghi chú mới"
                />
              </label>
              <div className="note-paper-top">
                <h2 id="note-editor-title" className="sr-only">
                  Bạn cần làm những việc gì?
                </h2>
                <time dateTime={note.updatedAt || openedAt.current}>
                  {dateFmt(note.updatedAt || openedAt.current, {
                    day: "numeric",
                    month: "long",
                    year: "numeric",
                    hour: "2-digit",
                    minute: "2-digit",
                  })}
                </time>
                <span
                  className={`note-save-status ${dirty || !note.id ? "is-unsaved" : ""}`}
                  role="status"
                >
                  <span className="note-save-dot" aria-hidden="true" />
                  {autosaveStatus === "Đang tự động lưu…"
                    ? autosaveStatus
                    : note.id
                      ? dirty
                        ? "Chờ tự động lưu…"
                        : autosaveStatus || "Đã lưu ghi chú"
                      : "Ghi chú mới"}
                </span>
              </div>
              <label className="note-body-field">
                <span className="sr-only">Nội dung ghi chú</span>
                <textarea
                  ref={editor}
                  aria-label="Nội dung ghi chú"
                  required
                  maxLength={8000}
                  rows={8}
                  value={note.content}
                  onChange={(event) =>
                    setNote({ ...note, content: event.target.value })
                  }
                  placeholder={
                    "Bạn đang nghĩ gì?\nGhi lại việc cần làm, thời lượng và hạn chót nếu biết."
                  }
                />
              </label>
              {!reviewing && checklist}
              <div className="note-editor-footer">
                <div className="note-editor-caption">
                  <p className="note-privacy">
                    {icon("shield")} Ghi chú của bạn chỉ gửi đến AI khi bạn chọn
                    xem trước hoặc tạo lịch.
                  </p>
                  <span className="note-character-count">
                    {note.content.length.toLocaleString("vi-VN")}/8.000 ký tự
                  </span>
                </div>
                <div className="form-actions note-editor-actions">
                  <button type="submit" className="btn secondary">
                    {hasHistory ? "Lưu ghi chú" : "Lưu bản nháp"}
                  </button>
                  <button
                    type="button"
                    className="btn primary"
                    disabled={!agent?.aiAvailable || !note.content.trim()}
                    onClick={analyze}
                  >
                    {icon("spark")}{" "}
                    {hasHistory ? "Nhận diện việc mới" : "Xem trước công việc"}{" "}
                    {icon("arrow")}
                  </button>
                </div>
              </div>
              {!busy && !agent?.aiAvailable && (
                <p className="warning spaced">
                  AI chưa sẵn sàng. Bạn vẫn có thể lưu bản nháp hoặc{" "}
                  <a href="#tasks">tự thêm công việc</a> để lập kế hoạch.
                </p>
              )}
              <details className="note-quick-options">
                <summary>Tùy chọn tạo lịch nhanh</summary>
                <label className="field spaced">
                  Số ngày tự xếp lịch
                  <input
                    type="number"
                    min={1}
                    max={30}
                    value={horizonDays}
                    onChange={(event) => setHorizonDays(event.target.value)}
                  />
                </label>
                <p className="reason spaced">
                  Ghi chú có sự kiện hoặc thông tin còn thiếu sẽ mở bước kiểm
                  tra trước khi lưu lịch.
                </p>
                <button
                  type="button"
                  className="btn secondary"
                  disabled={!agent?.aiAvailable}
                  onClick={() => requestSchedule("auto")}
                >
                  Tạo và lưu lịch ngay
                </button>
              </details>
            </fieldset>
          </form>
          {hasHistory && (
            <div className="note-sync-banner info spaced">
              <p>
                {icon("check")} Bạn có thể tiếp tục viết vào ghi chú này. Bản
                nháp tự động lưu; bấm <strong>Nhận diện việc mới</strong> để AI
                tìm phần bổ sung rồi xem lịch đề xuất. Các công việc và sự kiện
                đã tạo được giữ nguyên.
              </p>
              <div className="form-actions spaced">
                <button
                  type="button"
                  className="btn secondary small"
                  onClick={goReview}
                >
                  Xem công việc và lịch
                </button>
                <a href="#tasks" className="btn ghost small">
                  Quản lý công việc
                </a>
              </div>
            </div>
          )}
          {!hasTime && (
            <p className="note-soft-help">
              Bạn có thể viết trước, chọn <a href="#time">giờ rảnh</a> ở bước
              tiếp theo.
            </p>
          )}
          {dirty && note.analysis && (
            <p className="warning spaced">
              Bạn đã sửa nội dung. Phân tích lại để cập nhật các gợi ý.
            </p>
          )}
        </section>
        {feedback}
        {availability}
        {reviewing && checklist}
        {note.analysis && (
          <section
            className="note-review"
            aria-labelledby="note-analysis-title"
            hidden={!reviewing}
          >
            <div className="note-review-header">
              <div>
                <span className="note-section-kicker">
                  TỪ GHI CHÚ ĐẾN KẾ HOẠCH
                </span>
                <h2 id="note-analysis-title" ref={reviewHeading} tabIndex={-1}>
                  {note.analysis.activities
                    ? note.planId
                      ? "Lịch đã được lưu"
                      : "Xem trước lịch & công việc"
                    : imported
                      ? "Công việc đã được lưu"
                      : "Xem trước công việc"}
                </h2>
                <p>
                  {note.analysis.activities ? (
                    note.analysis.activities.some(
                      (activity) => activity.kind === "TASK",
                    ) ? (
                      "Chọn việc muốn tạo và kiểm tra lịch. Các việc mới sẽ có ô tick hoàn thành trong Note sau khi bạn xác nhận lưu."
                    ) : (
                      "Kiểm tra ngày giờ trước khi xác nhận. Các sự kiện sẽ xuất hiện trên Lịch sau khi bạn lưu."
                    )
                  ) : (
                    <>
                      Phân tích bằng {note.analysis.source} ·{" "}
                      {imported
                        ? "Lịch chỉ được lưu khi bạn xác nhận."
                        : "Chọn việc cần làm, chỉnh thời lượng và kiểm tra lịch."}
                    </>
                  )}
                </p>
              </div>
              <button
                type="button"
                className="btn secondary small"
                disabled={!!busy}
                onClick={goWrite}
                aria-label="Sửa ghi chú"
                title="Sửa ghi chú"
              >
                {icon("edit")} <span>Sửa ghi chú</span>
              </button>
            </div>
            {note.analysis.activities ? (
              <NoteActivitiesReview
                key={`${note.id}/${note.version}`}
                note={note}
                busy={busy}
                dirty={dirty}
                hasAiAnalysis={hasAiAnalysis}
                workspace={workspace}
                hasTime={hasTime}
                onSetTime={onSetTime}
                prepare={prepareActivities}
                apply={applyActivities}
                onReviewDirty={onActivityReviewDirty}
              />
            ) : (
              <div className="note-review-grid">
                <form
                  className="card note-review-tasks"
                  noValidate
                  onSubmit={(event) => {
                    event.preventDefault();
                    requestSchedule("proposal");
                  }}
                >
                  <div className="note-task-list-heading">
                    <h3>
                      {imported
                        ? `${drafts.length} việc đã lưu`
                        : `${drafts.filter((task) => task.selected).length}/${drafts.length} việc được chọn`}
                    </h3>
                    <span>
                      {minutes(
                        drafts
                          .filter((task) => task.selected)
                          .reduce(
                            (sum, task) =>
                              sum + Number(task.estimatedMinutes || 0),
                            0,
                          ),
                      )}
                    </span>
                  </div>
                  {!drafts.length && (
                    <p className="info">
                      Chưa có gợi ý công việc. Kiểm tra nội dung và phân tích
                      lại.
                    </p>
                  )}
                  <fieldset
                    className="note-fields"
                    disabled={!!busy || imported || dirty || !hasAiAnalysis}
                    hidden={imported}
                  >
                    {drafts.map((task, index) => (
                      <NoteDraftRow
                        key={index}
                        task={task}
                        index={index}
                        update={updateDraft}
                      />
                    ))}
                  </fieldset>
                  {imported && (
                    <div className="note-created-tasks">
                      {drafts.map((task, index) => (
                        <article key={index}>
                          <strong>{task.name}</strong>
                          <p>
                            {minutes(task.estimatedMinutes)} · Hạn{" "}
                            {dateFmt(task.deadline, {
                              hour: "2-digit",
                              minute: "2-digit",
                            })}
                          </p>
                        </article>
                      ))}
                    </div>
                  )}
                  {!!note.analysis.messages?.length && (
                    <details className="note-analysis-explanation">
                      <summary>
                        {note.analysis.messages.length} lưu ý từ ghi chú
                      </summary>
                      <ul>
                        {note.analysis.messages.map((text, index) => (
                          <li key={index}>{text}</li>
                        ))}
                      </ul>
                    </details>
                  )}
                  {note.analysis.needsReview && (
                    <p className="warning spaced">
                      AI chưa thể tự áp dụng các điều kiện trong ghi chú. Kiểm
                      tra lưu ý, giờ rảnh và phụ thuộc trước khi lưu lịch.
                    </p>
                  )}
                  {!hasAiAnalysis && !imported && (
                    <p className="warning spaced">
                      Kết quả này chưa được phân tích bằng AI. Sửa ghi chú rồi
                      chọn Xem trước công việc để cập nhật.
                    </p>
                  )}
                  {!note.planId && (
                    <div className="note-proposal-footer">
                      <label className="note-horizon">
                        Xếp trong{" "}
                        <input
                          aria-label="Số ngày xếp lịch"
                          type="number"
                          min={1}
                          max={30}
                          value={horizonDays}
                          disabled={!!busy}
                          onChange={(event) =>
                            setHorizonDays(event.target.value)
                          }
                        />{" "}
                        ngày tới
                      </label>
                      <p
                        id="note-proposal-help"
                        className={
                          proposalIssue ? "warning spaced" : "reason spaced"
                        }
                      >
                        {proposalIssue ||
                          (!hasTime
                            ? "Bạn sẽ chọn giờ rảnh trước khi tạo công việc và lịch đề xuất."
                            : "Công việc sẽ được tạo; bạn xem và xác nhận lịch ở bước tiếp theo.")}
                      </p>
                      <button
                        type="submit"
                        className="btn primary"
                        aria-describedby="note-proposal-help"
                        disabled={!!busy || !!proposalIssue}
                      >
                        {imported
                          ? "Xem lịch đề xuất"
                          : "Tạo công việc & xem lịch"}{" "}
                        {icon("arrow")}
                      </button>
                    </div>
                  )}
                  {note.planId && (
                    <div className="form-actions">
                      <a className="btn primary" href="#today">
                        Mở Hôm nay để làm việc
                      </a>
                    </div>
                  )}
                  {imported && !note.planId && (
                    <button
                      type="button"
                      className="btn ghost small spaced"
                      disabled={!!busy}
                      onClick={() => requestSchedule("auto")}
                    >
                      Tiếp tục tự xếp lịch
                    </button>
                  )}
                </form>
                <div className="note-review-context">
                  <NoteImpact
                    drafts={drafts}
                    horizonDays={horizonDays}
                    hasTime={hasTime}
                    enabled={
                      reviewing && !busy && !imported && !dirty && hasAiAnalysis
                    }
                    workspace={workspace}
                    onSetTime={onSetTime}
                    showPreview
                  />
                  {imported && (
                    <div className="card note-preview-placeholder">
                      {icon("calendar")}
                      <h3>Tiếp tục với lịch của bạn</h3>
                      <p>
                        Các công việc đã được lưu. Mở lịch đề xuất để kiểm tra
                        và chọn Lưu lịch này.
                      </p>
                      <a className="btn secondary" href="#calendar">
                        Mở lịch làm việc
                      </a>
                    </div>
                  )}
                  {!imported &&
                    (dirty ||
                      !hasAiAnalysis ||
                      !drafts.some((task) => task.selected)) && (
                      <div className="card note-preview-placeholder">
                        {icon("calendar")}
                        <h3>Lịch hiện ở đây sau khi kiểm tra</h3>
                        <p>
                          Chọn công việc và phân tích nội dung mới nhất để xem
                          lịch thử.
                        </p>
                      </div>
                    )}
                </div>
              </div>
            )}
          </section>
        )}
      </div>
    </div>
  );
}

function NoteGlyph({ name }) {
  if (name === "notebook")
    return (
      <svg viewBox="0 0 36 40" fill="none" aria-hidden="true">
        <rect x="3" y="2" width="30" height="36" rx="7" fill="currentColor" />
        <path
          d="M3 12h30v19a7 7 0 0 1-7 7H10a7 7 0 0 1-7-7V12Z"
          fill="#fffdf5"
        />
        <path
          d="M10 19h16M10 24h16M10 29h11"
          stroke="#d4c9a5"
          strokeWidth="1.6"
          strokeLinecap="round"
        />
        <path
          d="M12 2v5M18 2v5M24 2v5"
          stroke="#9e771b"
          strokeOpacity=".4"
          strokeLinecap="round"
        />
      </svg>
    );
  const paths = {
    checklist: "M9 6h11M9 12h11M9 18h11M2 5l2 2 3-4M2 11l2 2 3-4M2 17l2 2 3-4",
    ruled:
      "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2ZM7 8h10M7 12h10M7 16h10",
    focus: "M8 3H3v5M16 3h5v5M3 16v5h5M21 16v5h-5",
    collapse: "M3 8h5V3M21 8h-5V3M8 21v-5H3M16 21v-5h5",
  };
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d={paths[name]} />
    </svg>
  );
}

function NoteDraftRow({ task, index, update }) {
  return (
    <article
      className={`note-task-draft ${task.selected ? "is-selected" : ""}`}
    >
      <div className="note-draft-line">
        <label className="note-draft-check">
          <input
            type="checkbox"
            aria-label={`Việc ${index + 1}`}
            checked={task.selected}
            onChange={(event) =>
              update(index, "selected", event.target.checked)
            }
          />
          <span aria-hidden="true">{icon("check")}</span>
        </label>
        <label className="note-draft-name">
          <span className="sr-only">Tên công việc</span>
          <input
            aria-label="Tên công việc"
            maxLength={200}
            value={task.name}
            onChange={(event) => update(index, "name", event.target.value)}
          />
        </label>
      </div>
      <details className="note-draft-details">
        <summary>
          <span>
            {minutes(task.estimatedMinutes)} ·{" "}
            {task.deadline ? `Hạn ${dateFmt(task.deadline)}` : "Chưa có hạn"}
          </span>
          <span className="note-draft-edit-label">Chỉnh sửa</span>
        </summary>
        <div className="form-grid">
          <label className="field">
            Deadline
            <input
              type="datetime-local"
              disabled={!task.selected}
              value={task.deadline ? localInput(task.deadline) : ""}
              onChange={(event) =>
                update(
                  index,
                  "deadline",
                  event.target.value ? toUtc(event.target.value) : "",
                )
              }
            />
          </label>
          <label className="field">
            Thời gian dự kiến (phút)
            <input
              type="number"
              min={1}
              max={43200}
              disabled={!task.selected}
              value={task.estimatedMinutes}
              onChange={(event) =>
                update(index, "estimatedMinutes", Number(event.target.value))
              }
            />
          </label>
          <label className="field">
            Mức ưu tiên
            <select
              value={task.priority}
              onChange={(event) =>
                update(index, "priority", Number(event.target.value))
              }
            >
              {[1, 2, 3, 4].map((priority) => (
                <option key={priority} value={priority}>
                  {priorities[priority]}
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            Loại công việc
            <select
              value={types[task.taskType] ? task.taskType : "OTHER"}
              onChange={(event) =>
                update(index, "taskType", event.target.value)
              }
            >
              {Object.entries(types).map(([key, label]) => (
                <option key={key} value={key}>
                  {label}
                </option>
              ))}
            </select>
          </label>
        </div>
        <label className="checkbox spaced">
          <input
            type="checkbox"
            checked={!!task.isHardDeadline}
            onChange={(event) =>
              update(index, "isHardDeadline", event.target.checked)
            }
          />
          Deadline cứng: không xếp sau hạn chót
        </label>
      </details>
    </article>
  );
}
