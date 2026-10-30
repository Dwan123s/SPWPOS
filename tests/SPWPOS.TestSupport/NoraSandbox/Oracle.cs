using System.Globalization;
using System.Text;
using System.Text.Json;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.TestSupport.NoraSandbox;

// This oracle deliberately does not call PlanningEngine/FreeSlots/ValidateSession.
public static class SandboxOracle
{
    public static string Fold(string text) => string.Concat(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant().Replace('đ', 'd');
    private static bool Same<T>(T left, T right)
    {
        static string Comparable(object? row)
        {
            var json = JsonSerializer.SerializeToNode(row, SandboxJson.Options);
            // This token serializes writes, including chat bookkeeping. It is
            // not a change to the user's calendar. Keep Revision and every
            // actual preference in the independent business-change oracle.
            if (row is WorkPreference) json?.AsObject().Remove("mutationVersion");
            return json?.ToJsonString() ?? "null";
        }
        return Comparable(left) == Comparable(right);
    }
    public static bool IsProviderError(string? code) => code is "AGENT_AI_NOT_CONFIGURED" or "AGENT_AI_AUTH_FAILED" or
        "AGENT_AI_MODEL_UNAVAILABLE" or "AGENT_AI_QUOTA_EXCEEDED" or "AGENT_AI_TIMEOUT" or "AGENT_AI_INVALID_RESPONSE" or "AI_DAILY_LIMIT" or "AI_CONCURRENT_LIMIT";
    public static bool SafeRejection(string? code) => code is "AGENT_INVALID_INTENT" or "VALIDATION_ERROR" or "INVALID_TASK_TYPE" or
        "LOCKED_SESSION_CONFLICT" or "FIXED_EVENT_OVERLAP" or "DEADLINE_CONFLICT" or "AGENT_HORIZON_CONFLICT" or
        "AGENT_PREVIEW_EXPIRED" or "AGENT_STALE" or "AGENT_REQUEST_REUSED" or "NOT_FOUND" or "AGENT_UNDO_STALE" or "AGENT_UNDO_STARTED" or "TASK_WORK_WINDOW";

    public static SandboxChange[] Diff(SandboxSnapshot before, SandboxSnapshot after)
    {
        var changes = new List<SandboxChange>();
        void Rows<T>(string entity, IEnumerable<T> oldRows, IEnumerable<T> newRows, Func<T, string> key)
        {
            var old = oldRows.ToDictionary(key); var current = newRows.ToDictionary(key);
            foreach (var id in old.Keys.Union(current.Keys).Order(StringComparer.Ordinal))
            {
                var was = old.TryGetValue(id, out var left); var exists = current.TryGetValue(id, out var right);
                if (was && exists && Same(left, right)) continue;
                changes.Add(new(entity, id, !was ? "ADD" : !exists ? "REMOVE" : "UPDATE",
                    was ? JsonSerializer.SerializeToElement(left, SandboxJson.Options) : null,
                    exists ? JsonSerializer.SerializeToElement(right, SandboxJson.Options) : null));
            }
        }
        Rows("task", before.Tasks, after.Tasks, x => x.Id.ToString());
        Rows("session", before.Sessions, after.Sessions, x => x.Id.ToString());
        Rows("event", before.Events, after.Events, x => x.Id.ToString());
        Rows("weekly", before.Weekly, after.Weekly, x => x.Id.ToString());
        Rows("exception", before.Exceptions, after.Exceptions, x => x.Id.ToString());
        Rows("dependency", before.Dependencies, after.Dependencies, x => x.Id.ToString());
        Rows("preference", new[] { before.Preference }, [after.Preference], _ => "account");
        Rows("proposal", before.Proposals, after.Proposals, x => x.Id.ToString());
        return changes.ToArray();
    }

    public static bool[] FreeMinutes(SandboxSnapshot state, DateOnly date)
    {
        var minutes = new bool[1440];
        var datedFree = state.Exceptions.Where(x => x.Date == date && x.IsAvailable).ToArray();
        var exclusive = datedFree.Any(x => x.OverridesWeeklyAvailability);
        foreach (var slot in state.Weekly.Where(x => !exclusive && x.DayOfWeek == (int)date.DayOfWeek))
            for (var i = Math.Max(0, slot.StartMinute); i < Math.Min(1440, slot.EndMinute); i++) minutes[i] = true;
        foreach (var slot in datedFree.Where(x => !exclusive || x.OverridesWeeklyAvailability))
            for (var i = Math.Max(0, slot.StartMinute); i < Math.Min(1440, slot.EndMinute); i++) minutes[i] = true;
        foreach (var slot in state.Exceptions.Where(x => x.Date == date && !x.IsAvailable))
            for (var i = Math.Max(0, slot.StartMinute); i < Math.Min(1440, slot.EndMinute); i++) minutes[i] = false;
        return minutes;
    }

    public static SandboxCheck[] Evaluate(SandboxExpectation expected, AgentTurnView? result, string? errorCode,
        SandboxSnapshot before, SandboxSnapshot after, DateTimeOffset now)
    {
        var checks = new List<SandboxCheck>();
        void Check(string name, bool passed, string detail, bool critical = false) => checks.Add(new(name, passed, detail, critical));
        var changes = Diff(before, after);
        var actual = result?.Status ?? errorCode ?? "NO_RESULT";
        Check("expected-outcome", expected.Statuses.Contains(actual) || expected.Statuses.Contains("REJECTED") && SafeRejection(errorCode),
            $"Expected {string.Join('/', expected.Statuses)}; actual {actual}.");
        if (expected.Unchanged || result?.Status is "NEEDS_INPUT" or "ANSWERED" or "PREVIEW" or "BLOCKED" || errorCode is not null)
            Check("zero-persisted-changes", changes.Length == 0, $"Observed {changes.Length} persisted changes.", true);
        if (result?.Status == "NEEDS_INPUT")
            Check("clarification-question", !string.IsNullOrWhiteSpace(result.Question) && result.Changes.Length == 0 && result.Plan is null && !result.CanUndo,
                "Clarification must have a question, no proposed changes/plan, and no undo.", true);
        if (expected.TaskCount is { } tasks) Check("task-count", after.Tasks.Count(x => !x.IsDeleted) == tasks, $"Expected {tasks}; actual {after.Tasks.Count(x => !x.IsDeleted)}.");
        if (expected.EventCount is { } events) Check("event-count", after.Events.Length == events, $"Expected {events}; actual {after.Events.Length}.");
        if (expected.ExceptionCount is { } exceptions) Check("exception-count", after.Exceptions.Length == exceptions, $"Expected {exceptions}; actual {after.Exceptions.Length}.");
        foreach (var target in expected.Tasks)
        {
            var matches = after.Tasks.Where(x => !x.IsDeleted && (target.Id is not null ? x.Id.ToString() == target.Id : Fold(x.Name).Contains(Fold(target.Match), StringComparison.Ordinal))).ToArray();
            Check("task-identity:" + target.Match, matches.Length == 1, $"Expected one task matching {target.Match}; actual {matches.Length}.");
            if (matches.Length != 1) continue;
            var task = matches[0];
            Check("task-duration:" + target.Match, task.EstimatedMinutes == target.Minutes, $"Expected {target.Minutes}; actual {task.EstimatedMinutes}.");
            Check("task-deadline:" + target.Match, task.Deadline == DateTimeOffset.Parse(target.Deadline, CultureInfo.InvariantCulture), $"Actual {task.Deadline:O}.");
            if (target.WorkWindowStart is { } from) Check("task-window-start:" + target.Match,
                task.WorkWindowStart == DateTimeOffset.Parse(from, CultureInfo.InvariantCulture), $"Actual {task.WorkWindowStart:O}.", true);
            if (target.WorkWindowEnd is { } to) Check("task-window-end:" + target.Match,
                task.WorkWindowEnd == DateTimeOffset.Parse(to, CultureInfo.InvariantCulture), $"Actual {task.WorkWindowEnd:O}.", true);
            if (target.Splittable is { } split) Check("task-continuity:" + target.Match, task.Splittable == split, $"Expected splittable={split}; actual {task.Splittable}.");
            if (target.Priority is { } priority) Check("task-priority:" + target.Match, task.Priority == priority, $"Expected {priority}; actual {task.Priority}.");
            if (target.Scheduled)
            {
                var total = after.Sessions.Where(x => x.TaskId == task.Id && x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused)
                    .Sum(x => (x.End - x.Start).TotalMinutes);
                var required = target.ScheduledMinutes ?? target.Minutes;
                Check("scheduled-minutes:" + target.Match, total == required, $"Expected {required}; actual {total}.", true);
            }
        }
        foreach (var target in expected.Events)
            Check("exact-event:" + target.Match, after.Events.Any(x => Fold(x.Name).Contains(Fold(target.Match), StringComparison.Ordinal) &&
                x.Start == DateTimeOffset.Parse(target.Start, CultureInfo.InvariantCulture) && x.End == DateTimeOffset.Parse(target.End, CultureInfo.InvariantCulture) &&
                (target.EventType is null || x.EventType == target.EventType)),
                $"Expected {target.Start} -> {target.End}." + (target.EventType is null ? "" : $" Category: {target.EventType}."));
        foreach (var descriptor in expected.Preserve)
        {
            var pair = descriptor.Split(':', 2); var match = Fold(pair[1]);
            if (pair[0] == "id")
            {
                Check("preserved:" + descriptor, !changes.Any(x => x.Id == pair[1]), "Referenced ID must be untouched.", true);
                continue;
            }
            var ids = pair[0] switch {
                "task" => before.Tasks.Where(x => Fold(x.Name).Contains(match, StringComparison.Ordinal)).Select(x => x.Id),
                "event" => before.Events.Where(x => Fold(x.Name).Contains(match, StringComparison.Ordinal)).Select(x => x.Id),
                "session" => before.Sessions.Where(x => before.Tasks.Any(t => t.Id == x.TaskId && Fold(t.Name).Contains(match, StringComparison.Ordinal))).Select(x => x.Id),
                _ => throw new InvalidOperationException("Unknown preserve descriptor")
            };
            Check("preserved:" + descriptor, !changes.Any(x => x.Entity == pair[0] && ids.Any(id => id.ToString() == x.Id)), "Referenced saved entities must be untouched.", true);
        }
        foreach (var id in expected.PreviewPreserveSessionIds)
        {
            var original = before.Sessions.SingleOrDefault(x => x.Id.ToString() == id);
            Check("preview-preserved-session:" + id, original is not null && result?.Plan?.Sessions.Any(x =>
                x.Id == original.Id && x.TaskId == original.TaskId && x.Start == original.Start && x.End == original.End && x.Preserved) == true,
                "Preview must retain the saved session ID, task and interval.", true);
        }
        if (expected.Weekly is { } weekly)
        {
            static string Key(int day, int start, int end) => $"{day}:{start}:{end}";
            Check("weekly-slots", after.Weekly.Select(x => Key(x.DayOfWeek, x.StartMinute, x.EndMinute)).Order().SequenceEqual(weekly.Select(x => Key(x[0], x[1], x[2])).Order()), "Exact weekly slots compared.");
        }
        foreach (var date in expected.Free.Select(x => x.Date).Distinct())
        {
            var wanted = new bool[1440];
            foreach (var window in expected.Free.Where(x => x.Date == date)) for (var i = window.StartMinute; i < window.EndMinute; i++) wanted[i] = true;
            Check("effective-free:" + date, FreeMinutes(after, DateOnly.Parse(date, CultureInfo.InvariantCulture)).SequenceEqual(wanted), "Weekly + available exceptions minus busy exceptions compared.", true);
        }
        var changedIds = changes.Where(x => x.Entity == "session").Select(x => x.Id).ToHashSet();
        var future = after.Sessions.Where(x => x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused).ToArray();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(after.Preference.TimeZoneId);
        foreach (var session in future.Where(x => changedIds.Contains(x.Id.ToString())))
        {
            var task = after.Tasks.Single(x => x.Id == session.TaskId);
            var length = (session.End - session.Start).TotalMinutes;
            var remaining = Math.Ceiling(task.EstimatedMinutes * (1 - task.Progress / 100d));
            var otherWork = future.Where(x => x.TaskId == task.Id && x.Id != session.Id).Sum(x => (x.End - x.Start).TotalMinutes);
            Check("session-length:" + session.Id, (length >= Math.Max(after.Preference.MinimumSessionMinutes, task.MinimumSessionMinutes) ||
                length > 0 && length + otherWork == remaining) && length <= after.Preference.MaximumSessionMinutes,
                $"Actual {length} minutes.", true);
            Check("session-deadline:" + session.Id, !task.IsHardDeadline || session.End <= task.Deadline, "Hard deadline enforced.", true);
            Check("session-work-window:" + session.Id, (task.WorkWindowStart is null || session.Start >= task.WorkWindowStart) &&
                (task.WorkWindowEnd is null || session.End <= task.WorkWindowEnd), "Allowed execution window enforced independently.", true);
            Check("session-future:" + session.Id, session.Start >= now, "New work must start in the future.", true);
            Check("session-active-task:" + session.Id, !task.IsDeleted && task.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled, "No completed/cancelled/deleted work.", true);
            Check("session-event-overlap:" + session.Id, !after.Events.Any(x => x.Start < session.End && session.Start < x.End), "No fixed-event overlap.", true);
            Check("session-overlap:" + session.Id, !future.Any(x => x.Id != session.Id && x.Start < session.End && session.Start < x.End), "No work overlap.", true);
            var isFree = session.End > session.Start;
            for (var at = session.Start; at < session.End && isFree; at = at.AddMinutes(1))
            {
                var local = TimeZoneInfo.ConvertTime(at, zone);
                isFree = FreeMinutes(after, DateOnly.FromDateTime(local.DateTime))[local.Hour * 60 + local.Minute];
            }
            Check("session-free-time:" + session.Id, isFree, "Every scheduled minute fits effective availability.", true);
            foreach (var predecessor in after.Dependencies.Where(x => x.TaskId == task.Id))
            {
                var previous = after.Tasks.Single(x => x.Id == predecessor.PredecessorId);
                var predecessorSessions = after.Sessions.Where(x => x.TaskId == previous.Id && x.Status != WorkSessionStatus.Cancelled && x.Status != WorkSessionStatus.Skipped).ToArray();
                var complete = previous.Status == WorkTaskStatus.Completed || predecessorSessions.Sum(x => (x.End - x.Start).TotalMinutes) >= previous.EstimatedMinutes && predecessorSessions.All(x => x.End <= session.Start);
                Check("dependency:" + session.Id, complete, "Predecessor completes before dependent work starts.", true);
            }
        }
        foreach (var task in after.Tasks.Where(x => !x.Splittable && future.Any(s => s.TaskId == x.Id && changedIds.Contains(s.Id.ToString()))))
            Check("unsplit-task:" + task.Id, future.Count(x => x.TaskId == task.Id) == 1, "Continuous work must be a single session.", true);
        foreach (var task in after.Tasks.Where(x => future.Any(s => s.TaskId == x.Id && changedIds.Contains(s.Id.ToString()))))
            Check("no-excess-work:" + task.Id, future.Where(x => x.TaskId == task.Id).Sum(x => (x.End - x.Start).TotalMinutes) <= Math.Ceiling(task.EstimatedMinutes * (1 - task.Progress / 100d)),
                "Scheduled minutes cannot exceed the raw remaining estimate with bias disabled.", true);
        if (changedIds.Count > 0)
        {
            foreach (var group in after.Sessions.Where(x => x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed)
                .GroupBy(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.Start, zone).DateTime)))
                Check("daily-budget:" + group.Key, group.Sum(x => (x.End - x.Start).TotalMinutes) <= after.Preference.MaximumDailyMinutes, "Completed and reserved work count toward the daily limit.", true);
            var ordered = future.OrderBy(x => x.Start).ToArray();
            for (var i = 1; i < ordered.Length; i++)
                if (changedIds.Contains(ordered[i].Id.ToString()) || changedIds.Contains(ordered[i - 1].Id.ToString()))
                    Check("break:" + ordered[i].Id, (ordered[i].Start - ordered[i - 1].End).TotalMinutes >= after.Preference.BreakMinutes, "Required rest between work sessions.", true);
        }
        foreach (var protectedSession in before.Sessions.Where(x => x.IsLocked || x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused))
            Check("protected-session:" + protectedSession.Id, after.Sessions.Any(x => x.Id == protectedSession.Id && Same(x, protectedSession)), "Locked/running/paused work preserved.", true);
        foreach (var task in after.Tasks.Where(x => changes.Any(c => c.Entity == "task" && c.Id == x.Id.ToString()) && !x.IsDeleted))
            Check("active-category:" + task.Id, after.ActiveTaskTypes.Contains(task.TaskType), "Persisted task category is active.", true);
        foreach (var meeting in after.Events.Where(x => changes.Any(c => c.Entity == "event" && c.Id == x.Id.ToString())))
            Check("event-conflict:" + meeting.Id, !after.Events.Any(x => x.Id != meeting.Id && x.Start < meeting.End && meeting.Start < x.End) &&
                !future.Any(x => x.Start < meeting.End && meeting.Start < x.End), "Fixed event conflicts checked against saved events/work.", true);
        return checks.ToArray();
    }
}
