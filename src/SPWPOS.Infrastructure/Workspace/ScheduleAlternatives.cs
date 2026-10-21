using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private string BlockedWorkWindowSuggestions(PlanningState state, PlanView plan, bool useEstimateBias)
    {
        var replies = new List<string>();
        var bias = useEstimateBias ? engine.Biases(state) : [new BiasView("All", 0, 1, false)];
        foreach (var warning in plan.Warnings.Where(x => x.UnscheduledMinutes > 0).DistinctBy(x => x.TaskId).Take(3))
        {
            var task = state.Tasks.SingleOrDefault(x => x.Id == warning.TaskId);
            if (task?.WorkWindowStart is not { } requestedStart || task.WorkWindowEnd is null) continue;
            var minutes = engine.Remaining(task, bias);
            if (minutes < Math.Max(task.MinimumSessionMinutes, state.Preference.MinimumSessionMinutes) || minutes > state.Preference.MaximumSessionMinutes) continue;
            var relaxedTask = Copy(task);
            relaxedTask.WorkWindowStart = null; relaxedTask.WorkWindowEnd = null;
            var relaxed = state with { Tasks = state.Tasks.Where(x => x.Id != task.Id).Append(relaxedTask).ToArray() };
            var occupied = Occupied(state).Where(x => x.TaskId != task.Id || state.Sessions.Any(s => s.Id == x.Id &&
                (s.IsLocked || s.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused))).ToArray();
            var blocks = occupied.Select(x => new TimeSlot(x.Start.AddMinutes(-state.Preference.BreakMinutes), x.End.AddMinutes(state.Preference.BreakMinutes)));
            bool Accept(TimeSlot slot)
            {
                try { ValidateSession(relaxed, task.Id, slot.Start, slot.End, occupied, Guid.Empty, false, useEstimateBias, false); return true; }
                catch (AuthException) { return false; }
            }
            replies.Add(CalendarConflict("TASK_WORK_WINDOW", $"“{task.Name}”: các đề xuất sau chỉ áp dụng nếu bạn đồng ý đổi khung giờ thực hiện.",
                relaxed, requestedStart, requestedStart.AddMinutes(minutes), blocks, Accept).Message);
        }
        return string.Join("\n", replies);
    }

    private AuthException CalendarConflict(string code, string message, PlanningState state,
        DateTimeOffset requestedStart, DateTimeOffset requestedEnd, IEnumerable<TimeSlot> occupied,
        Func<TimeSlot, bool>? acceptable = null, IEnumerable<DateTimeOffset>? candidateStarts = null)
    {
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        var day = LocalDate(requestedStart, state.Preference);
        var from = PlanningEngine.At(day, 0, zone);
        if (from < Now) from = Now;
        var until = PlanningEngine.At(day.AddDays(8), 0, zone);
        var duration = requestedEnd - requestedStart;
        var candidates = new List<TimeSlot>();
        long Distance(DateTimeOffset start) => Math.Abs((start - requestedStart).Ticks);
        if (duration > TimeSpan.Zero && until > from)
        {
            foreach (var gap in engine.FreeSlots(state, from, until, occupied))
            {
                var latestStart = gap.End - duration;
                if (latestStart < gap.Start) continue;
                // The nearest placement in an earlier gap is its latest
                // fitting start, rather than the beginning of that gap.
                var nearest = requestedStart < gap.Start ? gap.Start
                    : requestedStart > latestStart ? latestStart : requestedStart;
                var starts = new[] { nearest, gap.Start, latestStart }
                    .Concat(candidateStarts ?? []).Distinct()
                    .OrderBy(Distance).ThenBy(x => x < requestedStart ? 1 : 0).ThenBy(x => x);
                foreach (var start in starts)
                {
                    var alternative = new TimeSlot(start, start + duration);
                    if (alternative.End > gap.End || alternative.Start < gap.Start ||
                        alternative.Start == requestedStart || acceptable is not null && !acceptable(alternative)) continue;
                    candidates.Add(alternative);
                    // Keep one best valid choice per free interval.
                    break;
                }
            }
        }
        var alternatives = candidates.Distinct().OrderBy(x => Distance(x.Start))
            .ThenBy(x => x.Start < requestedStart ? 1 : 0).ThenBy(x => x.Start)
            .Take(3).ToArray();
        var suggestion = alternatives.Length == 0
            ? "Chưa tìm được khoảng rảnh liên tục đủ thời lượng và thỏa các ràng buộc trong 8 ngày từ ngày bạn chọn. Hãy điều chỉnh giờ rảnh hoặc thời lượng."
            : "Đề xuất giữ nguyên thời lượng: " + string.Join("; ", alternatives.Select(x =>
            {
                var localStart = TimeZoneInfo.ConvertTime(x.Start, zone);
                var localEnd = TimeZoneInfo.ConvertTime(x.End, zone);
                var endText = localEnd.TimeOfDay == TimeSpan.Zero && localEnd.Date > localStart.Date ? "24:00" : localEnd.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                return $"{localStart:dd/MM/yyyy HH:mm}–{endText}";
            })) + ". Bạn chọn một khung giờ để thử lại.";
        return new(code, $"{message} Chưa lưu thay đổi. {suggestion}", 409);
    }

    private void RequireSessionTime(bool condition, string code, string message, PlanningState state,
        Guid taskId, DateTimeOffset start, DateTimeOffset end, IEnumerable<ProposedSession> occupied,
        Guid excludeId, bool dependenciesMustBeCompleted, bool useEstimateBias, bool suggestAlternatives)
    {
        if (condition) return;
        if (!suggestAlternatives) throw new AuthException(code, message, 409);
        var others = occupied.Where(x => x.Id != excludeId).ToArray();
        var blocks = others.Select(x => new TimeSlot(x.Start.AddMinutes(-state.Preference.BreakMinutes), x.End.AddMinutes(state.Preference.BreakMinutes)))
            .Concat(state.Sessions.Where(x => x.Id != excludeId && x.Status == WorkSessionStatus.Completed)
                .Select(x => new TimeSlot(x.Start.AddMinutes(-state.Preference.BreakMinutes), x.End.AddMinutes(state.Preference.BreakMinutes))));
        bool Accept(TimeSlot slot)
        {
            try
            {
                ValidateSession(state, taskId, slot.Start, slot.End, others, excludeId, dependenciesMustBeCompleted, useEstimateBias, false);
                return true;
            }
            catch (AuthException) { return false; }
        }
        var bias = useEstimateBias ? engine.Biases(state) : [new BiasView("All", 0, 1, false)];
        var completion = engine.CompletionTimes(state, dependenciesMustBeCompleted ? [] : others, bias, Now);
        var boundaries = completion.Values.Concat(state.Tasks.Where(x => x.Id == taskId && x.WorkWindowStart is not null).Select(x => x.WorkWindowStart!.Value));
        throw CalendarConflict(code, message, state, start, end, blocks, Accept, boundaries);
    }
}
