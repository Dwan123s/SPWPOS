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
        new("get_tasks", "Find the account's active tasks by name/ID or list active work. Resolve duplicate names; use real IDs before editing.", Object(("query", String())), "Đọc công việc"),
        new("get_calendar", "Read saved calendar items between explicit-offset start and end; includes fixed commitments and locked sessions.", Object(("start", String()), ("end", String())), "Đọc lịch đã lưu"),
        new("get_preferences", "Read timezone, available hours, breaks and daily/session limits. This tool cannot change settings.", Object(), "Kiểm tra giờ rảnh và giới hạn"),
        new("find_free_slots", "Compute calendar gaps in the requested range. Gaps alone do not guarantee a task fits dependencies, breaks or capacity.", Object(("start", String()), ("end", String())), "Tìm khoảng trống"),
        new("analyze_workload", "Compute actual workload and capacity for the requested dates. Does not change the calendar.", Object(("start", String()), ("end", String())), "Phân tích khả năng thực hiện"),
        new("prepare_changes", "Prepare a complete grounded planning intent as a run-local draft. All fields are required by schema, empty actions use empty arrays. No business data is saved.", Object(("intent", TaskInterpreter.AgentSchema(codes))), "Chuẩn bị bản nháp"),
        new("simulate_plan", "Try a prepared draft. keep_existing preserves unrelated sessions; replan_flexible is only allowed when the user explicitly permits rearranging flexible work. Returns candidate ID and real shortages. No tasks/events/sessions are saved.",
            Object(("draftId", String()), ("strategy", new { type = "string", @enum = new[] { "keep_existing", "replan_flexible" } })), "Thử phương án lịch"),
        new("evaluate_candidate", "Inspect a server-issued candidate ID, feasibility, changed times and warnings before selecting it.", Object(("candidateId", String())), "Kiểm tra phương án"),
        new("finish_proposal", "Select a feasible server-issued candidate, with a brief Vietnamese explanation. This only returns a proposal, never claims a save.", Object(("candidateId", String()), ("reply", String())), "Hoàn tất đề xuất"),
        new("finish_answer", "Finish a read-only answer grounded in observed tool data; cannot save or move any calendar item.", Object(("reply", String())), "Trả kết quả đã đối chiếu"),
        new("ask_user", "Ask one concise question when essential facts/permission are missing. Do not change any business data.", Object(("reply", String()), ("question", String()),
            ("options", new { type = "array", items = new { type = "string" } })), "Cần bạn bổ sung thông tin")
    ];
}
