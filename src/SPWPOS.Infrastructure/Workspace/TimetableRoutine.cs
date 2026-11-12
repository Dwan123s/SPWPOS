using System.Text.Json;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private RoutineDay[] CurrentRoutineDays(PlanningState state) => Enumerable.Range(0, 7)
        .Select(i => state.Policy?.ForDate(LocalDate(Now, state.Preference).AddDays(i))).OfType<RoutineDay>().ToArray();
    // These are reviewable proposals, not inferred personal habits. Only the
    // confirmed policy is used by the real scheduler.
    private static DateOnly RoutineWeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    private AvailabilityPolicy ProposeTimetableRoutine(PlanningState state, DateOnly? weekStart = null, bool useTimetableHours = false)
    {
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        var first = weekStart ?? RoutineWeekStart(LocalDate(state.Events.Where(x => x.End > Now).Select(x => x.Start).DefaultIfEmpty(Now).Min(), state.Preference));
        var days = new List<RoutineDay>();
        int Minute(DateTimeOffset time) { var local = TimeZoneInfo.ConvertTime(time, zone); return local.Hour * 60 + local.Minute; }
        int Wake(DateOnly date)
        {
            var firstEvent = state.Events.Where(x => LocalDate(x.Start, state.Preference) == date && Minute(x.Start) < 720)
                .OrderBy(x => x.Start).FirstOrDefault();
            var baseline = state.Policy?.AwakeStart ?? 420;
            return firstEvent is null ? baseline : Math.Clamp(Math.Min(baseline, Minute(firstEvent.Start) - 60), 0, baseline);
        }
        var policy = new AvailabilityPolicy { UserId = state.Preference.UserId, AwakeStart = state.Policy?.AwakeStart ?? 420, AwakeEnd = state.Policy?.AwakeEnd ?? 1380,
            LunchStart = 720, LunchEnd = 750, DinnerStart = 1080, DinnerEnd = 1110, CommuteMinutes = state.Policy?.CommuteMinutes ?? 30, FocusMinutes = state.Policy?.FocusMinutes ?? 45 };
        for (var date = first; date < first.AddDays(7); date = date.AddDays(1))
        {
            var wake = Wake(date);
            var bedtime = Math.Min(policy.AwakeEnd, 1440 + Wake(date.AddDays(1)) - 480);
            var from = PlanningEngine.At(date, 0, zone); var to = PlanningEngine.At(date.AddDays(1), 0, zone);
            var events = state.Events.Where(x => x.Start < to && x.End > from).ToArray();
            var lastEvent = events.Select(x => Math.Min(1440, (int)(x.End - from).TotalMinutes)).DefaultIfEmpty(0).Max();
            bedtime = Math.Clamp(Math.Max(bedtime, lastEvent + 60), wake + 1, 1440);
            var blocks = PlanningEngine.Merge(events.Select(x => new TimeSlot(x.Start, x.End)))
                .Select(x => new TimeSlot(x.Start.AddMinutes(-policy.CommuteMinutes), x.End.AddMinutes(policy.CommuteMinutes)))
                .Concat(state.Sessions.Where(x => x.Start < to && x.End > from && x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused)
                    .Select(x => new TimeSlot(x.Start, x.End)))
                .Concat(state.Exceptions.Where(x => x.Date == date && !x.IsAvailable).Select(x => new TimeSlot(PlanningEngine.At(date, x.StartMinute, zone), PlanningEngine.At(date, x.EndMinute, zone)))).ToList();
            (int Start, int End) Place(int preferred, int earliest, int latest, int length)
            {
                var window = new TimeSlot(PlanningEngine.At(date, Math.Max(wake, earliest), zone), PlanningEngine.At(date, Math.Min(bedtime, latest), zone));
                var options = PlanningEngine.Subtract(window, blocks).Where(x => (x.End - x.Start).TotalMinutes >= length)
                    .Select(x => Math.Clamp(preferred, (int)(x.Start - from).TotalMinutes, (int)(x.End - from).TotalMinutes - length))
                    .OrderBy(x => Math.Abs(x - preferred)).ThenBy(x => x).ToArray();
                if (options.Length == 0) return (0, 0);
                var at = options[0]; blocks.Add(new(PlanningEngine.At(date, at, zone), PlanningEngine.At(date, at + length, zone))); return (at, at + length);
            }
            var breakfast = Place(wake + 15, wake, Math.Min(660, wake + 120), 30);
            var lunch = Place(720, 660, 900, 30); var dinner = Place(1080, 1020, 1260, 30);
            var rest = Place(900, Math.Max(840, lunch.End), Math.Min(1260, bedtime), 15);
            days.Add(new(date, wake, bedtime, lunch.Start, lunch.End, dinner.Start, dinner.End, rest.Start, rest.End, breakfast.Start, breakfast.End,
                useTimetableHours || state.Policy?.ForDate(date)?.UseTimetableHours == true));
        }
        var previous = state.Policy is null ? [] : JsonSerializer.Deserialize<RoutineDay[]>(state.Policy.RoutineDaysJson) ?? [];
        policy.RoutineDaysJson = JsonSerializer.Serialize(days.Concat(previous.Where(x => x.Date < first || x.Date >= first.AddDays(7))));
        return policy;
    }
    private RoutineBlock[] RoutineTimeline(PlanningState state, DateOnly day, TimeSlot[] gaps)
    {
        if (state.Policy is not { } policy) return [];
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        var d = policy.ForDate(day) ?? new(day, policy.AwakeStart, policy.AwakeEnd, policy.LunchStart, policy.LunchEnd, policy.DinnerStart, policy.DinnerEnd);
        var first = PlanningEngine.At(day, 0, zone); var last = PlanningEngine.At(day.AddDays(1), 0, zone);
        var rows = new List<RoutineBlock>();
        void Add(string kind, string label, int start, int end) { if (end > start) rows.Add(new(kind, label, PlanningEngine.At(day, start, zone), PlanningEngine.At(day, end, zone))); }
        Add("SLEEP", "Ngủ / nghỉ ban đêm", 0, d.AwakeStart); Add("SLEEP", "Đi ngủ", d.AwakeEnd, 1440);
        Add("MEAL", $"Ăn trưa · {d.LunchEnd - d.LunchStart} phút", d.LunchStart, d.LunchEnd); Add("MEAL", $"Ăn tối · {d.DinnerEnd - d.DinnerStart} phút", d.DinnerStart, d.DinnerEnd);
        Add("REST", "Nghỉ ngơi", d.RestStart, d.RestEnd);
        Add("MEAL", "Ăn sáng", d.BreakfastStart, d.BreakfastEnd);
        rows.AddRange(state.Events.Where(x => x.Start < last && x.End > first).Select(x => new RoutineBlock("FIXED", x.Name, x.Start < first ? first : x.Start, x.End > last ? last : x.End)));
        rows.AddRange(state.Exceptions.Where(x => x.Date == day && !x.IsAvailable).Select(x => new RoutineBlock("EXTRA", string.IsNullOrWhiteSpace(x.Note) ? "Hoạt động riêng" : x.Note, PlanningEngine.At(day, x.StartMinute, zone), PlanningEngine.At(day, x.EndMinute, zone))));
        rows.AddRange(gaps.Select(x => new RoutineBlock("FREE", "Thời gian rảnh", x.Start, x.End)));
        var fixedBlocks = state.Events.Where(x => x.Start < last && x.End > first).Select(x => new TimeSlot(x.Start, x.End)).ToArray();
        var occupied = rows.Where(x => x.Kind != "FREE").Select(x => new TimeSlot(x.Start, x.End)).ToArray();
        foreach (var block in PlanningEngine.Merge(fixedBlocks).Select(x => new TimeSlot(x.Start.AddMinutes(-policy.CommuteMinutes), x.End.AddMinutes(policy.CommuteMinutes))))
        {
            var clipped = new TimeSlot(block.Start < first ? first : block.Start, block.End > last ? last : block.End);
            rows.AddRange(PlanningEngine.Subtract(clipped, occupied).Select(x => new RoutineBlock("COMMUTE", "Di chuyển / chuẩn bị", x.Start, x.End)));
        }
        return rows.OrderBy(x => x.Start).ToArray();
    }
}
