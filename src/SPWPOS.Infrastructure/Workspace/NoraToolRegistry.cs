using System.Text.Json;
using SPWPOS.Application.Workspace;

namespace SPWPOS.Infrastructure.Workspace;

public static class NoraToolRegistry
{
    private static JsonElement Object(params (string Name, object Schema)[] properties) => JsonSerializer.SerializeToElement(new {
        type = "object", additionalProperties = false, required = properties.Select(x => x.Name).ToArray(),
        properties = properties.ToDictionary(x => x.Name, x => x.Schema)
    });
    private static object String(bool nullable = false) => new { type = nullable ? new[] { "string", "null" } : ["string"] };
    internal static NoraToolDefinition[] Create(string[] codes) => [
        new("reply_to_user", "Respond naturally in Vietnamese to conversation, encouragement, planning advice or explanations that do not require saved personal data. This is a direct response, not a calendar tool. Never claim to have read or changed saved tasks/calendar. Use ask_user for essential missing facts, answer_calendar/answer_free_time for live data, propose_changes for requested actions.", Object(("reply", String())), "Trò chuyện với bạn"),
        new("get_tasks", "Find the account's active tasks by name/ID or list active work. Resolve duplicate names; use real IDs before editing.", Object(("query", String())), "Đọc công việc"),
        new("manage_reminder", "Prepare a personal reminder, list reminders, or cancel a specific reminder. Call alone. Independent of tasks, deadlines, meetings and scheduling. Create requires message and future at with timezone offset; relative delays are grounded by server from user request. Ask when time/content missing. Cancel accepts reminderId or identifying message; ambiguous matches need clarification. List uses message='' and at/reminderId=null. No save until server validates and applies.",
            Object(("operation",new {type="string",@enum=new[]{"Create","List","Cancel"}}),("message",String()),("at",String(true)),("reminderId",String(true))), "Quản lý lời nhắc"),
        new("get_calendar", "Read saved calendar items between explicit-offset start and end; includes fixed commitments and locked sessions.", Object(("start", String()), ("end", String())), "Đọc lịch đã lưu"),
        new("get_preferences", "Read timezone, available hours, breaks and daily/session limits. This tool cannot change settings.", Object(), "Kiểm tra giờ rảnh và giới hạn"),
        new("find_free_slots", "Compute calendar gaps in the requested range. Gaps alone do not guarantee a task fits dependencies, breaks or capacity.", Object(("start", String()), ("end", String())), "Tìm khoảng trống"),
        new("answer_calendar", "Preferred for a read-only request to see the saved calendar: load the complete calendar and return the server-verified answer in ONE call. Call alone. Use explicit-offset range. Saves nothing.", Object(("start", String()), ("end", String())), "Đọc và trả lịch đã xác minh"),
        new("answer_free_time", "Preferred for a read-only free-time question: calculate and return verified calendar gaps in ONE call. Call alone. Use explicit-offset range. Saves nothing; gaps do not guarantee task feasibility.", Object(("start", String()), ("end", String())), "Tính và trả giờ rảnh"),
        new("analyze_workload", "Compute actual workload and capacity for the requested dates. Does not change the calendar.", Object(("start", String()), ("end", String())), "Phân tích khả năng thực hiện"),
        new("prepare_changes", "Prepare a complete grounded planning intent as a run-local draft. All fields are required by schema, empty actions use empty arrays. No business data is saved.", Object(("intent", TaskInterpreter.AgentSchema(codes))), "Chuẩn bị bản nháp"),
        new("propose_changes", "Preferred for complete new tasks/events/timetables: prepare intent, load the real calendar, validate and simulate, then finish the proposal if feasible in one call. Call alone. No business data is saved. If infeasible returns real blockers for correction. Never invent facts; ask_user for missing essential data.",
            Object(("intent", TaskInterpreter.AgentSchema(codes)), ("strategy", new { type = "string", @enum = new[] { "keep_existing", "replan_flexible" } })), "Kiểm tra và hoàn tất đề xuất"),
        new("simulate_plan", "Try a prepared draft. keep_existing preserves unrelated sessions except a bounded confirmation-only rescue for important work due within 48 hours when short of time: only flexible sessions with later deadlines can move, preserving all deadlines. replan_flexible is only allowed when the user explicitly permits broader rearrangement. Returns candidate ID and real shortages. No tasks/events/sessions are saved.",
            Object(("draftId", String()), ("strategy", new { type = "string", @enum = new[] { "keep_existing", "replan_flexible" } })), "Thử phương án lịch"),
        new("evaluate_candidate", "Inspect a server-issued candidate ID, feasibility, changed times and warnings before selecting it.", Object(("candidateId", String())), "Kiểm tra phương án"),
        new("finish_proposal", "Select a feasible server-issued candidate, with a brief Vietnamese explanation. This only returns a proposal, never claims a save.", Object(("candidateId", String()), ("reply", String())), "Hoàn tất đề xuất"),
        new("finish_answer", "Finish a read-only answer grounded in observed tool data; cannot save or move any calendar item.", Object(("reply", String())), "Trả kết quả đã đối chiếu"),
        new("ask_user", "Ask one concise question when essential facts/permission are missing. Do not change any business data.", Object(("reply", String()), ("question", String()),
            ("options", new { type = "array", items = new { type = "string" } })), "Cần bạn bổ sung thông tin")
    ];
}
