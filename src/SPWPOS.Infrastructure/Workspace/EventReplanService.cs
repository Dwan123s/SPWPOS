using System.Text.Json;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private const string EventReplanKind = "EVENT_REPLAN";
    private sealed record EventReplanPayload(FixedEvent Event, Guid[] Released, PlanView Plan);

    public Task<EventReplanPreview> PreviewEventReplanAsync(Guid uid, EventInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var now = Now;
        var start = now.AddMinutes(5);
        Require(input.RepeatWeeks == 1 && input.Start > now && input.End <= now.AddDays(30),
            "EVENT_REPLAN_RANGE", "Chọn một sự kiện trong 30 ngày tới.");
        var state = await State(uid, ct);
        var item = EventOccurrences(uid, input, state.Preference).Single();
        var released = state.Sessions.Where(s => s.Status == WorkSessionStatus.Scheduled && !s.IsLocked &&
            s.Start > now && s.Start < item.End && item.Start < s.End).ToArray();
        var ids = released.Select(s => s.Id).ToHashSet();
        var kept = state.Sessions.Where(s => !ids.Contains(s.Id)).ToArray();
        ValidateEventConflicts(state with { Sessions = kept }, [item], null);
        Require(released.Length > 0, "NO_FLEXIBLE_CONFLICT", "Không còn phiên linh hoạt trùng giờ. Hãy kiểm tra lịch rồi lưu sự kiện.", 409);
        var candidate = state with
        {
            Events = state.Events.Append(item).ToArray(),
            Sessions = kept.Select(s => s.Status == WorkSessionStatus.Scheduled ? CloneLocked(s) : s).ToArray()
        };
        var before = released.Select(s => new ProposedSession(s.Id, s.TaskId, s.Start, s.End, s.ReasonCode, false)).ToArray();
        var view = new EventView(item.Id, item.Name, item.Start, item.End, item.Description, item.EventType, item.IsUrgent == true);
        var affected = released.Select(s => s.TaskId).Distinct().ToArray();
        var plan = engine.Generate(candidate, new(start, 30, affected,
            true, "AVAILABILITY_CHANGED", false, false) { AllowDeadlineRescue = false, RestrictAffectedTasks = true }, now) with { Strict = true };
        var keptFuture = kept.Where(s => s.End > now &&
            s.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused);
        var completion = engine.CompletionTimes(candidate, plan.Sessions.Concat(keptFuture.Select(s =>
            new ProposedSession(s.Id, s.TaskId, s.Start, s.End, s.ReasonCode, true))), [new("All", 0, 1, false)], now);
        var downstream = PlanningEngine.AffectedClosure(state, affected);
        var blockers = keptFuture.Where(s => downstream.Contains(s.TaskId) && state.Dependencies.Any(d => d.TaskId == s.TaskId &&
                (!completion.TryGetValue(d.PredecessorId, out var finished) || finished > s.Start)))
            .Select(s => new PlanWarning(s.TaskId, state.Tasks.Single(t => t.Id == s.TaskId).Name,
                "DEPENDENCY_CONFLICT", "Dời phiên này sẽ ảnh hưởng đến một phiên phụ thuộc đang giữ nguyên.", 0)).Distinct().ToArray();
        if (blockers.Length > 0)
            return new EventReplanPreview(null, view, before, [], plan.Warnings.Concat(blockers).ToArray());
        var retained = plan.Sessions.Where(s => s.Preserved).Select(s => s.Id).ToHashSet();
        Require(kept.Where(s => s.Status == WorkSessionStatus.Scheduled && s.Start >= plan.Start && s.Start < plan.End)
            .All(s => retained.Contains(s.Id)), "EVENT_REPLAN_SCOPE", "Không thể giữ nguyên các phiên ngoài phạm vi sự kiện.", 409);
        var after = plan.Sessions.Where(s => !s.Preserved).ToArray();
        if (HasPlanShortfall(plan)) return new EventReplanPreview(null, view, before, after, plan.Warnings);
        var row = new PrivatePlanningProposal { UserId = uid, Kind = EventReplanKind, Revision = state.Preference.Revision,
            ExpiresAt = now.AddMinutes(15), PayloadJson = JsonSerializer.Serialize(new EventReplanPayload(item, ids.ToArray(), plan)) };
        db.PrivatePlanningProposals.Add(row);
        return new EventReplanPreview(row.Id, view, before, after, plan.Warnings);
    }, ct, false);

    public Task<Guid> ConfirmEventReplanAsync(Guid uid, Guid id, CancellationToken ct) => Mutate(uid, async () =>
    {
        var row = await Find<PrivatePlanningProposal>(uid, id, ct);
        Require(row.Kind == EventReplanKind, "NOT_FOUND", "Không tìm thấy đề xuất sự kiện.", 404);
        var payload = JsonSerializer.Deserialize<EventReplanPayload>(row.PayloadJson)!;
        if (row.Applied) return payload.Event.Id;
        var state = await State(uid, ct);
        Require(row.Revision == state.Preference.Revision && row.ExpiresAt > Now && payload.Event.Start > Now &&
            payload.Plan.Sessions.Where(s => !s.Preserved).All(s => s.Start >= Now),
            "PLAN_STALE", "Lịch đã thay đổi hoặc đề xuất đã hết hạn. Hãy xem phương án mới.", 409);
        var released = state.Sessions.Where(s => payload.Released.Contains(s.Id)).ToArray();
        Require(released.Length == payload.Released.Length && released.All(s => s.Status == WorkSessionStatus.Scheduled &&
            !s.IsLocked && s.Start > Now), "PLAN_STALE", "Phiên bị ảnh hưởng đã thay đổi.", 409);
        var kept = state.Sessions.Where(s => !payload.Released.Contains(s.Id)).ToArray();
        ValidateEventConflicts(state with { Sessions = kept }, [payload.Event], null);
        var candidate = state with { Events = state.Events.Append(payload.Event).ToArray(), Sessions = kept };
        foreach (var session in released) session.Status = WorkSessionStatus.Cancelled;
        var proposal = await StoreAgentPlanAsync(uid, payload.Plan, state, "AVAILABILITY_CHANGED", ct);
        db.FixedEvents.Add(payload.Event);
        await ApplyPlanCoreAsync(uid, proposal, payload.Plan with { Id = proposal.Id }, candidate, ct);
        row.Applied = true;
        return payload.Event.Id;
    }, ct, repairTimeConflicts: false);
}
