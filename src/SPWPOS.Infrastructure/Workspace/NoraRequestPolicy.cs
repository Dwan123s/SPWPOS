using System.Text.Json;
using System.Text.Json.Nodes;
using SPWPOS.Application.Workspace;

namespace SPWPOS.Infrastructure.Workspace;

// Public interpretation of intent, never private reasoning or authorization.
// Calendar mutations still pass the existing evidence, ownership and revision checks.
internal static class NoraRequestPolicy
{
    internal static readonly string[] Intents = ["Conversation", "Advice", "Clarification", "CalendarQuery", "FreeTimeQuery", "TaskQuery", "Analysis", "Mutation"];
    private static readonly HashSet<string> FirstTurnTools = ["reply_to_user", "ask_user", "answer_calendar", "answer_free_time", "get_tasks", "get_calendar", "get_preferences", "analyze_workload", "propose_changes"];
    internal static NoraToolDefinition[] OfferedTools(NoraModelRequest request) => request.RequireUnderstanding
        ? request.Tools.Where(t => FirstTurnTools.Contains(t.Name)).ToArray() : request.Tools;
    internal static void Validate(NoraRequestUnderstanding value)
    {
        if (!Intents.Contains(value.Intent) || string.IsNullOrWhiteSpace(value.Summary) || value.Summary.Length > 300 ||
            value.MissingDetails is null || value.MissingDetails.Length > 4 ||
            value.MissingDetails.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 200))
            throw new JsonException("Invalid request understanding");
    }

    internal static JsonElement Parameters(NoraToolDefinition tool, bool requireUnderstanding)
    {
        if (!requireUnderstanding) return tool.Parameters;
        var schema = JsonNode.Parse(tool.Parameters.GetRawText())!.AsObject();
        var understanding = JsonSerializer.SerializeToNode(new {
            type = "object", additionalProperties = false,
            required = new[] { "intent", "summary", "needsPersonalData", "missingDetails" },
            properties = new {
                intent = new { type = "string", @enum = Intents },
                summary = new { type = "string", description = "One short Vietnamese sentence stating what the user wants, including referenced work/date if known. No reasoning." },
                needsPersonalData = new { type = "boolean", description = "True only when this request needs live saved account data." },
                missingDetails = new { type = "array", items = new { type = "string" }, description = "Only essential unresolved facts. Empty when clear. Never ask for facts already in history." }
            }
        });
        schema["properties"]!["requestUnderstanding"] = understanding;
        schema["required"]!.AsArray().Add("requestUnderstanding");
        return JsonSerializer.SerializeToElement(schema);
    }

    internal static bool Allows(NoraRequestUnderstanding value, string tool)
    {
        if (value.MissingDetails.Length > 0 || value.Intent == "Clarification") return tool == "ask_user";
        if (tool == "ask_user") return true;
        return value.Intent switch {
            "Conversation" when !value.NeedsPersonalData => tool == "reply_to_user",
            "Advice" when !value.NeedsPersonalData => tool == "reply_to_user",
            "Advice" or "Analysis" => tool is "get_tasks" or "get_calendar" or "get_preferences" or "find_free_slots" or "analyze_workload" or "finish_answer",
            "CalendarQuery" => tool is "answer_calendar" or "get_calendar" or "finish_answer",
            "FreeTimeQuery" => tool is "answer_free_time" or "find_free_slots" or "get_calendar" or "finish_answer",
            "TaskQuery" => tool is "get_tasks" or "finish_answer",
            "Mutation" => tool is not "reply_to_user" and not "finish_answer",
            _ => false
        };
    }
}
