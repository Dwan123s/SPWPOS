import {afterAll,beforeEach,expect,it,vi} from "vitest";
import {fireEvent,render,screen,waitFor} from "@testing-library/react";
import {writeFileSync} from "node:fs";
import {NotesPage} from "../../frontend/src/notes.jsx";
import {state} from "../../frontend/src/ui.jsx";
vi.mock("../../frontend/src/calendar.jsx",()=>({planModal:vi.fn()}));
const evidence=[];
let stored,calls,offline;
beforeEach(()=>{
 localStorage.clear();calls=[];offline=false;stored=[];
 state.user={id:"audit-user"};state.workspace={tasks:[],events:[],sessions:[],availability:[{dayOfWeek:1}],preference:{timeZoneId:"Asia/Ho_Chi_Minh"}};
 vi.stubGlobal("fetch",vi.fn(async(url,options={})=>{
  const method=options.method||"GET";const body=options.body?JSON.parse(options.body):undefined;calls.push({url,method,body});let data;
  if(url==="/api/notes/agent") data={aiAvailable:true,provider:"Gemini"};
  else if(url==="/api/notes"&&method==="GET") data=structuredClone(stored);
  else if(["POST","PUT"].includes(method)){
    if(offline) throw new Error("Audit network offline");
    data={...body,id:"note-1",version:"v2",analysis:null,createdTaskIds:[],updatedAt:"2026-10-25T08:00:00Z"};stored=[data];
  }else throw new Error("Unexpected audit request");
  return{ok:true,status:200,json:async()=>({data})};
 }));
});
async function ready(){await waitFor(()=>expect(screen.getByRole("button",{name:"Lưu bản nháp"}).closest("fieldset").disabled).toBe(false));}
it("U01: edited activity metadata is not autosaved, does not protect beforeunload, and is lost on reload",async()=>{
 stored=[{id:"note-1",title:"Audit note",content:"Viết báo cáo 30 phút",version:"v1",createdTaskIds:[],updatedAt:"2026-10-25T08:00:00Z",analysis:{source:"Gemini",tasks:[],messages:[],activities:[{kind:"TASK",name:"Báo cáo",type:"WRITING",estimatedMinutes:30,deadline:"2026-10-30T15:00:00Z"}]}}];
 let view=render(<NotesPage onWorkspaceChanged={vi.fn()}/>);await ready();fireEvent.click(screen.getByRole("button",{name:"Audit note"}));
 fireEvent.click(screen.getByText("Chỉnh sửa",{selector:"summary"}));
 fireEvent.change(screen.getByRole("spinbutton",{name:"Thời lượng Báo cáo (phút)"}),{target:{value:"45"}});
 await new Promise(resolve=>setTimeout(resolve,1200));
 const unload=new Event("beforeunload",{cancelable:true});window.dispatchEvent(unload);
 expect(unload.defaultPrevented).toBe(false);
 expect(calls.filter(call=>["PUT","POST"].includes(call.method))).toHaveLength(0);
 view.unmount();view=render(<NotesPage onWorkspaceChanged={vi.fn()}/>);await ready();fireEvent.click(screen.getByRole("button",{name:"Audit note"}));
 fireEvent.click(screen.getByText("Chỉnh sửa",{selector:"summary"}));
 expect(screen.getByRole("spinbutton",{name:"Thời lượng Báo cáo (phút)"}).value).toBe("30");
 evidence.push({id:"U01",classification:"GAP",title:"Review metadata lost on reload",details:{editedMinutes:45,reloadedMinutes:30,serverWrites:0,beforeUnloadProtected:false}});
});
it("U02: checklist toolbar inserts a text glyph and autosaves a note but creates no real task",async()=>{
 render(<NotesPage onWorkspaceChanged={vi.fn()}/>);await ready();
 fireEvent.click(screen.getByRole("button",{name:"Thêm dòng checklist"}));
 expect(screen.getByLabelText("Nội dung ghi chú").value).toContain("☐");
 await waitFor(()=>expect(stored).toHaveLength(1),{timeout:3000});
 expect(screen.queryAllByRole("checkbox",{name:/^Hoàn thành/})).toHaveLength(0);
 expect(calls.some(call=>/\/tasks|\/completion|\/analyze|\/proposals/.test(call.url))).toBe(false);
 evidence.push({id:"U02",classification:"DESIGN_GAP",title:"Text checkbox is not task checkbox",details:{content:stored[0].content,createdTaskIds:stored[0].createdTaskIds,realCompletionCheckboxes:0}});
});
it("U03: offline draft exists only in the tab; unload warns but reload cannot recover it",async()=>{
 offline=true;let view=render(<NotesPage onWorkspaceChanged={vi.fn()}/>);await ready();
 fireEvent.change(screen.getByLabelText("Nội dung ghi chú"),{target:{value:"Ý đang viết lúc mất mạng"}});
 await screen.findByText(/Chưa tự động lưu được/,{},{timeout:3000});
 const unload=new Event("beforeunload",{cancelable:true});window.dispatchEvent(unload);expect(unload.defaultPrevented).toBe(true);
 view.unmount();view=render(<NotesPage onWorkspaceChanged={vi.fn()}/>);await ready();
 expect(screen.getByLabelText("Nội dung ghi chú").value).toBe("");
 evidence.push({id:"U03",classification:"RECOVERY_RISK",title:"No persistent offline draft",details:{unloadWarning:true,recoveredAfterReload:false,serverNotes:stored.length}});
});
it("U04: a title-only draft is neither autosaved nor protected on reload",async()=>{
 let view=render(<NotesPage onWorkspaceChanged={vi.fn()}/>);await ready();
 fireEvent.change(screen.getByLabelText("Tiêu đề ghi chú"),{target:{value:"Ý tưởng chỉ có tiêu đề"}});
 await new Promise(resolve=>setTimeout(resolve,1200));
 expect(calls.filter(call=>["PUT","POST"].includes(call.method))).toHaveLength(0);
 const unload=new Event("beforeunload",{cancelable:true});window.dispatchEvent(unload);expect(unload.defaultPrevented).toBe(false);
 view.unmount();view=render(<NotesPage onWorkspaceChanged={vi.fn()}/>);await ready();
 expect(screen.getByLabelText("Tiêu đề ghi chú").value).toBe("");
 evidence.push({id:"U04",classification:"RECOVERY_RISK",title:"Title-only draft has no autosave or reload protection",details:{serverWrites:0,beforeUnloadProtected:false,recoveredTitle:""}});
});

afterAll(()=>writeFileSync(process.env.NOTE_AUDIT_OUTPUT,JSON.stringify({time:new Date().toISOString(),probes:evidence},null,2)));
