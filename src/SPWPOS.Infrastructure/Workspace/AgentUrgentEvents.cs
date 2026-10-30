using System.Text.RegularExpressions;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private const string EventUrgencyPattern = @"(?:khẩn(?:\s+cấp)?|khan(?:\s+cap)?|gấp|gap|cấp bách|cap bach|đột xuất|dot xuat|urgent|emergency)";

    private static bool ExplicitEventUrgency(string text)
    {
        // Keep accents here: “gặp” (meet) must never become “gấp” (urgent).
        var reading = VietnameseTelex.Normalize(text).ToLowerInvariant();
        return Regex.IsMatch(reading, @"\b" + EventUrgencyPattern + @"\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) &&
            !Regex.IsMatch(reading, @"\b(?:không|khong|chưa|chua|chẳng|chang|not)\s+(?:phải\s+|phai\s+|quá\s+|qua\s+)?" + EventUrgencyPattern + @"\b",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    private static bool IsUrgentEvent(FixedEvent item) => item.IsUrgent ?? ExplicitEventUrgency(item.Name + " " + item.Description);

    private static bool RequestedUrgentEvent(FixedEvent item, string request)
    {
        // AI may use “khẩn cấp” in the title when the user only said “khẩn”.
        // Match the activity itself; urgency must still come from the user's clause.
        var activity = MatchText(Regex.Replace(VietnameseTelex.Normalize(item.Name).ToLowerInvariant(), @"\b" + EventUrgencyPattern + @"\b", "",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))).Trim();
        return Regex.Split(VietnameseTelex.Normalize(request), @"[,.!?;\r\n]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            .Any(clause => ExplicitEventUrgency(clause) && (MatchText(clause).Contains(MatchText(item.Name), StringComparison.Ordinal) ||
                activity.Length > 0 && MatchText(clause).Contains(activity, StringComparison.Ordinal) ||
                item.EventType == "MEETING" && ChatMatch(clause, @"\b(hop|meeting)\b").Success));
    }

    private static bool CanProposeEventMove(FixedEvent item, string request) =>
        (IsUrgentEvent(item) || item.EventType == "WORK" || item.EventType == "OTHER" &&
            ChatMatch(item.Name, @"\b(mua sam|shopping|di cho|di sieu thi|di dao|don dep|giat do|tap the duc|di ia|di ve sinh|ve sinh ca nhan|an sang|an trua|an toi|di an|an uong|nghi ngoi|giai tri|choi game|doc sach)\b").Success) &&
        !ChatMatch(item.Description, @"\b(co dinh|khong (?:the |duoc )?(?:doi|doi gio|di chuyen)|bat buoc dung gio)\b").Success &&
        !ChatMatch(request, @"\b(khong|dung|cho)\s+(?:tu dong\s+)?(?:doi|di chuyen|thay doi)\b[^.!?;\r\n]{0,50}" +
            Regex.Escape(MatchText(item.Name).Replace("di ", "")) + @"\b").Success;

    private (FixedEvent[] Events, Guid[] UrgentIds) PrepareAgentEvents(PlanningState state, FixedEvent[] incoming, string request, bool timetableContext = false)
    {
        var urgent = incoming.Where(item => RequestedUrgentEvent(item, request)).ToArray();
        foreach (var item in urgent) item.IsUrgent = true;
        bool Overlaps(DateTimeOffset start, DateTimeOffset end) => urgent.Any(item => start < item.End && item.Start < end);
        var classes = timetableContext || IsTimetableRequest(request)
            ? incoming.Where(item => item.EventType == "CLASS").ToArray() : [];
        // Consent can name the work or identify only sessions overlapping the new event.
        // Unrelated work, fixed events and locked/active sessions stay protected.
        var preserved = ExplicitlyPreservedTasks(request, state);
        var editsEventTime = incoming.Any(item => state.Events.Any(original => original.Id == item.Id &&
            (original.Start != item.Start || original.End != item.End))) &&
            ChatMatch(request, @"\b(?:doi|chuyen|sua|cap nhat|move|reschedule|edit)\b").Success && !ForbidsSessionMoves(request);
        var conflictingMoves = RequestsConflictingSessionMove(request) || editsEventTime;
        var authorizedMoves = state.Tasks.Where(task => !preserved.Contains(task.Id) && (conflictingMoves || ChatMatch(request,
            @"\b(?:doi|doi lich|doi gio|di chuyen|xep lai|sap xep lai)\s+(?:(?:rieng|lich|phien|cho|cong viec)\s+)*" +
            Regex.Escape(MatchText(task.Name)) + @"\b").Success)).Select(task => task.Id).ToHashSet();
        bool NeedsReplan(DateTimeOffset start, DateTimeOffset end) => Overlaps(start, end) || classes.Any(item => start < item.End && item.Start < end);
        var existing = state.Events.Where(item => !incoming.Any(change => change.Id == item.Id)).ToArray();
        var movable = existing.Where(item => item.Start >= Now.AddMinutes(5) && Overlaps(item.Start, item.End) && CanProposeEventMove(item, request)).ToArray();
        var released = state.Sessions.Where(session => session.Status == WorkSessionStatus.Scheduled && !session.IsLocked &&
            session.Start >= Now.AddMinutes(5) && (NeedsReplan(session.Start, session.End) || authorizedMoves.Contains(session.TaskId) &&
                incoming.Any(item => session.Start < item.End && item.Start < session.End)) &&
            state.Tasks.Any(task => task.Id == session.TaskId && !task.IsDeleted &&
                task.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled)).Select(session => session.Id).ToHashSet();
        var calendar = state with { Events = existing.Where(item => !movable.Any(move => move.Id == item.Id)).Concat(incoming).ToArray(),
            Sessions = state.Sessions.Where(session => !released.Contains(session.Id)).ToArray() };
        // Work shifts can be proposed conditionally, never silently moved.
        // Other appointments, locked/active work and busy exceptions remain protected.
        // Imported classes keep their actual hours. Overlapping flexible sessions
        // are regenerated below by the planner; class imports cannot move old events.
        ValidateEventConflicts(calendar with { Events = calendar.Events.Where(item => !incoming.Any(change => change.Id == item.Id)).ToArray() }, incoming, null);
        var moved = new List<FixedEvent>();
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        foreach (var item in movable.OrderBy(item => item.Start).ThenBy(item => item.Id))
        {
            var localStart = TimeZoneInfo.ConvertTime(item.Start, zone);
            var day = LocalDate(item.Start, state.Preference);
            var days = IsUrgentEvent(item) ? PlanningEngine.MaximumUrgentDisplacementDays + 1 : 1;
            var blocks = calendar.Events.Concat(moved).Select(block => new TimeSlot(block.Start, block.End))
                .Concat(calendar.Sessions.Where(block => block.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused or WorkSessionStatus.Completed)
                    .Select(block => new TimeSlot(block.Start, block.End)))
                .Concat(state.Exceptions.Where(block => !block.IsAvailable).Select(block => new TimeSlot(
                    PlanningEngine.At(block.Date, block.StartMinute, zone), PlanningEngine.At(block.Date, block.EndMinute, zone))));
            var duration = item.End - item.Start;
            var windows = Enumerable.Range(0, days).Select(offset => new TimeSlot(
                offset == 0 ? item.Start : PlanningEngine.At(day.AddDays(offset), localStart.Hour * 60 + localStart.Minute, zone),
                PlanningEngine.At(day.AddDays(offset + 1), 0, zone)));
            var gap = windows.SelectMany(window => PlanningEngine.Subtract(window, blocks)).FirstOrDefault(slot => slot.End - slot.Start >= duration);
            Require(gap is not null, "URGENT_EVENT_REPLAN_INFEASIBLE",
                $"Để giữ {string.Join(", ", urgent.Where(meeting => item.Start < meeting.End && meeting.Start < item.End).Select(meeting => $"“{meeting.Name}” ({LocalWindow(meeting.Start, meeting.End, state.Preference)})"))}, cần dời “{item.Name}” ({LocalWindow(item.Start, item.End, state.Preference)}). " +
                $"Chưa có khoảng trống {(days == 1 ? "cùng ngày" : "trong ngày này và 2 ngày kế tiếp")} đủ {(int)duration.TotalMinutes} phút để dời toàn bộ sự kiện này. Lịch chưa thay đổi; bạn có thể chọn ngày khác cho “{item.Name}” hoặc đổi giờ cuộc họp.", 409);
            var change = Copy(item); change.Start = gap!.Start; change.End = change.Start + duration;
            ValidateEventConflicts(calendar with { Events = calendar.Events.Concat(moved).ToArray() }, [change], change.Id);
            moved.Add(change);
        }
        return (incoming.Concat(moved).ToArray(), movable.Length > 0 || released.Count > 0 ? urgent.Select(item => item.Id).ToArray() : []);
    }

    private static string EventMoveReason(FixedEvent original) =>
        (IsUrgentEvent(original) ? "Lịch khẩn mới được ưu tiên trước lịch khẩn cũ. Dời lịch khẩn cũ sang giờ trống kế tiếp, giữ nguyên toàn bộ thời lượng. " : "") +
        (original.EventType == "WORK"
        ? "Đây là ca làm. Chỉ xác nhận phương án nếu bạn được phép đổi ca; NORA chưa xác nhận việc đổi giờ với nơi làm việc."
        : IsUrgentEvent(original) ? "" : "Dời sự kiện này vì trùng lịch khẩn; giữ nguyên ngày và toàn bộ thời lượng.");

    private static string UrgentReplanReply(AgentChangeSet changes, PlanningState state, AgentChangeView[] summary)
    {
        if (changes.UrgentEventIds is not { Length: > 0 } ids)
            return "Việc khẩn cần dời một số phiên đã lên lịch. Xem giờ cũ → giờ mới bên dưới rồi bấm Xác nhận dời lịch; chưa lưu thay đổi và không đổi hạn chót.";
        var requested = changes.Events.Where(item => ids.Contains(item.Id)).Select(item =>
            $"“{item.Name}” ({LocalWindow(item.Start, item.End, state.Preference)})");
        var moves = summary.Where(change => change.Before.Length > 0 && change.Before != change.After && change.Kind is "EVENT" or "SESSION").Select(change =>
            $"- Dời **{change.Name}**: giờ cũ {change.Before}; giờ mới đề xuất {change.After}." +
            (change.Reason.Length > 0 ? " " + change.Reason : " Giữ nguyên thời lượng và deadline."));
        return $"Đề xuất giữ đúng giờ lịch khẩn {string.Join(", ", requested)}. Để tránh trùng giờ, cần thay đổi các mục sau:\n\n" +
            string.Join("\n", moves) + "\n\nChưa thay đổi lịch. Kiểm tra từng giờ cũ → giờ mới rồi bấm Xác nhận dời lịch nếu đồng ý. Nếu không thể dời mục được nêu, hãy giữ lịch cũ và chọn giờ khác cho cuộc họp.";
    }
}
