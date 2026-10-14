import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import { button, closeModal, icon, modal, state } from "./ui.jsx";
import {
  getGuide,
  subscribeGuide,
  startGuide,
  skipGuide,
  advanceGuide,
} from "./onboarding.js";
import { workflowSteps } from "./workflow.js";
export { workflowSteps } from "./workflow.js";

const menuKey = "planora.menu.compact";
function read(key) {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}
function write(key, value) {
  try {
    localStorage.setItem(key, value);
  } catch {
    /* Storage is optional. */
  }
}

const lessons = [
  {
    title: "Nhập việc cần làm",
    route: "notes",
    cta: "Mở Nhập việc",
    text: "Mỗi việc viết một dòng, thêm thời lượng và hạn chót nếu biết, rồi bấm Xem trước công việc. Bạn cũng có thể thêm việc thủ công trong Công việc khi không dùng AI.",
    example:
      "Viết báo cáo ngày mai, khoảng 2 tiếng\nÔn chương 4 trước thứ Sáu, khoảng 90 phút",
    note: "Lưu bản nháp chỉ lưu nội dung. Xem trước công việc cho bạn sửa kết quả AI trước khi tạo lịch.",
  },
  {
    title: "Chọn thời gian bạn rảnh",
    route: "time",
    cta: "Mở Giờ rảnh",
    text: "Chọn những ngày và giờ bạn có thể làm việc. Các cuộc họp, buổi học hoặc lịch hẹn được thêm ở Sự kiện cố định.",
    example: "Ví dụ: Thứ Hai và Thứ Tư, 19:00–22:00",
    note: "Nếu chưa có giờ rảnh, trang ghi chú sẽ cho bạn chọn ngay trước khi tạo lịch. Ứng dụng không tự đoán thời gian rảnh của bạn.",
  },
  {
    title: "Tạo và lưu lịch",
    route: "notes",
    cta: "Mở ghi chú để tạo lịch",
    text: "Kiểm tra kết quả AI rồi bấm Tạo công việc & xem lịch. Xem giờ làm và các cảnh báo trước khi bấm Lưu lịch này. Với công việc đã có, mở Lịch làm việc và bấm Lập kế hoạch.",
    example:
      "Nhập việc → Xem trước công việc → Chọn giờ rảnh → Xem lịch đề xuất → Lưu lịch này",
    note: "Lịch đề xuất chưa được lưu cho đến khi bấm Lưu lịch này. Nếu thiếu thời gian, xem cảnh báo để biết việc nào chưa được xếp đủ.",
  },
  {
    title: "Làm việc và cập nhật tiến độ",
    route: "calendar",
    cta: "Mở Lịch làm việc",
    text: "Bấm vào một khoảng trong lịch, chọn Bắt đầu. Bạn có thể tạm dừng, tiếp tục hoặc bấm Xong phiên để kết thúc lượt làm.",
    example:
      "Công việc: viết báo cáo, cần 3 giờ\nMột phiên: làm báo cáo hôm nay từ 19:00–20:00",
    note: "Xong phiên chỉ dừng đồng hồ. Sau đó cập nhật tiến độ; chỉ đánh dấu hoàn thành cả công việc khi bạn đã làm xong.",
  },
];

function Tutorial() {
  const [step, setStep] = useState(0);
  const lesson = lessons[step];
  const heading = useRef(null);
  useEffect(() => {
    heading.current?.focus();
  }, [step]);
  return (
    <div className="tutorial-body">
      <ol className="tutorial-progress" aria-label="Các bước hướng dẫn">
        {lessons.map((item, index) => (
          <li
            key={item.title}
            aria-current={index === step ? "step" : undefined}
          >
            <span>{index + 1}</span>
            <small>
              {["Nhập việc", "Giờ rảnh", "Tạo lịch", "Làm việc"][index]}
            </small>
          </li>
        ))}
      </ol>
      <h3 ref={heading} tabIndex={-1}>
        {step + 1}. {lesson.title}
      </h3>
      <p>{lesson.text}</p>
      <div className="tutorial-example">
        <span>Ví dụ minh họa · chưa lưu vào tài khoản</span>
        <pre>{lesson.example}</pre>
      </div>
      <p className="info spaced">{lesson.note}</p>
      <button
        className="btn secondary small"
        type="button"
        onClick={() => {
          closeModal();
          location.hash = lesson.route;
        }}
      >
        {lesson.cta} {icon("arrow")}
      </button>
      <div className="form-actions tutorial-actions">
        {step === 0 && (
          <button
            className="btn secondary"
            type="button"
            onClick={() => {
              startGuide();
              closeModal();
              location.hash = missionFor(getGuide()).route;
            }}
          >
            Hướng dẫn từng thao tác
          </button>
        )}
        <button className="btn ghost" type="button" onClick={closeModal}>
          Đóng hướng dẫn
        </button>
        <button
          className="btn secondary"
          type="button"
          disabled={step === 0}
          onClick={() => setStep(step - 1)}
        >
          Quay lại
        </button>
        <button
          className="btn primary"
          type="button"
          onClick={() =>
            step === lessons.length - 1 ? closeModal() : setStep(step + 1)
          }
        >
          {step === lessons.length - 1 ? "Đã hiểu, bắt đầu dùng" : "Tiếp theo"}
        </button>
      </div>
    </div>
  );
}
export function openTutorial() {
  modal(
    "Hướng dẫn sử dụng PLANORA",
    "4 bước để biến việc cần làm thành lịch và theo dõi tiến độ.",
    <Tutorial />,
  );
}

