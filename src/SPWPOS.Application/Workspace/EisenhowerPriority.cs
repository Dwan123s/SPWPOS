namespace SPWPOS.Application.Workspace;

public static class EisenhowerPriority
{
    public static string Quadrant(string importance, string urgency) =>
        importance is not ("Important" or "Lower") || urgency is not ("Urgent" or "NotUrgent") ? "Unknown" :
        importance == "Important" ? urgency == "Urgent" ? "Q1" : "Q2" : urgency == "Urgent" ? "Q3" : "Q4";

    public static int Rank(string quadrant) => quadrant switch { "Q1" => 0, "Q2" => 1, "Q3" => 2, "Q4" => 3, _ => 4 };

    public static string Urgency(DateTimeOffset? deadline, DateTimeOffset now) => deadline is null ? "Unknown"
        : deadline <= now.AddHours(24) ? "Urgent" : "NotUrgent";

    public static EisenhowerPriorityView Describe(string importance, DateTimeOffset? deadline, DateTimeOffset now)
    {
        var urgency = Urgency(deadline, now);
        var explanation = urgency == "Urgent" ? "Đã quá hạn hoặc còn không quá 24 giờ trước hạn."
            : urgency == "NotUrgent" ? "Còn hơn 24 giờ trước hạn chót."
            : "Chưa có hạn chót để đánh giá độ khẩn cấp.";
        if (importance == "Unknown") explanation += " Cần xác nhận mức quan trọng.";
        return new(importance, urgency, Quadrant(importance, urgency), explanation);
    }
}
