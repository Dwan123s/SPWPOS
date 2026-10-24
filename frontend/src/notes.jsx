import { useEffect, useRef, useState } from "react";
import "./styles/notes-entry.css";
import { api } from "./api.js";
import { state, toast } from "./ui.jsx";
import { planModal } from "./calendar.jsx";
import { Loading } from "./loading.jsx";
import { getGuide, completeGuideStep } from "./onboarding.js";
import { NoteStudio } from "./note-studio.jsx";
import { ConversationDeleteDialog } from "./conversation-delete-dialog.jsx";
import {
  clearNoteAppearance,
  normalizeNoteAppearance,
  readNoteAppearance,
  saveNoteAppearance,
} from "./note-appearance.js";

const blank = () => ({ title: "", content: "" });
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
  const appearanceAccount =
    state.user?.id || state.workspace?.profile?.id || "guest";
  const [appearance, setAppearance] = useState(() =>
    readNoteAppearance(appearanceAccount),
  );
  const [notes, setNotes] = useState([]);
  const [note, setNote] = useState(blank);
  const [drafts, setDrafts] = useState([]);
  const [stage, setStage] = useState("write");
  const [agent, setAgent] = useState(null);
  const [agentResult, setAgentResult] = useState(null);
  const [pendingSchedule, setPendingSchedule] = useState(null);
  const [availabilityReady, setAvailabilityReady] = useState(false);
  const [busy, setBusy] = useState("Đang tải ghi chú…");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [failedAction, setFailedAction] = useState(null);
  const [selectionTarget, setSelectionTarget] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleteError, setDeleteError] = useState("");
  const selectionPrompt = useRef(null);
  const [libraryOpen, setLibraryOpen] = useState(
    () => !window.matchMedia("(max-width: 800px)").matches,
  );
  const [horizonDays, setHorizonDays] = useState(
    state.workspace?.preference?.planningHorizonDays || 14,
  );
  const locked = useRef(false);
  const activityReviewDirty = useRef(false);
  const alive = useRef(true);
  const imported = !!note.createdTaskIds?.length;
  const hasAiAnalysis = ["Gemini", "OpenAI", "VertexAI"].includes(
    note.analysis?.source,
  );
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
          : drafts.some(
                (task) =>
                  task.selected &&
                  (!task.name.trim() ||
                    task.name.length > 200 ||
                    !Number.isInteger(task.estimatedMinutes) ||
                    task.estimatedMinutes < 1 ||
                    task.estimatedMinutes > 43200 ||
                    !Number.isFinite(new Date(task.deadline).getTime())),
              )
            ? "Điền tên việc, thời lượng và hạn chót hợp lệ trước khi tạo lịch."
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
    activityReviewDirty.current = false;
    setSelectionTarget(null);
    setAppearance(readNoteAppearance(appearanceAccount, item.id));
    setNote(item);
    setStage(item.analysis ? "review" : "write");
    setDrafts(
      (item.analysis?.tasks || []).map((task) => ({ ...task, selected: true })),
    );
    setError("");
    setMessage("");
    setAgentResult(null);
    setPendingSchedule(null);
    setFailedAction(null);
  };
  const requestSelect = (item) => {
    if (busy || (item.id && item.id === note.id)) return;
    const contentChanged =
      dirty || (!note.id && (note.title.trim() || note.content.trim()));
    const reviewChanged =
      !imported &&
      note.analysis &&
      JSON.stringify(drafts) !==
        JSON.stringify(
          note.analysis.tasks.map((task) => ({ ...task, selected: true })),
        );
    if (contentChanged || reviewChanged || activityReviewDirty.current)
      setSelectionTarget(item);
    else select(item);
  };
  useEffect(() => {
    if (selectionTarget) {
      selectionPrompt.current?.focus();
      selectionPrompt.current?.scrollIntoView?.({
        block: "nearest",
        behavior: "smooth",
      });
    }
  }, [selectionTarget]);
  const remember = (saved) => {
    saveNoteAppearance(appearanceAccount, saved.id, appearance);
    if (!note.id) clearNoteAppearance(appearanceAccount);
    setNotes((items) => [saved, ...items.filter((n) => n.id !== saved.id)]);
    setNote(saved);
    return saved;
  };
  const run = async (label, operation, intent = null) => {
    if (locked.current) return;
    locked.current = true;
    setSelectionTarget(null);
    setBusy(label);
    setError("");
    setMessage("");
    setFailedAction(null);
    try {
      return await operation();
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
    if (!note.content.trim()) throw new Error("Nhập nội dung ghi chú.");
    const title =
      note.title.trim() ||
      note.content
        .split("\n")
        .find((line) => line.trim())
        ?.trim()
        .replace(/^[•☐\s]+/, "")
        .slice(0, 120) ||
      "Ghi chú mới";
    if (note.id && !dirty) return note;
    const saved = await api(
      `/api/notes${note.id ? `/${note.id}` : ""}`,
      note.id ? "PUT" : "POST",
      {
        title,
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
            (result.analysis.tasks.length ||
              result.analysis.activities?.length) &&
            ["Gemini", "OpenAI", "VertexAI"].includes(result.analysis.source)
          )
            completeGuideStep(0, result.id);
          setDrafts(
            result.analysis.tasks.map((task) => ({ ...task, selected: true })),
          );
          activityReviewDirty.current = false;
          setStage(
            result.analysis.tasks.length || result.analysis.activities?.length
              ? "review"
              : "write",
          );
          requestAnimationFrame(() =>
            document.getElementById("note-analysis-title")?.focus(),
          );
          setMessage(
            result.analysis.tasks.length || result.analysis.activities?.length
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
        if (
          !Number.isInteger(Number(horizonDays)) ||
          Number(horizonDays) < 1 ||
          Number(horizonDays) > 30
        )
          throw new Error("Chọn phạm vi xếp lịch từ 1 đến 30 ngày.");
        const saved = await save();
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
            result.plan.hasUrgentReplan && !result.plan.isApplied
              ? "Đã tạo đề xuất dời lịch để ưu tiên việc khẩn. Xem giờ cũ và giờ mới, rồi xác nhận trước khi lưu."
              : result.plan.hasDeadlineProtection && !result.plan.isApplied
                ? "Đã tạo đề xuất dành thời gian cho việc khẩn hoặc gần hạn. Kiểm tra các phiên rồi xác nhận kế hoạch trước khi lưu."
              : `Đã tự động áp dụng lịch với ${result.plan.sessions.filter((s) => !s.preserved).length} phiên mới. ${result.plan.warnings.length ? `${result.plan.warnings.length} công việc có cảnh báo; xem chi tiết kết quả.` : "Bạn có thể bắt đầu làm việc trong mục Hôm nay."}`,
          );
          planModal(result.plan);
        } catch (failure) {
          const latest = await api("/api/notes").catch(() => null);
          if (latest && alive.current) {
            setNotes(latest);
            const recovered = latest.find((n) => n.id === saved.id);
            if (recovered) {
              setNote(recovered);
              if (recovered.analysis?.activities) setStage("review");
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
        if (
          !Number.isInteger(Number(horizonDays)) ||
          Number(horizonDays) < 1 ||
          Number(horizonDays) > 30
        )
          throw new Error("Chọn phạm vi xếp lịch từ 1 đến 30 ngày.");
        if (createTasks && proposalIssue) throw new Error(proposalIssue);
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
      else if (next === "proposal") await propose(!imported);
    }
  };
  const updateDraft = (index, key, value) =>
    setDrafts((items) =>
      items.map((d, i) => (i === index ? { ...d, [key]: value } : d)),
    );
  const prepareActivities = (input) =>
    run("Đang kiểm tra sự kiện và công việc…", () =>
      api(`/api/notes/${note.id}/proposals`, "POST", {
        ...input,
        version: note.version,
      }),
    );
  const applyActivities = (planId) =>
    run("Đang lưu sự kiện và công việc…", async () => {
      const result = await api(
        `/api/notes/${note.id}/proposals/${planId}/apply`,
        "POST",
        { version: note.version },
      );
      if (!alive.current) return;
      remember(result.note);
      activityReviewDirty.current = false;
      await onWorkspaceChanged();
      return result;
    });

  return (
    <>
      {deleteTarget && (
        <ConversationDeleteDialog
          title={deleteTarget.title}
          headingText="Xóa ghi chú?"
          containerSelector=".note-studio"
          busy={!!busy}
          error={deleteError}
          descriptionContent={
            <>
              Ghi chú <strong>{deleteTarget.title}</strong> sẽ bị xóa vĩnh viễn.{" "}
              {deleteTarget.id === note.id &&
                dirty &&
                "Các thay đổi chưa lưu trong ghi chú này cũng sẽ bị xóa. "}
              Các công việc và lịch đã tạo vẫn được giữ.
            </>
          }
          onCancel={() => {
            if (!busy) {
              setDeleteTarget(null);
              setDeleteError("");
            }
          }}
          onConfirm={() => {
            if (busy) return;
            const saved = deleteTarget;
            setDeleteError("");
            run("Đang xóa ghi chú…", async () => {
              try {
                const versionParam = saved.version
                  ? `?version=${encodeURIComponent(saved.version)}`
                  : "";
                await api(`/api/notes/${saved.id}${versionParam}`, "DELETE");
                if (!alive.current) return;
                clearNoteAppearance(appearanceAccount, saved.id);
                setNotes((items) =>
                  items.filter((item) => item.id !== saved.id),
                );
                if (note.id === saved.id) select(blank());
                setDeleteTarget(null);
                toast(`Đã xóa ghi chú “${saved.title}”.`);
              } catch (e) {
                if (alive.current) setDeleteError(e.message);
              }
            });
          }}
        />
      )}
      <NoteStudio
        notes={notes}
        note={note}
        appearance={appearance}
        setAppearance={(value) => {
          const next = normalizeNoteAppearance(value);
          setAppearance(next);
          saveNoteAppearance(appearanceAccount, note.id, next);
        }}
        drafts={drafts}
        prepareActivities={prepareActivities}
        applyActivities={applyActivities}
        onActivityReviewDirty={(value) => {
          activityReviewDirty.current = value;
        }}
        agent={agent}
        imported={imported}
        busy={busy}
        dirty={dirty}
        hasAiAnalysis={hasAiAnalysis}
        hasTime={hasTime}
        horizonDays={horizonDays}
        setHorizonDays={setHorizonDays}
        libraryOpen={libraryOpen}
        setLibraryOpen={setLibraryOpen}
        select={requestSelect}
        setNote={setNote}
        analyze={analyze}
        requestSchedule={requestSchedule}
        updateDraft={updateDraft}
        proposalIssue={proposalIssue}
        workspace={state.workspace}
        stage={stage}
        setStage={setStage}
        navigationPrompt={
          selectionTarget && (
            <div
              className="note-switch-prompt"
              ref={selectionPrompt}
              tabIndex={-1}
              role="alert"
              aria-labelledby="note-switch-title"
            >
              <div>
                <strong id="note-switch-title">Có thay đổi chưa lưu</strong>
                <p>
                  Nội dung ghi chú hoặc công việc gợi ý đang được chỉnh sửa.
                  Chuyển ghi chú sẽ bỏ các thay đổi này.
                </p>
              </div>
              <div className="actions">
                <button
                  type="button"
                  className="btn secondary small"
                  onClick={() => {
                    setSelectionTarget(null);
                    requestAnimationFrame(() =>
                      document
                        .querySelector(
                          stage === "review"
                            ? "#note-analysis-title"
                            : ".note-title-field input",
                        )
                        ?.focus(),
                    );
                  }}
                >
                  Tiếp tục chỉnh sửa
                </button>
                <button
                  type="button"
                  className="btn danger small"
                  onClick={() => select(selectionTarget)}
                >
                  Bỏ thay đổi và chuyển
                </button>
              </div>
            </div>
          )
        }
        saveDraft={() =>
          run("Đang lưu ghi chú…", async () => {
            await save();
            setMessage(
              "Đã lưu bản nháp. Chưa tạo công việc hoặc thay đổi lịch.",
            );
          })
        }
        onSetTime={() => setPendingSchedule("impact")}
        availability={
          pendingSchedule && (
            <NoteAvailabilitySetup
              busy={!!busy}
              timeZone={
                state.workspace?.preference?.timeZoneId || "Asia/Ho_Chi_Minh"
              }
              onSave={saveAvailability}
              onCancel={() => setPendingSchedule(null)}
            />
          )
        }
        feedback={
          <>
            {busy && <Loading label={busy} />}
            {message && (
              <p role="status" className="note-feedback info">
                {message}
              </p>
            )}
            {agentResult && <AgentResult result={agentResult} />}
            {error && (
              <div role="alert" className="form-error note-feedback">
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
                          : requestSchedule(
                              failedAction === "auto" ? "auto" : "proposal",
                            )
                      }
                    >
                      Thử lại
                    </button>
                  </div>
                )}
              </div>
            )}
          </>
        }
        onDelete={(target = note) => {
          const currentTarget = target?.id ? target : note;
          if (busy || !currentTarget?.id) return;
          const saved =
            notes.find((item) => item.id === currentTarget.id) || currentTarget;
          setDeleteError("");
          setDeleteTarget(saved);
        }}
      />
    </>
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
