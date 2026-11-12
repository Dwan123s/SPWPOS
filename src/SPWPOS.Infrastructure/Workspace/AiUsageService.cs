using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Workspace;

// Scoped to one HTTP request. Only provider supplied counts are recorded.
public sealed class AiUsageCollector
{
    public long? InputTokens { get; private set; }
    public long? OutputTokens { get; private set; }
    public string? ModelVersion { get; private set; }
    public void Reset() { InputTokens=null;OutputTokens=null;ModelVersion=null; }
    public void Read(JsonElement root)
    {
        static long? Number(JsonElement value, string name) => value.TryGetProperty(name,out var count) && count.TryGetInt64(out var result) && result>=0 ? result : null;
        if(root.TryGetProperty("modelVersion",out var version)) ModelVersion=version.GetString();
        if(root.TryGetProperty("usageMetadata",out var usage))
        { InputTokens=Number(usage,"promptTokenCount"); var candidate=Number(usage,"candidatesTokenCount"); OutputTokens=candidate is null?null:candidate+ (Number(usage,"thoughtsTokenCount")??0); }
        else if(root.TryGetProperty("usage",out usage)) {InputTokens=Number(usage,"input_tokens");OutputTokens=Number(usage,"output_tokens");}
    }
}
public sealed class AiUsageService(ApplicationDbContext db, IConfiguration configuration, TimeProvider clock, AiUsageCollector collector) : IAiUsageService
{
    private const string TaskAutofillOperation = "TASK_AUTOFILL";
    private int DailyLimit => Math.Clamp(configuration.GetValue("AiLimits:DailyRequests",200),1,100000);
    private int ConcurrentLimit => Math.Clamp(configuration.GetValue("AiLimits:ConcurrentRequests",2),1,10);
    private DateTimeOffset Now=>clock.GetUtcNow();
    private DateTimeOffset Day=>new(Now.UtcDateTime.Date,TimeSpan.Zero);
    public async Task<T> RunAsync<T>(Guid uid,string operation,Func<Task<T>> action,CancellationToken ct)
    {
        var email=await db.Users.AsNoTracking().Where(x=>x.Id==uid && x.IsActive && x.EmailConfirmed).Select(x=>x.NormalizedEmail).SingleOrDefaultAsync(ct)
            ?? throw new AuthException("FORBIDDEN","Tài khoản không khả dụng.",403);
        var isAutofill = operation == TaskAutofillOperation;
        var model = isAutofill ? configuration["AiSettings:TaskAutofillModel"] ?? "gemini-3.5-flash-lite" : configuration["AiSettings:Model"];
        var row=new AiUsageRecord {UserId=uid,Operation=operation,ReservedAt=Now,LeaseUntil=Now.AddMinutes(2),
            Provider=configuration["AiSettings:Provider"]??"Unknown",Model=model??"Unknown"};
        // SQL account lock makes reservation atomic across tabs and application instances.
        // The provider call happens after commit, never while holding the lock.
        await using(var tx=await AccountTransaction.BeginAsync(db,email,ct))
        {
            if(await db.AiUsageRecords.CountAsync(x=>x.UserId==uid && x.ReservedAt>=Day,ct)>=DailyLimit)
                throw new AuthException("AI_DAILY_LIMIT","Bạn đã dùng hết lượt AI trong ngày (UTC). Hạn mức sẽ được làm mới vào 07:00 giờ Việt Nam.",429);
            if(await db.AiUsageRecords.CountAsync(x=>x.UserId==uid && x.Status=="RESERVED" && x.LeaseUntil>Now &&
                (x.Operation == TaskAutofillOperation) == isAutofill,ct)>=(isAutofill ? 1 : ConcurrentLimit))
                throw new AuthException("AI_CONCURRENT_LIMIT","Đang có yêu cầu AI xử lý. Hãy chờ hoàn tất rồi thử lại.",429);
            db.AiUsageRecords.Add(row);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        }
        collector.Reset();var watch=Stopwatch.StartNew();
        try {var result=await action();row.Status="COMPLETED";return result;}
        catch {row.Status="FAILED";throw;}
        finally
        {
            // A cancelled client must still settle a reservation, since the provider
            // may already have charged. Failure retains the daily request debit.
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            db.Entry(row).State=EntityState.Detached;
            var saved=await db.AiUsageRecords.SingleAsync(x=>x.Id==row.Id,cleanup.Token);
            saved.Status=row.Status;saved.FinishedAt=Now;saved.InputTokens=collector.InputTokens;saved.OutputTokens=collector.OutputTokens;
            saved.ModelVersion=collector.ModelVersion is {Length:<=120}?collector.ModelVersion:null;saved.LatencyMilliseconds=watch.ElapsedMilliseconds;
            await db.SaveChangesAsync(cleanup.Token);
        }
    }
    public async Task<AiUsageView> SummaryAsync(Guid uid,CancellationToken ct)
    {
        var rows=await db.AiUsageRecords.AsNoTracking().Where(x=>x.UserId==uid && x.ReservedAt>=Day)
            .Select(x=>new{x.Status,x.LeaseUntil,x.InputTokens,x.OutputTokens}).ToArrayAsync(ct);
        return new(Day.AddDays(1),DailyLimit,rows.Length,rows.Count(x=>x.Status=="RESERVED"&&x.LeaseUntil>Now),ConcurrentLimit,
            rows.Any(x=>x.InputTokens!=null)?rows.Sum(x=>x.InputTokens):null,rows.Any(x=>x.OutputTokens!=null)?rows.Sum(x=>x.OutputTokens):null,
            rows.Count(x=>x.InputTokens==null||x.OutputTokens==null));
    }
}
