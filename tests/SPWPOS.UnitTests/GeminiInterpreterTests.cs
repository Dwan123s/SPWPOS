using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Workspace;
using SPWPOS.Application.Authentication;
using SPWPOS.Infrastructure.Workspace;

namespace SPWPOS.UnitTests;

public sealed class GeminiInterpreterTests
{
    private const string Tasks = """
    [{"name":"Báo cáo","description":"Viết báo cáo","deadline":"2026-10-15T10:00:00Z","estimatedMinutes":120,"priority":3,"taskType":"WRITING","isHardDeadline":true},
     {"name":"Ôn bài","description":"Chương 4","deadline":"2026-10-16T10:00:00Z","estimatedMinutes":60,"priority":2,"taskType":"STUDY","isHardDeadline":false}]
    """;
    private sealed class Provider(string tasks = Tasks, string finish = "STOP", HttpStatusCode status = HttpStatusCode.OK, bool needsReview = false, bool omitReview = false, string? activities = null) : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        public Uri? Url { get; private set; }
        public string? Key { get; private set; }
        public bool Bearer { get; private set; }
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Url = request.RequestUri; Bearer = request.Headers.Authorization is not null;
            Key = request.Headers.GetValues("x-goog-api-key").Single();
            Body = await request.Content!.ReadAsStringAsync(ct);
            var payloadData = new Dictionary<string, object> { ["tasks"] = JsonSerializer.Deserialize<JsonElement>(tasks) };
            if (activities is not null) payloadData["activities"] = JsonSerializer.Deserialize<JsonElement>(activities);
            if (!omitReview) payloadData["needsReview"] = needsReview;
            var text = JsonSerializer.Serialize(payloadData);
            var payload = JsonSerializer.Serialize(new { candidates = new[] { new { finishReason = finish,
                content = new { parts = new object[] { new { thought = true, text = "private reasoning, not a task payload" }, new { text } } } } } });
            return new(status) { Content = new StringContent(payload) };
        }
    }
    private static TaskInterpreter Interpreter(HttpClient http, string? key = "test-gemini-key", string model = "gemini-3.5-flash-lite") => new(http,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = "Gemini", ["AiSettings:GeminiApiKey"] = key,
            ["AiSettings:ApiKey"] = "unrelated-openai-key", ["AiSettings:Model"] = model
        }).Build(), NullLogger<TaskInterpreter>.Instance);

    [Fact] public async Task Gemini_extracts_multiple_tasks_using_schema_and_header_key_without_account_data()
    {
        using var provider = new Provider(); using var http = new HttpClient(provider);
        var interpreter = Interpreter(http);
        Assert.True(interpreter.Capabilities.AiAvailable); Assert.Equal("Gemini", interpreter.Capabilities.Provider);
        var result = await interpreter.AnalyzeNoteAsync("Viết báo cáo và ôn bài", true, "Asia/Ho_Chi_Minh",
            DateTimeOffset.Parse("2026-10-08T10:00:00Z"), TestContext.Current.CancellationToken);
        Assert.Equal("Gemini", result.Source); Assert.Equal(2, result.Tasks.Length); Assert.True(result.Tasks[0].IsHardDeadline);
        Assert.Equal("generativelanguage.googleapis.com", provider.Url!.Host);
        Assert.EndsWith("/models/gemini-3.5-flash-lite:generateContent", provider.Url.AbsolutePath);
        Assert.Equal("", provider.Url.Query); Assert.Equal("test-gemini-key", provider.Key); Assert.False(provider.Bearer);
        Assert.DoesNotContain("test-gemini-key", provider.Body); Assert.DoesNotContain("unrelated-openai-key", provider.Body);
        Assert.DoesNotContain("email", provider.Body, StringComparison.OrdinalIgnoreCase);
        using var request = JsonDocument.Parse(provider.Body);
        Assert.False(request.RootElement.GetProperty("store").GetBoolean());
        Assert.Contains("untrusted data", request.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        // Raw REST uses the enum; the SDK MIME string is not valid in this field.
        Assert.Equal("APPLICATION_JSON", request.RootElement.GetProperty("generationConfig").GetProperty("responseFormat").GetProperty("text").GetProperty("mimeType").GetString());
    }

    [Fact] public async Task Notes_always_contact_Gemini_even_when_older_clients_send_false()
    {
        using var provider = new Provider(); using var http = new HttpClient(provider);
        var result = await Interpreter(http).AnalyzeNoteAsync("Viết báo cáo 60 phút", false, "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.Equal("Gemini", result.Source); Assert.Equal(1, provider.Calls);
    }

    [Fact] public async Task Task_autofill_uses_flash_lite_and_a_small_output_budget()
    {
        using var provider = new Provider(activities: "[]");
        using var http = new HttpClient(provider);
        var interpreter = Interpreter(http, model: "gemini-3.8-flash");
        var result = await interpreter.PreviewTaskTextAsync("Kiểm tra email lúc 14h", "Asia/Ho_Chi_Minh",
            DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.NotNull(result.Activities);
        Assert.EndsWith("/models/gemini-3.5-flash-lite:generateContent", provider.Url!.AbsolutePath);
        using var request = JsonDocument.Parse(provider.Body);
        var generation = request.RootElement.GetProperty("generationConfig");
        Assert.Equal(800, generation.GetProperty("maxOutputTokens").GetInt32());
        Assert.Equal("MINIMAL", generation.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
    }

    [Theory]
    [InlineData("MAX_TOKENS", HttpStatusCode.OK, "NOTE_AI_RESPONSE_INCOMPLETE")]
    [InlineData("SAFETY", HttpStatusCode.OK, "NOTE_AI_CONTENT_BLOCKED")]
    [InlineData("STOP", HttpStatusCode.TooManyRequests, "NOTE_AI_QUOTA_EXCEEDED")]
    [InlineData("STOP", HttpStatusCode.Unauthorized, "NOTE_AI_AUTH_FAILED")]
    [InlineData("STOP", HttpStatusCode.Forbidden, "NOTE_AI_AUTH_FAILED")]
    [InlineData("STOP", HttpStatusCode.BadRequest, "NOTE_AI_REQUEST_INVALID")]
    [InlineData("STOP", HttpStatusCode.NotFound, "NOTE_AI_MODEL_UNAVAILABLE")]
    [InlineData("STOP", HttpStatusCode.ServiceUnavailable, "NOTE_AI_UNAVAILABLE")]
    public async Task Incomplete_blocked_or_failed_responses_are_not_imported(string finish, HttpStatusCode status, string code)
    {
        using var provider = new Provider(finish: finish, status: status); using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpreter(http).AnalyzeNoteAsync("Viết báo cáo 60 phút", true, "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
        Assert.Equal(code, error.Code); Assert.Equal(503, error.StatusCode);
    }

    [Fact] public async Task Empty_Gemini_result_does_not_invent_a_task()
    {
        using var provider = new Provider("[]"); using var http = new HttpClient(provider);
        var result = await Interpreter(http).AnalyzeNoteAsync("Hôm nay tôi cảm thấy mệt", true, "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.Equal("Gemini", result.Source); Assert.Empty(result.Tasks);
    }

    [Fact] public async Task Missing_Gemini_key_never_sends_an_OpenAI_key_to_Google()
    {
        using var provider = new Provider(); using var http = new HttpClient(provider);
        var interpreter = Interpreter(http, key: null); Assert.False(interpreter.Capabilities.AiAvailable);
        var error = await Assert.ThrowsAsync<AuthException>(() => interpreter.AnalyzeNoteAsync("Viết báo cáo 60 phút", true, "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
        Assert.Equal("NOTE_AI_NOT_CONFIGURED", error.Code);
        Assert.Equal(0, provider.Calls);
    }

    [Fact] public async Task Invalid_model_configuration_cannot_change_the_endpoint_or_query()
    {
        using var provider = new Provider(); using var http = new HttpClient(provider);
        var interpreter = Interpreter(http, model: "https://example.com/?key=anything"); Assert.False(interpreter.Capabilities.AiAvailable);
        var error = await Assert.ThrowsAsync<AuthException>(() => interpreter.AnalyzeNoteAsync("Viết báo cáo 60 phút", true, "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
        Assert.Equal("NOTE_AI_NOT_CONFIGURED", error.Code);
        Assert.Equal(0, provider.Calls);
    }

    [Fact] public async Task Capture_and_breakdown_also_identify_Gemini_as_the_source()
    {
        using var provider = new Provider(); using var http = new HttpClient(provider);
        var interpreter = Interpreter(http);
        var capture = await interpreter.CaptureAsync(new("Viết báo cáo", true), "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.Equal("Gemini", capture.Source);
        var breakdown = await interpreter.BreakdownAsync(new TaskInput("Báo cáo", "", DateTimeOffset.Parse("2026-10-15T10:00:00Z"), 180), true, TestContext.Current.CancellationToken);
        Assert.Equal("Gemini", breakdown.Source); Assert.All(breakdown.Subtasks, x => Assert.True(x.Deadline <= DateTimeOffset.Parse("2026-10-15T10:00:00Z")));
    }
    [Fact] public async Task Ai_decides_when_conditions_need_review_without_local_rules()
    {
        using var provider = new Provider(needsReview: true); using var http = new HttpClient(provider);
        var result = await Interpreter(http).AnalyzeNoteAsync("Một yêu cầu chưa rõ", true, "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.True(result.NeedsReview);
        using var request = JsonDocument.Parse(provider.Body);
        var schema = request.RootElement.GetProperty("generationConfig").GetProperty("responseFormat").GetProperty("text").GetProperty("schema");
        Assert.Contains(schema.GetProperty("required").EnumerateArray(), x => x.GetString() == "needsReview");
    }
    [Fact] public async Task Missing_review_flag_is_rejected_without_rule_fallback()
    {
        using var provider = new Provider(omitReview: true); using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpreter(http).AnalyzeNoteAsync("Viết báo cáo 60 phút", true, "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
        Assert.Equal("NOTE_AI_UNAVAILABLE", error.Code);
    }
    [Theory]
    [InlineData("[{\"name\":\"bad\"}]")]
    [InlineData("[{\"name\":\"bad\",\"description\":\"\",\"deadline\":\"not-a-date\",\"estimatedMinutes\":60,\"priority\":2,\"taskType\":\"WRITING\",\"isHardDeadline\":false}]")]
    public async Task Invalid_ai_task_data_is_rejected_without_local_analysis(string tasks)
    {
        using var provider = new Provider(tasks); using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpreter(http).AnalyzeNoteAsync("Viết báo cáo 60 phút", true, "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
        Assert.Equal("NOTE_AI_UNAVAILABLE", error.Code);
    }
}
