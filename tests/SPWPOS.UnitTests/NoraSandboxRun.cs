using System.Text.Json;
using SPWPOS.TestSupport.NoraSandbox;
namespace SPWPOS.UnitTests;

// Only the explicit sandbox script sets this. Normal tests never call a live model.
public sealed class NoraSandboxRun
{
    public static bool Requested => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SPWPOS_NORA_SANDBOX_OPTIONS"));
    [Fact(SkipUnless = nameof(Requested), Skip = "Run scripts/Test-NoraSandbox.ps1 to execute the opt-in sandbox.")]
    public async Task Execute_selected_scenarios_and_export_results()
    {
        var path = Environment.GetEnvironmentVariable("SPWPOS_NORA_SANDBOX_OPTIONS")!;
        var options = JsonSerializer.Deserialize<SandboxOptions>(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken), SandboxJson.Options)!;
        var report = await SandboxRunner.RunAsync(options, Console.WriteLine, TestContext.Current.CancellationToken);
        Console.WriteLine("NORA_SANDBOX_REPORT " + Path.Combine(options.OutputDirectory, "report.md"));
        Assert.DoesNotContain(report.Cases, x => x.Status is "FAIL" or "ERROR");
    }
}
