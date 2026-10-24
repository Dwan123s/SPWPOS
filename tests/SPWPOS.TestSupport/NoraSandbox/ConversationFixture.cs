using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.TestSupport.NoraSandbox;

public static class ConversationFixture
{
    public static async Task<SandboxEnvironment> CreateAsync(ConversationPack pack, ConversationCase scenario,
        IConfiguration config, bool live, SandboxBudget budget, CancellationToken ct)
    {
        var env = await SandboxEnvironment.CreateAsync("B", config, live, budget, ct);
        try { await SeedAsync(env, pack.Fixture(scenario), scenario.Id, ct); return env; }
        catch { await env.DisposeAsync(); throw; }
    }
    private static string Text(JsonNode node, string key) => node[key]!.GetValue<string>();
    private static int Number(JsonNode node, string key, int fallback = 0) => node[key]?.GetValue<int>() ?? fallback;
    private static bool Flag(JsonNode node, string key, bool fallback = false) => node[key]?.GetValue<bool>() ?? fallback;
    private static DateTimeOffset At(JsonNode node, string key) => DateTimeOffset.Parse(Text(node, key), CultureInfo.InvariantCulture);
    private static int Minute(string text)
    {
        var parts = text.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var hours) || !int.TryParse(parts[1], out var minutes) ||
            hours is < 0 or > 24 || minutes is < 0 or > 59 || hours == 24 && minutes != 0) throw new InvalidDataException("Invalid clock range.");
        return hours * 60 + minutes;
    }
    private static Guid StableId(string caseId, string alias) => new(SHA256.HashData(Encoding.UTF8.GetBytes(caseId + ":" + alias))[..16]);

    private static async Task SeedAsync(SandboxEnvironment env, JsonObject fixture, string caseId, CancellationToken ct)
    {
        var now = At(fixture, "now"); env.Clock.Advance(now - env.Clock.GetUtcNow());
        await env.Db(async db => {
            var preference = await db.WorkPreferences.SingleAsync(x => x.UserId == env.UserId, ct);
            var settings = fixture["preferences"]!;
            preference.TimeZoneId = Text(fixture, "time_zone");
            preference.MaximumDailyMinutes = Number(settings, "maximum_daily_minutes");
            preference.MaximumSessionMinutes = Number(settings, "maximum_session_minutes");
            preference.MinimumSessionMinutes = Number(settings, "minimum_session_minutes");
            preference.BreakMinutes = Number(settings, "break_minutes");
            preference.PlanningHorizonDays = Number(settings, "planning_horizon_days");
            // Deterministic neutral preference: the pack only sets hard availability.
            preference.PreferredPeriod = "NONE"; preference.PreferredStartMinute = 0; preference.PreferredEndMinute = 1440;
            var active = fixture["active_task_categories"]!.AsArray().Select(x => x!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
            var definitions = await db.TaskTypes.ToArrayAsync(ct);
            if (active.Except(definitions.Select(x => x.Code), StringComparer.Ordinal).Any()) throw new InvalidDataException("Unseeded task category.");
            foreach (var type in definitions) type.IsActive = active.Contains(type.Code);
            db.WeeklyAvailability.RemoveRange(await db.WeeklyAvailability.Where(x => x.UserId == env.UserId).ToArrayAsync(ct));
            foreach (var slot in fixture["weekly_availability"]!.AsArray()) db.WeeklyAvailability.Add(new() {
                UserId = env.UserId, DayOfWeek = Number(slot!, "day_of_week"), StartMinute = Minute(Text(slot!, "start")), EndMinute = Minute(Text(slot!, "end"))
            });
            Guid Id(JsonNode row)
            {
                var alias = Text(row, "ref");
                if (!env.Ids.TryAdd(alias, StableId(caseId, alias))) throw new InvalidDataException("Duplicate fixture alias.");
                return env.Ids[alias];
            }
            void Task(JsonNode row, Guid owner)
            {
                db.WorkTasks.Add(new() { Id = Id(row), UserId = owner, Name = Text(row, "name"),
                    EstimatedMinutes = Number(row, "estimated_minutes"), Deadline = At(row, "deadline"),
                    IsHardDeadline = Flag(row, "is_hard_deadline", true), MinimumSessionMinutes = Number(row, "minimum_session_minutes", preference.MinimumSessionMinutes),
                    Priority = Number(row, "priority", 2), TaskType = Text(row, "category"), Splittable = Flag(row, "splittable", true),
                    Status = Enum.Parse<WorkTaskStatus>(Text(row, "status")), Progress = Number(row, "progress_percent"),
                    WorkWindowStart = row["work_window_start"] is null ? null : At(row, "work_window_start"),
                    WorkWindowEnd = row["work_window_end"] is null ? null : At(row, "work_window_end"),
                    CreatedAt = now, UpdatedAt = now });
            }
            void Event(JsonNode row) => db.FixedEvents.Add(new() { Id = Id(row), UserId = env.UserId, Name = Text(row, "name"),
                Start = At(row, "start"), End = At(row, "end"), EventType = Text(row, "category"), Description = row["description"]?.GetValue<string>() ?? "" });
            foreach (var row in fixture["tasks"]!.AsArray()) Task(row!, env.UserId);
            foreach (var account in fixture["external_accounts"]?.AsArray() ?? [])
            {
                var owner = StableId(caseId, Text(account!, "ref"));
                db.Users.Add(new() { Id = owner, UserName = "foreign-" + owner.ToString("N"), NormalizedUserName = "FOREIGN-" + owner.ToString("N"),
                    Email = "foreign@example.invalid", NormalizedEmail = "FOREIGN@EXAMPLE.INVALID", IsActive = true, EmailConfirmed = true });
                foreach (var row in account!["tasks"]!.AsArray()) Task(row!, owner);
            }
            foreach (var row in fixture["events"]!.AsArray()) Event(row!);
            foreach (var row in fixture["sessions"]!.AsArray()) db.WorkSessions.Add(new() {
                Id = Id(row!), UserId = env.UserId, TaskId = env.Ids[Text(row!, "task_ref")], Start = At(row!, "start"), End = At(row!, "end"),
                Status = Enum.Parse<WorkSessionStatus>(Text(row!, "status")), IsLocked = Flag(row!, "locked"),
                ActualSeconds = Number(row!, "actual_minutes") * 60L, CreatedAt = now, UpdatedAt = now
            });
            foreach (var row in fixture["exceptions"]!.AsArray()) db.AvailabilityExceptions.Add(new() {
                Id = Id(row!), UserId = env.UserId, Date = DateOnly.Parse(Text(row!, "date"), CultureInfo.InvariantCulture),
                StartMinute = Minute(Text(row!, "start")), EndMinute = Minute(Text(row!, "end")), IsAvailable = Flag(row!, "available"),
                OverridesWeeklyAvailability = Flag(row!, "overrides_weekly_availability"), Note = row!["note"]?.GetValue<string>() ?? ""
            });
            foreach (var row in fixture["dependencies"]!.AsArray()) db.TaskDependencies.Add(new() {
                UserId = env.UserId, TaskId = env.Ids[Text(row!, "task_ref")], PredecessorId = env.Ids[Text(row!, "predecessor_ref")], CreatedAt = now
            });
            if (fixture["generated_seed"] is { } generated)
            {
                if (generated["tasks"] is { } tasks)
                    for (var i = 1; i <= Number(tasks, "count"); i++)
                    {
                        var row = (JsonObject)tasks.DeepClone(); row["ref"] = "generated_task_" + i.ToString("000", CultureInfo.InvariantCulture);
                        row["name"] = Text(tasks, "name_template").Replace("{index:03}", i.ToString("000", CultureInfo.InvariantCulture), StringComparison.Ordinal);
                        Task(row, env.UserId);
                    }
                if (generated["events"] is { } events)
                    for (var i = 1; i <= Number(events, "count"); i++)
                    {
                        var start = At(events, "first_start").AddMinutes((i - 1) * Number(events, "every_minutes"));
                        Event(new JsonObject { ["ref"] = "generated_event_" + i.ToString("000", CultureInfo.InvariantCulture),
                            ["name"] = Text(events, "name_template").Replace("{index:03}", i.ToString("000", CultureInfo.InvariantCulture), StringComparison.Ordinal),
                            ["start"] = start.ToString("O"), ["end"] = start.AddMinutes(Number(events, "duration_minutes")).ToString("O"), ["category"] = Text(events, "category") });
                    }
            }
            if (fixture["history"]!.AsArray() is { Count: > 0 } history)
            {
                db.AgentConversations.Add(new() { Id = env.ConversationId, UserId = env.UserId, Title = caseId, CreatedAt = now.AddMinutes(-history.Count), UpdatedAt = now });
                for (var i = 0; i < history.Count; i++)
                {
                    var row = history[i]!;
                    var turn = new AgentTurn { Id = StableId(caseId, "history-" + i), ConversationId = env.ConversationId, UserId = env.UserId,
                        RequestId = Guid.NewGuid(), Text = Text(row, "text"), Status = Text(row, "status"), CreatedAt = now.AddMinutes(i - history.Count), UpdatedAt = now };
                    turn.ResultJson = JsonSerializer.Serialize(new AgentTurnView(turn.Id, env.ConversationId, turn.RequestId, turn.Text,
                        Text(row, "reply"), turn.Status, "", null, [], [], turn.CreatedAt), SandboxJson.Options);
                    db.AgentTurns.Add(turn);
                }
            }
            await db.SaveChangesAsync(ct);
        }, ct);
    }
}
