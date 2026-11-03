using System.Text.Json;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static string MatchText(string value) => AgentInputRules.Normalize(value);
    private (string Json, string[] Notices) BuildAgentContext(PlanningState state, string request, string[] activeTaskTypes, AgentPendingRequest? pending)
    {
        var search = MatchText(request);
        DateTimeOffset Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, PlanningEngine.Zone(state.Preference.TimeZoneId));
        DateTimeOffset? LocalOrNull(DateTimeOffset? at) => at is { } value ? Local(value) : null;
        var today = LocalDate(Now, state.Preference);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var active = state.Tasks.Where(x => x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled).ToArray();
        var matches = state.Tasks.Where(x => MatchText(x.Name.Trim()).Length >= 2 && search.Contains(MatchText(x.Name.Trim()), StringComparison.Ordinal) || request.Contains(x.Id.ToString(), StringComparison.OrdinalIgnoreCase)).ToArray();
        var tasks = matches.Concat(active.OrderBy(x=>x.Deadline).ThenByDescending(x=>x.Priority)).DistinctBy(x=>x.Id).Take(100).ToArray();
        var events = state.Events.Where(x=>x.End > Now && x.Start < Now.AddDays(30)).OrderBy(x=>x.Start).ToArray();
        var sessions = state.Sessions.Where(x=>x.End > Now && x.Start < Now.AddDays(30) && x.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused).OrderBy(x=>x.Start).ToArray();
        var notices = new List<string>();
        if (active.Length > tasks.Count(x=>active.Any(t=>t.Id==x.Id))) notices.Add($"NORA đang đọc {tasks.Count(x=>active.Any(t=>t.Id==x.Id))}/{active.Length} công việc đang hoạt động, ưu tiên tên khớp yêu cầu và deadline. Hãy nêu tên cụ thể để xem phần khác.");
        if (events.Length > 100 || sessions.Length > 100) notices.Add("Danh sách chi tiết gửi NORA gồm tối đa 100 sự kiện và 100 phiên trong 30 ngày tới. Các khoảng giờ rảnh vẫn được tính từ toàn bộ lịch đã lưu trong phạm vi này.");
        if (notices.Count > 0) notices.Add($"Phạm vi lịch đang đọc: {Local(Now):dd'/'MM'/'yyyy HH:mm} đến {Local(Now.AddDays(30)):dd'/'MM'/'yyyy HH:mm} ({state.Preference.TimeZoneId}).");
        var json = JsonSerializer.Serialize(new {
            activeTaskTypes, pendingRequest = pending is null ? null : pending with {
                WorkWindowStart = LocalOrNull(pending.WorkWindowStart), WorkWindowEnd = LocalOrNull(pending.WorkWindowEnd), Deadline = LocalOrNull(pending.Deadline) },
            clock = new { currentLocal = Local(Now), today, tomorrow = today.AddDays(1), dayAfterTomorrow = today.AddDays(2),
                currentWeekStart = weekStart, nextWeekStart = weekStart.AddDays(7) },
            capabilities = new { taskWorkWindows = true, preferenceChanges = false },
            timetableAutomation = new { hasFixedSchedule = state.Events.Length > 0,
                hasConfirmedWorkHours = state.Policy != null || state.Availability.Length > 0 || state.Exceptions.Any(x => x.IsAvailable),
                policy = "Start from the user's uploaded fixed timetable. Import only supplied commitments and return a verified seven-day analysis. Never create default hours or assume empty cells are work availability. Save a complete timetable even when work hours are missing; then ask conversationally for the user's actual work/sleep/rest windows. Configure only explicitly supplied hours. Urgent tasks use confirmed gaps while protecting fixed commitments." },
            verifiedFreeTime = new { rangeStart = Local(Now), rangeEnd = Local(Now.AddDays(30)),
                timeZoneId = state.Preference.TimeZoneId, includesAllSavedCalendarItems = true,
                intervals = AgentFreeTimeSlots(state, Now, Now.AddDays(30)).Select(x => new {
                    date = LocalDate(x.Start, state.Preference), start = Local(x.Start), end = Local(x.End),
                    minutes = (int)(x.End - x.Start).TotalMinutes }) },
            currentCalendarCapacity = new { reservationsMayChangeWhenRescheduling = true, usesCapacityFactor = false,
                rangeEnd = Local(Now.AddDays(30)), perDate = engine.CapacityDays(state, Now.AddMinutes(5), Now.AddDays(30), Now, false)
                    .Select(x => new { x.Date, x.DailyBudgetMinutes, x.SchedulableMinutes }) },
            savedFeasibility = tasks.Select(task => new { task.Id, task.Name,
                requiredMinutes = engine.Remaining(task, [new BiasView("All", 0, 1, false)]),
                scheduledBeforeDeadlineMinutes = sessions.Where(s => s.TaskId == task.Id && s.End <= task.Deadline && PlanningEngine.InsideWorkWindow(task, s.Start, s.End)).Sum(s => (s.End - s.Start).TotalMinutes),
                requiresConflictAndDependencyValidation = true }),
            scope = new { rangeStart=Local(Now),rangeEnd=Local(Now.AddDays(30)), timeZoneId=state.Preference.TimeZoneId,
                tasks=new { totalActive=active.Length,included=tasks.Length,truncated=active.Any(x=>!tasks.Any(t=>t.Id==x.Id)),matchingRequestIds=matches.Select(x=>x.Id) },
                events=new { total=events.Length,included=Math.Min(100,events.Length),truncated=events.Length>100 },
                sessions=new { total=sessions.Length,included=Math.Min(100,sessions.Length),truncated=sessions.Length>100 },
                history=new { maximumTurns=8, truncatedPossible=true } },
            tasks=tasks.Select(x=>new {x.Id,x.Name,Deadline=Local(x.Deadline),WorkWindowStart=LocalOrNull(x.WorkWindowStart),WorkWindowEnd=LocalOrNull(x.WorkWindowEnd),x.EstimatedMinutes,x.Progress,x.Splittable,x.MinimumSessionMinutes,x.Priority,x.TaskType,importance=ImportanceDto(x),status=x.Status.ToString()}),
            availability=state.Availability.Select(x=>new AvailabilityInput(x.DayOfWeek,x.StartMinute,x.EndMinute)),
            availabilityPolicy=state.Policy is { } policy ? PolicyDto(policy) : null,
            dailyRoutine=CurrentRoutineDays(state),
            exceptions=state.Exceptions.Where(x=>x.Date>=LocalDate(Now,state.Preference) && x.Date<=LocalDate(Now.AddDays(30),state.Preference)).Select(x=>new {x.Id,x.Date,x.StartMinute,x.EndMinute,x.IsAvailable,x.Note,x.OverridesWeeklyAvailability}),
            events=events.Take(100).Select(x=>new{x.Id,x.Name,Start=Local(x.Start),End=Local(x.End),x.EventType,x.Description,IsUrgent=IsUrgentEvent(x)}),
            sessions=sessions.Take(100).Select(x=>new{x.TaskId,Start=Local(x.Start),End=Local(x.End),x.IsLocked,status=x.Status.ToString()}),
            dependencies=state.Dependencies.Select(x=>new{x.TaskId,x.PredecessorId}),
            preference=new {state.Preference.MaximumDailyMinutes,state.Preference.MaximumSessionMinutes,state.Preference.MinimumSessionMinutes,state.Preference.BreakMinutes,state.Preference.PlanningHorizonDays,state.Preference.AutoReschedule}
        },AgentJson);
        return (json,notices.ToArray());
    }
}
