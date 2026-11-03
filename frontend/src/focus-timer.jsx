import {
  createContext,
  useContext,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from "react";
import { createPortal } from "react-dom";
import { icon, timeFmt, dateFmt } from "./ui.jsx";
import { focusEntries, scheduleClock } from "./scheduled-focus.js";
import {
  adjustFocus,
  clampFocusPosition,
  completeFocus,
  focusStorageKey,
  readFocus,
  timerDisplayMs,
  startFocus,
  tickFocus,
  toggleFocus,
} from "./focus-timer.js";
import { createFocusAudio } from "./focus-audio.js";
import "./styles/focus-popup.css";

const FocusContext = createContext(null);
export const useFocusTimer = () => useContext(FocusContext);
const phaseName = (timer) =>
  timer?.mode === "stopwatch"
    ? "Stopwatch"
    : timer?.phase === "longBreak"
      ? "Nghỉ dài"
      : timer?.phase === "break"
        ? "Nghỉ ngắn"
        : "Tập trung";

export function FocusTimerProvider({ uid, workspace, children }) {
  const [owner, setOwner] = useState(uid);
  const [model, setModel] = useState(() => readFocus(uid));
  const [now, setNow] = useState(Date.now);
  const [announcement, setAnnouncement] = useState("");
  const audio = useRef(null),
    previous = useRef(model.timer);
  if (!audio.current) audio.current = createFocusAudio();
  // Reset only the countdown on account changes, preserving the app's page tree.
  if (owner !== uid) {
    setOwner(uid);
    setModel(readFocus(uid));
    setAnnouncement("");
    previous.current = null;
  }
  useEffect(() => {
    if (!uid) return;
    const refresh = () => {
      const time = Date.now();
      setNow(time);
      setModel((value) => tickFocus(value, time));
    };
    refresh();
    const interval = setInterval(refresh, 1000);
    window.addEventListener("focus", refresh);
    document.addEventListener("visibilitychange", refresh);
    return () => {
      clearInterval(interval);
      window.removeEventListener("focus", refresh);
      document.removeEventListener("visibilitychange", refresh);
    };
  }, [uid]);
  useEffect(() => {
    if (!uid) return;
    try {
      localStorage.setItem(focusStorageKey(uid), JSON.stringify(model));
    } catch {
      /* Countdown still works with storage disabled. */
    }
  }, [model, uid]);
  useEffect(() => () => audio.current.close(), [uid]);
  useEffect(() => {
    const before = previous.current,
      after = model.timer;
    previous.current = after;
    if (
      before?.status === "running" &&
      after &&
      (before.phase !== after.phase ||
        after.status === "completed" ||
        before.round !== after.round)
    ) {
      setAnnouncement(
        after.status === "completed"
          ? "Đã kết thúc phiên tập trung."
          : `${phaseName(before)} đã hết giờ. ${phaseName(after)}${after.status === "paused" ? " đang chờ bắt đầu" : " đã bắt đầu"}.`,
      );
      if (after.mode === "pomodoro" && model.settings.alarm)
        audio.current.chime(model.settings.volume);
    }
  }, [model.timer, model.settings.alarm, model.settings.volume]);
  const change = (operation) => {
    audio.current.unlock();
    setNow(Date.now());
    setModel(operation);
  };
  const value = {
    model,
    now,
    workspace,
    enabled: !!uid,
    open: () => change((m) => ({ ...m, view: "expanded" })),
    view: (view) => change((m) => ({ ...m, view })),
    start: () => {
      setAnnouncement("");
      change((m) => startFocus(m));
    },
    toggle: () => change((m) => toggleFocus(m)),
    adjust: (minutes) => change((m) => adjustFocus(m, minutes)),
    finish: () => change((m) => completeFocus(m)),
    mode: (mode) =>
      change((m) => ({
        ...m,
        mode,
        timer: m.timer?.status === "completed" ? null : m.timer,
      })),
    settings: (patch) =>
      change((m) => ({ ...m, settings: { ...m.settings, ...patch } })),
    position: (position) => setModel((m) => ({ ...m, position })),
  };
  return (
    <FocusContext.Provider value={value}>
      {children}
      {uid && (
        <>
          <span className="sr-only" role="status">
            {announcement}
          </span>
          <FocusPopup />
        </>
      )}
    </FocusContext.Provider>
  );
}

export function FocusLauncher({ compact = false }) {
  const focus = useFocusTimer();
  if (!focus?.enabled) return null;
  const runningPomodoro =
    focus.model.timer?.mode === "pomodoro" &&
    focus.model.timer.status === "running";
  const label = runningPomodoro
    ? "Pomodoro đang chạy"
    : "Mở Stopwatch / Pomodoro";
  return (
    <button
      type="button"
      className={`btn ${compact ? "ghost is-compact" : "secondary small"} focus-launcher ${runningPomodoro ? "is-pomodoro-running" : ""}`}
      onClick={focus.open}
      aria-label={`Focus · ${label}`}
      title={`Focus · ${label}`}
    >
      {runningPomodoro ? (
        <svg
          className="icon"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
          aria-hidden="true"
        >
          <path d="M9 2h6M12 2v3M18 5l2 2" />
          <circle cx="12" cy="14" r="8" />
          <path d="M12 9v5l3 2" />
        </svg>
      ) : (
        icon("clock")
      )}
      <span>Focus</span>
    </button>
  );
}

export function UpcomingFocus({ workspace }) {
  const [now, setNow] = useState(Date.now);
  useEffect(() => {
    const refresh = () => setNow(Date.now());
    const timer = setInterval(refresh, 1000);
    return () => clearInterval(timer);
  }, []);
  const upcoming = focusEntries(workspace, now)
    .filter((item) => Date.parse(item.start) > now)
    .sort((a, b) => Date.parse(a.start) - Date.parse(b.start))
    .slice(0, 3);
  return (
    <section className="focus-upcoming" aria-label="Công việc sắp tới">
      <header>
        <h3>{icon("calendar")} Công việc sắp tới</h3>
        <a href="#calendar">Xem lịch {icon("arrow")}</a>
      </header>
      {upcoming.length ? (
        <ul>
          {upcoming.map((item) => (
            <li key={`${item.kind}-${item.id}`}>
              <span className="focus-upcoming-clock">
                <strong>{timeFmt(item.start)}</strong>
                <small>{dateFmt(item.start)}</small>
              </span>
              <span className="focus-upcoming-name">
                <strong>{item.title}</strong>
                <small>
                  {item.kind === "event" ? "Lịch cố định" : "Phiên làm việc"} ·{" "}
                  {Math.round(
                    (Date.parse(item.end) - Date.parse(item.start)) / 60000,
                  )}{" "}
                  phút
                </small>
              </span>
              <button
                className="btn ghost icon-only"
                type="button"
                title="Chi tiết lịch"
                aria-label={`Chi tiết: ${item.title}`}
                data-action={
                  item.kind === "event" ? "event-detail" : "session-detail"
                }
                data-id={item.id}
              >
                {icon("arrow")}
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p>
          Chưa có lịch sắp tới. Bạn vẫn có thể bắt đầu Stopwatch / Pomodoro.
        </p>
      )}
    </section>
  );
}

function ProgressRing({
  remaining,
  total,
  stopwatch = false,
  running = false,
}) {
  const circumference = 2 * Math.PI * 92;
  return (
    <svg
      className={`focus-progress-ring ${stopwatch ? "is-stopwatch" : ""} ${running ? "is-running" : ""}`}
      viewBox="0 0 200 200"
      aria-hidden="true"
    >
      <circle className="focus-ring-track" cx="100" cy="100" r="92" />
      <circle
        className={
          stopwatch ? "focus-ring-value focus-ring-loader" : "focus-ring-value"
        }
        cx="100"
        cy="100"
        r="92"
        pathLength={stopwatch ? 100 : undefined}
        strokeDasharray={stopwatch ? "22 78" : circumference}
        strokeDashoffset={
          stopwatch
            ? 0
            : circumference *
              (1 - Math.max(0, Math.min(1, remaining / Math.max(1, total))))
        }
      />
    </svg>
  );
}

function MiniFocus() {
  const focus = useFocusTimer(),
    timer = focus.model.timer;
  const [position, setPosition] = useState(() =>
    clampFocusPosition(focus.model.position),
  );
  const drag = useRef(null),
    moved = useRef(false);
  useEffect(() => {
    const resize = () => setPosition((p) => clampFocusPosition(p));
    window.addEventListener("resize", resize);
    return () => window.removeEventListener("resize", resize);
  }, []);
  const remaining = timerDisplayMs(timer, focus.now);
  const endDrag = (event) => {
    if (!drag.current || drag.current.id !== event.pointerId) return;
    drag.current = null;
    focus.position(position);
    event.currentTarget.releasePointerCapture?.(event.pointerId);
  };
  return (
    <div
      className={`focus-mini ${timer.status === "paused" ? "is-paused" : ""} ${remaining >= 3600000 ? "has-hours" : ""}`}
      style={{ left: position.x, top: position.y }}
      role="group"
      aria-label="Bộ đếm thu nhỏ, kéo vòng tròn để di chuyển"
      onPointerDown={(event) => {
        if (event.button !== 0 || event.target.closest("button")) return;
        drag.current = {
          id: event.pointerId,
          x: event.clientX,
          y: event.clientY,
          origin: position,
        };
        moved.current = false;
        event.currentTarget.setPointerCapture?.(event.pointerId);
      }}
      onPointerMove={(event) => {
        const d = drag.current;
        if (!d || d.id !== event.pointerId) return;
        const dx = event.clientX - d.x,
          dy = event.clientY - d.y;
        if (Math.hypot(dx, dy) > 4) moved.current = true;
        if (moved.current)
          setPosition(
            clampFocusPosition({ x: d.origin.x + dx, y: d.origin.y + dy }),
          );
      }}
      onPointerUp={endDrag}
      onPointerCancel={endDrag}
      onLostPointerCapture={() => {
        drag.current = null;
      }}
    >
      <ProgressRing
        remaining={remaining}
        total={timer.durationMs}
        stopwatch={timer.mode === "stopwatch"}
        running={timer.status === "running"}
      />
      <button
        className="focus-mini-time"
        type="button"
        onClick={focus.open}
        title="Phóng to Stopwatch / Pomodoro"
        aria-label="Phóng to Stopwatch / Pomodoro"
      >
        <span role="timer" aria-live="off">
          {scheduleClock(
            timer.mode === "stopwatch"
              ? Math.floor(remaining / 1000)
              : remaining / 1000,
          )}
        </span>
      </button>
      <button
        className="focus-mini-toggle"
        type="button"
        onClick={focus.toggle}
        aria-label={timer.status === "running" ? "Tạm dừng" : "Tiếp tục"}
        title={timer.status === "running" ? "Tạm dừng" : "Tiếp tục"}
      >
        {icon(timer.status === "running" ? "pause" : "play")}
      </button>
    </div>
  );
}

function FocusSettings({ active }) {
  const focus = useFocusTimer(),
    settings = focus.model.settings;
  return (
    <section className="focus-settings" aria-label="Cấu hình phiên">
      <h3>{icon("settings")} Cấu hình phiên</h3>
      <div className="focus-presets" role="group" aria-label="Mẫu thời gian">
        {[
          [25, 5],
          [50, 10],
        ].map(([work, rest]) => (
          <button
            key={work}
            type="button"
            aria-pressed={
              settings.focusMinutes === work && settings.breakMinutes === rest
            }
            onClick={() =>
              focus.settings({ focusMinutes: work, breakMinutes: rest })
            }
          >
            {work}/{rest}
          </button>
        ))}
      </div>
      <div className="focus-durations">
        {[
          ["focusMinutes", "Tập trung"],
          ["breakMinutes", "Nghỉ ngắn"],
          ["longBreakMinutes", "Nghỉ dài"],
        ].map(([name, label]) => (
          <label key={name}>
            {label}
            <span>
              <input
                aria-label={`${label} (phút)`}
                type="number"
                min="1"
                max="180"
                value={settings[name]}
                onChange={(event) => {
                  const n = event.target.valueAsNumber;
                  if (Number.isFinite(n) && n >= 1 && n <= 180)
                    focus.settings({ [name]: Math.round(n) });
                }}
              />{" "}
              phút
            </span>
          </label>
        ))}
      </div>
      {active && (
        <small>
          Thời lượng mới áp dụng từ chặng tiếp theo. Dùng −/+ để chỉnh bộ đếm
          đang chạy.
        </small>
      )}
      <label className="focus-setting-row">
        <span>Tự bắt đầu giờ nghỉ</span>
        <input
          type="checkbox"
          role="switch"
          checked={settings.autoBreak}
          onChange={(e) => focus.settings({ autoBreak: e.target.checked })}
        />
      </label>
      <label className="focus-setting-row">
        <span>Âm báo hết giờ</span>
        <input
          type="checkbox"
          role="switch"
          checked={settings.alarm}
          onChange={(e) => focus.settings({ alarm: e.target.checked })}
        />
      </label>
      <label className="focus-setting-row">
        <span>Âm lượng · {settings.volume}%</span>
        <input
          type="range"
          min="0"
          max="100"
          value={settings.volume}
          onChange={(e) => focus.settings({ volume: Number(e.target.value) })}
        />
      </label>
    </section>
  );
}

function ExpandedFocus() {
  const focus = useFocusTimer(),
    { model, now, workspace } = focus;
  const timer = model.timer,
    active = timer && timer.status !== "completed";
  const stopwatch = (active ? timer.mode : model.mode) === "stopwatch";
  const [settingsOpen, setSettingsOpen] = useState(false),
    [controlsOpen, setControlsOpen] = useState(false);
  const dialog = useRef(null);
  useLayoutEffect(() => {
    const previous = document.activeElement;
    dialog.current.showModal();
    return () => {
      dialog.current?.close();
      if (previous?.isConnected) previous.focus();
    };
  }, []);
  const remaining = timer
    ? timerDisplayMs(timer, now)
    : stopwatch
      ? 0
      : model.settings.focusMinutes * 60000;
  const entries = focusEntries(workspace || {}, now)
    .filter((entry) => Date.parse(entry.start) > now)
    .sort((a, b) => Date.parse(a.start) - Date.parse(b.start));
  const conflict =
    active &&
    !stopwatch &&
    entries.find((item) => Date.parse(item.start) < now + remaining);
  return (
    <dialog
      ref={dialog}
      className="focus-expanded"
      aria-labelledby="focus-popup-title"
      onCancel={(e) => {
        e.preventDefault();
        focus.view(active ? "mini" : "hidden");
      }}
      onClick={(e) => {
        if (e.target === dialog.current) {
          const r = dialog.current.getBoundingClientRect();
          if (
            e.clientX < r.left ||
            e.clientX > r.right ||
            e.clientY < r.top ||
            e.clientY > r.bottom
          )
            focus.view(active ? "mini" : "hidden");
        }
      }}
    >
      <div className="focus-popup-content">
        <header className="focus-popup-header">
          <h2 id="focus-popup-title">{icon("clock")} Phiên tập trung</h2>
          <div>
            <button
              type="button"
              className="focus-window-button"
              aria-label="Ẩn bộ đếm"
              title="Ẩn bộ đếm, phiên vẫn tiếp tục"
              onClick={() => focus.view("hidden")}
            >
              <span aria-hidden="true">−</span>
            </button>
            <button
              type="button"
              className="focus-window-button"
              aria-label="Thu nhỏ bộ đếm"
              title="Thu nhỏ bộ đếm"
              disabled={!active}
              onClick={() => focus.view("mini")}
            >
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M4 10h6V4m10 10h-6v6M10 10 3 3m11 11 7 7" />
              </svg>
            </button>
          </div>
        </header>
        <div
          className="focus-mode-tabs"
          role="group"
          aria-label="Chế độ tập trung"
        >
          {[
            ["stopwatch", "Stopwatch"],
            ["pomodoro", "Pomodoro"],
          ].map(([mode, label]) => (
            <button
              key={mode}
              type="button"
              disabled={!!active}
              aria-pressed={(active ? timer.mode : model.mode) === mode}
              onClick={() => focus.mode(mode)}
            >
              {label}
            </button>
          ))}
        </div>
        <div
          className={`focus-dial ${controlsOpen ? "show-controls" : ""} ${timer?.status === "paused" ? "is-paused" : ""} ${remaining >= 3600000 ? "has-hours" : ""}`}
        >
          <ProgressRing
            remaining={remaining}
            stopwatch={stopwatch}
            running={active && timer.status === "running"}
            total={
              active ? timer.durationMs : model.settings.focusMinutes * 60000
            }
          />
          <span className="focus-phase-label">
            {active
              ? phaseName(timer)
              : timer?.status === "completed"
                ? "Đã kết thúc phiên"
                : "Sẵn sàng"}
          </span>
          <span className="focus-countdown-label">
            {stopwatch ? "THỜI GIAN" : "ĐẾM NGƯỢC KẾT THÚC"}
          </span>
          <button
            type="button"
            className="focus-dial-time"
            aria-label={
              stopwatch ? "Thời gian đã trôi qua" : "Hiện điều chỉnh thời gian"
            }
            aria-expanded={controlsOpen}
            disabled={!active || stopwatch}
            onClick={() => setControlsOpen(!controlsOpen)}
          >
            <span role="timer" aria-live="off">
              {scheduleClock(
                stopwatch ? Math.floor(remaining / 1000) : remaining / 1000,
              )}
            </span>
          </button>
          {active && !stopwatch && (
            <div
              className="focus-time-adjust"
              role="group"
              aria-label="Điều chỉnh thời gian"
            >
              <button
                type="button"
                onClick={() => focus.adjust(-5)}
                disabled={remaining <= 1000}
                aria-label="Bớt 5 phút"
              >
                −5 phút
              </button>
              <button
                type="button"
                onClick={() => focus.adjust(5)}
                disabled={remaining >= 180 * 60000}
                aria-label="Thêm 5 phút"
              >
                +5 phút
              </button>
            </div>
          )}
          <small className="focus-end-time">
            {stopwatch
              ? timer?.status === "completed"
                ? "Đã dừng đồng hồ"
                : timer?.status === "paused"
                  ? "Đang tạm dừng"
                  : "Đếm lên"
              : active
                ? timer.status === "paused"
                  ? "Đang tạm dừng"
                  : `Kết thúc lúc ${timeFmt(now + remaining)}`
                : `${model.settings.focusMinutes} phút dành cho bạn`}
          </small>
        </div>
        {!stopwatch && (
          <div className="focus-cycle">
            <>
              <span className="focus-cycle-dots" aria-hidden="true">
                {[1, 2, 3, 4].map((round) => (
                  <i
                    key={round}
                    className={
                      round <= (active ? timer.round : 1) ? "filled" : ""
                    }
                  />
                ))}
              </span>
              <span>
                Phiên {active ? timer.round : 1}/4 · Nghỉ{" "}
                {model.settings.breakMinutes} phút
              </span>
            </>
          </div>
        )}
        {conflict && (
          <p className="focus-conflict" role="status">
            {icon("alertCircle")} Bộ đếm sẽ trùng “{conflict.title}” lúc{" "}
            {timeFmt(conflict.start)}. Lịch đã lên vẫn được giữ nguyên.
          </p>
        )}
        <div className="focus-main-actions">
          {active ? (
            <>
              <button
                type="button"
                className="btn primary"
                onClick={focus.toggle}
              >
                {icon(timer.status === "running" ? "pause" : "play")}
                {timer.status === "running" ? "Tạm dừng" : "Tiếp tục"}
              </button>
              <button
                type="button"
                className="btn secondary"
                onClick={focus.finish}
              >
                {icon("check")}Kết thúc phiên
              </button>
            </>
          ) : (
            <button
              type="button"
              className="btn primary"
              onClick={() => focus.start()}
            >
              {icon("play")}
              {timer
                ? "Bắt đầu phiên mới"
                : stopwatch
                  ? "Bắt đầu đếm giờ"
                  : "Bắt đầu Pomodoro"}
            </button>
          )}
        </div>
        {!stopwatch && (
          <button
            type="button"
            className="focus-config-button"
            aria-expanded={settingsOpen}
            aria-controls="focus-settings-panel"
            onClick={() => setSettingsOpen(!settingsOpen)}
          >
            {icon("sliders")} Cấu hình thời gian & âm báo
          </button>
        )}
        {!stopwatch && settingsOpen && (
          <div id="focus-settings-panel">
            <FocusSettings active={!!active} />
          </div>
        )}
      </div>
    </dialog>
  );
}

function FocusPopup() {
  const focus = useFocusTimer();
  const { timer, view } = focus.model;
  if (view === "hidden") return null;
  return createPortal(
    view === "expanded" ? (
      <ExpandedFocus />
    ) : timer && timer.status !== "completed" ? (
      <MiniFocus />
    ) : null,
    document.body,
  );
}
