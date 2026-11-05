using System.Globalization;
using System.Text.Json;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class TaskInterpreter
{
    private sealed class NoteFieldException(string field) : Exception
    {
        public string Field { get; } = field;
    }
    private static readonly JsonElement NoteActivitySchema = JsonSerializer.Deserialize<JsonElement>("""
    {"type":"object","additionalProperties":false,"required":["activities","needsReview","messages"],"properties":{
      "needsReview":{"type":"boolean"},"messages":{"type":"array","items":{"type":"string"}},
      "activities":{"type":"array","items":{"type":"object","additionalProperties":false,
        "required":["kind","name","description","type","start","end","estimatedMinutes","deadline","priority","isHardDeadline","repeatWeeks","question","splittable"],
        "properties":{"kind":{"type":"string","enum":["EVENT","TASK"]},"name":{"type":"string"},"description":{"type":"string"},"type":{"type":"string","enum":["WORK","OTHER","PROGRAMMING","WRITING","STUDY","MEETING_PREP","ADMIN","CLASS","MEETING","APPOINTMENT"]},
          "start":{"type":["string","null"],"description":"ISO 8601 timestamp with timezone offset, or null"},"end":{"type":["string","null"],"description":"ISO 8601 timestamp with timezone offset, or null"},"estimatedMinutes":{"type":["integer","null"],"description":"Task duration in minutes when stated; null for events or unknown task durations"},
          "deadline":{"type":["string","null"],"description":"Explicit completion deadline with timezone offset, or null"},"priority":{"type":"integer","minimum":1,"maximum":4},"isHardDeadline":{"type":"boolean"},
          "repeatWeeks":{"type":"integer","minimum":1,"maximum":12},"question":{"type":"string"},"splittable":{"type":"boolean","description":"False for events and uninterrupted/continuous tasks; preparation and ordinary tasks may be split."}}}}}}
    """);

    private static AiTaskResult? ReadNoteActivities(JsonElement root, JsonElement items, bool needsReview, int limit, string? timeZoneId)
    {
        if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > limit) throw new NoteFieldException("activities");
        var messages = new List<string>();
        if (root.TryGetProperty("messages", out var notices) && notices.ValueKind != JsonValueKind.Null)
            messages.AddRange(notices.EnumerateArray().Select(x => x.GetString()!));
        if (messages.Count > 12 || messages.Any(x => x is null || x.Length > 1000)) throw new NoteFieldException("messages");
        static string Text(JsonElement item, string key)
        {
            if (!item.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) return "";
            if (value.ValueKind != JsonValueKind.String) throw new NoteFieldException("activities." + key);
            return value.GetString()!;
        }
        static int? Number(JsonElement item, string key)
        {
            if (!item.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) return null;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number)) throw new NoteFieldException("activities." + key);
            return number;
        }
        DateTimeOffset? Date(JsonElement item, string key)
        {
            var text = Text(item, key).Trim();
            if (text.Length == 0) return null;
            if (text.EndsWith('Z') || text.Length >= 6 && text[^3] == ':' && text[^6] is '+' or '-')
            {
                if (DateTimeOffset.TryParseExact(text, ["yyyy-MM-dd'T'HH:mmzzz", "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
                    "yyyy-MM-dd'T'HH:mm'Z'", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var offset)) return offset;
            }
            else if (timeZoneId is not null && DateTime.TryParseExact(text,
                ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                // A model may omit the offset. Interpret a complete local timestamp only in
                // the supplied account timezone, never in the API host's local timezone.
                var zone = PlanningEngine.Zone(timeZoneId);
                if (!zone.IsInvalidTime(local) && !zone.IsAmbiguousTime(local))
                    return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
            }
            // Event times are mandatory in review. A task's time fields are optional,
            // so dropping an invalid value there could silently remove a constraint.
            if (Text(item, "kind").Trim().Equals("TASK", StringComparison.OrdinalIgnoreCase))
                throw new NoteFieldException("activities." + key);
            needsReview = true;
            var message = $"Kiểm tra lại ngày giờ của “{Text(item, "name")}”.";
            if (!messages.Contains(message)) messages.Add(message);
            return null;
        }
        var activities = items.EnumerateArray().Select(x =>
        {
            var kind = Text(x, "kind").Trim().ToUpperInvariant();
            // Event duration comes from start/end. A task estimate is irrelevant and must
            // never reject the entire note (providers may emit 0 for this unused field).
            var duration = kind == "EVENT" ? null : Number(x, "estimatedMinutes");
            if (duration is < 5 or > 43200) { duration = null; needsReview = true; }
            var repeat = Number(x, "repeatWeeks") ?? 1;
            return new NoteActivity(kind, Text(x, "name"), Text(x, "description"), Text(x, "type").Trim().ToUpperInvariant(),
                Date(x, "start"), Date(x, "end"), duration, Date(x, "deadline"), Number(x, "priority") ?? 2,
                x.TryGetProperty("isHardDeadline", out var hard) && hard.ValueKind != JsonValueKind.Null && hard.GetBoolean(),
                repeat == 0 ? 1 : repeat, Text(x, "question"), kind == "TASK" &&
                    (!x.TryGetProperty("splittable", out var split) || split.ValueKind == JsonValueKind.Null || split.GetBoolean()));
        }).ToArray();
        foreach (var activity in activities)
        {
            var field = activity.Kind is not ("EVENT" or "TASK") ? "kind"
                : string.IsNullOrWhiteSpace(activity.Name) || activity.Name.Length > 200 ? "name"
                : activity.Description.Length > 4000 ? "description"
                : activity.Type.Length > 60 || activity.Kind == "EVENT" && activity.Type is not ("CLASS" or "WORK" or "MEETING" or "APPOINTMENT" or "OTHER") ? "type"
                : activity.Question.Length > 500 ? "question"
                : activity.Priority is < 1 or > 4 ? "priority"
                : activity.RepeatWeeks is < 1 or > 12 ? "repeatWeeks" : null;
            if (field is not null) throw new NoteFieldException("activities." + field);
        }
        return new([], needsReview, activities, messages.ToArray());
    }
}
