using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private const string ExtraTimeKind = "SESSION_EXTENSION";
    private const string SessionOutcomeKind = "SESSION_OUTCOME";
    private sealed record ExtraTimePayload(Guid SessionId, Guid TaskId, Guid TaskVersion, int Minutes, ExtraTimeSlot[] Slots,
        DateTimeOffset? ConfirmedAt = null, int BudgetMinutes = 0, int Progress = 0, int Estimate = 0, Guid[]? CompletedBefore = null, DateTimeOffset? ReviewedThrough = null, bool Closed = false,
        SessionOutcomeInput? Outcome = null, long RecordedSeconds = 0);
    private async Task<ExtraTimePayload[]> AppliedExtraTimes(Guid uid, CancellationToken ct) =>
        (await Mine<PrivatePlanningProposal>(uid).AsNoTracking().Where(p=>(p.Kind==ExtraTimeKind || p.Kind==SessionOutcomeKind) && p.Applied).OrderByDescending(p=>p.ExpiresAt)
            .Select(p=>p.PayloadJson).ToArrayAsync(ct)).Select(p=>JsonSerializer.Deserialize<ExtraTimePayload>(p)!).ToArray();
    private async Task<DateTimeOffset> WorkTimeConfirmationAt(Guid uid, CancellationToken ct)
    {
        var latest = (await AppliedExtraTimes(uid, ct)).Select(b => b.ConfirmedAt ?? DateTimeOffset.MinValue)
            .DefaultIfEmpty(DateTimeOffset.MinValue).Max();
        return latest >= Now ? latest.AddTicks(1) : Now;
    }
    private async Task ApplyRemainingTimeBudgets(Guid uid, WorkTask[] tasks, WorkSession[] sessions, CancellationToken ct)
    {
        var budgets=await AppliedExtraTimes(uid,ct);
        foreach(var task in tasks) {
            task.ConfirmedRemainingMinutes=null;
            var budget=budgets.Where(b=>b.TaskId==task.Id && !b.Closed && (b.Outcome == null || b.Outcome.RemainingMinutes != null)).OrderByDescending(b=>b.ConfirmedAt).FirstOrDefault();
            if(budget==null || budget.Estimate!=task.EstimatedMinutes || task.Progress<budget.Progress || task.Status is WorkTaskStatus.Completed or WorkTaskStatus.Cancelled)continue;
            var spent=sessions.Where(s=>s.TaskId==task.Id && s.Status==WorkSessionStatus.Completed && !(budget.CompletedBefore??[]).Contains(s.Id))
                .Sum(s=>Math.Max(0,s.ActualSeconds/60d));
            var progressRemaining=(int)Math.Ceiling(budget.BudgetMinutes*(100-task.Progress)/(double)Math.Max(1,100-budget.Progress));
            task.ConfirmedRemainingMinutes=Math.Max(0,Math.Min(progressRemaining,budget.BudgetMinutes-(int)Math.Floor(spent)));
        }
    }
    private async Task<bool> TimeReviewAnswered(Guid uid, Guid id, CancellationToken ct) {
        var source=await Find<WorkSession>(uid,id,ct);
        return (await AppliedExtraTimes(uid,ct)).Any(p=>p.SessionId==id || p.TaskId==source.TaskId && p.ReviewedThrough>=source.End);
    }
    public Task<WorkTimeReview[]> GetWorkTimeReviewsAsync(Guid uid, CancellationToken ct) => WorkTimeReviews(uid, false, ct);
    private async Task<WorkTimeReview[]> WorkTimeReviews(Guid uid, bool includeMissed, CancellationToken ct)
    {
        var answers=await AppliedExtraTimes(uid,ct);
        var rows=await (from s in Mine<WorkSession>(uid).AsNoTracking() join t in Mine<WorkTask>(uid).AsNoTracking() on s.TaskId equals t.Id
            where !t.IsDeleted && t.Status!=WorkTaskStatus.Completed && t.Status!=WorkTaskStatus.Cancelled && s.End<=Now &&
                (s.Status==WorkSessionStatus.Scheduled || s.Status==WorkSessionStatus.NeedsReview || s.Status==WorkSessionStatus.Running || s.Status==WorkSessionStatus.Paused || s.Status==WorkSessionStatus.Completed || includeMissed && s.Status==WorkSessionStatus.Missed)
            orderby s.End descending select new WorkTimeReview(s.Id,t.Id,t.Name,s.Start,s.End,s.ActualSeconds,
                db.TeamTaskAssignments.Any(a=>a.UserId==uid && a.WorkTaskId==t.Id && a.Active))).Take(100).ToArrayAsync(ct);
        return rows.Where(r=>!answers.Any(a=>a.SessionId==r.SessionId || a.TaskId==r.TaskId && a.ReviewedThrough>=r.End)).Take(20).ToArray();
    }
    private async Task EnsureTimeReviewNotifications(Guid uid,CancellationToken ct)
    {
        foreach(var review in (await GetWorkTimeReviewsAsync(uid,ct)).Where(r=>r.End>=Now.AddDays(-1))) {
            var session=await Find<WorkSession>(uid,review.SessionId,ct);
            if(session.Status==WorkSessionStatus.Running) {await StopSegment(session,ct,session.End);session.Status=WorkSessionStatus.Paused;(await Preference(uid,ct)).Revision=Guid.NewGuid();}
            else if(session.Status==WorkSessionStatus.Scheduled){session.Status=WorkSessionStatus.NeedsReview;(await Preference(uid,ct)).Revision=Guid.NewGuid();}
            await AddNotification(uid,"SESSION_REVIEW","Đã hết giờ — bạn đã làm xong chưa?",$"{review.TaskName}: phiên đã hết giờ. Mở Hôm nay để ghi kết quả và thời gian thực làm. Sau đó, bạn có thể xem phương án cho phần còn lại.",
                $"review:{session.Id}:{session.End.UtcTicks}",review.TaskId,ct,sessionId:session.Id);
        }
    }
    private async Task ValidateExtraTimeTask(Guid uid, WorkTask task,CancellationToken ct) {
        Require(!task.IsDeleted && task.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled,"INVALID_TASK","Công việc đã kết thúc.",409);
        Require(!await Mine<WorkTask>(uid).AnyAsync(t=>t.ParentId==task.Id && !t.IsDeleted,ct),"CONTAINER_TASK","Hãy thêm thời gian cho công việc con cụ thể.",409);
        var assignment=await db.TeamTaskAssignments.SingleOrDefaultAsync(a=>a.UserId==uid && a.WorkTaskId==task.Id && a.Active,ct);
        if(assignment!=null) {var shared=await SharedTask(uid,assignment.TeamTaskId,ct);Require(shared.AssigneeId==uid && shared.Status=="Assigned" && shared.PendingChangeJson==null,
            "TEAM_TASK_CHANGED","Task nhóm đang chờ duyệt hoặc thay đổi yêu cầu. Hãy xử lý trong nhóm trước.",409);}
    }
    private static PlanningState ClosedReviewState(PlanningState state,Guid id) => state with {Sessions=state.Sessions.Select(s=>{
        if(s.Id!=id)return s;var copy=Copy(s);copy.Status=WorkSessionStatus.Completed;copy.SegmentStartedAt=null;return copy;
    }).ToArray()};
    public Task<ExtraTimePreview> PreviewExtraTimeAsync(Guid uid,Guid id,ExtraTimeInput input,CancellationToken ct) => Mutate(uid,async()=>{
        Require(input.Minutes is >=5 and <=480,"VALIDATION_ERROR","Chọn từ 5 đến 480 phút bổ sung.");
        var source=await Find<WorkSession>(uid,id,ct);var task=await Find<WorkTask>(uid,source.TaskId,ct);await ValidateExtraTimeTask(uid,task,ct);
        Require(task.HasPlanningDetails,"TASK_PLANNING_DETAILS_REQUIRED","Bổ sung hạn chót và thời gian dự kiến để xếp thêm thời gian.",409);
        Require(source.End<=Now && source.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.NeedsReview or WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed,
            "SESSION_NOT_DUE","Chỉ thêm thời gian sau khi phiên đã hết giờ.",409);
        Require(!await TimeReviewAnswered(uid,id,ct),"TIME_REVIEW_ANSWERED","Phiên này đã được xử lý. Hãy tải lại lịch.",409);
        var state=ClosedReviewState(Copy(await State(uid,ct)),id);var start=Now.AddMinutes(1);var until=task.Deadline<start.AddDays(30)?task.Deadline:start.AddDays(30);
        var reserved=state.Sessions.Where(s=>s.TaskId==task.Id && s.Status==WorkSessionStatus.Scheduled && s.End>Now).Sum(s=>(int)(s.End-s.Start).TotalMinutes);
        foreach(var s in state.Sessions.Where(s=>s.TaskId==task.Id && s.Status==WorkSessionStatus.Scheduled))s.IsLocked=true;
        state.Tasks.Single(t=>t.Id==task.Id).ConfirmedRemainingMinutes=reserved+input.Minutes;
        if(until<=start)return new ExtraTimePreview(Guid.Empty,state.Preference.Revision,id,task.Name,input.Minutes,[],"Đã hết quỹ thời gian trước deadline. Hãy trao đổi để điều chỉnh deadline; NORA chưa thay lịch.",false);
        var plan=engine.Generate(state,new(start,30,[task.Id],UseCapacityFactor:false,UseEstimateBias:false){WindowEnd=until},Now);
        var slots=plan.Sessions.Where(s=>!s.Preserved && s.TaskId==task.Id).Select(s=>new ExtraTimeSlot(s.Id,s.Start,s.End)).ToArray();
        var feasible=plan.Warnings.All(w=>w.UnscheduledMinutes==0) && slots.Sum(s=>(s.End-s.Start).TotalMinutes)==input.Minutes;
        if(!feasible)return new ExtraTimePreview(Guid.Empty,state.Preference.Revision,id,task.Name,input.Minutes,[],
            $"Chưa có đủ {input.Minutes} phút phù hợp trước deadline. Giữ nguyên lịch cố định, giờ nghỉ và các công việc khác; bạn có thể giảm thời gian bổ sung hoặc trao đổi lại deadline.",false);
        var row=new PrivatePlanningProposal{UserId=uid,Kind=ExtraTimeKind,Revision=state.Preference.Revision,ExpiresAt=Now.AddMinutes(10),
            PayloadJson=JsonSerializer.Serialize(new ExtraTimePayload(id,task.Id,task.Version,input.Minutes,slots))};
        db.PrivatePlanningProposals.Add(row);await db.SaveChangesAsync(ct);
        return new ExtraTimePreview(row.Id,row.Revision,id,task.Name,input.Minutes,slots,"NORA tìm được phiên bổ sung. Chỉ thêm vào lịch khi bạn xác nhận; giữ nguyên deadline và các phiên đã có.",true);
    },ct,false);
    public Task<WorkTimeResult> ConfirmExtraTimeAsync(Guid uid,Guid id,ExtraTimeConfirm input,CancellationToken ct) => Mutate(uid,async()=>{
        var row=await Find<PrivatePlanningProposal>(uid,id,ct);Require(row.Kind==ExtraTimeKind,"NOT_FOUND","Không tìm thấy đề xuất thêm giờ.",404);
        var payload=JsonSerializer.Deserialize<ExtraTimePayload>(row.PayloadJson)!;
        if(row.Applied)return new WorkTimeResult("Phiên bổ sung đã được lưu.",payload.Slots.Select(s=>s.Id).ToArray());
        var task=await Find<WorkTask>(uid,payload.TaskId,ct);var source=await Find<WorkSession>(uid,payload.SessionId,ct);await ValidateExtraTimeTask(uid,task,ct);
        Require(row.Revision==input.Revision && row.Revision==(await Preference(uid,ct)).Revision && row.ExpiresAt>Now && task.Version==payload.TaskVersion && payload.Slots.All(s=>s.Start>=Now),
            "EXTRA_TIME_STALE","Lịch hoặc thời gian đã thay đổi. Bấm đề xuất lại để NORA kiểm tra giờ mới.",409);
        Require(!await TimeReviewAnswered(uid,source.Id,ct),"TIME_REVIEW_ANSWERED","Phiên này đã được xử lý ở nơi khác.",409);
        await StopSegment(source,ct,source.End);source.Status=WorkSessionStatus.Completed;source.CompletedAt=source.End;
        await AwardSessionExperienceAsync(uid,source,task,source.End,ct);
        var execution=await Mine<SessionExecution>(uid).SingleOrDefaultAsync(e=>e.SessionId==source.Id,ct);if(execution!=null)execution.EndedAt=source.End;
        await db.SaveChangesAsync(ct);
        var state=await State(uid,ct);var occupied=Occupied(state).ToList();
        var reserved=state.Sessions.Where(s=>s.TaskId==task.Id && s.Status==WorkSessionStatus.Scheduled && s.End>Now).Sum(s=>(int)(s.End-s.Start).TotalMinutes);
        state.Tasks.Single(t=>t.Id==task.Id).ConfirmedRemainingMinutes=reserved+payload.Minutes;
        foreach(var slot in payload.Slots) {
            Require(slot.End<=task.Deadline,"DEADLINE_CONFLICT","Phiên bổ sung không được vượt deadline.",409);
            ValidateSession(state,task.Id,slot.Start,slot.End,occupied,Guid.Empty,false,false);
            db.WorkSessions.Add(new(){Id=slot.Id,UserId=uid,TaskId=task.Id,Start=slot.Start,End=slot.End,ReasonCode="EXTRA_TIME"});
            occupied.Add(new(slot.Id,task.Id,slot.Start,slot.End,"EXTRA_TIME",false));
        }
        row.Applied=true;row.PayloadJson=JsonSerializer.Serialize(payload with{ConfirmedAt=await WorkTimeConfirmationAt(uid,ct),BudgetMinutes=reserved+payload.Minutes,Progress=task.Progress,Estimate=task.EstimatedMinutes,
            CompletedBefore=state.Sessions.Where(s=>s.TaskId==task.Id && s.Status==WorkSessionStatus.Completed).Select(s=>s.Id).ToArray(),ReviewedThrough=source.End});
        task.Status=WorkTaskStatus.InProgress;task.UpdatedAt=Now;
        await ResolveWorkTimeNotifications(uid,task.Id,source.End,ct);
        await AddNotification(uid,"EXTRA_TIME_SAVED","Đã xếp thêm thời gian",$"{task.Name}: thêm {payload.Minutes} phút, giữ nguyên deadline.",$"extra-time:{id}",task.Id,ct);
        return new WorkTimeResult($"Đã xếp thêm {payload.Minutes} phút cho {task.Name}.",payload.Slots.Select(s=>s.Id).ToArray());
    },ct);
    private async Task ReleaseCompletedTaskSessions(Guid uid,Guid taskId,CancellationToken ct) {
        foreach(var session in await Mine<WorkSession>(uid).Where(s=>s.TaskId==taskId && s.Status==WorkSessionStatus.Scheduled).ToArrayAsync(ct))
            if(session.Status==WorkSessionStatus.Scheduled)session.Status=WorkSessionStatus.Cancelled;
        await ResolveWorkTimeNotifications(uid,taskId,null,ct);
        foreach(var row in await Mine<PrivatePlanningProposal>(uid).Where(p=>(p.Kind==ExtraTimeKind || p.Kind==SessionOutcomeKind) && p.Applied).ToArrayAsync(ct)) {
            var payload=JsonSerializer.Deserialize<ExtraTimePayload>(row.PayloadJson)!;
            if(payload.TaskId==taskId && !payload.Closed)row.PayloadJson=JsonSerializer.Serialize(payload with{Closed=true});
        }
    }
    private async Task ResolveWorkTimeNotifications(Guid uid,Guid taskId,DateTimeOffset? through,CancellationToken ct) {
        foreach(var notice in await Mine<InAppNotification>(uid).Where(n=>n.TaskId==taskId && n.Kind=="SESSION_REVIEW" &&
            (through==null || db.WorkSessions.Any(s=>s.UserId==uid && s.Id==n.SessionId && s.End<=through))).ToArrayAsync(ct)) {
            notice.ReadAt=Now;notice.TelegramPending=false;notice.NextTelegramAttemptAt=null;
        }
    }
    public Task<WorkTimeResult> FinishWorkTaskAsync(Guid uid,Guid id,CancellationToken ct) => Mutate(uid,async()=>{
        var session=await Find<WorkSession>(uid,id,ct);var task=await Find<WorkTask>(uid,session.TaskId,ct);
        if(task.Status==WorkTaskStatus.Completed)return new WorkTimeResult("Công việc đã hoàn thành; các phiên tương lai đã được giải phóng.",[]);
        await ValidateExtraTimeTask(uid,task,ct);
        Require(session.Start<=Now && session.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.NeedsReview or WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed,
            "INVALID_TRANSITION","Phiên chưa bắt đầu hoặc không còn khả dụng.",409);
        foreach(var active in await Mine<WorkSession>(uid).Where(s=>s.TaskId==task.Id && (s.Status==WorkSessionStatus.Running || s.Status==WorkSessionStatus.Paused)).ToArrayAsync(ct)) {
            await StopSegment(active,ct,Now<active.End?Now:active.End);active.Status=WorkSessionStatus.Completed;active.CompletedAt=Now;
            if(Now>active.Start && Now<active.End)active.End=Now;
            await AwardSessionExperienceAsync(uid,active,task,Now,ct);
            var execution=await Mine<SessionExecution>(uid).SingleOrDefaultAsync(e=>e.SessionId==active.Id,ct);if(execution!=null)execution.EndedAt=Now;
        }
        await db.SaveChangesAsync(ct);await SetTaskStatusCore(uid,task.Id,new("Completed",100),ct);
        return new WorkTimeResult("Đã ghi nhận hoàn thành và giải phóng các phiên tương lai. Task nhóm vẫn cần trưởng nhóm duyệt báo cáo.",[]);
    },ct);
}
