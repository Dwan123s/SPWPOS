using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public async Task<RecoveryPage> GetRecoveryAsync(Guid uid,int page,CancellationToken ct)
    {
        Require(page is >=1 and <=100000,"VALIDATION_ERROR","Trang không hợp lệ.");
        var settings=await Mine<RecoverySetting>(uid).AsNoTracking().SingleOrDefaultAsync(ct)??new RecoverySetting {Version=Guid.Empty};
        var revision=await Mine<WorkPreference>(uid).Select(x=>x.Revision).SingleOrDefaultAsync(ct);
        var total=await Mine<RecoveryCase>(uid).CountAsync(ct);
        var rows=await Mine<RecoveryCase>(uid).AsNoTracking().OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id).Skip((page-1)*20).Take(20).ToArrayAsync(ct);
        var ids=rows.Where(x=>x.PlanId!=null).Select(x=>x.PlanId!.Value).ToArray();
        var plans=await Mine<PlanProposal>(uid).AsNoTracking().Where(x=>ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
        return new(new(settings.Enabled,settings.AutoApply,settings.MissedDays,settings.QuietStartMinute,settings.QuietEndMinute,settings.Version),rows.Select(row=>
        {
            var proposal=row.PlanId is {} planId?plans.GetValueOrDefault(planId):null;
            var status=proposal?.IsApplied==true?"APPLIED":row.Status is "PROPOSED" or "BLOCKED" && (row.Revision!=revision||proposal?.ExpiresAt<=Now)?"STALE":row.Status;
            return new RecoveryView(row.Id,status,row.Message,JsonSerializer.Deserialize<Guid[]>(row.TaskIdsJson)??[],proposal is null?null:JsonSerializer.Deserialize<PlanView>(proposal.PayloadJson),row.CreatedAt);
        }).ToArray(),page,total);
    }
    public Task SetRecoverySettingsAsync(Guid uid,RecoverySettingInput input,CancellationToken ct)=>Mutate(uid,async()=>
    {
        Require(input.MissedDays is >=1 and <=14 && input.QuietStartMinute is >=0 and <1440 && input.QuietEndMinute is >=0 and <1440 && (!input.AutoApply||input.Enabled),"VALIDATION_ERROR","Cấu hình phục hồi không hợp lệ.");
        var row=await Mine<RecoverySetting>(uid).SingleOrDefaultAsync(ct);
        Require(row?.Version==input.Version||row is null&&input.Version==Guid.Empty,"RECOVERY_SETTINGS_STALE","Cấu hình vừa thay đổi. Hãy tải lại.",409);
        if(row is null){row=new(){UserId=uid};db.RecoverySettings.Add(row);}
        row.Enabled=input.Enabled;row.AutoApply=input.AutoApply;row.MissedDays=input.MissedDays;row.QuietStartMinute=input.QuietStartMinute;row.QuietEndMinute=input.QuietEndMinute;row.Version=Guid.NewGuid();
    },ct,false);
    public async Task ScanRecoveryAsync(Guid uid,CancellationToken ct)
    {
        Guid? autoCase = null;
        await Mutate(uid,async()=>
    {
        var settings=await Mine<RecoverySetting>(uid).SingleOrDefaultAsync(ct)??new();if(!settings.Enabled)return;
        var state=await State(uid,ct);var today=LocalDate(Now,state.Preference);var seeds=new List<(Guid Id,DateOnly Last)>();
        var local=TimeZoneInfo.ConvertTime(Now,PlanningEngine.Zone(state.Preference.TimeZoneId));var minute=local.Hour*60+local.Minute;
        var quiet=settings.QuietStartMinute==settings.QuietEndMinute?false:settings.QuietStartMinute<settings.QuietEndMinute?minute>=settings.QuietStartMinute&&minute<settings.QuietEndMinute:minute>=settings.QuietStartMinute||minute<settings.QuietEndMinute;
        foreach(var task in state.Tasks.Where(x=>x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled && !state.Tasks.Any(child=>child.ParentId==x.Id)))
        {
            var days=state.Sessions.Where(x=>x.TaskId==task.Id && x.End<=Now && LocalDate(x.Start,state.Preference)<today && x.Status is not WorkSessionStatus.Cancelled and not WorkSessionStatus.Skipped)
                .GroupBy(x=>LocalDate(x.Start,state.Preference)).OrderByDescending(x=>x.Key).ToArray();
            var missed=days.TakeWhile(day=>day.All(x=>x.ActualSeconds==0 && x.Status is WorkSessionStatus.Missed or WorkSessionStatus.Scheduled)).Take(settings.MissedDays).ToArray();
            if(missed.Length>=settings.MissedDays)seeds.Add((task.Id,missed[0].Key));
        }
        if(seeds.Count==0)return;
        var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("v1:"+settings.MissedDays+":"+string.Join(";",seeds.OrderBy(x=>x.Id).Select(x=>$"{x.Id:D}:{x.Last:yyyy-MM-dd}")))));
                var existing=await Mine<RecoveryCase>(uid).SingleOrDefaultAsync(x=>x.DedupeKey==key,ct);
        if(existing is not null)
        {
            var expired = existing.PlanId is { } oldPid && await Mine<PlanProposal>(uid).AnyAsync(x=>x.Id==oldPid&&x.ExpiresAt<=Now&&x.Status=="PROPOSED",ct);
            if(settings.AutoApply&&!quiet&&existing.Status=="PROPOSED"&&existing.Revision==state.Preference.Revision&&expired)
                await BuildRecovery(uid,existing,state,ct);
            if(!quiet&&existing.Status is "PROPOSED" or "BLOCKED" or "NEEDS_DECISION")
                await AddNotification(uid,"RECOVERY","Kế hoạch cần điều chỉnh",existing.Message,"recovery:"+key,null,ct,planId:existing.PlanId);
            var isProposalActive = expired
                ? existing.PlanId != null
                : await Mine<PlanProposal>(uid).AnyAsync(x=>x.Id==existing.PlanId&&x.ExpiresAt>Now&&x.Status=="PROPOSED",ct);
            if(settings.AutoApply&&!quiet&&existing.Status=="PROPOSED"&&existing.Revision==state.Preference.Revision&&isProposalActive)
                autoCase=existing.Id;
            return;
        }
        var row=new RecoveryCase{UserId=uid,DedupeKey=key,TaskIdsJson=JsonSerializer.Serialize(seeds.Select(x=>x.Id)),CreatedAt=Now,UpdatedAt=Now};db.RecoveryCases.Add(row);
        await BuildRecovery(uid,row,state,ct);
        if(!quiet)await AddNotification(uid,"RECOVERY","Kế hoạch cần điều chỉnh",row.Message,"recovery:"+key,null,ct,planId:row.PlanId);
        if(settings.AutoApply&&!quiet&&row.Status=="PROPOSED")autoCase=row.Id;
        },ct,false);
        if(autoCase is {} caseId)
        {
            var recovery = await Find<RecoveryCase>(uid, caseId, ct);
            var proposal = await Find<PlanProposal>(uid, recovery.PlanId!.Value, ct);
            if (JsonSerializer.Deserialize<PlanView>(proposal.PayloadJson)?.RequiresConfirmation != true)
                await ApplyRecoveryAsync(uid,caseId,ct);
        }
    }
    private async Task BuildRecovery(Guid uid,RecoveryCase row,PlanningState state,CancellationToken ct)
    {
        if(row.PlanId is {} oldId)
        {
            var old=await Mine<PlanProposal>(uid).SingleOrDefaultAsync(x=>x.Id==oldId,ct);
            if(old is {IsApplied:false})old.Status="DISCARDED";
        }
        row.Revision=state.Preference.Revision;row.UpdatedAt=Now;
        var ids=(JsonSerializer.Deserialize<Guid[]>(row.TaskIdsJson)??[]).Where(id=>state.Tasks.Any(x=>x.Id==id && x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled)).ToArray();
        Require(ids.Length>0,"RECOVERY_RESOLVED","Các công việc đã hoàn tất hoặc không còn hoạt động.",409);
        if(state.Tasks.Any(x=>ids.Contains(x.Id)&&x.IsHardDeadline&&x.Deadline<=Now))
        {row.Status="NEEDS_DECISION";row.PlanId=null;row.Message="Có deadline cứng đã qua. Hãy gia hạn, giảm phạm vi hoặc hủy công việc trước khi xếp lại; hệ thống không tự đổi deadline.";return;}
        var plan=engine.Generate(state,new(Now.AddMinutes(5),state.Preference.PlanningHorizonDays,ids,true,"SESSION_MISSED"),Now);
        var proposal=new PlanProposal{UserId=uid,Kind="RESCHEDULE",TriggerEvent="SESSION_MISSED",Revision=state.Preference.Revision,CreatedAt=Now,ExpiresAt=Now.AddMinutes(30),HorizonStart=plan.Start,HorizonEnd=plan.End};
        proposal.PreviousPlanId=await Mine<PlanProposal>(uid).Where(x=>x.Status=="APPLIED").OrderByDescending(x=>x.AppliedAt).Select(x=>(Guid?)x.Id).FirstOrDefaultAsync(ct);
        plan=plan with{Id=proposal.Id,Kind="RESCHEDULE",PreviousPlanId=proposal.PreviousPlanId};proposal.PayloadJson=JsonSerializer.Serialize(plan);db.PlanProposals.Add(proposal);StorePlanDecisions(uid,proposal,plan,state);
        row.PlanId=proposal.Id;row.Status=plan.Warnings.Any(x=>x.UnscheduledMinutes>0||x.Code=="SOFT_DEADLINE_EXCEEDED")?"BLOCKED":"PROPOSED";
        row.Message=row.Status=="BLOCKED"?"Chưa đủ thời gian để phục hồi toàn bộ kế hoạch. Xem phần còn thiếu và điều chỉnh giờ rảnh hoặc phạm vi.":"Có công việc bị lỡ nhiều ngày đã có lịch dự kiến. Đề xuất giữ phiên khóa/đang chạy và các ràng buộc hiện tại; hãy xem trước khi áp dụng.";
    }
    public Task RebuildRecoveryAsync(Guid uid,Guid id,CancellationToken ct)=>Mutate(uid,async()=>
    {var row=await Find<RecoveryCase>(uid,id,ct);Require(row.Status!="APPLIED","RECOVERY_APPLIED","Đề xuất đã áp dụng.",409);await BuildRecovery(uid,row,await State(uid,ct),ct);},ct,false);
    public Task ApplyRecoveryAsync(Guid uid,Guid id,CancellationToken ct)=>Mutate(uid,async()=>
    {
        var row=await Find<RecoveryCase>(uid,id,ct);if(row.Status=="APPLIED")return;
        Require(row.Status=="PROPOSED"&&row.PlanId!=null,"RECOVERY_BLOCKED","Cần giải quyết cảnh báo hoặc tạo lại đề xuất.",409);
        var proposal=await Find<PlanProposal>(uid,row.PlanId!.Value,ct);var state=await State(uid,ct);
        Require(row.Revision==state.Preference.Revision&&proposal.ExpiresAt>Now,"PLAN_STALE","Lịch đã đổi hoặc đề xuất hết hạn. Hãy tạo lại.",409);
        await ApplyPlanCoreAsync(uid,proposal,JsonSerializer.Deserialize<PlanView>(proposal.PayloadJson)!,state,ct);row.Status="APPLIED";row.UpdatedAt=Now;
    },ct);
    public Task DismissRecoveryAsync(Guid uid,Guid id,CancellationToken ct)=>Mutate(uid,async()=>
    {var row=await Find<RecoveryCase>(uid,id,ct);Require(row.Status!="APPLIED","RECOVERY_APPLIED","Đề xuất đã áp dụng.",409);row.Status="DISMISSED";row.UpdatedAt=Now;},ct,false);
}
