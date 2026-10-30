using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Domain.Enums;
using SPWPOS.TestSupport;

var rows = new List<Probe>();
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var output = Path.GetFullPath(args.FirstOrDefault(x => x.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) ?? "artifacts/note-audit-2026-10-25/backend-probes.json");
if(args.Contains("--live")) return await LiveNotesProbe.Run(output);
async Task Run(string id, string title, Func<Task<object>> action, string classification="GAP")
{
    try { var details=await action(); rows.Add(new(id,title,classification,details)); Console.WriteLine($"{id}: {classification}"); }
    catch(Exception error) { rows.Add(new(id,title,"HARNESS_ERROR",new { type=error.GetType().Name, message=error is AuthException a ? a.Code : error.Message })); Console.WriteLine($"{id}: HARNESS_ERROR ({error.GetType().Name})"); }
}
void Check(bool test,string message) { if(!test) throw new InvalidOperationException(message); }
async Task<T> Call<T>(ServiceHarness h,Func<IWorkspaceService,Task<T>> action)
{ await using var scope=h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
async Task<(ServiceHarness h,Guid uid,NoteView note,Stub ai)> Setup(bool legacy=false,int count=1)
{
    var ai=new Stub { Legacy=legacy, Count=count };
    var h=await ServiceHarness.CreateAsync(services=>services.AddSingleton<ITaskInterpreter>(ai));
    var user=await h.RegisterAsync();
    await Call(h,async s=>{ await s.SetPreferenceAsync(user.Id,new("UTC",5,240,0,0,1440,MaximumDailyMinutes:1440),default); await s.SetAvailabilityAsync(user.Id,new(Enumerable.Range(0,7).Select(d=>new AvailabilityInput(d,0,1440)).ToArray()),default); return true; });
    var content=count==1?"Đọc tài liệu 30 phút":count==2?"Đọc tài liệu 30 phút\nChuẩn bị slide 30 phút":"Đọc tài liệu 30 phút\nChuẩn bị slide 30 phút\nRà soát số liệu 30 phút";
    var note=await Call(h,s=>s.SaveNoteAsync(user.Id,null,new("Audit note",content),default));
    note=await Call(h,s=>s.AnalyzeNoteAsync(user.Id,note.Id,new(note.Version),default));
    return(h,user.Id,note,ai);
}
async Task<NoteView> Apply(ServiceHarness h,Guid uid,NoteView note,NoteActivity[] activities)
{
    var plan=await Call(h,s=>s.PreviewNoteActivitiesAsync(uid,note.Id,new(note.Version,activities),default));
    var result=await Call(h,s=>s.ApplyNoteActivitiesAsync(uid,note.Id,plan.Id,new(note.Version),default)); return result.Note;
}
await Run("B01","Same task name on a later date is silently removed",async()=>{
    var(h,uid,note,ai)=await Setup(); await using var life=h;
    note=await Apply(h,uid,note,note.Analysis!.Activities!);
    ai.DeadlineDays=3;
    note=await Call(h,s=>s.SaveNoteAsync(uid,note.Id,new(note.Title,note.Content+"\nĐọc tài liệu thêm lần nữa vào ngày khác",note.Version),default));
    note=await Call(h,s=>s.AnalyzeNoteAsync(uid,note.Id,new(note.Version),default));
    Check(note.Analysis!.Activities!.Length==0,"Expected current name-only filter to drop the second occurrence.");
    return new { aiReturned=1, newActivities=note.Analysis.Activities.Length, persistedTasks=await h.DbAsync(db=>db.WorkTasks.CountAsync()), nextDeadlineDays=3 };
});
await Run("B02","Two occurrences with the same name in one analysis collapse to one",async()=>{
    var(h,uid,note,ai)=await Setup(count:2); await using var life=h;
    ai.SameNames=true;
    note=await Call(h,s=>s.AnalyzeNoteAsync(uid,note.Id,new(note.Version),default));
    Check(note.Analysis!.Activities!.Length==1,"Expected one occurrence after DistinctBy(name).");
    return new { providerActivities=2, reviewedActivities=1 };
});
await Run("B03","Unselected activity is no longer suggested for unchanged content",async()=>{
    var(h,uid,note,ai)=await Setup(count:2); await using var life=h;
    var skipped=note.Analysis!.Activities![1].Name;
    note=await Apply(h,uid,note,[note.Analysis.Activities[0]]);
    var calls=ai.Calls;
    note=await Call(h,s=>s.AnalyzeNoteAsync(uid,note.Id,new(note.Version),default));
    Check(note.Analysis!.Activities!.Length==0 && ai.Calls==calls,"Expected unchanged-content shortcut to skip the still-unselected activity.");
    return new { skipped, persistedTasks=await h.DbAsync(db=>db.WorkTasks.CountAsync()), suggestionsAfterRetry=0, providerCalledAgain=false };
});
await Run("B04","Reanalyzing imported unscheduled tasks clears their continuation state",async()=>{
    var(h,uid,note,ai)=await Setup(legacy:true); await using var life=h;
    note=await Call(h,s=>s.ImportNoteAsync(uid,note.Id,new(note.Version,note.Analysis!.Tasks),default));
    var before=note.PendingTaskIds!.Length;
    note=await Call(h,s=>s.AnalyzeNoteAsync(uid,note.Id,new(note.Version),default));
    string code;
    try { await Call(h,s=>s.ScheduleNoteAsync(uid,note.Id,new(note.Version),default)); code="NONE"; } catch(AuthException error) { code=error.Code; }
    Check(before==1 && note.PendingTaskIds!.Length==0 && code=="NOTE_NO_NEW_ACTIVITIES","Expected loss of pending task IDs and blocked continuation.");
    return new { pendingBefore=before,pendingAfter=note.PendingTaskIds!.Length,code,persistedTasks=await h.DbAsync(db=>db.WorkTasks.CountAsync()),sessions=await h.DbAsync(db=>db.WorkSessions.CountAsync()) };
});
await Run("B05","Renaming a selected subset lets the original source name reappear",async()=>{
    var(h,uid,note,ai)=await Setup(count:2); await using var life=h;
    var source=note.Analysis!.Activities![0];
    note=await Apply(h,uid,note,[source with { Name="Tên đã chỉnh trong bước kiểm tra" }]);
    ai.Count=3;
    note=await Call(h,s=>s.SaveNoteAsync(uid,note.Id,new(note.Title,note.Content+"\nThêm ý mới",note.Version),default));
    note=await Call(h,s=>s.AnalyzeNoteAsync(uid,note.Id,new(note.Version),default));
    var names=note.Analysis!.Activities!.Select(x=>x.Name).ToArray();
    Check(names.Contains(source.Name),"Expected previously renamed source to be offered again.");
    return new { savedAs="Tên đã chỉnh trong bước kiểm tra", sourceOfferedAgain=source.Name, suggestions=names };
});
await Run("B06","A title-only checklist task cannot be created without scheduling metadata",async()=>{
    var(h,uid,note,ai)=await Setup(); await using var life=h;
    string code;
    try { await Call(h,s=>s.PreviewNoteActivitiesAsync(uid,note.Id,new(note.Version,[new("TASK","Gọi điện cho mẹ")]),default)); code="NONE"; } catch(AuthException error) { code=error.Code; }
    Check(code=="NOTE_ACTIVITY_INCOMPLETE","Expected missing duration and date to block creation.");
    return new {code,tasks=await h.DbAsync(db=>db.WorkTasks.CountAsync())};
},"DESIGN_GAP");
await Run("B07","One task without capacity blocks all valid tasks and events in the batch",async()=>{
    var(h,uid,note,ai)=await Setup(); await using var life=h;
    await Call(h,async s=>{await s.SetAvailabilityAsync(uid,new([]),default);return true;});
    var now=h.Clock.GetUtcNow();
    var acts=new[]{new NoteActivity("EVENT","Cuộc họp hợp lệ",Start:now.AddDays(1),End:now.AddDays(1).AddHours(1),Type:"MEETING"),new NoteActivity("TASK","Task chưa có giờ rảnh",EstimatedMinutes:30,Deadline:now.AddDays(2))};
    var plan=await Call(h,s=>s.PreviewNoteActivitiesAsync(uid,note.Id,new(note.Version,acts),default));
    string code; try { await Call(h,s=>s.ApplyNoteActivitiesAsync(uid,note.Id,plan.Id,new(note.Version),default));code="NONE";}catch(AuthException error){code=error.Code;}
    Check(code=="NOTE_PLAN_INCOMPLETE","Expected atomic plan validation to block both activities.");
    return new {code,tasks=await h.DbAsync(db=>db.WorkTasks.CountAsync()),events=await h.DbAsync(db=>db.FixedEvents.CountAsync()),warnings=plan.Warnings.Select(x=>x.Code).ToArray()};
},"DESIGN_GAP");
await Run("B08","A note can be deleted through the legacy API without a version",async()=>{
    var(h,uid,note,ai)=await Setup(); await using var life=h;
    note=await Call(h,s=>s.SaveNoteAsync(uid,note.Id,new(note.Title,note.Content+"\nNewer draft",note.Version),default));
    await Call(h,async s=>{await s.DeleteNoteAsync(uid,note.Id,Guid.Empty,default);return true;});
    Check((await Call(h,s=>s.GetNotesAsync(uid,default))).Length==0,"Expected legacy delete without version to succeed.");
    return new {versionOmitted=true,deleted=true};
},"API_RISK");
await Run("B09","Other accounts cannot analyze, edit, delete, preview or tick this note",async()=>{
    var(h,uid,note,ai)=await Setup();await using var life=h;
    note=await Apply(h,uid,note,note.Analysis!.Activities!);
    const string address="second.note.audit@example.com";
    await h.AuthAsync(auth=>auth.RegisterAsync(new(address,ServiceHarness.Password,ServiceHarness.Password,"Second","Audit","second.note.audit"),ServiceHarness.Context,default));
    await h.AuthAsync(auth=>auth.VerifyEmailAsync(new(address,h.Email.Inbox[(address,OtpPurpose.EmailVerification)]),ServiceHarness.Context,default));
    var other=await h.DbAsync(db=>db.Users.Where(x=>x.Email==address).Select(x=>x.Id).SingleAsync());
    var version=await h.DbAsync(db=>db.WorkTasks.Select(x=>x.Version).SingleAsync());
    var codes=new List<string>();
    async Task Denied(Func<Task> action){try{await action();codes.Add("ALLOWED");}catch(AuthException error){codes.Add(error.Code);}}
    await Denied(async()=>{await Call(h,s=>s.AnalyzeNoteAsync(other,note.Id,new(note.Version),default));});
    await Denied(async()=>{await Call(h,s=>s.SaveNoteAsync(other,note.Id,new("Other","Other",note.Version),default));});
    await Denied(async()=>{await Call(h,async s=>{await s.DeleteNoteAsync(other,note.Id,note.Version,default);return true;});});
    await Denied(async()=>{await Call(h,s=>s.PreviewNoteActivitiesAsync(other,note.Id,new(note.Version,note.Analysis!.Activities!),default));});
    await Denied(async()=>{await Call(h,s=>s.SetNoteTaskCompletionAsync(other,note.Id,note.CreatedTaskIds[0],new(version,true),default));});
    Check(codes.Count==5 && codes.All(x=>x=="NOT_FOUND"),"Expected all cross-account operations to be denied.");
    Check((await Call(h,s=>s.GetNotesAsync(other,default))).Length==0,"Other account can list foreign notes.");
    return new {operations=6,codes,ownerNoteCount=(await Call(h,s=>s.GetNotesAsync(uid,default))).Length};
},"PASS");
await Run("B10","Modern mixed activity analysis cannot use the legacy create-tasks-only endpoint",async()=>{
    var(h,uid,note,ai)=await Setup();await using var life=h;
    string code;try{await Call(h,s=>s.ImportNoteAsync(uid,note.Id,new(note.Version,[new("Một việc","",h.Clock.GetUtcNow().AddDays(2),30)]),default));code="NONE";}catch(AuthException error){code=error.Code;}
    Check(code=="NOTE_NEEDS_REVIEW","Expected current Activities branch to require proposal/apply.");
    return new {code,tasks=await h.DbAsync(db=>db.WorkTasks.CountAsync())};
},"DESIGN_GAP");
if(args.Contains("--sql")) await Run("B11","Concurrent SQL Server note saves have one winner and one NOTE_STALE",async()=>{
    await using var factory=new AuditSqlFactory(Path.GetFullPath("src/SPWPOS.Api")); using var client=await factory.ClientAsync();
    async Task<JsonElement> Post(string route,object body){using var response=await client.PostAsJsonAsync(route,body);Check(response.StatusCode==HttpStatusCode.OK,"Unexpected HTTP setup status.");using var parsed=JsonDocument.Parse(await response.Content.ReadAsStringAsync());return parsed.RootElement.GetProperty("data").Clone();}
    await Post("/api/auth/register",new{userName=ServiceHarness.UserName,email=ServiceHarness.Address,password=ServiceHarness.Password,confirmPassword=ServiceHarness.Password,firstName="Note",lastName="Audit"});
    await Post("/api/auth/verify-email",new{email=ServiceHarness.Address,otp=factory.Email.Inbox[(ServiceHarness.Address,OtpPurpose.EmailVerification)]});
    var tokens=await Post("/api/auth/login",new{email=ServiceHarness.Address,password=ServiceHarness.Password});
    client.DefaultRequestHeaders.Authorization=new("Bearer",tokens.GetProperty("accessToken").GetString());
    var first=await Post("/api/notes",new NoteInput("SQL audit","Initial note"));
    var id=first.GetProperty("id").GetGuid();var version=first.GetProperty("version").GetGuid();
    using var a=JsonContent.Create(new NoteInput("Tab A","Text from A",version));using var b=JsonContent.Create(new NoteInput("Tab B","Text from B",version));
    var responses=await Task.WhenAll(client.PutAsync($"/api/notes/{id}",a),client.PutAsync($"/api/notes/{id}",b));
    try{
      var statuses=responses.Select(x=>(int)x.StatusCode).Order().ToArray();Check(statuses.SequenceEqual(new[]{200,409}),"Expected one winner and one conflict.");
      using var error=JsonDocument.Parse(await responses.Single(x=>x.StatusCode==HttpStatusCode.Conflict).Content.ReadAsStringAsync());
      Check(error.RootElement.GetProperty("error").GetProperty("code").GetString()=="NOTE_STALE","Expected NOTE_STALE.");
      return new{statuses,conflict="NOTE_STALE",database="isolated SPWPOS_Tests_<GUID>"};
    }finally{foreach(var response in responses)response.Dispose();}
},"PASS");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new {time=DateTimeOffset.UtcNow,probes=rows},json));
Console.WriteLine($"PROBES: {rows.Count}; harness errors: {rows.Count(x=>x.Classification=="HARNESS_ERROR")}");
return rows.Any(x=>x.Classification=="HARNESS_ERROR")?1:0;

