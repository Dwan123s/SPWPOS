namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static bool IsTimetableRequest(string text) => MatchText(text).Contains("thoi khoa bieu", StringComparison.Ordinal);

    private static string[] TimetableCutoffNotices(int skipped, int clipped)
    {
        if (skipped == 0 && clipped == 0) return [];
        var parts = new List<string>();
        if (skipped > 0) parts.Add($"Mình bỏ qua {skipped} buổi học đã kết thúc");
        if (clipped > 0) parts.Add($"{(parts.Count > 0 ? "với" : "Với")} {clipped} buổi đang diễn ra, chỉ giữ phần thời gian còn lại");
        return [string.Join("; ", parts) + ". Các buổi sau vẫn theo ngày và giờ của thời khóa biểu."];
    }
}
