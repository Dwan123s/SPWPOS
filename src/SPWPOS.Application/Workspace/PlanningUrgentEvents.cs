using SPWPOS.Domain.Entities;
namespace SPWPOS.Application.Workspace;

public sealed partial class PlanningEngine
{
    public PlanView GenerateForUrgentEvents(PlanningState state, PlanInput input, DateTimeOffset now, Guid[] eventIds)
    {
        var start = input.Start > now ? input.Start : now;
        var end = input.WindowEnd ?? start.AddDays(input.HorizonDays);
        var urgent = state.Events.Where(item => eventIds.Contains(item.Id)).ToArray();
        var tasks = state.Tasks.ToDictionary(task => task.Id);
        var hits = state.Sessions.Where(session => session.Status == WorkSessionStatus.Scheduled && !session.IsLocked &&
            session.Start >= start && session.Start < end && tasks.ContainsKey(session.TaskId) &&
            urgent.Any(item => session.Start < item.End && item.Start < session.End)).ToArray();
        var hitIds = hits.Select(session => session.Id).ToHashSet();
        var roots = hits.Select(session => session.TaskId).ToHashSet();
        var closure = AffectedClosure(state, roots);
        var released = state.Sessions.Where(session => hitIds.Contains(session.Id) ||
            closure.Contains(session.TaskId) && !roots.Contains(session.TaskId) && session.Status == WorkSessionStatus.Scheduled &&
            !session.IsLocked && session.Start >= start && session.Start < end).ToArray();
        var releasedIds = released.Select(session => session.Id).ToHashSet();
        var retained = state.Sessions.Where(session => !releasedIds.Contains(session.Id)).Select(session => session.Id).ToHashSet();
        var moves = released.GroupBy(session => session.TaskId).ToDictionary(group => group.Key, group => {
            var anchor = group.Min(session => session.Start);
            var latest = group.Max(session => session.End).AddDays(MaximumUrgentDisplacementDays);
            if (tasks[group.Key].Deadline < latest) latest = tasks[group.Key].Deadline;
            // Prefer the nearest later slot. Fixed appointments keep their exact
            // requested hours; existing task deadlines and work windows still apply.
            return new UrgentMove(anchor, anchor > start ? anchor : start, latest, true);
        });
        var affected = input.AffectedTaskIds is null ? null : input.AffectedTaskIds.Concat(closure).Distinct().ToArray();
        var plan = GenerateProtected(state, input with { AffectedTaskIds = affected }, now, new(retained, moves));
        return plan with { HasUrgentReplan = true, Explanations = plan.Explanations.Append(
            "Giữ đúng giờ lịch khẩn; dời các phiên chưa khóa bị trùng sang giờ phù hợp gần nhất sau lịch cũ, không đổi deadline hoặc khung giờ cho phép. Các lịch khác được giữ nguyên; cần xác nhận trước khi lưu.").ToArray() };
    }
}
