using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static void Window(int start, int end) => Require(start >= 0 && start < end && end <= 1440, "INVALID_TIME_WINDOW", "Giờ bắt đầu phải trước giờ kết thúc, trong cùng ngày.");
    public Task SetAvailabilityAsync(Guid uid, WeeklyInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        Require(input.Slots.Length <= 28, "VALIDATION_ERROR", "Tối đa 28 khoảng thời gian mỗi tuần.");
        foreach (var slot in input.Slots) { Window(slot.StartMinute, slot.EndMinute); Require(slot.DayOfWeek is >= 0 and <= 6, "VALIDATION_ERROR", "Ngày không hợp lệ."); }
        foreach (var group in input.Slots.GroupBy(x => x.DayOfWeek))
        {
            var ordered = group.OrderBy(x => x.StartMinute).ToArray();
            Require(!ordered.Zip(ordered.Skip(1)).Any(x => x.First.EndMinute > x.Second.StartMinute), "AVAILABILITY_OVERLAP", "Các khoảng thời gian rảnh không được trùng nhau.");
        }
        var state = await State(uid, ct);
        var replacement = input.Slots.Select(x => new WeeklyAvailability { UserId = uid, DayOfWeek = x.DayOfWeek, StartMinute = x.StartMinute, EndMinute = x.EndMinute }).ToArray();
        ValidateProtectedSessions(state with { Availability = replacement });
        db.WeeklyAvailability.RemoveRange(await Mine<WeeklyAvailability>(uid).ToArrayAsync(ct)); db.WeeklyAvailability.AddRange(replacement);
    }, ct);
    public Task<Guid> SaveExceptionAsync(Guid uid, Guid? id, ExceptionInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        Window(input.StartMinute, input.EndMinute); Require(input.Date.Year is >= 2000 and <= 2100, "VALIDATION_ERROR", "Ngày không hợp lệ.");
        var state = await State(uid, ct); var item = id is { } exceptionId ? await Find<AvailabilityException>(uid, exceptionId, ct) : new AvailabilityException { UserId = uid };
        item.Date = input.Date; item.StartMinute = input.StartMinute; item.EndMinute = input.EndMinute; item.IsAvailable = input.IsAvailable; item.Note = input.Note;
        ValidateProtectedSessions(state with { Exceptions = state.Exceptions.Where(x => x.Id != item.Id).Append(item).ToArray() });
        if (id is null) db.AvailabilityExceptions.Add(item); return item.Id;
    }, ct);
    public Task DeleteExceptionAsync(Guid uid, Guid id, CancellationToken ct) => Mutate(uid, async () =>
    {
        var item = await Find<AvailabilityException>(uid, id, ct); var state = await State(uid, ct);
        ValidateProtectedSessions(state with { Exceptions = state.Exceptions.Where(x => x.Id != id).ToArray() }); db.AvailabilityExceptions.Remove(item);
    }, ct);
    public Task<Guid> SaveEventAsync(Guid uid, Guid? id, EventInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        Require(id is null || input.RepeatWeeks == 1, "VALIDATION_ERROR", "Khi sửa sự kiện, chỉ cập nhật lần xuất hiện đang chọn.");
        var state = await State(uid, ct); var occurrences = EventOccurrences(uid, input, state.Preference);
        ValidateEventConflicts(state, occurrences, id);
        if (id is { } eventId)
        {
            var item = await Find<FixedEvent>(uid, eventId, ct); var occurrence = occurrences[0];
            item.Name = occurrence.Name; item.Start = occurrence.Start; item.End = occurrence.End; item.Description = occurrence.Description; item.EventType = occurrence.EventType;
            return item.Id;
        }
        db.FixedEvents.AddRange(occurrences); return occurrences[0].Id;
    }, ct);
    private static FixedEvent[] EventOccurrences(Guid uid, EventInput input, WorkPreference preference)
    {
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 200 && input.Start < input.End && (input.End - input.Start).TotalDays <= 31 &&
            input.RepeatWeeks is >= 1 and <= 12 && input.Description is not null && input.Description.Length <= 1000 &&
            input.EventType is "MEETING" or "CLASS" or "APPOINTMENT" or "WORK" or "OTHER", "VALIDATION_ERROR", "Sự kiện hoặc số tuần lặp không hợp lệ.");
        var zone = PlanningEngine.Zone(preference.TimeZoneId);
        var start = TimeZoneInfo.ConvertTime(input.Start, zone).DateTime; var end = TimeZoneInfo.ConvertTime(input.End, zone).DateTime;
        return Enumerable.Range(0, input.RepeatWeeks).Select(index =>
        {
            var from = start.AddDays(index * 7); var to = end.AddDays(index * 7);
            Require(index == 0 || !zone.IsInvalidTime(from) && !zone.IsInvalidTime(to) && !zone.IsAmbiguousTime(from) && !zone.IsAmbiguousTime(to),
                "INVALID_TIME_WINDOW", "Một lần lặp rơi vào giờ chuyển múi giờ không rõ ràng; hãy nhập riêng lần đó.");
            return new FixedEvent { UserId = uid, Name = input.Name.Trim(), Description = input.Description, EventType = input.EventType,
                Start = index == 0 ? input.Start : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(from, zone)),
                End = index == 0 ? input.End : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(to, zone)) };
        }).ToArray();
    }
    private void ValidateEventConflicts(PlanningState state, FixedEvent[] events, Guid? excludeId)
    {
        foreach (var item in events)
        {
            var conflict = state.Events.Concat(events.Where(x => x.Id != item.Id)).FirstOrDefault(x => x.Id != excludeId && x.Start < item.End && item.Start < x.End);
            Require(conflict is null, "FIXED_EVENT_OVERLAP", conflict is null ? "Sự kiện bị trùng lịch."
                : $"Sự kiện trùng với “{conflict.Name}” ({LocalWindow(conflict.Start, conflict.End, state.Preference)}). Chưa lưu thay đổi. Hãy chọn giờ khác hoặc chỉnh sự kiện đang trùng.", 409);
            Require(!state.Sessions.Any(x => x.Start < item.End && item.Start < x.End &&
                (x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused || x.Status == WorkSessionStatus.Scheduled && (x.IsLocked || x.Start < Now))),
                "LOCKED_SESSION_CONFLICT", "Sự kiện trùng với phiên đã khóa hoặc đang thực hiện.", 409);
        }
    }
    public Task DeleteEventAsync(Guid uid, Guid id, CancellationToken ct) => Mutate(uid, async () => db.FixedEvents.Remove(await Find<FixedEvent>(uid, id, ct)), ct);
    public Task SetPreferenceAsync(Guid uid, PreferenceInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        Require(input.MinimumSessionMinutes >= 5 && input.MaximumSessionMinutes >= input.MinimumSessionMinutes && input.MaximumSessionMinutes <= 480 && input.BreakMinutes is >= 0 and <= 120,
            "VALIDATION_ERROR", "Giới hạn phiên hoặc thời gian nghỉ không hợp lệ."); Window(input.PreferredStartMinute, input.PreferredEndMinute);
        try { PlanningEngine.Zone(input.TimeZoneId); } catch (TimeZoneNotFoundException) { throw new SPWPOS.Application.Authentication.AuthException("INVALID_TIMEZONE", "Múi giờ không hợp lệ.", 400); }
        var preference = await Preference(uid, ct); preference.TimeZoneId = input.TimeZoneId; preference.MinimumSessionMinutes = input.MinimumSessionMinutes;
        preference.MaximumSessionMinutes = input.MaximumSessionMinutes; preference.BreakMinutes = input.BreakMinutes;
        preference.PreferredStartMinute = input.PreferredStartMinute; preference.PreferredEndMinute = input.PreferredEndMinute;
        Require(input.PlanningHorizonDays is >= 1 and <= 30 && input.PreferredPeriod is "MORNING" or "AFTERNOON" or "EVENING" or "NONE", "VALIDATION_ERROR", "Tùy chọn kế hoạch không hợp lệ.");
        Require(input.MaximumDailyMinutes is >= 5 and <= 1440, "VALIDATION_ERROR", "Giới hạn mỗi ngày phải từ 5 đến 1440 phút.");
        preference.MaximumDailyMinutes = input.MaximumDailyMinutes; preference.AutoReschedule = input.AutoReschedule;
        preference.PlanningHorizonDays = input.PlanningHorizonDays; preference.PreferredPeriod = input.PreferredPeriod;
        var profile = await EnsureProfileAsync(uid, ct); profile.TimeZoneId = input.TimeZoneId; profile.UpdatedAt = Now;
        ValidateProtectedSessions(await State(uid, ct));
    }, ct);
    private void ValidateProtectedSessions(PlanningState state)
    {
        foreach (var session in state.Sessions.Where(x => x.End > Now && (x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused || x.Status == WorkSessionStatus.Scheduled && x.IsLocked)))
            Require(PlanningEngine.InsideWorkWindow(state.Tasks.Single(x => x.Id == session.TaskId), session.Start, session.End),
                "LOCKED_SESSION_CONFLICT", "Khung giờ thực hiện mới xung đột với phiên đã khóa hoặc đang thực hiện.", 409);
        foreach (var active in state.Sessions.Where(x => x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused ||
            x.Status == WorkSessionStatus.Scheduled && x.End > Now && x.Start < Now))
            Require(!state.Events.Any(x => x.Start < active.End && active.Start < x.End) &&
                engine.FreeSlots(state, active.Start, active.End).Any(x => x.Start <= active.Start && x.End >= active.End),
                "LOCKED_SESSION_CONFLICT", "Thay đổi thời gian xung đột với phiên đang thực hiện hoặc đã bắt đầu.", 409);
        foreach (var session in state.Sessions.Where(x => x.Start >= Now && x.Status == WorkSessionStatus.Scheduled && x.IsLocked))
        {
            var free = engine.FreeSlots(state, session.Start, session.End);
            Require(free.Any(x => x.Start <= session.Start && x.End >= session.End), "LOCKED_SESSION_CONFLICT", "Thay đổi thời gian rảnh xung đột với phiên đã khóa.", 409);
            var task = state.Tasks.Single(x => x.Id == session.TaskId);
            Require((session.End - session.Start).TotalMinutes >= Math.Max(task.MinimumSessionMinutes, state.Preference.MinimumSessionMinutes) &&
                (session.End - session.Start).TotalMinutes <= state.Preference.MaximumSessionMinutes, "LOCKED_SESSION_CONFLICT", "Giới hạn phiên mới xung đột với phiên đã khóa.", 409);
        }
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        foreach (var group in state.Sessions.Where(x => x.Status is WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed ||
            x.Status == WorkSessionStatus.Scheduled && x.IsLocked && x.End > Now)
            .GroupBy(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.Start, zone).DateTime)))
            Require(group.Sum(x => (x.End - x.Start).TotalMinutes) <= state.Preference.MaximumDailyMinutes ||
                group.Key < DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Now, zone).DateTime),
                "LOCKED_SESSION_CONFLICT", "Giới hạn mỗi ngày nhỏ hơn tổng thời gian đã thực hiện hoặc đã khóa.", 409);
    }

    private async Task RescheduleTimeConflictsAsync(Guid uid, CancellationToken ct)
    {
        var state = await State(uid, ct);
        var affected = TimeConflictTaskIds(state);
        if (affected.Length == 0) return;
        if (!state.Preference.AutoReschedule)
        {
            await AddNotification(uid, "SCHEDULE_CHANGE", "Cần kiểm tra lại lịch", "Thời gian đã thay đổi. Có phiên cần xếp lại; chế độ tự điều chỉnh đang tắt.", "time:" + state.Preference.Revision, null, ct);
            return;
        }
        Require(!state.Sessions.Any(x => affected.Contains(x.TaskId) && x.Status == WorkSessionStatus.Scheduled && x.Start >= Now.AddDays(30) &&
            !engine.FreeSlots(state, x.Start, x.End).Any(s => s.Start <= x.Start && s.End >= x.End)), "TIME_CHANGE_OUTSIDE_HORIZON", "Có phiên bị ảnh hưởng ngoài phạm vi 30 ngày. Hãy chỉnh phiên đó trước.", 409);
        // Leave a short margin for validation/persistence; already-started work is protected.
        Require(!state.Sessions.Any(x => x.Status == WorkSessionStatus.Scheduled && x.Start >= Now && x.Start < Now.AddMinutes(1) &&
            !engine.FreeSlots(state, x.Start, x.End).Any(s => s.Start <= x.Start && s.End >= x.End)),
            "LOCKED_SESSION_CONFLICT", "Thay đổi trùng phiên sắp bắt đầu. Hãy xử lý phiên đó trước.", 409);
        var plan = engine.Generate(state, new(Now.AddMinutes(1), 30, affected, true, "AVAILABILITY_CHANGED"), Now);
        var proposal = await StoreAgentPlanAsync(uid, plan, state, "AVAILABILITY_CHANGED", ct);
        plan = plan with { Id = proposal.Id };
        await ApplyPlanCoreAsync(uid, proposal, plan, state, ct);
    }

    private Guid[] TimeConflictTaskIds(PlanningState state)
    {
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        var future = state.Sessions.Where(x => x.Status == WorkSessionStatus.Scheduled && !x.IsLocked && x.Start >= Now).ToArray();
        var affected = future.Where(x => !engine.FreeSlots(state, x.Start, x.End).Any(s => s.Start <= x.Start && s.End >= x.End) ||
            !PlanningEngine.InsideWorkWindow(state.Tasks.Single(t => t.Id == x.TaskId), x.Start, x.End) ||
            (x.End - x.Start).TotalMinutes > state.Preference.MaximumSessionMinutes ||
            (x.End - x.Start).TotalMinutes < Math.Max(state.Preference.MinimumSessionMinutes, state.Tasks.Single(t => t.Id == x.TaskId).MinimumSessionMinutes))
            .Select(x => x.TaskId).ToHashSet();
        foreach (var group in state.Sessions.Where(x => x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed && x.End > Now.AddDays(-1))
            .GroupBy(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.Start, zone).DateTime)))
            if (group.Sum(x => (x.End - x.Start).TotalMinutes) > state.Preference.MaximumDailyMinutes)
                foreach (var session in group.Where(x => x.Status == WorkSessionStatus.Scheduled && !x.IsLocked && x.Start >= Now)) affected.Add(session.TaskId);
        return affected.ToArray();
    }
}
