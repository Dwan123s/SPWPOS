using System.Text;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public async Task<string> ExportCalendarAsync(Guid uid, CancellationToken ct)
    {
        var now = Now; var rangeStart = now.AddDays(-30); var until = now.AddDays(366);
        var events = await Mine<FixedEvent>(uid).AsNoTracking().Where(x => x.End > rangeStart && x.Start < until).ToArrayAsync(ct);
        var sessions = await (from session in Mine<WorkSession>(uid).AsNoTracking()
            join task in Mine<WorkTask>(uid).AsNoTracking() on session.TaskId equals task.Id
            where !task.IsDeleted && session.End > rangeStart && session.Start < until && session.Status != WorkSessionStatus.Cancelled && session.Status != WorkSessionStatus.Skipped
            select new { session.Id, task.Name, session.Start, session.End }).ToArrayAsync(ct);
        static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n").Replace(";", "\\;").Replace(",", "\\,");
        static string Utc(DateTimeOffset at) => at.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var lines = new List<string> { "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//PLANORA//Personal Calendar//VI", "CALSCALE:GREGORIAN", "METHOD:PUBLISH" };
        void Event(Guid id, string kind, string name, DateTimeOffset start, DateTimeOffset end) {
            lines.AddRange(["BEGIN:VEVENT", $"UID:{kind}-{id:D}@planora", "DTSTAMP:" + Utc(now), "DTSTART:" + Utc(start), "DTEND:" + Utc(end),
                "SUMMARY:" + Escape(name), "END:VEVENT"]);
        }
        foreach (var item in events) Event(item.Id, "event", item.Name, item.Start, item.End);
        foreach (var item in sessions) Event(item.Id, "session", item.Name, item.Start, item.End);
        lines.Add("END:VCALENDAR");
        var output = new StringBuilder();
        foreach (var line in lines) {
            var count = 0;
            foreach (var rune in line.EnumerateRunes()) {
                if (count + rune.Utf8SequenceLength > 75) { output.Append("\r\n "); count = 1; }
                output.Append(rune.ToString()); count += rune.Utf8SequenceLength;
            }
            output.Append("\r\n");
        }
        return output.ToString();
    }
}
