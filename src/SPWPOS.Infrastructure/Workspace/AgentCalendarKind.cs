using SPWPOS.Application.Workspace;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static string CalendarKindPrompt(AgentTurnInput input) => input.CalendarKind switch
    {
        "Event" => input.Text + "\n[User calendar choice: Event. Treat this request as a fixed event with explicit start/end, not a new task/work session. Preserve existing commitments; existing flexible work may only move under the ordinary collision/consent rules. Ask only for missing date/time facts.]",
        "WorkSession" => input.Text + "\n[User calendar choice: WorkSession. Treat the requested personal work as a task with work sessions, not a fixed event. An exact clock range is a work window, not permission to create an event. Use supplied duration/deadline; propose missing duration for confirmation. Do not ask about personal goals merely to classify importance; leave importance Unknown when evidence is missing.]",
        _ => input.Text
    };

    private static AgentIntent RespectCalendarKind(AgentIntent intent, string kind)
    {
        if (intent.Question.Length > 0) return intent;
        if (kind == "Event" && intent.Tasks.Length > 0)
            return ClarifyAgentIntent(intent with { Reply = "Bạn đang chọn Sự kiện cố định. Mình chưa lưu công việc hoặc phiên làm việc." },
                "Hãy ghi ngày, giờ bắt đầu và kết thúc của sự kiện; hoặc chuyển loại lịch sang Phiên công việc nếu bạn muốn NORA xếp thời gian làm task.");
        if (kind == "WorkSession" && intent.Events.Length > 0)
            return ClarifyAgentIntent(intent with { Reply = "Bạn đang chọn Phiên công việc. Mình chưa tạo sự kiện cố định." },
                "Bạn muốn làm công việc gì, trong bao lâu và hạn chót khi nào? Nếu đây là cuộc hẹn có giờ cố định, hãy đổi sang Sự kiện cố định.");
        return intent;
    }
}
