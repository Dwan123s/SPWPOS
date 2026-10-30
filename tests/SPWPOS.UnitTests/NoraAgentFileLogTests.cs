using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

internal sealed class FileLogTestEnvironment(string name, string root) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "NoraFileLogTests";
    public string ContentRootPath { get; set; } = root;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public sealed class NoraAgentFileLogTests
{
    private static NoraAgentFileLog Create(string root, string environment = "Development", bool enabled = true,
        TimeProvider? clock = null) => new(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["NoraAgent:FileLog:Enabled"] = enabled.ToString() }).Build(),
        clock ?? TimeProvider.System, NullLogger<NoraAgentFileLog>.Instance, new FileLogTestEnvironment(environment, root));

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Testing", true)]
    [InlineData("Development", false)]
    public void Disabled_or_non_development_logging_creates_no_files(string environment, bool enabled)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "file-log-tests", Guid.NewGuid().ToString("N"));
        Create(root, environment, enabled).Initialize();
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task Concurrent_records_are_valid_json_and_never_contain_raw_model_or_tool_data()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "file-log-tests", Guid.NewGuid().ToString("N"));
        var log = Create(root); log.Initialize();
        var run = Guid.NewGuid(); var turn = Guid.NewGuid();
        const string privateData = "PRIVATE_PROVIDER_PAYLOAD_AND_SECRET";
        log.RunStarted(run, turn, Guid.NewGuid(), new(Guid.NewGuid(), Guid.NewGuid(), privateData), 0);
        log.ModelStarted(run, turn, 1);
        log.ModelCompleted(run, turn, 1, 10, new(JsonSerializer.SerializeToElement(new { thoughtSignature = privateData }),
            [new("id", privateData, JsonSerializer.SerializeToElement(new { apiKey = privateData }))]));
        await Task.WhenAll(Enumerable.Range(1, 50).Select(n => Task.Run(() => log.ToolCompleted(run, turn,
            new(n, "get_tasks", privateData, "OK", n), "AABB", new("OK", new { text = privateData }, Message: privateData)))));
        log.Failed(run, turn, "EXECUTION", new AuthException("AGENT_AI_TIMEOUT", privateData, 503), false);
        log.RunCompleted(run, turn, "PROPOSED", 1, 50, 0);
        var text = File.ReadAllText(Assert.Single(Directory.GetFiles(Path.Combine(root, "logs", "nora-agent"))));
        Assert.DoesNotContain(privateData, text);
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(56, lines.Length);
        foreach (var line in lines) { using var parsed = JsonDocument.Parse(line); Assert.True(parsed.RootElement.TryGetProperty("eventName", out _)); }
        Assert.Contains("UNKNOWN_TOOL", text);
        Assert.Contains("AGENT_AI_TIMEOUT", text);
        Assert.Contains("\"businessDataSaved\":false", text);
    }

    [Fact]
    public void Log_rotates_at_utc_midnight_and_io_failures_do_not_break_agent_processing()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "file-log-tests", Guid.NewGuid().ToString("N"));
        var clock = new TestClock(); var log = Create(root, clock: clock);
        log.Initialize(); clock.Advance(TimeSpan.FromDays(1)); log.Initialize();
        Assert.Equal(2, Directory.GetFiles(Path.Combine(root, "logs", "nora-agent")).Length);
        var blocked = Path.Combine(root, "file-as-directory"); File.WriteAllText(blocked, "fixture");
        var unavailable = Create(blocked);
        unavailable.Initialize();
        unavailable.RunCompleted(Guid.NewGuid(), Guid.NewGuid(), "COMPLETED", 1, 1, 0);
    }

    [Fact]
    public void Daily_cap_is_respected_without_interrupting_processing()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "file-log-tests", Guid.NewGuid().ToString("N"));
        var log = Create(root); log.Initialize();
        var file = Assert.Single(Directory.GetFiles(Path.Combine(root, "logs", "nora-agent")));
        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Write)) stream.SetLength(10 * 1024 * 1024);
        log.Initialize();
        Assert.Equal(10 * 1024 * 1024, new FileInfo(file).Length);
    }
}
