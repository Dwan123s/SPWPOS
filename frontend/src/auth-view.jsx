import { brand } from "./brand.jsx";
import { icon } from "./ui.jsx";
export function authView(mode, title, description, fields, buttons) {
  const login = mode === "login";
  return (
    <div
      className={`auth-layout auth-facebook ${login ? "is-login" : "is-account"}`}
    >
      {"\n "}
      <div className="auth-content">
        {"\n  "}
        <aside className="auth-story">
          {"\n   "}
          {brand("/")}
          {"\n   "}
          <div className="auth-story-hero">
            <h1>
              {"Khám phá "}
              <br />
              {"nhịp làm việc "}
              <br />
              <span>
                {"phù hợp "}
                <br />
                với bạn.
              </span>
            </h1>
            <img
              className="auth-hero-art"
              src="/assets/login-hero-planora.png"
              alt={``}
              width="1122"
              height="1402"
              decoding="async"
            />
          </div>
          {"\n  "}
        </aside>
        {"\n  "}
        <main className="auth-area">
          {"\n   "}
          <section className="auth-card" aria-labelledby="auth-title">
            {"\n    "}
            <div className="fb-header-row">
              {!login ? (
                <button
                  type="button"
                  className="fb-back-btn"
                  data-action="auth-login"
                  aria-label="Quay lại đăng nhập"
                >
                  {icon("back")}
                </button>
              ) : (
                ""
              )}
              <h2 id="auth-title">{title}</h2>
            </div>
            {"\n    "}
            {!login ? <p className="fb-subtitle">{description}</p> : ""}
            {"\n    "}
            <form data-form="auth" data-mode={mode}>
              {"\n     "}
              {fields}
              {"\n     "}
              <div className="form-error" role="alert" />
              {"\n     "}
              <button type="submit" className="btn primary auth-submit">
                {buttons[mode]}
              </button>
              {"\n    "}
            </form>
            {"\n    "}
            {login ? (
              <>
                <div className="auth-forgot">
                  <button
                    type="button"
                    className="text-btn"
                    data-action="auth-forgot"
                  >
                    Quên mật khẩu?
                  </button>
                </div>
                <div className="auth-register">
                  <button
                    type="button"
                    className="btn auth-create-account"
                    data-action="auth-register"
                  >
                    Tạo tài khoản mới
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
            {"\n    "}
            {mode === "verify" ? (
              <p className="auth-resend">
                {"Chưa nhận được mã? "}
                <button
                  type="button"
                  className="text-btn"
                  data-action="auth-resend"
                >
                  Gửi lại mã
                </button>
              </p>
            ) : (
              ""
            )}
            {"\n   "}
          </section>
          {"\n   "}
          <div className="auth-card-caption">
            <img
              src="/assets/planora-mark.png"
              width="32"
              height="32"
              alt={``}
            />
            <span>PLANORA</span>
          </div>
          {"\n  "}
        </main>
        {"\n "}
      </div>
      {"\n "}
      <footer className="auth-site-footer">
        <div className="auth-footer-inner">
          <div className="auth-footer-language">Tiếng Việt</div>
          <div className="auth-footer-details">
            <span>PLANORA · Personal Workspace</span>
            <span>Plan smarter. Work realistically.</span>
          </div>
        </div>
      </footer>
      {"\n "}
    </div>
  );
}
