using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private async Task<PlanningState> AgentCalendarStateAsync(Guid uid, PlanningState state, CancellationToken ct)
    {
        var start = PlanningEngine.At(LocalDate(Now, state.Preference), 0, PlanningEngine.Zone(state.Preference.TimeZoneId));
        return await CalendarStateAsync(uid, state, start, Now.AddDays(30), ct);
    }

    private async Task<PlanningState> CalendarStateAsync(Guid uid, PlanningState state, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        // State's history cap is useful for planning, but must not hide an
        // occupied interval when answering a calendar query.
        var sessions = await Mine<WorkSession>(uid).AsNoTracking()
            .Where(x => x.Start < end && x.End > start &&
                (x.Status == WorkSessionStatus.Scheduled || x.Status == WorkSessionStatus.Running ||
                 x.Status == WorkSessionStatus.Paused || x.Status == WorkSessionStatus.Completed))
            .ToArrayAsync(ct);
        return state with { Sessions = sessions };
    }

    private TimeSlot[] AgentFreeTimeSlots(PlanningState state, DateTimeOffset start, DateTimeOffset end)
    {
        var occupied = state.Sessions.Where(x => x.Status is WorkSessionStatus.Scheduled or
            WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed)
            .Where(x => x.Start < end && x.End > start).Select(x => new TimeSlot(x.Start, x.End));
        // Reuse the planner's dated overrides, interval merging and subtraction.
        // Do not treat a task without a saved session as a calendar reservation.
        return engine.FreeSlots(state, start, end, occupied);
    }

    private string VerifiedFreeTimeReply(AgentFreeTimeQuery query, PlanningState state)
    {
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        var todayStart = PlanningEngine.At(LocalDate(Now, state.Preference), 0, zone);
        if (query.Start < todayStart || query.End > Now.AddDays(30))
            return "Mình chỉ có thể xác nhận giờ rảnh từ hiện tại đến 30 ngày tới. Bạn chọn một khoảng trong phạm vi này nhé. Lịch chưa thay đổi.";
        var start = query.Start < Now ? Now : query.Start;
        var slots = query.End > start ? AgentFreeTimeSlots(state, start, query.End) : [];
        var localStart = TimeZoneInfo.ConvertTime(query.Start, zone);
        var localEnd = TimeZoneInfo.ConvertTime(query.End, zone);
        // End is exclusive: midnight belongs to the day just inspected.
        var lastDate = DateOnly.FromDateTime(localEnd.AddTicks(-1).DateTime);
        var firstDate = DateOnly.FromDateTime(localStart.DateTime);
        var range = firstDate == lastDate ? firstDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : $"{firstDate:dd/MM/yyyy}–{lastDate:dd/MM/yyyy}";
        if (slots.Length == 0)
            return $"Trong khoảng bạn hỏi ({range}), không còn khoảng rảnh khả dụng theo giờ rảnh đã cài đặt và lịch đã lưu. Lịch chưa thay đổi.";

        var days = slots.GroupBy(x => LocalDate(x.Start, state.Preference)).Select(day =>
            $"{day.Key:dd/MM/yyyy}: " + string.Join(", ", day.Select(slot =>
            {
                var from = TimeZoneInfo.ConvertTime(slot.Start, zone);
                var to = TimeZoneInfo.ConvertTime(slot.End, zone);
                var endText = to.TimeOfDay == TimeSpan.Zero && to.Date > from.Date ? "24:00" : to.ToString("HH:mm", CultureInfo.InvariantCulture);
                return $"{from:HH:mm}–{endText} ({(int)(slot.End - slot.Start).TotalMinutes} phút)";
            })));
        return "Các khoảng bạn còn rảnh theo giờ rảnh đã cài đặt và lịch đã lưu:\n" + string.Join("\n", days) +
            $"\nTổng cộng {(int)slots.Sum(x => (x.End - x.Start).TotalMinutes)} phút. Lịch chưa thay đổi.";
    }
}
