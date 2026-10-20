import { useOutsideDismiss } from "./popup-dismissal.jsx";
import { useLayoutEffect, useRef, useState } from "react";
import { brand } from "./brand.jsx";
import { icon } from "./ui.jsx";
import { LoginBackground } from "./login-scene.jsx";
import {
  GettingStartedMotion,
  readStartMotionPaused,
} from "./getting-started-motion.jsx";
import { StartEntry } from "./start-entry.jsx";
import { MascotEyes } from "./mascot-eyes.jsx";

const steps = [
  {
    label: "Nhập việc",
    icon: "edit",
    title: "Bắt đầu từ điều đang ở trong đầu bạn.",
    text: "Viết việc cần làm bằng lời của bạn. AI giúp tách thành công việc, gợi ý thời lượng và hạn chót để bạn kiểm tra.",
  },
  {
    label: "Chọn giờ rảnh",
    icon: "clock",
    title: "Một kế hoạch phải vừa với cuộc sống.",
    text: "Chọn lúc bạn có thể làm việc. Thêm lịch học, cuộc họp hay cuộc hẹn để hệ thống dành đúng khoảng trống cho công việc.",
  },
  {
    label: "Tạo lịch",
    icon: "calendar",
    title: "Từ danh sách việc đến thời gian cụ thể.",
    text: "Xem lịch đề xuất trước khi lưu, hoặc để AI tạo công việc và lưu lịch ngay. Nếu thiếu thời gian, cảnh báo giúp bạn điều chỉnh.",
  },
  {
    label: "Bắt đầu làm",
    icon: "play",
    title: "Một khoảng thời gian. Một việc cần làm.",
    text: "Bắt đầu một phiên, tạm dừng khi cần và ghi nhận tiến độ. Bạn luôn biết mình đang làm gì và bước tiếp theo là gì.",
  },
];
const sampleSessions = [
  { time: "19:00", end: "20:00", name: "Viết báo cáo", duration: "60 phút" },
  { time: "20:10", end: "20:55", name: "Ôn chương 4", duration: "45 phút" },
];
function ProductDemo() {
  const [step, setStep] = useState(0),
    panel = useRef(null);
  useLayoutEffect(() => {
    const root = panel.current.closest(".getting-started");
    if (root?.dataset.motionState !== "running" || !panel.current.animate)
      return;
    const effect = panel.current.animate(
      [
        { opacity: 0.35, translate: "0 10px" },
        { opacity: 1, translate: "0 0" },
      ],
      { duration: 300, easing: "cubic-bezier(.22,1,.36,1)" },
    );
    return () => effect.cancel();
  }, [step]);
  return (
    <div className="start-demo" data-start-reveal>
      <div
        className="start-demo-tabs"
        role="tablist"
        aria-label="Khám phá quy trình PLANORA"
      >
        {steps.map((item, index) => (
          <button
            key={item.label}
            type="button"
            role="tab"
            id={`start-demo-tab-${index}`}
            aria-selected={step === index}
            aria-controls="start-demo-panel"
            tabIndex={step === index ? 0 : -1}
            onClick={() => setStep(index)}
            onKeyDown={(event) => {
              const next =
                event.key === "ArrowRight"
                  ? (step + 1) % 4
                  : event.key === "ArrowLeft"
                    ? (step + 3) % 4
                    : event.key === "Home"
                      ? 0
                      : event.key === "End"
                        ? 3
                        : null;
              if (next !== null) {
                event.preventDefault();
                setStep(next);
                document.getElementById(`start-demo-tab-${next}`)?.focus();
              }
            }}
          >
            <span>{index + 1}</span>
            {item.label}
          </button>
        ))}
      </div>
      <div
        ref={panel}
        id="start-demo-panel"
        className="start-demo-panel"
        role="tabpanel"
        aria-labelledby={`start-demo-tab-${step}`}
      >
        <div className="start-demo-copy">
          <span className="start-eyebrow">Bước 0{step + 1}</span>
          <h3>{steps[step].title}</h3>
          <p>{steps[step].text}</p>
          <button
            className="start-text-button"
            type="button"
            onClick={() => setStep((step + 1) % 4)}
          >
            {step === 3 ? "Xem lại từ đầu" : "Xem bước tiếp theo"}
            {icon("arrow")}
          </button>
          <small>Ví dụ minh họa · Không lưu dữ liệu hoặc gọi AI.</small>
        </div>
        <div className="start-demo-preview" key={step}>
          <div className="start-preview-header">
            <span>
              {icon(steps[step].icon)}
              {steps[step].label}
            </span>
            <span className="start-preview-dots" aria-hidden="true">
              •••
            </span>
          </div>
          {step === 0 && (
            <>
              <label className="start-sample-note">
                Ghi chú tuần này
                <textarea
                  aria-label="Ghi chú minh họa"
                  readOnly
                  rows={5}
                  value={
                    "Viết báo cáo, khoảng 1 giờ\nÔn chương 4, khoảng 45 phút\nTối nay mình rảnh từ 19:00 đến 21:00"
                  }
                />
              </label>
              <div className="start-sample-result">
                {icon("check")} Hai việc cần làm đã được nhận diện
              </div>
            </>
          )}
          {step === 1 && (
            <div className="start-sample-hours">
              <span>Giờ rảnh minh họa</span>
              <div className="start-week-days">
                {["T2", "T3", "T4", "T5", "T6", "T7", "CN"].map(
                  (day, index) => (
                    <span
                      className={
                        index === 0 || index === 2 ? "is-selected" : ""
                      }
                      key={day}
                    >
                      {day}
                    </span>
                  ),
                )}
              </div>
              <strong>
                19:00 <span>đến</span> 21:00
              </strong>
              <div className="start-hours-bar">
                <span />
              </div>
              <p>Các khoảng bận được dành riêng.</p>
            </div>
          )}
          {step === 2 && (
            <div className="start-sample-calendar">
              {sampleSessions.map((session, index) => (
                <div key={session.name}>
                  <span className="start-sample-time">
                    {session.time}
                    <small>{session.end}</small>
                  </span>
                  <div
                    className={`start-sample-session ${index === 1 ? "is-blue" : ""}`}
                  >
                    <strong>{session.name}</strong>
                    <span>{session.duration}</span>
                  </div>
                  {index === 0 && (
                    <p className="start-sample-break">Nghỉ 10 phút</p>
                  )}
                </div>
              ))}
            </div>
          )}
          {step === 3 && (
            <div className="start-sample-focus">
              <div className="start-focus-ring">{icon("play")}</div>
              <span>Sẵn sàng tập trung</span>
              <strong>Viết báo cáo</strong>
              <div className="start-focus-clock">25:00</div>
              <p>Bắt đầu phiên → Làm việc → Cập nhật tiến độ</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

const faqs = [
  [
    "Mình bắt đầu từ đâu?",
    "Tạo tài khoản, xác minh email rồi bắt đầu. Bạn nhập việc, chọn giờ rảnh, tạo lịch và bắt đầu phiên đầu tiên.",
  ],
  [
    "Ghi chú được gửi đến AI khi nào?",
    "Khi bạn bấm Xem trước công việc hoặc Tạo và lưu lịch ngay trong trang ghi chú. Lưu bản nháp chưa gửi nội dung đến AI. Phần minh họa trên trang này không gọi AI.",
  ],
  [
    "Nếu lịch không đủ chỗ cho mọi công việc?",
    "PLANORA hiển thị cảnh báo và thời lượng chưa xếp được. Bạn có thể bổ sung giờ rảnh, sửa thời lượng hoặc điều chỉnh hạn chót rồi tạo lại lịch.",
  ],
  [
    "Mình có thể sửa lịch và công việc không?",
    "Có. Bạn có thể sửa công việc, cập nhật tiến độ và điều chỉnh các phiên chưa khóa. Kết thúc một phiên không tự đánh dấu hoàn thành toàn bộ công việc.",
  ],
];
export function GettingStarted({ signedIn = false, onLogin }) {
  const [menuOpen, setMenuOpen] = useState(false);
  const menuPanel = useRef(null),
    menuTrigger = useRef(null);
  useOutsideDismiss(
    [menuPanel, menuTrigger],
    () => setMenuOpen(false),
    menuOpen,
  );
  const cta = signedIn ? "#today" : "#register";
  const info = (section) => {
    setMenuOpen(false);
    const target = document.getElementById(`start-${section}`);
    target?.scrollIntoView?.({
      behavior:
        target.closest(".getting-started").dataset.motionState === "running"
          ? "smooth"
          : "instant",
      block: "start",
    });
    target?.focus({ preventScroll: true });
  };
  return (
    <div
      className="getting-started login-composition start-immersive start-scroll"
      data-motion-state={
        matchMedia("(prefers-reduced-motion: reduce)").matches
          ? "reduced"
          : readStartMotionPaused()
            ? "paused"
            : "running"
      }
    >
      <a
        className="skip-link"
        href="#start-main"
        onClick={(event) => {
          event.preventDefault();
          document.getElementById("start-main")?.focus();
        }}
      >
        Đến nội dung chính
      </a>
      <header className="start-header">
        <div className="start-header-inner">
          {brand("#getting-started", "start-brand")}
          <nav
            ref={menuPanel}
            id="start-navigation"
            className={menuOpen ? "is-open" : ""}
            aria-label="Điều hướng giới thiệu"
            onKeyDown={(event) => {
              if (event.key === "Escape") {
                setMenuOpen(false);
                menuTrigger.current?.focus();
              }
            }}
          >
            <button type="button" onClick={() => info("about")}>
              Về PLANORA
            </button>
            <button type="button" onClick={() => info("how")}>
              Cách hoạt động
            </button>
            <button type="button" onClick={() => info("faq")}>
              Câu hỏi thường gặp
            </button>
          </nav>
          <div className="start-header-actions">
            <GettingStartedMotion />
            <a
              className="start-login-link"
              href={signedIn ? "#today" : "#login"}
            >
              {signedIn ? "Vào ứng dụng" : "Đăng nhập"}
              {icon("arrow")}
            </a>
            <button
              ref={menuTrigger}
              onKeyDown={(event) => {
                if (event.key === "Escape") setMenuOpen(false);
              }}
              className="start-menu-toggle"
              type="button"
              aria-label={
                menuOpen ? "Đóng menu giới thiệu" : "Mở menu giới thiệu"
              }
              aria-expanded={menuOpen}
              aria-controls="start-navigation"
              onClick={() => setMenuOpen(!menuOpen)}
            >
              {icon(menuOpen ? "close" : "menu")}
            </button>
          </div>
        </div>
      </header>
      <main id="start-main" tabIndex={-1}>
        <section className="start-hero" aria-labelledby="start-title">
          <LoginBackground />
          <div className="start-world-title" data-start-reveal>
            <p>PLAN SMARTER. WORK REALISTICALLY.</p>
            <h1 id="start-title">PLANORA</h1>
          </div>
          <div className="start-world-floor" aria-hidden="true">
            <span />
            <span />
            <span />
          </div>
          <div className="start-hero-art">
            <div className="start-art-halo" />
            <div className="start-mascot" data-start-reveal>
              <div className="start-mascot-figure">
                <img
                  src="/assets/start/planora-owl-v1.png"
                  width="1254"
                  height="1254"
                  alt="Linh vật cú xanh PLANORA ôm một cuốn lịch"
                  fetchPriority="high"
                  decoding="async"
                />
                <MascotEyes />
                <StartEntry
                  onContinue={
                    signedIn
                      ? () => {
                          location.hash = "today";
                        }
                      : onLogin
                  }
                />
              </div>
              <span className="start-mascot-shadow" aria-hidden="true" />
            </div>
          </div>
          <div className="start-world-footer">
            <button
              className="start-scroll-cue"
              type="button"
              onClick={() => info("about")}
            >
              <span className="start-scroll-mark" aria-hidden="true" />
              Cuộn để khám phá
            </button>
            <a href={cta}>
              {signedIn ? "Không gian của bạn" : "Tạo tài khoản"}
              {icon("arrow")}
            </a>
          </div>
        </section>
        <div className="start-content start-container">
          <section
            id="start-about"
            className="start-section start-about"
            tabIndex={-1}
            aria-labelledby="start-about-title"
          >
            <div className="start-about-intro" data-start-reveal>
              <div>
                <span className="start-eyebrow">
                  Một kế hoạch. Một khởi đầu.
                </span>
                <h2 id="start-about-title">
                  Việc nhiều.
                  <br />
                  <span>Ngày vẫn nhẹ nhàng.</span>
                </h2>
              </div>
              <p>
                Ghi lại điều cần làm bằng lời của bạn. AI giúp chuyển ghi chú
                thành công việc; PLANORA xếp lịch theo giờ rảnh, lịch bận và hạn
                chót. Một người bạn nhỏ, giúp bạn biết bước tiếp theo là gì.
              </p>
            </div>
            <div className="start-about-points" data-start-reveal>
              {[
                [
                  "edit",
                  "01",
                  "Ghi chú thành công việc",
                  "Kiểm tra kết quả AI trước khi tạo việc, hoặc chọn tạo và lưu lịch ngay.",
                ],
                [
                  "calendar",
                  "02",
                  "Một lịch vừa với bạn",
                  "Dành thời gian cho việc quan trọng và xem cảnh báo khi lịch chưa đủ chỗ.",
                ],
                [
                  "play",
                  "03",
                  "Tập trung từng phiên",
                  "Bắt đầu một phiên, ghi nhận thời gian rồi cập nhật tiến độ công việc.",
                ],
              ].map(([symbol, number, title, text]) => (
                <article key={number}>
                  <div>
                    <span>{number}</span>
                    {icon(symbol)}
                  </div>
                  <h3>{title}</h3>
                  <p>{text}</p>
                </article>
              ))}
            </div>
          </section>
          <section
            id="start-how"
            className="start-section"
            tabIndex={-1}
            aria-labelledby="start-how-title"
          >
            <div className="start-section-heading" data-start-reveal>
              <span className="start-eyebrow">Từ ý tưởng đến hành động</span>
              <h2 id="start-how-title">Bốn bước để bắt đầu.</h2>
              <p>
                Chọn từng bước để khám phá. Ví dụ bên dưới không được lưu và
                không gọi AI.
              </p>
            </div>
            <ProductDemo />
          </section>
          <section
            id="start-faq"
            className="start-section start-faq"
            tabIndex={-1}
            aria-labelledby="start-faq-title"
          >
            <div className="start-section-heading" data-start-reveal>
              <span className="start-eyebrow">Trước khi bắt đầu</span>
              <h2 id="start-faq-title">
                Một vài điều
                <br />
                bạn muốn biết.
              </h2>
              <p>Hiểu cách PLANORA làm việc cùng bạn.</p>
            </div>
            <div className="start-faq-list" data-start-reveal>
              {faqs.map(([question, answer]) => (
                <details key={question}>
                  <summary>
                    {question}
                    <span aria-hidden="true">+</span>
                  </summary>
                  <p>{answer}</p>
                </details>
              ))}
            </div>
          </section>
          <section
            className="start-final-cta"
            aria-labelledby="start-ready-title"
            data-start-reveal
          >
            <span className="start-eyebrow">
              Sẵn sàng cho một ngày có chủ đích?
            </span>
            <h2 id="start-ready-title">
              Kế hoạch của bạn.
              <br />
              <span>Bắt đầu từ đây.</span>
            </h2>
            <a className="start-button start-button-primary" href={cta}>
              {signedIn ? "Vào ứng dụng" : "Tạo tài khoản"}
              {icon("arrow")}
            </a>
            {!signedIn && (
              <p>
                Đã có tài khoản? <a href="#login">Đăng nhập</a>
              </p>
            )}
          </section>
          <footer className="start-footer">
            <span>PLANORA · Plan smarter. Work realistically.</span>
            <small>Ghi chú → Lịch → Tập trung</small>
          </footer>
        </div>
      </main>
    </div>
  );
}
