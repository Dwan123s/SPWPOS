using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public async Task<SessionView[]> GetCatchUpAsync(Guid uid, CancellationToken ct)
    {
        var now = Now;
        return await (from session in Mine<WorkSession>(uid).AsNoTracking()
            join task in Mine<WorkTask>(uid).AsNoTracking() on session.TaskId equals task.Id
            where !task.IsDeleted && task.Status != WorkTaskStatus.Completed && task.Status != WorkTaskStatus.Cancelled && session.End < now &&
                (session.Status == WorkSessionStatus.Scheduled || session.Status == WorkSessionStatus.NeedsReview || session.Status == WorkSessionStatus.Missed) &&
                !db.SessionExecutions.Any(x => x.UserId == uid && x.SessionId == session.Id)
            orderby session.End descending
            select new SessionView(session.Id, task.Id, task.Name, session.Start, session.End, "NeedsReview", session.IsLocked,
                (int)(session.ActualSeconds / 60), session.ActualSeconds, null, session.ReasonCode)).Take(20).ToArrayAsync(ct);
    }

    public Task<CatchUpResult> CatchUpAsync(Guid uid, Guid id, CatchUpInput input, CancellationToken ct) => Mutate(uid, async () => {
        var session = await Find<WorkSession>(uid, id, ct); var task = await Find<WorkTask>(uid, session.TaskId, ct);
        var existing = await Mine<SessionExecution>(uid).SingleOrDefaultAsync(x => x.SessionId == id, ct);
        var replacement = await Mine<WorkSession>(uid).SingleOrDefaultAsync(x => x.RepairSourceSessionId == id, ct);
        if (existing is not null && session.Status is WorkSessionStatus.Completed or WorkSessionStatus.Missed)
            return new CatchUpResult(id, session.Status.ToString(), replacement?.Id, "Kết quả phiên này đã được ghi nhận.");
        Require(session.End < Now && session.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.NeedsReview or WorkSessionStatus.Missed,
            "SESSION_REVIEW_UNAVAILABLE", "Chỉ ghi nhận bù cho phiên đã qua và chưa có kết quả.", 409);
        Require(!task.IsDeleted && task.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled,
            "INVALID_TASK", "Công việc không còn hoạt động.", 409);
        Require(input.Outcome is "COMPLETED" or "PARTIAL" or "MISSED", "VALIDATION_ERROR", "Chọn kết quả thực tế của phiên.");
        if (input.Outcome == "PARTIAL") Require(input.Progress is > 0 and < 100 && input.Progress >= task.Progress && input.ActualMinutes is > 0 and <= 10080,
            "VALIDATION_ERROR", "Nhập tiến độ hiện tại (1–99%, không giảm) và số phút đã làm.");
        session.Status = input.Outcome == "MISSED" ? WorkSessionStatus.Missed : WorkSessionStatus.Completed;
        session.ActualSeconds = input.Outcome == "MISSED" ? 0 : input.Outcome == "PARTIAL" ? input.ActualMinutes!.Value * 60L : (long)(session.End - session.Start).TotalSeconds;
        session.CompletedAt = session.Status == WorkSessionStatus.Completed ? session.End : null;
        session.UpdatedAt = Now;
        db.SessionExecutions.Add(new() { UserId = uid, SessionId = id, StartedAt = session.Start, EndedAt = Now,
            ActualSeconds = session.ActualSeconds, IsUserAdjusted = true });
        if (input.Outcome != "MISSED") {
            task.Progress = input.Outcome == "PARTIAL" ? input.Progress!.Value : Math.Min(100, task.Progress + (int)Math.Ceiling(100 * (session.End - session.Start).TotalMinutes / task.EstimatedMinutes));
            task.Status = task.Progress == 100 ? WorkTaskStatus.Completed : WorkTaskStatus.InProgress;
            task.CompletedAt = task.Progress == 100 ? session.End : null; task.UpdatedAt = Now;
            if(task.Status==WorkTaskStatus.Completed)await ReleaseCompletedTaskSessions(uid,task.Id,ct);
            if (input.Outcome == "COMPLETED")
            {
                await AwardSessionExperienceAsync(uid, session, task, session.End, ct);
                if (!await db.TeamTaskAssignments.AnyAsync(x => x.UserId == uid && x.WorkTaskId == task.Id && x.Active, ct))
                    await AwardTaskExperienceAsync(uid, task, session.End, ct);
            }
        }
        await db.SaveChangesAsync(ct);
        if (input.Outcome == "COMPLETED") return new CatchUpResult(id, "Completed", null, "Mình đã ghi nhận kết quả. Bạn không cần bật timer để hoàn thành phiên.");
        var healed = await AutoHealScheduleAsync(uid, task, session, ct);
        return new CatchUpResult(id, session.Status.ToString(), healed?.Id,
            healed is null ? "Đã ghi nhận. Chưa tìm được khoảng trống phù hợp trong 48 giờ tới; mình giữ nguyên deadline và các lịch khác."
                : $"Đã ghi nhận và xếp phần còn lại vào {LocalWindow(healed.Start, healed.End, (await Preference(uid, ct)))}.");
    }, ct);

    private async Task<WorkSession?> AutoHealScheduleAsync(Guid uid, WorkTask task, WorkSession missed, CancellationToken ct)
    {
        var state = await State(uid, ct); var now = Now; var start = now.AddMinutes(5); var until = now.AddHours(48);
        if (task.IsHardDeadline && task.Deadline < until) until = task.Deadline;
        if (until <= start) return null;
        var bias = new[] { new BiasView("All", 0, 1, false) };
        var occupied = Occupied(state);
        var reserved = occupied.Where(x => x.TaskId == task.Id && x.End > now).Sum(x => (x.End - x.Start).TotalMinutes);
        var remaining = Math.Max(0, engine.Remaining(task, bias) - (int)reserved);
        var duration = Math.Min((int)Math.Ceiling((missed.End - missed.Start).TotalMinutes), remaining);
        if (duration < Math.Max(task.MinimumSessionMinutes, state.Preference.MinimumSessionMinutes) || duration > state.Preference.MaximumSessionMinutes) return null;
        var blocks = occupied.Select(x => new TimeSlot(x.Start.AddMinutes(-state.Preference.BreakMinutes), x.End.AddMinutes(state.Preference.BreakMinutes)));
        var gaps = engine.FreeSlots(state, start, until, blocks);
        foreach (var gap in gaps.OrderBy(x => x.Start)) {
            var from = task.WorkWindowStart is { } bound && bound > gap.Start ? bound : gap.Start;
            var to = from.AddMinutes(duration);
            if (to > gap.End || task.WorkWindowEnd is { } limit && to > limit) continue;
            try { ValidateSession(state, task.Id, from, to, occupied, Guid.Empty, false, false, false); }
            catch (SPWPOS.Application.Authentication.AuthException) { continue; }
            var session = new WorkSession { UserId = uid, TaskId = task.Id, Start = from, End = to, RepairSourceSessionId = missed.Id,
                ReasonCode = "AUTO_HEAL", CreatedAt = now, UpdatedAt = now };
            db.WorkSessions.Add(session);
            await AddNotification(uid, "AUTO_HEAL", "Đã xếp phiên bù", $"{task.Name}: {LocalWindow(from, to, state.Preference)}.",
                "auto-heal:" + missed.Id, task.Id, ct, sessionId: session.Id);
            return session;
        }
        await AddNotification(uid, "AUTO_HEAL_BLOCKED", "Phiên bù cần điều chỉnh", "Chưa có đủ thời gian phù hợp cho " + task.Name + " trong 48 giờ tới.",
            "auto-heal:" + missed.Id, task.Id, ct);
        return null;
    }
}
