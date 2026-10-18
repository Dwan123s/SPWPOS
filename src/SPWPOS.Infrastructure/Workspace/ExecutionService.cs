using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private void ValidateExecutionStart(PlanningState state, WorkSession session, WorkTask task, bool resuming)
    {
        Require(!state.Events.Any(x => x.Start <= Now && x.End > Now),
            "FIXED_EVENT_CONFLICT", "Đang có sự kiện cố định. Hãy tiếp tục công việc sau sự kiện hoặc xếp lại phần còn lại.", 409);
        Require(Now >= session.Start.AddMinutes(-15) && Now < session.End,
            resuming ? "SESSION_WINDOW_EXPIRED" : "INVALID_TRANSITION",
            "Phiên chưa đến giờ hoặc đã hết khung lịch. Hãy kết thúc phiên cũ và xếp lại phần công việc còn lại.", 409);
        Require(!state.Sessions.Any(x => x.Id != session.Id && x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused),
            "SESSION_ACTIVE", "Chỉ chạy một phiên tại một thời điểm.", 409);
        Require(!state.Sessions.Any(x => x.Id != session.Id && x.Status == WorkSessionStatus.Scheduled && x.Start <= Now && x.End > Now),
            "SESSION_OVERLAP", "Thời điểm này thuộc phiên khác. Hãy kiểm tra lịch trước khi bắt đầu.", 409);
        var completed = engine.CompletionTimes(state, [], engine.Biases(state), Now);
        Require(state.Dependencies.Where(x => x.TaskId == task.Id).All(x => completed.ContainsKey(x.PredecessorId)),
            "DEPENDENCY_INCOMPLETE", "Hoàn thành công việc tiền nhiệm trước.", 409);
        Require(PlanningEngine.InsideWorkWindow(task, Now < session.Start ? Now : session.Start, session.End) &&
            (!task.IsHardDeadline || session.End <= task.Deadline && Now < task.Deadline) &&
            engine.FreeSlots(state, session.Start, session.End).Any(x => x.Start <= session.Start && x.End >= session.End) &&
            engine.FreeSlots(state, Now, session.End).Any(x => x.Start <= Now && x.End >= session.End),
            "STALE_SESSION", "Lịch đã thay đổi hoặc hiện không nằm trong giờ rảnh. Hãy xếp lại phần còn lại trước khi tiếp tục.", 409);
    }

    private async Task StopSegment(WorkSession session, CancellationToken ct)
    {
        if (session.SegmentStartedAt is not { } at) return;
        session.ActualSeconds += (long)Math.Max(0, (Now - at).TotalSeconds);
        session.SegmentStartedAt = null;
        var execution = await Mine<SessionExecution>(session.UserId).SingleAsync(x => x.SessionId == session.Id, ct);
        var segment = await Mine<ExecutionSegment>(session.UserId).SingleAsync(x => x.ExecutionId == execution.Id && x.End == null, ct);
        segment.End = Now; execution.TimerSeconds += (long)Math.Max(0, (Now - at).TotalSeconds); execution.ActualSeconds = session.ActualSeconds;
    }
    public Task ExecuteSessionAsync(Guid uid, Guid id, string action, CancellationToken ct) => Mutate(uid, async () =>
    {
        var session = await Find<WorkSession>(uid, id, ct); var task = await Find<WorkTask>(uid, session.TaskId, ct);
        action = action.ToLowerInvariant();
        // Replayed actions acknowledge the existing result; they never append
        // another execution segment or count the same time twice.
        if (action == "start" && session.Status == WorkSessionStatus.Running ||
            action == "pause" && session.Status == WorkSessionStatus.Paused ||
            action == "resume" && session.Status == WorkSessionStatus.Running ||
            action == "complete" && session.Status == WorkSessionStatus.Completed ||
            action == "skip" && session.Status == WorkSessionStatus.Skipped) return;
        Require(!task.IsDeleted && task.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled, "INVALID_TASK", "Công việc không còn hoạt động.");
        switch (action.ToLowerInvariant())
        {
            case "start":
                Require(session.Status == WorkSessionStatus.Scheduled,
                    "INVALID_TRANSITION", "Chỉ bắt đầu phiên đến giờ thực hiện (sớm tối đa 15 phút).");
                ValidateExecutionStart(await State(uid, ct), session, task, false);
                session.Status = WorkSessionStatus.Running; session.SegmentStartedAt = Now; task.Status = WorkTaskStatus.InProgress; break;
            case "pause":
                Require(session.Status == WorkSessionStatus.Running, "INVALID_TRANSITION", "Phiên chưa chạy."); await StopSegment(session, ct); session.Status = WorkSessionStatus.Paused; break;
            case "resume":
                Require(session.Status == WorkSessionStatus.Paused, "INVALID_TRANSITION", "Phiên chưa tạm dừng.");
                ValidateExecutionStart(await State(uid, ct), session, task, true);
                session.Status = WorkSessionStatus.Running; session.SegmentStartedAt = Now; break;
            case "complete":
                Require(session.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused, "INVALID_TRANSITION", "Bắt đầu phiên trước khi hoàn thành.");
                await StopSegment(session, ct); session.Status = WorkSessionStatus.Completed; session.CompletedAt = Now; break;
            case "skip":
                Require(session.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Missed,
                    "INVALID_TRANSITION", "Phiên đã kết thúc."); await StopSegment(session, ct); session.Status = WorkSessionStatus.Skipped; break;
            default: throw new SPWPOS.Application.Authentication.AuthException("INVALID_ACTION", "Thao tác không hợp lệ.", 400);
        }
        var execution = await Mine<SessionExecution>(uid).SingleOrDefaultAsync(x => x.SessionId == id, ct);
        if (action == "start")
        {
            Require(execution is null, "INVALID_TRANSITION", "Phiên đã bắt đầu.", 409);
            execution = new() { UserId = uid, SessionId = id, StartedAt = Now }; db.SessionExecutions.Add(execution);
        }
        if (action is "start" or "resume")
        {
            Require(execution is not null, "INVALID_TRANSITION", "Không có lịch sử bắt đầu phiên.");
            db.ExecutionSegments.Add(new() { UserId = uid, ExecutionId = execution.Id, Start = Now });
        }
        if (execution is not null && action is "complete" or "skip") execution.EndedAt = Now;
        session.UpdatedAt = Now;
        task.UpdatedAt = Now;
    }, ct);
    public Task RecordDurationAsync(Guid uid, Guid id, DurationInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var session = await Find<WorkSession>(uid, id, ct);
        Require(session.Status == WorkSessionStatus.Completed && input.ActualMinutes is >= 0 and <= 10080, "INVALID_DURATION", "Chỉ điều chỉnh thời lượng phiên đã hoàn thành (0–10080 phút).");
        session.ActualSeconds = input.ActualMinutes * 60L;
        var execution = await Mine<SessionExecution>(uid).SingleOrDefaultAsync(x => x.SessionId == id, ct);
        Require(execution is not null, "INVALID_TRANSITION", "Không có lịch sử phiên thực hiện.");
        execution.ActualSeconds = session.ActualSeconds; execution.IsUserAdjusted = true; session.UpdatedAt = Now;
    }, ct);
    public async Task<ExecutionView?> GetExecutionAsync(Guid uid, Guid sessionId, CancellationToken ct)
    {
        await Find<WorkSession>(uid, sessionId, ct);
        var execution = await Mine<SessionExecution>(uid).AsNoTracking().SingleOrDefaultAsync(x => x.SessionId == sessionId, ct);
        if (execution is null) return null;
        var segments = await Mine<ExecutionSegment>(uid).Where(x => x.ExecutionId == execution.Id).OrderBy(x => x.Start)
            .Select(x => new ExecutionSegmentView(x.Start, x.End)).ToArrayAsync(ct);
        var running = segments.Where(x => x.End is null).Sum(x => Math.Max(0, (Now - x.Start).TotalSeconds));
        return new(sessionId, execution.StartedAt, execution.EndedAt, execution.TimerSeconds + (long)running,
            execution.ActualSeconds + (long)running, execution.IsUserAdjusted, segments);
    }
}
