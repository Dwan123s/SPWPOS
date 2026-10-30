using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SPWPOS.Application.Workspace;

public static class AgentInputRules
{
    public static string Normalize(string value) => string.Concat(VietnameseTelex.Normalize(value).Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant().Replace('đ', 'd');

    // Accent folding must not turn the pronoun tôi (including Telex tooi) into
    // the evening qualifier tối when validating an otherwise ambiguous clock.
    public static string NormalizeTimeText(string value) => Normalize(Regex.Replace(VietnameseTelex.Normalize(value),
        @"\btôi\b", "nguoi_nhap", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));

    public static (int StartMinute, int EndMinute)? ExplicitTimeRange(string request)
    {
        var text = NormalizeTimeText(request);
        var match = Regex.Match(text, @"\btu\s+(?<from>\d{1,2})\s*(?:h|gio|:)\s*(?<fm>\d{1,2})?\s*(?:den|[-–])\s*(?<to>\d{1,2})\s*(?:h|gio|:)\s*(?<tm>\d{1,2})?\s*(?:buoi\s+)?(?<period>sang|trua|chieu|toi|am|pm)\b",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success) return null;
        if (Regex.IsMatch(text.Remove(match.Index, match.Length), @"\b\d{1,2}\s*(?:h(?=\d|\b)|gio\b|:)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return null;
        int Part(string name) => match.Groups[name].Success ? int.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture) : 0;
        var from = Part("from"); var to = Part("to"); var fm = Part("fm"); var tm = Part("tm");
        if (from > 23 || to > 23 || fm > 59 || tm > 59) return null;
        if (match.Groups["period"].Value is "sang" or "am" && (from >= 12 || to > 12 || to == 12 && tm > 0)) return null;
        if (match.Groups["period"].Value == "am" && to == 12) return null;
        if (match.Groups["period"].Value is "chieu" or "toi" or "pm")
        { if (from < 12) from += 12; if (to < 12) to += 12; }
        // In a same-morning range such as 7h–12h sáng, the end is noon.
        // Overnight/reversed ranges remain ambiguous and are never repaired here.
        var start = from * 60 + fm; var end = to * 60 + tm;
        return start < end && end <= 1440 ? (start, end) : null;
    }

    public static (string Question, string[] Options)? AmbiguousClock(string request)
    {
        var text = NormalizeTimeText(request);
        var match = Regex.Match(text, @"\b(?:luc|vao|truoc|tu)\s+(\d{1,2}|muoi hai|muoi mot|mot|hai|ba|bon|nam|sau|bay|tam|chin|muoi)\s*(?:gio\b|h(?=\d|\b)|:)\s*(\d{1,2})?",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success || Regex.IsMatch(text, @"\b(sang|chieu|toi|trua|dem|am|pm)\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return null;
        var words = new[] { "", "mot", "hai", "ba", "bon", "nam", "sau", "bay", "tam", "chin", "muoi", "muoi mot", "muoi hai" };
        var hour = int.TryParse(match.Groups[1].Value, out var parsed) ? parsed : Array.IndexOf(words, match.Groups[1].Value);
        if (hour is < 1 or > 12 || match.Groups[1].Value.StartsWith('0') ||
            match.Value.Contains(':') && match.Groups[2].Value.Length == 2) return null;
        var minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        if (text[(match.Index + match.Length)..].TrimStart().StartsWith("ruoi", StringComparison.Ordinal)) minute = 30;
        if (minute >= 60) return ("Bạn cho mình giờ hẹn chính xác, gồm giờ và phút nhé.", []);
        var morning = $"{hour % 12:00}:{minute:00} sáng";
        var evening = $"{hour % 12 + 12:00}:{minute:00} chiều/tối";
        return ($"Bạn muốn lúc {morning} hay {evening}?", [morning, evening]);
    }
}
