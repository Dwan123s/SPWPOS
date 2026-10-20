using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public Task<PlanView> PreviewNoteActivitiesAsync(Guid uid, Guid noteId, NoteActivityPlanInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var note = await Find<PlanningNote>(uid, noteId, ct);
        CheckNoteVersion(note, input.Version);
        CheckActivityNote(note);
        var analysis = JsonSerializer.Deserialize<NoteAnalysis>(note.AnalysisJson, NoteJson)!;
        Require(!analysis.NeedsReview || input.ReviewedConditions, "NOTE_NEEDS_REVIEW", "Kiểm tra và xác nhận các lưu ý trong ghi chú trước khi xem lịch.");
        Require(input.Activities is { Length: >= 1 and <= 12 } && input.HorizonDays is >= 1 and <= 30,
            "VALIDATION_ERROR", "Chọn 1–12 hoạt động và phạm vi 1–30 ngày.");
        var state = await State(uid, ct);
        var tasks = await NoteTasks(uid, input.Activities, state, ct);
        var events = NoteEvents(uid, input.Activities, state);
        var oneOffHours = input.AllowRequestedTimeOutsideAvailability ? NoteRequestedHours(uid, tasks, state.Preference) : [];
        var candidate = NoteCandidate(state with { Exceptions = state.Exceptions.Concat(oneOffHours).ToArray() }, tasks, events);
        var affected = tasks.Select(x => x.Id).Concat(TimeConflictTaskIds(candidate)).Distinct().ToArray();
        var start = Now.AddMinutes(5);
        Require(!candidate.Sessions.Any(x => affected.Contains(x.TaskId) && x.Status == WorkSessionStatus.Scheduled &&
            (x.Start < start || x.Start >= start.AddDays(input.HorizonDays)) &&
            !engine.FreeSlots(candidate, x.Start, x.End).Any(s => s.Start <= x.Start && s.End >= x.End)),
            "NOTE_HORIZON_CONFLICT", "Có phiên bị ảnh hưởng ngoài phạm vi xem trước. Chỉnh phạm vi hoặc lịch trước khi tiếp tục.", 409);
        var plan = engine.Generate(candidate, new(start, input.HorizonDays, affected, true, "NEW_TASK", false, false), Now);
        plan = ExplainNoteAvailability(plan, candidate, tasks);
        var proposal = new PlanProposal { UserId = uid, CreatedAt = Now, ExpiresAt = Now.AddMinutes(30), Revision = state.Preference.Revision,
            Kind = "RESCHEDULE", TriggerEvent = "NEW_TASK", HorizonStart = plan.Start, HorizonEnd = plan.End };
        plan = plan with { Id = proposal.Id, Kind = proposal.Kind, NoteContext = new(noteId, note.Version, input.Activities,
            tasks.Select(x => x.Id).ToArray(), events.Select(x => new EventView(x.Id, x.Name, x.Start, x.End, x.Description, x.EventType)).ToArray(),
            input.AllowRequestedTimeOutsideAvailability) };
        // The proposal stores drafts only. Task/event/session rows are created together after confirmation.
        proposal.PayloadJson = JsonSerializer.Serialize(plan);
        db.PlanProposals.Add(proposal);
        return plan;
    }, ct, false);

    public Task<NoteScheduleResult> ApplyNoteActivitiesAsync(Guid uid, Guid noteId, Guid planId, NoteApplyInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var note = await Find<PlanningNote>(uid, noteId, ct);
        var proposal = await Find<PlanProposal>(uid, planId, ct);
        var plan = JsonSerializer.Deserialize<PlanView>(proposal.PayloadJson)!;
        var context = plan.NoteContext;
        Require(context is not null && context.NoteId == noteId, "NOT_FOUND", "Không tìm thấy đề xuất của ghi chú.", 404);
        if (note.PlanId == planId && proposal.IsApplied)
            return new NoteScheduleResult(NoteViewOf(note), plan with { IsApplied = true, Status = proposal.Status, AppliedAt = proposal.AppliedAt });
        CheckNoteVersion(note, input.Version);
        Require(context!.NoteVersion == note.Version, "NOTE_STALE", "Ghi chú đã thay đổi. Xem trước lại trước khi lưu.", 409);
        CheckActivityNote(note);
        var state = await State(uid, ct);
        Require(proposal.Status == "PROPOSED" && !proposal.IsApplied && proposal.ExpiresAt > Now, "PLAN_EXPIRED", "Đề xuất hết hạn. Xem trước lại để tiếp tục.", 409);
        Require(proposal.Revision == state.Preference.Revision, "PLAN_STALE", "Lịch đã thay đổi. Xem trước lại để kiểm tra xung đột.", 409);
        Require(!plan.Warnings.Any(x => x.UnscheduledMinutes > 0 || x.Code == "SOFT_DEADLINE_EXCEEDED"),
            "NOTE_PLAN_INCOMPLETE", "Chưa xếp đủ các hoạt động. Chỉnh thời lượng, giờ rảnh hoặc hạn chót rồi xem trước lại.", 409);
        var tasks = await NoteTasks(uid, context.Activities, state, ct);
        Require(tasks.Length == context.TaskIds.Length, "PLAN_STALE", "Đề xuất không còn hợp lệ.", 409);
        for (var i = 0; i < tasks.Length; i++) tasks[i].Id = context.TaskIds[i];
        var events = context.Events.Select(x => new FixedEvent { Id = x.Id, UserId = uid, Name = x.Name,
            Description = x.Description, EventType = x.EventType, Start = x.Start, End = x.End }).ToArray();
        Require(events.All(x => x.Start >= Now), "PLAN_EXPIRED", "Có hoạt động đã bắt đầu. Xem trước lại để cập nhật.", 409);
        var oneOffHours = context.AllowRequestedTimeOutsideAvailability ? NoteRequestedHours(uid, tasks, state.Preference) : [];
        var candidate = NoteCandidate(state with { Exceptions = state.Exceptions.Concat(oneOffHours).ToArray() }, tasks, events);
        db.AvailabilityExceptions.AddRange(oneOffHours);
        db.WorkTasks.AddRange(tasks);
        db.FixedEvents.AddRange(events.Where(x => !state.Events.Any(e => e.Id == x.Id)));
        await db.SaveChangesAsync(ct);
        StorePlanDecisions(uid, proposal, plan, candidate);
        await db.SaveChangesAsync(ct);
        await ApplyPlanCoreAsync(uid, proposal, plan, candidate, ct);
        note.CreatedTaskIdsJson = JsonSerializer.Serialize(context.TaskIds, NoteJson);
        var analysis = JsonSerializer.Deserialize<NoteAnalysis>(note.AnalysisJson, NoteJson)!;
        note.AnalysisJson = JsonSerializer.Serialize(analysis with { Activities = context.Activities }, NoteJson);
        note.PlanId = planId; note.Version = Guid.NewGuid(); note.UpdatedAt = Now;
        return new NoteScheduleResult(NoteViewOf(note), plan with { IsApplied = true, Status = "APPLIED", AppliedAt = Now });
    }, ct);

    private static void CheckActivityNote(PlanningNote note)
    {
        Require(note.PlanId is null && note.CreatedTaskIdsJson == "[]", "NOTE_CONVERTED", "Ghi chú đã được lưu vào lịch. Tạo ghi chú mới cho hoạt động bổ sung.", 409);
        var analysis = note.AnalysisJson.Length == 0 ? null : JsonSerializer.Deserialize<NoteAnalysis>(note.AnalysisJson, NoteJson);
        Require(analysis?.Activities is not null && analysis.Source is "OpenAI" or "Gemini" or "VertexAI",
            "NOTE_NOT_ANALYZED", "Phân tích ghi chú trước khi xem lịch.", 409);
    }

    private static AvailabilityException[] NoteRequestedHours(Guid uid, WorkTask[] tasks, WorkPreference preference)
    {
        var zone = PlanningEngine.Zone(preference.TimeZoneId);
        var hours = new List<AvailabilityException>();
        foreach (var task in tasks.Where(x => x.WorkWindowStart is not null && x.WorkWindowEnd is not null))
        {
            var from = TimeZoneInfo.ConvertTime(task.WorkWindowStart!.Value, zone);
            var to = TimeZoneInfo.ConvertTime(task.WorkWindowEnd!.Value, zone);
            for (var date = DateOnly.FromDateTime(from.Date); date <= DateOnly.FromDateTime(to.Date); date = date.AddDays(1))
            {
                var first = date == DateOnly.FromDateTime(from.Date) ? from.Hour * 60 + from.Minute : 0;
                var last = date == DateOnly.FromDateTime(to.Date) ? to.Hour * 60 + to.Minute : 1440;
                if (last > first) hours.Add(new AvailabilityException { UserId = uid, Date = date, StartMinute = first,
                    EndMinute = last, IsAvailable = true, Note = "Giờ thực hiện đã xác nhận từ ghi chú" });
            }
        }
        return hours.ToArray();
    }

    private PlanView ExplainNoteAvailability(PlanView plan, PlanningState state, WorkTask[] tasks) => plan with
    {
        Warnings = plan.Warnings.Select(warning =>
        {
            var task = tasks.FirstOrDefault(x => x.Id == warning.TaskId);
            if (warning.Code != "INSUFFICIENT_CAPACITY" || task?.WorkWindowStart is not { } from || task.WorkWindowEnd is not { } to)
                return warning;
            // Check availability alone so event conflicts are never offered an availability override.
            var hoursOnly = state with { Events = [] };
            if (engine.FreeSlots(hoursOnly, from, to).Sum(x => (x.End - x.Start).TotalMinutes) >= task.EstimatedMinutes)
                return warning;
            var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
            var localStart = TimeZoneInfo.ConvertTime(from, zone); var localEnd = TimeZoneInfo.ConvertTime(to, zone);
            if (state.Exceptions.Where(x => !x.IsAvailable).Any(x =>
                PlanningEngine.At(x.Date, x.StartMinute, zone) < to && PlanningEngine.At(x.Date, x.EndMinute, zone) > from))
                return warning with { Code = "NOTE_UNAVAILABLE_TIME",
                    Message = $"Khung giờ {localStart:dd/MM HH:mm}–{localEnd:HH:mm} trùng thời gian đã đánh dấu không rảnh. Chọn giờ khác hoặc chỉnh thời gian không rảnh rồi xem lại lịch." };
            return warning with { Code = "NOTE_OUTSIDE_AVAILABILITY",
                Message = $"Khung giờ {localStart:dd/MM HH:mm}–{localEnd:HH:mm} nằm ngoài giờ rảnh đã thiết lập. Cho phép dùng giờ đã chọn cho lần này hoặc chỉnh giờ rảnh rồi xem lại lịch." };
        }).ToArray()
    };

    private async Task<WorkTask[]> NoteTasks(Guid uid, NoteActivity[] activities, PlanningState state, CancellationToken ct)
    {
        Require(activities.All(x => x is not null && x.Kind is "TASK" or "EVENT" && !string.IsNullOrWhiteSpace(x.Name) &&
            x.Name.Length <= 200 && x.Description is not null && x.Description.Length <= 4000 && !string.IsNullOrWhiteSpace(x.Type) && x.Type.Length <= 60 &&
            x.Priority is >= 1 and <= 4 && x.RepeatWeeks is >= 1 and <= 12), "VALIDATION_ERROR", "Hoạt động không hợp lệ.");
        var tasks = new List<WorkTask>();
        foreach (var activity in activities.Where(x => x.Kind == "TASK"))
        {
            Require(activity.RepeatWeeks == 1, "NOTE_NEEDS_REVIEW", "Công việc lặp cần được tạo riêng cho từng lần thực hiện.");
            Require(activity.EstimatedMinutes is >= 5 and <= 43200 && (activity.Start is not null || activity.End is null) &&
                (activity.Start is not null || activity.Deadline is not null),
                "NOTE_ACTIVITY_INCOMPLETE", $"Bổ sung thời lượng và giờ bắt đầu hoặc hạn chót cho “{activity.Name}”.");
            var end = activity.End ?? activity.Start?.AddMinutes(activity.EstimatedMinutes!.Value);
            var deadline = activity.Deadline ?? end!.Value;
            Require(deadline > Now && (activity.Start is null || activity.Start >= Now.AddMinutes(5) && end > activity.Start &&
                (end.Value - activity.Start.Value).TotalMinutes >= activity.EstimatedMinutes && deadline >= end),
                "NOTE_ACTIVITY_INCOMPLETE", $"Kiểm tra ngày và khung giờ của “{activity.Name}”.");
            var type = NormalizeTaskType(activity.Type);
            Require(await db.TaskTypes.AnyAsync(x => x.Code == type && x.IsActive, ct), "INVALID_TASK_TYPE", "Loại công việc không hợp lệ.");
            var input = new TaskInput(activity.Name, activity.Description, deadline, activity.EstimatedMinutes!.Value,
                activity.Priority, type, activity.Start is null, Math.Max(5, Math.Min(state.Preference.MinimumSessionMinutes, activity.EstimatedMinutes.Value)),
                IsHardDeadline: activity.IsHardDeadline, CreationSource: "NATURAL_LANGUAGE");
            ValidTask(input);
            var task = NewTask(uid, input, Now);
            task.WorkWindowStart = activity.Start; task.WorkWindowEnd = end;
            tasks.Add(task);
        }
        Require(state.Tasks.Count(x => !x.IsDeleted && x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled) + tasks.Count <= 500,
            "TASK_LIMIT", "Tối đa 500 công việc đang hoạt động.");
        return tasks.ToArray();
    }

    private FixedEvent[] NoteEvents(Guid uid, NoteActivity[] activities, PlanningState state)
    {
        var events = new List<FixedEvent>();
        foreach (var activity in activities.Where(x => x.Kind == "EVENT"))
        {
            Require(activity.Start is not null && activity.End is not null && activity.Start >= Now,
                "NOTE_ACTIVITY_INCOMPLETE", $"Bổ sung giờ bắt đầu và kết thúc cho “{activity.Name}”.");
            foreach (var item in EventOccurrences(uid, new(activity.Name, activity.Start!.Value, activity.End!.Value,
                activity.Description, activity.Type, activity.RepeatWeeks), state.Preference))
            {
                var existing = state.Events.Concat(events).FirstOrDefault(x => x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase) &&
                    x.EventType == item.EventType && x.Start == item.Start && x.End == item.End);
                events.Add(existing ?? item);
            }
        }
        return events.DistinctBy(x => x.Id).ToArray();
    }

    private PlanningState NoteCandidate(PlanningState state, WorkTask[] tasks, FixedEvent[] events)
    {
        var added = events.Where(x => !state.Events.Any(e => e.Id == x.Id)).ToArray();
        ValidateEventConflicts(state, added, null);
        var candidate = state with { Tasks = state.Tasks.Concat(tasks).ToArray(), Events = state.Events.Concat(added).ToArray() };
        ValidateProtectedSessions(candidate);
        return candidate;
    }
}
