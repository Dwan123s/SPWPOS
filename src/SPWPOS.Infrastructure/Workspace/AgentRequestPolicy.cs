using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static bool HasAgentActions(AgentIntent intent) => intent.ReplaceWeeklyAvailability || intent.WeeklySlots.Length > 0 ||
        intent.Exceptions.Length > 0 || intent.Events.Length > 0 || intent.Tasks.Length > 0 || intent.ScheduleTaskIds.Length > 0 || intent.Reschedule || intent.Reminder is not null;

    private static AgentIntent LimitAgentBatch(AgentIntent intent) => intent.Tasks is { Length: > 12 and <= 500 }
        ? ClarifyAgentIntent(intent with { Reply = "Một yêu cầu chỉ hỗ trợ tối đa 12 công việc. Mình chưa lưu công việc nào.", Options = [], Pending = null },
            "Bạn muốn chia danh sách thành các nhóm tối đa 12 công việc để mình xử lý đầy đủ từng nhóm không?")
        : intent;

    private static HashSet<Guid> ExplicitlyPreservedTasks(string request, PlanningState state) => state.Tasks.Where(task =>
        ChatMatch(request, @"\b(?:giu nguyen|(?:khong|dung|cho)\s+(?:(?:can|duoc|tu dong)\s+)?(?:(?:tao|xep|doi)\s+(?:hay|hoac|va)\s+)?(?:tao|doi|di chuyen|xep|sap xep))\s+(?:(?:lich|phien|cho|cong viec)\s+)*" +
            Regex.Escape(MatchText(task.Name)) + @"\b").Success).Select(task => task.Id).ToHashSet();

    private static string? ConsequentialRequestQuestion(AgentIntent intent, string request, PlanningState state)
    {
        if (!HasAgentActions(intent)) return null;
        var text = MatchText(request);
        foreach (Match date in RequestMatches(text, @"\b\d{1,2}/\d{1,2}/\d{4}\b"))
            if (!DateOnly.TryParseExact(date.Value, ["d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return "Ngày bạn nhập không tồn tại trên lịch. Bạn cho mình ngày hợp lệ để mình lưu đúng nhé?";
        if (RequestMatches(text, @"\b(?:cst|ist|bst)\b").Count > 0 &&
            RequestMatches(text, @"\b(?:utc|gmt)\s*[+-]\s*\d|\b(?:asia|america|europe)/").Count == 0)
            return "Tên múi giờ viết tắt có nhiều nghĩa. Bạn cho mình múi giờ cụ thể hoặc độ lệch UTC để lưu đúng giờ nhé?";
        if (RequestMatches(text, @"\b(?:gui|send)\s+(?:email|e-mail|mail|thu moi)|\b(?:email|e-mail)\s+(?:moi|invite)\b").Count > 0)
            return "Mình có thể cập nhật lịch nhưng chưa gửi email mời. Bạn muốn chỉ lưu lịch, hay dừng để dùng công cụ gửi lời mời?";
        foreach (var edit in intent.Tasks.Where(x => x.TaskId is not null))
        {
            var original = state.Tasks.FirstOrDefault(x => x.Id == edit.TaskId);
            if (original is null) continue;
            var sameName = state.Tasks.Where(x => !x.IsDeleted && x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled &&
                MatchText(x.Name) == MatchText(original.Name)).ToArray();
            if (sameName.Length < 2 || request.Contains(original.Id.ToString(), StringComparison.OrdinalIgnoreCase) ||
                ChatMatch(request, @"\b(?:tat ca|ca hai|both|all)\b").Success) continue;
            if (original.Deadline is not { } deadline)
                return $"Có nhiều công việc tên “{original.Name}”. Bạn mở chi tiết và chọn đúng công việc cần đổi nhé.";
            var date = LocalDate(deadline, state.Preference);
            var dates = RequestMatches(text, @"\b\d{1,2}/\d{1,2}/\d{4}\b|\b\d{4}-\d{2}-\d{2}\b")
                .Select(m => DateOnly.TryParseExact(m.Value, ["d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy", "yyyy-MM-dd"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? (DateOnly?)parsed : null).ToArray();
            if (!dates.Contains(date) || sameName.Count(x => x.Deadline is { } due && LocalDate(due, state.Preference) == date) != 1)
                return $"Có nhiều công việc tên “{original.Name}”. Bạn muốn đổi công việc có hạn chót ngày nào?";
        }
        if (intent.Events.Length == 0) return null;
        if (RequestMatches(text, @"\b(?:vo (?:han|cuc)|mai mai|forever|indefinitely|khong (?:co )?(?:ngay )?ket thuc)\b").Count > 0)
            return "Lịch lặp cần có giới hạn. Bạn muốn lặp bao nhiêu lần hoặc kết thúc vào ngày nào?";
        if (RequestMatches(text, @"\b(?:hai|2) tuan (?:mot|1) lan|\b(?:biweekly|fortnightly|every (?:two|2) weeks)\b").Count > 0 &&
            intent.Events.Any(x => x.RepeatWeeks > 1))
            return "Lặp hai tuần một lần khác với lặp hàng tuần. Bạn cho mình các ngày cụ thể để mình lưu đúng từng buổi nhé?";
        if (intent.Events.Any(x => x.RepeatWeeks > 1) &&
            RequestMatches(text, @"\b\d+\s*(?:lan|buoi|tuan|weeks?|times?)\b|\b(?:den|toi|until|through|ket thuc)\s+(?:ngay\s+)?\d{1,4}[/\-]").Count == 0)
            return "Bạn muốn lịch lặp bao nhiêu lần hoặc kết thúc ngày nào? Mình chưa tự chọn số buổi.";
        foreach (var edited in intent.Events.Where(x => x.ExistingId is not null))
        {
            var original = state.Events.FirstOrDefault(x => x.Id == edited.ExistingId);
            if (original is not null && state.Events.Count(x => x.Name.Equals(original.Name, StringComparison.OrdinalIgnoreCase)) > 1 &&
                RequestMatches(text, @"\b\d{1,2}/\d{1,2}|\b\d{4}-\d{2}-\d{2}|\b(?:hom nay|ngay mai|today|tomorrow)\b").Count == 0 &&
                !request.Contains(edited.ExistingId!.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                return $"Có nhiều buổi “{original.Name}”. Bạn muốn đổi buổi ngày nào?";
        }
        return null;
    }

    private static AgentIntent ClarifyAgentIntent(AgentIntent intent, string question) => intent with {
        Reply = ClarificationReply(intent.Reply), Question = question.Trim(), ResultKind = "CLARIFICATION",
        ReplaceWeeklyAvailability = false, WeeklySlots = [], Exceptions = [], Events = [], Tasks = [], ScheduleTaskIds = [], Reschedule = false, FreeTimeQuery = null, ScheduleReviewQuery = null, OptimizationQuery = null
    };

    private static MatchCollection RequestMatches(string text, string pattern) => Regex.Matches(text, pattern,
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static AgentIntent NormalizeDatedFreeReplacement(AgentIntent intent, string request)
    {
        if (intent.Question.Length > 0) return intent;
        var free = intent.Exceptions.Where(x => x.IsAvailable).ToArray();
        if (free.Length == 0 || intent.Exceptions.Any(x => x.Date != free[0].Date)) return intent;
        var range = ChatMatch(request, @"\bthay (?:the )?(?:gio|khung(?: gio)?) ranh[^.!?;\r\n]{0,60}?\bthanh\s+(\d{1,2}):([0-5]\d)\s*(?:den|[-–])\s*(\d{1,2}):([0-5]\d)\b");
        if (!range.Success) return intent;
        var start = int.Parse(range.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
        var end = int.Parse(range.Groups[3].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(range.Groups[4].Value, CultureInfo.InvariantCulture);
        if (start >= end || end > 1440) return intent;
        // The source explicitly replaces one day's free hours with this range.
        // Busy exceptions subtract their gaps; a model's partial/additive free
        // representation must not accidentally delete the rest of that range.
        var exact = new AgentExceptionCommand(free[0].Date, start, end, true, free[0].Note, free[0].ExistingId);
        return intent with { Exceptions = intent.Exceptions.Where(x => !x.IsAvailable).Prepend(exact).ToArray() };
    }

    // Explicit consent in the message also applies to retries of the same request ID.
    private static bool PreviewOnly(string text) => RequestMatches(MatchText(text),
        @"\b(?:chi (?:xem thu|xem truoc|xem ban nhap)|chua (?:dong y )?(?:luu|ap dung)|(?:dung|khong|chua) (?:tu dong )?(?:luu|ap dung)|preview only|do not (?:save|apply)|don't (?:save|apply))\b").Count > 0;

    private static AgentIntent GroundAgentDates(AgentIntent intent, string request, WorkPreference preference, DateTimeOffset now)
    {
        var text = MatchText(request);
        AgentIntent AskCalendar(string reply, string question) => ClarifyAgentIntent(intent with {
            Reply = reply, Options = [], Pending = intent.Pending ?? new(request, null, null, null, null, null)
        }, question);
        // Only adjacent weekday/date pairs describe the same date. Separate clauses may name different days.
        foreach (Match match in RequestMatches(text,
            @"\b(?<weekday>thu (?:hai|ba|tu|nam|sau|bay|[2-7])|chu nhat)\s*(?:ngay\s*)?(?<date>\d{1,2}/\d{1,2}/\d{4})\b"))
        {
            if (!DateOnly.TryParseExact(match.Groups["date"].Value, ["d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
            var weekday = match.Groups["weekday"].Value switch {
                "thu hai" or "thu 2" => DayOfWeek.Monday, "thu ba" or "thu 3" => DayOfWeek.Tuesday,
                "thu tu" or "thu 4" => DayOfWeek.Wednesday, "thu nam" or "thu 5" => DayOfWeek.Thursday,
                "thu sau" or "thu 6" => DayOfWeek.Friday, "thu bay" or "thu 7" => DayOfWeek.Saturday, _ => DayOfWeek.Sunday };
            if (date.DayOfWeek != weekday)
                return AskCalendar("Thứ và ngày trong yêu cầu không khớp nhau.",
                    $"Ngày {date:dd'/'MM'/'yyyy} là {(date.DayOfWeek == DayOfWeek.Sunday ? "Chủ nhật" : "thứ " + ((int)date.DayOfWeek + 1))}. Bạn muốn dùng ngày này hay thứ đã nêu?");
        }
        if ((intent.OptimizationQuery ?? intent.ScheduleReviewQuery ?? intent.FreeTimeQuery) is { } query)
        {
            var queryDay = RequestMatches(text, @"\b(ngay mai|hom nay|tomorrow|today|ngay kia)\b");
            if (queryDay.Count == 1 && RequestMatches(text, @"\d{1,2}/\d{1,2}|\d{4}-\d{2}-\d{2}").Count == 0)
            {
                var offset = queryDay[0].Value is "ngay mai" or "tomorrow" ? 1 : queryDay[0].Value == "ngay kia" ? 2 : 0;
                var expectedDay = LocalDate(now, preference).AddDays(offset);
                if (LocalDate(query.Start, preference) != expectedDay || LocalDate(query.End.AddTicks(-1), preference) != expectedDay)
                    return AskCalendar("Ngày AI diễn giải chưa khớp ngày theo múi giờ của bạn.",
                        $"Theo múi giờ của bạn, {queryDay[0].Value} là {expectedDay:dd'/'MM'/'yyyy}. Bạn muốn kiểm tra ngày này nhé?");
            }
            return intent;
        }
        if (!HasAgentActions(intent)) return intent;
        // A single relative date for a single event/exception has an unambiguous account-local anchor.
        // Multiple dates and task deadlines require clause-level interpretation, so are left to clarification by AI.
        var relative = RequestMatches(text, @"\b(ngay mai|hom nay|tomorrow|today)\b");
        if (relative.Count != 1 || RequestMatches(text, @"\d{1,2}/\d{1,2}|\d{4}-\d{2}-\d{2}|ngay kia").Count > 0 ||
            intent.Events.Length + intent.Exceptions.Length != 1 || intent.Tasks.Length > 0) return intent;
        var expected = LocalDate(now, preference).AddDays(relative[0].Value is "ngay mai" or "tomorrow" ? 1 : 0);
        var actual = intent.Events.Length == 1 ? LocalDate(intent.Events[0].Start, preference) : intent.Exceptions[0].Date;
        return actual == expected ? intent : AskCalendar("Ngày AI diễn giải chưa khớp ngày theo múi giờ của bạn.",
            $"Theo múi giờ của bạn, {relative[0].Value} là {expected:dd'/'MM'/'yyyy}. Bạn xác nhận dùng ngày này nhé?");
    }

    private static string ClarificationReply(string reply)
        => AgentExplanation(reply) is { Length: > 0 } explanation ? explanation : "Mình cần làm rõ trước khi thay đổi lịch.";

    private static string AgentExplanation(string reply)
    {
        // Keep the explanation, but never surface a model's claim that discarded actions were saved.
        var sentences = Regex.Split(reply, @"(?<=[.!?])\s+|[\r\n]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        var safe = sentences.Where(sentence => RequestMatches(MatchText(sentence),
            @"\b(?:da|vua)\b(?!\s+hieu\b).*\b(?:luu|ghi nhan|tao|cap nhat|xep|len lich|ap dung|doi|them)\b|\b(?:toi|minh|he thong) se (?:luu|tao|xep|len lich|cap nhat|ap dung)\b|\b(?:saved|created|updated|applied|scheduled)\b").Count == 0);
        var explanation = string.Join(" ", safe).Trim();
        return explanation;
    }

    private AgentIntent ConstrainEventReplanning(AgentIntent intent, string request, PlanningState state)
    {
        if (intent.Events.Length == 0) {
            if (intent.Question.Length == 0 && (intent.Reschedule || intent.ScheduleTaskIds.Length > 0)) {
                var clockTasks = ClockReferencedSessionTasks(request, state);
                if (clockTasks.Count > 0) return intent with { Reschedule = false, ScheduleTaskIds = clockTasks.ToArray() };
            }
            return intent;
        }
        if (intent.Events.Length == 1 && SessionMovePurposeMinute(request) is { } purposeMinute) {
            var local = TimeZoneInfo.ConvertTime(intent.Events[0].Start, PlanningEngine.Zone(state.Preference.TimeZoneId));
            Require(local.Hour * 60 + local.Minute == purposeMinute, "WRONG_MOVE_TARGET",
                "Bạn yêu cầu dời phiên làm việc và giữ giờ hẹn đã nói. Hãy giữ đúng giờ hẹn, chỉ thử dời phần việc được cho phép.");
        }
        var text = MatchText(request);
        var replan = RequestMatches(text, @"\b(?:xep lai|sap xep lai|toi uu (?:lai )?|lap lai lich|reschedule|replan)\b").Count > 0;
        var global = replan && RequestMatches(text, @"\b(?:toan bo|tat ca|all|whole)\b").Count > 0;
        var preserved = ExplicitlyPreservedTasks(request, state);
        var named = state.Tasks.Where(t => !preserved.Contains(t.Id) && (text.Contains(MatchText(t.Name), StringComparison.Ordinal) ||
            request.Contains(t.Id.ToString(), StringComparison.OrdinalIgnoreCase))).Select(t => t.Id).ToHashSet();
        var clockReferenced = ClockReferencedSessionTasks(request, state, intent.Events);
        // A task mentioned in the PURPOSE or in “keep other work” is not a
        // movement target. Exact clock selection narrows a model's broad IDs.
        if (clockReferenced.Count > 0) named = clockReferenced;
        return intent with { Reschedule = intent.Reschedule && global,
            ScheduleTaskIds = global ? intent.ScheduleTaskIds : intent.ScheduleTaskIds.Where(named.Contains).ToArray() };
    }

    private static bool RequestsConflictingSessionMove(string request) =>
        (ChatMatch(request, @"\b(?:xep lai|sap xep lai|chuyen|di chuyen|doi)\s+(?:(?:rieng|cac|nhung|toan bo|tat ca)\s+)*phien\s+(?:bi\s+)?(?:trung|anh huong|xung dot)\b").Success ||
            ChatMatch(request, @"\bgiu (?:nguyen )?lich khac toi da\b").Success) &&
        !ForbidsSessionMoves(request);

    private static bool ForbidsSessionMoves(string request) => ChatMatch(request,
        @"\b(?:khong|dung|cho)\s+(?:tu dong\s+)?(?:xep lai|sap xep lai|chuyen|di chuyen|doi)\b(?!\s+(?:(?:gio|lich|bai|cuoc|buoi)\s+)*(?:kiem tra|hop|kham|hen|thi)\b)(?!\s+(?:(?:cac|nhung)\s+)?(?:cong viec|phien|lich)\s+khong lien quan\b)").Success;

    private static string? ClarificationRecoveryContext(AgentIntent intent, string request, string context, string[] activeTypes)
    {
        if (string.IsNullOrWhiteSpace(intent.Question) || HasAgentActions(intent)) return null;
        var question = MatchText(intent.Question);
        if (RequestMatches(question, @"\b(?:bao nhieu|khi nao|ngay nao|deadline|thoi luong|thoi han)\b").Count > 0) return null;
        var facts = new JsonObject();
        if (RequestMatches(question, @"\b(?:chia|split)\b").Count > 0 &&
            RequestMatches(MatchText(request), @"\b(?:chia (?:duoc|phien|nho)|co the chia|splittable)\b").Count > 0 &&
            RequestMatches(MatchText(request), @"\b(?:khong|dung) chia\b|\blien tuc\b").Count == 0)
            facts["splittingAlreadyAuthorized"] = true;
        if (RequestMatches(question, @"\b(?:loai|danh muc|category)\b").Count > 0)
        {
            var category = RequestedAgentCategory(request);
            if (category.Success && !CategorySubstitutionForbidden(request))
            {
                var requested = category.Groups["code"].Value.ToUpperInvariant();
                var resolved = activeTypes.Contains(requested, StringComparer.Ordinal) ? requested : "OTHER";
                if (activeTypes.Contains(resolved, StringComparer.Ordinal))
                    facts["taskCategory"] = new JsonObject { ["requestedCode"] = requested, ["resolvedCode"] = resolved, ["mayCreateCategory"] = false };
            }
        }
        if (AgentInputRules.ExplicitTimeRange(request) is { } range)
        {
            // The model can offer AM/PM alternatives entirely as clock values,
            // e.g. "07:00 đến 12:00 hay 19:00 đến 24:00", without saying sáng/tối.
            var clocks = RequestMatches(question, @"\b(?<hour>[01]?\d|2[0-4]):(?<minute>[0-5]\d)\b")
                .Select(m => int.Parse(m.Groups["hour"].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(m.Groups["minute"].Value, CultureInfo.InvariantCulture)).ToHashSet();
            var numericAlternative = RequestMatches(question, @"\b(?:hay|or)\b").Count > 0 &&
                clocks.Contains(range.StartMinute) && clocks.Contains((range.StartMinute + 720) % 1440);
            if (numericAlternative || RequestMatches(question, @"\bsang\b.*\b(?:toi|chieu)\b|\bmorning\b.*\bevening\b|\b(?:am|pm)\b").Count > 0)
                facts["explicitTimeRange"] = new JsonObject { ["startMinute"] = range.StartMinute, ["endMinute"] = range.EndMinute };
        }
        if (facts.Count == 0) return null;
        var root = JsonNode.Parse(context)!.AsObject(); root["serverResolvedFacts"] = facts;
        return root.ToJsonString();
    }

    // Category codes are uppercase identifiers. Natural-language labels such as
    // "loại công việc" must not be mistaken for an unknown code named CONG.
    private static Match RequestedAgentCategory(string request) => Regex.Match(request,
        @"\b(?i:ma loai|mã loại|loai|loại|category|tasktype|eventtype)\s*(?i:la|là|is|must be)?\s*[:=]?\s*(?<code>[A-Z][A-Z0-9_]{0,59})\b",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static bool CategorySubstitutionForbidden(string request) => RequestMatches(MatchText(request),
        @"\b(?:bat buoc|khong (?:dung|su dung|chap nhan) loai khac|only|must|no fallback|do not (?:use|allow) (?:any )?other)\b").Count > 0;
}
