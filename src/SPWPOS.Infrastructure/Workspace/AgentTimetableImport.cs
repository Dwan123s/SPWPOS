using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static bool IsTimetableRequest(string text) => ChatMatch(text,
        @"\b(?:thoi khoa bieu|thoi gian bieu|lich hoc|timetable)\b").Success;
    private static bool TimetableFollowup(string text) => IsTimetableRequest(text) || ChatMatch(text,
        @"\b(?:\d+\s*tuan|tuan (?:toi|sau|nay)|tiep tuc xu ly)\b").Success;

    private static bool SimpleTimetableUpload(string text) => ChatMatch(text.Trim(),
        @"^nhap thoi khoa bieu(?: trong anh)?(?:, tu phan tich chi tiet lich ban va cac khoang ranh tung ngay| va cap nhat gio lam viec)?[.!]?$").Success
        || ChatMatch(text.Trim(), @"^toi muon gui anh thoi khoa bieu\. hay doc gio hoc va luu cac buoi hoc thanh lich co dinh sau khi du thong tin\. sau do, thiet lap gio lam viec theo thoi khoa bieu cho cac tuan da nhap, dua vao lich hoc, ca lam va lich ban da luu\.$").Success;

    private static bool SimpleTimetableFollowup(string text) => TimetableFollowup(text) || ChatMatch(text.Trim(),
        @"^(?:(?:bat dau )?(?:tu )?(?:ngay )?(?:\d{1,2}[/-]\d{1,2}(?:[/-]\d{4})?|\d{4}-\d{2}-\d{2})|ngay mai|hom nay|ngay kia)[.!]?$").Success;

    private static string TimetableImportReply(AgentChangeSet changes, bool applied)
    {
        var count = changes.Events.Count(x => x.EventType is "CLASS" or "WORK");
        var result = applied ? $"Đã lưu {count} buổi từ thời khóa biểu." : $"Đã đọc {count} buổi từ thời khóa biểu.";
        if (count == 0) result = "Các buổi trong ảnh đã có trong lịch.";
        return result + (applied
            ? " Xem các khung đã cập nhật ở Lịch → Giờ làm việc → Theo từng tuần."
            : " Lưu thay đổi để cập nhật Lịch và Giờ làm việc.");
    }

    // Use the same calendar arithmetic as the planner, after proposed session
    // movements. The model must not invent gaps or subtract busy time twice.
    private string TimetableAnalysis(PlanningState state, FixedEvent[] classes, PlanView? plan, bool applied)
    {
        if (classes.Length == 0) classes = state.Events.Where(x => x.End > Now && x.Start <= Now.AddDays(30)).ToArray();
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        var first = RoutineWeekStart(LocalDate(classes.Min(x => x.Start), state.Preference));
        var start = PlanningEngine.At(first, 0, zone);
        var end = PlanningEngine.At(first.AddDays(7), 0, zone);
        if (plan is not null)
        {
            var retained = state.Sessions.Where(x => x.Status != WorkSessionStatus.Scheduled || x.Start < plan.Start || x.Start >= plan.End);
            state = state with { Sessions = retained.Concat(plan.Sessions.Select(x => new WorkSession {
                Id = x.Id, TaskId = x.TaskId, Start = x.Start, End = x.End, Status = WorkSessionStatus.Scheduled
            })).ToArray() };
        }
        var remainingStart = start > Now ? start : Now;
        var hasWorkHours = state.Policy != null || state.Availability.Length > 0 || state.Exceptions.Any(x => x.IsAvailable &&
            x.Date >= first && x.Date <= LocalDate(end.AddTicks(-1), state.Preference));
        var proposedPolicy = !hasWorkHours ? ProposeTimetableRoutine(state, first) : null;
        var usesTimetableHours = state.Policy?.ForDate(first)?.UseTimetableHours == true;
        var slots = remainingStart < end ? AgentFreeTimeSlots(proposedPolicy is null ? state : state with { Policy = proposedPolicy }, remainingStart, end) : [];
        string Hours(DateTimeOffset from, DateTimeOffset to) =>
            $"{TimeZoneInfo.ConvertTime(from, zone):HH:mm}–" +
            (TimeZoneInfo.ConvertTime(to, zone).TimeOfDay == TimeSpan.Zero ? "24:00" : $"{TimeZoneInfo.ConvertTime(to, zone):HH:mm}");
        var lines = new List<string> {
            applied ? "Đã lưu thời khóa biểu làm lịch cố định để sắp xếp công việc quanh các giờ bận. Bạn có thể hoàn tác yêu cầu này."
                : "Đã phân tích thời khóa biểu. Đây là lịch sau khi áp dụng đề xuất; chưa lưu thay đổi.",
            usesTimetableHours ? "Giờ làm việc theo thời khóa biểu đã được tính quanh lịch học, các sự kiện và giờ sinh hoạt."
                : hasWorkHours ? "Dùng giờ làm việc bạn đã cung cấp, trừ lịch học, sự kiện, phiên làm việc và các khoảng bận."
                : "Mình đã tự chuẩn bị lịch sinh hoạt và giờ rảnh đề xuất: trừ lịch cố định, giờ ngủ, bữa trưa/tối 30 phút, nghỉ ngơi và di chuyển. Đây là đề xuất để bạn xem và áp dụng, chưa lưu thành giờ làm việc.",
            $"Phân tích từ {first:dd/MM/yyyy} đến {LocalDate(end.AddTicks(-1), state.Preference):dd/MM/yyyy} ({state.Preference.TimeZoneId}); chỉ tính thời gian còn lại:" };
        for (var day = first; PlanningEngine.At(day, 0, zone) < end; day = day.AddDays(1))
        {
            var dayStart = PlanningEngine.At(day, 0, zone); var dayEnd = PlanningEngine.At(day.AddDays(1), 0, zone);
            var busy = state.Events.Where(x => x.Start < dayEnd && x.End > dayStart && x.End > Now)
                .Select(x => new { x.Start, Text = $"{x.Name} {Hours(x.Start, x.End)}" })
                .Concat(state.Sessions.Where(x => x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed)
                    .Where(x => x.Start < dayEnd && x.End > dayStart && x.End > Now)
                    .Select(x => new { x.Start, Text = $"{state.Tasks.FirstOrDefault(t => t.Id == x.TaskId)?.Name ?? "Phiên làm việc"} {Hours(x.Start, x.End)}" }))
                .Concat(state.Exceptions.Where(x => !x.IsAvailable && x.Date == day)
                    .Select(x => new { Start = PlanningEngine.At(day, x.StartMinute, zone), Text =
                        $"{(string.IsNullOrWhiteSpace(x.Note) ? "Giờ bận" : x.Note)} {Hours(PlanningEngine.At(day, x.StartMinute, zone), PlanningEngine.At(day, x.EndMinute, zone))}" }))
                .OrderBy(x => x.Start).Select(x => x.Text).ToArray();
            var free = slots.Where(x => LocalDate(x.Start, state.Preference) == day).ToArray();
            var dayHasWorkHours = proposedPolicy != null || state.Policy != null || state.Availability.Any(x => x.DayOfWeek == (int)day.DayOfWeek) ||
                state.Exceptions.Any(x => x.IsAvailable && x.Date == day);
            var inspectStart = dayStart > Now ? dayStart : Now;
            var inspectEnd = dayEnd < end ? dayEnd : end;
            var unoccupied = !dayHasWorkHours && inspectStart < inspectEnd
                ? PlanningEngine.Subtract(new TimeSlot(inspectStart, inspectEnd), state.Events.Select(x => new TimeSlot(x.Start, x.End))
                    .Concat(state.Sessions.Where(x => x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed)
                        .Select(x => new TimeSlot(x.Start, x.End)))
                    .Concat(state.Exceptions.Where(x => !x.IsAvailable && x.Date == day).Select(x => new TimeSlot(
                        PlanningEngine.At(day, x.StartMinute, zone), PlanningEngine.At(day, x.EndMinute, zone))))).ToArray() : [];
            lines.Add($"- {day:dd/MM}: " + (busy.Length > 0 ? "Bận: " + string.Join("; ", busy) + ". " : "Không có lịch bận đã lưu. ") +
                (free.Length == 0 ? dayHasWorkHours ? "Không có khoảng rảnh trong khung đang xét." : "Giờ rảnh: chưa xác nhận." : (proposedPolicy != null ? "Giờ rảnh đề xuất: " : "Rảnh: ") + string.Join(", ", free.Select(x =>
                    $"{Hours(x.Start, x.End)} ({(int)(x.End - x.Start).TotalMinutes} phút)")) + $". Tổng {free.Sum(x => (int)(x.End - x.Start).TotalMinutes)} phút.") +
                (unoccupied.Length > 0 ? " Chưa có lịch bận: " + string.Join(", ", unoccupied.Select(x => Hours(x.Start, x.End))) + " (chưa xác nhận có thể làm việc)." : ""));
        }
        if (hasWorkHours) lines.Add($"Tổng thời gian rảnh đã xác nhận: {slots.Sum(x => (int)(x.End - x.Start).TotalMinutes)} phút.");
        if (slots.Length > 0)
        {
            var longest = slots.MaxBy(x => x.End - x.Start)!;
            lines.Add($"Khoảng dài nhất: {LocalDate(longest.Start, state.Preference):dd/MM} {Hours(longest.Start, longest.End)} ({(int)(longest.End - longest.Start).TotalMinutes} phút).");
        }
        lines.Add($"Thời gian trống không đồng nghĩa làm việc liên tục: tối đa {state.Preference.MaximumDailyMinutes} phút/ngày, mỗi phiên {state.Preference.MinimumSessionMinutes}–{state.Preference.MaximumSessionMinutes} phút, nghỉ {state.Preference.BreakMinutes} phút giữa các phiên.");
        lines.Add(usesTimetableHours
            ? "Mở Lịch → Giờ làm việc để xem các khung theo từng tuần đã nhập."
            : $"Lịch sinh hoạt và các khoảng rảnh được đề xuất tự động ở thẻ bên dưới cho đúng tuần {first:dd/MM}–{first.AddDays(6):dd/MM} của thời khóa biểu vừa gửi. Bạn chỉ cần xem và bấm áp dụng; có thể chỉnh giờ ngủ, bữa ăn hoặc thêm hoạt động riêng sau đó, không cần tự khai báo các khung giờ rảnh.");
        return string.Join("\n", lines);
    }

    private static string[] TimetableCutoffNotices(int skipped, int clipped)
    {
        if (skipped == 0 && clipped == 0) return [];
        var parts = new List<string>();
        if (skipped > 0) parts.Add($"Mình bỏ qua {skipped} buổi học đã kết thúc");
        if (clipped > 0) parts.Add($"{(parts.Count > 0 ? "với" : "Với")} {clipped} buổi đang diễn ra, chỉ giữ phần thời gian còn lại");
        return [string.Join("; ", parts) + ". Các buổi sau vẫn theo ngày và giờ của thời khóa biểu."];
    }
}
