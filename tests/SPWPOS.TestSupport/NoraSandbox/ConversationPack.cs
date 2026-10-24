using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace SPWPOS.TestSupport.NoraSandbox;

public sealed record ConversationTurn(string Type, string? Text, string? Action, JsonObject? Params, ConversationExpectation Expected);
public sealed record ConversationExpectation(string Outcome, string[] Checks);
public sealed record ConversationCase(string Id, string CoverageKey, string Title, string VoiceProfile, string Fixture,
    string Execution, bool IntentionalRepetition, JsonObject? FixtureOverrides, ConversationTurn[] Turns);
public sealed record ConversationGroup(string Id, string Title, ConversationCase[] Cases);

// This schema is deliberately separate from the legacy scripted catalog.
public sealed class ConversationPack
{
    private static readonly JsonSerializerOptions Json = new(SandboxJson.Options) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    public JsonObject Source { get; }
    public ConversationGroup[] Groups { get; }
    public ConversationCase[] Cases => Groups.SelectMany(x => x.Cases).ToArray();
    private ConversationPack(JsonObject source)
    {
        Source = source;
        Groups = source["groups"]?.Deserialize<ConversationGroup[]>(Json) ?? throw new InvalidDataException("Missing conversation groups.");
        Validate();
    }
    public static ConversationPack Load(string root) => Parse(File.ReadAllText(Path.Combine(root, "tests", "nora-sandbox", "planning-conversations.vi.json")));
    public static ConversationPack Parse(string json) => new(JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("Empty conversation pack."));

    public ConversationCase[] Select(SandboxOptions options)
    {
        var groupIds = options.GroupIds ?? []; var caseIds = options.CaseIds ?? [];
        if (groupIds.Except(Groups.Select(x => x.Id), StringComparer.Ordinal).Any()) throw new ArgumentException("Unknown conversation group ID.");
        if (caseIds.Except(Cases.Select(x => x.Id), StringComparer.Ordinal).Any()) throw new ArgumentException("Unknown conversation case ID.");
        var smoke = new[] { "G01-01", "G03-01", "G10-01", "G21-01", "G23-01" };
        var selected = Groups.Where(g => groupIds.Length == 0 || groupIds.Contains(g.Id))
            .SelectMany(g => g.Cases).Where(c => caseIds.Length > 0 ? caseIds.Contains(c.Id)
                : groupIds.Length > 0 || options.Suite == "All" || smoke.Contains(c.Id)).ToArray();
        if (selected.Length == 0) throw new ArgumentException("Conversation selection is empty; group and case filters intersect.");
        return selected;
    }

    public JsonObject Fixture(ConversationCase scenario) => Merge(Resolve(scenario.Fixture, []), scenario.FixtureOverrides ?? new());

    private JsonObject Resolve(string id, HashSet<string> chain)
    {
        if (!chain.Add(id)) throw new InvalidDataException("Cyclic fixture inheritance: " + id);
        var fixture = Source["fixtures"]?[id] as JsonObject ?? throw new InvalidDataException("Unknown fixture: " + id);
        return Merge(fixture["extends"] is JsonValue parent ? Resolve(parent.GetValue<string>(), chain) : new(), fixture);
    }
    public static JsonObject Merge(JsonObject original, JsonObject patch)
    {
        var result = (JsonObject)original.DeepClone();
        foreach (var (key, value) in patch)
            result[key] = value is JsonObject child && result[key] is JsonObject prior ? Merge(prior, child) : value?.DeepClone();
        return result;
    }
    private void Validate()
    {
        void Require(bool condition, string error) { if (!condition) throw new InvalidDataException(error); }
        Require(Source["schema_version"]?.GetValue<string>() == "1.0.0", "Unsupported conversation schema.");
        var cases = Cases;
        Require(Groups.Length > 0 && cases.Length > 0, "Empty groups/cases.");
        Require(Groups.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == Groups.Length, "Duplicate group IDs.");
        Require(cases.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == cases.Length, "Duplicate case IDs.");
        Require(cases.Select(x => x.CoverageKey).Distinct(StringComparer.Ordinal).Count() == cases.Length, "Duplicate coverage keys.");
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in Groups)
        foreach (var c in group.Cases)
        {
            Require(c.Id.StartsWith(group.Id + "-", StringComparison.Ordinal), "Case belongs to another group: " + c.Id);
            Require(c.CoverageKey.Length > 0 && c.Title.Length > 0 && c.Turns.Length > 0, "Incomplete case: " + c.Id);
            Require(Source["voice_profiles"]?[c.VoiceProfile] is JsonObject, "Unknown voice: " + c.Id);
            Require(!c.IntentionalRepetition || group.Title == "Repetition Request", "Repetition must be explicitly grouped: " + c.Id);
            var fixture = Fixture(c);
            Require(DateTimeOffset.TryParse(fixture["now"]?.GetValue<string>(), out _), "Invalid clock: " + c.Id);
            _ = TimeZoneInfo.FindSystemTimeZoneById(fixture["time_zone"]!.GetValue<string>());
            Require((fixture["sessions"]?.AsArray() ?? []).Count(s => s?["status"]?.GetValue<string>() is "Running" or "Paused") <= 1,
                "Only one Running/Paused session per account is valid: " + c.Id);
            var refs = new[] { "tasks", "events", "sessions", "exceptions" }.SelectMany(key => (fixture[key]?.AsArray() ?? []).Select(x => x!["ref"]!.GetValue<string>())).ToArray();
            Require(refs.Distinct(StringComparer.Ordinal).Count() == refs.Length, "Duplicate fixture refs: " + c.Id);
            var foreign = (fixture["external_accounts"]?.AsArray() ?? []).SelectMany(a => a!["tasks"]!.AsArray().Select(t => t!["ref"]!.GetValue<string>()));
            var allRefs = refs.Concat(foreign).ToHashSet(StringComparer.Ordinal);
            foreach (var turn in c.Turns)
            {
                Require(turn.Expected is not null && turn.Expected.Checks is { Length: > 0 }, "Missing review criteria: " + c.Id);
                Require(Source["outcome_rules"]?[turn.Expected!.Outcome] is JsonObject, "Unknown outcome: " + c.Id);
                Require(turn.Type is "user" or "harness", "Unknown turn type: " + c.Id);
                if (turn.Type == "harness")
                {
                    Require(turn.Action is not null && Source["execution_contract"]?["harness_action_catalog"]?[turn.Action] is not null, "Unknown harness action: " + c.Id);
                    continue;
                }
                Require(turn.Text is not null && turn.Text.Length <= 2000, "Invalid prompt: " + c.Id);
                Require(!string.IsNullOrWhiteSpace(turn.Text) || c.CoverageKey == "input.empty_whitespace", "Empty prompt: " + c.Id);
                foreach (Match match in Regex.Matches(turn.Text!, @"\{\{([^}]+)\}\}")) Require(allRefs.Contains(match.Groups[1].Value), "Unknown placeholder: " + c.Id);
                var key = Regex.Replace(turn.Text!.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"\s+", " ").Trim();
                Require(!seen.TryGetValue(key, out var prior) || prior == c.Id && c.IntentionalRepetition, "Duplicate normalized prompt: " + c.Id);
                seen[key] = c.Id;
            }
        }
    }
}
