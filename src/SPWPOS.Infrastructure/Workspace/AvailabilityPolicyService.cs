using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private sealed record PolicyPayload(AvailabilityPolicy Policy, PlanView Plan, DateOnly? WeekStart = null);
    private static PolicyView PolicyDto(AvailabilityPolicy p) => new(p.AwakeStart, p.AwakeEnd, p.LunchStart, p.LunchEnd, p.DinnerStart, p.DinnerEnd, p.CommuteMinutes, p.FocusMinutes, p.ConfirmedAt != default, p.ConfirmedAt == default ? null : p.ConfirmedAt, p.RoutineDaysJson != "[]");
    public async Task<PolicyView?> GetAvailabilityPolicyAsync(Guid uid, CancellationToken ct) =>
        await Mine<AvailabilityPolicy>(uid).AsNoTracking().SingleOrDefaultAsync(ct) is { } p ? PolicyDto(p) : null;
    public async Task<PolicyPreviewView> PreviewAvailabilityPolicyAsync(Guid uid, CancellationToken ct, Guid? timetableTurnId = null)
        => await PreviewPolicyCore(uid, null, ct, timetableTurnId);
    public async Task<PolicyPreviewView> DraftAvailabilityPolicyAsync(Guid uid, TeamTextInput input, CancellationToken ct, Guid? timetableTurnId = null)
    {
        Require(interpreter is IPolicyDraftInterpreter, "AI_NOT_CONFIGURED", "AI chưa hỗ trợ cấu hình sinh hoạt.", 503);
        var current = await GetAvailabilityPolicyAsync(uid, ct) ?? PolicyDto(new());
        var draft = await RunJob(uid, "AI_REQUEST", () => ((IPolicyDraftInterpreter)interpreter).DraftPolicyAsync(input.Text, current, ct), ct);
        Require(draft.AwakeStart >= 0 && draft.AwakeEnd <= 1440 && draft.AwakeStart < draft.AwakeEnd && draft.LunchStart >= 0 && draft.LunchEnd <= 1440 && draft.LunchStart < draft.LunchEnd && draft.DinnerStart >= 0 && draft.DinnerEnd <= 1440 && draft.DinnerStart < draft.DinnerEnd && draft.CommuteMinutes is >= 0 and <= 180 && draft.FocusMinutes is >= 5 and <= 180,
            "INVALID_POLICY", "Giờ sinh hoạt chưa hợp lệ. Hãy mô tả rõ các khung giờ trong ngày.");
        return await PreviewPolicyCore(uid, new AvailabilityPolicy { UserId = uid, AwakeStart = draft.AwakeStart, AwakeEnd = draft.AwakeEnd, LunchStart = draft.LunchStart, LunchEnd = draft.LunchEnd, DinnerStart = draft.DinnerStart, DinnerEnd = draft.DinnerEnd, CommuteMinutes = draft.CommuteMinutes, FocusMinutes = draft.FocusMinutes }, ct, timetableTurnId);
    }
    private async Task<DateOnly> TimetableRoutineWeek(Guid uid, PlanningState state, Guid? turnId, CancellationToken ct)
    {
        var turns = turnId is { } id ? new[] { await Find<AgentTurn>(uid, id, ct) } :
            await Mine<AgentTurn>(uid).AsNoTracking().Where(x => x.Status == "APPLIED").OrderByDescending(x => x.UpdatedAt).Take(100).ToArrayAsync(ct);
        foreach (var turn in turns)
        {
            var changes = string.IsNullOrWhiteSpace(turn.ChangesJson) ? null : JsonSerializer.Deserialize<AgentChangeSet>(turn.ChangesJson, AgentJson);
            if (turn.Status != "APPLIED" || changes?.AnalyzeTimetable != true || changes.Events.Length == 0)
            { Require(turnId is null, "TIMETABLE_NOT_APPLIED", "Xác nhận thời khóa biểu trước khi xem đề xuất tuần.", 409); continue; }
            var ids = changes.Events.Select(x => x.Id).ToHashSet();
            var dates = state.Events.Where(x => ids.Contains(x.Id)).Select(x => LocalDate(x.Start, state.Preference)).ToArray();
            if (dates.Length > 0) return RoutineWeekStart(dates.Min());
            Require(turnId is null, "TIMETABLE_CHANGED", "Thời khóa biểu đã được xóa hoặc thay đổi. Hãy gửi lại để đề xuất đúng tuần.", 409);
        }
        return RoutineWeekStart(LocalDate(state.Events.Where(x => x.End > Now).Select(x => x.Start).DefaultIfEmpty(Now).Min(), state.Preference));
    }
    private async Task<PolicyPreviewView> PreviewPolicyCore(Guid uid, AvailabilityPolicy? requested, CancellationToken ct, Guid? timetableTurnId)
    {
        var state = await State(uid, ct);
        Require(state.Events.Length > 0, "TIMETABLE_REQUIRED", "Gửi và xác nhận thời gian biểu trước khi cấu hình giờ sinh hoạt.", 409);
        var first = await TimetableRoutineWeek(uid, state, timetableTurnId, ct);
        var end = PlanningEngine.At(first.AddDays(7), 0, PlanningEngine.Zone(state.Preference.TimeZoneId));
        Require(end > Now.AddMinutes(5), "TIMETABLE_WEEK_ENDED", "Tuần của thời khóa biểu đã kết thúc. Hãy gửi thời khóa biểu của tuần cần sắp xếp.", 409);
        var policy = requested ?? ProposeTimetableRoutine(state, first);
        if (requested is not null)
        {
            var previous = state.Policy is null ? [] : JsonSerializer.Deserialize<RoutineDay[]>(state.Policy.RoutineDaysJson) ?? [];
            policy.RoutineDaysJson = JsonSerializer.Serialize(Enumerable.Range(0, 7).Select(i => new RoutineDay(first.AddDays(i), policy.AwakeStart, policy.AwakeEnd, policy.LunchStart, policy.LunchEnd, policy.DinnerStart, policy.DinnerEnd))
                .Concat(previous.Where(x => x.Date < first || x.Date >= first.AddDays(7))));
        }
        var candidate = state with { Policy = policy };
        var start = PlanningEngine.At(first, 0, PlanningEngine.Zone(state.Preference.TimeZoneId));
        if (start < Now.AddMinutes(5)) start = Now.AddMinutes(5);
        var plan = engine.Generate(candidate, new(start, 7, IsReschedule: true, UseCapacityFactor: false, UseEstimateBias: false) { WindowEnd = end }, Now);
        var row = new PrivatePlanningProposal { UserId = uid, Kind = "POLICY", Revision = state.Preference.Revision,
            ExpiresAt = Now.AddMinutes(20), PayloadJson = JsonSerializer.Serialize(new PolicyPayload(policy, plan, first)) };
        db.PrivatePlanningProposals.Add(row); await db.SaveChangesAsync(ct);
        var after = candidate with { Sessions = candidate.Sessions.Where(x => x.Status != WorkSessionStatus.Scheduled || x.Start < plan.Start || x.Start >= plan.End)
            .Concat(plan.Sessions.Where(x => x.Start >= plan.Start && x.Start < plan.End).Select(x => new WorkSession { Id = x.Id, UserId = uid, TaskId = x.TaskId, Start = x.Start, End = x.End, Status = WorkSessionStatus.Scheduled })).ToArray() };
        var policyWarnings = plan.Warnings.Select(x => x.Message).ToList();
        if (Enumerable.Range(0, 7).Select(i => policy.ForDate(first.AddDays(i))).Any(d => d is not null && (d.LunchEnd <= d.LunchStart || d.DinnerEnd <= d.DinnerStart)))
            policyWarnings.Add("Có ngày chưa tìm được đủ 30 phút cho bữa ăn quanh lịch cố định. Hãy trao đổi với NORA để điều chỉnh trước khi áp dụng.");
        return new(row.Id, PolicyDto(policy), row.ExpiresAt, PrivateSessions(state, state.Sessions.Where(x => x.Status == WorkSessionStatus.Scheduled && x.Start >= start && x.Start < end).Select(x => new ProposedSession(x.Id, x.TaskId, x.Start, x.End, "", true))),
            PrivateSessions(candidate, plan.Sessions.Where(x => x.Start >= start && x.Start < end)), policyWarnings.ToArray(), PolicyDays(after, first), first, first.AddDays(6));
    }
    public Task ConfirmAvailabilityPolicyAsync(Guid uid, Guid proposalId, CancellationToken ct) => Mutate(uid, async () =>
    {
        var row = await Find<PrivatePlanningProposal>(uid, proposalId, ct);
        if (row.Applied) return;
        Require(row.Kind == "POLICY" && row.ExpiresAt > Now, "PROPOSAL_EXPIRED", "Đề xuất hết hạn. Hãy xem lại.", 409);
        var state = await State(uid, ct);
        Require(row.Revision == state.Preference.Revision, "PLAN_STALE", "Lịch đã thay đổi. Hãy xem lại chính sách.", 409);
        var payload = JsonSerializer.Deserialize<PolicyPayload>(row.PayloadJson)!;
        Require(Enumerable.Range(0, 7).Select(i => payload.Policy.ForDate((payload.WeekStart ?? LocalDate(Now, state.Preference)).AddDays(i))).All(d => d is null || d.LunchEnd > d.LunchStart && d.DinnerEnd > d.DinnerStart), "ROUTINE_MEAL_CONFLICT", "Chưa có đủ giờ ăn trong lịch đề xuất; hãy điều chỉnh trước khi lưu.", 409);
        Require(payload.Plan.Warnings.All(x => x.UnscheduledMinutes == 0 && x.Code != "SOFT_DEADLINE_EXCEEDED"), "POLICY_CONFLICT", "Chính sách này chưa giữ được lịch hiện tại. Trao đổi với NORA để điều chỉnh.", 409);
        var candidate = state with { Policy = payload.Policy };
        foreach (var s in state.Sessions.Where(x => x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused || x.IsLocked && x.Status == WorkSessionStatus.Scheduled))
            Require(engine.FreeSlots(candidate, s.Start, s.End).Any(x => x.Start <= s.Start && x.End >= s.End), "PROTECTED_SESSION_CONFLICT", "Chính sách xung đột phiên đang thực hiện hoặc đã khóa.", 409);
        if (state.Policy is null) { payload.Policy.ConfirmedAt = Now; db.AvailabilityPolicies.Add(payload.Policy); }
        else
        {
            payload.Policy.Id = state.Policy.Id; payload.Policy.ConfirmedAt = Now; payload.Policy.Version = Guid.NewGuid();
            db.Entry(state.Policy).CurrentValues.SetValues(payload.Policy); candidate = state;
        }
        var proposal = await StoreAgentPlanAsync(uid, payload.Plan, candidate, "AVAILABILITY_CHANGED", ct);
        await ApplyPlanCoreAsync(uid, proposal, payload.Plan, candidate, ct);
        (await Preference(uid, ct)).Revision = Guid.NewGuid(); row.Applied = true;
    }, ct, false);
    private static PrivateClaimSession[] PrivateSessions(PlanningState state, IEnumerable<ProposedSession> sessions) => sessions
        .Select(x => new PrivateClaimSession(x.Id, state.Tasks.FirstOrDefault(t => t.Id == x.TaskId)?.Name ?? "Công việc", x.Start, x.End, x.Preserved)).ToArray();
    public async Task<PolicyDayView[]> GetPolicySlotsAsync(Guid uid, CancellationToken ct)
    {
        var state = await State(uid, ct);
        var routine = state.Policy is null ? [] : JsonSerializer.Deserialize<RoutineDay[]>(state.Policy.RoutineDaysJson) ?? [];
        return state.Policy is null ? [] : PolicyDays(await AgentCalendarStateAsync(uid, state, ct), routine.FirstOrDefault()?.Date);
    }
    private PolicyDayView[] PolicyDays(PlanningState state, DateOnly? weekStart = null)
    {
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId); var first = weekStart ?? LocalDate(Now, state.Preference);
        var result = new List<PolicyDayView>();
        var raw = state with { Policy = null, Availability = Enumerable.Range(0, 7).Select(day => new WeeklyAvailability { DayOfWeek = day, StartMinute = 0, EndMinute = 1440 }).ToArray(), Exceptions = state.Exceptions.Where(x => !x.IsAvailable).ToArray() };
        for (var day = first; day < first.AddDays(7); day = day.AddDays(1))
        {
            var start = PlanningEngine.At(day, 0, zone); if (start < Now) start = Now;
            var end = PlanningEngine.At(day.AddDays(1), 0, zone);
            var work = start < end ? AgentFreeTimeSlots(state, start, end) : [];
            var blocks = state.Sessions.Where(x => x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed)
                .Select(x => new TimeSlot(x.Start.AddMinutes(-state.Preference.BreakMinutes), x.End.AddMinutes(state.Preference.BreakMinutes)));
            var focus = start < end ? engine.FocusSlots(state, start, end, blocks) : [];
            var capacity = start < end ? engine.CapacityDays(state, start, end, Now, false).Sum(x => x.SchedulableMinutes) : 0;
            result.Add(new(day, start < end ? (int)AgentFreeTimeSlots(raw, start, end).Sum(x => (x.End - x.Start).TotalMinutes) : 0, work, focus, capacity, state.Policy?.ForDate(day), RoutineTimeline(state, day, work)));
        }
        return result.ToArray();
    }
}
