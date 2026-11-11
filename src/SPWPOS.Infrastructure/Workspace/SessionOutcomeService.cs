using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public Task<SessionOutcomeResult> RecordSessionOutcomeAsync(Guid uid, Guid id, SessionOutcomeInput input, CancellationToken ct) =>
        Mutate(uid, async () =>
        {
            var session = await Find<WorkSession>(uid, id, ct);
            var previous = (await AppliedExtraTimes(uid, ct)).FirstOrDefault(x => x.SessionId == id);
            if (previous is not null)
            {
                Require(previous.Outcome == input, "TIME_REVIEW_ANSWERED", "Phiên này đã được xử lý. Hãy tải lại lịch.", 409);
                return new SessionOutcomeResult(id, session.TaskId, input.Outcome, previous.RecordedSeconds, input.RemainingMinutes);
            }
            var task = await Find<WorkTask>(uid, session.TaskId, ct);
            await ValidateExtraTimeTask(uid, task, ct);
            Require(session.End <= Now && session.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.NeedsReview or
                WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed or WorkSessionStatus.Missed,
                "SESSION_NOT_DUE", "Chỉ ghi nhận kết quả sau khi phiên kết thúc.", 409);
            Require((input.ActualMinutes is null or >= 0 and <= 10080) && (input.Outcome is "COMPLETED" or "PARTIAL" or "MISSED") &&
                (input.Outcome == "PARTIAL" ? input.RemainingMinutes is > 0 and <= 43200 : input.RemainingMinutes is null),
                "INVALID_OUTCOME", "Chọn kết quả và nhập số phút còn lại khi làm một phần.");
            await StopSegment(session, ct, session.End);
            var actual = input.ActualMinutes is { } minutes ? minutes * 60L : session.ActualSeconds;
            var missed = input.Outcome == "MISSED";
            Require(!missed || session.ActualSeconds == 0 && actual == 0, "RECORDED_WORK", "Phiên đã ghi nhận thời gian. Hãy chọn Làm một phần.", 409);
            Require(missed || input.ActualMinutes is not null || actual > 0,
                "ACTUAL_REQUIRED", "Nhập thời gian đã làm hoặc xác nhận thời gian từ bộ đếm.");
            session.ActualSeconds = actual;
            session.Status = missed ? WorkSessionStatus.Missed : WorkSessionStatus.Completed;
            session.CompletedAt = missed ? null : session.End;
            var execution = await Mine<SessionExecution>(uid).SingleOrDefaultAsync(e => e.SessionId == id, ct);
            if (execution is null)
            {
                execution = new() { UserId = uid, SessionId = id, StartedAt = session.Start };
                db.SessionExecutions.Add(execution);
            }
            execution.EndedAt ??= session.End;
            execution.ActualSeconds = actual;
            execution.IsUserAdjusted |= input.ActualMinutes != null || missed;
            if (!missed) task.Status = WorkTaskStatus.InProgress;
            task.UpdatedAt = Now;
            task.Version = Guid.NewGuid();
            if (input.Outcome == "COMPLETED") await AwardSessionExperienceAsync(uid, session, task, session.End, ct);
            var completedBefore = await Mine<WorkSession>(uid).Where(s => s.TaskId == task.Id && s.Status == WorkSessionStatus.Completed)
                .Select(s => s.Id).ToArrayAsync(ct);
            var confirmedAt = await WorkTimeConfirmationAt(uid, ct);
            var payload = new ExtraTimePayload(id, task.Id, task.Version, 0, [], confirmedAt, input.RemainingMinutes ?? 0,
                task.Progress, task.EstimatedMinutes, completedBefore.Append(id).Distinct().ToArray(), null,
                Outcome: input, RecordedSeconds: actual);
            db.PrivatePlanningProposals.Add(new() { UserId = uid, Kind = SessionOutcomeKind, Applied = true,
                ExpiresAt = Now, PayloadJson = JsonSerializer.Serialize(payload) });
            foreach (var notice in await Mine<InAppNotification>(uid).Where(n => n.Kind == "SESSION_REVIEW" && n.SessionId == id).ToArrayAsync(ct))
            { notice.ReadAt = Now; notice.TelegramPending = false; notice.NextTelegramAttemptAt = null; }
            return new SessionOutcomeResult(id, task.Id, input.Outcome, actual, input.RemainingMinutes);
        }, ct);
}
