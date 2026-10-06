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
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Start < input.End && (input.End - input.Start).TotalDays <= 31, "VALIDATION_ERROR", "Sự kiện phải có tên và thời gian hợp lệ, tối đa 31 ngày.");
        Require(!await Mine<FixedEvent>(uid).AnyAsync(x => x.Id != id && x.Start < input.End && input.Start < x.End, ct), "FIXED_EVENT_OVERLAP", "Sự kiện trùng với một sự kiện khác.", 409);
        Require(!await Mine<WorkSession>(uid).AnyAsync(x => x.Start < input.End && input.Start < x.End &&
            (x.Status == WorkSessionStatus.Running || x.Status == WorkSessionStatus.Paused || x.Status == WorkSessionStatus.Scheduled && x.IsLocked), ct),
            "LOCKED_SESSION_CONFLICT", "Sự kiện trùng với phiên đã khóa hoặc đang chạy.", 409);
        var item = id is { } eventId ? await Find<FixedEvent>(uid, eventId, ct) : new FixedEvent { UserId = uid };
        Require(input.EventType is "MEETING" or "CLASS" or "APPOINTMENT" or "OTHER" && input.Description.Length <= 1000, "VALIDATION_ERROR", "Loại hoặc mô tả sự kiện không hợp lệ.");
        item.Name = input.Name.Trim(); item.Start = input.Start; item.End = input.End; item.Description = input.Description; item.EventType = input.EventType;
        if (id is null) db.FixedEvents.Add(item); return item.Id;
    }, ct);
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
        preference.PlanningHorizonDays = input.PlanningHorizonDays; preference.PreferredPeriod = input.PreferredPeriod;
        var profile = await EnsureProfileAsync(uid, ct); profile.TimeZoneId = input.TimeZoneId; profile.UpdatedAt = Now;
        ValidateProtectedSessions(await State(uid, ct));
    }, ct);
    private void ValidateProtectedSessions(PlanningState state)
    {
        foreach (var session in state.Sessions.Where(x => x.Start >= Now && x.Status == WorkSessionStatus.Scheduled && x.IsLocked))
        {
            var free = engine.FreeSlots(state, session.Start, session.End);
            Require(free.Any(x => x.Start <= session.Start && x.End >= session.End), "LOCKED_SESSION_CONFLICT", "Thay đổi thời gian rảnh xung đột với phiên đã khóa.", 409);
            var task = state.Tasks.Single(x => x.Id == session.TaskId);
            Require((session.End - session.Start).TotalMinutes >= Math.Max(task.MinimumSessionMinutes, state.Preference.MinimumSessionMinutes) &&
                (session.End - session.Start).TotalMinutes <= state.Preference.MaximumSessionMinutes, "LOCKED_SESSION_CONFLICT", "Giới hạn phiên mới xung đột với phiên đã khóa.", 409);
        }
    }
}
