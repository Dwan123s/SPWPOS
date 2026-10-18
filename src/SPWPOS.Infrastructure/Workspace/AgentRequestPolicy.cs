using System.Globalization;
using System.Text.RegularExpressions;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static MatchCollection RequestMatches(string text, string pattern) => Regex.Matches(text, pattern,
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // Explicit consent in the message also applies to retries of the same request ID.
    private static bool PreviewOnly(string text) => RequestMatches(MatchText(text),
        @"\b(?:chi (?:xem thu|xem truoc|xem ban nhap)|chua (?:dong y )?(?:luu|ap dung)|(?:dung|khong|chua) (?:tu dong )?(?:luu|ap dung)|preview only|do not (?:save|apply)|don't (?:save|apply))\b").Count > 0;

    private static AgentIntent GroundAgentDates(AgentIntent intent, string request, WorkPreference preference, DateTimeOffset now)
    {
        var text = MatchText(request);
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
                return ClarifyAgentIntent(intent with { Reply = "Thứ và ngày trong yêu cầu không khớp nhau.", Options = [] },
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
        return actual == expected ? intent : ClarifyAgentIntent(intent with { Reply = "Ngày AI diễn giải chưa khớp ngày theo múi giờ của bạn.", Options = [] },
            $"Theo múi giờ của bạn, {relative[0].Value} là {expected:dd/MM/yyyy}. Bạn xác nhận dùng ngày này nhé?");
    }

    private static string ClarificationReply(string reply)
    {
        // Keep the explanation, but never surface a model's claim that discarded actions were saved.
        var sentences = Regex.Split(reply, @"(?<=[.!?])\s+|[\r\n]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        var safe = sentences.Where(sentence => RequestMatches(MatchText(sentence),
            @"\b(?:da|vua)\b.*\b(?:luu|ghi nhan|tao|cap nhat|xep|ap dung|doi|them)\b|\b(?:toi|minh|he thong) se (?:luu|tao|xep|cap nhat|ap dung)\b|\b(?:saved|created|updated|applied|scheduled)\b").Count == 0);
        var explanation = string.Join(" ", safe).Trim();
        return explanation.Length > 0 ? explanation : "Mình cần làm rõ trước khi thay đổi lịch.";
    }
}
