using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SPWPOS.Domain.Entities;

public static class ActivityContinuity
{
    private static readonly Regex ContinuousName = new(
        @"^(?:(?:tham gia|tham du|du|di|attend|join)\s+)?(?:" +
        @"(?:cuoc\s+)?hop\b|(?:team\s+)?meeting\b(?!\s+(?:prep(?:aration)?|notes|minutes|summary|report|agenda)\b)|" +
        @"(?:lop|tiet)\s+hoc\b|(?:cuoc\s+)?hen\b|appointment\b|" +
        @"(?:lich\s+)?kham\s+(?:benh|suc khoe)\b|phong van\b|interview\b(?!\s+(?:prep(?:aration)?|practice)\b)|" +
        @"ca\s+lam(?:\s+viec)?\b|work shift\b|ky thi\b|thi\s+(?:mon|cuoi ky|giua ky|dau vao|tuyen|chung chi|tot nghiep)\b|exam\b(?!\s+(?:prep(?:aration)?|practice|revision)\b))",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    // Fixed events already have one exact start/end. This also protects actual
    // meetings/appointments accidentally imported as tasks, including legacy data.
    // Preparation work has separate names/categories and remains splittable.
    public static bool RequiresContinuousSession(string name, string? type = null)
    {
        if (type?.Trim().ToUpperInvariant() is "MEETING" or "CLASS" or "APPOINTMENT" or "INTERVIEW" or "EXAM" or "WORK_SHIFT")
            return true;
        if ((name ?? "").TrimStart().StartsWith("hợp ", StringComparison.OrdinalIgnoreCase)) return false;
        var normalized = string.Concat((name ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
            .Normalize(NormalizationForm.FormC).Replace('đ', 'd');
        return ContinuousName.IsMatch(normalized);
    }

    public static bool CanSplit(string name, string? type, bool requested) =>
        requested && !RequiresContinuousSession(name, type);
}
