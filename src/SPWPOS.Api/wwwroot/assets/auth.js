import {authView} from './auth-view.js';
import {$,state,esc,icon,field,toast} from './ui.js';
import {api,submit} from './api.js';
let currentMode='login',onLogin=()=>{};
export function authSetup(callback){onLogin=callback;}
document.addEventListener('click',e=>{
 const btn=e.target.closest('[data-toggle-pwd]');
 if(!btn)return;
 e.preventDefault();
 const wrap=btn.closest('.input-with-action');
 if(!wrap)return;
 const input=wrap.querySelector('input');
 if(!input)return;
 const isPwd=input.type==='password';
 input.type=isPwd?'text':'password';
 btn.innerHTML=icon(isPwd?'eyeOff':'eye');
 btn.setAttribute('aria-label',isPwd?'Ẩn mật khẩu':'Hiện mật khẩu');
});

export function renderAuth(mode='login'){
 currentMode=mode;
 const titles={
  login:['Đăng nhập vào PLANORA','Đăng nhập để tiếp tục kế hoạch làm việc của bạn.'],
  register:['Tạo tài khoản PLANORA','Điền thông tin và xác minh email để bắt đầu.'],
  verify:['Xác minh email','Nhập mã xác nhận 6 số được gửi đến hộp thư của bạn.'],
  forgot:['Tìm tài khoản của bạn','Nhập email đã đăng ký để nhận mã xác minh đặt lại mật khẩu.'],
  forgotOtp:['Xác minh mã OTP','Mã xác minh có hiệu lực trong vòng 5 phút.'],
  reset:['Đặt mật khẩu mới','Thiết lập mật khẩu an toàn mới cho tài khoản của bạn.']
 };
 const [title,description]=titles[mode]||titles.login;
 let fields='';
 if(mode==='register')fields+=`<div class="form-grid">${field('Tên','firstName','text','','required maxlength="100" autocomplete="given-name" placeholder="Tên của bạn"')}${field('Họ','lastName','text','','required maxlength="100" autocomplete="family-name" placeholder="Họ của bạn"')}</div>`;
 if(mode==='login')fields+=`<label class="field"><span class="auth-visually-hidden">Email</span><input type="email" name="email" value="${esc(state.authEmail)}" required maxlength="254" autocomplete="email" placeholder="Địa chỉ email"></label><label class="field password-field"><span class="auth-visually-hidden">Mật khẩu</span><div class="input-with-action"><input type="password" name="password" required maxlength="128" autocomplete="current-password" placeholder="Mật khẩu"><button type="button" class="btn-toggle-pwd" data-toggle-pwd aria-label="Hiện mật khẩu">${icon('eye')}</button></div></label>`;
 else if(mode!=='reset')fields+=field('Email','email','email',state.authEmail,'required maxlength="254" autocomplete="email" placeholder="ban@example.com"');
 if(mode==='register'||mode==='reset')fields+=field('Mật khẩu'+(mode==='reset'?' mới':''),'password','password','','required minlength="10" maxlength="128" autocomplete="new-password" placeholder="Mật khẩu (tối thiểu 10 ký tự)"')+field('Nhập lại mật khẩu','confirmPassword','password','','required minlength="10" maxlength="128" autocomplete="new-password" placeholder="Nhập lại mật khẩu"');
 if(mode==='verify'||mode==='forgotOtp')fields+=`<div class="auth-otp">${field('Mã OTP','otp','text','','required inputmode="numeric" pattern="[0-9]{6,8}" minlength="6" maxlength="8" autocomplete="one-time-code" placeholder="Mã xác thực"')}</div>`;
 const buttons={login:'Đăng nhập',register:'Tạo tài khoản',verify:'Xác minh email',forgot:'Gửi mã xác minh',forgotOtp:'Xác minh mã',reset:'Đặt mật khẩu mới'};
 
 $('#app').innerHTML=authView(mode,title,description,fields,buttons);
 const first=$('input',$('#app'));if(first)first.focus();
}
export async function handleAuthAction(action){
 if(!action.startsWith('auth-'))return false;
 const email=$('input[name="email"]');if(email)state.authEmail=email.value;
 if(action==='auth-resend'){try{await api('/api/auth/resend-verification-otp','POST',{email:state.authEmail});toast('Nếu tài khoản cần xác minh, mã mới sẽ được gửi.');}catch(e){toast(e.message,'error');}return true;}
 renderAuth(action.slice(5));return true;
}
export async function handleAuthSubmit(form){
 if(form.dataset.form!=='auth')return false;
 try{await submit(form,async data=>{
  const email=String(data.get('email')||state.authEmail);state.authEmail=email;
  const password=String(data.get('password')||'');
  switch(currentMode){
   case 'login':await api('/api/browser-session/login','POST',{email,password},false);await onLogin();break;
   case 'register':await api('/api/auth/register','POST',{email,password,confirmPassword:String(data.get('confirmPassword')),firstName:String(data.get('firstName')),lastName:String(data.get('lastName'))});renderAuth('verify');toast('Kiểm tra hộp thư để lấy mã xác minh.');break;
   case 'verify':await api('/api/auth/verify-email','POST',{email,otp:String(data.get('otp'))});renderAuth('login');toast('Email đã xác minh. Bạn có thể đăng nhập.','success');break;
   case 'forgot':await api('/api/auth/forgot-password','POST',{email});renderAuth('forgotOtp');toast('Nếu email có tài khoản, mã xác minh sẽ được gửi.');break;
   case 'forgotOtp':state.resetToken=(await api('/api/auth/verify-forgot-password-otp','POST',{email,otp:String(data.get('otp'))})).resetToken;renderAuth('reset');break;
   case 'reset':await api('/api/auth/reset-password','POST',{resetToken:state.resetToken,newPassword:password,confirmNewPassword:String(data.get('confirmPassword'))});state.resetToken=null;renderAuth('login');toast('Đã đặt lại mật khẩu. Hãy đăng nhập lại.','success');break;
  }
 });}catch(e){if(e.code==='EMAIL_NOT_VERIFIED'){renderAuth('verify');toast('Xác minh email trước khi đăng nhập.');}}
 return true;
}