const missions = [
  {
    title: "Nhập công việc đầu tiên",
    route: "notes",
    target: ".note-editor-actions",
    text: "Nhập các việc cần làm rồi bấm Xem trước công việc để kiểm tra kết quả AI. Bạn cũng có thể mở Công việc và thêm một việc thủ công để bắt đầu.",
    done: "Bạn đã chuẩn bị công việc đầu tiên.",
    link: "Mở Nhập việc",
  },
  {
    title: "Lưu giờ bạn rảnh",
    route: "time",
    target: '[data-form="availability"]',
    text: "Thêm ít nhất một khoảng giờ bạn thật sự rảnh trong tuần rồi bấm Lưu lịch rảnh. Đây là thời gian hệ thống được phép xếp việc.",
    done: "Giờ rảnh đã được lưu.",
    link: "Mở Giờ rảnh",
  },
  {
    title: "Tạo và lưu lịch đầu tiên",
    route: "notes",
    target: '[aria-describedby="note-proposal-help"]',
    text: "Mở lại ghi chú đã phân tích, kiểm tra kết quả và bấm Tạo công việc & xem lịch. Chỉ khi bấm Lưu lịch này, lịch mới được áp dụng và bước này mới hoàn thành.",
    done: "Bạn đã lưu lịch có phiên làm việc.",
    link: "Mở ghi chú để tạo lịch",
  },
  {
    title: "Bắt đầu một phiên",
    route: "calendar",
    target: ".calendar-workspace",
    text: "Chọn một phiên trên lịch và bấm Bắt đầu để ghi nhận thời gian. Xong phiên chỉ kết thúc lượt làm; cập nhật tiến độ riêng khi làm xong.",
    done: "Phiên đã bắt đầu. Bạn đã hoàn thành hướng dẫn!",
    link: "Mở Lịch làm việc",
  },
];
const manualInputMission = {
  ...missions[0],
  route: "tasks",
  target: ".page-header .actions",
  text: "Bấm Thêm công việc, nhập tên, thời lượng và hạn chót rồi lưu. Bạn không cần sử dụng AI để hoàn thành bước này.",
  link: "Mở Công việc",
};
const manualPlanMission = {
  ...missions[2],
  route: "calendar",
  target: ".page-header .actions",
  text: "Bấm Lập kế hoạch để xem lịch đề xuất cho các công việc đã lưu. Kiểm tra giờ làm và cảnh báo, rồi bấm Lưu lịch này khi phù hợp.",
  link: "Mở Lịch làm việc",
};
function missionFor(guide, route) {
  if (guide.step === 0 && route === "tasks") return manualInputMission;
  if (guide.step === 2 && !guide.noteId) return manualPlanMission;
  return missions[guide.step];
}
function openWelcome() {
  modal(
    "Chào mừng đến PLANORA",
    "Bạn muốn được hướng dẫn trực tiếp khi sử dụng lần đầu?",
    <div className="guide-welcome">
      <div className="guide-welcome-symbol" aria-hidden="true">
        {icon("calendar")}
      </div>
      <h3>Học bằng cách làm, từng bước một</h3>
      <p>Nhập việc → Chọn giờ rảnh → Lưu lịch → Bắt đầu làm.</p>
      <p className="info spaced">
        Hướng dẫn sẽ chỉ thao tác trên trang thật. Bạn cần hoàn thành mỗi bước
        để tiếp tục; các việc và lịch bạn tạo được lưu vào tài khoản.
      </p>
      <div className="form-actions">
        <button
          className="btn secondary"
          type="button"
          onClick={() => {
            skipGuide();
            closeModal();
          }}
        >
          Bỏ qua
        </button>
        <button
          className="btn primary"
          type="button"
          onClick={() => {
            startGuide();
            closeModal();
            location.hash = missionFor(getGuide()).route;
          }}
        >
          Tiếp tục
        </button>
      </div>
    </div>,
  );
}
export function GuidedTutorial({ route }) {
  const guide = useSyncExternalStore(subscribeGuide, getGuide, getGuide);
  const mission = missionFor(guide, route);
  const done = guide.completed.includes(guide.step);
  useEffect(() => {
    if (guide.status !== "active" || route !== mission.route || done) return;
    let previous;
    const mark = () => {
      const target = document.querySelector(mission.target);
      if (!target || target === previous) return;
      previous?.removeAttribute("data-guide-highlight");
      previous = target;
      target.setAttribute("data-guide-highlight", "true");
    };
    mark();
    const observer = new MutationObserver(mark);
    observer.observe(document.querySelector("#app") || document.body, {
      childList: true,
      subtree: true,
    });
    return () => {
      observer.disconnect();
      previous?.removeAttribute("data-guide-highlight");
    };
  }, [guide.status, guide.step, route, done, mission]);
  if (guide.status !== "active") return null;
  return (
    <section
      className="guide-mission card"
      aria-labelledby="guide-mission-title"
    >
      <div className="guide-mission-heading">
        <span className="guide-step-number">{guide.step + 1}</span>
        <div>
          <small>Hướng dẫn tương tác · bước {guide.step + 1}/4</small>
          <h2 id="guide-mission-title">{mission.title}</h2>
        </div>
      </div>
      <p>{mission.text}</p>
      <p className={done ? "guide-check" : "reason"} role="status">
        {done
          ? `✓ ${mission.done}`
          : "Hoàn thành thao tác trên để mở bước tiếp theo."}
      </p>
      <div className="guide-mission-actions">
        <button className="btn ghost small" type="button" onClick={skipGuide}>
          Bỏ qua hướng dẫn
        </button>
        {route !== mission.route ? (
          <a className="btn secondary small" href={`#${mission.route}`}>
            {mission.link}
          </a>
        ) : (
          !done && (
            <button
              className="btn secondary small"
              type="button"
              onClick={() =>
                document.querySelector(mission.target)?.scrollIntoView({
                  block: "center",
                  behavior: matchMedia("(prefers-reduced-motion: reduce)")
                    .matches
                    ? "instant"
                    : "smooth",
                })
              }
            >
              Chỉ vị trí thao tác {icon("arrow")}
            </button>
          )
        )}
        <button
          className="btn primary small"
          type="button"
          disabled={!done}
          onClick={() => {
            if (advanceGuide() && getGuide().status === "active")
              location.hash = missionFor(getGuide()).route;
          }}
        >
          {guide.step === 3 ? "Hoàn thành hướng dẫn" : "Tiếp tục"}
        </button>
      </div>
      <div
        className="guide-mission-progress"
        aria-label={`Bước ${guide.step + 1} trong 4 bước`}
      >
        {missions.map((item, index) => (
          <span
            key={item.title}
            className={index <= guide.step ? "is-current" : ""}
          />
        ))}
      </div>
    </section>
  );
}

