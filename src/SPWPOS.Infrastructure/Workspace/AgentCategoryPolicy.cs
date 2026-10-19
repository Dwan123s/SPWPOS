namespace SPWPOS.Infrastructure.Workspace;

// This recovery applies only to AI category labels; the calendar and task
// validators still enforce ownership, active types and scheduling constraints.
internal static class AgentCategoryPolicy
{
    internal static readonly string[] EventTypes = ["MEETING", "CLASS", "APPOINTMENT", "WORK", "OTHER"];

    internal static string Normalize(string? category, string[] allowed, bool editing) =>
        string.IsNullOrWhiteSpace(category) && editing ? "" :
        allowed.Contains(category, StringComparer.Ordinal) ? category! : "OTHER";
}
