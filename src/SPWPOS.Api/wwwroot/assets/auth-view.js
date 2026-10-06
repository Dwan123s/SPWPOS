import {brand} from './brand.js';
import {esc,icon} from './ui.js';

export function authView(mode,title,description,fields,buttons){
 const login=mode==='login';
 return `<div class="auth-layout auth-facebook ${login?'is-login':'is-account'}">
 <div class="auth-content">
  <aside class="auth-story">
   ${brand('/')}
   <div class="auth-story-hero"><h1>Khám phá <br>nhịp làm việc <br><span>phù hợp <br>với bạn.</span></h1><img class="auth-hero-art" src="/assets/login-hero-planora.png" alt="" width="1122" height="1402" decoding="async"></div>
  </aside>
  <main class="auth-area">
   <section class="auth-card" aria-labelledby="auth-title">
    <div class="fb-header-row">${!login?`<button type="button" class="fb-back-btn" data-action="auth-login" aria-label="Quay lại đăng nhập">${icon('back')}</button>`:''}<h2 id="auth-title">${esc(title)}</h2></div>
    ${!login?`<p class="fb-subtitle">${esc(description)}</p>`:''}
    <form data-form="auth" data-mode="${esc(mode)}">
     ${fields}
     <div class="form-error" role="alert"></div>
     <button type="submit" class="btn primary auth-submit">${esc(buttons[mode])}</button>
    </form>
    ${login?`<div class="auth-forgot"><button type="button" class="text-btn" data-action="auth-forgot">Quên mật khẩu?</button></div><div class="auth-register"><button type="button" class="btn auth-create-account" data-action="auth-register">Tạo tài khoản mới</button></div>`:`<div class="auth-register"><button type="button" class="text-btn" data-action="auth-login">Quay lại đăng nhập</button></div>`}
    ${mode==='verify'?'<p class="auth-resend">Chưa nhận được mã? <button type="button" class="text-btn" data-action="auth-resend">Gửi lại mã</button></p>':''}
   </section>
   <div class="auth-card-caption"><img src="/assets/planora-mark.png" width="32" height="32" alt=""><span>PLANORA</span></div>
  </main>
 </div>
 <footer class="auth-site-footer"><div class="auth-footer-inner"><div class="auth-footer-language">Tiếng Việt</div><div class="auth-footer-details"><span>PLANORA · Personal Workspace</span><span>Plan smarter. Work realistically.</span></div></div></footer>
 </div>`;
}
