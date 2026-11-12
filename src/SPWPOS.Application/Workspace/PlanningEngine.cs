using SPWPOS.Domain.Entities;
namespace SPWPOS.Application.Workspace;

public sealed record TimeSlot(DateTimeOffset Start, DateTimeOffset End);
public sealed record PlanningState(WorkTask[] Tasks, TaskDependency[] Dependencies, WeeklyAvailability[] Availability,
    AvailabilityException[] Exceptions, FixedEvent[] Events, WorkSession[] Sessions, WorkPreference Preference, EstimationObservation[]? Observations = null,
    AvailabilityPolicy? Policy = null, bool EisenhowerScheduling = false);

// Pure deterministic planner: no EF, HTTP, secrets, AI, or wall-clock dependencies.
public sealed partial class PlanningEngine
{
    public const int MinimumBiasSamples = 3;
    public const double ColdCapacityFactor = .8;
    public BiasView[] Biases(PlanningState state)
    {
        if (state.Observations is { } history)
        {
            BiasView FromHistory(string type, IEnumerable<EstimationObservation> rows)
            {
                var data = rows.Where(x => x.ActualMinutes > 0 && x.EstimatedMinutes > 0).OrderBy(x => x.ObservedAt).ThenBy(x => x.Id).ToArray();
                var ratio = data.Length >= MinimumBiasSamples ? AdaptiveLearning.Ema(data.Select(x => x.ActualMinutes / x.EstimatedMinutes),
                    1, AdaptiveLearning.MinimumBias, AdaptiveLearning.MaximumBias) : 1;
                return new(type, data.Length, Math.Round(ratio, 3), data.Length >= MinimumBiasSamples);
            }
            return new[] { FromHistory("All", history) }.Concat(history.Where(x => x.ActualMinutes > 0 && x.EstimatedMinutes > 0).GroupBy(x => x.TaskType).Select(g => FromHistory(g.Key, g))).ToArray();
        }
        var observations = state.Tasks.Where(x => !x.IsDeleted && x.Status == WorkTaskStatus.Completed && x.EstimatedMinutes > 0)
            .Select(x => (Task: x, Actual: state.Sessions.Where(s => s.TaskId == x.Id && s.Status == WorkSessionStatus.Completed).Sum(s => s.ActualSeconds) / 60d))
            .Where(x => x.Actual > 0).ToArray();
        BiasView Make(string type, IEnumerable<(WorkTask Task, double Actual)> rows)
        {
            var data = rows.OrderBy(x => x.Task.CompletedAt).ThenBy(x => x.Task.Id).ToArray();
            var ratio = data.Length >= MinimumBiasSamples ? AdaptiveLearning.Ema(data.Select(x => x.Actual / x.Task.EstimatedMinutes),
                1, AdaptiveLearning.MinimumBias, AdaptiveLearning.MaximumBias) : 1;
            return new(type, data.Length, Math.Round(ratio, 3), data.Length >= MinimumBiasSamples);
        }
        return new[] { Make("All", observations) }.Concat(observations.GroupBy(x => x.Task.TaskType).Select(g => Make(g.Key, g))).ToArray();
    }
    public int Adjusted(WorkTask task, BiasView[] biases)
    {
        var bias = biases.FirstOrDefault(x => x.TaskType == task.TaskType && x.Personalized) ?? biases[0];
        return (int)Math.Ceiling(task.EstimatedMinutes * bias.Ratio);
    }
    public int Remaining(WorkTask task, BiasView[] biases) => task.IsDeleted || task.Status is WorkTaskStatus.Completed or WorkTaskStatus.Cancelled ? 0 :
        task.ConfirmedRemainingMinutes ?? (int)Math.Ceiling(Adjusted(task, biases) * (1 - task.Progress / 100d));
    public static TimeZoneInfo Zone(string id) => TimeZoneInfo.FindSystemTimeZoneById(id);
    public static bool InsideWorkWindow(WorkTask task, DateTimeOffset start, DateTimeOffset end) =>
        (task.WorkWindowStart is null || start >= task.WorkWindowStart) && (task.WorkWindowEnd is null || end <= task.WorkWindowEnd);
    public static DateTimeOffset At(DateOnly date, int minute, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue).AddMinutes(minute);
        // A recurring wall-clock boundary inside a DST gap moves to the next valid minute.
        // This keeps weekly availability usable across daylight-saving transitions.
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
    public TimeSlot[] FreeSlots(PlanningState state, DateTimeOffset start, DateTimeOffset end, IEnumerable<TimeSlot>? additionalBlocks = null)
    {
        var zone = Zone(state.Preference.TimeZoneId); var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, zone).DateTime);
        var last = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(end, zone).DateTime);
        var result = new List<TimeSlot>();
        var eventBlocks = Merge(state.Events.Select(x => new TimeSlot(x.Start, x.End))).ToArray();
        var blocks = eventBlocks.Select(x => state.Policy is { } p && p.AppliesOn(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.Start, zone).DateTime))
            ? new TimeSlot(x.Start.AddMinutes(-p.CommuteMinutes), x.End.AddMinutes(p.CommuteMinutes)) : x)
            .Concat(additionalBlocks ?? []).ToArray();
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var daily = state.Policy?.ForDate(day);
            var activePolicy = state.Policy?.AppliesOn(day) == true ? state.Policy : null;
            var dated = state.Exceptions.Where(x => x.Date == day && x.IsAvailable).ToArray();
            var replaceDate = dated.Any(x => x.OverridesWeeklyAvailability);
            var weekly = state.Availability.Where(x => x.DayOfWeek == (int)day.DayOfWeek).ToArray();
            var useTimetable = daily?.UseTimetableHours == true;
            var windows = (replaceDate || useTimetable ? [] : weekly)
                .Select(x => new TimeSlot(At(day, x.StartMinute, zone), At(day, x.EndMinute, zone)))
                .Concat(dated.Where(x => x.StartMinute < x.EndMinute && (!replaceDate || x.OverridesWeeklyAvailability))
                    .Select(x => new TimeSlot(At(day, x.StartMinute, zone), At(day, x.EndMinute, zone))));
            if (!replaceDate && (useTimetable || state.Availability.Length == 0) && activePolicy is { } policy)
                windows = windows.Append(new(At(day, daily?.AwakeStart ?? policy.AwakeStart, zone), At(day, daily?.AwakeEnd ?? policy.AwakeEnd, zone)));
            var unavailable = state.Exceptions.Where(x => x.Date == day && !x.IsAvailable).Select(x => new TimeSlot(At(day, x.StartMinute, zone), At(day, x.EndMinute, zone)));
            if (activePolicy is { } routine)
                unavailable = unavailable.Concat(new[] { new TimeSlot(At(day, daily?.LunchStart ?? routine.LunchStart, zone), At(day, daily?.LunchEnd ?? routine.LunchEnd, zone)),
                    new TimeSlot(At(day, daily?.DinnerStart ?? routine.DinnerStart, zone), At(day, daily?.DinnerEnd ?? routine.DinnerEnd, zone)) });
            if (daily is { RestEnd: > 0 } && daily.RestEnd > daily.RestStart)
                unavailable = unavailable.Append(new(At(day, daily.RestStart, zone), At(day, daily.RestEnd, zone)));
            if (daily is { BreakfastEnd: > 0 } && daily.BreakfastEnd > daily.BreakfastStart)
                unavailable = unavailable.Append(new(At(day, daily.BreakfastStart, zone), At(day, daily.BreakfastEnd, zone)));
            foreach (var window in Merge(windows))
            {
                var clipped = new TimeSlot(window.Start < start ? start : window.Start, window.End > end ? end : window.End);
                if (activePolicy is { } awake)
                {
                    var from = At(day, daily?.AwakeStart ?? awake.AwakeStart, zone); var to = At(day, daily?.AwakeEnd ?? awake.AwakeEnd, zone);
                    clipped = new(clipped.Start < from ? from : clipped.Start, clipped.End > to ? to : clipped.End);
                }
                if (clipped.End > clipped.Start) result.AddRange(Subtract(clipped, blocks.Concat(unavailable)));
            }
        }
        return result.OrderBy(x => x.Start).ToArray();
    }
    public TimeSlot[] FocusSlots(PlanningState state, DateTimeOffset start, DateTimeOffset end, IEnumerable<TimeSlot>? blocks = null) =>
        FreeSlots(state, start, end, blocks).Where(x => state.Policy is null || !state.Policy.AppliesOn(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.Start, Zone(state.Preference.TimeZoneId)).DateTime)) || (x.End - x.Start).TotalMinutes >= state.Policy.FocusMinutes).ToArray();
    public static IEnumerable<TimeSlot> Merge(IEnumerable<TimeSlot> slots)
    {
        TimeSlot? current = null;
        foreach (var slot in slots.OrderBy(x => x.Start))
        {
            if (current is null) current = slot;
            else if (slot.Start <= current.End) current = current with { End = slot.End > current.End ? slot.End : current.End };
            else { yield return current; current = slot; }
        }
        if (current is not null) yield return current;
    }
    public static IEnumerable<TimeSlot> Subtract(TimeSlot slot, IEnumerable<TimeSlot> blocked)
    {
        var cursor = slot.Start;
        foreach (var block in Merge(blocked.Where(x => x.End > slot.Start && x.Start < slot.End)))
        {
            if (block.Start > cursor) yield return new(cursor, block.Start);
            if (block.End > cursor) cursor = block.End;
            if (cursor >= slot.End) yield break;
        }
        if (cursor < slot.End) yield return new(cursor, slot.End);
    }
    public static bool HasCycle(IEnumerable<WorkTask> tasks, IEnumerable<TaskDependency> dependencies)
    {
        var nodes = tasks.Where(x => !x.IsDeleted).ToArray();
        // A parent cannot complete before its children. Include these implicit
        // prerequisites so a dependency on an ancestor cannot deadlock execution.
        var edges = dependencies.Select(x => (TaskId: x.TaskId, PredecessorId: x.PredecessorId))
            .Concat(nodes.Where(x => x.ParentId != null).Select(x => (TaskId: x.ParentId!.Value, PredecessorId: x.Id)))
            .GroupBy(x => x.TaskId).ToDictionary(x => x.Key, x => x.Select(d => d.PredecessorId).Distinct().ToArray());
        var visited = new HashSet<Guid>(); var stack = new HashSet<Guid>();
        bool Visit(Guid id)
        {
            if (stack.Contains(id)) return true;
            if (!visited.Add(id)) return false;
            stack.Add(id);
            if (edges.TryGetValue(id, out var parents) && parents.Any(Visit)) return true;
            stack.Remove(id); return false;
        }
        return nodes.Any(x => Visit(x.Id));
    }
    private PlanView GenerateGreedy(PlanningState state, PlanInput input, DateTimeOffset now, UrgentReplanPolicy? urgency,
        bool urgentFirst, HashSet<Guid>? promoted = null)
    {
        var start = input.Start < now ? now : input.Start; var end = input.WindowEnd ?? start.AddDays(input.HorizonDays);
        var bias = input.UseEstimateBias ? Biases(state) : [new BiasView("All", 0, 1, false)];
        var factor = input.UseCapacityFactor ? Capacity(state, now).Factor : 1;
        var affected = input.AffectedTaskIds is null ? null : input.RestrictAffectedTasks
            ? input.AffectedTaskIds.ToHashSet() : AffectedClosure(state, input.AffectedTaskIds);
        var preserve = state.Sessions.Where(x => x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused ||
            x.Status == WorkSessionStatus.Scheduled && x.End > now && (x.Start < start || x.Start >= end || x.IsLocked ||
                urgency?.RetainedSessionIds.Contains(x.Id) == true || affected is not null && !affected.Contains(x.TaskId))).ToArray();
        var schedule = preserve.Where(x => x.End > start && x.Start < end).Select(x => new ProposedSession(x.Id, x.TaskId, x.Start, x.End, "PRESERVED_SESSION", true)).ToList();
        TimeSlot Block(DateTimeOffset from, DateTimeOffset to) => new(from.AddMinutes(-state.Preference.BreakMinutes), to.AddMinutes(state.Preference.BreakMinutes));
        var completedBlocks = state.Sessions.Where(x => x.Status == WorkSessionStatus.Completed).Select(x => Block(x.Start, x.End)).ToArray();
        var free = FocusSlots(state, start, end, preserve.Select(x => Block(x.Start, x.End)).Concat(completedBlocks));
        var zone = Zone(state.Preference.TimeZoneId);
        DateOnly Day(DateTimeOffset date) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(date, zone).DateTime);
        var budgets = CapacityDays(state, start, end, now, input.UseCapacityFactor, preserve)
            .ToDictionary(x => x.Date, x => x.SchedulableMinutes);
        var tasks = state.Tasks.Where(x => x.HasPlanningDetails && Remaining(x, bias) > 0 && !state.Tasks.Any(child => child.ParentId == x.Id && !child.IsDeleted) && (affected is null || affected.Contains(x.Id))).ToDictionary(x => x.Id);
        var preservedSessions = preserve.Select(x => new ProposedSession(x.Id, x.TaskId, x.Start, x.End, x.ReasonCode, true)).ToArray();
        var done = CompletionTimes(state, preservedSessions, bias, start);
        var warnings = new List<PlanWarning>();
        var important = state.EisenhowerScheduling ? state.Tasks.Where(t => t.EffectiveImportance == "Important").Select(t => t.Id).ToHashSet() : [];
        if (important.Count > 0) IncludePrerequisites(state, important);
        while (tasks.Count > 0)
        {
            var task = tasks.Values.Where(t => state.Dependencies.Where(d => d.TaskId == t.Id).All(d => done.ContainsKey(d.PredecessorId)))
                .OrderByDescending(t => promoted?.Contains(t.Id) == true)
                .ThenByDescending(t => urgentFirst && urgency?.RequestedUrgentTaskIds?.Contains(t.Id) == true)
                .ThenByDescending(t => urgentFirst && t.Priority == 4)
                .ThenByDescending(t => important.Contains(t.Id))
                .ThenBy(t => t.Deadline).ThenByDescending(t => t.Priority).ThenBy(t => t.Id).FirstOrDefault();
            if (task is null)
            {
                foreach (var blocked in tasks.Values) warnings.Add(new(blocked.Id, blocked.Name, "DEPENDENCY_UNSCHEDULED", "Công việc phụ thuộc chưa có phương án hoàn thành.", Remaining(blocked, bias)));
                break;
            }
            tasks.Remove(task.Id);
            var earliest = state.Dependencies.Where(d => d.TaskId == task.Id).Select(d => done[d.PredecessorId]).DefaultIfEmpty(start).Max();
            var remaining = Math.Max(0, Remaining(task, bias) - (int)preserve.Where(x => x.TaskId == task.Id).Sum(x => (x.End - x.Start).TotalMinutes));
            // A minimum session preference must never manufacture extra work.
            // Allow a whole short task (or its final remaining work) in one session.
            var minimum = Math.Min(remaining, Math.Max(task.MinimumSessionMinutes, state.Preference.MinimumSessionMinutes));
            if (!task.CanSplit && preserve.Any(x => x.TaskId == task.Id) && remaining > 0)
            { warnings.Add(new(task.Id, task.Name, "NON_SPLITTABLE_PRESERVED", "Phiên đã giữ không đủ thời lượng cho công việc không thể chia nhỏ.", remaining)); continue; }
            var tentative = new List<ProposedSession>(); var trialBudgets = new Dictionary<DateOnly, int>(budgets);
            var preferredWindows = free.Select(slot =>
            {
                var hours = state.Preference.PreferredPeriod switch { "MORNING" => (360, 720), "AFTERNOON" => (720, 1080), "EVENING" => (1080, 1440), _ => (state.Preference.PreferredStartMinute, state.Preference.PreferredEndMinute) };
                var preferredStart = At(Day(slot.Start), hours.Item1, zone);
                var preferredEnd = At(Day(slot.Start), hours.Item2, zone);
                return new TimeSlot(slot.Start > preferredStart ? slot.Start : preferredStart, slot.End < preferredEnd ? slot.End : preferredEnd);
            }).Where(slot => slot.End > slot.Start).ToArray();
            // Prefer the configured productive hours across the horizon. Fall back to
            // other available hours so soft preferences never block a hard deadline.
            var choices = preferredWindows.Concat(task.CanSplit ? free.SelectMany(slot => Subtract(slot, preferredWindows)) : free).ToArray();
            var strictDeadline = task.IsHardDeadline || task.Priority == 4 || urgency?.Displaced.ContainsKey(task.Id) == true ||
                urgency?.ProtectedTaskIds?.Contains(task.Id) == true;
            var deadline = task.Deadline!.Value;
            var candidates = choices.Select(x => new TimeSlot(x.Start, x.End < deadline ? x.End : deadline)).Where(x => x.End > x.Start)
                .Concat(strictDeadline ? [] : choices.Select(x => new TimeSlot(x.Start > deadline ? x.Start : deadline, x.End)).Where(x => x.End > x.Start))
                .Select(x => new TimeSlot(task.WorkWindowStart is { } from && from > x.Start ? from : x.Start,
                    task.WorkWindowEnd is { } to && to < x.End ? to : x.End)).Where(x => x.End > x.Start);
            if (urgency?.Displaced.TryGetValue(task.Id, out var move) == true)
                candidates = DisplacedWindows(candidates, move, remaining, task.CanSplit, state.Preference.MaximumSessionMinutes, zone);
            // Use the earliest feasible local day before scoring hours within it.
            // Productive-hour and fragmentation bonuses must not leave today's
            // usable evening empty just to select tomorrow's preferred morning.
            // Urgent displacement has an explicit proximity/day policy. Optimize
            // ordinary work without overriding that higher-priority constraint.
            var orderedCandidates = urgency?.Displaced.ContainsKey(task.Id) == true
                ? candidates
                : task.Priority == 4
                ? candidates.OrderBy(window => Day(window.Start > earliest ? window.Start : earliest))
                : candidates.OrderBy(window => Day(window.Start > earliest ? window.Start : earliest))
                    .ThenBy(window => ScoreCandidate(task, window, remaining, state.Preference, earliest,
                        trialBudgets.GetValueOrDefault(Day(window.Start))).PenaltyScore).ThenBy(window => window.Start);
            foreach (var slot in orderedCandidates)
            {
                // Preferred windows may be visited before earlier fallback windows.
                // Reserve breaks on both sides of every tentative session.
                var available = Subtract(slot, tentative.Select(x => Block(x.Start, x.End))).ToArray();
                foreach (var window in available)
                {
                    if (remaining <= 0) break;
                    var cursor = window.Start > earliest ? window.Start : earliest;
                    var limit = strictDeadline && deadline < window.End ? deadline : window.End;
                    if (cursor >= limit) continue;
                    var day = Day(cursor); var budget = Math.Max(0, trialBudgets.GetValueOrDefault(day));
                    while (remaining > 0 && cursor < limit && budget > 0)
                    {
                        var space = Math.Min((int)Math.Floor((limit - cursor).TotalMinutes), budget);
                        var duration = task.CanSplit ? Math.Min(remaining, Math.Min(space, state.Preference.MaximumSessionMinutes)) : remaining;
                        if (duration > space || duration > state.Preference.MaximumSessionMinutes || duration < minimum) break;
                        if (task.CanSplit && remaining > duration && remaining - duration < minimum) duration -= minimum - (remaining - duration);
                        if (duration <= 0 || remaining > duration && duration < minimum) break;
                        var reason = urgency?.Displaced.TryGetValue(task.Id, out var moved) == true
                            ? moved.NearDeadline ? "URGENT_NEAR_DEADLINE_MOVE" : "URGENT_FLEXIBLE_MOVE"
                            : task.Priority == 4 ? "URGENT_TASK_FIRST" : "DEADLINE_PRIORITY_CAPACITY";
                        tentative.Add(new(Guid.NewGuid(), task.Id, cursor, cursor.AddMinutes(duration), reason, false));
                        remaining -= duration; budget -= duration; cursor = cursor.AddMinutes(duration + state.Preference.BreakMinutes);
                        if (!task.CanSplit) break;
                    }
                    trialBudgets[day] = budget;
                }
            }
            if (tentative.Any(x => x.End > task.Deadline))
                warnings.Add(new(task.Id, task.Name, "SOFT_DEADLINE_EXCEEDED", "Đề xuất có phiên sau deadline mềm; kiểm tra trước khi áp dụng.", 0));
            if (remaining > 0)
            {
                // Partial sessions are useful, but do not satisfy dependencies until the whole task fits.
                warnings.Add(new(task.Id, task.Name, task.Deadline <= start ? "DEADLINE_PASSED" : task.RequiresContinuousSession ? "NO_CONTIGUOUS_SLOT" : "INSUFFICIENT_CAPACITY",
                    !task.CanSplit && task.Deadline > start
                        ? $"Cần một khoảng liên tục đủ {remaining} phút và trong giới hạn thời lượng một phiên trước hạn chót; không thể ghép các khoảng rảnh rời nhau."
                        : "Không đủ thời gian phù hợp trước hạn chót; phần còn lại chưa được xếp lịch.", remaining));
            }
            schedule.AddRange(tentative); budgets = trialBudgets;
            done = CompletionTimes(state, preservedSessions.Concat(schedule.Where(x => !x.Preserved)), bias, start);
            var allBlocks = preserve.Select(x => Block(x.Start, x.End)).Concat(schedule.Where(x => !x.Preserved).Select(x => Block(x.Start, x.End)));
            free = FocusSlots(state, start, end, allBlocks.Concat(completedBlocks));
        }
        return new(Guid.Empty, state.Preference.Revision, start, end, schedule.OrderBy(x => x.Start).ToArray(), warnings.ToArray(),
            ["Ưu tiên công việc khẩn, sau đó theo deadline và thứ tự phụ thuộc.", "Ưu tiên ngày sớm còn giờ phù hợp và hoàn thành trong ngày nếu đủ thời gian; giờ làm việc ưu tiên không đẩy phần việc còn lại sang ngày sau.", $"Khả năng sử dụng thời gian: {factor:P0}; " + (input.UseEstimateBias ? $"dữ liệu ước lượng chỉ cá nhân hóa từ {MinimumBiasSamples} mẫu." : "dùng thời lượng công việc đã xác định trong yêu cầu."), "Giữ phiên đã khóa/đang thực hiện; không vượt deadline cứng. Phiên sau deadline mềm luôn có cảnh báo."], false, UseEstimateBias: input.UseEstimateBias);
    }
    public IntelligenceView Analyze(PlanningState state, PlanInput input, DateTimeOffset now)
    {
        var start = input.Start < now ? now : input.Start; var end = input.WindowEnd ?? start.AddDays(input.HorizonDays);
        var bias = input.UseEstimateBias ? Biases(state) : [new BiasView("All", 0, 1, false)];
        var capacity = Capacity(state, now); var free = FreeSlots(state, start, end);
        var freeMinutes = (int)free.Sum(x => (x.End - x.Start).TotalMinutes);
        var days = CapacityDays(state, start, end, now, input.UseCapacityFactor);
        var usable = days.Sum(x => x.EffectiveCapacityMinutes);
        var feasibility = Generate(state, input with { Start = start }, now);
        var tasks = state.Tasks.Where(x => x.HasPlanningDetails && Remaining(x, bias) > 0 && !state.Tasks.Any(child => child.ParentId == x.Id && !child.IsDeleted)).ToArray();
        var remaining = tasks.Sum(x => Remaining(x, bias));
        var horizonDemand = tasks.Where(x => x.Deadline <= end).Sum(x => Remaining(x, bias));
        var ratio = usable == 0 ? horizonDemand == 0 ? 0 : 99 : horizonDemand / (double)usable;
        var risks = tasks.Select(task =>
        {
            var deadline = task.Deadline!.Value;
            var minutes = Remaining(task, bias);
            var beforeDeadline = deadline > start
                ? CapacityDays(state, start, deadline < end ? deadline : end, now, input.UseCapacityFactor).Sum(x => x.EffectiveCapacityMinutes) : 0;
            var demand = tasks.Where(x => x.Deadline <= task.Deadline).Sum(x => Remaining(x, bias));
            var reasons = new List<string>(); int score = 0;
            if (task.Deadline <= now) { score += 70; reasons.Add("DEADLINE_PASSED"); }
            if (task.Deadline > end) reasons.Add("DEADLINE_OUTSIDE_HORIZON");
            else if (beforeDeadline < demand || feasibility.Warnings.Any(x => x.TaskId == task.Id && x.UnscheduledMinutes > 0 && x.Code != "DEPENDENCY_UNSCHEDULED"))
            { score += 55; reasons.Add("DEADLINE_CAPACITY_DEFICIT"); }
            if (deadline - now <= TimeSpan.FromHours(48)) { score += 15; reasons.Add("DEADLINE_WITHIN_48_HOURS"); }
            if (feasibility.Warnings.Any(x => x.TaskId == task.Id && x.Code == "DEPENDENCY_UNSCHEDULED"))
            { score += 55; reasons.Add("UNFINISHED_DEPENDENCY"); }
            else if (state.Dependencies.Where(x => x.TaskId == task.Id).Any(d => state.Tasks.Any(t => t.Id == d.PredecessorId && t.Status != WorkTaskStatus.Completed)))
            { score += 20; reasons.Add("UNFINISHED_DEPENDENCY"); }
            if (task.Deadline <= end && !task.CanSplit && !free.Any(s =>
                ((task.IsHardDeadline && deadline < s.End ? deadline : s.End) - s.Start).TotalMinutes >= minutes))
            { score += 30; reasons.Add("NO_CONTIGUOUS_SLOT"); }
            return new RiskView(task.Id, task.Name, score >= 55 ? "High" : score >= 20 ? "Medium" : "Low", Math.Min(100, score), reasons.ToArray(), minutes);
        }).ToArray();
        var explanations = new List<string>();
        if (ratio > 1) explanations.Add("WORKLOAD_EXCEEDS_EFFECTIVE_CAPACITY");
        if (risks.Any(x => x.Reasons.Contains("DEADLINE_CAPACITY_DEFICIT"))) explanations.Add("DEADLINE_CAPACITY_DEFICIT");
        if (tasks.Count(x => x.Deadline <= now.AddHours(48)) >= 3) explanations.Add("DEADLINE_CLUSTER");
        return new(remaining, freeMinutes, usable, Math.Round(capacity.Factor, 3), capacity.Samples, Math.Round(ratio, 2),
            ratio > 1 ? "Overload" : risks.Any(x => x.Level == "High") || ratio > .85 ? "High" : ratio > .6 ? "Moderate" : "Low",
            explanations.ToArray(), bias, risks, (int)state.Sessions.Where(x => x.Status == WorkSessionStatus.Completed).Sum(x => (x.End - x.Start).TotalMinutes), (int)(state.Sessions.Sum(x => x.ActualSeconds) / 60), horizonDemand,
            days, start, end);
    }
}
