import {brand} from './brand.js';
import {$,state,esc,icon,button,userAvatar,dateFmt,modal} from './ui.js';

export function workspaceShell(routes,title,description,body){
 const p=state.workspace.profile||{},unread=(state.workspace.notifications||[]).filter(n=>!n.isRead).length;
 const nav=keys=>keys.filter(key=>key!=='admin'||state.roles.includes('Admin')).map(key=>`<button type="button" class="${state.route===key?'active':''}" data-action="nav" data-id="${key}" ${state.route===key?'aria-current="page"':''}>${icon(routes[key][2])}<span>${routes[key][0]}</span>${key==='notifications'&&unread?`<span class="notification-number">${unread}</span>`:''}</button>`).join('');
 const actions=state.route==='tasks'?button('Ghi nhanh','capture','secondary','','spark')+button('Thêm công việc','new-task','primary','','plus'):state.route==='today'?button('Thêm công việc','new-task','primary','','plus'):state.route==='calendar'?button('Lập kế hoạch','generate-plan','primary','','spark'):'';
 const headline=state.route==='today'?`Một ngày có chủ đích${p.firstName?`, ${esc(p.firstName)}`:''}.`:title;
 return `<a class="skip-link" href="#main-content">Đến nội dung chính</a><div class="shell glass-workspace"><button type="button" class="sidebar-overlay" data-action="close-menu" aria-label="Đóng menu" tabindex="-1" hidden></button><aside class="sidebar" id="workspace-menu"><div class="sidebar-brand-header">${brand('#today','','PLANORA — Hôm nay')}<button type="button" class="btn ghost icon-only sidebar-close" data-action="close-menu" aria-label="Đóng menu">${icon('close')}</button></div><div class="workspace-label">${icon('folder')}<span>Không gian cá nhân</span><span class="workspace-dot"></span></div><button type="button" class="quick-search" data-action="search">${icon('search')}<span>Tìm công việc</span><kbd>Ctrl K</kbd></button><nav class="nav" aria-label="Điều hướng chính"><div class="nav-label">LÀM VIỆC</div>${nav(['today','tasks','calendar'])}<div class="nav-label">LÊN KẾ HOẠCH</div>${nav(['time','insights','scenario'])}<div class="nav-label">QUẢN LÝ</div>${nav(['notifications','settings','admin'])}</nav><div class="sidebar-bottom"><div class="side-note"><span class="side-note-icon">${icon('spark')}</span><strong>Dành chỗ cho việc quan trọng</strong><p>Một kế hoạch vừa sức bắt đầu từ thời gian bạn có.</p>${button('Xem giờ rảnh','nav','small ghost','time','arrow')}</div><div class="user-mini">${userAvatar(p)}<button class="user-link" type="button" data-action="nav" data-id="settings"><strong>${esc(p.displayName||`${p.firstName||''} ${p.lastName||''}`.trim()||'Tài khoản')}</strong><small>${state.roles.includes('Admin')?'Quản trị viên':'Không gian của bạn'}</small></button>${button('','logout','ghost icon-only','','logout')}</div></div></aside><div class="main"><header class="topbar"><div class="actions"><button type="button" class="btn ghost icon-only mobile-menu" data-action="mobile-menu" aria-controls="workspace-menu" aria-expanded="false" aria-label="Mở menu">${icon('menu')}</button><div class="breadcrumb">Không gian cá nhân<span>/</span><strong>${title}</strong></div></div><div class="topbar-right"><span class="top-date">${dateFmt(Date.now(),{weekday:'long',year:'numeric'})}</span><button type="button" class="bell" data-action="nav" data-id="notifications" aria-label="Thông báo${unread?`, ${unread} chưa đọc`:''}">${icon('bell')}${unread?'<span class="dot"></span>':''}</button><button type="button" class="avatar-button" data-action="nav" data-id="settings" aria-label="Tài khoản">${userAvatar(p)}</button></div></header><main class="content" id="main-content" tabindex="-1"><div class="page-header"><div><div class="eyebrow">${state.route==='today'?dateFmt(Date.now(),{weekday:'long',month:'long'}):'KHÔNG GIAN CỦA BẠN'}</div><h1>${headline}</h1><p>${state.route==='today'?'Nhìn rõ việc cần làm. Dành thời gian để hoàn thành.':description}</p></div><div class="actions">${actions}</div></div>${state.analysisError?`<div class="warning analysis-warning" role="status"><div><strong>Phân tích chưa sẵn sàng</strong><p>Chưa tải được đầy đủ phân tích hoặc kiểm tra xung đột. Không thể xác nhận lịch đang an toàn.</p></div>${button('Thử lại','reload-workspace','small secondary','','refresh')}</div>`:''}${body}<footer class="workspace-footer"><span>PLANORA · Làm việc theo nhịp của bạn.</span><span>${esc(state.workspace.preference.timeZoneId)}</span></footer></main></div></div>`;
}