record Probe(string Id,string Title,string Classification,object Details);
sealed class AuditSqlFactory(string root) : ApiFactory(true)
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    { base.ConfigureWebHost(builder); builder.UseContentRoot(root); }
}
sealed class Stub : ITaskInterpreter
{
    public bool Legacy {get;set;} public int Count {get;set;}=1; public bool SameNames {get;set;} public int DeadlineDays {get;set;}=2; public int Calls {get;private set;}
    public NoteAgentCapabilities Capabilities=>new(true,Provider:"Gemini");
    public Task<NoteAnalysis> AnalyzeNoteAsync(string text,bool useAi,string zone,DateTimeOffset now,CancellationToken ct)
    {
        Calls++;
        var names=Enumerable.Range(0,Count).Select(i=>i==0||SameNames?"Đọc tài liệu":i==1?"Chuẩn bị slide":"Rà soát số liệu").ToArray();
        return Task.FromResult(Legacy?new NoteAnalysis(names.Select((name,i)=>new TaskInput(name,"",now.AddDays(DeadlineDays+i),30,TaskType:"STUDY")).ToArray(),"Gemini",[]):
            new NoteAnalysis([],"Gemini",[],Activities:names.Select((name,i)=>new NoteActivity("TASK",name,Type:"STUDY",EstimatedMinutes:30,Deadline:now.AddDays(DeadlineDays+i))).ToArray()));
    }
    public Task<TaskSuggestion> CaptureAsync(CaptureInput i,string z,DateTimeOffset n,CancellationToken ct)=>throw new NotSupportedException();
    public Task<BreakdownView> BreakdownAsync(TaskInput i,bool a,CancellationToken ct)=>throw new NotSupportedException();
}
