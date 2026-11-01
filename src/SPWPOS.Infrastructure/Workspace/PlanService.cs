using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private void ValidPlan(PlanInput input) => Require(input.HorizonDays is >= 1 and <= 30 && input.Start >= Now.AddDays(-1) && input.Start <= Now.AddDays(30),
        "INVALID_HORIZON", "Chọn thời điểm trong 30 ngày tới và phạm vi 1–30 ngày.");
    public Task<PlanView> GeneratePlanAsync(Guid uid, PlanInput input, CancellationToken ct) => RunJob(uid, "PLAN_GENERATION", () => Mutate(uid, async () =>
    {
        ValidPlan(input); var state = await State(uid, ct);
        if (input.AffectedTaskIds is not null) Require(input.AffectedTaskIds.All(id => state.Tasks.Any(t => t.Id == id)), "NOT_FOUND", "Công việc không thuộc tài khoản.", 404);
        // Leave time for review; an immediate start would already be in the past at Apply.
        var earliestStart = Now.AddMinutes(5);
        var plan = engine.Generate(state, input with { Start = input.Start < earliestStart ? earliestStart : input.Start }, Now);
        var proposal = new PlanProposal { UserId = uid, CreatedAt = Now, ExpiresAt = Now.AddMinutes(30), Revision = state.Preference.Revision };
        proposal.PreviousPlanId = await Mine<PlanProposal>(uid).Where(x => x.Status == "APPLIED").OrderByDescending(x => x.AppliedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        proposal.Kind = input.IsReschedule || input.AffectedTaskIds is not null || proposal.PreviousPlanId is not null ? "RESCHEDULE" : "INITIAL";
        proposal.TriggerEvent = input.TriggerEvent; proposal.HorizonStart = plan.Start; proposal.HorizonEnd = plan.End;
        plan = plan with { Id = proposal.Id, Kind = proposal.Kind, PreviousPlanId = proposal.PreviousPlanId };
        proposal.PayloadJson = JsonSerializer.Serialize(plan); db.PlanProposals.Add(proposal);
        StorePlanDecisions(uid, proposal, plan, state); return plan;
    }, ct, false), ct);
    public async Task<PlanView> GetPlanAsync(Guid uid, Guid id, CancellationToken ct)
    {
        var proposal = await Find<PlanProposal>(uid, id, ct); var plan = JsonSerializer.Deserialize<PlanView>(proposal.PayloadJson)!;
        return plan with { IsApplied = proposal.IsApplied, Status = proposal.Status, Kind = proposal.Kind, PreviousPlanId = proposal.PreviousPlanId, AppliedAt = proposal.AppliedAt };
    }
    public async Task<PlanWarning[]> DetectConflictsAsync(Guid uid, PlanInput input, CancellationToken ct)
    {
        ValidPlan(input); var state = await State(uid, ct); var start = input.Start < Now ? Now : input.Start;
        var sessions = state.Sessions.Where(x => x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused).ToArray();
        var warnings = new List<PlanWarning>(); var biases = engine.Biases(state);
        var completion = engine.CompletionTimes(state, Occupied(state), biases, Now);
        foreach (var session in sessions.Where(x => x.End > start && x.Start < start.AddDays(input.HorizonDays)))
        {
            var task = state.Tasks.FirstOrDefault(x => x.Id == session.TaskId); if (task is null) continue;
            void Warn(string code, string message) => warnings.Add(new(task.Id, task.Name, code, message, 0));
            if (session.End > task.Deadline) Warn("DEADLINE_CONFLICT", "Phiên kết thúc sau deadline mới của công việc.");
            if (!engine.FreeSlots(state, session.Start, session.End).Any(x => x.Start <= session.Start && x.End >= session.End))
                Warn("OUTSIDE_AVAILABILITY", "Phiên xung đột với giờ rảnh, ngoại lệ hoặc sự kiện cố định.");
            if (sessions.Any(x => x.Id != session.Id && x.Start < session.End && session.Start < x.End)) Warn("SESSION_OVERLAP", "Hai phiên làm việc đang trùng giờ.");
            foreach (var edge in state.Dependencies.Where(x => x.TaskId == task.Id))
            {
                if (!completion.TryGetValue(edge.PredecessorId, out var finished) || finished > session.Start)
                    Warn("DEPENDENCY_CONFLICT", "Công việc tiền nhiệm chưa có đủ thời gian trước phiên này.");
            }
        }
        return warnings.DistinctBy(x => (x.TaskId, x.Code)).ToArray();
    }
    public Task ApplyPlanAsync(Guid uid, Guid id, CancellationToken ct) => Mutate(uid, async () =>
    {
        var proposal = await Find<PlanProposal>(uid, id, ct); var state = await State(uid, ct);
        Require(!proposal.IsApplied && proposal.Status == "PROPOSED" && proposal.ExpiresAt > Now, "PLAN_EXPIRED", "Kế hoạch đã dùng hoặc hết hạn. Hãy tạo lại.", 409);
        Require(proposal.Revision == state.Preference.Revision, "PLAN_STALE", "Dữ liệu đã thay đổi. Hãy tạo lại kế hoạch.", 409);
        var plan = JsonSerializer.Deserialize<PlanView>(proposal.PayloadJson)!;
        Require(plan.NoteContext is null, "NOTE_PLAN_CONFIRMATION_REQUIRED", "Lưu đề xuất này từ màn hình ghi chú để tạo đủ sự kiện và công việc.", 409);
        await ApplyPlanCoreAsync(uid, proposal, plan, state, ct);
    }, ct);
    private async Task ApplyPlanCoreAsync(Guid uid, PlanProposal proposal, PlanView plan, PlanningState state, CancellationToken ct)
    {
        Require(!plan.HasDeadlineProtection || !plan.Warnings.Any(x => x.UnscheduledMinutes > 0 || x.Code == "SOFT_DEADLINE_EXCEEDED"),
            "DEADLINE_PROTECTION_INFEASIBLE", "Chưa đủ thời gian để hoàn thành việc khẩn hoặc gần hạn cùng yêu cầu mới. Lịch cũ được giữ nguyên; hãy điều chỉnh thời lượng, giờ rảnh hoặc điều kiện của việc mới.", 409);
        Require(!plan.HasUrgentReplan || !plan.Warnings.Any(x => x.UnscheduledMinutes > 0 || x.Code == "SOFT_DEADLINE_EXCEEDED"),
            "URGENT_REPLAN_INFEASIBLE", "Chưa thể nhường giờ cho việc khẩn mà vẫn hoàn thành các công việc còn lại đúng hạn. Lịch cũ được giữ nguyên; hãy điều chỉnh giờ rảnh, thời lượng hoặc xem lại các điều kiện.", 409);
        var id = proposal.Id;
        var retained = plan.Sessions.Where(x => x.Preserved).Select(x => x.Id).ToHashSet();
        // Sessions outside the bounded proposal horizon remain untouched.
        var replacements = await Mine<WorkSession>(uid).Where(x => x.Status == WorkSessionStatus.Scheduled && x.Start >= plan.Start && x.Start < plan.End).ToArrayAsync(ct);
        foreach (var previous in replacements.Where(x => !retained.Contains(x.Id))) previous.Status = WorkSessionStatus.Cancelled;
        var occupied = state.Sessions.Where(x => x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused || x.Status == WorkSessionStatus.Scheduled && (!replacements.Any(r => r.Id == x.Id) || retained.Contains(x.Id)))
            .Select(x => new ProposedSession(x.Id, x.TaskId, x.Start, x.End, x.ReasonCode, true)).ToList();
        foreach (var session in plan.Sessions.Where(x => !x.Preserved).OrderBy(x => x.Start))
        {
            ValidateSession(state, session.TaskId, session.Start, session.End, occupied, Guid.Empty, false, plan.UseEstimateBias);
            var decision = await Mine<PlanItem>(uid).SingleOrDefaultAsync(x => x.PlanId == id && x.Id == session.Id, ct);
            var item = new WorkSession { Id = session.Id, UserId = uid, TaskId = session.TaskId, Start = session.Start, End = session.End, ReasonCode = session.ReasonCode,
                SourceItemId = decision?.Id, CreatedAt = Now, UpdatedAt = Now };
            db.WorkSessions.Add(item); occupied.Add(session);
        }
        proposal.IsApplied = true;
        proposal.Status = "APPLIED"; proposal.AppliedAt = Now;
        foreach (var previous in await Mine<PlanProposal>(uid).Where(x => x.Id != id && x.Status == "APPLIED").ToArrayAsync(ct)) previous.Status = "SUPERSEDED";
        await AddNotification(uid, "SCHEDULE_CHANGE", "Kế hoạch đã cập nhật", $"Đã áp dụng {plan.Sessions.Count(x => !x.Preserved)} phiên mới. {plan.Warnings.Length} công việc có cảnh báo.", "plan:" + id, null, ct, planId: id);
    }
    private void ValidateSession(PlanningState state, Guid taskId, DateTimeOffset start, DateTimeOffset end,
        IEnumerable<ProposedSession> occupied, Guid excludeId, bool dependenciesMustBeCompleted, bool useEstimateBias = true, bool suggestAlternatives = true)
    {
        var task = state.Tasks.SingleOrDefault(x => x.Id == taskId);
        Require(task is not null && !task.IsDeleted && task.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled, "INVALID_TASK", "Công việc không thể xếp lịch.");
        Require(start >= Now && end > start && (!task!.IsHardDeadline || end <= task.Deadline), "DEADLINE_CONFLICT", "Phiên phải ở tương lai và không vượt deadline cứng.", 409);
        Require(PlanningEngine.InsideWorkWindow(task!, start, end), "TASK_WORK_WINDOW", "Phiên nằm ngoài khung giờ thực hiện đã yêu cầu cho công việc.", 409);
        var duration = (end - start).TotalMinutes;
        var others = occupied.Where(x => x.Id != excludeId).ToArray();
        var bias = useEstimateBias ? engine.Biases(state) : [new BiasView("All", 0, 1, false)];
        var remaining = engine.Remaining(task, bias) - others.Where(x => x.TaskId == taskId).Sum(x => (x.End - x.Start).TotalMinutes);
        Require((duration >= Math.Max(state.Preference.MinimumSessionMinutes, task.MinimumSessionMinutes) || duration == remaining && remaining > 0) && duration <= state.Preference.MaximumSessionMinutes,
            "SESSION_LENGTH", "Thời lượng phiên vượt giới hạn.");
        RequireSessionTime(engine.FreeSlots(state, start, end).Any(x => x.Start <= start && x.End >= end), "OUTSIDE_AVAILABILITY", "Phiên ngoài thời gian rảnh hoặc trùng sự kiện cố định.",
            state, taskId, start, end, occupied, excludeId, dependenciesMustBeCompleted, useEstimateBias, suggestAlternatives);
        RequireSessionTime(!others.Any(x => x.Start < end && start < x.End), "SESSION_OVERLAP", "Phiên làm việc trùng lịch.",
            state, taskId, start, end, occupied, excludeId, dependenciesMustBeCompleted, useEstimateBias, suggestAlternatives);
        RequireSessionTime(!others.Any(x => x.Start < end.AddMinutes(state.Preference.BreakMinutes) && start.AddMinutes(-state.Preference.BreakMinutes) < x.End),
            "SESSION_BREAK_CONFLICT", "Cần giữ thời gian nghỉ giữa các phiên làm việc.",
            state, taskId, start, end, occupied, excludeId, dependenciesMustBeCompleted, useEstimateBias, suggestAlternatives);
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, zone).DateTime);
        double MinutesOnDay(DateTimeOffset from, DateTimeOffset to)
        {
            var first = PlanningEngine.At(day, 0, zone); var last = PlanningEngine.At(day.AddDays(1), 0, zone);
            var a = from > first ? from : first; var b = to < last ? to : last;
            return b > a ? (b - a).TotalMinutes : 0;
        }
        var elapsed = state.Sessions.Where(x => x.Status == WorkSessionStatus.Completed).Sum(x => MinutesOnDay(x.Start, x.End));
        Require(elapsed + others.Sum(x => MinutesOnDay(x.Start, x.End)) + duration <= state.Preference.MaximumDailyMinutes,
            "DAILY_LIMIT", "Tổng thời gian làm việc vượt giới hạn mỗi ngày.", 409);
        var completion = engine.CompletionTimes(state, others, bias, Now);
        foreach (var edge in state.Dependencies.Where(x => x.TaskId == taskId))
        {
            var actualCompletion = dependenciesMustBeCompleted ? engine.CompletionTimes(state, [], bias, Now) : completion;
            Require(actualCompletion.TryGetValue(edge.PredecessorId, out var finished) && finished <= start,
                "DEPENDENCY_CONFLICT", "Công việc tiền nhiệm chưa thể hoàn thành trước phiên này.", 409);
        }
        foreach (var edge in state.Dependencies.Where(x => x.PredecessorId == taskId))
            Require(!others.Any(x => x.TaskId == edge.TaskId && x.Start < end), "DEPENDENCY_CONFLICT", "Phiên mới kết thúc sau lúc bắt đầu công việc phụ thuộc.", 409);
    }
    private static ProposedSession[] Occupied(PlanningState state) => state.Sessions.Where(x => x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused)
        .Select(x => new ProposedSession(x.Id, x.TaskId, x.Start, x.End, x.ReasonCode, true)).ToArray();
    public Task EditSessionAsync(Guid uid, Guid id, SessionEdit input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var session = await Find<WorkSession>(uid, id, ct); var state = await State(uid, ct);
        Require(session.Status == WorkSessionStatus.Scheduled && !session.IsLocked, "SESSION_NOT_EDITABLE", "Chỉ chỉnh sửa phiên chưa bắt đầu và chưa khóa.", 409);
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        state = await CalendarStateAsync(uid, state, PlanningEngine.At(LocalDate(input.Start, state.Preference), 0, zone), input.End.AddDays(8), ct);
        ValidateSession(state, session.TaskId, input.Start, input.End, Occupied(state), id, false);
        session.Start = input.Start; session.End = input.End; session.ReasonCode = "USER_ADJUSTMENT";
        await AddNotification(uid, "SCHEDULE_CHANGED", "Phiên đã được chỉnh sửa", "Đã cập nhật thời gian của một phiên làm việc.", "edit:" + Guid.NewGuid(), session.TaskId, ct);
    }, ct);
    public Task DeleteSessionAsync(Guid uid, Guid id, CancellationToken ct) => Mutate(uid, async () =>
    {
        var session = await Find<WorkSession>(uid, id, ct);
        if (session.Status == WorkSessionStatus.Cancelled) return;
        Require(session.Status == WorkSessionStatus.Scheduled && session.ActualSeconds == 0 && session.SegmentStartedAt is null &&
            !await Mine<SessionExecution>(uid).AnyAsync(x => x.SessionId == id, ct),
            "SESSION_NOT_DELETABLE", "Chỉ xóa phiên chưa bắt đầu và chưa có lịch sử thực hiện.", 409);
        Require(!session.IsLocked, "SESSION_LOCKED", "Mở khóa phiên trước khi xóa khỏi lịch.", 409);
        session.Status = WorkSessionStatus.Cancelled;
        foreach (var notification in await Mine<InAppNotification>(uid).Where(x => x.SessionId == id).ToArrayAsync(ct))
        { notification.IsVisible = false; notification.EmailPending = false; notification.TelegramPending = false; }
    }, ct);
    public Task LockSessionAsync(Guid uid, Guid id, bool locked, CancellationToken ct) => Mutate(uid, async () =>
    {
        var session = await Find<WorkSession>(uid, id, ct); Require(session.Status == WorkSessionStatus.Scheduled && session.Start >= Now, "SESSION_NOT_EDITABLE", "Chỉ khóa phiên trong tương lai.");
        session.IsLocked = locked;
    }, ct);
    public Task SplitSessionAsync(Guid uid, Guid id, SplitInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var session = await Find<WorkSession>(uid, id, ct); var task = await Find<WorkTask>(uid, session.TaskId, ct); var preference = await Preference(uid, ct);
        Require(task.Splittable && session.Status == WorkSessionStatus.Scheduled && !session.IsLocked && session.Start >= Now, "CANNOT_SPLIT", "Phiên không thể chia nhỏ.");
        var minimum = Math.Max(task.MinimumSessionMinutes, preference.MinimumSessionMinutes); var midpoint = session.Start.AddMinutes(input.FirstMinutes);
        Require(input.FirstMinutes >= minimum && (session.End - midpoint).TotalMinutes >= minimum, "SESSION_LENGTH", "Mỗi phần phải đủ thời lượng tối thiểu.");
        var state = await State(uid, ct);
        var split = new WorkSession { UserId = uid, TaskId = session.TaskId, Start = midpoint.AddMinutes(preference.BreakMinutes), End = session.End.AddMinutes(preference.BreakMinutes), ReasonCode = "USER_SPLIT", CreatedAt = Now, UpdatedAt = Now };
        var occupied = Occupied(state).Where(x => x.Id != id).ToArray();
        ValidateSession(state, session.TaskId, session.Start, midpoint, occupied, id, false);
        ValidateSession(state, session.TaskId, split.Start, split.End,
            occupied.Append(new(id, session.TaskId, session.Start, midpoint, "USER_SPLIT", true)), Guid.Empty, false);
        session.End = midpoint; session.ReasonCode = "USER_SPLIT"; db.WorkSessions.Add(split);
    }, ct);
    // Ad-hoc analysis is a read. Learning and risk history are saved on mutations.
    public async Task<IntelligenceView> AnalyzeAsync(Guid uid, PlanInput input, CancellationToken ct)
    { ValidPlan(input); return engine.Analyze(await State(uid, ct), input, Now); }
    public async Task<ScenarioView> ScenarioAsync(Guid uid, ScenarioInput input, CancellationToken ct)
    {
        ValidPlan(input.Plan);
        var inputs = new[] { input.NewTask }.Concat(input.AdditionalTasks ?? []).ToArray();
        Require(inputs.Length <= 12, "INVALID_SCENARIO", "Chỉ kiểm tra tối đa 12 công việc mỗi lần.");
        foreach (var task in inputs)
        {
            Require(task is not null, "INVALID_SCENARIO", "Công việc giả định không hợp lệ.");
            ValidTask(task!);
            Require(task!.ParentId is null && task.CategoryId is null, "INVALID_SCENARIO", "Công việc giả định không tham chiếu dữ liệu thật.");
        }
        var planInput = input.Plan.IsReschedule && input.Plan.Start < Now.AddMinutes(5)
            ? input.Plan with { Start = Now.AddMinutes(5) } : input.Plan;
        var state = await State(uid, ct); var before = engine.Analyze(state, planInput, Now);
        var simulated = inputs.Select(task => NewTask(uid, task, Now)).ToArray();
        var clone = state with { Tasks = state.Tasks.Concat(simulated).ToArray() };
        var after = engine.Analyze(clone, planInput, Now);
        var plan = engine.Generate(clone, planInput with { AffectedTaskIds = planInput.IsReschedule ? simulated.Select(task => task.Id).ToArray() : null }, Now);
        return new(before, after, plan, after.RemainingMinutes - before.RemainingMinutes);
    }
    private void StorePlanDecisions(Guid uid, PlanProposal proposal, PlanView plan, PlanningState state)
    {
        var replaced = state.Sessions.Where(x => x.Status == WorkSessionStatus.Scheduled && x.Start >= plan.Start && x.Start < plan.End).ToList();
        foreach (var session in plan.Sessions)
        {
            var previous = session.Preserved ? state.Sessions.FirstOrDefault(x => x.Id == session.Id) : replaced.FirstOrDefault(x => x.TaskId == session.TaskId && !plan.Sessions.Any(p => p.Preserved && p.Id == x.Id));
            var item = new PlanItem { Id = session.Preserved ? Guid.NewGuid() : session.Id, UserId = uid, PlanId = proposal.Id, TaskId = session.TaskId,
                PreviousSessionId = previous?.Id, Action = session.Preserved ? "KEEP" : previous is null ? "ADD" : previous.Start == session.Start ? "RESIZE" : "MOVE",
                Start = session.Start, End = session.End, PlannedMinutes = (int)(session.End - session.Start).TotalMinutes };
            db.PlanItems.Add(item); db.PlanItemReasons.Add(new() { UserId = uid, ItemId = item.Id, Code = session.ReasonCode,
                DetailJson = JsonSerializer.Serialize(new { session.Preserved, session.Start, session.End }) });
            if (previous is not null) replaced.Remove(previous);
        }
        foreach (var session in replaced)
            db.PlanItems.Add(new() { UserId = uid, PlanId = proposal.Id, TaskId = session.TaskId, PreviousSessionId = session.Id, Action = "REMOVE" });
        foreach (var warning in plan.Warnings)
        {
            if (warning.UnscheduledMinutes > 0)
            {
                var item = new PlanItem { UserId = uid, PlanId = proposal.Id, TaskId = warning.TaskId, Action = "UNSCHEDULED" };
                db.PlanItems.Add(item); db.PlanItemReasons.Add(new() { UserId = uid, ItemId = item.Id, Code = warning.Code, DetailJson = JsonSerializer.Serialize(new { warning.UnscheduledMinutes }) });
            }
            db.PlanWarnings.Add(new() { UserId = uid, PlanId = proposal.Id, TaskId = warning.TaskId, Code = warning.Code, Detail = warning.Message });
        }
    }
    public async Task<PlanItemView[]> GetPlanItemsAsync(Guid uid, Guid id, CancellationToken ct)
    {
        await Find<PlanProposal>(uid, id, ct);
        var items = await Mine<PlanItem>(uid).Where(x => x.PlanId == id).OrderBy(x => x.Start).ToArrayAsync(ct);
        var ids = items.Select(x => x.Id).ToArray(); var reasons = await Mine<PlanItemReason>(uid).Where(x => ids.Contains(x.ItemId)).ToArrayAsync(ct);
        return items.Select(x => new PlanItemView(x.Id, x.TaskId, x.PreviousSessionId, x.Action, x.Start, x.End, x.PlannedMinutes, reasons.Where(r => r.ItemId == x.Id).Select(r => r.Code).ToArray())).ToArray();
    }
    public Task DiscardPlanAsync(Guid uid, Guid id, CancellationToken ct) => Mutate(uid, async () =>
    {
        var plan = await Find<PlanProposal>(uid, id, ct); Require(plan.Status == "PROPOSED", "PLAN_EXPIRED", "Đề xuất đã kết thúc.", 409); plan.Status = "DISCARDED";
    }, ct, false);
}
