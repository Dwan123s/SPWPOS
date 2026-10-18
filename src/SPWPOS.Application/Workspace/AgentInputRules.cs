using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SPWPOS.Application.Workspace;

public static class AgentInputRules
{
    public static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant().Replace('đ', 'd');

    public static (string Question, string[] Options)? AmbiguousClock(string request)
    {
        var text = Normalize(request);
        var match = Regex.Match(text, @"\b(?:luc|vao|truoc|tu)\s+(\d{1,2}|muoi hai|muoi mot|mot|hai|ba|bon|nam|sau|bay|tam|chin|muoi)\s*(?:gio\b|h(?=\d|\b)|:)\s*(\d{1,2})?",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success || Regex.IsMatch(text, @"\b(sang|chieu|toi|trua|dem|am|pm)\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return null;
        var words = new[] { "", "mot", "hai", "ba", "bon", "nam", "sau", "bay", "tam", "chin", "muoi", "muoi mot", "muoi hai" };
        var hour = int.TryParse(match.Groups[1].Value, out var parsed) ? parsed : Array.IndexOf(words, match.Groups[1].Value);
        if (hour is < 1 or > 12 || match.Groups[1].Value.StartsWith('0')) return null;
        var minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        if (text[(match.Index + match.Length)..].TrimStart().StartsWith("ruoi", StringComparison.Ordinal)) minute = 30;
        if (minute >= 60) return ("Bạn cho mình giờ hẹn chính xác, gồm giờ và phút nhé.", []);
        var morning = $"{hour % 12:00}:{minute:00} sáng";
        var evening = $"{hour % 12 + 12:00}:{minute:00} chiều/tối";
        return ($"Bạn muốn lúc {morning} hay {evening}?", [morning, evening]);
    }
}