export function TutorialButton({ route }) {
  useEffect(() => {
    if (
      route !== "today" ||
      state.workspace?.tasks?.length ||
      getGuide().status !== "new"
    )
      return;
    const timer = setTimeout(() => {
      if (
        (location.hash.slice(1) || "today") === "today" &&
        getGuide().status === "new" &&
        !document.querySelector("#modal[open]")
      )
        openWelcome();
    }, 350);
    return () => clearTimeout(timer);
  }, [route]);
  return (
    <button
      type="button"
      className="btn secondary small tutorial-help-button"
      onClick={openTutorial}
    >
      <svg
        className="icon"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.7"
        aria-hidden="true"
      >
        <circle cx="12" cy="12" r="9" />
        <path d="M9.5 9a2.5 2.5 0 0 1 5 0c0 2-2.5 2-2.5 4M12 16.5v.5" />
      </svg>
      <span>Hướng dẫn</span>
    </button>
  );
}

export function NextStep() {
  const steps = workflowSteps(state.workspace);
  const current = steps.findIndex((step) => !step.done);
  const next = steps[current];
  return (
    <section
      className="card workflow-next"
      aria-labelledby="workflow-next-title"
    >
      <div className="workflow-next-main">
        <div>
          <h2 id="workflow-next-title">
            Bước tiếp theo: {next.title.toLowerCase()}
          </h2>
          <p>{next.description}</p>
        </div>
        {button(
          next.label,
          next.action || "nav",
          "primary small",
          next.route || "",
          next.route === "notes"
            ? "edit"
            : next.route === "time"
              ? "clock"
              : "calendar",
        )}
      </div>
      <details className="workflow-roadmap">
        <summary>Xem quy trình 4 bước</summary>
        <ol>
          {steps.map((step, index) => (
            <li
              key={step.title}
              aria-current={index === current ? "step" : undefined}
            >
              <span>{step.done ? icon("check") : index + 1}</span>
              <strong>{step.title}</strong>
              {step.done && <small>Đã có</small>}
            </li>
          ))}
        </ol>
      </details>
    </section>
  );
}

export function MenuLabelsControl() {
  const host = useRef(null);
  const [compact, setCompact] = useState(() => read(menuKey) === "true");
  useLayoutEffect(() => {
    host.current.closest(".planning-console").dataset.navMode = compact
      ? "compact"
      : "expanded";
  }, [compact]);
  return (
    <button
      ref={host}
      type="button"
      className="btn ghost icon-only menu-labels-control"
      aria-label={compact ? "Hiện tên menu" : "Thu gọn menu"}
      title={compact ? "Hiện tên menu" : "Thu gọn menu"}
      aria-expanded={!compact}
      aria-controls="workspace-menu"
      onClick={() => {
        const value = !compact;
        setCompact(value);
        write(menuKey, String(value));
      }}
    >
      {icon("menu")}
    </button>
  );
}
