using SPWPOS.Domain.Entities;
namespace SPWPOS.Application.Workspace;

public sealed partial class PlanningEngine
{
    public EisenhowerTaskView[] AssessEisenhower(PlanningState state, DateTimeOffset now, int horizonDays = 30)
    {
        var end = now.AddDays(Math.Clamp(horizonDays, 1, 30));
        var bias = new[] { new BiasView("All", 0, 1, false) };
        var tasks = state.Tasks.Where(t => t.HasPlanningDetails && Remaining(t, bias) > 0 &&
            !state.Tasks.Any(child => !child.IsDeleted && child.ParentId == t.Id)).ToArray();
        var ready = state.Events.Length > 0 && (state.Policy != null || state.Availability.Length > 0 || state.Exceptions.Any(x => x.IsAvailable));
        // One feasibility run for the entire snapshot. Assessment must never call itself through scheduling.
        var plan = ready ? Generate(state with { EisenhowerScheduling = false }, new(now, Math.Clamp(horizonDays, 1, 30)), now) : null;
        var protectedSessions = state.Sessions.Where(s => s.End > now &&
            (s.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused || s.Status == WorkSessionStatus.Scheduled && s.IsLocked)).ToArray();
        var capacity = new Dictionary<DateTimeOffset, int>();
        foreach (var due in tasks.Where(t => t.Deadline > now && t.Deadline <= end).Select(t => t.Deadline!.Value).Distinct())
        {
            var days = CapacityDays(state, now, due, now, false, protectedSessions);
            var relevantReserved = protectedSessions.Where(s => tasks.Any(t => t.Id == s.TaskId && t.Deadline <= due))
                .Sum(s => Math.Max(0, ((s.End < due ? s.End : due) - (s.Start > now ? s.Start : now)).TotalMinutes));
            capacity[due] = (int)Math.Floor(Math.Min(days.Sum(d => d.EffectiveCapacityMinutes), days.Sum(d => d.SchedulableMinutes) + relevantReserved));
        }
        return tasks.Select(task => {
            var deadline = task.Deadline!.Value;
            var reasons = new List<string>(); var remaining = Remaining(task, bias);
            var overdue = task.Deadline <= now;
            var outside = task.Deadline > end;
            var covered = state.Availability.Length > 0 || state.Policy == null || state.Policy.AppliesOn(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(deadline, Zone(state.Preference.TimeZoneId)).DateTime)) ||
                state.Exceptions.Any(x => x.IsAvailable && x.Date >= DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone(state.Preference.TimeZoneId)).DateTime) && x.Date <= DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(deadline, Zone(state.Preference.TimeZoneId)).DateTime));
            var blocked = state.Dependencies.Where(d => d.TaskId == task.Id).Any(d =>
                state.Tasks.Any(t => t.Id == d.PredecessorId && t.Status != WorkTaskStatus.Completed));
            var importance = task.EffectiveImportance;
            var urgency = "Unknown"; int? available = null; var atRisk = overdue;
            if (overdue) { urgency = "Urgent"; reasons.Add("OVERDUE"); }
            else if (!ready) reasons.Add("SCHEDULE_REQUIRED");
            else if (outside) reasons.Add("OUTSIDE_HORIZON");
            else if (!covered) reasons.Add("SCHEDULE_REQUIRED");
            else {
                available = capacity.GetValueOrDefault(deadline);
                var demand = tasks.Where(t => t.Deadline <= task.Deadline).Sum(t => Remaining(t, bias));
                var deficient = plan!.Warnings.Any(w => w.TaskId == task.Id && w.UnscheduledMinutes > 0);
                atRisk = deficient || available < demand;
                var reserve = Math.Max(state.Preference.MinimumSessionMinutes, Math.Ceiling(demand * .25));
                if (atRisk) reasons.Add("INSUFFICIENT_CAPACITY");
                if (available - demand <= reserve) reasons.Add("LOW_BUFFER");
                if (task.Deadline - now <= TimeSpan.FromHours(24)) reasons.Add("DUE_WITHIN_24H");
                urgency = reasons.Count > 0 ? "Urgent" : "NotUrgent";
            }
            if (blocked) reasons.Add("DEPENDENCY_PENDING");
            if (importance == "Unknown") reasons.Add("IMPORTANCE_UNKNOWN");
            var manualUrgency = task.UrgencyOverride is "Urgent" or "NotUrgent";
            if (manualUrgency) { urgency = task.UrgencyOverride; reasons.Add("USER_URGENCY"); }
            var quadrant = importance == "Unknown" || urgency == "Unknown" ? "Unknown" :
                importance == "Important" ? urgency == "Urgent" ? "Q1" : "Q2" : urgency == "Urgent" ? "Q3" : "Q4";
            var explanation = reasons.Contains("OVERDUE") ? "Đã quá hạn và còn công việc cần xử lý."
                : reasons.Contains("SCHEDULE_REQUIRED") ? "Cần thời khóa biểu và giờ có thể làm việc đã xác nhận cho khoảng ngày này để đánh giá độ gấp."
                : outside ? "Deadline nằm ngoài phạm vi đánh giá 30 ngày."
                : atRisk ? "Quỹ thời gian hoặc phương án hiện tại chưa đáp ứng đầy đủ công việc trước hạn."
                : reasons.Contains("LOW_BUFFER") ? "Quỹ thời gian trước hạn còn ít dự phòng cho các việc đang cạnh tranh."
                : reasons.Contains("DUE_WITHIN_24H") ? "Deadline trong 24 giờ tới."
                : manualUrgency && urgency == "Urgent" ? "Bạn chọn ưu tiên khẩn cấp."
                : "Còn quỹ thời gian và dự phòng trước hạn; nên chủ động sắp xếp sớm.";
            if (importance == "Unknown") explanation += " Chưa đủ căn cứ xác định mức quan trọng.";
            if (manualUrgency) explanation += " Độ khẩn cấp do bạn chọn; cảnh báo deadline và thiếu giờ vẫn được giữ.";
            return new EisenhowerTaskView(task.Id, task.Name, task.Deadline, importance, urgency, quadrant, explanation,
                reasons.ToArray(), remaining, available, atRisk, overdue, blocked, quadrant == "Unknown",
                Version: task.Version, Order: task.MatrixOrder, UrgencySource: manualUrgency ? "User" : "Auto",
                Recommendation: quadrant is "Q1" or "Q2" ? "SuggestSchedule" : quadrant == "Q3" ? remaining <= 15 ? "QuickAction" : "OnDemand" : quadrant == "Q4" ? "Backlog" : "Clarify",
                Description: task.Description, EstimatedMinutes: task.EstimatedMinutes, Progress: task.Progress, CanEdit: true, Splittable: task.CanSplit,
                RequiresContinuousSession: task.RequiresContinuousSession);
        }).OrderBy(t => t.Order).ThenBy(t => t.Deadline).ThenBy(t => t.Id).ToArray();
    }
}
