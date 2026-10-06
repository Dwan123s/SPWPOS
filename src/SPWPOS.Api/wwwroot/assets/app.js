import {workspaceShell,setMobileMenu,openSearch,filterSearch,syncDecorations} from './shell.js';
import {$,state,esc,icon,button,badge,userAvatar,field,modal,closeModal,toast,dateFmt,timeFmt,minutes,reasons,dayKey,localInput,toUtc,minuteValue} from './ui.js';
import {api,submit} from './api.js';
import {authSetup,renderAuth,handleAuthAction,handleAuthSubmit} from './auth.js';
import {tasksPage,filterTasks,taskById,openTask,openDetail,taskInput,taskForm,captureModal,breakdownModal} from './tasks.js';
import {calendarPage,timePage,sessionById,sessionDetail,eventModal,exceptionModal,availabilityRow,planModal} from './calendar.js';
import {todayPage,insightsPage,scenarioPage,notificationsPage,settingsPage,categoryModal,adminPage,adminUsers,adminStatus} from './pages.js';
const routes={today:['Hôm nay','Một góc nhìn rõ ràng về công việc và thời gian của bạn.','today'],tasks:['Công việc','Quản lý điều cần làm, deadline và những bước thực hiện.','tasks'],calendar:['Lịch làm việc','Dành những khoảng thời gian cụ thể cho công việc.','calendar'],time:['Thời gian & tùy chọn','Thiết lập lịch rảnh và cách bạn muốn làm việc.','clock'],insights:['Phân tích cá nhân','Hiểu cách bạn ước lượng và thực hiện công việc.','insights'],scenario:['Thử kịch bản','Xem điều gì thay đổi trước khi nhận thêm việc.','scenario'],notifications:['Thông báo','Những cập nhật cần chú ý trong không gian của bạn.','bell'],settings:['Tài khoản & nhóm','Thông tin cá nhân, nhóm công việc và bảo mật.','settings'],admin:['Quản trị','Trạng thái hệ thống và quản lý tài khoản.','users']};
let loading=false,working=false;
function navigation(key){location.hash=key;if(state.route===key)render();}
function expiry(){if(!state.workspace)return;state.workspace=null;state.insights=null;state.user=null;state.roles=[];closeModal();renderAuth();toast('Phiên đăng nhập đã kết thúc. Hãy đăng nhập lại.');}
async function load(renderPage=true){
 if(loading)return;loading=true;
 try{
  state.workspace=await api('/api/workspace');state.loadedAt=Date.now();
  const input={start:new Date().toISOString(),horizonDays:7};
  const [insights,conflicts]=await Promise.allSettled([api('/api/intelligence','POST',input),api('/api/plans/conflicts','POST',input)]);
  state.insights=insights.status==='fulfilled'?insights.value:null;
  state.conflicts=conflicts.status==='fulfilled'?conflicts.value:null;
  state.analysisError=insights.status==='rejected'||conflicts.status==='rejected';
  if(renderPage)render();
 }finally{loading=false;}
}
async function loggedIn(){const data=await api('/api/browser-session/me');state.user=data.user;state.roles=data.roles||[];await load();}
function render(){
 if(!state.workspace)return;
 state.route=location.hash.slice(1)||'today';
 if(!routes[state.route]||(state.route==='admin'&&!state.roles.includes('Admin')))state.route='today';
 const [title,description]=routes[state.route];
 const renderers={today:todayPage,tasks:tasksPage,calendar:calendarPage,time:timePage,insights:insightsPage,scenario:scenarioPage,notifications:notificationsPage,settings:settingsPage,admin:adminPage};
 $('#app').innerHTML=workspaceShell(routes,title,description,renderers[state.route]());
 setMobileMenu(false);syncDecorations();
 if(state.route==='scenario'){const input=$('input[name="deadline"]');if(input&&!input.value)input.value=localInput(Date.now()+7*86400000);}
 if(state.route==='admin')loadAdmin().catch(e=>toast(e.message,'error'));tick();
}
async function loadAdmin(search=''){const [users,status,jobs]=await Promise.all([api(`/api/admin/users?search=${encodeURIComponent(search)}`),api('/api/admin/status'),api('/api/admin/jobs')]);if($('#admin-jobs'))$('#admin-jobs').innerHTML=`<table><thead><tr><th>Job</th><th>Kết quả</th><th>Bắt đầu</th><th>Chi tiết</th></tr></thead><tbody>${jobs.map(j=>`<tr><td>${esc(j.jobType)}</td><td>${badge(j.status,j.status==='FAILED'?'red':'green')}</td><td>${dateFmt(j.startedAt)} ${timeFmt(j.startedAt)}</td><td>${esc(j.errorMessage||'—')}</td></tr>`).join('')}</tbody></table>`;if($('#admin-users'))$('#admin-users').innerHTML=adminUsers(users);if($('#system-status'))$('#system-status').innerHTML=adminStatus(status);}
function tick(){if(!state.workspace)return;for(const el of document.querySelectorAll('[data-timer]')){const s=sessionById(el.dataset.timer);if(!s)continue;const seconds=Math.max(0,Math.floor(s.actualSeconds+(s.status==='Running'?(Date.now()-state.loadedAt)/1000:0)));el.textContent=`${String(Math.floor(seconds/3600)).padStart(2,'0')}:${String(Math.floor(seconds/60)%60).padStart(2,'0')}:${String(seconds%60).padStart(2,'0')}`;}}
function planOptions(affected=[]){state.affected=affected;modal(affected.length?'Lập lại lịch liên quan':'Tạo kế hoạch mới','Bạn sẽ được xem lại đề xuất trước khi áp dụng.',`<form data-form="plan"><div class="form-grid">${field('Bắt đầu','start','datetime-local',localInput(Date.now()),'required')}${field('Số ngày','horizonDays','number',state.workspace.preference.planningHorizonDays||14,'required min="1" max="30"')}</div><p class="reason spaced">${affected.length?'Giữ nguyên phiên không bị ảnh hưởng, phiên đã khóa và lịch sử thực hiện.':'Giữ nguyên phiên đã khóa hoặc đang chạy. Các phiên chưa khóa trong khoảng này sẽ được lập lại.'}</p><div class="form-error"></div><div class="form-actions"><button class="btn primary" type="submit">Tạo đề xuất</button></div></form>`);}
function confirmAction(title,description,operation){state.confirm=operation;modal(title,description,`<div class="form-actions">${button('Hủy','close-modal','ghost')}${button('Xác nhận','confirm-action','danger')}</div>`);}
async function refreshAfter(message='Đã lưu.'){closeModal();await load();toast(message,'success');}
async function action(name,id,element){if(await handleAuthAction(name))return;
 switch(name){
 case 'nav':navigation(id);break;
 case 'reload-workspace':await load();break;
 case 'settings-tab':state.settingsTab=id;render();break;
 case 'mobile-menu':setMobileMenu(!$('.sidebar')?.classList.contains('open'));break;
 case 'close-menu':setMobileMenu(false);break;
 case 'search':setMobileMenu(false);openSearch();break;
 case 'task-scope':state.taskScope=id;filterTasks();document.querySelectorAll('[data-action="task-scope"]').forEach(b=>{b.classList.toggle('active',b.dataset.id===id);b.setAttribute('aria-pressed',String(b.dataset.id===id));});break;
 case 'calendar-view':state.calendarView=id;render();break;
 case 'close-modal':closeModal();break;
 case 'logout':await api('/api/browser-session/logout','POST');expiry();break;
 case 'logout-all':confirmAction('Đăng xuất tất cả thiết bị?','Tất cả phiên sẽ kết thúc ngay.',async()=>{await api('/api/auth/logout-all','POST');expiry();});break;
 case 'new-task':openTask();break;
 case 'subtask':openTask({},id);break;
 case 'task-detail':openDetail(id);break;
 case 'edit-task':openTask(taskById(id));break;
 case 'delete-task':confirmAction('Xóa công việc?','Việc con và các phiên chưa thực hiện cũng sẽ được hủy.',async()=>{await api(`/api/tasks/${id}`,'DELETE');await refreshAfter('Đã xóa công việc.');});break;
 case 'remove-dependency':{const [task,predecessor]=id.split('|');await api(`/api/tasks/${task}/dependencies/${predecessor}`,'DELETE');await load();openDetail(task);break;}
 case 'capture':captureModal();break;
 case 'breakdown':await breakdownModal(id);break;
 case 'ai-breakdown':if(!$('#breakdown-ai')?.checked)throw new Error('Chọn cho phép gửi nội dung trước khi sử dụng AI.');{const result=await api(`/api/tasks/${id}/breakdown?useAi=true`,'POST');state.breakdown=result;modal('Gợi ý chia nhỏ',`Nguồn: ${result.source==='OpenAI'?'AI':'quy tắc (AI chưa khả dụng)'}`,`<form data-form="breakdown" data-id="${id}">${result.subtasks.map((s,n)=>`<label class="list-line checkbox"><input type="checkbox" name="part" value="${n}" checked>${esc(s.name)} · ${s.estimatedMinutes}p</label>`).join('')}<div class="form-error"></div><div class="form-actions"><button class="btn primary" type="submit">Lưu các việc con đã chọn</button></div></form>`);}break;
 case 'new-category':categoryModal();break;
 case 'edit-category':categoryModal(state.workspace.categories.find(c=>c.id===id));break;
 case 'delete-category':confirmAction('Xóa nhóm?','Công việc trong nhóm sẽ chuyển thành chưa phân nhóm.',async()=>{await api(`/api/categories/${id}`,'DELETE');await refreshAfter();});break;
 case 'new-event':eventModal();break;
 case 'edit-event':eventModal(state.workspace.events.find(e=>e.id===id));break;
 case 'delete-event':confirmAction('Xóa sự kiện?','Khoảng này sẽ trở lại khả dụng khi lập lịch.',async()=>{await api(`/api/time/events/${id}`,'DELETE');await refreshAfter();});break;
 case 'new-exception':exceptionModal();break;
 case 'edit-exception':exceptionModal(state.workspace.exceptions.find(e=>e.id===id));break;
 case 'delete-exception':await api(`/api/time/exceptions/${id}`,'DELETE');await refreshAfter();break;
 case 'add-slot':$('#availability-rows').insertAdjacentHTML('beforeend',availabilityRow());break;
 case 'remove-slot':element.closest('.availability-row').remove();break;
 case 'week-prev':case 'week-next':{const d=new Date(`${state.calendarStart}T12:00:00Z`);d.setUTCDate(d.getUTCDate()+(name==='week-prev'?-7:7));state.calendarStart=d.toISOString().slice(0,10);render();break;}
 case 'week-today':state.calendarStart=dayKey(Date.now());render();break;
 case 'generate-plan':planOptions();break;
 case 'replan-conflicts':planOptions([...new Set(state.conflicts.map(c=>c.taskId))]);break;
 case 'replan-task':planOptions([id]);break;
 case 'apply-plan':await api(`/api/plans/${id}/apply`,'POST');await refreshAfter('Đã áp dụng kế hoạch.');navigation('calendar');break;
 case 'session-detail':sessionDetail(id);break;
 case 'session-lock':case 'session-unlock':await api(`/api/sessions/${id}/${name.slice(8)}`,'POST');await load();sessionDetail(id);break;
 case 'session-start':case 'session-pause':case 'session-resume':case 'session-skip':case 'session-complete':{const s=sessionById(id);await api(`/api/sessions/${id}/${name.slice(8)}`,'POST');await refreshAfter(name==='session-complete'?'Đã ghi nhận phiên. Cập nhật tiến độ công việc để kế hoạch tiếp theo chính xác.':'Đã cập nhật phiên.');if(name==='session-complete')openDetail(s.taskId);break;}
 case 'discard-plan':await api(`/api/plans/${id}/discard`,'POST');closeModal();toast('Đã bỏ đề xuất.');break;
 case 'plan-decisions':{const items=await api(`/api/plans/${id}/items`);modal('Các quyết định trong đề xuất','Lịch hiện tại chỉ thay đổi khi bạn áp dụng.',`<div class="table-scroll"><table><thead><tr><th>Công việc</th><th>Quyết định</th><th>Thời lượng</th><th>Lý do</th></tr></thead><tbody>${items.map(x=>`<tr><td>${esc(taskById(x.taskId)?.name||'Công việc')}</td><td>${esc({ADD:'Thêm',KEEP:'Giữ',MOVE:'Dời',RESIZE:'Đổi thời lượng',REMOVE:'Bỏ',UNSCHEDULED:'Chưa xếp'}[x.action]||x.action)}</td><td>${x.plannedMinutes??'—'}</td><td>${x.reasonCodes.map(c=>esc(reasons[c]||c)).join(' · ')}</td></tr>`).join('')}</tbody></table></div>${button('Quay lại đề xuất','review-plan','secondary',id)}`);}break;
 case 'review-plan':planModal(await api(`/api/plans/${id}`));break;
 case 'execution-history':{const x=await api(`/api/sessions/${id}/execution`);modal('Lịch sử thực hiện','Timer giữ nguyên khi bạn chỉnh thời lượng thực tế.',x?`<p>Timer: ${minutes(Math.floor(x.timerSeconds/60))} · Thực tế: ${minutes(Math.floor(x.actualSeconds/60))}${x.isUserAdjusted?' · Đã chỉnh sửa':''}</p>${x.segments.map(s=>`<div class="list-line">${dateFmt(s.start)} ${timeFmt(s.start)} → ${s.end?timeFmt(s.end):'Đang chạy'}</div>`).join('')}`:'Chưa bắt đầu phiên.');}break;
 case 'notification-read':await api(`/api/notifications/${id}/read`,'POST');await load();break;
 case 'change-otp':await api('/api/auth/change-password/request-otp','POST');toast('Mã xác minh đã được gửi đến email của bạn.');break;
 case 'scenario-plan':{const p=state.scenario.proposedPlan;planModal(p);$('#modal-content [data-action="apply-plan"]').remove();break;}
 case 'scenario-create':openTask(state.scenarioTask);break;
 case 'admin-account-history':{const logs=await api(`/api/admin/users/${id}/status-logs`);modal('Lịch sử trạng thái tài khoản','Các thao tác quản trị được lưu trong database.',logs.map(l=>`<div class="list-line"><div><strong>${l.action==='LOCK'?'Khóa':'Mở khóa'}</strong><p>${esc(l.reason||'Không có ghi chú')}</p><small>${dateFmt(l.createdAt)} ${timeFmt(l.createdAt)}</small></div></div>`).join('')||'Chưa có thao tác.');}break;
 case 'remove-avatar':{const hidden=$('#avatarUrl-hidden');if(hidden)hidden.value='';const initials=(state.workspace?.profile?.displayName||state.workspace?.profile?.firstName||'U')[0].toUpperCase();const heroBox=$('.profile-hero-avatar')||$('.profile-hero-avatar-box');if(heroBox){const imgEl=heroBox.querySelector('img');if(imgEl)imgEl.remove();let ph=heroBox.querySelector('.avatar-initials')||heroBox.querySelector('.profile-hero-avatar-placeholder');if(!ph){ph=document.createElement('span');ph.className='avatar-initials';ph.id='hero-avatar-placeholder';ph.textContent=initials;heroBox.prepend(ph);}}element.remove();toast('Đã xóa ảnh đại diện. Bấm "Lưu thông tin hồ sơ" để hoàn tất.','info');break;}
 case 'admin-refresh':await loadAdmin();break;
 case 'admin-lock':case 'admin-unlock':confirmAction(name==='admin-lock'?'Khóa tài khoản?':'Mở khóa tài khoản?',name==='admin-lock'?'Phiên đăng nhập của tài khoản này sẽ bị thu hồi.':'Cho phép tài khoản hoạt động lại.',async()=>{await api(`/api/admin/users/${id}/${name.slice(6)}`,'POST');closeModal();await loadAdmin();toast('Đã cập nhật tài khoản.');});break;
 case 'confirm-action':{const op=state.confirm;state.confirm=null;closeModal();if(op)await op();break;}
 }
}
async function formAction(form,data){const id=form.dataset.id;switch(form.dataset.form){
 case 'task':await api(`/api/tasks${id?`/${id}`:''}`,id?'PUT':'POST',taskInput(data));await refreshAfter('Đã lưu công việc.');break;
 case 'task-progress':await api(`/api/tasks/${id}/status`,'PATCH',{status:Number(data.get('progress'))===100&&data.get('status')!=='Cancelled'?'Completed':String(data.get('status')),progress:Number(data.get('progress'))});await refreshAfter('Đã cập nhật tiến độ.');break;
 case 'dependency':if(!data.get('predecessor'))throw new Error('Chọn một công việc tiền nhiệm.');await api(`/api/tasks/${id}/dependencies/${data.get('predecessor')}`,'POST');await load();openDetail(id);break;
 case 'capture':{const result=await api('/api/tasks/capture','POST',{text:String(data.get('text')),useAi:data.has('useAi')});modal('Kiểm tra công việc gợi ý',`Nguồn: ${result.source==='OpenAI'?'AI':'quy tắc tiếng Việt/Anh'}`,`<div class="info section-gap">${result.notes.map(esc).join('<br>')}</div>${taskForm(result.task)}`);break;}
 case 'breakdown':{const selected=data.getAll('part');if(!selected.length)throw new Error('Chọn ít nhất một việc con.');let saved=0;try{for(const index of selected){await api('/api/tasks','POST',{...state.breakdown.subtasks[Number(index)],parentId:id,categoryId:taskById(id).categoryId,creationSource:'AI_BREAKDOWN'});saved++;const checkbox=form.querySelector(`input[value="${index}"]`);checkbox.checked=false;checkbox.disabled=true;}}catch(e){await load(false);throw new Error(`Đã lưu ${saved} việc con. ${e.message} Bạn có thể thử lại các mục chưa lưu.`);}await refreshAfter(`Đã thêm ${saved} công việc con.`);break;}
 case 'category':await api(`/api/categories${id?`/${id}`:''}`,id?'PUT':'POST',{name:String(data.get('name')),color:String(data.get('color'))});await refreshAfter();break;
 case 'availability':{const slots=[...form.querySelectorAll('.availability-row')].map(row=>({dayOfWeek:Number(row.querySelector('[name="day"]').value),startMinute:minuteValue(row.querySelector('[name="start"]').value),endMinute:minuteValue(row.querySelector('[name="end"]').value)}));await api('/api/time/availability','PUT',{slots});await refreshAfter('Đã lưu thời gian rảnh.');break;}
 case 'preferences':await api('/api/time/preferences','PUT',{timeZoneId:String(data.get('timeZoneId')),minimumSessionMinutes:Number(data.get('minimumSessionMinutes')),maximumSessionMinutes:Number(data.get('maximumSessionMinutes')),breakMinutes:Number(data.get('breakMinutes')),preferredStartMinute:minuteValue(String(data.get('preferredStart'))),preferredEndMinute:minuteValue(String(data.get('preferredEnd'))),planningHorizonDays:Number(data.get('planningHorizonDays')),preferredPeriod:String(data.get('preferredPeriod'))});await refreshAfter();break;
 case 'exception':await api(`/api/time/exceptions${id?`/${id}`:''}`,id?'PUT':'POST',{date:String(data.get('date')),startMinute:minuteValue(String(data.get('start'))),endMinute:minuteValue(String(data.get('end'))),isAvailable:data.get('isAvailable')==='true',note:String(data.get('note'))});await refreshAfter();break;
 case 'event':await api(`/api/time/events${id?`/${id}`:''}`,id?'PUT':'POST',{name:String(data.get('name')),start:toUtc(String(data.get('start'))),end:toUtc(String(data.get('end'))),description:String(data.get('description')||''),eventType:String(data.get('eventType')||'OTHER')});await refreshAfter();break;
 case 'plan':planModal(await api(state.affected?.length?'/api/plans/reschedule':'/api/plans','POST',{start:toUtc(String(data.get('start'))),horizonDays:Number(data.get('horizonDays')),affectedTaskIds:state.affected?.length?state.affected:null}));break;
 case 'session-edit':await api(`/api/sessions/${id}`,'PUT',{start:toUtc(String(data.get('start'))),end:toUtc(String(data.get('end')))});await refreshAfter();break;
 case 'session-split':await api(`/api/sessions/${id}/split`,'POST',{firstMinutes:Number(data.get('firstMinutes'))});await refreshAfter();break;
 case 'actual-duration':await api(`/api/sessions/${id}/actual-duration`,'PUT',{actualMinutes:Number(data.get('actualMinutes'))});await refreshAfter();break;
 case 'notification-settings':await api('/api/notifications/settings','PUT',{inAppEnabled:data.has('inAppEnabled'),deadlineLeadHours:Number(data.get('deadlineLeadHours')),sessionReminderLeadMinutes:Number(data.get('sessionReminderLeadMinutes')),workloadWarningLevel:String(data.get('workloadWarningLevel'))});await refreshAfter();break;
 case 'profile':await api('/api/workspace/profile','PUT',{firstName:String(data.get('firstName')),lastName:String(data.get('lastName')),displayName:String(data.get('displayName')),bio:String(data.get('bio')||''),avatarUrl:String(data.get('avatarUrl')||'')});await refreshAfter('Đã cập nhật thông tin hồ sơ.');break;
 case 'password':await api('/api/auth/change-password','POST',Object.fromEntries(data));await api('/api/browser-session/logout','POST');expiry();toast('Mật khẩu đã đổi. Hãy đăng nhập lại.','success');break;
 case 'scenario':{state.scenarioTask={...taskInput(data),splittable:true,minimumSessionMinutes:25};state.scenario=await api('/api/plans/scenario','POST',{plan:{start:new Date().toISOString(),horizonDays:7},newTask:state.scenarioTask});render();break;}
 case 'admin-search':await loadAdmin(String(data.get('search')));break;
 }}
