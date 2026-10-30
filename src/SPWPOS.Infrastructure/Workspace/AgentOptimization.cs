using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private AgentIntent RecognizeAgentOptimization(AgentIntent intent, string request, WorkPreference preference)
    {
        if (!intent.Reschedule || intent.Question.Length > 0 || intent.Events.Length > 0 || intent.Tasks.Length > 0 ||
            intent.ReplaceWeeklyAvailability || intent.WeeklySlots.Length > 0 || intent.Exceptions.Length > 0) return intent;
        var text = MatchText(request);
        if (RequestMatches(text, @"\b(?:toi uu(?: hoa)?|xep lai|sap xep lai|lap lai lich|replan|reschedule)\b").Count == 0)
            return intent.OptimizationQuery is null ? intent : ClarifyAgentIntent(intent, "Bạn muốn mình chuẩn bị phương án xếp lại lịch trong khoảng này nhé?");
        if (RequestMatches(text, @"\b(?:khong|dung|chua) (?:can |hay )?(?:toi uu|xep lai|sap xep lai|lap lai lich)|\b(?:toi uu|xep lai) (?:la gi|nhu the nao)|^(?:ban |hay )?(?:lam sao|huong dan|giai thich)\b").Count > 0)
            return ClarifyAgentIntent(intent, "Bạn muốn tìm hiểu cách tối ưu hay muốn mình chuẩn bị phương án xếp lại lịch để xem trước?");
        var today = LocalDate(Now, preference);
        var zone = PlanningEngine.Zone(preference.TimeZoneId);
        var relative = RequestMatches(text, @"\b(?:hom nay|ngay mai|ngay kia|today|tomorrow|tuan nay|tuan sau)\b");
        AgentFreeTimeQuery? expected = null;
        if (relative.Count == 1 && RequestMatches(text, @"\d{1,2}/\d{1,2}|\d{4}-\d{2}-\d{2}").Count == 0)
        {
            var label = relative[0].Value;
            var week = label is "tuan nay" or "tuan sau";
            var first = week ? today.AddDays(-((int)today.DayOfWeek + 6) % 7 + (label == "tuan sau" ? 7 : 0))
                : today.AddDays(label is "ngay mai" or "tomorrow" ? 1 : label == "ngay kia" ? 2 : 0);
            expected = new(PlanningEngine.At(first, 0, zone), PlanningEngine.At(first.AddDays(week ? 7 : 1), 0, zone));
        }
        // Preserve explicitly narrowed hours. Whole relative days/weeks have
        // authoritative local boundaries, independent of a provider's UTC clock.
        var narrowed = RequestMatches(text, @"\b(?:buoi (?:sang|chieu|toi)|sang|chieu|tu \d|den \d)\b|\d{1,2}:\d{2}|\d{1,2}\s*(?:h|gio)\b").Count > 0;
        if (expected is not null && !narrowed) intent = intent with { OptimizationQuery = expected };
        if (intent.OptimizationQuery is null)
        {
            if (relative.Count > 0 || narrowed || RequestMatches(text, @"\d|thu hai|thu ba|thu tu|thu nam|thu sau|thu bay|chu nhat").Count > 0)
                return ClarifyAgentIntent(intent, "Bạn muốn mình tối ưu từ ngày, giờ nào đến ngày, giờ nào?");
            intent = intent with { OptimizationQuery = new(PlanningEngine.At(today, 0, zone), PlanningEngine.At(today.AddDays(7), 0, zone)) };
        }
        var scope = intent.OptimizationQuery;
        if (scope.End <= Now.AddMinutes(5) || scope.End <= scope.Start || scope.End - scope.Start > TimeSpan.FromDays(31) ||
            scope.End > PlanningEngine.At(today.AddDays(31), 0, zone))
            return ClarifyAgentIntent(intent, "Bạn chọn một khoảng từ hiện tại trong 30 ngày tới để mình tối ưu nhé?");
        return intent with { FreeTimeQuery = null, ScheduleReviewQuery = null, ResultKind = "MUTATION",
            HorizonDays = Math.Clamp((int)Math.Ceiling((scope.End - (scope.Start > Now ? scope.Start : Now)).TotalDays), 1, 30) };
    }

    private Guid[] OptimizationTaskIds(PlanningState state, AgentFreeTimeQuery scope)
    {
        var start = scope.Start > Now.AddMinutes(5) ? scope.Start : Now.AddMinutes(5);
        return state.Tasks.Where(task => !task.IsDeleted && task.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled &&
            task.Deadline > start && (task.WorkWindowStart is null || task.WorkWindowStart < scope.End) &&
            (task.WorkWindowEnd is null || task.WorkWindowEnd > start) &&
            (state.Sessions.Any(session => session.TaskId == task.Id && session.Status == WorkSessionStatus.Scheduled &&
                !session.IsLocked && session.Start >= start && session.Start < scope.End) ||
             !state.Sessions.Any(session => session.TaskId == task.Id && session.End > Now &&
                session.Status is WorkSessionStatus.Scheduled or WorkSessionStatus.Running or WorkSessionStatus.Paused)))
            .Select(task => task.Id).ToArray();
    }

    private static string OptimizationReply(PlanView? plan, AgentChangeView[] changes, WorkPreference preference, bool changed)
    {
        if (plan is null) return "Mình chưa thấy công việc nào có thể xếp lại trong khoảng này. Bạn có thể thêm việc cần làm hoặc chọn khoảng khác nhé.";
        var range = LocalWindow(plan.Start, plan.End, preference);
        if (!changed) return $"Mình đã kiểm tra lịch từ {range}. Với giờ rảnh và các điều kiện hiện tại, mình chưa tìm được cách xếp khác; bạn có thể giữ lịch đang có.";
        var moved = changes.Count(change => change.Kind == "SESSION" && change.Before.Length > 0);
        return $"Mình đã chuẩn bị phương án từ {range}" + (moved > 0 ? $", xếp lại {moved} công việc." : ".") +
            " Mình xét hạn chót và mức ưu tiên trước, rồi cân nhắc giờ làm việc bạn thích, khoảng trống và việc chia nhỏ phiên. " +
            "Bạn xem giờ cũ và giờ đề xuất bên dưới nhé. Bấm Xác nhận kế hoạch để lưu phương án này.";
    }
}
