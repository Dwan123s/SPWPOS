import { useEffect, useState } from "react";
import { api } from "./api.js";
import { state, dateFmt, timeFmt } from "./ui.jsx";
import { secretaryMode } from "./agent-handoff.js";
import "./styles/workspace-upgrade.css";

export function CatchUpCard({ onWorkspaceChanged = async () => {} }) {
  const [sessions, setSessions] = useState([]),
    [partial, setPartial] = useState(false);
  const [progress, setProgress] = useState(50),
    [minutes, setMinutes] = useState(25);
  const [busy, setBusy] = useState(false),
    [message, setMessage] = useState("");
  const supported = state.workspace?.features?.includes("ambient-tracking");
  useEffect(() => {
    let active = true;
    if (supported)
      api("/api/sessions/needs-review")
        .then((rows) => {
          if (active && Array.isArray(rows)) setSessions(rows);
        })
        .catch(() => {});
    return () => {
      active = false;
    };
  }, [supported]);
  const session = sessions[0];
  async function report(outcome) {
    if (busy) return;
    setBusy(true);
    setMessage("");
    try {
      const result = await api(`/api/sessions/${session.id}/catch-up`, "POST", {
        outcome,
        ...(outcome === "PARTIAL"
          ? { progress: Number(progress), actualMinutes: Number(minutes) }
          : {}),
      });
      setMessage(result.message);
      setSessions((rows) => rows.slice(1));
      setPartial(false);
      await onWorkspaceChanged();
    } catch (error) {
      setMessage(error.message);
    } finally {
      setBusy(false);
    }
  }
  if (!supported || (!session && !message)) return null;
  return (
    <section className="card catch-up-card" aria-label="Ghi nhận phiên đã qua">
      {session && (
        <>
          <h2>Bạn đã làm được đến đâu?</h2>
          <p>
            Phiên “{session.taskName}” ({dateFmt(session.start)} ·{" "}
            {timeFmt(session.start)}–{timeFmt(session.end)}) đã qua. Bạn có thể
            ghi nhận kết quả mà không cần bật timer.
          </p>
          {partial && !secretaryMode() && (
            <div className="catch-up-partial">
              <label>
                Tiến độ hiện tại của công việc: {progress}%
                <input
                  aria-label="Tiến độ hiện tại"
                  type="range"
                  min="1"
                  max="99"
                  value={progress}
                  onChange={(event) => setProgress(event.target.value)}
                  disabled={busy}
                />
              </label>
              <label>
                Số phút thực tế đã làm
                <input
                  aria-label="Số phút đã làm"
                  type="number"
                  min="1"
                  max="10080"
                  value={minutes}
                  onChange={(event) => setMinutes(event.target.value)}
                  disabled={busy}
                />
              </label>
              <button
                type="button"
                className="btn primary small"
                disabled={busy}
                onClick={() => report("PARTIAL")}
              >
                Ghi nhận một phần
              </button>
            </div>
          )}
          <div className="catch-up-actions">
            <button
              type="button"
              className="btn primary small"
              disabled={busy}
              onClick={() => report("COMPLETED")}
            >
              Đã hoàn thành
            </button>
            {!secretaryMode() && <button
              type="button"
              className="btn secondary small"
              disabled={busy}
              onClick={() => {
                setPartial(!partial);
                const current =
                  state.workspace?.tasks?.find((x) => x.id === session.taskId)
                    ?.progress || 0;
                setProgress(Math.min(99, Math.max(50, current)));
              }}
            >
              Hoàn thành một phần
            </button>}
            <button
              type="button"
              className="btn secondary small"
              disabled={busy}
              onClick={() => report("MISSED")}
            >
              Bị lỡ / Chưa làm
            </button>
          </div>
          <small>
            Nếu còn việc chưa làm, NORA tìm giờ bù trong 48 giờ tới và giữ
            nguyên các lịch khác.
          </small>
        </>
      )}
      {message && <p role="status">{message}</p>}
    </section>
  );
}
