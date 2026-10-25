using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.Application.Workspace;
namespace SPWPOS.UnitTests;

public sealed class NoteInterpreterTests
{
    private sealed class FakeProvider(string tasks) : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = await request.Content!.ReadAsStringAsync(ct);
            var text = JsonSerializer.Serialize(new { tasks = JsonSerializer.Deserialize<JsonElement>(tasks), needsReview = false });
            var response = JsonSerializer.Serialize(new { output = new[] { new { type = "message", content = new[] { new { type = "output_text", text } } } } });
            return new(HttpStatusCode.OK) { Content = new StringContent(response) };
        }
    }
    private static TaskInterpreter Interpreter(HttpClient http) => new(http,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:ApiKey"] = "test-only-key", ["AiSettings:Model"] = "test-only-model"
        }).Build(), NullLogger<TaskInterpreter>.Instance);

    [Fact] public async Task Ai_extracts_multiple_tasks_and_does_not_send_unrelated_account_data()
    {
        using var provider = new FakeProvider("""
        [{"name":"Báo cáo","description":"Viết báo cáo","deadline":"2026-10-15T10:00:00Z","estimatedMinutes":120,"priority":3,"taskType":"WRITING"},
         {"name":"Ôn bài","description":"Chương 4","deadline":"2026-10-16T10:00:00Z","estimatedMinutes":60,"priority":2,"taskType":"STUDY"}]
        """);
        using var http = new HttpClient(provider);
        var result = await Interpreter(http).AnalyzeNoteAsync("Viết báo cáo và ôn bài", true, "Asia/Ho_Chi_Minh",
            DateTimeOffset.Parse("2026-10-08T10:00:00Z"), TestContext.Current.CancellationToken);
        Assert.Equal("OpenAI", result.Source); Assert.Equal(2, result.Tasks.Length);
        using var request = JsonDocument.Parse(provider.Body);
        Assert.False(request.RootElement.GetProperty("store").GetBoolean());
        Assert.Contains("Asia/Ho_Chi_Minh", request.RootElement.GetProperty("input").GetString());
        Assert.Contains("untrusted data", request.RootElement.GetProperty("instructions").GetString());
        Assert.DoesNotContain("email", provider.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task Empty_ai_result_is_not_replaced_by_an_invented_task()
    {
        using var provider = new FakeProvider("[]"); using var http = new HttpClient(provider);
        var result = await Interpreter(http).AnalyzeNoteAsync("Hôm nay tôi cảm thấy mệt", true, "Asia/Ho_Chi_Minh",
            DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.Equal("OpenAI", result.Source); Assert.Empty(result.Tasks);
    }
    [Fact] public async Task Incremental_analysis_sends_old_content_as_context_and_requests_only_additions()
    {
        using var provider = new FakeProvider("[]"); using var http = new HttpClient(provider);
        await Interpreter(http).AnalyzeNoteChangesAsync("Old task. New meeting.",
            new NoteAnalysisContext("Old task.", ["OLD TASK"], []), "Asia/Ho_Chi_Minh",
            DateTimeOffset.Parse("2026-10-24T10:00:00Z"), TestContext.Current.CancellationToken);
        using var request = JsonDocument.Parse(provider.Body);
        var prompt = request.RootElement.GetProperty("input").GetString()!;
        Assert.Contains("ONLY new activities", prompt);
        Assert.Contains("Old task.", prompt);
        Assert.Contains("Old task. New meeting.", prompt);
        Assert.Contains("do not emit them again", prompt);
        Assert.Contains("untrusted data", request.RootElement.GetProperty("instructions").GetString());
    }

}
