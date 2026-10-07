import { brand } from "./brand.jsx";
import { icon } from "./ui.jsx";
import { LoginBackground, LoginScene } from "./login-scene.jsx";
import { LoginMotionControl } from "./login-motion.jsx";

export function authView(mode, title, description, fields, buttons) {
  const login = mode === "login";
  return (
    <div
      className="auth-layout auth-facebook is-login login-composition"
      data-auth-mode={mode}
    >
      <div className="auth-content">
        <LoginBackground />
        <div className="login-foreground">
          <aside className="auth-story">
            {brand("/")}
            <LoginScene />
          </aside>
          <main className="auth-area">
            <section className="auth-card" aria-labelledby="auth-title">
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
              <div className="auth-panel-viewport">
                <div
                  className="auth-panel-content"
                  key={mode}
                  data-auth-panel={mode}
                >
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
                          <span className="login-title-intro">
                            Đăng nhập vào
                          </span>{" "}
                          <span className="login-title-brand">PLANORA</span>
                        </>
                      ) : (
                        title
                      )}
                    </h2>
                  </div>
                  {!login && <p className="fb-subtitle">{description}</p>}
                  <form key={mode} data-form="auth" data-mode={mode}>
                    {fields}
                    <div className="form-error" role="alert" />
                    <button type="submit" className="btn primary auth-submit">
                      {buttons[mode]}
                      {icon("arrow")}
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
                    <div className="auth-register auth-return">
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
                </div>
              </div>
            </section>
          </main>
        </div>
      </div>
      <footer className="auth-site-footer">
        <div className="auth-footer-inner">
          <div className="auth-footer-language">
            {icon("globe")}Tiếng Việt
            <LoginMotionControl />
          </div>
          {brand("/", "login-footer-brand")}
          <span className="login-footer-tagline">
            Plan smarter. Work realistically.
          </span>
        </div>
      </footer>
    </div>
  );
}
