using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static bool HasAgentActions(AgentIntent intent) => intent.ReplaceWeeklyAvailability || intent.WeeklySlots.Length > 0 ||
        intent.Exceptions.Length > 0 || intent.Events.Length > 0 || intent.Tasks.Length > 0 || intent.ScheduleTaskIds.Length > 0 || intent.Reschedule;

    private static AgentIntent ClarifyAgentIntent(AgentIntent intent, string question) => intent with {
        Reply = ClarificationReply(intent.Reply), Question = question.Trim(), ResultKind = "CLARIFICATION",
        ReplaceWeeklyAvailability = false, WeeklySlots = [], Exceptions = [], Events = [], Tasks = [], ScheduleTaskIds = [], Reschedule = false
    };

    private static MatchCollection RequestMatches(string text, string pattern) => Regex.Matches(text, pattern,
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

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
                    $"Ngày {date:dd/MM/yyyy} là {(date.DayOfWeek == DayOfWeek.Sunday ? "Chủ nhật" : "thứ " + ((int)date.DayOfWeek + 1))}. Bạn muốn dùng ngày này hay thứ đã nêu?");
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
            $"Theo múi giờ của bạn, {relative[0].Value} là {expected:dd/MM/yyyy}. Bạn xác nhận dùng ngày này nhé?");
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

    private static AgentIntent ConstrainEventReplanning(AgentIntent intent, string request, PlanningState state)
    {
        if (intent.Events.Length == 0) return intent;
        var text = MatchText(request);
        var replan = RequestMatches(text, @"\b(?:xep lai|sap xep lai|toi uu (?:lai )?|lap lai lich|reschedule|replan)\b").Count > 0;
        var global = replan && RequestMatches(text, @"\b(?:toan bo|tat ca|all|whole)\b").Count > 0;
        var named = state.Tasks.Where(t => text.Contains(MatchText(t.Name), StringComparison.Ordinal) ||
            request.Contains(t.Id.ToString(), StringComparison.OrdinalIgnoreCase)).Select(t => t.Id).ToHashSet();
        return intent with { Reschedule = intent.Reschedule && global,
            ScheduleTaskIds = global ? intent.ScheduleTaskIds : intent.ScheduleTaskIds.Where(named.Contains).ToArray() };
    }

    private static string? ClarificationRecoveryContext(AgentIntent intent, string request, string context, string[] activeTypes)
    {
        if (string.IsNullOrWhiteSpace(intent.Question) || HasAgentActions(intent)) return null;
        var question = MatchText(intent.Question);
        if (RequestMatches(question, @"\b(?:bao nhieu|khi nao|ngay nao|deadline|thoi luong|thoi han)\b").Count > 0) return null;
        var facts = new JsonObject();
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
