import { useEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { state, dateFmt, localInput, toUtc } from "./ui.jsx";
import { planModal } from "./calendar.jsx";
import { Loading } from "./loading.jsx";
import { getGuide, completeGuideStep } from "./onboarding.js";

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
const aiMessages = {
  NOTE_AI_NOT_CONFIGURED:
    "Chức năng AI chưa sẵn sàng. Bạn có thể lưu bản nháp hoặc thêm việc ở mục Công việc.",
  NOTE_AI_AUTH_FAILED:
    "Chức năng AI đang gặp sự cố truy cập. Ghi chú được giữ nguyên; hãy thử lại sau hoặc báo người quản lý ứng dụng.",
  NOTE_AI_MODEL_UNAVAILABLE:
    "Chức năng AI tạm thời chưa sẵn sàng. Ghi chú được giữ nguyên; hãy thử lại sau.",
  NOTE_AI_REQUEST_INVALID:
    "Ứng dụng chưa gửi được yêu cầu phân tích. Ghi chú được giữ nguyên; hãy thử lại sau hoặc báo người quản lý ứng dụng.",
  NOTE_AI_QUOTA_EXCEEDED:
    "AI đang đạt giới hạn sử dụng. Ghi chú được giữ nguyên; hãy chờ rồi bấm Thử lại.",
  NOTE_AI_TIMEOUT:
    "AI phản hồi chậm. Ghi chú được giữ nguyên; bạn có thể thử lại hoặc chia thành các ghi chú ngắn hơn.",
  NOTE_AI_CONNECTION_FAILED:
    "Chưa kết nối được với AI. Ghi chú được giữ nguyên; hãy thử lại sau.",
  NOTE_AI_UNAVAILABLE:
    "AI chưa phân tích được ghi chú. Nội dung đã lưu được giữ nguyên; hãy thử lại sau.",
  NOTE_AI_RESPONSE_INCOMPLETE:
    "AI chưa trả đủ kết quả. Hãy thử lại hoặc chia nội dung thành các ghi chú ngắn hơn.",
  NOTE_AI_CONTENT_BLOCKED:
    "AI chưa xử lý được nội dung này. Hãy sửa cách diễn đạt các việc cần làm rồi thử lại.",
  NOTE_NEEDS_REVIEW:
    "Ghi chú có thông tin cần kiểm tra. Bấm Xem trước công việc để sửa kết quả trước khi tạo lịch.",
};

export function NotesPage({ onWorkspaceChanged }) {
  const [notes, setNotes] = useState([]);
  const [note, setNote] = useState(blank);
  const [drafts, setDrafts] = useState([]);
  const [agent, setAgent] = useState(null);
  const [agentResult, setAgentResult] = useState(null);
  const [pendingSchedule, setPendingSchedule] = useState(null);
  const [availabilityReady, setAvailabilityReady] = useState(false);
  const [busy, setBusy] = useState("Đang tải ghi chú…");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [failedAction, setFailedAction] = useState(null);
  const [libraryOpen, setLibraryOpen] = useState(
    () => !window.matchMedia("(max-width: 800px)").matches,
  );
  const [horizonDays, setHorizonDays] = useState(
    state.workspace?.preference?.planningHorizonDays || 14,
  );
  const locked = useRef(false);
  const alive = useRef(true);
  const imported = !!note.createdTaskIds?.length;
  const hasAiAnalysis = ["Gemini", "OpenAI"].includes(note.analysis?.source);
  const dirty =
    !!note.id &&
    notes.some(
      (n) =>
        n.id === note.id &&
        (n.title !== note.title || n.content !== note.content),
    );
  const hasTime =
    availabilityReady ||
    !!state.workspace?.availability?.length ||
    !!state.workspace?.exceptions?.some((e) => e.isAvailable);
  const proposalIssue = imported
    ? ""
    : dirty
      ? "Nội dung ghi chú đã thay đổi. Phân tích lại trước khi tạo công việc."
      : !hasAiAnalysis
        ? "Phân tích lại ghi chú bằng AI trước khi tạo công việc."
        : !drafts.some((task) => task.selected)
          ? "Chọn ít nhất một công việc để tạo lịch."
          : "";

  useEffect(() => {
    alive.current = true;
    Promise.all([api("/api/notes"), api("/api/notes/agent").catch(() => null)])
      .then(([items, capabilities]) => {
        if (alive.current) {
          setNotes(items);
          setAgent(capabilities);
          const guide = getGuide();
          const activeNote =
            guide.status === "active" &&
            items.find((item) => item.id === guide.noteId);
          if (activeNote) select(activeNote);
        }
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
    setAgentResult(null);
    setPendingSchedule(null);
    setFailedAction(null);
  };
  const remember = (saved) => {
    setNotes((items) => [saved, ...items.filter((n) => n.id !== saved.id)]);
    setNote(saved);
    return saved;
  };
  const run = async (label, operation, intent = null) => {
    if (locked.current) return;
    locked.current = true;
    setBusy(label);
    setError("");
    setMessage("");
    setFailedAction(null);
    try {
      await operation();
    } catch (e) {
      if (alive.current) {
        setError(aiMessages[e.code] || e.message);
        setFailedAction(intent);
      }
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
    run(
      "AI đang đọc và phân tích ghi chú…",
      async () => {
        const saved = await save();
        const result = await api(`/api/notes/${saved.id}/analyze`, "POST", {
          version: saved.version,
          useAi: true,
        });
        if (alive.current) {
          remember(result);
          if (
            result.analysis.tasks.length &&
            ["Gemini", "OpenAI"].includes(result.analysis.source)
          )
            completeGuideStep(0, result.id);
          setDrafts(
            result.analysis.tasks.map((task) => ({ ...task, selected: true })),
          );
          setMessage(
            result.analysis.tasks.length
              ? "Đây là kết quả xem trước. Chưa tạo công việc hoặc lưu lịch; bạn có thể chỉnh các việc bên dưới."
              : "Chưa tìm thấy công việc. Bổ sung việc cần làm rồi phân tích lại.",
          );
        }
      },
      "analysis",
    );
  const autoSchedule = () =>
    run(
      "AI đang phân tích, tạo công việc và lưu lịch…",
      async () => {
        const saved = imported ? note : await save();
        try {
          const result = await api(`/api/notes/${saved.id}/schedule`, "POST", {
            version: saved.version,
            useAi: true,
            horizonDays: Number(horizonDays),
          });
          if (!alive.current) return;
          remember(result.note);
          setAgentResult(result);
          if (result.note.analysis?.tasks?.length)
            completeGuideStep(0, result.note.id);
          if (
            result.plan.isApplied &&
            result.plan.sessions.some((session) => !session.preserved)
          )
            completeGuideStep(2);
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
      },
      "auto",
    );
  const propose = (createTasks) =>
    run(
      "Đang chuẩn bị công việc và lịch đề xuất…",
      async () => {
        let saved = note;
        if (createTasks) {
          if (!hasAiAnalysis)
            throw new Error(
              "Phân tích lại ghi chú bằng AI trước khi tạo công việc.",
            );
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
            "Đã tạo lịch đề xuất. Xem cảnh báo và chọn Lưu lịch này để lưu lịch.",
          );
          planModal(proposal);
        }
      },
      "proposal",
    );
  const requestSchedule = (kind) => {
    if (!hasTime && !note.planId) {
      setPendingSchedule(kind);
      return;
    }
    return kind === "auto" ? autoSchedule() : propose(!imported);
  };
  const saveAvailability = async (newSlots) => {
    const next = pendingSchedule;
    let saved = false;
    await run("Đang lưu giờ rảnh…", async () => {
      // Read the latest windows so setup in another tab is preserved.
      const latest = await api("/api/workspace");
      const slots = [
        ...(latest.availability || []).map(
          ({ dayOfWeek, startMinute, endMinute }) => ({
            dayOfWeek,
            startMinute,
            endMinute,
          }),
        ),
        ...newSlots,
      ];
      await api("/api/time/availability", "PUT", { slots });
      if (slots.length) completeGuideStep(1);
      state.workspace = { ...latest, availability: slots };
      setAvailabilityReady(true);
      setPendingSchedule(null);
      saved = true;
    });
    if (saved && alive.current) {
      if (next === "auto") await autoSchedule();
      else await propose(!imported);
    }
  };
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
          <button
            type="button"
            className="note-library-toggle"
            aria-expanded={libraryOpen}
            aria-controls="saved-notes"
            onClick={() => setLibraryOpen(!libraryOpen)}
          >
            <span id="note-library-title">Ghi chú đã lưu ({notes.length})</span>
          </button>
          <button
            type="button"
            className="btn secondary small"
            disabled={!!busy}
            onClick={() => select(blank())}
          >
            Ghi chú mới
          </button>
        </div>
        <div id="saved-notes" className="notes-list" hidden={!libraryOpen}>
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
          <div className="note-agent-banner">
            <span className="note-agent-symbol" aria-hidden="true">
              ✦
            </span>
            <div>
              <strong>Nhập việc, để AI giúp xếp lịch</strong>
              <p>Viết ghi chú → Chọn giờ rảnh → Tạo lịch → Bắt đầu làm</p>
            </div>
            <span className="note-agent-badge">
              {agent?.aiAvailable
                ? `${agent.provider || "AI"} sẵn sàng`
                : "AI chưa sẵn sàng"}
            </span>
          </div>
          <div className="card-header">
            <div>
              <h2 id="note-editor-title">Bạn cần làm những việc gì?</h2>
              <p>
                Ghi lại các việc bạn cần làm, thời lượng và hạn chót nếu đã
                biết.
              </p>
            </div>
          </div>
          <form
            hidden={imported}
            onSubmit={(event) => {
              event.preventDefault();
              run("Đang lưu ghi chú…", async () => {
                await save();
                setMessage(
                  "Đã lưu bản nháp. Chưa tạo công việc hoặc thay đổi lịch.",
                );
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
                  aria-label="Nội dung ghi chú"
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
              {!note.content.trim() && !note.id && (
                <div className="note-example-action spaced">
                  <button
                    type="button"
                    className="btn secondary small"
                    onClick={() =>
                      setNote({
                        ...note,
                        title: note.title || "Kế hoạch tuần này",
                        content:
                          "Viết báo cáo ngày mai, khoảng 2 tiếng\nÔn chương 4 trước thứ Sáu, khoảng 90 phút",
                      })
                    }
                  >
                    Dùng ví dụ
                  </button>
                  <small>Chỉ điền nội dung, chưa lưu hoặc tạo lịch.</small>
                </div>
              )}
              <p className="info spaced">
                Nội dung được gửi đến AI khi bạn chọn xem trước hoặc tạo lịch.
                Lưu bản nháp chưa gửi nội dung đến AI.
              </p>
              {!agent?.aiAvailable && (
                <p className="warning spaced">
                  AI chưa sẵn sàng. Bạn vẫn có thể lưu bản nháp hoặc
                  <a href="#tasks"> tự thêm công việc</a> để lập kế hoạch.
                </p>
              )}
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
              <div className="form-actions note-editor-actions">
                <button
                  type="button"
                  className="btn primary"
                  disabled={!agent?.aiAvailable}
                  onClick={() => requestSchedule("auto")}
                >
                  Tạo và lưu lịch ngay
                </button>
                <button
                  type="button"
                  className="btn secondary"
                  disabled={!agent?.aiAvailable}
                  onClick={analyze}
                >
                  Xem trước công việc
                </button>
                <button type="submit" className="btn secondary">
                  Lưu bản nháp
                </button>
              </div>
              <p className="note-action-help spaced">
                <strong>Tạo và lưu lịch ngay:</strong> tạo công việc và lưu lịch
                tự động. <strong>Xem trước công việc:</strong> kiểm tra và sửa
                kết quả trước khi tạo lịch.
              </p>
            </fieldset>
          </form>
          {imported && (
            <article className="note-source spaced">
              <h3>{note.title}</h3>
              <p>{note.content}</p>
            </article>
          )}
          {imported && (
            <p className="info spaced">
              Ghi chú đã tạo {note.createdTaskIds.length} công việc. Sửa tiến độ
              và nội dung ở mục Công việc; tạo ghi chú mới để thêm việc.
            </p>
          )}
          {imported && (
            <div className="form-actions">
              <a className="btn secondary small" href="#tasks">
                Sửa công việc đã tạo
              </a>
              <button
                className="btn ghost small"
                type="button"
                disabled={!!busy}
                onClick={() => select(blank())}
              >
                Nhập việc mới
              </button>
            </div>
          )}
          {!hasTime && (
            <p className="warning spaced">
              Thêm <a href="#time">giờ rảnh</a> để AI có thể tự xếp lịch. Bạn
              vẫn có thể lưu và phân tích ghi chú.
            </p>
          )}
          {imported && (
            <div className="form-actions">
              <button
                type="button"
                className="btn primary"
                disabled={!!busy}
                onClick={() => requestSchedule("auto")}
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
          {agentResult && <AgentResult result={agentResult} />}
          {error && (
            <div role="alert" className="form-error spaced">
              {error}
              {failedAction && (
                <div className="form-actions">
                  <button
                    type="button"
                    className="btn secondary small"
                    disabled={!!busy}
                    onClick={() =>
                      failedAction === "analysis"
                        ? analyze()
                        : failedAction === "auto"
                          ? requestSchedule("auto")
                          : requestSchedule("proposal")
                    }
                  >
                    Thử lại
                  </button>
                </div>
              )}
            </div>
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
        {pendingSchedule && (
          <NoteAvailabilitySetup
            busy={!!busy}
            timeZone={
              state.workspace?.preference?.timeZoneId || "Asia/Ho_Chi_Minh"
            }
            onSave={saveAvailability}
            onCancel={() => setPendingSchedule(null)}
          />
        )}
        {note.analysis && (
          <section className="card" aria-labelledby="note-analysis-title">
            <div className="card-header">
              <div>
                <h2 id="note-analysis-title">
                  {imported ? "Công việc đã được lưu" : "Xem trước công việc"}
                </h2>
                <p>
                  {["OpenAI", "Gemini"].includes(note.analysis.source)
                    ? `Phân tích bằng ${note.analysis.source}`
                    : "Kết quả phân tích cũ"}{" "}
                  ·{" "}
                  {imported
                    ? "Các công việc đã lưu từ ghi chú này."
                    : "Chọn và chỉnh sửa các việc bạn muốn đưa vào lịch."}
                </p>
              </div>
            </div>
            {!imported && (
              <p className="info spaced">
                Các việc dưới đây chưa được lưu. Chọn việc cần làm, sửa tên,
                thời lượng hoặc hạn chót, rồi bấm Tạo công việc & xem lịch. Sau
                đó chọn Lưu lịch này trong bản xem trước.
              </p>
            )}
            {!imported && !hasAiAnalysis && (
              <p className="warning spaced">
                Kết quả này chưa được phân tích bằng AI. Bấm Xem trước công việc
                để cập nhật trước khi tạo công việc.
              </p>
            )}
            <ul className="explanation-list">
              {note.analysis.messages.map((text, index) => (
                <li key={index}>{text}</li>
              ))}
            </ul>
            {note.analysis.needsReview && (
              <p className="warning spaced">
                AI chưa thể tự áp dụng các điều kiện trong ghi chú. Kiểm tra các
                dòng bên trên, thiết lập giờ rảnh hoặc phụ thuộc rồi sửa note để
                tiếp tục. Bạn vẫn có thể chỉnh công việc và xem lịch đề xuất.
              </p>
            )}
            {!drafts.length && (
              <p className="info">
                Chưa có gợi ý công việc. Kiểm tra nội dung và phân tích lại.
              </p>
            )}
            {imported && (
              <div className="note-created-tasks spaced">
                {drafts.map((task, index) => (
                  <article key={index}>
                    <strong>{task.name}</strong>
                    <p>
                      Hạn chót:{" "}
                      {dateFmt(task.deadline, {
                        hour: "2-digit",
                        minute: "2-digit",
                      })}
                    </p>
                    <div>
                      <span>{task.estimatedMinutes} phút</span>
                      <span>
                        Ưu tiên {priorityLabels[task.priority]?.toLowerCase()}
                      </span>
                      <span>{typeLabels[task.taskType] || "Khác"}</span>
                      {task.isHardDeadline && <span>Deadline cứng</span>}
                    </div>
                  </article>
                ))}
              </div>
            )}
            <form
              onSubmit={(event) => {
                event.preventDefault();
                requestSchedule("proposal");
              }}
            >
              <fieldset
                disabled={!!busy || imported || dirty || !hasAiAnalysis}
                hidden={imported}
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
              {!note.planId && (
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
              )}
              {!hasTime && (
                <p className="warning spaced">
                  Chưa có giờ rảnh. Bấm nút bên dưới để chọn ngay tại đây; các
                  công việc bạn đã chỉnh sửa được giữ nguyên. Bạn cũng có thể
                  thiết lập ở mục <a href="#time">giờ rảnh</a>.
                </p>
              )}
              {!note.planId && (
                <p className="reason spaced">
                  Giữ các phiên hiện có và đề xuất phiên cho những việc từ ghi
                  chú. Bạn xem lịch trước khi áp dụng.
                </p>
              )}
              {!note.planId && (
                <p
                  id="note-proposal-help"
                  className={proposalIssue ? "warning spaced" : "reason spaced"}
                >
                  {proposalIssue ||
                    (!hasTime
                      ? "Bạn sẽ chọn giờ rảnh trước khi tạo công việc và lịch đề xuất."
                      : "Sẵn sàng tạo công việc và xem lịch đề xuất.")}
                </p>
              )}
              {!note.planId && (
                <div className="form-actions">
                  <button
                    type="submit"
                    className="btn primary"
                    disabled={!!busy || !!proposalIssue}
                    aria-describedby="note-proposal-help"
                  >
                    {imported ? "Xem lịch đề xuất" : "Tạo công việc & xem lịch"}
                  </button>
                </div>
              )}
            </form>
          </section>
        )}
      </div>
    </div>
  );
}

function NoteAvailabilitySetup({ busy, timeZone, onSave, onCancel }) {
  const [days, setDays] = useState([]);
  const [start, setStart] = useState("");
  const [end, setEnd] = useState("");
  const [error, setError] = useState("");
  const panel = useRef(null);
  useEffect(() => {
    panel.current?.focus();
    panel.current?.scrollIntoView?.({ block: "center", behavior: "smooth" });
  }, []);
  return (
    <section
      ref={panel}
      tabIndex={-1}
      className="card note-time-setup"
      aria-labelledby="note-time-title"
    >
      <h2 id="note-time-title">Chọn giờ rảnh để tạo lịch</h2>
      <p>Chọn thời gian bạn có thể làm việc mỗi tuần. Múi giờ: {timeZone}.</p>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          setError("");
          if (!days.length) {
            setError("Chọn ít nhất một ngày trong tuần.");
            return;
          }
          const minute = (value) => {
            const [hour, min] = value.split(":").map(Number);
            return hour * 60 + min;
          };
          const from = minute(start),
            to = minute(end);
          if (!Number.isFinite(from) || !Number.isFinite(to) || to <= from) {
            setError("Giờ kết thúc phải sau giờ bắt đầu trong cùng ngày.");
            return;
          }
          onSave(
            days.map((dayOfWeek) => ({
              dayOfWeek,
              startMinute: from,
              endMinute: to,
            })),
          );
        }}
      >
        <fieldset className="note-fields" disabled={busy}>
          <fieldset className="note-days">
            <legend>Ngày làm việc</legend>
            {[1, 2, 3, 4, 5, 6, 0].map((day) => (
              <label key={day} className="checkbox">
                <input
                  type="checkbox"
                  checked={days.includes(day)}
                  onChange={(event) =>
                    setDays((current) =>
                      event.target.checked
                        ? [...current, day]
                        : current.filter((value) => value !== day),
                    )
                  }
                />
                {day === 0
                  ? "Chủ Nhật"
                  : `Thứ ${["", "Hai", "Ba", "Tư", "Năm", "Sáu", "Bảy"][day]}`}
              </label>
            ))}
          </fieldset>
          <div className="form-grid spaced">
            <label className="field">
              Giờ bắt đầu
              <input
                type="time"
                required
                value={start}
                onChange={(event) => setStart(event.target.value)}
              />
            </label>
            <label className="field">
              Giờ kết thúc
              <input
                type="time"
                required
                value={end}
                onChange={(event) => setEnd(event.target.value)}
              />
            </label>
          </div>
          {error && (
            <p role="alert" className="form-error spaced">
              {error}
            </p>
          )}
          <div className="form-actions">
            <button type="button" className="btn ghost" onClick={onCancel}>
              Hủy
            </button>
            <button type="submit" className="btn primary">
              Lưu giờ rảnh & tiếp tục
            </button>
          </div>
        </fieldset>
      </form>
    </section>
  );
}

function AgentResult({ result }) {
  const sessions = result.plan.sessions.filter((session) => !session.preserved);
  const warnings = result.plan.warnings;
  return (
    <section className="note-agent-result spaced" aria-label="Kết quả xếp lịch">
      <div className="card-header">
        <h3>
          {result.plan.isApplied ? "Lịch đã được lưu" : "Kết quả lập lịch"}
        </h3>
        <a className="btn secondary small" href="#calendar">
          Mở lịch để bắt đầu →
        </a>
      </div>
      <div className="note-agent-stats">
        <div>
          <strong>{result.note.createdTaskIds.length}</strong>
          <span>Công việc đã tạo</span>
        </div>
        <div>
          <strong>{sessions.length}</strong>
          <span>Phiên mới</span>
        </div>
        <div>
          <strong>{warnings.length}</strong>
          <span>Cảnh báo</span>
        </div>
      </div>
      {!!result.steps?.length && (
        <details className="note-process-details">
          <summary>Xem các bước đã thực hiện</summary>
          <ol className="note-agent-steps">
            {result.steps.map((step) => (
              <li key={step.code}>{step.message}</li>
            ))}
          </ol>
        </details>
      )}
      {!!warnings.length && (
        <>
          <p className="warning spaced">
            {warnings.some((item) => item.unscheduledMinutes > 0)
              ? "Một số công việc chưa được xếp đủ thời gian. Xem các mục dưới đây để điều chỉnh."
              : "Lịch có cảnh báo cần kiểm tra trước khi làm việc."}
          </p>
          <ul className="note-agent-warnings">
            {warnings.map((warning, index) => (
              <li key={`${warning.taskId}-${warning.code}-${index}`}>
                <strong>{warning.taskName}</strong>: {warning.message}
                {warning.unscheduledMinutes > 0 &&
                  ` · Còn ${warning.unscheduledMinutes} phút chưa xếp được`}
              </li>
            ))}
          </ul>
          <div className="form-actions">
            <a href="#time" className="btn secondary small">
              Bổ sung giờ rảnh
            </a>
            <a href="#tasks" className="btn ghost small">
              Sửa công việc
            </a>
          </div>
        </>
      )}
      {!sessions.length && (
        <p className="reason spaced">
          Chưa có phiên mới. Xem cảnh báo hoặc bổ sung giờ rảnh để xếp đủ công
          việc.
        </p>
      )}
    </section>
  );
}
