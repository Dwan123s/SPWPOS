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

    private string VerifiedFreeTimeReply(AgentFreeTimeQuery query, PlanningState state, bool partOfOverview = false)
    {
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        var todayStart = PlanningEngine.At(LocalDate(Now, state.Preference), 0, zone);
        if (query.Start < todayStart || query.End > Now.AddDays(30))
            return "Mình xem được giờ rảnh từ bây giờ đến 30 ngày tới. Bạn chọn một ngày trong khoảng này để mình kiểm tra nhé.";
        var start = query.Start < Now ? Now : query.Start;
        var slots = query.End > start ? AgentFreeTimeSlots(state, start, query.End) : [];
        var localStart = TimeZoneInfo.ConvertTime(query.Start, zone);
        var localEnd = TimeZoneInfo.ConvertTime(query.End, zone);
        // End is exclusive: midnight belongs to the day just inspected.
        var lastDate = DateOnly.FromDateTime(localEnd.AddTicks(-1).DateTime);
        var firstDate = DateOnly.FromDateTime(localStart.DateTime);
        var range = partOfOverview ? "Trong khoảng này" : CalendarQueryLabel(query, state.Preference);
        if (slots.Length == 0)
        {
            var hasAvailability = state.Policy != null || state.Availability.Length > 0 || state.Exceptions.Any(x => x.IsAvailable && x.Date >= firstDate && x.Date <= lastDate);
            return hasAvailability
                ? $"{range}, mình không tìm thấy khoảng rảnh nào trong giờ bạn đã đặt. Bạn có thể cho mình một ngày khác để tìm tiếp nhé."
                : $"{range}, mình chưa có giờ làm việc được bạn xác nhận. Bạn cho mình biết ngày và khung giờ có thể làm việc, cùng giờ sinh hoạt/ngủ/nghỉ cần chừa; mình sẽ cấu hình quanh thời gian biểu cố định của bạn.";
        }

        string Window(TimeSlot slot)
        {
            var from = TimeZoneInfo.ConvertTime(slot.Start, zone);
            var to = TimeZoneInfo.ConvertTime(slot.End, zone);
            var endText = to.TimeOfDay == TimeSpan.Zero && to.Date > from.Date ? "24:00" : to.ToString("HH:mm", CultureInfo.InvariantCulture);
            return $"{from:HH:mm}–{endText} ({(int)(slot.End - slot.Start).TotalMinutes} phút)";
        }
        var days = slots.GroupBy(x => LocalDate(x.Start, state.Preference)).Select(day =>
            $"{day.Key:dd'/'MM'/'yyyy}: " + string.Join(", ", day.Select(Window)));
        var total = (int)slots.Sum(x => (x.End - x.Start).TotalMinutes);
        var opening = $"{range}, bạn còn {slots.Length} khoảng rảnh trong giờ đã đặt:";
        var next = slots.All(x => (x.End - x.Start).TotalMinutes < state.Preference.MinimumSessionMinutes)
            ? "Các khoảng này khá ngắn; bạn có thể để nghỉ hoặc chuẩn bị cho việc tiếp theo."
            : slots.Length == 1
                ? "Nếu muốn dành khoảng này cho một việc cụ thể, bạn cho mình biết việc gì và cần bao lâu nhé."
                : "Nếu muốn xếp thêm việc, bạn nói tên việc và thời lượng nhé; mình sẽ xem khoảng nào phù hợp.";
        if (slots.Length == 1)
        {
            var day = LocalDate(slots[0].Start, state.Preference);
            var when = firstDate == lastDate ? "" : $" ngày {day:dd'/'MM'/'yyyy}";
            var lead = partOfOverview ? "Bạn còn rảnh từ " : $"{range}, bạn còn rảnh từ ";
            return lead + Window(slots[0]) + when + ". " + next;
        }
        if (partOfOverview) opening = $"Bạn còn {slots.Length} khoảng rảnh trong giờ đã đặt:";
        return opening + "\n" + string.Join("\n", days) + $"\n\nTổng cộng {total} phút. " + next;
    }

    private string CalendarQueryLabel(AgentFreeTimeQuery query, WorkPreference preference)
    {
        var first = LocalDate(query.Start, preference);
        var last = LocalDate(query.End.AddTicks(-1), preference);
        if (first != last) return $"Từ {first:dd'/'MM'/'yyyy} đến {last:dd'/'MM'/'yyyy}";
        var today = LocalDate(Now, preference);
        return first == today ? $"Hôm nay ({first:dd'/'MM'/'yyyy})"
            : first == today.AddDays(1) ? $"Ngày mai ({first:dd'/'MM'/'yyyy})" : $"Ngày {first:dd'/'MM'/'yyyy}";
    }
}
