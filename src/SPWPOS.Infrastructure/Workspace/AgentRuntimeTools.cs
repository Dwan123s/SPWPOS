using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private sealed record RuntimeCandidate(string Id, AgentIntent Intent, AgentChangeSet Changes, PlanningState State,
        Guid[] Affected, AgentChangeView[] Summary, string[] Notices, PlanView? Plan)
    {
        public bool Feasible => Plan?.Warnings.Any(x => x.UnscheduledMinutes > 0 || x.Code == "SOFT_DEADLINE_EXCEEDED") != true;
    }
    private sealed record RuntimeExecution(AgentIntent Intent, RuntimeCandidate? Candidate, string? ReadReply, NoraRunView View, string? BlockCode = null);
    private static AgentIntent EmptyRuntimeIntent(string reply = "") => new(reply, "", false, [], [], [], [], [], false, 14);

    private AgentFreeTimeQuery RuntimeRange(JsonElement arguments, WorkPreference preference)
    {
        DateTimeOffset Read(string field)
        {
            var text = arguments.GetProperty(field).GetString()!;
            Require(Regex.IsMatch(text, @"(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) &&
                DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _), "INVALID_RANGE", "Ngày giờ cần có múi giờ rõ ràng.");
            return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
        }
        var query = new AgentFreeTimeQuery(Read("start"), Read("end"));
        var midnight = PlanningEngine.At(LocalDate(Now, preference), 0, PlanningEngine.Zone(preference.TimeZoneId));
        Require(query.End > query.Start && query.End - query.Start <= TimeSpan.FromDays(31) &&
            query.Start >= midnight.AddDays(-30) && query.End <= midnight.AddDays(31), "INVALID_RANGE", "Chọn phạm vi tối đa 31 ngày trong 30 ngày trước/tới.");
        return query;
    }

    private static bool RuntimeReplanAllowed(string source) => ChatMatch(source,
        @"\b(?:toi uu|xep lai|sap xep lai|lap lai lich|doi (?:cac |nhung )?phien|doi lich|doi cac (?:viec|cong viec)|reschedule|replan)\b").Success &&
        !ChatMatch(source, @"\b(?:khong|dung|chua) (?:duoc |tu dong )?(?:doi (?:cac )?phien|doi lich|xep lai|toi uu)\b").Success;

    private AgentIntent NormalizeRuntimeDraft(JsonElement payload, string source, PlanningState state, AgentPendingRequest? pending, string[] types)
    {
        var intent = TaskInterpreter.DecodeAgentIntent(payload, types);
        ValidateIntent(intent, true);
        intent = NormalizeAgentCategories(intent, state, types, source);
        intent = ConstrainEventReplanning(intent, source, state);
        intent = NormalizeAgentIntent(intent, source, state, pending);
        intent = RecognizeAgentOptimization(intent, source, state.Preference);
        intent = GroundAgentDates(intent, source, state.Preference, Now);
        ValidateIntent(intent);
        var normalizedSource = MatchText(source);
        var directives = RequestMatches(normalizedSource, @"\b(?:(?<split>khong can (?:lam )?lien tuc)|(?<continuous>lien tuc|khong (?:duoc )?(?:chia|tach)(?: nho)?)|(?<split>(?:(?:cho phep|dong y|co the|duoc|hay) )?chia nho|chia thanh|tach thanh|split))\b");
        var directive = directives.Cast<Match>().LastOrDefault(match => !match.Groups["split"].Success ||
            !ChatMatch(normalizedSource[..match.Index], @"(?:^|[.!?;,\r\n])[^.!?;,\r\n]*\bkhong(?: (?:duoc|the))?\s*$").Success);
        var allowSplit = directive?.Groups["split"].Success == true;
        var continuous = directive?.Groups["continuous"].Success == true;
        var changeDeadline = ChatMatch(source, @"\b(?:doi|thay doi|cap nhat|gia han|lui|doi lai)\s+(?:lai\s+)?(?:deadline|han chot|han hoan thanh)\b").Success;
        var changePriority = ChatMatch(source, @"\b(?:ha|giam|doi|thay doi|cap nhat)\s+(?:muc\s+)?(?:do\s+)?uu tien\b").Success;
        intent = intent with { Tasks = intent.Tasks.Select(task =>
        {
            var original = task.TaskId is { } id ? state.Tasks.SingleOrDefault(x => x.Id == id) : null;
            // A simulation cannot manufacture feasibility by relaxing a saved or
            // explicit constraint. A later, explicit user directive may change it.
            if (original is { Splittable: false } && !allowSplit || intent.Tasks.Length == 1 && continuous)
                task = task with { Splittable = false };
            if (original is not null && task.Deadline > original.Deadline && !changeDeadline)
                task = task with { Deadline = original.Deadline };
            if (original is not null && task.Priority < original.Priority && !changePriority)
                task = task with { Priority = original.Priority };
            if (!changePriority && RequestedUrgentEvent(new FixedEvent { Name = task.Name, EventType = "OTHER" }, source))
                task = task with { Priority = 4 };
            return task;
        }).ToArray(), ScheduleTaskIds = intent.ScheduleTaskIds.Concat(intent.Tasks.Where(x => x.TaskId is not null).Select(x => x.TaskId!.Value)).Distinct().ToArray() };
        Require(intent.Question.Length > 0 || HasAgentActions(intent), "DRAFT_HAS_NO_ACTION", "Bản nháp phải có hành động; câu hỏi chỉ đọc dùng công cụ đọc.");
        Require(!HasAgentActions(intent) || !ChatMatch(source, @"\b(?:chi xem|chi tu van|khong thay doi|khong tao|read only)\b").Success,
            "READ_ONLY_REQUEST", "Yêu cầu chỉ đọc không được chuẩn bị thay đổi.");
        Require(!intent.Reschedule || RuntimeReplanAllowed(source) || intent.Events.Length > 0,
            "REPLAN_NOT_AUTHORIZED", "Chưa có yêu cầu xếp lại các phiên đã lưu.");
        return intent;
    }

    private async Task<RuntimeCandidate> SimulateRuntimeAsync(Guid uid, AgentIntent intent, PlanningState state,
        AgentTurnInput input, string source, bool timetable, string strategy, CancellationToken ct)
    {
        Require(strategy is "keep_existing" or "replan_flexible", "INVALID_STRATEGY", "Chọn chiến lược được hỗ trợ.");
        Require(strategy != "replan_flexible" || RuntimeReplanAllowed(source), "REPLAN_NOT_AUTHORIZED", "Bạn cần cho phép đề xuất dời phiên linh hoạt trước.");
        var (changes, candidate, affected, summary, notices) = await BuildAgentChangesAsync(uid, intent, state, source, ct, source, timetable);
        var start = intent.OptimizationQuery is { } scope && scope.Start > Now.AddMinutes(5) ? scope.Start : Now.AddMinutes(5);
        var end = intent.OptimizationQuery?.End;
        if (strategy == "replan_flexible")
        {
            // Scope is derived from the grounded request, never expanded by the
            // model. New-task windows bound which old sessions can be moved.
            var from = intent.OptimizationQuery?.Start ?? candidate.Tasks.Where(x => affected.Contains(x.Id))
                .Select(x => x.WorkWindowStart ?? start).DefaultIfEmpty(start).Min();
            var until = intent.OptimizationQuery?.End ?? candidate.Tasks.Where(x => affected.Contains(x.Id))
                .Select(x => x.WorkWindowEnd ?? x.Deadline).DefaultIfEmpty(start.AddDays(intent.HorizonDays)).Max();
            if (from > start) start = from;
            until = until < start.AddDays(intent.HorizonDays) ? until : start.AddDays(intent.HorizonDays);
            end = until;
            var movable = candidate.Sessions.Where(x => x.Status == WorkSessionStatus.Scheduled && !x.IsLocked && x.Start >= start && x.Start < until)
                .Select(x => x.TaskId);
            affected = affected.Concat(movable).Distinct().ToArray();
        }
        Require(end is null || end > start, "INVALID_RANGE", "Khoảng thực hiện đã qua hoặc không còn hợp lệ.");
        var planInput = new PlanInput(start, intent.HorizonDays, affected, true, "AVAILABILITY_CHANGED",
            input.UseCapacityFactor, input.UseEstimateBias) { WindowEnd = end };
        PlanView? plan = changes.UrgentEventIds is { Length: > 0 } urgent
            ? engine.GenerateForUrgentEvents(candidate, planInput, Now, urgent)
            : affected.Length > 0 ? engine.Generate(candidate, planInput, Now) : null;
        if (plan is not null)
        {
            plan = plan with { IsOptimization = intent.OptimizationQuery is not null || strategy == "replan_flexible" };
            plan = RespectSchedulingExclusions(plan, candidate, affected, input with { Text = source });
        }
        return new(Guid.NewGuid().ToString("N"), intent, changes, candidate, affected, summary, notices, plan);
    }

    private static object RuntimeCandidateFacts(RuntimeCandidate candidate, PlanningState original)
    {
        var p = candidate.Plan;
        var tasks = candidate.State.Tasks.ToDictionary(x => x.Id);
        object Slot(DateTimeOffset start, DateTimeOffset end) => new {
            start = TimeZoneInfo.ConvertTime(start, PlanningEngine.Zone(original.Preference.TimeZoneId)),
            end = TimeZoneInfo.ConvertTime(end, PlanningEngine.Zone(original.Preference.TimeZoneId))
        };
        return new { candidateId = candidate.Id, feasible = candidate.Feasible, snapshotRevision = original.Preference.Revision,
            changes = candidate.Summary, warnings = p?.Warnings ?? [],
            proposed = p?.Sessions.Where(x => !x.Preserved).Select(x => new { x.TaskId, name = tasks.GetValueOrDefault(x.TaskId)?.Name, time = Slot(x.Start, x.End) }).ToArray(),
            previous = original.Sessions.Where(x => p is not null && x.Status == WorkSessionStatus.Scheduled && x.Start >= p.Start && x.Start < p.End)
                .Take(100).Select(x => new { x.TaskId, name = tasks.GetValueOrDefault(x.TaskId)?.Name, time = Slot(x.Start, x.End), x.IsLocked }).ToArray(),
            requiresConfirmation = p?.RequiresConfirmation == true, businessDataSaved = false };
    }
}
