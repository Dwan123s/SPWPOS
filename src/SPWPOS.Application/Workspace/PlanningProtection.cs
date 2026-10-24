using SPWPOS.Domain.Entities;
namespace SPWPOS.Application.Workspace;

public sealed partial class PlanningEngine
{
    public PlanView Generate(PlanningState state, PlanInput input, DateTimeOffset now)
        => GenerateProtected(state, input, now);

    private PlanView GenerateProtected(PlanningState state, PlanInput input, DateTimeOffset now, UrgentReplanPolicy? baseline = null)
    {
        if (baseline is null && input.AffectedTaskIds is { Length: > 0 } requested)
        {
            var newest = state.Tasks.Where(task => requested.Contains(task.Id) && task.Priority == 4).Select(task => task.Id).ToHashSet();
            if (newest.Count > 0) baseline = new([], [], RequestedUrgentTaskIds: newest);
        }
        if (input.AffectedTaskIds is null || input.AffectedTaskIds.Length == 0)
            return GenerateUrgent(state, input, now, baseline);
        var start = input.Start > now ? input.Start : now;
        var end = start.AddDays(input.HorizonDays);
        var selected = AffectedClosure(state, input.AffectedTaskIds);
        var bias = input.UseEstimateBias ? Biases(state) : [new BiasView("All", 0, 1, false)];
        // A scoped request must still account for urgent and imminent work that
        // has not yet been allocated enough valid future time. Fully covered
        // tasks remain outside the request, with their saved sessions preserved.
        var protectedIds = state.Tasks.Where(task => !selected.Contains(task.Id) && Remaining(task, bias) > 0 &&
            task.Deadline > start && (task.Priority == 4 || task.Deadline - start <= NearDeadlineWindow) &&
            (task.WorkWindowStart is null || task.WorkWindowStart < end) &&
            !state.Tasks.Any(child => !child.IsDeleted && child.ParentId == task.Id) &&
            state.Sessions.Where(session => session.TaskId == task.Id && session.End > now &&
                session.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused &&
                session.End <= task.Deadline && InsideWorkWindow(task, session.Start, session.End))
                .Sum(session => (session.End - session.Start).TotalMinutes) < Remaining(task, bias))
            .Select(task => task.Id).ToHashSet();
        if (protectedIds.Count == 0) return GenerateUrgent(state, input, now, baseline);
        IncludePrerequisites(state, protectedIds);
        var expanded = AffectedClosure(state, selected.Concat(protectedIds));
        // Expanding the scope is permission to fill missing time, not to move
        // existing sessions belonging to tasks outside the original request.
        var retained = state.Sessions.Where(session => !selected.Contains(session.TaskId)).Select(session => session.Id).ToHashSet();
        if (baseline is not null) retained.UnionWith(baseline.RetainedSessionIds);
        if (baseline?.ProtectedTaskIds is not null) protectedIds.UnionWith(baseline.ProtectedTaskIds);
        var policy = new UrgentReplanPolicy(retained, baseline?.Displaced ?? [], protectedIds, baseline?.RequestedUrgentTaskIds);
        var plan = GenerateUrgent(state, input with { AffectedTaskIds = expanded.ToArray() }, now, policy);
        var lateSaved = state.Tasks.Where(task => protectedIds.Contains(task.Id) && plan.Sessions.Any(session =>
            session.Preserved && session.TaskId == task.Id && session.End > task.Deadline))
            .Select(task => new PlanWarning(task.Id, task.Name, "SOFT_DEADLINE_EXCEEDED",
                "Phiên đã lưu nằm sau deadline; cần điều chỉnh trước khi thêm việc mới.", 0));
        return plan with { HasDeadlineProtection = true, Warnings = plan.Warnings.Concat(lateSaved).DistinctBy(x => (x.TaskId, x.Code)).ToArray(),
            Explanations = plan.Explanations.Append(
            "Đã xét thêm việc khẩn hoặc còn hạn trong 48 giờ chưa có đủ lịch và các việc tiền nhiệm. Giữ các phiên cũ, bổ sung thời gian trước hạn rồi mới xếp việc mới; cần xác nhận kế hoạch trước khi lưu.").ToArray() };
    }
}
