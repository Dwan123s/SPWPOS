using System.Text.Json;
using System.Text.Json.Nodes;
using SPWPOS.TestSupport.NoraSandbox;
namespace SPWPOS.UnitTests;

public sealed class ConversationPackTests
{
    [Fact]
    public async Task Prepared_fixture_coverage_validates_supported_extra_cases_without_claiming_model_success_or_enabling_missing_adapters()
    {
        var directory = Directory.CreateTempSubdirectory("nora-prepared-").FullName;
        try
        {
            var selected = new[] { "G07-05", "G07-06", "G16-06", "G20-03", "G20-06", "G21-06", "G22-04", "G22-05", "G15-06", "G24-01" };
            var report = await SandboxRunner.RunAsync(new(Root(), directory, Catalog: "Conversations", CaseIds: selected) {
                IncludePreparedFixtures = true }, null, TestContext.Current.CancellationToken);
            Assert.Equal(8, report.Cases.Count(c => c.Status == "VALIDATED"));
            Assert.Equal("NOT_RUN", report.Cases.Single(c => c.Id == "G15-06").Status);
            Assert.Equal("NOT_RUN", report.Cases.Single(c => c.Id == "G24-01").Status);
            Assert.All(report.Cases, c => { Assert.Empty(c.Turns); Assert.Equal("NOT_EVALUATED", c.ResponseReview); });
            Assert.Equal(0, report.CallsUsed);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "SPWPOS.sln"))) return dir.FullName;
        throw new InvalidOperationException("Repository not found.");
    }
    private static SandboxOptions Options(string[]? groups = null, string[]? cases = null) => new(Root(), "unused", Catalog: "Conversations", GroupIds: groups, CaseIds: cases);

    [Fact] public void Pack_loads_all_groups_and_allows_repetition_only_in_the_declared_group()
    {
        var pack = ConversationPack.Load(Root());
        Assert.Equal(31, pack.Groups.Length); Assert.Equal(204, pack.Cases.Length);
        Assert.Equal(225, pack.Cases.SelectMany(c => c.Turns).Count(t => t.Type == "user"));
        Assert.All(pack.Cases.Where(c => c.IntentionalRepetition), c => Assert.StartsWith("G25-", c.Id));
    }

    [Fact] public void Selection_intersects_groups_and_cases_and_rejects_typos()
    {
        var pack = ConversationPack.Load(Root());
        Assert.Equal(12, pack.Select(Options(["G01", "G03"])).Length);
        Assert.Equal("G03-02", Assert.Single(pack.Select(Options(["G03"], ["G03-02"]))).Id);
        Assert.Throws<ArgumentException>(() => pack.Select(Options(["G99"])));
        Assert.Throws<ArgumentException>(() => pack.Select(Options(cases: ["G01-99"])));
        Assert.Throws<ArgumentException>(() => pack.Select(Options(["G01"], ["G03-02"])));
    }

    [Fact] public void Fixture_resolution_merges_objects_but_replaces_arrays_without_modifying_shared_source()
    {
        var pack = ConversationPack.Load(Root());
        var original = pack.Source.ToJsonString();
        var twoDays = pack.Fixture(pack.Cases.Single(c => c.Id == "G15-02"));
        Assert.Equal(120, twoDays["preferences"]!["maximum_daily_minutes"]!.GetValue<int>());
        Assert.Equal(15, twoDays["preferences"]!["minimum_session_minutes"]!.GetValue<int>());
        var dep = pack.Fixture(pack.Cases.Single(c => c.Id == "G16-03"));
        Assert.Equal(3, dep["tasks"]!.AsArray().Count);
        Assert.Equal("Completed", dep["tasks"]![0]!["status"]!.GetValue<string>());
        Assert.Equal(original, pack.Source.ToJsonString());
    }

    [Theory] [InlineData("cycle")] [InlineData("duplicate_prompt")] [InlineData("unknown_outcome")] [InlineData("unknown_placeholder")] [InlineData("active_session_collision")]
    public void Invalid_catalogs_are_rejected_before_any_model_call(string defect)
    {
        var source = (JsonObject)ConversationPack.Load(Root()).Source.DeepClone();
        var first = source["groups"]![0]!["cases"]![0]!;
        if (defect == "cycle") source["fixtures"]!["BASE"]!["extends"] = "BASE";
        if (defect == "duplicate_prompt") source["groups"]![0]!["cases"]![1]!["turns"]![0]!["text"] = first["turns"]![0]!["text"]!.GetValue<string>();
        if (defect == "unknown_outcome") first["turns"]![0]!["expected"]!["outcome"] = "MAGIC_SUCCESS";
        if (defect == "unknown_placeholder") first["turns"]![0]!["text"] = "Sửa {{unknown_task}} giúp tôi.";
        if (defect == "active_session_collision")
        {
            var sessions = source["fixtures"]!["PROTECTED"]!["sessions"]!.AsArray();
            var second = (JsonObject)sessions.Single(s => s!["status"]!.GetValue<string>() == "Running")!.DeepClone();
            second["ref"] = "second_active"; second["status"] = "Paused"; sessions.Add(second);
        }
        Assert.Throws<InvalidDataException>(() => ConversationPack.Parse(source.ToJsonString()));
    }

    [Theory] [InlineData("G20-01", 3, 0)] [InlineData("G22-04", 125, 110)] [InlineData("G10-01", 0, 0)] [InlineData("G17-04", 3, 0)] [InlineData("G17-05", 3, 0)]
    public async Task Database_fixture_is_isolated_and_materializes_entities(string id, int tasks, int events)
    {
        var pack = ConversationPack.Load(Root()); var config = SandboxEnvironment.LoadConfiguration(Root(), false); var budget = new SandboxBudget(1);
        await using var env = await ConversationFixture.CreateAsync(pack, pack.Cases.Single(c => c.Id == id), config, false, budget, TestContext.Current.CancellationToken);
        var state = await env.Snapshot(TestContext.Current.CancellationToken);
        Assert.Equal(tasks, state.Tasks.Length); Assert.Equal(events, state.Events.Length); Assert.Equal(0, budget.Used);
        Assert.All(state.Sessions, s => Assert.Contains(state.Tasks, t => t.Id == s.TaskId));
        Assert.Equal(DateTimeOffset.Parse(pack.Fixture(pack.Cases.Single(c => c.Id == id))["now"]!.GetValue<string>()), env.Clock.GetUtcNow());
    }

    [Fact] public async Task Offline_validation_does_not_claim_model_passes_and_exports_full_review_criteria()
    {
        var directory = Directory.CreateTempSubdirectory("nora-conversations-").FullName;
        try
        {
            var report = await SandboxRunner.RunAsync(new(Root(), directory, Catalog: "Conversations", CaseIds: ["G01-01", "G10-01", "G25-01"], MaxCalls: 1), null, TestContext.Current.CancellationToken);
            Assert.Equal(0, report.CallsUsed);
            Assert.Equal("VALIDATED", report.Cases.Single(c => c.Id == "G01-01").Status);
            Assert.Equal("VALIDATED", report.Cases.Single(c => c.Id == "G10-01").Status);
            Assert.Equal("NOT_RUN", report.Cases.Single(c => c.Id == "G25-01").Status);
            Assert.All(report.Cases, c => { Assert.Empty(c.Turns); Assert.Equal("NOT_EVALUATED", c.ResponseReview); });
            using var catalog = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "catalog.json"), TestContext.Current.CancellationToken));
            var first = catalog.RootElement.GetProperty("groups")[0].GetProperty("cases")[0];
            Assert.NotEmpty(first.GetProperty("turns")[0].GetProperty("expected").GetProperty("checks").EnumerateArray());
            Assert.True(File.Exists(Path.Combine(directory, "report.md")));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
