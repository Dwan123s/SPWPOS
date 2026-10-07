import { brand } from "./brand.jsx";
import { icon } from "./ui.jsx";
import { LoginBackground, LoginScene } from "./login-scene.jsx";
import { LoginMotionControl } from "./login-motion.jsx";

function AuthForeground({ login, children }) {
  return login ? <div className="login-foreground">{children}</div> : children;
}

export function authView(mode, title, description, fields, buttons) {
  const login = mode === "login";
  return (
    <div
      className={`auth-layout auth-facebook ${login ? "is-login login-composition" : "is-account"}`}
    >
      <div className="auth-content">
        {login && <LoginBackground />}
        <AuthForeground login={login}>
          <aside className="auth-story">
            {brand("/")}
            {login ? (
              <LoginScene />
            ) : (
              <div className="auth-story-hero">
                <h1>
                  Khám phá <br />
                  nhịp làm việc <br />
                  <span>
                    phù hợp <br />
                    với bạn.
                  </span>
                </h1>
                <img
                  className="auth-hero-art"
                  src="/assets/login-hero-planora.png"
                  alt=""
                  width="1122"
                  height="1402"
                  decoding="async"
                />
              </div>
            )}
          </aside>
          <main className="auth-area">
            <section className="auth-card" aria-labelledby="auth-title">
              {login && (
                <div className="login-card-decoration" aria-hidden="true">
                  <picture className="login-card-ribbon">
                    <source
                      srcSet="/assets/login-sprites/ribbon-background.webp"
                      type="image/webp"
                    />
                    <img
                      src="/assets/login-sprites/ribbon-background.png"
                      alt=""
                      decoding="async"
                    />
                  </picture>
                  <img
                    className="login-card-bubble"
                    src="/assets/login-sprites/bubble.webp"
                    alt=""
                  />
                  <img
                    className="login-card-bubble-small"
                    src="/assets/login-sprites/bubble.webp"
                    alt=""
                  />
                </div>
              )}
              <div className="fb-header-row">
                {!login && (
                  <button
                    type="button"
                    className="fb-back-btn"
                    data-action="auth-login"
                    aria-label="Quay lại đăng nhập"
                  >
                    {icon("back")}
                  </button>
                )}
                <h2 id="auth-title">
                  {login ? (
                    <>
                      <span className="login-title-intro">Đăng nhập vào</span>{" "}
                      <span className="login-title-brand">PLANORA</span>
                    </>
                  ) : (
                    title
                  )}
                </h2>
              </div>
              {!login && <p className="fb-subtitle">{description}</p>}
              <form data-form="auth" data-mode={mode}>
                {fields}
                <div className="form-error" role="alert" />
                <button type="submit" className="btn primary auth-submit">
                  {buttons[mode]}
                  {login && icon("arrow")}
                </button>
              </form>
              {login ? (
                <>
                  <div className="login-divider">
                    <span>Hoặc</span>
                  </div>
                  <div className="auth-register">
                    <button
                      type="button"
                      className="btn auth-create-account"
                      data-action="auth-register"
                    >
                      {icon("userPlus")}Tạo tài khoản mới
                    </button>
                  </div>
                  <div className="auth-forgot">
                    <button
                      type="button"
                      className="text-btn"
                      data-action="auth-forgot"
                    >
                      Quên mật khẩu?
                    </button>
                  </div>
                </>
              ) : (
                <div className="auth-register">
                  <button
                    type="button"
                    className="text-btn"
                    data-action="auth-login"
                  >
                    Quay lại đăng nhập
                  </button>
                </div>
              )}
              {mode === "verify" && (
                <p className="auth-resend">
                  Chưa nhận được mã?{" "}
                  <button
                    type="button"
                    className="text-btn"
                    data-action="auth-resend"
                  >
                    Gửi lại mã
                  </button>
                </p>
              )}
            </section>
            {!login && (
              <div className="auth-card-caption">
                <img
                  src="/assets/planora-mark.png"
                  width="32"
                  height="32"
                  alt=""
                />
                <span>PLANORA</span>
              </div>
            )}
          </main>
        </AuthForeground>
      </div>
      <footer className="auth-site-footer">
        <div className="auth-footer-inner">
          <div className="auth-footer-language">
            {login && icon("globe")}Tiếng Việt
            {login && <LoginMotionControl />}
          </div>
          {login ? (
            <>
              {brand("/", "login-footer-brand")}
              <span className="login-footer-tagline">
                Plan smarter. Work realistically.
              </span>
            </>
          ) : (
            <div className="auth-footer-details">
              <span>PLANORA · Personal Workspace</span>
              <span>Plan smarter. Work realistically.</span>
            </div>
          )}
        </div>
      </footer>
    </div>
  );
}
