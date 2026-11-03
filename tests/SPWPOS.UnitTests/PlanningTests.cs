using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed class PlanningTests
{
    private readonly PlanningEngine engine = new();
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static WorkTask Task(string name = "Work", int minutes = 60) => new() { Name = name, EstimatedMinutes = minutes, Deadline = Now.AddDays(1), MinimumSessionMinutes = 25, IsHardDeadline = true };
    [Fact] public void Soft_deadline_can_be_exceeded_only_with_explicit_warning()
    {
        var task = Task(); task.IsHardDeadline = false; task.Deadline = Now.AddHours(-1);
        var plan = engine.Generate(State(task), new(Now, 1), Now);
        Assert.NotEmpty(plan.Sessions); Assert.Contains(plan.Warnings, x => x.Code == "SOFT_DEADLINE_EXCEEDED");
    }
    [Fact] public void Persistent_observations_survive_missing_or_deleted_tasks()
    {
        var history = Enumerable.Range(0, 3).Select(i => new EstimationObservation { TaskType = "WRITING", EstimatedMinutes = 60, ActualMinutes = 90, ObservedAt = Now.AddDays(-i) }).ToArray();
        var bias = engine.Biases(State() with { Observations = history });
        Assert.Equal(1.5, bias[0].Ratio); Assert.Equal(3, bias[0].Samples);
    }
    private static PlanningState State(params WorkTask[] tasks) => new(tasks, [],
        Enumerable.Range(0, 7).Select(day => new WeeklyAvailability { DayOfWeek = day, StartMinute = 8 * 60, EndMinute = 18 * 60 }).ToArray(), [], [], [],
        new() { TimeZoneId = "UTC", MaximumSessionMinutes = 240, BreakMinutes = 10 });
    [Fact] public void Plan_avoids_fixed_events_and_overlap_and_deadlines()
    {
        var a = Task("A", 180); var b = Task("B", 120); var state = State(a, b) with { Events = [new() { Start = Now.AddHours(1), End = Now.AddHours(2) }] };
        var plan = engine.Generate(state, new(Now, 2), Now);
        Assert.Empty(plan.Warnings); Assert.All(plan.Sessions, s => Assert.True(s.End <= state.Tasks.First(t => t.Id == s.TaskId).Deadline));
        foreach (var s in plan.Sessions) Assert.False(s.Start < state.Events[0].End && state.Events[0].Start < s.End);
        Assert.All(plan.Sessions.Zip(plan.Sessions.Skip(1)), pair => Assert.True(pair.First.End <= pair.Second.Start));
    }
    [Fact] public void Dependency_cannot_start_until_all_predecessor_sessions_end()
    {
        var a = Task("Predecessor", 150); var b = Task("Dependent", 60); b.Priority = 4;
        var state = State(a, b) with { Dependencies = [new() { TaskId = b.Id, PredecessorId = a.Id }] };
        var plan = engine.Generate(state, new(Now, 2), Now);
        Assert.Empty(plan.Warnings); Assert.True(plan.Sessions.Where(x => x.TaskId == b.Id).Min(x => x.Start) >= plan.Sessions.Where(x => x.TaskId == a.Id).Max(x => x.End));
    }
    [Fact] public void Unscheduled_predecessor_blocks_dependent_with_reason()
    {
        var a = Task("A", 2000); var b = Task("B", 30); var state = State(a, b) with { Dependencies = [new() { TaskId = b.Id, PredecessorId = a.Id }] };
        var plan = engine.Generate(state, new(Now, 1), Now);
        Assert.DoesNotContain(plan.Sessions, x => x.TaskId == b.Id); Assert.Contains(plan.Warnings, x => x.TaskId == b.Id && x.Code == "DEPENDENCY_UNSCHEDULED");
    }
    [Fact] public void Non_splittable_requires_contiguous_slot()
    {
        var task = Task(minutes: 120); task.Splittable = false; var state = State(task) with { Availability = [new() { DayOfWeek = 1, StartMinute = 480, EndMinute = 540 }, new() { DayOfWeek = 1, StartMinute = 600, EndMinute = 660 }] };
        var plan = engine.Generate(state, new(Now, 1), Now); Assert.Empty(plan.Sessions); Assert.Single(plan.Warnings);
    }
    [Fact] public void Locked_session_is_preserved_and_capacity_not_double_counted()
    {
        var task = Task(minutes: 120); var locked = new WorkSession { TaskId = task.Id, Start = Now.AddHours(1), End = Now.AddHours(2), IsLocked = true };
        var plan = engine.Generate(State(task) with { Sessions = [locked] }, new(Now, 1), Now);
        Assert.Contains(plan.Sessions, s => s.Id == locked.Id && s.Preserved); Assert.Equal(120, plan.Sessions.Sum(s => (s.End - s.Start).TotalMinutes));
    }
    [Fact] public void No_availability_yields_explained_warning()
    { var plan = engine.Generate(State(Task()) with { Availability = [] }, new(Now, 1), Now); Assert.Empty(plan.Sessions); Assert.Equal("INSUFFICIENT_CAPACITY", Assert.Single(plan.Warnings).Code); }
    [Fact] public void Existing_session_outside_horizon_is_not_scheduled_again_inside_horizon()
    {
        var task = Task(minutes: 60); task.Deadline = Now.AddDays(5);
        var future = new WorkSession { TaskId = task.Id, Start = Now.AddDays(2), End = Now.AddDays(2).AddHours(1) };
        var plan = engine.Generate(State(task) with { Sessions = [future] }, new(Now, 1), Now);
        Assert.Empty(plan.Sessions); Assert.Empty(plan.Warnings);
    }
    [Fact] public void Past_missed_scheduled_session_does_not_satisfy_remaining_work()
    {
        var task = Task(minutes: 60);
        var past = new WorkSession { TaskId = task.Id, Start = Now.AddHours(-2), End = Now.AddHours(-1) };
        var plan = engine.Generate(State(task) with { Sessions = [past] }, new(Now, 1), Now);
        Assert.Empty(plan.Warnings); Assert.Equal(60, plan.Sessions.Sum(x => (x.End - x.Start).TotalMinutes));
        Assert.DoesNotContain(plan.Sessions, x => x.Id == past.Id);
    }
    [Fact] public void Expired_deadline_never_scheduled()
    { var task = Task(); task.Deadline = Now.AddMinutes(-1); var plan = engine.Generate(State(task), new(Now, 1), Now); Assert.Empty(plan.Sessions); Assert.Equal("DEADLINE_PASSED", Assert.Single(plan.Warnings).Code); }
    [Fact] public void Bias_requires_samples_and_uses_type_ratio()
    {
        var tasks = Enumerable.Range(0, 3).Select(_ => Task()).ToArray(); foreach (var t in tasks) { t.Status = WorkTaskStatus.Completed; t.TaskType = "Programming"; }
        var state = State(tasks) with { Sessions = tasks.Select(t => new WorkSession { TaskId = t.Id, Status = WorkSessionStatus.Completed, ActualSeconds = 90 * 60 }).ToArray() };
        var bias = engine.Biases(state); var next = Task(minutes: 120); next.TaskType = "Programming";
        Assert.Equal(180, engine.Adjusted(next, bias)); Assert.Equal(1, engine.Biases(state with { Tasks = tasks.Take(2).ToArray() })[0].Ratio);
    }
    [Fact] public void Capacity_uses_completed_planned_work_not_actual_overruns()
    {
        var sessions = Enumerable.Range(0, 8).Select(i => new WorkSession { Start = Now.AddHours(-i - 2), End = Now.AddHours(-i - 1), Status = i < 6 ? WorkSessionStatus.Completed : WorkSessionStatus.Skipped, ActualSeconds = 120 * 60 }).ToArray();
        // Two skipped sessions on the older local day, then six completions:
        // daily EMA = .2 * 1 + .8 * 0, limited by the safety floor .25.
        Assert.Equal(.25, engine.Capacity(State() with { Sessions = sessions }, Now).Factor);
        foreach (var session in sessions) session.ActualSeconds = 60;
        Assert.Equal(.25, engine.Capacity(State() with { Sessions = sessions }, Now).Factor);
    }
    [Fact] public void Unavailable_exception_overrides_weekly_and_extra_availability()
    {
        var date = DateOnly.FromDateTime(Now.DateTime); var state = State(Task()) with { Exceptions = [new() { Date = date, StartMinute = 480, EndMinute = 600, IsAvailable = false }] };
        Assert.All(engine.FreeSlots(state, Now, Now.AddDays(1)), x => Assert.True(x.Start >= Now.AddHours(2)));
    }
    [Fact] public void Cycle_is_detected()
    { var a = Task(); var b = Task(); Assert.True(PlanningEngine.HasCycle([a, b], [new() { TaskId = a.Id, PredecessorId = b.Id }, new() { TaskId = b.Id, PredecessorId = a.Id }])); }
    [Fact] public void Dependency_cycle_includes_implicit_parent_completion()
    {
        var parent = Task(); var child = Task(); child.ParentId = parent.Id; var other = Task();
        Assert.True(PlanningEngine.HasCycle([parent, child], [new() { TaskId = child.Id, PredecessorId = parent.Id }]));
        Assert.True(PlanningEngine.HasCycle([parent, child, other], [new() { TaskId = child.Id, PredecessorId = other.Id }, new() { TaskId = other.Id, PredecessorId = parent.Id }]));
        Assert.False(PlanningEngine.HasCycle([parent, child], [new() { TaskId = parent.Id, PredecessorId = child.Id }]));
    }
    [Fact] public void Earlier_fallback_sessions_and_other_tasks_keep_breaks_around_preferred_and_locked_sessions()
    {
        var a = Task(minutes: 120); var b = Task(minutes: 60);
        var preference = new WorkPreference { TimeZoneId = "UTC", MinimumSessionMinutes = 25, MaximumSessionMinutes = 60, BreakMinutes = 15, PreferredStartMinute = 540, PreferredEndMinute = 600 };
        var locked = new WorkSession { TaskId = b.Id, Start = Now.AddHours(3), End = Now.AddHours(4), IsLocked = true };
        var plan = engine.Generate(State(a, b) with { Preference = preference, Sessions = [locked] }, new(Now, 1), Now);
        Assert.Empty(plan.Warnings);
        Assert.Equal(120, plan.Sessions.Where(x => x.TaskId == a.Id).Sum(x => (x.End - x.Start).TotalMinutes));
        Assert.All(plan.Sessions.Zip(plan.Sessions.Skip(1)), pair => Assert.True((pair.Second.Start - pair.First.End).TotalMinutes >= 15));
    }
    [Fact] public void Distant_deadline_is_not_declared_infeasible_from_a_short_analysis_horizon()
    {
        var task = Task(minutes: 1500); task.Deadline = Now.AddDays(60); task.Splittable = false;
        var state = State(task) with { Availability = Enumerable.Range(0, 7).Select(d => new WeeklyAvailability { DayOfWeek = d, StartMinute = 480, EndMinute = 720 }).ToArray() };
        var result = engine.Analyze(state, new(Now, 7), Now);
        Assert.Equal(1500, result.RemainingMinutes); Assert.Equal(0, result.HorizonDemandMinutes); Assert.Equal(0, result.LoadRatio);
        Assert.Equal("Low", result.RiskLevel);
        Assert.Contains("DEADLINE_OUTSIDE_HORIZON", Assert.Single(result.Tasks).Reasons);
        Assert.DoesNotContain("DEADLINE_CAPACITY_DEFICIT", result.Tasks[0].Reasons); Assert.DoesNotContain("NO_CONTIGUOUS_SLOT", result.Tasks[0].Reasons);
        task.Deadline = Now.AddDays(1);
        Assert.Contains("DEADLINE_CAPACITY_DEFICIT", engine.Analyze(state, new(Now, 7), Now).Tasks[0].Reasons);
    }
    [Fact] public void Reschedule_keeps_unaffected_future_sessions()
    {
        var a = Task(); var b = Task(); var session = new WorkSession { TaskId = b.Id, Start = Now.AddHours(2), End = Now.AddHours(3) };
        var plan = engine.Generate(State(a, b) with { Sessions = [session] }, new(Now, 1, [a.Id]), Now);
        Assert.Contains(plan.Sessions, s => s.Id == session.Id && s.Preserved);
    }
    [Fact] public void Parent_containers_do_not_double_count_child_workload()
    {
        var parent = Task(minutes: 200); var child = Task(minutes: 60); child.ParentId = parent.Id;
        Assert.Equal(60, engine.Analyze(State(parent, child), new(Now, 1), Now).RemainingMinutes);
    }
    [Fact] public void Dst_gap_moves_nonexistent_wall_time_to_next_valid_minute()
    {
        var zone = PlanningEngine.Zone("America/New_York");
        var instant = PlanningEngine.At(new DateOnly(2026, 3, 8), 150, zone);
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        Assert.Equal(3, local.Hour); Assert.Equal(0, local.Minute);
    }
    [Fact] public void Available_evening_is_used_before_next_days_preferred_hours_and_respects_deadline()
    {
        var evening = new DateTimeOffset(Now.UtcDateTime.Date, TimeSpan.Zero).AddHours(19); var task = Task(minutes: 60); task.Deadline = evening.AddDays(1);
        var state = State(task) with { Availability = Enumerable.Range(0, 7).Select(day => new WeeklyAvailability { DayOfWeek = day, StartMinute = 0, EndMinute = 1440 }).ToArray() };
        var plan = engine.Generate(state, new(evening, 2), evening);
        Assert.Empty(plan.Warnings);
        Assert.Equal(evening, Assert.Single(plan.Sessions).Start);
        Assert.Equal(evening.AddHours(1), plan.Sessions.Single().End);
        task.Deadline = evening.AddHours(2);
        var urgent = engine.Generate(state, new(evening, 2), evening);
        Assert.Empty(urgent.Warnings); Assert.True(urgent.Sessions.Single().End <= task.Deadline);
    }
}
