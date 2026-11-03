import { useEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { state, dateFmt, timeFmt, icon } from "./ui.jsx";
import "./work-time-review.css";

export function FinishWorkButton({
  sessionId,
  onWorkspaceChanged = async () => {},
}) {
  const [confirm, setConfirm] = useState(false),
    [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  return (
    <span className="work-time-finish">
      {!confirm ? (
        <button
          type="button"
          className="btn primary small"
          onClick={() => setConfirm(true)}
        >
          Đã xong công việc
        </button>
      ) : (
        <>
          <small>
            Hoàn thành toàn bộ task và giải phóng các phiên còn lại?
          </small>
          <button
            type="button"
            className="btn primary small"
            disabled={busy}
            onClick={async () => {
              setBusy(true);
              setError("");
              try {
                await api(`/api/sessions/${sessionId}/finish-task`, "POST");
                await onWorkspaceChanged();
                setConfirm(false);
              } catch (failure) {
                setError(failure.message);
              } finally {
                setBusy(false);
              }
            }}
          >
            Xác nhận hoàn thành
          </button>
          <button
            type="button"
            className="btn secondary small"
            disabled={busy}
            onClick={() => setConfirm(false)}
          >
            Tiếp tục làm
          </button>
        </>
      )}
      {error && (
        <span role="alert" className="team-error">
          {error}
        </span>
      )}
    </span>
  );
}
export function WorkTimeReviewCard({ onWorkspaceChanged = async () => {} }) {
  const enabled = state.workspace?.features?.includes("ambient-tracking");
  const [rows, setRows] = useState([]),
    [selected, setSelected] = useState(null),
    [minutes, setMinutes] = useState(30);
  const [preview, setPreview] = useState(null),
    [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [message, setMessage] = useState("");
  const alive = useRef(false),
    fetching = useRef(false);
  async function reload() {
    if (!enabled || fetching.current) return;
    fetching.current = true;
    try {
      const value = await api("/api/sessions/time-review");
      if (alive.current && Array.isArray(value)) setRows(value);
    } catch {
      /* Notification remains available; retry on the next poll. */
    } finally {
      fetching.current = false;
    }
  }
  useEffect(() => {
    alive.current = true;
    if (!enabled)
      return () => {
        alive.current = false;
      };
    reload();
    const refresh = () => {
      if (!document.hidden) reload();
    };
    const timer = setInterval(refresh, 10000);
    window.addEventListener("focus", refresh);
    return () => {
      alive.current = false;
      clearInterval(timer);
      window.removeEventListener("focus", refresh);
    };
  }, [enabled]);
  const current = rows.find((row) => row.sessionId === selected) || rows[0];
  useEffect(() => {
    setPreview(null);
    setError("");
  }, [current?.sessionId]);
  if (!enabled || (!current && !message)) return null;
  async function changed() {
    await onWorkspaceChanged();
    await reload();
  }
  return (
    <section
      className="card work-time-review"
      aria-label="NORA hỏi kết quả công việc"
    >
      {current && (
        <>
          <header>
            <span>{icon("clock")}</span>
            <div>
              <small>NORA · ĐÃ HẾT GIỜ</small>
              <h2>Bạn đã làm xong chưa?</h2>
            </div>
          </header>
          <p>
            <strong>{current.taskName}</strong> · {dateFmt(current.start)} ·{" "}
            {timeFmt(current.start)}–{timeFmt(current.end)}. NORA chưa tự đánh
            dấu hoàn thành.
          </p>
          {rows.length > 1 && (
            <label className="field">
              Phiên cần phản hồi
              <select
                aria-label="Phiên cần phản hồi"
                value={current.sessionId}
                disabled={busy}
                onChange={(event) => setSelected(event.target.value)}
              >
                {rows.map((row) => (
                  <option key={row.sessionId} value={row.sessionId}>
                    {row.taskName} · {dateFmt(row.end)} {timeFmt(row.end)}
                  </option>
                ))}
              </select>
            </label>
          )}
          <div className="work-time-actions">
            <FinishWorkButton
              key={current.sessionId}
              sessionId={current.sessionId}
              onWorkspaceChanged={changed}
            />
            <a className="btn secondary small" href="#calendar">
              Xem lịch
            </a>
          </div>
          {current.teamTask && (
            <small>
              Task nhóm sẽ được gửi trưởng nhóm duyệt; các phiên cá nhân còn lại
              được giải phóng ngay.
            </small>
          )}
          <div className="work-time-extension">
            <h3>Cần thêm thời gian</h3>
            <p>
              Chọn thời gian bổ sung. NORA giữ nguyên deadline và các lịch khác.
            </p>
            <div
              className="work-time-choices"
              role="group"
              aria-label="Thời gian bổ sung"
            >
              {[15, 30, 60].map((value) => (
                <button
                  key={value}
                  type="button"
                  className="btn secondary small"
                  aria-pressed={Number(minutes) === value}
                  disabled={busy}
                  onClick={() => {
                    setMinutes(value);
                    setPreview(null);
                  }}
                >
                  +{value} phút
                </button>
              ))}
            </div>
            <form
              onSubmit={async (event) => {
                event.preventDefault();
                setBusy(true);
                setError("");
                setPreview(null);
                try {
                  const value = await api(
                    `/api/sessions/${current.sessionId}/extra-time/preview`,
                    "POST",
                    { minutes: Number(minutes) },
                  );
                  if (alive.current) setPreview(value);
                } catch (failure) {
                  if (alive.current) setError(failure.message);
                } finally {
                  if (alive.current) setBusy(false);
                }
              }}
            >
              <label className="field">
                Số phút cần thêm
                <input
                  type="number"
                  min={5}
                  max={480}
                  step={1}
                  required
                  value={minutes}
                  disabled={busy}
                  onChange={(event) => {
                    setMinutes(event.target.value);
                    setPreview(null);
                  }}
                />
              </label>
              <button className="btn primary small" disabled={busy}>
                {busy ? "NORA đang kiểm tra…" : "NORA đề xuất thêm giờ"}
              </button>
            </form>
            {preview && (
              <div
                className={`work-time-proposal ${preview.feasible ? "" : "is-blocked"}`}
                role="status"
              >
                <p>{preview.message}</p>
                {preview.slots?.map((slot) => (
                  <p key={slot.id}>
                    {icon("calendar")} {dateFmt(slot.start)} ·{" "}
                    <strong>
                      {timeFmt(slot.start)}–{timeFmt(slot.end)}
                    </strong>
                  </p>
                ))}
                {preview.feasible && (
                  <button
                    type="button"
                    className="btn primary small"
                    disabled={busy}
                    onClick={async () => {
                      setBusy(true);
                      setError("");
                      try {
                        const result = await api(
                          `/api/sessions/extra-time/${preview.id}/confirm`,
                          "POST",
                          { revision: preview.revision },
                        );
                        setMessage(result.message);
                        setPreview(null);
                        await changed();
                      } catch (failure) {
                        setError(failure.message);
                        setPreview(null);
                      } finally {
                        setBusy(false);
                      }
                    }}
                  >
                    Xác nhận thêm {preview.minutes} phút
                  </button>
                )}
              </div>
            )}
          </div>
        </>
      )}
      {error && (
        <p role="alert" className="team-error">
          {error}
        </p>
      )}
      {message && <p role="status">{message}</p>}
    </section>
  );
}
