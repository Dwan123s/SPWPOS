using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

// Defense question: can the planner call a feasible small workload impossible?
// Exhaustive reference search uses only integer slots, never planner helpers.
[Trait("Category", "Defense")]
public sealed class DefensePlanningTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 19, 0, 0, TimeSpan.Zero);
    private sealed record Job(int Length, int Deadline);

    private static bool CanFit(Job[] jobs, bool[] available, int rest)
    {
        // Invariant: placements contain complete, available, mutually separated
        // jobs [0,index). Each recursion places one more job; depth <= jobs.Length.
        var placements = new List<(int Start, int End)>();
        bool Place(int index)
        {
            if (index == jobs.Length) return true;
            var job = jobs[index];
            for (var start = 0; start + job.Length <= Math.Min(job.Deadline, available.Length); start++)
            {
                var end = start + job.Length;
                if (Enumerable.Range(start, job.Length).Any(slot => !available[slot]) ||
                    placements.Any(old => !(end + rest <= old.Start || old.End + rest <= start))) continue;
                placements.Add((start, end));
                if (Place(index + 1)) return true;
                placements.RemoveAt(placements.Count - 1);
            }
            return false;
        }
        return Place(0);
    }

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(1, false, false)]
    [InlineData(0, true, false)]
    public void Reference_search_distinguishes_exact_fit_from_rest_or_fragmentation(int rest, bool blocked, bool expected)
    {
        var available = Enumerable.Repeat(true, 6).ToArray();
        if (blocked) available[2] = false;
        Assert.Equal(expected, CanFit([new(4, 4), new(2, 6)], available, rest));
    }

    [Theory]
    [InlineData(1, 1, 0, false)] [InlineData(1, 4, 0, false)]
    [InlineData(4, 1, 0, false)] [InlineData(4, 4, 0, false)]
    [InlineData(1, 1, 1, false)] [InlineData(1, 4, 1, false)]
    [InlineData(4, 1, 1, false)] [InlineData(4, 4, 1, false)]
    [InlineData(1, 1, 0, true)] [InlineData(1, 4, 0, true)]
    [InlineData(4, 1, 0, true)] [InlineData(4, 4, 0, true)]
    [InlineData(1, 1, 1, true)] [InlineData(1, 4, 1, true)]
    [InlineData(4, 1, 1, true)] [InlineData(4, 4, 1, true)]
    public void Small_workloads_match_independent_feasibility_search(int priorityA, int priorityB, int rest, bool blocked)
    {
        var engine = new PlanningEngine();
        var failures = new List<string>();
        var available = Enumerable.Repeat(true, 6).ToArray();
        if (blocked) available[2] = false;
        // 16 priority/rest/busy combinations x 144 duration/deadline pairs = 2304.
        foreach (var lengthA in Enumerable.Range(1, 4))
        foreach (var lengthB in Enumerable.Range(1, 4))
        foreach (var deadlineA in new[] { 2, 4, 6 })
        foreach (var deadlineB in new[] { 2, 4, 6 })
        {
            Job[] jobs = [new(lengthA, deadlineA), new(lengthB, deadlineB)];
            var tasks = jobs.Select((job, index) => new WorkTask {
                Id = new Guid(index == 0 ? "00000000-0000-0000-0000-000000000001" : "00000000-0000-0000-0000-000000000002"),
                Name = index == 0 ? "A" : "B", EstimatedMinutes = job.Length * 15,
                Deadline = Start.AddMinutes(job.Deadline * 15), Priority = index == 0 ? priorityA : priorityB,
                Splittable = false, MinimumSessionMinutes = 15, IsHardDeadline = true
            }).ToArray();
            var state = new PlanningState(tasks, [], [new() { DayOfWeek = 1, StartMinute = 1140, EndMinute = 1230 }], [],
                blocked ? [new() { Start = Start.AddMinutes(30), End = Start.AddMinutes(45) }] : [], [],
                new() { TimeZoneId = "UTC", MinimumSessionMinutes = 15, MaximumSessionMinutes = 90,
                    MaximumDailyMinutes = 90, BreakMinutes = rest * 15, PreferredStartMinute = 1140, PreferredEndMinute = 1230 });
            var plan = engine.Generate(state, new(Start, 1, UseCapacityFactor: false, UseEstimateBias: false), Start.AddHours(-1));
            var label = $"A={lengthA * 15}m/due+{deadlineA * 15}; B={lengthB * 15}m/due+{deadlineB * 15}";
            // Check validity even when the algorithm reports a partial plan.
            var ordered = plan.Sessions.OrderBy(session => session.Start).ToArray();
            foreach (var session in ordered)
            {
                var task = Assert.Single(tasks, task => task.Id == session.TaskId);
                Assert.True(session.Start >= Start && session.End <= task.Deadline, label);
                Assert.Equal(task.EstimatedMinutes, (session.End - session.Start).TotalMinutes);
                Assert.True(session.End <= Start.AddMinutes(90), label);
                if (blocked) Assert.False(session.Start < Start.AddMinutes(45) && Start.AddMinutes(30) < session.End, label);
            }
            foreach (var pair in ordered.Zip(ordered.Skip(1)))
                Assert.True((pair.Second.Start - pair.First.End).TotalMinutes >= rest * 15, label);
            Assert.All(ordered.GroupBy(session => session.TaskId), group => Assert.Single(group));
            var completed = tasks.All(task => ordered.Any(session => session.TaskId == task.Id));
            var feasible = CanFit(jobs, available, rest);
            if (completed != feasible) failures.Add($"{label}: reference feasible={feasible}, planner complete={completed}");
            if (!completed) Assert.Contains(plan.Warnings, warning => warning.UnscheduledMinutes > 0);
        }
        Assert.True(failures.Count == 0, $"{failures.Count}/144 feasibility mismatches. First examples:\n" + string.Join("\n", failures.Take(8)));
    }
}
