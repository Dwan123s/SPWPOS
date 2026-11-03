using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Workspace;
namespace SPWPOS.UnitTests;

public sealed class ImportanceLiveTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("PLANORA_LIVE_IMPORTANCE") == "1";
    [Fact(SkipUnless = nameof(Enabled), Skip = "Opt-in real Gemini importance proposal with fictional task only.")]
    public async Task Configured_flash_proposes_importance_from_fictional_deliverables()
    {
        var root = Environment.GetEnvironmentVariable("PLANORA_REPOSITORY_ROOT")!;
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "src/SPWPOS.Api/appsettings.json"))
            .AddUserSecrets("SPWPOS-Authentication-Development").Build();
        Assert.Equal("gemini-3.8-flash", config["AiSettings:Model"]);
        using var http = new HttpClient(); var model = new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance);
        var context = new ImportanceContext("Hợp nhất và nghiệm thu proposal SSG105", "Bàn giao proposal cuối cùng bắt buộc để nộp môn học. Rà soát đủ mục tiêu, phạm vi và tiêu chí chấm điểm.", []);
        var timer = Stopwatch.StartNew(); var result = await model.SuggestImportanceAsync(context, TestContext.Current.CancellationToken);
        Assert.Equal("Important", result.Level); Assert.NotEmpty(result.Reason); Assert.NotEmpty(result.Evidence);
        Assert.True((context.Name + "\n" + context.Description).Contains(result.Evidence, StringComparison.OrdinalIgnoreCase));
        Assert.InRange(result.Confidence, 0, 1);
        Console.WriteLine($"Live gemini-3.8-flash importance proposal: {result.Level}; {timer.Elapsed.TotalSeconds:F1}s; fictional input only, no tasks saved.");
    }
}
