// Local-only UI test server. No connection to a real database or authentication provider.
const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../../src/SPWPOS.Api/wwwroot');
const date=(day,hour=17,minute=0)=>{const d=new Date();d.setUTCHours(0,0,0,0);d.setUTCDate(d.getUTCDate()+day);d.setUTCHours(hour-7,minute);return d.toISOString();};
const categories=[{id:'product',name:'Dự án cá nhân',color:'#80a88d'},{id:'study',name:'Học tập',color:'#a9ad7f'},{id:'life',name:'Cuộc sống',color:'#8fabc0'}];
const names=['Hoàn thiện giao diện dự án PLANORA','Chuẩn bị nội dung buổi thảo luận','Đọc tài liệu kiến trúc hệ thống','Viết báo cáo tiến độ tuần','Ôn tập và ghi chú chương 4','Sắp xếp kế hoạch tuần tới'];
let tasks=names.map((name,index)=>({id:`task-${index}`,name,description:'Công việc minh họa dùng để kiểm tra giao diện.',status:index===0?'InProgress':index===5?'Completed':'Todo',deadline:date(index+1),estimatedMinutes:90+index*30,adjustedMinutes:100+index*30,remainingMinutes:index===5?0:80+index*20,actualMinutes:index===0?30:index===5?120:0,plannedMinutes:60,progress:index===0?35:index===5?100:0,priority:index===0?4:index===1?3:2,categoryId:categories[index%3].id,parentId:null,dependencies:[],taskType:['PROGRAMMING','MEETING_PREP','STUDY','WRITING','STUDY','OTHER'][index],isHardDeadline:true,splittable:true,minimumSessionMinutes:25,creationSource:'MANUAL'}));
let sessions=Array.from({length:7},(_,index)=>({id:`session-${index}`,taskId:tasks[index%5].id,taskName:tasks[index%5].name,start:date(index<3?0:index-2,index<3?9+index*2:10),end:date(index<3?0:index-2,index<3?10+index*2:11),status:index===0?'Completed':'Scheduled',isLocked:index===3,reasonCode:'DEADLINE_PRIORITY_CAPACITY',actualMinutes:index===0?55:0,actualSeconds:index===0?3300:0}));
const profile={id:'preview-user',firstName:'Minh',lastName:'Nguyễn',displayName:'Minh Nguyễn',email:'minh@example.test',avatarUrl:null,bio:'Dành thời gian cho những điều quan trọng.'};
const workspace=()=>({profile,tasks,sessions,categories,availability:[1,2,3,4,5].map(dayOfWeek=>({dayOfWeek,startMinute:480,endMinute:1080})),exceptions:[],events:[{id:'event-1',name:'Thảo luận nhóm dự án',start:date(1,14),end:date(1,15),eventType:'MEETING',description:''}],preference:{timeZoneId:'Asia/Ho_Chi_Minh',minimumSessionMinutes:25,maximumSessionMinutes:90,breakMinutes:15,planningHorizonDays:14,preferredPeriod:'MORNING',preferredStartMinute:480,preferredEndMinute:1080},notifications:[{id:'notification-1',title:'Một deadline đang đến gần',message:'Chuẩn bị nội dung buổi thảo luận đến hạn trong 48 giờ.',createdAt:date(0,8),isRead:false,taskId:'task-1'},{id:'notification-2',title:'Kế hoạch đã được cập nhật',message:'Các phiên đã khóa được giữ nguyên khi lập lại lịch.',createdAt:date(-1),isRead:true}],notificationSettings:{inAppEnabled:true,deadlineLeadHours:24,sessionReminderLeadMinutes:10,workloadWarningLevel:'HIGH'}});
const insights={remainingMinutes:720,effectiveCapacityMinutes:1440,calendarFreeMinutes:1800,horizonDemandMinutes:720,capacityFactor:.8,capacitySamples:8,riskLevel:'Moderate',loadRatio:.5,plannedMinutes:480,actualMinutes:525,biases:[{taskType:'All',samples:6,personalized:true,ratio:1.09},{taskType:'PROGRAMMING',samples:3,personalized:true,ratio:1.18},{taskType:'STUDY',samples:2,personalized:false,ratio:1}],reasons:[],tasks:[]};
const plan={id:'preview-plan',start:date(0,8),end:date(7,17),sessions:sessions.slice(1),warnings:[],explanations:['Xếp phiên trong giờ rảnh và chừa thời gian nghỉ.','Giữ nguyên phiên đã khóa.'],status:'READY',isApplied:false};
const baseline=JSON.stringify({tasks,sessions});
http.createServer(async(req,res)=>{try{
 const url=new URL(req.url,'http://127.0.0.1'),send=(data,status=200)=>{res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify({data}));};
 if(url.pathname.startsWith('/api/')){
  let body='';for await(const chunk of req)body+=chunk;const input=body?JSON.parse(body):{};
  if(url.pathname==='/api/qa/reset'){({tasks,sessions}=JSON.parse(baseline));plan.sessions=sessions.slice(1);plan.isApplied=false;return send({});}
  if(url.pathname==='/api/browser-session/me')return req.headers.referer?.includes('guest=1')?send(null,401):send({user:profile,roles:['Admin']});
  if(url.pathname==='/api/browser-session/refresh')return send(null,401);
  if(url.pathname==='/api/workspace')return send(workspace());
  if(url.pathname==='/api/intelligence')return send(insights);
  if(url.pathname==='/api/plans/conflicts')return send([]);
  if(url.pathname==='/api/tasks'&&req.method==='POST'){const task={...tasks[1],...input,id:`created-${tasks.length}`,status:'Todo',progress:0,remainingMinutes:input.estimatedMinutes,dependencies:[]};tasks.push(task);return send(task);}
  if(url.pathname==='/api/plans'&&req.method==='POST')return send(plan);
  if(url.pathname==='/api/plans/preview-plan/apply'){plan.isApplied=true;return send({});}
  if(url.pathname.startsWith('/api/sessions/')&&req.method==='POST'){const [,id,action]=url.pathname.split('/').slice(2);const s=sessions.find(s=>s.id===id);if(s)s.status={start:'Running',pause:'Paused',resume:'Running',complete:'Completed',skip:'Skipped'}[action]||s.status;return send({});}
  if(url.pathname==='/api/admin/users')return send([]);
  if(url.pathname==='/api/admin/jobs')return send([]);
  if(url.pathname==='/api/admin/status')return send({databaseHealthy:true,serverTime:new Date().toISOString(),users:1,tasks:tasks.length,activeSessions:0,failedLoginsLast24Hours:0,lastNotificationRun:null,failedNotificationRuns:0});
  if(url.pathname==='/api/tasks/capture')return send({source:'Rules',notes:['Minh họa kiểm tra giao diện.'],task:{...tasks[1],id:undefined,name:input.text}});
  return send({});
 }
 const relative=decodeURIComponent(url.pathname==='/'?'/index.html':url.pathname);const file=path.resolve(root,'.'+relative);
 if(!file.startsWith(root+path.sep)){res.writeHead(403);return res.end();}
 const data=fs.readFileSync(file);
 res.setHeader('Content-Security-Policy',"default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'");
 res.setHeader('Content-Type',({'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.css':'text/css; charset=utf-8','.svg':'image/svg+xml','.png':'image/png','.ttf':'font/ttf'})[path.extname(file)]||'application/octet-stream');res.end(data);
 }catch(e){res.writeHead(404);res.end('Not found');}
}).listen(5189,'127.0.0.1',()=>console.log('UI fixture: http://127.0.0.1:5189 (synthetic data only)'));
