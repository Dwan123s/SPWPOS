using System.Globalization;
using System.Text;
using System.Text.Json;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static string MatchText(string value) => string.Concat(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant().Replace('đ','d');
    private (string Json, string[] Notices) BuildAgentContext(PlanningState state, string request, string[] activeTaskTypes, AgentPendingRequest? pending)
    {
        var search = MatchText(request);
        var active = state.Tasks.Where(x => x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled).ToArray();
        var matches = state.Tasks.Where(x => MatchText(x.Name.Trim()).Length >= 2 && search.Contains(MatchText(x.Name.Trim()), StringComparison.Ordinal) || request.Contains(x.Id.ToString(), StringComparison.OrdinalIgnoreCase)).ToArray();
        var tasks = matches.Concat(active.OrderBy(x=>x.Deadline).ThenByDescending(x=>x.Priority)).DistinctBy(x=>x.Id).Take(100).ToArray();
        var events = state.Events.Where(x=>x.End > Now && x.Start < Now.AddDays(30)).OrderBy(x=>x.Start).ToArray();
        var sessions = state.Sessions.Where(x=>x.End > Now && x.Start < Now.AddDays(30) && x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused).OrderBy(x=>x.Start).ToArray();
        var notices = new List<string>();
        if (active.Length > tasks.Count(x=>active.Any(t=>t.Id==x.Id))) notices.Add($"NORA đang đọc {tasks.Count(x=>active.Any(t=>t.Id==x.Id))}/{active.Length} công việc đang hoạt động, ưu tiên tên khớp yêu cầu và deadline. Hãy nêu tên cụ thể để xem phần khác.");
        if (events.Length > 100 || sessions.Length > 100) notices.Add("NORA chỉ đọc 100 sự kiện và 100 phiên gần nhất trong 30 ngày tới. Câu trả lời chỉ áp dụng cho phạm vi này.");
        var json = JsonSerializer.Serialize(new {
            activeTaskTypes, pendingRequest = pending,
            savedFeasibility = tasks.Select(task => new { task.Id, task.Name,
                requiredMinutes = engine.Remaining(task, [new BiasView("All", 0, 1, false)]),
                scheduledBeforeDeadlineMinutes = sessions.Where(s => s.TaskId == task.Id && s.End <= task.Deadline && PlanningEngine.InsideWorkWindow(task, s.Start, s.End)).Sum(s => (s.End - s.Start).TotalMinutes),
                requiresConflictAndDependencyValidation = true }),
            scope = new { rangeStart=Now,rangeEnd=Now.AddDays(30), timeZoneId=state.Preference.TimeZoneId,
                tasks=new { totalActive=active.Length,included=tasks.Length,truncated=active.Any(x=>!tasks.Any(t=>t.Id==x.Id)),matchingRequestIds=matches.Select(x=>x.Id) },
                events=new { total=events.Length,included=Math.Min(100,events.Length),truncated=events.Length>100 },
                sessions=new { total=sessions.Length,included=Math.Min(100,sessions.Length),truncated=sessions.Length>100 },
                history=new { maximumTurns=8, truncatedPossible=true } },
            tasks=tasks.Select(x=>new {x.Id,x.Name,x.Deadline,x.WorkWindowStart,x.WorkWindowEnd,x.EstimatedMinutes,x.Progress,x.Splittable,x.MinimumSessionMinutes,x.Priority,x.TaskType,status=x.Status.ToString()}),
            availability=state.Availability.Select(x=>new AvailabilityInput(x.DayOfWeek,x.StartMinute,x.EndMinute)),
            exceptions=state.Exceptions.Where(x=>x.Date>=LocalDate(Now,state.Preference) && x.Date<=LocalDate(Now.AddDays(30),state.Preference)).Select(x=>new {x.Id,x.Date,x.StartMinute,x.EndMinute,x.IsAvailable,x.Note,x.OverridesWeeklyAvailability}),
            events=events.Take(100).Select(x=>new{x.Id,x.Name,x.Start,x.End,x.EventType,x.Description}),
            sessions=sessions.Take(100).Select(x=>new{x.TaskId,x.Start,x.End,x.IsLocked,status=x.Status.ToString()}),
            dependencies=state.Dependencies.Select(x=>new{x.TaskId,x.PredecessorId}),
            preference=new {state.Preference.MaximumDailyMinutes,state.Preference.MaximumSessionMinutes,state.Preference.MinimumSessionMinutes,state.Preference.BreakMinutes,state.Preference.PlanningHorizonDays}
        },AgentJson);
        return (json,notices.ToArray());
    }
}
