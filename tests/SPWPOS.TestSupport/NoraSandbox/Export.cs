using System.Globalization;
using System.Text;
using System.Text.Json;
namespace SPWPOS.TestSupport.NoraSandbox;

public static class SandboxExport
{
    private static string Text(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal);
    private static string Csv(string value)
    {
        // Keep untrusted model text inert when the CSV is opened in spreadsheet software.
        var leading = value.TrimStart(' ', '\t', '\r', '\n');
        if (leading.Length > 0 && leading[0] is '=' or '+' or '-' or '@') value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
    private static string ChangedValues(SandboxChange change)
    {
        var fields = new[] { "name", "estimatedMinutes", "deadline", "priority", "taskType", "splittable", "status", "isDeleted", "start", "end", "dayOfWeek", "date", "startMinute", "endMinute", "isAvailable", "note", "revision", "isApplied" };
        var pieces = new List<string>();
        foreach (var field in fields)
        {
            JsonElement before = default, after = default;
            var hasBefore = change.Before is { ValueKind: JsonValueKind.Object } old && old.TryGetProperty(field, out before);
            var hasAfter = change.After is { ValueKind: JsonValueKind.Object } current && current.TryGetProperty(field, out after);
            if (hasBefore && hasAfter && before.GetRawText() == after.GetRawText()) continue;
            if (hasBefore || hasAfter) pieces.Add(field + ": " + (hasBefore ? before.ToString() : "∅") + " → " + (hasAfter ? after.ToString() : "∅"));
        }
        return Text(pieces.Count > 0 ? string.Join("; ", pieces) : "Metadata/derived fields; see JSON before/after.");
    }
    private static async Task WriteFile(string path, string content, CancellationToken ct)
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), ct);
        File.Move(temporary, path, overwrite: true);
    }
    public static async Task WriteAsync(SandboxRun report, string directory, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        await WriteFile(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(report, SandboxJson.Options), ct);
        var md = new StringBuilder("# NORA sandbox results\n\n");
        md.AppendLine($"Mode: **{report.Mode}** · provider: {Text(report.Provider)} · model: {Text(report.Model ?? "none (scripted)")}");
        md.AppendLine($"\nCommit: `{report.GitCommit ?? "unavailable"}` · dirty: {report.DirtyWorkingTree} · interpreter attempts: {report.CallsUsed}/{report.CallLimit}");
        md.AppendLine("\nAutomatic action checks and human response review are separate. PASS does not certify live response semantics.\n");
        foreach (var group in report.Cases.GroupBy(x => x.Status)) md.AppendLine($"- {group.Key}: {group.Count()}");
        md.AppendLine("\n| Case | Repeat | Automatic checks | Response review | Steps |\n|---|---:|---|---|---:|");
        foreach (var item in report.Cases) md.AppendLine($"| {item.Id} | {item.Repeat} | {item.Status} | {item.ResponseReview} | {item.Turns.Length} |");
        var csv = new StringBuilder("caseId,repeat,fixture,status,responseReview,step,operation,resultStatus,errorCode,elapsedMs,actualChanges,failedChecks,prompt,reply,verifiedOutcome,question\n");
        foreach (var item in report.Cases)
        {
            md.AppendLine($"\n## {item.Id} — {Text(item.Title)} (repeat {item.Repeat})\n\nFixture: {item.Fixture} · result: **{item.Status}**");
            if (item.Reason is not null) md.AppendLine("\n" + Text(item.Reason));
            for (var i = 0; i < item.Turns.Length; i++)
            {
                var turn = item.Turns[i];
                md.AppendLine($"\n### Step {i + 1}: {turn.Operation}\n");
                md.AppendLine("Prompt:\n");
                foreach (var line in turn.Prompt.Split('\n')) md.AppendLine("> " + Text(line));
                md.AppendLine($"\nStatus: **{(turn.ErrorCode is not null ? "REJECTED / ERROR" : turn.Result?.Status ?? "no result")}** · error code: {turn.ErrorCode ?? "none"} · {turn.ElapsedMilliseconds} ms · persisted changes: {turn.Actions.Length}");
                if (turn.ErrorCode is not null && turn.PersistedTurn is not null) md.AppendLine("\nRetained stored turn status: " + turn.PersistedTurn.Status + " (not a new successful operation).");
                md.AppendLine("\nReply:\n");
                foreach (var line in (turn.ErrorMessage ?? turn.Result?.Reply ?? "No public reply returned.").Split('\n')) md.AppendLine("> " + Text(line));
                if (!string.IsNullOrWhiteSpace(turn.Result?.Outcome))
                {
                    md.AppendLine("\nVerified outcome:\n");
                    foreach (var line in turn.Result.Outcome.Split('\n')) md.AppendLine("> " + Text(line));
                }
                if (!string.IsNullOrWhiteSpace(turn.Result?.Question)) md.AppendLine("\nQuestion: " + Text(turn.Result.Question));
                md.AppendLine("\nChecks:\n");
                foreach (var check in turn.Checks) md.AppendLine($"- {(check.Passed ? "PASS" : "FAIL")}{(check.Critical ? " [invariant]" : "")} `{check.Name}`: {Text(check.Detail)}");
                if (turn.Actions.Length > 0)
                {
                    md.AppendLine("\nActual database actions:\n\n| Entity | ID | Operation | Changed values |\n|---|---|---|---|");
                    foreach (var change in turn.Actions) md.AppendLine($"| {change.Entity} | `{change.Id}` | {change.Operation} | {ChangedValues(change)} |");
                }
                var zone = TimeZoneInfo.FindSystemTimeZoneById(turn.After.Preference.TimeZoneId);
                md.AppendLine("\nSaved work sessions (account local time):\n\n| Task | Start | End | Minutes | Status | Locked |\n|---|---|---|---:|---|---|");
                foreach (var session in turn.After.Sessions.OrderBy(x => x.Start))
                    md.AppendLine($"| {Text(turn.After.Tasks.Single(x => x.Id == session.TaskId).Name)} | {TimeZoneInfo.ConvertTime(session.Start, zone):yyyy-MM-dd HH:mm zzz} | {TimeZoneInfo.ConvertTime(session.End, zone):yyyy-MM-dd HH:mm zzz} | {(session.End-session.Start).TotalMinutes} | {session.Status} | {session.IsLocked} |");
                md.AppendLine("\nSaved events (account local time):\n\n| Event | Start | End |\n|---|---|---|");
                foreach (var meeting in turn.After.Events.OrderBy(x => x.Start))
                    md.AppendLine($"| {Text(meeting.Name)} | {TimeZoneInfo.ConvertTime(meeting.Start, zone):yyyy-MM-dd HH:mm zzz} | {TimeZoneInfo.ConvertTime(meeting.End, zone):yyyy-MM-dd HH:mm zzz} |");
                var row = new[] { item.Id, item.Repeat.ToString(CultureInfo.InvariantCulture), item.Fixture, item.Status, item.ResponseReview, (i+1).ToString(CultureInfo.InvariantCulture),
                    turn.Operation, turn.ErrorCode is not null ? "REJECTED / ERROR" : turn.Result?.Status ?? "", turn.ErrorCode ?? "", turn.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture), turn.Actions.Length.ToString(CultureInfo.InvariantCulture),
                    string.Join("; ", turn.Checks.Where(x => !x.Passed).Select(x => x.Name)), turn.Prompt, turn.ErrorMessage ?? turn.Result?.Reply ?? "", turn.Result?.Outcome ?? "", turn.Result?.Question ?? "" };
                csv.AppendLine(string.Join(',', row.Select(Csv)));
            }
        }
        md.AppendLine("\n## Coverage\n\n| Scenario | Coverage |\n|---|---|");
        foreach (var entry in report.Coverage) md.AppendLine($"| {entry.Key} | {entry.Value} |");
        md.AppendLine("\n## Limitations\n"); foreach (var limitation in report.Limitations) md.AppendLine("- " + Text(limitation));
        md.AppendLine("\n`results.json` includes structured model intents, bounded context/history, sanitized stored commands, complete synthetic before/after snapshots, and actual field values for each action. CSV has one row per executed step. No private model reasoning is requested or exported.");
        await WriteFile(Path.Combine(directory, "report.md"), md.ToString(), ct);
        await WriteFile(Path.Combine(directory, "turns.csv"), csv.ToString(), ct);
    }
}
