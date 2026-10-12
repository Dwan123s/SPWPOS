using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Infrastructure.Workspace;
namespace SPWPOS.UnitTests;

public sealed class NoteInterpreterTests
{
    private sealed class FakeProvider(string tasks) : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = await request.Content!.ReadAsStringAsync(ct);
            var text = JsonSerializer.Serialize(new { tasks = JsonSerializer.Deserialize<JsonElement>(tasks) });
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

    [Fact] public async Task Rules_report_time_conditions_and_do_not_silently_truncate_long_notes()
    {
        using var http = new HttpClient();
        var result = await Interpreter(http).AnalyzeNoteAsync("Tôi rảnh 19h đến 21h\nViết báo cáo 2 tiếng", false,
            "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.Single(result.Tasks); Assert.Contains(result.Messages, x => x.Contains("Chưa áp dụng điều kiện thời gian"));
        var tooMany = await Interpreter(http).AnalyzeNoteAsync(string.Join('\n', Enumerable.Repeat("Viết báo cáo 60 phút", 13)), false,
            "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.Empty(tooMany.Tasks); Assert.Contains(tooMany.Messages, x => x.Contains("tối đa 12 việc"));
    }
}
