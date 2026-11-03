using System.Globalization;
using System.Text.RegularExpressions;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private const string SessionMovePattern = @"\b(?:doi|dich|chuyen|di chuyen|lui|xep lai|sap xep lai|move|reschedule)\s+(?:(?:lai|giup|ho|cho|minh|toi|rieng|cac|nhung|mot|cai)\s+)*(?:phien(?:\s+lam viec)?|buoi lam viec|lich lam viec|work session)\b";
    private static bool HasSessionMoveRequest(string request) => ChatMatch(request, SessionMovePattern).Success && !ForbidsSessionMoves(request);

    private static int? SessionMovePurposeMinute(string request)
    {
        var text = MatchText(request); var move = ChatMatch(text, SessionMovePattern);
        if (!move.Success || !HasSessionMoveRequest(request)) return null;
        var purpose = ChatMatch(text[(move.Index + move.Length)..], @"\b(?:de|nhuong gio cho)\s+[^.!?;\r\n]{0,160}?\b(?:luc|vao)\s+(?<hour>[01]?\d|2[0-3]):(?<minute>[0-5]\d)\b");
        return purpose.Success ? int.Parse(purpose.Groups["hour"].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(purpose.Groups["minute"].Value, CultureInfo.InvariantCulture) : null;
    }

    // A reference to a saved session's CLOCK RANGE identifies work, not a new
    // deadline or a request to move the appointment mentioned in the purpose clause.
    private HashSet<Guid> ClockReferencedSessionIds(string request, PlanningState state, IEnumerable<AgentEventCommand>? incoming = null)
    {
        var selected = new HashSet<Guid>();
        if (!HasSessionMoveRequest(request)) return selected;
        var text = MatchText(request); var preserved = ExplicitlyPreservedTasks(request, state);
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        foreach (Match move in RequestMatches(text, SessionMovePattern))
        {
            var target = Regex.Split(text[(move.Index + move.Length)..], @"\b(?:de|vi|nhuong|tranh|giu)\b|[.!?;\r\n]",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))[0];
            var range = ChatMatch(target, @"\b(?<h1>[01]?\d|2[0-3])\s*[:h]\s*(?<m1>[0-5]\d)\s*(?:[-–—]|den|to)\s*(?<h2>[01]?\d|2[0-3])\s*[:h]\s*(?<m2>[0-5]\d)\b");
            if (!range.Success) continue;
            int Minute(string h, string m) => int.Parse(range.Groups[h].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(range.Groups[m].Value, CultureInfo.InvariantCulture);
            var start = Minute("h1", "m1"); var end = Minute("h2", "m2");
            var dates = RequestMatches(target, @"\b\d{1,2}/\d{1,2}/\d{4}\b|\b\d{4}-\d{2}-\d{2}\b")
                .Select(m => DateOnly.TryParseExact(m.Value, ["d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy", "yyyy-MM-dd"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? (DateOnly?)date : null).Where(d => d != null).ToArray();
            if (dates.Length == 0 && ChatMatch(target, @"\b(?:hom nay|today|ngay mai|tomorrow)\b") is { Success: true } relative)
                dates = [LocalDate(Now, state.Preference).AddDays(relative.Value is "ngay mai" or "tomorrow" ? 1 : 0)];
            var matches = state.Sessions.Where(s => s.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused &&
                !preserved.Contains(s.TaskId) && TimeZoneInfo.ConvertTime(s.Start, zone).TimeOfDay.TotalMinutes == start &&
                TimeZoneInfo.ConvertTime(s.End, zone).TimeOfDay.TotalMinutes == end && (dates.Length == 0 || dates.Contains(LocalDate(s.Start, state.Preference)))).ToArray();
            // A supplied event can disambiguate the day only if its exact time
            // overlaps the selected session; never choose the first of repeated days.
            if (matches.Length > 1 && dates.Length == 0 && incoming?.Any() == true)
                matches = matches.Where(s => incoming.Any(e => s.Start < e.End && e.Start < s.End)).ToArray();
            Require(matches.Length <= 1, "SESSION_REFERENCE_AMBIGUOUS", "Có nhiều phiên cùng khung giờ. Hãy nói thêm ngày của phiên cần dời.");
            foreach (var session in matches) selected.Add(session.Id);
        }
        return selected;
    }
    private HashSet<Guid> ClockReferencedSessionTasks(string request, PlanningState state, IEnumerable<AgentEventCommand>? incoming = null) {
        var ids=ClockReferencedSessionIds(request,state,incoming);
        return state.Sessions.Where(s=>ids.Contains(s.Id)).Select(s=>s.TaskId).ToHashSet();
    }
    private PlanningState ProtectUnselectedSessionReferences(string request, PlanningState state, AgentEventCommand[] incoming) {
        var selected=ClockReferencedSessionIds(request,state,incoming);
        if(selected.Count==0)return state;
        var taskIds=state.Sessions.Where(s=>selected.Contains(s.Id)).Select(s=>s.TaskId).ToHashSet();
        // Planning-only copies retain the other sessions of the SAME task.
        // No lock setting is persisted or sent as a user-requested edit.
        return state with {Sessions=state.Sessions.Select(s=>{
            if(s.Status!=WorkSessionStatus.Scheduled || !taskIds.Contains(s.TaskId) || selected.Contains(s.Id))return s;
            var retained=Copy(s);retained.IsLocked=true;return retained;
        }).ToArray()};
    }
}
