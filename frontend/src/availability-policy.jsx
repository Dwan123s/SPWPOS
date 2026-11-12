import { useEffect, useState } from "react";
import { api } from "./api.js";
import { state, timeValue, timeFmt, dateFmt } from "./ui.jsx";
import { SessionList } from "./team-session-list.jsx";
import { askNora } from "./agent-handoff.js";

export function AvailabilityPolicyCard({
  onWorkspaceChanged,
  embedded = false,
  autoPreview = false,
  timetableTurnId,
  useTimetableHours = false,
  onConfirmed,
}) {
  const params = new URLSearchParams();
  if (timetableTurnId) params.set("timetableTurnId", timetableTurnId);
  if (useTimetableHours) params.set("useTimetableHours", "true");
  const query = params.size ? `?${params}` : "";
  const [current, setCurrent] = useState(state.workspace?.availabilityPolicy);
  const [preview, setPreview] = useState(null),
    [text, setText] = useState("");
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const [days, setDays] = useState([]);
  useEffect(() => {
    if (!autoPreview || !state.workspace?.events?.length) return;
    let live = true;
    setBusy(true);
    setError("");
    setPreview(null);
    api(`/api/time/policy/preview${query}`, "POST")
      .then((data) => {
        if (live) setPreview(data);
      })
      .catch((e) => {
        if (live) setError(e.message);
      })
      .finally(() => {
        if (live) setBusy(false);
      });
    return () => {
      live = false;
    };
  }, [autoPreview, query]);
  useEffect(() => {
    let live = true;
    if (current?.confirmed)
      api("/api/time/policy/slots")
        .then((data) => {
          if (live) setDays(data);
        })
        .catch((e) => {
          if (live) setError(e.message);
        });
    return () => {
      live = false;
    };
  }, [current]);
  async function run(action) {
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  }
  const p = preview?.policy || current;
  const routineDays = preview?.days || days;
  const hasDaily = routineDays.some((day) => day.routine);
  const weekStart = preview?.weekStart || routineDays[0]?.date;
  const weekEnd = preview?.weekEnd || routineDays.at(-1)?.date;
  return (
    <section
      className={`card stack${embedded ? " team-availability-setup" : ""}`}
    >
      <h2>
        {autoPreview
          ? "Lịch sinh hoạt và giờ rảnh NORA đề xuất"
          : "Chính sách sinh hoạt"}
      </h2>
      {weekStart && weekEnd && <p className="info"><strong>Tuần {dateFmt(`${weekStart}T12:00:00Z`, { timeZone: "UTC" })} – {dateFmt(`${weekEnd}T12:00:00Z`, { timeZone: "UTC", year: "numeric" })}</strong><br />Đề xuất chỉ áp dụng trong tuần này, theo thời khóa biểu bạn đã gửi.</p>}
      <p>
        {current?.confirmed
          ? "Đã xác nhận. Khoảng trống được tính lại theo lịch cố định thực tế từng ngày."
          : "NORA trừ lịch cố định, giờ ngủ, ăn trưa/tối 30 phút, nghỉ ngơi và di chuyển để tìm giờ rảnh. Bạn xem đề xuất rồi áp dụng, không cần tự nhập giờ rảnh."}
      </p>
      {p && (
        <div>
          {!hasDaily && (
            <p>
              Khung sinh hoạt: {timeValue(p.awakeStart)}–{timeValue(p.awakeEnd)}
            </p>
          )}
          {!hasDaily && (
            <p>
              Nghỉ trưa: {timeValue(p.lunchStart)}–{timeValue(p.lunchEnd)} ·
              Nghỉ tối: {timeValue(p.dinnerStart)}–{timeValue(p.dinnerEnd)}
            </p>
          )}
          {hasDaily && (
            <p>
              Giờ thức dậy, đi ngủ, ăn và nghỉ được đề xuất theo lịch từng ngày
              bên dưới.
            </p>
          )}
          <p>
            Đệm di chuyển: {p.commuteMinutes} phút · Khoảng tập trung tối thiểu:{" "}
            {p.focusMinutes} phút
          </p>
        </div>
      )}
      {busy && !preview && (
        <p role="status">
          NORA đang sắp xếp ăn, ngủ, nghỉ quanh thời khóa biểu và tính các
          khoảng rảnh…
        </p>
      )}
      {!autoPreview && (
        <button
          className="btn secondary"
          disabled={busy || !state.workspace?.events?.length}
          onClick={() =>
            run(async () =>
              setPreview(await api(`/api/time/policy/preview${query}`, "POST")),
            )
          }
        >
          {useTimetableHours ? "Tính giờ làm việc từ lịch đã lưu" : "Xem đề xuất giờ sinh hoạt"}
        </button>
      )}
      {autoPreview && error && (
        <button
          className="btn secondary"
          disabled={busy}
          onClick={() =>
            run(async () =>
              setPreview(await api(`/api/time/policy/preview${query}`, "POST")),
            )
          }
        >
          Thử lại đề xuất lịch sinh hoạt
        </button>
      )}
      <details className="routine-adjust">
        <summary>Điều chỉnh đề xuất bằng lời</summary>
        <form
          className="stack"
          onSubmit={(e) => {
            e.preventDefault();
            run(async () =>
              setPreview(await api(`/api/time/policy/draft${query}`, "POST", { text })),
            );
          }}
        >
          <label className="field">
            Trao đổi giờ sinh hoạt với NORA
            <textarea
              rows={2}
              maxLength={2000}
              required
              value={text}
              onChange={(e) => {
                setText(e.target.value);
                setPreview(null);
              }}
              placeholder="Ví dụ: Tôi ngủ lúc 22h, ăn tối từ 18h30 đến 19h, di chuyển mất 20 phút."
            />
          </label>
          <button
            className="btn secondary"
            disabled={busy || !state.workspace?.events?.length}
          >
            NORA phân tích thay đổi
          </button>
        </form>
      </details>
      {error && <p role="alert">{error}</p>}
      {!!(preview?.days || days).length && (
        <details open>
          <summary>
            Phân tích 7 ngày{" "}
            {preview ? "theo chính sách đề xuất" : "từ lịch đã lưu"}
          </summary>
          {(preview?.days || days).map((day) => (
            <div key={day.date} className="routine-day">
              <strong>
                {dateFmt(`${day.date}T12:00:00`, {
                  weekday: "long",
                  day: "2-digit",
                  month: "2-digit",
                })}
              </strong>
              {day.routine && (
                <p>
                  Thức dậy {timeValue(day.routine.awakeStart)} · Đi ngủ{" "}
                  {timeValue(day.routine.awakeEnd)} · Ăn trưa{" "}
                  {timeValue(day.routine.lunchStart)}–
                  {timeValue(day.routine.lunchEnd)} · Ăn tối{" "}
                  {timeValue(day.routine.dinnerStart)}–
                  {timeValue(day.routine.dinnerEnd)}
                </p>
              )}
              <p>
                Giờ rảnh sau khi trừ lịch cố định và sinh hoạt:{" "}
                {Math.floor(
                  (day.workGaps || []).reduce(
                    (sum, s) =>
                      sum + (new Date(s.end) - new Date(s.start)) / 60000,
                    0,
                  ),
                )}{" "}
                phút. Có thể xếp thêm tối đa {day.schedulableMinutes} phút công
                việc theo giới hạn ngày.
              </p>
              {!!day.workGaps?.length && (
                <p className="routine-free-summary">
                  Giờ rảnh:{" "}
                  {day.workGaps
                    .map((s) => `${timeFmt(s.start)}–${timeFmt(s.end)}`)
                    .join(" · ")}
                </p>
              )}
              <p>
                Khoảng tập trung:{" "}
                {day.focusSlots.length
                  ? day.focusSlots
                      .map((s) => `${timeFmt(s.start)}–${timeFmt(s.end)}`)
                      .join(" · ")
                  : "Chưa có khoảng phù hợp"}
              </p>
              {!!day.timeline?.length && (
                <details className="routine-timeline">
                  <summary>Xem lịch sinh hoạt trong ngày</summary>
                  <ol>
                    {day.timeline.map((block, i) => (
                      <li
                        className={`routine-block ${block.kind.toLowerCase()}`}
                        key={i}
                      >
                        <time>
                          {timeFmt(block.start)}–{timeFmt(block.end)}
                        </time>
                        <span>{block.label}</span>
                      </li>
                    ))}
                  </ol>
                </details>
              )}
              <p>
                Các khoảng ngắn vẫn được giữ để bạn dùng linh hoạt; phiên tập
                trung còn phải phù hợp thời lượng và deadline của công việc.
              </p>
            </div>
          ))}
        </details>
      )}
      {preview && (
        <div className="stack">
          <p>
            {useTimetableHours
              ? "Khi áp dụng, tuần này sẽ dùng các khung giờ tính từ thời khóa biểu. Các giờ riêng bạn đã đặt theo ngày vẫn được ưu tiên."
              : "Đây là đề xuất; lịch chưa thay đổi. Giờ rảnh bạn đã xác nhận trước đó được giữ lại."}
          </p>
          {preview.warnings.map((warning, i) => (
            <p role="status" key={i}>
              {warning}
            </p>
          ))}
          {!!preview.before.length && (
            <details>
              <summary>Lịch hiện tại</summary>
              <SessionList items={preview.before} />
            </details>
          )}
          {!!preview.after.length && (
            <details open>
              <summary>Lịch sau khi áp dụng</summary>
              <SessionList items={preview.after} />
            </details>
          )}
          <div className="form-actions">
            <button
              className="btn primary"
              disabled={busy || !!preview.warnings.length}
              onClick={() =>
                run(async () => {
                  await api(`/api/time/policy/${preview.id}/confirm`, "POST");
                  setCurrent({ ...preview.policy, confirmed: true });
                  setPreview(null);
                  await onWorkspaceChanged?.();
                  onConfirmed?.(preview.weekStart);
                })
              }
            >
              {useTimetableHours ? "Áp dụng giờ làm việc tuần này" : "Xác nhận chính sách và lịch"}
            </button>
            <button
              className="btn secondary"
              disabled={busy}
              onClick={() => setPreview(null)}
            >
              Hủy đề xuất
            </button>
          </div>
        </div>
      )}
      <button
        className="btn secondary"
        onClick={() =>
          askNora(
            "Tôi muốn thêm hoạt động riêng vào lịch, ngoài thời khóa biểu và lịch sinh hoạt đã đề xuất. Hãy để tôi mô tả hoạt động và thời gian, rồi kiểm tra tác động tới giờ rảnh.",
          )
        }
      >
        Thêm hoạt động riêng / thời gian extra
      </button>
    </section>
  );
}
