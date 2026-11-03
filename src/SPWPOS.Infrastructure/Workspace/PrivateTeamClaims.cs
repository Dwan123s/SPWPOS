using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public Task<PrivateClaimView> PreviewTeamClaimAsync(Guid uid, Guid taskId, CancellationToken ct, bool allowExtendedDisplacement = false) => PreviewClaim(uid, taskId, false, ct, allowExtendedDisplacement);
    public Task<PrivateClaimView> PreviewTeamTaskChangeAsync(Guid uid, Guid taskId, CancellationToken ct) => PreviewClaim(uid, taskId, true, ct);
    private async Task<PrivateClaimView> PreviewClaim(Guid uid, Guid taskId, bool change, CancellationToken ct, bool extended = false)
    {
        var shared = await SharedTask(uid, taskId, ct); var member = await TeamMember(uid, shared.TeamId, ct);
        Require(change ? shared.AssigneeId == uid && shared.Status == "Assigned" && shared.PendingChangeJson != null : shared.AssigneeId == null && shared.Status == "Open",
            "TASK_UNAVAILABLE", "Công việc đã được nhận hoặc không có yêu cầu thay đổi.", 409);
        Require(change || shared.DesignatedUserId == null || shared.DesignatedUserId == uid, "TASK_DESIGNATED", "Task này được chỉ định cho thành viên khác.", 403);
        var state = await State(uid, ct);
        Require(state.Events.Length > 0, "TIMETABLE_REQUIRED", "Gửi và xác nhận thời gian biểu trước khi nhận việc.", 409);
        var data = change ? JsonSerializer.Deserialize<SharedTaskView>(shared.PendingChangeJson!)! : SharedDto(shared);
        Require(data.Deadline > Now.AddMinutes(5) && data.Deadline <= Now.AddDays(30), "TEAM_DEADLINE_RANGE", "Chỉ lập lịch nhận việc có deadline trong 30 ngày tới.", 409);
        var task = new WorkTask { UserId = uid, Name = data.Name, Description = data.Description, EstimatedMinutes = data.EstimatedMinutes, Deadline = data.Deadline,
            Priority = data.Priority, Splittable = data.Splittable, IsHardDeadline = true, CreationSource = "TEAM", CreatedAt = Now, UpdatedAt = Now,
            MinimumSessionMinutes = state.Preference.MinimumSessionMinutes };
        task.InheritedImportance = shared.ImportanceLevel; task.InheritedImportanceReason = shared.ImportanceReason;
        if (change)
        {
            var assignment = await db.TeamTaskAssignments.SingleAsync(x => x.TeamTaskId == taskId && x.UserId == uid && x.Active, ct);
            var original = state.Tasks.Single(x => x.Id == assignment.WorkTaskId);
            task.Id = original.Id; task.Progress = original.Progress; task.Status = original.Status; task.CreatedAt = original.CreatedAt;
            WriteImportance(task, new(original.ImportanceLevel, original.ImportanceSource, original.ImportanceReason, original.ImportanceConfidence), original.ImportanceConfirmedAt);
        }
        var candidate = state with { Tasks = state.Tasks.Where(x => x.Id != task.Id).Append(task).ToArray() };
        var start = Now.AddMinutes(5);
        if (state.Policy == null && state.Availability.Length == 0 && !state.Exceptions.Any(x => x.IsAvailable))
            return new(Guid.Empty, taskId, Now, [], [], !change, "Bạn đã có thời gian biểu cố định, nhưng chưa xác nhận giờ có thể làm việc. NORA cần khung sinh hoạt được bạn chấp nhận để phân biệt khoảng chưa có lịch với giờ thực sự có thể làm việc.",
                new("WORK_HOURS_NOT_CONFIRMED", task.EstimatedMinutes, 0, 0, 0, 0,
                    ["Chưa dùng preset hoặc tự coi khoảng ngoài giờ học là giờ làm việc.", "Xem đề xuất giờ sinh hoạt bên dưới, xác nhận một lần rồi NORA sẽ tính lại task này."], true));
        // Include displaced work all the way to its own deadline, within the supported horizon.
        var end = state.Tasks.Where(x => x.Status is WorkTaskStatus.Todo or WorkTaskStatus.InProgress).Select(x => x.Deadline).Append(task.Deadline).Max();
        if (end > start.AddDays(30)) end = start.AddDays(30);
        var request = new PlanInput(start, 30, [task.Id], true, UseCapacityFactor: false, UseEstimateBias: false) { WindowEnd = end };
        var plans = new List<PlanView>();
        var locked = candidate with { Sessions = candidate.Sessions.Select(x => x.TaskId != task.Id && x.Status == WorkSessionStatus.Scheduled ? CloneLocked(x) : x).ToArray() };
        var promoted = JsonSerializer.Deserialize<WorkTask>(JsonSerializer.Serialize(task))!; promoted.Priority = 4;
        var negotiable = candidate with { Tasks = candidate.Tasks.Where(x => x.Id != task.Id).Append(promoted).ToArray(),
            Sessions = candidate.Sessions.Select(x => x.TaskId != task.Id && candidate.Tasks.Any(t => t.Id == x.TaskId && t.Priority >= task.Priority) ? CloneLocked(x) : x).ToArray() };
        var attempts = new List<PlanView> { engine.Generate(locked, request, Now), engine.Generate(negotiable, request, Now) };
        if (extended) attempts.Add(engine.Generate(negotiable, request with { UrgentDisplacementDays = 30 }, Now));
        foreach (var plan in attempts)
        {
            if (!ClaimFeasible(candidate, task, plan)) continue;
            var signature = string.Join(";", plan.Sessions.OrderBy(x => x.Start).ThenBy(x => x.TaskId).Select(x => $"{x.TaskId}:{x.Start:O}:{x.End:O}"));
            if (plans.Any(p => string.Join(";", p.Sessions.OrderBy(x => x.Start).ThenBy(x => x.TaskId).Select(x => $"{x.TaskId}:{x.Start:O}:{x.End:O}")) == signature)) continue;
            plans.Add(plan);
        }
        if (plans.Count == 0) return FailedTeamClaim(taskId, task, candidate, attempts.ToArray(), start, change);
        plans = plans.OrderBy(p => state.Sessions.Where(x => x.TaskId != task.Id && x.Status == WorkSessionStatus.Scheduled && x.Start >= start && x.Start < end && !p.Sessions.Any(s => s.Id == x.Id && s.Preserved)).Sum(x => (x.End - x.Start).TotalMinutes))
            .ThenBy(p => p.Sessions.Count(x => !x.Preserved)).Take(3).ToList();
        var before = state.Sessions.Where(x => x.Status == WorkSessionStatus.Scheduled && x.Start >= start && x.Start < end).Select(CloneSession).ToArray();
        var analysis = await ExplainPrivateClaim(uid, task, candidate, plans.ToArray(), ct);
        var proposal = new TeamClaimProposal { UserId = uid, TeamTaskId = taskId, TeamTaskVersion = shared.Version, MembershipVersion = member.Version,
            Revision = state.Preference.Revision, ExpiresAt = Now.AddMinutes(15), PayloadJson = JsonSerializer.Serialize(new ClaimPayload(task, plans.ToArray(), before, change, analysis)) };
        db.TeamClaimProposals.Add(proposal); await db.SaveChangesAsync(ct);
        return ClaimView(proposal, candidate);
    }
    private PrivateClaimView FailedTeamClaim(Guid sharedId, WorkTask task, PlanningState state, PlanView[] attempts, DateTimeOffset start, bool change)
    {
        var end = task.Deadline;
        var remaining = engine.Remaining(task, [new("All", 0, 1, false)]);
        var occupied = state.Sessions.Where(x => x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed)
            .Select(x => new TimeSlot(x.Start.AddMinutes(-state.Preference.BreakMinutes), x.End.AddMinutes(state.Preference.BreakMinutes))).ToArray();
        var raw = engine.FreeSlots(state, start, end, occupied);
        var focus = engine.FocusSlots(state, start, end, occupied);
        var freeMinutes = (int)raw.Sum(x => (x.End - x.Start).TotalMinutes);
        var focusMinutes = (int)focus.Sum(x => (x.End - x.Start).TotalMinutes);
        var capacity = engine.CapacityDays(state, start, end, Now, false).Sum(x => x.SchedulableMinutes);
        var baseline = state with { Events = [], Sessions = [], Exceptions = state.Exceptions.Where(x => x.IsAvailable).ToArray() };
        var hasHours = engine.FreeSlots(baseline, start, end).Length > 0;
        var code = !hasHours ? "NO_WORK_HOURS_BEFORE_DEADLINE" : !task.Splittable && remaining > state.Preference.MaximumSessionMinutes ? "CONTINUOUS_TASK_EXCEEDS_SESSION_LIMIT"
            : focusMinutes < remaining || capacity < remaining ? "INSUFFICIENT_TIME_BEFORE_DEADLINE" : "SCHEDULE_CONSTRAINTS";
        var blockers = new List<string>();
        if (!hasHours) blockers.Add("Khung giờ làm việc đã xác nhận không có khoảng nào từ bây giờ đến deadline của task này.");
        else
        {
            blockers.Add($"Trước deadline có {freeMinutes} phút trống trong khung đã xác nhận; {focusMinutes} phút nằm trong khoảng đủ dài để tập trung, quỹ có thể xếp thêm theo giới hạn ngày là {capacity} phút.");
            if (!task.Splittable && remaining > state.Preference.MaximumSessionMinutes)
                blockers.Add($"Task yêu cầu {remaining} phút liên tục, nhưng mỗi phiên của tài khoản tối đa {state.Preference.MaximumSessionMinutes} phút. Cần được phép chia phiên hoặc điều chỉnh yêu cầu công việc.");
            else if (task.Splittable && remaining > state.Preference.MaximumSessionMinutes)
                blockers.Add($"Task cần ít nhất {(int)Math.Ceiling(remaining / (double)state.Preference.MaximumSessionMinutes)} phiên; mỗi phiên tối đa {state.Preference.MaximumSessionMinutes} phút và cần nghỉ {state.Preference.BreakMinutes} phút giữa các phiên.");
            if (state.Policy is { } policy && freeMinutes > focusMinutes) blockers.Add($"Các khe ngắn hơn {policy.FocusMinutes} phút không được dùng làm khoảng tập trung.");
            var protectedCount = state.Sessions.Count(x => x.Start < end && x.End > start && (x.IsLocked || x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused));
            if (protectedCount > 0) blockers.Add($"Có {protectedCount} phiên đã khóa hoặc đang thực hiện được giữ nguyên.");
            var best = attempts.OrderBy(p => p.Warnings.Sum(w => w.UnscheduledMinutes)).FirstOrDefault();
            if (best is not null) blockers.AddRange(best.Warnings.Take(3).Select(w => $"{w.TaskName}: {w.Message}"));
            if (code == "SCHEDULE_CONSTRAINTS") blockers.Add("Tổng phút trống chưa đảm bảo xếp được task: còn phải giữ độ dài phiên, nghỉ, phụ thuộc và deadline của các việc đã có.");
        }
        var missing = hasHours ? Math.Max(0, remaining - Math.Min(focusMinutes, capacity)) : 0;
        var failure = new ClaimFailureView(code, remaining, freeMinutes, focusMinutes, capacity, missing, blockers.Distinct().ToArray());
        return new(Guid.Empty, sharedId, Now, [], [], !change,
            !hasHours ? "Chưa có khung giờ làm việc được xác nhận trước deadline này. Đây là thông tin cần bổ sung, chưa phải kết luận bạn không có thời gian."
                : $"NORA chưa xếp được đủ {remaining} phút trước deadline với các điều kiện hiện tại. Task vẫn đang mở và lịch của bạn chưa thay đổi.", failure);
    }
    private static WorkSession CloneSession(WorkSession x) => JsonSerializer.Deserialize<WorkSession>(JsonSerializer.Serialize(x))!;
    private static WorkSession CloneLocked(WorkSession x) { var clone = CloneSession(x); clone.IsLocked = true; return clone; }
    private bool ClaimFeasible(PlanningState state, WorkTask task, PlanView plan)
    {
        if (plan.Warnings.Any(x => x.UnscheduledMinutes > 0 || x.Code == "SOFT_DEADLINE_EXCEEDED")) return false;
        if (plan.Sessions.Any(x => state.Tasks.Any(t => t.Id == x.TaskId && x.End > t.Deadline))) return false;
        if (plan.Sessions.Where(x => x.TaskId == task.Id).Sum(x => (x.End - x.Start).TotalMinutes) < engine.Remaining(task, [new("All", 0, 1, false)])) return false;
        return state.Sessions.Where(x => x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused || x.Status == WorkSessionStatus.Scheduled && (x.IsLocked || x.Start < Now.AddMinutes(5)))
            .All(x => plan.Sessions.Any(p => p.Id == x.Id && p.Preserved && p.Start == x.Start && p.End == x.End) || x.Start >= plan.End || x.End <= plan.Start);
    }
    private PrivateClaimView ClaimView(TeamClaimProposal proposal, PlanningState state)
    {
        var payload = JsonSerializer.Deserialize<ClaimPayload>(proposal.PayloadJson)!;
        var candidate = state with { Tasks = state.Tasks.Where(x => x.Id != payload.Task.Id).Append(payload.Task).ToArray() };
        return new(proposal.Id, proposal.TeamTaskId, proposal.ExpiresAt, payload.Plans.Select((plan, index) => new PrivateClaimOption(index,
            plan.Sessions.Any(x => !x.Preserved && x.TaskId != payload.Task.Id) ? "Dời phiên linh hoạt, giữ deadline" : "Xếp vào khoảng còn trống",
            PrivateSessions(candidate, plan.Sessions), PrivateSessions(state, payload.Before.Select(x => new ProposedSession(x.Id, x.TaskId, x.Start, x.End, x.ReasonCode, true))), plan.Explanations)).ToArray(), [], !payload.IsChange, payload.Analysis);
    }
    private async Task<string> ExplainPrivateClaim(Guid uid, WorkTask task, PlanningState state, PlanView[] plans, CancellationToken ct)
    {
        var first = plans[0]; var sessions = first.Sessions.Where(x => x.TaskId == task.Id).ToArray();
        var remaining = engine.Remaining(task, [new("All", 0, 1, false)]);
        var fallback = $"Công việc cần {task.EstimatedMinutes} phút, còn {remaining} phút theo tiến độ bạn xác nhận. " +
            $"NORA đã đối chiếu lịch riêng của bạn và tìm được {plans.Length} phương án, với {sessions.Length} phiên cho công việc mới trong phương án đầu tiên. " +
            "Các phiên này nằm trước deadline, giữ lịch cố định và phiên được bảo vệ. Xem giờ cụ thể bên dưới rồi xác nhận; lịch chưa thay đổi.";
        if (interpreter is not IPlanningAgentInterpreter model || !model.Capabilities.AiAvailable) return fallback;
        var context = JsonSerializer.Serialize(new { mode = "TEAM_CLAIM_REVIEW", sharedTask = new { task.Name, task.Description, task.EstimatedMinutes, task.Deadline, task.Priority, task.Progress },
            verified = new { fixedCommitments = state.Events.Count(x => x.Start < first.End && x.End > first.Start), maximumSessionMinutes = state.Preference.MaximumSessionMinutes,
                alternatives = plans.Select(p => new { sessions = PrivateSessions(state, p.Sessions), p.Explanations }) } });
        try
        {
            var reply = await RunJob(uid, "AI_REQUEST", () => model.InterpretAsync("Phân tích công việc và giải thích phương án xếp lịch dành riêng cho tôi từ kết quả đã kiểm chứng. Chưa lưu hoặc nhận việc.", context, [], state.Preference.TimeZoneId, Now, ct), ct);
            if (!HasAgentActions(reply) && reply.Question.Length == 0 && reply.Reply.Length is >= 80 and <= 1800 &&
                !ChatMatch(reply.Reply, @"\b(?:da|vua) (?:tu dong )?(?:luu|doi lich|cap nhat|ap dung|nhan task)\b").Success)
                return reply.Reply;
        }
        catch (AuthException) { /* The verified schedule remains usable if provider advice is unavailable. */ }
        return fallback;
    }
    public Task ConfirmTeamClaimAsync(Guid uid, Guid proposalId, ClaimConfirmInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var row = await Find<TeamClaimProposal>(uid, proposalId, ct); var shared = await SharedTask(uid, row.TeamTaskId, ct); var member = await TeamMember(uid, shared.TeamId, ct);
        if (row.Status == "Applied") { Require(shared.AssigneeId == uid, "CLAIM_STALE", "Phân công đã thay đổi.", 409); return; }
        var state = await State(uid, ct); var payload = JsonSerializer.Deserialize<ClaimPayload>(row.PayloadJson)!;
        Require(row.Status == "Proposed" && row.ExpiresAt > Now && row.TeamTaskVersion == shared.Version && row.MembershipVersion == member.Version && row.Revision == state.Preference.Revision,
            "CLAIM_STALE", "Công việc, thành viên hoặc lịch đã thay đổi. Hãy xem phương án mới.", 409);
        Require(payload.IsChange ? shared.AssigneeId == uid && shared.PendingChangeJson != null : shared.AssigneeId == null && shared.Status == "Open", "TASK_UNAVAILABLE", "Task đã được nhận.", 409);
        Require(payload.IsChange || shared.DesignatedUserId == null || shared.DesignatedUserId == uid, "TASK_DESIGNATED", "Task này được chỉ định cho thành viên khác.", 403);
        Require(input.Option >= 0 && input.Option < payload.Plans.Length, "INVALID_OPTION", "Phương án không hợp lệ.");
        var plan = payload.Plans[input.Option];
        Require(plan.PriorityPolicy == null || plan.PriorityPolicy == (state.EisenhowerScheduling ? "eisenhower-v1" : "legacy"),
            "CLAIM_STALE", "Chính sách ưu tiên đã thay đổi. Hãy xem phương án mới.", 409);
        var candidate = state with { Tasks = state.Tasks.Where(x => x.Id != payload.Task.Id).Append(payload.Task).ToArray() };
        Require(ClaimFeasible(candidate, payload.Task, plan), "CLAIM_INFEASIBLE", "Phương án không còn hợp lệ.", 409);
        // Acquire the shared task and membership versions BEFORE any calendar writes.
        shared.Version = Guid.NewGuid(); member.MutationVersion = Guid.NewGuid(); await db.SaveChangesAsync(ct);
        if (payload.IsChange)
        {
            var personal = state.Tasks.Single(x => x.Id == payload.Task.Id);
            Require(!state.Sessions.Any(x => x.TaskId == personal.Id && x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused), "SESSION_ACTIVE", "Kết thúc phiên đang thực hiện trước khi đổi yêu cầu.", 409);
            db.TeamPlanningWrite = true;
            personal.Name = payload.Task.Name; personal.Description = payload.Task.Description; personal.EstimatedMinutes = payload.Task.EstimatedMinutes; personal.Deadline = payload.Task.Deadline;
            personal.Priority = payload.Task.Priority; personal.Splittable = payload.Task.Splittable; personal.UpdatedAt = Now;
            candidate = state;
            shared.Name = personal.Name; shared.Description = personal.Description; shared.EstimatedMinutes = personal.EstimatedMinutes; shared.Deadline = personal.Deadline; shared.Priority = personal.Priority; shared.Splittable = personal.Splittable; shared.PendingChangeJson = null;
        }
        else
        {
            db.WorkTasks.Add(payload.Task); shared.AssigneeId = uid; shared.Status = "Assigned";
            db.TeamTaskAssignments.Add(new() { TeamTaskId = shared.Id, UserId = uid, WorkTaskId = payload.Task.Id, ProposalId = row.Id });
        }
        var proposal = await StoreAgentPlanAsync(uid, plan, candidate, "NEW_TASK", ct);
        await ApplyPlanCoreAsync(uid, proposal, plan, candidate, ct);
        row.Status = "Applied"; row.TeamTaskVersion = shared.Version; row.AppliedRevision = (await Preference(uid, ct)).Revision = Guid.NewGuid();
        TeamLog(uid, shared.TeamId, shared.Id, payload.IsChange ? "TASK_CHANGE_ACCEPTED" : "TASK_CLAIMED");
    }, ct, false);
    public Task CancelTeamClaimAsync(Guid uid, Guid proposalId, CancellationToken ct) => Mutate(uid, async () =>
    {
        var row = await Find<TeamClaimProposal>(uid, proposalId, ct); Require(row.Status != "Applied", "CLAIM_APPLIED", "Dùng hoàn tác hoặc trả việc sau khi đã nhận.", 409); row.Status = "Cancelled";
    }, ct, false);
    public Task UndoTeamClaimAsync(Guid uid, Guid proposalId, CancellationToken ct) => Mutate(uid, async () =>
    {
        var row = await Find<TeamClaimProposal>(uid, proposalId, ct); var payload = JsonSerializer.Deserialize<ClaimPayload>(row.PayloadJson)!;
        Require(!payload.IsChange && row.Status == "Applied", "UNDO_UNAVAILABLE", "Không thể hoàn tác thay đổi yêu cầu; hãy xem phương án mới hoặc trả việc.", 409);
        var shared = await SharedTask(uid, row.TeamTaskId, ct); var member = await TeamMember(uid, shared.TeamId, ct); var state = await State(uid, ct);
        Require(row.AppliedRevision == state.Preference.Revision && row.TeamTaskVersion == shared.Version && shared.AssigneeId == uid && shared.Progress == 0 && shared.PendingChangeJson == null, "UNDO_STALE", "Lịch hoặc công việc đã thay đổi. Hãy trả việc nếu cần.", 409);
        var assignment = await db.TeamTaskAssignments.SingleAsync(x => x.ProposalId == row.Id && x.Active && x.UserId == uid, ct);
        var addedIds = payload.Plans.SelectMany(x => x.Sessions).Where(x => !x.Preserved).Select(x => x.Id).ToArray();
        var added = state.Sessions.Where(x => addedIds.Contains(x.Id)).ToArray();
        Require(added.All(x => x.Status == WorkSessionStatus.Scheduled && x.Start > Now && !x.IsLocked) && payload.Before.All(x => x.Start > Now), "UNDO_STARTED", "Một phiên đã bắt đầu hoặc đã khóa; không thể hoàn tác.", 409);
        Require(!state.Sessions.Any(x => x.TaskId == assignment.WorkTaskId && x.Status != WorkSessionStatus.Scheduled && x.Status != WorkSessionStatus.Cancelled), "UNDO_STARTED", "Công việc đã được thực hiện.", 409);
        shared.Version = Guid.NewGuid(); member.MutationVersion = Guid.NewGuid(); await db.SaveChangesAsync(ct);
        foreach (var session in added) session.Status = WorkSessionStatus.Cancelled;
        foreach (var previous in payload.Before)
        {
            var saved = await Find<WorkSession>(uid, previous.Id, ct); saved.Status = previous.Status; saved.Start = previous.Start; saved.End = previous.End;
        }
        db.TeamPlanningWrite = true; var personal = await Find<WorkTask>(uid, assignment.WorkTaskId, ct); personal.IsDeleted = true; personal.DeletedAt = Now;
        assignment.Active = false; assignment.Version = Guid.NewGuid(); shared.AssigneeId = null; shared.Status = "Open"; shared.Progress = 0; row.Status = "Undone";
        (await Preference(uid, ct)).Revision = Guid.NewGuid(); TeamLog(uid, shared.TeamId, shared.Id, "CLAIM_UNDONE");
    }, ct, false);
    public async Task<TeamReturnPreview> PreviewTeamReturnAsync(Guid uid, Guid taskId, CancellationToken ct)
    {
        var shared = await SharedTask(uid, taskId, ct);
        Require(shared.AssigneeId == uid && shared.Status == "Assigned", "TEAM_FORBIDDEN", "Chỉ người nhận được xem tác động trả việc.", 403);
        var assignment = await db.TeamTaskAssignments.SingleAsync(x => x.TeamTaskId == taskId && x.UserId == uid && x.Active, ct);
        var state = await State(uid, ct);
        return new(taskId, shared.Version, state.Preference.Revision, PrivateSessions(state, state.Sessions.Where(x => x.TaskId == assignment.WorkTaskId && x.Status == WorkSessionStatus.Scheduled).Select(x => new ProposedSession(x.Id, x.TaskId, x.Start, x.End, x.ReasonCode, true))));
    }
    public Task ReturnTeamTaskAsync(Guid uid, Guid taskId, Guid version, CancellationToken ct, Guid? revision = null) => Mutate(uid, async () =>
    {
        var task = await SharedTask(uid, taskId, ct); var member = await TeamMember(uid, task.TeamId, ct);
        Require(task.AssigneeId == uid && task.Version == version && task.Status == "Assigned", "TASK_STALE", "Chỉ người đang nhận việc mới có thể trả đúng phiên bản.", 409);
        Require(revision is not null && revision == (await Preference(uid, ct)).Revision, "PLAN_STALE", "Xem lại các phiên sẽ hủy trước khi xác nhận trả việc.", 409);
        var assignment = await db.TeamTaskAssignments.SingleAsync(x => x.TeamTaskId == taskId && x.UserId == uid && x.Active, ct);
        var sessions = await Mine<WorkSession>(uid).Where(x => x.TaskId == assignment.WorkTaskId).ToArrayAsync(ct);
        Require(!sessions.Any(x => x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused || x.Status == WorkSessionStatus.Scheduled && (x.IsLocked || x.Start <= Now)), "SESSION_PROTECTED", "Cần kết thúc phiên đang chạy hoặc mở khóa phiên trước khi trả việc.", 409);
        task.Version = Guid.NewGuid(); member.MutationVersion = Guid.NewGuid(); await db.SaveChangesAsync(ct);
        foreach (var session in sessions.Where(x => x.Status == WorkSessionStatus.Scheduled)) session.Status = WorkSessionStatus.Cancelled;
        db.TeamPlanningWrite = true;
        var personal = await Find<WorkTask>(uid, assignment.WorkTaskId, ct); personal.Status = WorkTaskStatus.Cancelled; personal.UpdatedAt = Now;
        assignment.Active = false; assignment.Version = Guid.NewGuid(); task.AssigneeId = null; task.Status = "Open"; task.Progress = 0; task.PendingChangeJson = null;
        task.CompletionRequestId = null; task.CompletionApproved = null; task.CompletionSubmittedAt = null; task.CompletionReviewedAt = null; task.CompletionReviewedBy = null; task.CompletionReviewNote = null; task.ProgressBeforeReview = 0;
        (await Preference(uid, ct)).Revision = Guid.NewGuid(); TeamLog(uid, task.TeamId, task.Id, "TASK_RETURNED");
    }, ct, false);
    public Task UpdateTeamProgressAsync(Guid uid, Guid taskId, TeamProgressInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var shared = await SharedTask(uid, taskId, ct);
        if (shared.AssigneeId == uid && input.Progress == 100 && shared.Status is "PendingReview" or "Completed") return;
        Require(shared.AssigneeId == uid && shared.Version == input.Version && shared.Status == "Assigned" && input.Progress is >= 0 and <= 100, "TASK_STALE", "Chỉ người nhận được cập nhật tiến độ của phiên bản hiện tại.", 409);
        var member = await TeamMember(uid, shared.TeamId, ct); shared.Version = Guid.NewGuid(); member.MutationVersion = Guid.NewGuid(); await db.SaveChangesAsync(ct);
        var assignment = await db.TeamTaskAssignments.SingleAsync(x => x.TeamTaskId == taskId && x.UserId == uid && x.Active, ct);
        var personal = await Find<WorkTask>(uid, assignment.WorkTaskId, ct);
        await SetTaskStatusCore(uid, personal.Id, new(input.Progress == 100 ? "Completed" : "InProgress", input.Progress), ct);
        (await Preference(uid, ct)).Revision = Guid.NewGuid();
    }, ct, false);
    public async Task<TeamTaskDraft> ProposeTeamTaskChangeAsync(Guid uid, Guid taskId, TeamTextInput input, CancellationToken ct)
    {
        var current = await SharedTask(uid, taskId, ct); await TeamMember(uid, current.TeamId, ct, true); var version = current.Version;
        Require(current.Status is not ("Completed" or "PendingReview"), "TASK_COMPLETED", "Không đổi yêu cầu công việc đang chờ duyệt hoặc đã hoàn thành.", 409);
        var draft = await InterpretSharedDraft(uid, current.TeamId, input.Text, SharedDto(current), ct);
        if (draft is null) return new(Guid.Empty, "Chưa thay đổi yêu cầu.", "Hãy nêu cụ thể yêu cầu và deadline mới.", null);
        var member = await TeamMember(uid, current.TeamId, ct, true);
        var row = new PrivatePlanningProposal { UserId = uid, Kind = "TEAM_CHANGE", ExpiresAt = Now.AddMinutes(20), PayloadJson = JsonSerializer.Serialize(new TeamDraftPayload(current.TeamId, member.Version, draft with { Version = version })) };
        db.PrivatePlanningProposals.Add(row); await db.SaveChangesAsync(ct);
        return new(row.Id, "Xem yêu cầu mới trước khi gửi. Người nhận sẽ xác nhận lịch riêng; yêu cầu hiện tại còn hiệu lực đến lúc đó.", "", draft);
    }
    public Task RejectTeamTaskChangeAsync(Guid uid, Guid taskId, Guid version, CancellationToken ct) => Mutate(uid, async () =>
    {
        var shared = await SharedTask(uid, taskId, ct); var member = await TeamMember(uid, shared.TeamId, ct);
        Require(shared.Version == version && shared.AssigneeId == uid, "TASK_STALE", "Chỉ người nhận mới có thể từ chối thay đổi.", 409);
        shared.PendingChangeJson = null; shared.Version = Guid.NewGuid(); member.MutationVersion = Guid.NewGuid(); TeamLog(uid, shared.TeamId, shared.Id, "TASK_CHANGE_REJECTED");
    }, ct, false);
}