document.addEventListener('click',async event=>{const el=event.target.closest('[data-action]');if(!el||working)return;working=true;el.disabled=true;try{await action(el.dataset.action,el.dataset.id,el);}catch(e){toast(e.message,'error');}finally{working=false;if(el.isConnected)el.disabled=false;}});
document.addEventListener('submit',async event=>{const form=event.target;if(!form.dataset.form)return;event.preventDefault();if(working)return;working=true;try{if(!await handleAuthSubmit(form))await submit(form,data=>formAction(form,data));}catch{/* submit renders the error beside the form. */}finally{working=false;}});
document.addEventListener('input',event=>{if(event.target.id==='task-search')filterTasks();if(event.target.id==='workspace-search')filterSearch();});
function openAvatarCropper(imgSrc){
 const img=new Image();
 img.onload=()=>{
  const W=320,H=320,R=120,CX=160,CY=160;
  const minScale=Math.max((R*2)/img.width,(R*2)/img.height);
  let scale=minScale,offsetX=0,offsetY=0,rotation=0,isDragging=false,lastX=0,lastY=0;
  const bodyHtml=`<div class="avatar-cropper-wrap"><p class="avatar-cropper-hint">Kéo ảnh để căn vị trí · Dùng thanh trượt hoặc cuộn chuột để phóng to/thu nhỏ</p><div class="avatar-cropper-canvas-box" id="cropper-box"><canvas id="cropper-canvas" width="${W}" height="${H}"></canvas></div><div class="avatar-cropper-controls"><label>Thu phóng</label><input type="range" class="avatar-cropper-zoom-slider" id="cropper-zoom" min="${(minScale*0.8).toFixed(3)}" max="${(minScale*4).toFixed(3)}" step="0.005" value="${scale.toFixed(3)}"></div><div class="avatar-cropper-tools-row"><div class="avatar-crop-preview-wrap"><canvas id="cropper-preview-circle" class="avatar-crop-preview-circle" width="60" height="60"></canvas><small>Xem trước</small></div><div class="actions"><button type="button" class="btn ghost small" id="cropper-rotate-btn">${icon('refresh')} Xoay 90°</button><button type="button" class="btn ghost small" id="cropper-reset-btn">Đặt lại</button></div></div><div class="form-actions right-align" style="width:100%;margin-top:10px">${button('Hủy','close-modal','ghost')}<button type="button" class="btn primary" id="cropper-apply-btn">${icon('check')} Áp dụng khung ảnh</button></div></div>`;
  modal('Căn chỉnh ảnh đại diện','Kéo và chọn khung ảnh phù hợp nhất cho bạn.',bodyHtml);
  const canvas=$('#cropper-canvas'),box=$('#cropper-box'),zoomSlider=$('#cropper-zoom'),previewCanvas=$('#cropper-preview-circle');
  if(!canvas||!box)return;
  const ctx=canvas.getContext('2d'),previewCtx=previewCanvas?previewCanvas.getContext('2d'):null;
  function clamp(){
   const isRot=(rotation%180!==0);
   const natW=isRot?img.height:img.width,natH=isRot?img.width:img.height;
   const curMinScale=Math.max((R*2)/natW,(R*2)/natH);
   if(scale<curMinScale)scale=curMinScale;
   const curW=natW*scale,curH=natH*scale;
   const maxOffX=Math.max(0,(curW-R*2)/2),maxOffY=Math.max(0,(curH-R*2)/2);
   offsetX=Math.max(-maxOffX,Math.min(maxOffX,offsetX));
   offsetY=Math.max(-maxOffY,Math.min(maxOffY,offsetY));
  }
  function renderCrop(){
   clamp();
   ctx.clearRect(0,0,W,H);
   ctx.save();
   ctx.translate(CX+offsetX,CY+offsetY);
   ctx.rotate((rotation*Math.PI)/180);
   ctx.scale(scale,scale);
   ctx.drawImage(img,-img.width/2,-img.height/2);
   ctx.restore();
   ctx.save();
   ctx.fillStyle='rgba(15, 23, 42, 0.72)';
   ctx.beginPath();
   ctx.rect(0,0,W,H);
   ctx.arc(CX,CY,R,0,Math.PI*2,true);
   ctx.fill();
   ctx.strokeStyle='#6366f1';
   ctx.lineWidth=2.5;
   ctx.beginPath();
   ctx.arc(CX,CY,R,0,Math.PI*2);
   ctx.stroke();
   ctx.save();
   ctx.beginPath();
   ctx.arc(CX,CY,R,0,Math.PI*2);
   ctx.clip();
   ctx.strokeStyle='rgba(255, 255, 255, 0.28)';
   ctx.lineWidth=1;
   ctx.setLineDash([3,4]);
   const third=(R*2)/3;
   ctx.beginPath();
   ctx.moveTo(CX-R,CY-R+third);ctx.lineTo(CX+R,CY-R+third);
   ctx.moveTo(CX-R,CY-R+third*2);ctx.lineTo(CX+R,CY-R+third*2);
   ctx.moveTo(CX-R+third,CY-R);ctx.lineTo(CX-R+third,CY+R);
   ctx.moveTo(CX-R+third*2,CY-R);ctx.lineTo(CX-R+third*2,CY+R);
   ctx.stroke();
   ctx.restore();
   ctx.restore();
   if(previewCtx){
    previewCtx.clearRect(0,0,60,60);
    previewCtx.save();
    previewCtx.beginPath();
    previewCtx.arc(30,30,30,0,Math.PI*2);
    previewCtx.clip();
    previewCtx.fillStyle='#ffffff';
    previewCtx.fillRect(0,0,60,60);
    const pr=60/(R*2);
    previewCtx.translate(30+offsetX*pr,30+offsetY*pr);
    previewCtx.rotate((rotation*Math.PI)/180);
    previewCtx.scale(scale*pr,scale*pr);
    previewCtx.drawImage(img,-img.width/2,-img.height/2);
    previewCtx.restore();
   }
  }
  renderCrop();
  function onPointerDown(e){isDragging=true;lastX=e.clientX??e.touches?.[0].clientX;lastY=e.clientY??e.touches?.[0].clientY;box.setPointerCapture?.(e.pointerId);}
  function onPointerMove(e){if(!isDragging)return;const clientX=e.clientX??e.touches?.[0].clientX;const clientY=e.clientY??e.touches?.[0].clientY;const rect=canvas.getBoundingClientRect();offsetX+=(clientX-lastX)*W/Math.max(1,rect.width);offsetY+=(clientY-lastY)*H/Math.max(1,rect.height);lastX=clientX;lastY=clientY;renderCrop();}
  function onPointerUp(e){isDragging=false;try{box.releasePointerCapture?.(e.pointerId);}catch{}}
  box.addEventListener('pointerdown',onPointerDown);
  window.addEventListener('pointermove',onPointerMove);
  window.addEventListener('pointerup',onPointerUp);
  if(zoomSlider)zoomSlider.addEventListener('input',e=>{scale=Number(e.target.value);renderCrop();});
  box.addEventListener('wheel',e=>{e.preventDefault();const zoomFactor=e.deltaY<0?1.08:0.92;scale=Math.max(minScale*0.8,Math.min(minScale*4,scale*zoomFactor));if(zoomSlider)zoomSlider.value=scale;renderCrop();},{passive:false});
  $('#cropper-rotate-btn')?.addEventListener('click',()=>{rotation=(rotation+90)%360;renderCrop();});
  $('#cropper-reset-btn')?.addEventListener('click',()=>{offsetX=0;offsetY=0;scale=minScale;rotation=0;if(zoomSlider)zoomSlider.value=scale;renderCrop();});
  $('#cropper-apply-btn')?.addEventListener('click',()=>{
   clamp();
   const outCanvas=document.createElement('canvas');outCanvas.width=300;outCanvas.height=300;
   const outCtx=outCanvas.getContext('2d');
   outCtx.fillStyle='#ffffff';
   outCtx.fillRect(0,0,300,300);
   const outRatio=300/(R*2);
   outCtx.save();
   outCtx.translate(150+offsetX*outRatio,150+offsetY*outRatio);
   outCtx.rotate((rotation*Math.PI)/180);
   outCtx.scale(scale*outRatio,scale*outRatio);
   outCtx.drawImage(img,-img.width/2,-img.height/2);
   outCtx.restore();
   const base64=outCanvas.toDataURL('image/jpeg',0.92);
   const hidden=$('#avatarUrl-hidden');if(hidden)hidden.value=base64;
   const heroBox=$('.profile-hero-avatar')||$('.profile-hero-avatar-box');
   if(heroBox){
    let imgEl=heroBox.querySelector('img');
    if(!imgEl){
     const ph=heroBox.querySelector('.avatar-initials')||heroBox.querySelector('.profile-hero-avatar-placeholder');if(ph)ph.remove();
     imgEl=document.createElement('img');imgEl.className='avatar-img';imgEl.id='hero-avatar-img';heroBox.prepend(imgEl);
    }
    imgEl.src=base64;
   }
   const nameRow=$('.profile-hero-name-row');
   if(nameRow&&!$('#avatar-remove-btn')){nameRow.insertAdjacentHTML('beforeend',`<button type="button" class="btn ghost small danger" id="avatar-remove-btn" data-action="remove-avatar">${icon('trash')} Xóa ảnh</button>`);}
   closeModal();
   toast('Đã căn chỉnh ảnh đại diện! Bấm "Lưu thông tin hồ sơ" để hoàn tất.','success');
  });
 };
 img.src=imgSrc;
}
document.addEventListener('change',event=>{
 if(event.target.id==='avatar-file-input'||event.target.classList.contains('avatar-file-trigger')){
  const file=event.target.files[0];
  if(!file)return;
  if(file.size>8*1024*1024){toast('Kích thước ảnh tối đa 8MB.','error');return;}
  const reader=new FileReader();
  reader.onload=e=>{
   openAvatarCropper(e.target.result);
   event.target.value='';
  };
  reader.readAsDataURL(file);
 }
 if(['task-status','task-category','task-priority','task-from','task-to'].includes(event.target.id))filterTasks();
});
window.addEventListener('hashchange',()=>{if(state.workspace)render();});window.addEventListener('session-expired',expiry);
$('#modal').addEventListener('click',e=>{if(e.target===$('#modal'))closeModal();});
authSetup(loggedIn);setInterval(tick,1000);setInterval(()=>{if(state.workspace&&!document.hidden&&!$('#modal').open&&!working&&!['time','settings','scenario','admin','tasks'].includes(state.route))load().catch(e=>{if(state.workspace)toast(e.message,'error');});},30000);
try{await loggedIn();}catch{renderAuth();}
