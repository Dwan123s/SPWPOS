using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure;

static class LiveNotesProbe
{
    public static async Task<int> Run(string output)
    {
        var api=Path.GetFullPath("src/SPWPOS.Api");
        var secrets=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","UserSecrets","SPWPOS-Authentication-Development","secrets.json");
        // Initialize-Dotnet exposes the same shared development APPDATA store.
        if(Environment.GetEnvironmentVariable("APPDATA") is {Length:>0} appData) secrets=Path.Combine(appData,"Microsoft","UserSecrets","SPWPOS-Authentication-Development","secrets.json");
        var config=new ConfigurationBuilder().AddJsonFile(Path.Combine(api,"appsettings.json"),optional:true)
            .AddJsonFile(Path.Combine(api,"appsettings.Development.json"),optional:true).AddJsonFile(secrets,optional:true).AddEnvironmentVariables().Build();
        var services=new ServiceCollection();services.AddSingleton<IConfiguration>(config);services.AddLogging(options=>options.SetMinimumLevel(LogLevel.None));services.AddInfrastructure(config);
        await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();
        var ai=scope.ServiceProvider.GetRequiredService<ITaskInterpreter>();
        var now=DateTimeOffset.UtcNow;var zone=TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var tomorrow=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,zone).DateTime).AddDays(1);
        DateTimeOffset At(int hour)=>new(tomorrow.ToDateTime(new TimeOnly(hour,0)),TimeSpan.FromHours(7));
        const string original="Kế hoạch công việc ngày mai:\n- Viết báo cáo tuần, cần 60 phút, hạn chót 22:00 ngày mai.\n- Chuẩn bị slide thuyết trình, cần 45 phút, hạn chót 22:00 ngày mai.\n- Đọc tài liệu dự án, cần 30 phút, hạn chót 22:00 ngày mai.";
        var results=new List<object>();NoteAnalysis? first=null;var unavailable=false;
        NoteActivity[] Acts(NoteAnalysis value)=>value.Activities ?? value.Tasks.Select(task=>new NoteActivity("TASK",task.Name,task.Description,task.TaskType,EstimatedMinutes:task.EstimatedMinutes,Deadline:task.Deadline,IsHardDeadline:task.IsHardDeadline)).ToArray();
        async Task Case(string id,string input,Func<NoteAnalysis,bool> valid,string expectation,NoteAnalysisContext? context=null)
        {
            if(unavailable||!ai.Capabilities.AiAvailable){results.Add(new{id,status="NOT_RUN",expectation,reason=unavailable?"Earlier provider request failed":"AI not configured"});return;}
            var watch=System.Diagnostics.Stopwatch.StartNew();
            try {
                using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(35));
                var result=context is null?await ai.AnalyzeNoteAsync(input,true,"Asia/Ho_Chi_Minh",now,ct.Token):await ai.AnalyzeNoteChangesAsync(input,context,"Asia/Ho_Chi_Minh",now,ct.Token);
                if(id=="L01")first=result;
                var status=valid(result)?"PASS":"FAIL";
                results.Add(new{id,status,expectation,milliseconds=watch.ElapsedMilliseconds,source=result.Source,result.NeedsReview,activities=Acts(result)});
                Console.WriteLine($"{id}: {status} ({watch.ElapsedMilliseconds} ms)");
            }catch(Exception error){
                var code=error is AuthException auth?auth.Code:error.GetType().Name;
                results.Add(new{id,status="PROVIDER_ERROR",expectation,code,milliseconds=watch.ElapsedMilliseconds});
                unavailable=true;Console.WriteLine($"{id}: PROVIDER_ERROR ({code})");
            }
        }
        await Case("L01",original,result=>{var tasks=Acts(result);return tasks.Length==3&&tasks.All(x=>x.Kind=="TASK"&&x.Deadline==At(22))&&tasks.Select(x=>x.EstimatedMinutes).Order().SequenceEqual(new int?[]{30,45,60});},"Exactly 3 tasks, 60/45/30 minutes, tomorrow 22:00 in Asia/Ho_Chi_Minh.");
        await Case("L02","Gọi điện cho mẹ.",result=>{var tasks=Acts(result);return tasks.Length==1&&tasks[0].EstimatedMinutes is null&&tasks[0].Deadline is null&&result.NeedsReview;},"Keep one incomplete activity; do not invent a duration or deadline.");
        await Case("L03","Ngày mai tôi có cuộc họp từ 15:00 đến 16:00. Tôi cần viết báo cáo 45 phút, hạn chót 20:00 ngày mai.",result=>{var items=Acts(result);return items.Length==2&&items.Any(x=>x.Kind=="EVENT"&&x.Start==At(15)&&x.End==At(16))&&items.Any(x=>x.Kind=="TASK"&&x.EstimatedMinutes==45&&x.Deadline==At(20));},"One meeting tomorrow 15:00-16:00 and one 45-minute task due tomorrow 20:00.");
        if(first is not null){var names=Acts(first).Where(x=>x.Kind=="TASK").Select(x=>x.Name.ToUpperInvariant()).ToArray();
            await Case("L04",original+"\nBổ sung: Rà soát số liệu trong báo cáo, cần 20 phút, hạn chót 23:00 ngày mai.",result=>{var items=Acts(result);return items.Length==1&&items[0].Kind=="TASK"&&items[0].EstimatedMinutes==20&&items[0].Deadline==At(23);},"Incremental edit returns only the one new 20-minute task.",new(original,names,names.Select(x=>"TASK|"+x).ToArray()));
        }else results.Add(new{id="L04",status="NOT_RUN",reason="No initial analysis available"});
        await Case("L05","Tooi caafn ddocj taif lieeuj duwj asn 30 phuts, hanj chots 22h ngayf mai.",result=>{var items=Acts(result);return items.Length==1&&items[0].Kind=="TASK"&&items[0].EstimatedMinutes==30&&items[0].Deadline==At(22);},"Raw Telex preserves a 30-minute task due tomorrow 22:00.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new{time=DateTimeOffset.UtcNow,ai.Capabilities.Provider,ai.Capabilities.AiAvailable,referenceTime=now,tomorrow,zone="Asia/Ho_Chi_Minh",fictionalInputsOnly=true,databaseWrites=false,cases=results},new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
        return 0;
    }
}
