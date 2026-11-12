using System.Text.Json;
using System.Text.Json.Nodes;
using SPWPOS.TestSupport.NoraSandbox;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

[Trait("Category", "Defense")]
public sealed class DefenseConversationContractTests
{
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SPWPOS.sln"))) return directory.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void Exact_saved_state_requirements_survive_catalog_loading()
    {
        var source = (JsonObject)ConversationPack.Load(Root()).Source.DeepClone();
        source["groups"]![0]!["cases"]![0]!["turns"]![0]!["expected"]!["assertions"] = new JsonObject {
            ["task_count"] = 2,
            ["preserve"] = new JsonArray("task:Existing work")
        };
        var pack = ConversationPack.Parse(source.ToJsonString());
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(pack.Cases[0].Turns[0].Expected,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
        Assert.True(serialized.RootElement.TryGetProperty("assertions", out var assertions),
            "Exact state requirements must not be silently dropped by the conversation loader.");
        Assert.Equal(2, assertions.GetProperty("task_count").GetInt32());
        Assert.Equal("task:Existing work", assertions.GetProperty("preserve")[0].GetString());
    }

    [Fact]
    public void Specific_state_checks_cannot_relax_preview_or_outcome_safety()
    {
        var pack = ConversationPack.Load(Root());
        var assertions = new SandboxExpectation { TaskCount = 2, Statuses = ["APPLIED"], Preserve = ["task:Existing work"] };
        var expected = ConversationRunner.Expectation(pack, "PREVIEW", true, "PREVIEW", assertions);
        Assert.Equal(["PREVIEW"], expected.Statuses);
        Assert.True(expected.Unchanged);
        Assert.Equal(2, expected.TaskCount);
        Assert.Equal(assertions.Preserve, expected.Preserve);
    }

    [Theory] [InlineData(60, true)] [InlineData(120, false)] [InlineData(0, false)]
    public void Progress_checks_reject_rescheduling_completed_work_or_missing_remaining_work(int scheduled, bool passes)
    {
        var now = DateTimeOffset.Parse("2026-10-26T09:00:00+07:00");
        var task = new WorkTask { Name = "Read", EstimatedMinutes = 120, Progress = 50, Deadline = now.AddDays(1) };
        var snapshot = new SandboxSnapshot(new() { TimeZoneId = "UTC", MaximumDailyMinutes = 480,
            MaximumSessionMinutes = 240, MinimumSessionMinutes = 15 }, [task], [], [], [], [], [], [], []);
        var after = snapshot with { Sessions = scheduled == 0 ? [] : [new() { TaskId = task.Id,
            Start = now.AddHours(1), End = now.AddHours(1).AddMinutes(scheduled) }] };
        var expected = new SandboxExpectation { Tasks = [new("Read", 120, task.Deadline!.Value.ToString("O"), ScheduledMinutes: 60)] };
        var checks = SandboxOracle.Evaluate(expected, null, null, snapshot, after, now);
        Assert.Equal(passes, Assert.Single(checks, check => check.Name == "scheduled-minutes:Read").Passed);
    }

    [Fact]
    public void Exact_state_requirements_reject_wrong_saved_duration()
    {
        var now = DateTimeOffset.Parse("2026-10-26T09:00:00+07:00");
        var wrong = new WorkTask { Name = "Demo", EstimatedMinutes = 90, Deadline = now.AddDays(1) };
        var before = new SandboxSnapshot(new() { TimeZoneId = "UTC" }, [], [], [], [], [], [], [], []);
        var after = before with { Tasks = [wrong] };
        var pack = ConversationPack.Load(Root());
        var expected = ConversationRunner.Expectation(pack, "MUTATE", true, "APPLIED",
            new() { Tasks = [new("Demo", 45, wrong.Deadline!.Value.ToString("O"))] });
        var checks = SandboxOracle.Evaluate(expected, null, null, before, after, now);
        Assert.False(Assert.Single(checks, check => check.Name == "task-duration:Demo").Passed);
    }
}
