import { useEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { state, dateFmt, localInput, toUtc } from "./ui.jsx";
import { planModal } from "./calendar.jsx";
import { Loading } from "./loading.jsx";

const blank = () => ({ title: "", content: "" });
const priorityLabels = ["", "Thấp", "Vừa", "Cao", "Khẩn"];
const typeLabels = {
  OTHER: "Khác",
  PROGRAMMING: "Lập trình",
  WRITING: "Viết nội dung",
  STUDY: "Học tập",
  MEETING_PREP: "Chuẩn bị họp",
  DESIGN: "Thiết kế",
  ADMIN: "Hành chính",
};

export function NotesPage({ onWorkspaceChanged }) {
  const [notes, setNotes] = useState([]);
  const [note, setNote] = useState(blank);
  const [drafts, setDrafts] = useState([]);
  const [useAi, setUseAi] = useState(false);
  const [busy, setBusy] = useState("Đang tải ghi chú…");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [horizonDays, setHorizonDays] = useState(
    state.workspace?.preference?.planningHorizonDays || 14,
  );
  const locked = useRef(false);
  const alive = useRef(true);
  const imported = !!note.createdTaskIds?.length;
  const dirty =
    !!note.id &&
    notes.some(
      (n) =>
        n.id === note.id &&
        (n.title !== note.title || n.content !== note.content),
    );
  const hasTime =
    !!state.workspace?.availability?.length ||
    !!state.workspace?.exceptions?.some((e) => e.isAvailable);

  useEffect(() => {
    alive.current = true;
    api("/api/notes")
      .then((items) => {
        if (alive.current) setNotes(items);
      })
      .catch((e) => {
        if (alive.current) setError(e.message);
      })
      .finally(() => {
        if (alive.current) setBusy("");
      });
    return () => {
      alive.current = false;
    };
  }, []);

  const select = (item) => {
    setNote(item);
    setDrafts(
      (item.analysis?.tasks || []).map((task) => ({ ...task, selected: true })),
    );
    setError("");
    setMessage("");
  };
  const remember = (saved) => {
    setNotes((items) => [saved, ...items.filter((n) => n.id !== saved.id)]);
    setNote(saved);
    return saved;
  };
  const run = async (label, operation) => {
    if (locked.current) return;
    locked.current = true;
    setBusy(label);
    setError("");
    setMessage("");
    try {
      await operation();
    } catch (e) {
      if (alive.current) setError(e.message);
    } finally {
      locked.current = false;
      if (alive.current) setBusy("");
    }
  };
  const save = async () => {
    if (!note.title.trim() || !note.content.trim())
      throw new Error("Nhập tiêu đề và nội dung ghi chú.");
    if (note.id && !dirty) return note;
    const saved = await api(
      `/api/notes${note.id ? `/${note.id}` : ""}`,
      note.id ? "PUT" : "POST",
      {
        title: note.title,
        content: note.content,
        version: note.version,
      },
    );
    if (alive.current) {
      remember(saved);
      setDrafts([]);
    }
    return saved;
  };
  const analyze = () =>
    run("Agent đang đọc và phân tích ghi chú…", async () => {
      const saved = await save();
      const result = await api(`/api/notes/${saved.id}/analyze`, "POST", {
        version: saved.version,
        useAi,
      });
      if (alive.current) {
        remember(result);
        setDrafts(
          result.analysis.tasks.map((task) => ({ ...task, selected: true })),
        );
        setMessage(
          result.analysis.tasks.length
            ? "Đã phân tích. Kiểm tra các công việc trước khi tạo lịch."
            : "Chưa tìm thấy công việc. Bổ sung việc cần làm rồi phân tích lại.",
        );
      }
    });
  const autoSchedule = () =>
    run("Agent đang phân tích, tạo công việc và tự xếp lịch…", async () => {
      const saved = imported ? note : await save();
      try {
        const result = await api(`/api/notes/${saved.id}/schedule`, "POST", {
          version: saved.version,
          useAi,
          horizonDays: Number(horizonDays),
        });
        if (!alive.current) return;
        remember(result.note);
        setDrafts(
          result.note.analysis.tasks.map((task) => ({
            ...task,
            selected: true,
          })),
        );
        await onWorkspaceChanged();
        if (!alive.current) return;
        setMessage(
          `Đã tự động áp dụng lịch với ${result.plan.sessions.filter((s) => !s.preserved).length} phiên mới. ${result.plan.warnings.length ? `${result.plan.warnings.length} công việc có cảnh báo; xem chi tiết kết quả.` : "Bạn có thể bắt đầu làm việc trong mục Hôm nay."}`,
        );
        planModal(result.plan);
      } catch (failure) {
        const latest = await api("/api/notes").catch(() => null);
        if (latest && alive.current) {
          setNotes(latest);
          const recovered = latest.find((n) => n.id === saved.id);
          if (recovered) {
            setNote(recovered);
            setDrafts(
              (recovered.analysis?.tasks || []).map((task) => ({
                ...task,
                selected: true,
              })),
            );
            if (recovered.createdTaskIds.length)
              setMessage(
                "Các công việc đã được lưu. Bấm Tiếp tục tự xếp lịch để thử lại; công việc sẽ không được tạo trùng.",
              );
          }
        }
        throw failure;
      }
    });
  const propose = (createTasks) =>
    run("Đang chuẩn bị công việc và lịch đề xuất…", async () => {
      let saved = note;
      if (createTasks) {
        if (dirty)
          throw new Error(
            "Nội dung đã thay đổi. Phân tích lại ghi chú trước khi tạo công việc.",
          );
        const tasks = drafts
          .filter((d) => d.selected)
          .map(({ selected, ...task }) => ({
            ...task,
            categoryId: null,
            parentId: null,
            creationSource: "NATURAL_LANGUAGE",
          }));
        if (!tasks.length) throw new Error("Chọn ít nhất một công việc.");
        saved = await api(`/api/notes/${note.id}/tasks`, "POST", {
          version: note.version,
          tasks,
        });
        if (alive.current) {
          remember(saved);
          setDrafts(
            saved.analysis.tasks.map((task) => ({ ...task, selected: true })),
          );
          setMessage(
            "Đã lưu công việc. Nếu tạo lịch chưa thành công, bấm Xem lịch đề xuất để thử lại.",
          );
        }
      }
      await onWorkspaceChanged();
      const proposal = await api("/api/plans/reschedule", "POST", {
        start: new Date().toISOString(),
        horizonDays: Number(horizonDays),
        affectedTaskIds: saved.createdTaskIds,
        isReschedule: true,
        triggerEvent: "NEW_TASK",
      });
      if (alive.current) {
        setMessage(
          "Đã tạo lịch đề xuất. Xem cảnh báo và chọn Áp dụng kế hoạch để lưu lịch.",
        );
        planModal(proposal);
      }
    });
  const updateDraft = (index, key, value) =>
    setDrafts((items) =>
      items.map((d, i) => (i === index ? { ...d, [key]: value } : d)),
    );

  return (
    <div className="notes-workspace">
      <section
        className="card notes-library"
        aria-labelledby="note-library-title"
      >
        <div className="card-header">
          <h2 id="note-library-title">Ghi chú của bạn</h2>
          <button
            type="button"
            className="btn secondary small"
            disabled={!!busy}
            onClick={() => select(blank())}
          >
            Ghi chú mới
          </button>
        </div>
        <p className="muted">
          Lưu điều cần làm, rồi để agent giúp chuyển thành kế hoạch.
        </p>
        <div className="notes-list">
          {notes.map((item) => (
            <button
              key={item.id}
              type="button"
              className={`note-list-item ${note.id === item.id ? "active" : ""}`}
              disabled={!!busy}
              aria-pressed={note.id === item.id}
              onClick={() => select(item)}
            >
              <strong>{item.title}</strong>
              <span>{item.content.slice(0, 110)}</span>
              <small>
                {dateFmt(item.updatedAt)} ·{" "}
                {item.createdTaskIds.length
                  ? "Đã tạo công việc"
                  : item.analysis
                    ? "Đã phân tích"
                    : "Bản ghi chú"}
              </small>
            </button>
          ))}
          {!notes.length && !busy && (
            <p className="reason">
              Chưa có ghi chú. Viết ghi chú đầu tiên ở bên cạnh.
            </p>
          )}
        </div>
      </section>
      <div className="stack">
        <section className="card" aria-labelledby="note-editor-title">
          <div className="card-header">
            <div>
              <h2 id="note-editor-title">Từ ghi chú đến lịch làm việc</h2>
              <p>
                Ghi lại các việc bạn cần làm, thời lượng và hạn chót nếu đã
                biết.
              </p>
            </div>
          </div>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              run("Đang lưu ghi chú…", async () => {
                await save();
                setMessage("Đã lưu ghi chú.");
              });
            }}
          >
            <fieldset disabled={!!busy || imported} className="note-fields">
              <label className="field">
                Tiêu đề ghi chú
                <input
                  required
                  maxLength={120}
                  value={note.title}
                  onChange={(e) => setNote({ ...note, title: e.target.value })}
                  placeholder="Việc cần làm tuần này"
                />
              </label>
              <label className="field">
                Nội dung ghi chú
                <textarea
                  required
                  maxLength={8000}
                  rows={8}
                  value={note.content}
                  onChange={(e) =>
                    setNote({ ...note, content: e.target.value })
                  }
                  placeholder={
                    "Viết báo cáo SWP trước thứ Sáu, khoảng 3 tiếng\nÔn chương 4 vào ngày mai, khoảng 90 phút"
                  }
                />
              </label>
              <label className="checkbox spaced">
                <input
                  type="checkbox"
                  checked={useAi}
                  onChange={(e) => setUseAi(e.target.checked)}
                />
                Cho phép gửi nội dung ghi chú đến AI để phân tích
              </label>
              <label className="field spaced">
                Số ngày tự xếp lịch
                <input
                  type="number"
                  min={1}
                  max={30}
                  value={horizonDays}
                  onChange={(e) => setHorizonDays(e.target.value)}
                />
              </label>
              <p className="reason spaced">
                Bấm tự xếp lịch sẽ tạo các công việc được nhận diện và áp dụng
                lịch ngay. Nếu thiếu thông tin, thời lượng mặc định 60 phút,
                deadline sau 7 ngày.
              </p>
              <div className="form-actions">
                <button type="submit" className="btn secondary">
                  Lưu ghi chú
                </button>
                <button
                  type="button"
                  className="btn secondary"
                  onClick={analyze}
                >
                  Agent phân tích
                </button>
                <button
                  type="button"
                  className="btn primary"
                  disabled={!hasTime}
                  onClick={autoSchedule}
                >
                  Agent tự xếp lịch
                </button>
              </div>
            </fieldset>
          </form>
          {imported && (
            <p className="info spaced">
              Ghi chú đã tạo {note.createdTaskIds.length} công việc. Sửa tiến độ
              và nội dung ở mục Công việc; tạo ghi chú mới để thêm việc.
            </p>
          )}
          {!hasTime && (
            <p className="warning spaced">
              Thêm <a href="#time">giờ rảnh</a> để agent có thể tự xếp lịch. Bạn
              vẫn có thể lưu và phân tích ghi chú.
            </p>
          )}
          {imported && (
            <div className="form-actions">
              <button
                type="button"
                className="btn primary"
                disabled={!!busy || !hasTime}
                onClick={autoSchedule}
              >
                {note.planId
                  ? "Xem kết quả tự xếp lịch"
                  : "Tiếp tục tự xếp lịch"}
              </button>
            </div>
          )}
          {dirty && note.analysis && (
            <p className="warning spaced">
              Bạn đã sửa nội dung. Phân tích lại để cập nhật các gợi ý.
            </p>
          )}
          <p className="reason spaced">
            Lịch sử dụng giờ rảnh và sự kiện đã thiết lập. Điều kiện về lịch bận
            hoặc phụ thuộc trong ghi chú cần được khai báo ở các mục tương ứng.
          </p>
          {busy && <Loading label={busy} />}
          {message && (
            <p role="status" className="info spaced">
              {message}
            </p>
          )}
          {error && (
            <p role="alert" className="form-error spaced">
              {error}
            </p>
          )}
          {note.id && (
            <button
              type="button"
              className="btn ghost small spaced"
              disabled={!!busy}
              onClick={() => {
                if (
                  !window.confirm(
                    "Xóa ghi chú này? Các công việc và lịch đã tạo vẫn được giữ.",
                  )
                )
                  return;
                run("Đang xóa ghi chú…", async () => {
                  await api(
                    `/api/notes/${note.id}?version=${encodeURIComponent(note.version)}`,
                    "DELETE",
                  );
                  setNotes((items) => items.filter((n) => n.id !== note.id));
                  select(blank());
                });
              }}
            >
              Xóa ghi chú
            </button>
          )}
        </section>
        {note.analysis && (
          <section className="card" aria-labelledby="note-analysis-title">
            <div className="card-header">
              <div>
                <h2 id="note-analysis-title">Công việc agent đề xuất</h2>
                <p>
                  {note.analysis.source === "OpenAI"
                    ? "Phân tích bằng AI"
                    : "Phân tích bằng quy tắc"}{" "}
                  · Chọn và chỉnh sửa các việc bạn muốn đưa vào lịch.
                </p>
              </div>
            </div>
            <ul className="explanation-list">
              {note.analysis.messages.map((text, index) => (
                <li key={index}>{text}</li>
              ))}
            </ul>
            {!drafts.length && (
              <p className="info">
                Chưa có gợi ý công việc. Kiểm tra nội dung và phân tích lại.
              </p>
            )}
            <form
              onSubmit={(event) => {
                event.preventDefault();
                propose(!imported);
              }}
            >
              <fieldset
                disabled={!!busy || imported || dirty}
                className="note-fields"
              >
                {drafts.map((task, index) => (
                  <article key={index} className="note-task-draft">
                    <label className="checkbox">
                      <input
                        type="checkbox"
                        checked={task.selected}
                        onChange={(e) =>
                          updateDraft(index, "selected", e.target.checked)
                        }
                      />
                      Việc {index + 1}
                    </label>
                    <div className="form-grid spaced">
                      <label className="field wide">
                        Tên công việc
                        <input
                          required={task.selected}
                          maxLength={200}
                          value={task.name}
                          onChange={(e) =>
                            updateDraft(index, "name", e.target.value)
                          }
                        />
                      </label>
                      <label className="field">
                        Deadline
                        <input
                          type="datetime-local"
                          disabled={!task.selected}
                          required={task.selected}
                          value={task.deadline ? localInput(task.deadline) : ""}
                          onChange={(e) =>
                            updateDraft(
                              index,
                              "deadline",
                              e.target.value ? toUtc(e.target.value) : "",
                            )
                          }
                        />
                      </label>
                      <label className="field">
                        Thời gian dự kiến (phút)
                        <input
                          type="number"
                          required={task.selected}
                          disabled={!task.selected}
                          min={1}
                          max={43200}
                          value={task.estimatedMinutes}
                          onChange={(e) =>
                            updateDraft(
                              index,
                              "estimatedMinutes",
                              Number(e.target.value),
                            )
                          }
                        />
                      </label>
                      <label className="field">
                        Mức ưu tiên
                        <select
                          value={task.priority}
                          onChange={(e) =>
                            updateDraft(
                              index,
                              "priority",
                              Number(e.target.value),
                            )
                          }
                        >
                          {[1, 2, 3, 4].map((p) => (
                            <option key={p} value={p}>
                              {priorityLabels[p]}
                            </option>
                          ))}
                        </select>
                      </label>
                      <label className="field">
                        Loại công việc
                        <select
                          value={
                            typeLabels[task.taskType] ? task.taskType : "OTHER"
                          }
                          onChange={(e) =>
                            updateDraft(index, "taskType", e.target.value)
                          }
                        >
                          {Object.entries(typeLabels).map(([key, label]) => (
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
                        onChange={(e) =>
                          updateDraft(index, "isHardDeadline", e.target.checked)
                        }
                      />
                      Deadline cứng: không xếp sau hạn chót
                    </label>
                  </article>
                ))}
              </fieldset>
              <label className="field spaced">
                Số ngày xếp lịch
                <input
                  type="number"
                  min={1}
                  max={30}
                  required
                  value={horizonDays}
                  disabled={!!busy}
                  onChange={(e) => setHorizonDays(e.target.value)}
                />
              </label>
              {!hasTime && (
                <p className="warning spaced">
                  Thiết lập <a href="#time">giờ rảnh</a> trước khi tạo lịch đề
                  xuất. Ghi chú và kết quả phân tích đã được lưu.
                </p>
              )}
              <p className="reason spaced">
                Giữ các phiên hiện có và đề xuất phiên cho những việc từ ghi
                chú. Bạn xem lịch trước khi áp dụng.
              </p>
              <div className="form-actions">
                <button
                  type="submit"
                  className="btn primary"
                  disabled={
                    !!busy ||
                    !hasTime ||
                    (!imported && (dirty || !drafts.some((d) => d.selected)))
                  }
                >
                  {imported
                    ? "Xem lịch đề xuất"
                    : "Tạo công việc & đề xuất lịch"}
                </button>
              </div>
            </form>
          </section>
        )}
      </div>
    </div>
  );
}