export function setMobileMenu(open=false){
 const sidebar=$('.sidebar');if(!sidebar)return;
 const mobile=window.matchMedia('(max-width: 800px)').matches,hadFocus=sidebar.contains(document.activeElement),wasOpen=sidebar.classList.contains('open');
 if(!(mobile&&open)&&$('.main'))$('.main').inert=false;
 sidebar.classList.toggle('open',mobile&&open);sidebar.inert=mobile&&!open;
 const overlay=$('.sidebar-overlay');if(overlay)overlay.hidden=!(mobile&&open);
 document.body.classList.toggle('menu-open',mobile&&open);
 $('.mobile-menu')?.setAttribute('aria-expanded',String(mobile&&open));
 if(mobile&&open){$('.main').inert=true;sidebar.querySelector('a,button')?.focus();}
 else if(mobile&&(hadFocus||wasOpen)){queueMicrotask(()=>$('.mobile-menu')?.focus());}
}
export function syncDecorations(){
 document.querySelectorAll('[data-color]').forEach(el=>{if(/^#[0-9a-f]{6}$/i.test(el.dataset.color))el.style.backgroundColor=el.dataset.color;});
}
export function openSearch(){modal('Tìm trong không gian của bạn','Tìm theo tên hoặc nội dung công việc.',`<label class="field">Tìm công việc<input type="search" id="workspace-search" placeholder="Nhập để tìm kiếm…" autocomplete="off"></label><div id="search-results" class="search-results" aria-live="polite"></div>`);filterSearch();$('#workspace-search').focus();}
export function filterSearch(){
 const query=$('#workspace-search')?.value.trim().toLocaleLowerCase('vi')||'';
 const results=state.workspace.tasks.filter(t=>!query||`${t.name} ${t.description||''}`.toLocaleLowerCase('vi').includes(query));
 $('#search-results').innerHTML=results.slice(0,20).map(t=>`<button type="button" class="search-result" data-action="task-detail" data-id="${esc(t.id)}">${icon('tasks')}<span><strong>${esc(t.name)}</strong><small>Deadline ${dateFmt(t.deadline)} · ${t.progress}% hoàn thành</small></span>${icon('arrow')}</button>`).join('')||'<p class="search-empty">Không tìm thấy công việc phù hợp.</p>';
 if(results.length>20)$('#search-results').insertAdjacentHTML('beforeend',`<p class="reason">Đang hiển thị 20/${results.length} kết quả. Nhập thêm để thu hẹp tìm kiếm.</p>`);
}
window.addEventListener('resize',()=>{if(!window.matchMedia('(max-width: 800px)').matches)setMobileMenu(false);else if(!$('.sidebar')?.classList.contains('open'))setMobileMenu(false);});
document.addEventListener('keydown',event=>{
 if(event.target.matches('.clickable-avatar')&&['Enter',' '].includes(event.key)){event.preventDefault();event.target.querySelector('input[type="file"]')?.click();}
 if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='k'&&state.workspace){event.preventDefault();setMobileMenu(false);openSearch();}
 if(event.key==='Escape'&&$('.sidebar.open')){event.preventDefault();setMobileMenu(false);}
 if(event.key==='Tab'&&$('.sidebar.open')&&!$('#modal').open){const controls=[...$('.sidebar').querySelectorAll('a,button:not(:disabled)')],first=controls[0],last=controls.at(-1);if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}}
});
