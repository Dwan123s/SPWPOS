using System.Net;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure;
using SPWPOS.Infrastructure.Workspace;

namespace SPWPOS.UnitTests;

public sealed class VertexAiInterpreterTests
{
    private const string Command = """
        {"reply":"Thêm cuộc họp","resultKind":"MUTATION","question":"","options":[],"replaceWeeklyAvailability":false,"weeklySlots":[],"exceptions":[],
        "events":[{"name":"Họp","start":"2026-10-15T14:00:00+07:00","end":"2026-10-15T16:00:00+07:00","description":"","eventType":"MEETING","repeatWeeks":1,"existingId":null}],
        "tasks":[],"scheduleTaskIds":[],"reschedule":false,"horizonDays":14}
        """;
    private const string Note = """
        {"tasks":[{"name":"Báo cáo","description":"","deadline":"2026-10-15T18:00:00+07:00","estimatedMinutes":120,"priority":3,"taskType":"WRITING","isHardDeadline":true}],"needsReview":false}
        """;
    private sealed class Provider(string output = Command, HttpStatusCode status = HttpStatusCode.OK, string? errorBody = null) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Uri? Url { get; private set; }
        public string Body { get; private set; } = "";
        public string? Key { get; private set; }
        public string? Bearer { get; private set; }
        public string? BillingProject { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Url = request.RequestUri; Body = await request.Content!.ReadAsStringAsync(ct);
            Key = request.Headers.TryGetValues("x-goog-api-key", out var keys) ? keys.Single() : null;
            Bearer = request.Headers.Authorization?.Parameter;
            BillingProject = request.Headers.TryGetValues("x-goog-user-project", out var projects) ? projects.Single() : null;
            return new(status) { Content = new StringContent(errorBody ?? JsonSerializer.Serialize(new {
                candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = output } } } } }
            })) };
        }
    }
    private sealed class Tokens(bool fail = false) : IVertexAccessTokenProvider
    {
        public int Calls { get; private set; }
        public Task<string> GetTokenAsync(CancellationToken ct)
        {
            Calls++;
            return fail ? Task.FromException<string>(new InvalidOperationException("private-credential-details"))
                : Task.FromResult("cloud-access-token");
        }
    }
    private static TaskInterpreter Interpreter(HttpClient http, IVertexAccessTokenProvider? tokens = null,
        string? key = "cloud-key", string location = "global", string project = "planora-511614", string auth = "ApiKey", bool express = false) => new(http,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = "VertexAI", ["AiSettings:Model"] = "models/gemini-3.5-flash-lite",
            ["AiSettings:VertexApiKey"] = key, ["AiSettings:VertexProjectId"] = project, ["AiSettings:VertexLocation"] = location,
            ["AiSettings:VertexAuthentication"] = auth, ["AiSettings:VertexExpressMode"] = express.ToString(),
            ["AiSettings:GeminiApiKey"] = "old-studio-key", ["AiSettings:ApiKey"] = "unrelated-openai-key"
        }).Build(), NullLogger<TaskInterpreter>.Instance, tokens);
    private static Task<SPWPOS.Application.Workspace.AgentIntent> Interpret(TaskInterpreter interpreter) =>
        interpreter.InterpretAsync("Họp ngày mai 14h–16h", "{}", [], "Asia/Ho_Chi_Minh",
            DateTimeOffset.Parse("2026-10-14T06:00:00Z"), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Cloud_agent_uses_the_documented_api_key_route_with_a_separate_header_key()
    {
        using var provider = new Provider(); using var http = new HttpClient(provider);
        var interpreter = Interpreter(http);
        var result = await Interpret(interpreter);
        Assert.True(interpreter.Capabilities.AiAvailable); Assert.Equal("VertexAI", interpreter.Capabilities.Provider);
        Assert.Equal("aiplatform.googleapis.com", provider.Url!.Host);
        Assert.Equal("/v1/publishers/google/models/gemini-3.5-flash-lite:generateContent", provider.Url.AbsolutePath);
        Assert.Equal("?key=cloud-key", provider.Url.Query); Assert.Equal("cloud-key", provider.Key); Assert.Null(provider.Bearer);
        Assert.DoesNotContain("cloud-key", provider.Body); Assert.DoesNotContain("old-studio-key", provider.Body);
        Assert.DoesNotContain("unrelated-openai-key", provider.Body); Assert.Equal(14, Assert.Single(result.Events).Start.Hour);
        using var body = JsonDocument.Parse(provider.Body);
        var generation = body.RootElement.GetProperty("generationConfig");
        Assert.Equal("application/json", generation.GetProperty("responseMimeType").GetString());
        Assert.False(generation.GetProperty("responseJsonSchema").GetProperty("additionalProperties").GetBoolean());
        Assert.False(generation.TryGetProperty("responseFormat", out _)); Assert.False(body.RootElement.TryGetProperty("store", out _));
    }
    [Fact] public async Task Cloud_note_analysis_returns_a_source_that_the_note_import_workflow_can_accept()
    {
        using var provider = new Provider(Note); using var http = new HttpClient(provider);
        var result = await Interpreter(http).AnalyzeNoteAsync("Viết báo cáo 120 phút", true, "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.Equal("VertexAI", result.Source); Assert.True(Assert.Single(result.Tasks).IsHardDeadline);
        Assert.False(result.NeedsReview); Assert.Equal("aiplatform.googleapis.com", provider.Url!.Host);
    }
    [Fact] public async Task Missing_cloud_key_never_falls_back_to_the_studio_or_openai_key()
    {
        using var provider = new Provider(); using var http = new HttpClient(provider);
        var interpreter = Interpreter(http, key: null); Assert.False(interpreter.Capabilities.AiAvailable);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpret(interpreter));
        Assert.Equal("AGENT_AI_NOT_CONFIGURED", error.Code); Assert.Equal(0, provider.Calls);
    }
    [Theory] [InlineData("global.evil.test", "planora-511614", "ApiKey")]
    [InlineData("global", "project/../../other", "ApiKey")] [InlineData("global", "", "ApiKey")]
    [InlineData("global", "planora-511614", "unknown")]
    [InlineData("us-central1", "planora-511614", "ApiKey")]
    public async Task Invalid_cloud_configuration_cannot_redirect_credentials(string location, string project, string auth)
    {
        using var provider = new Provider(); using var http = new HttpClient(provider);
        var interpreter = Interpreter(http, location: location, project: project, auth: auth);
        Assert.False(interpreter.Capabilities.AiAvailable);
        await Assert.ThrowsAsync<AuthException>(() => Interpret(interpreter)); Assert.Equal(0, provider.Calls);
    }
    [Theory] [InlineData("global", "aiplatform.googleapis.com")] [InlineData("us-central1", "us-central1-aiplatform.googleapis.com")]
    public async Task Adc_uses_the_project_route_with_a_bearer_token_and_the_explicit_billing_project_without_any_api_key(string location, string host)
    {
        using var provider = new Provider(); using var http = new HttpClient(provider); var tokens = new Tokens();
        await Interpret(Interpreter(http, tokens, key: null, auth: "ADC", location: location));
        Assert.Equal(host, provider.Url!.Host);
        Assert.Equal($"/v1/projects/planora-511614/locations/{location}/publishers/google/models/gemini-3.5-flash-lite:generateContent", provider.Url.AbsolutePath);
        Assert.Equal("", provider.Url.Query);
        Assert.Equal(1, tokens.Calls); Assert.Equal("cloud-access-token", provider.Bearer);
        Assert.Null(provider.Key); Assert.Equal("planora-511614", provider.BillingProject);
        Assert.DoesNotContain("cloud-access-token", provider.Body);
    }
    [Fact] public async Task Missing_adc_credentials_fail_before_sending_calendar_data_and_hide_private_details()
    {
        using var provider = new Provider(); using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpret(Interpreter(http, new Tokens(true), key: null, auth: "ADC")));
        Assert.Equal("AGENT_AI_AUTH_FAILED", error.Code); Assert.DoesNotContain("private-credential-details", error.Message);
        Assert.Equal(0, provider.Calls);
    }
    [Fact] public async Task Legacy_express_setting_uses_the_same_documented_api_key_route()
    {
        using var provider = new Provider(); using var http = new HttpClient(provider);
        await Interpret(Interpreter(http, express: true));
        Assert.Equal("/v1/publishers/google/models/gemini-3.5-flash-lite:generateContent", provider.Url!.AbsolutePath);
        Assert.Equal("cloud-key", provider.Key);
    }
    [Fact] public async Task Cloud_query_key_is_encoded_as_one_parameter_and_matches_the_header()
    {
        const string key = "cloud-key&injected=value+more?x=#fragment";
        using var provider = new Provider(); using var http = new HttpClient(provider);
        await Interpret(Interpreter(http, key: key));
        Assert.Equal("?key=" + Uri.EscapeDataString(key), provider.Url!.Query);
        Assert.Equal("", provider.Url.Fragment);
        Assert.Equal(key, provider.Key);
    }
    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturedLogger(categoryName, Messages);
        public void Dispose() { }
        private sealed class CapturedLogger(string category, ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(category + ": " + formatter(state, exception));
        }
    }
    [Fact] public async Task Registered_ai_client_does_not_log_query_credentials_even_with_trace_logging()
    {
        const string key = "private-query-key&secret=value";
        using var provider = new Provider(); using var logs = new CapturedLogs();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=Unused;Integrated Security=true",
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = "VertexAI", ["AiSettings:Model"] = "gemini-3.5-flash-lite",
            ["AiSettings:VertexAuthentication"] = "ApiKey", ["AiSettings:VertexApiKey"] = key,
            ["AiSettings:VertexProjectId"] = "planora-511614", ["AiSettings:VertexLocation"] = "global"
        }).Build();
        var services = new ServiceCollection(); services.AddSingleton<IConfiguration>(config);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddInfrastructure(config);
        services.AddHttpClient<ITaskInterpreter, TaskInterpreter>().ConfigurePrimaryHttpMessageHandler(() => provider);
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        await Interpret((TaskInterpreter)scope.ServiceProvider.GetRequiredService<ITaskInterpreter>());
        Assert.Equal(key, provider.Key); Assert.Contains("?key=", provider.Url!.ToString());
        foreach (var line in logs.Messages)
        {
            Assert.DoesNotContain(key, line); Assert.DoesNotContain(Uri.EscapeDataString(key), line);
            Assert.DoesNotContain("System.Net.Http.HttpClient", line);
        }
    }
    [Theory] [InlineData(HttpStatusCode.Forbidden, "AGENT_AI_AUTH_FAILED")]
    [InlineData(HttpStatusCode.TooManyRequests, "AGENT_AI_QUOTA_EXCEEDED")]
    [InlineData(HttpStatusCode.BadRequest, "AGENT_AI_REQUEST_INVALID")]
    public async Task Cloud_errors_are_reported_without_retrying_on_another_provider(HttpStatusCode status, string code)
    {
        using var provider = new Provider(status: status); using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpret(Interpreter(http)));
        Assert.Equal(code, error.Code); Assert.Equal(1, provider.Calls);
    }
    [Theory]
    [InlineData("IAM_PERMISSION_DENIED", "IAM_PERMISSION_DENIED")]
    [InlineData("SERVICE_DISABLED", "SERVICE_DISABLED")]
    [InlineData("private-token-reason", "PERMISSION_DENIED")]
    public async Task Cloud_diagnostics_only_keep_allowlisted_reasons_and_never_provider_messages_or_metadata(string reason, string expected)
    {
        var body = JsonSerializer.Serialize(new { error = new {
            status = "PERMISSION_DENIED", message = "private-token-message",
            details = new[] { new Dictionary<string, object> {
                ["@type"] = "type.googleapis.com/google.rpc.ErrorInfo", ["reason"] = reason,
                ["metadata"] = new { credential = "private-token-metadata" }
            } }
        } });
        using var provider = new Provider(status: HttpStatusCode.Forbidden, errorBody: body);
        using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpret(Interpreter(http)));
        Assert.Equal("AGENT_AI_AUTH_FAILED", error.Code);
        Assert.Equal(403, error.Data["GoogleHttpStatus"]);
        Assert.Equal(expected, error.Data["GoogleReason"]);
        Assert.DoesNotContain("private-token", error.Message);
        foreach (var value in error.Data.Values) Assert.DoesNotContain("private-token", value?.ToString() ?? "");
    }
    [Theory]
    [InlineData("not JSON private-token")]
    [InlineData("{\"error\":[]}")]
    public async Task Malformed_cloud_error_details_preserve_the_original_failure(string body)
    {
        using var provider = new Provider(status: HttpStatusCode.Unauthorized, errorBody: body);
        using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpret(Interpreter(http)));
        Assert.Equal("AGENT_AI_AUTH_FAILED", error.Code);
        Assert.Equal(401, error.Data["GoogleHttpStatus"]);
        Assert.Equal("UNKNOWN", error.Data["GoogleReason"]);
        Assert.DoesNotContain("private-token", error.Message);
    }
    private static string StreamBody(string output, bool finished = true)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("data: ").Append(JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = "private-model-thought", thought = true } } } } } })).Append("\n\n");
        for (var index = 0; index < output.Length; index += 7)
        {
            var part = output.Substring(index, Math.Min(7, output.Length - index));
            builder.Append("data: ").Append(JsonSerializer.Serialize(new { candidates = new[] { new {
                content = new { parts = new[] { new { text = part } } },
                finishReason = finished && index + 7 >= output.Length ? "STOP" : null
            } } })).Append("\n\n");
        }
        return builder.ToString();
    }
    [Theory] [InlineData("MEETING", "MEETING")] [InlineData("WORK", "WORK")] [InlineData("UNSUPPORTED", "OTHER")]
    public async Task Streaming_cloud_reply_is_incremental_handles_escapes_and_never_emits_model_thoughts_or_commands(string category, string expected)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(Command)!;
        const string reply = "Mình sẽ chuẩn bị \"báo cáo\".\nLịch của bạn phù hợp 🦉.";
        json["reply"] = reply;
        json["events"]![0]!["eventType"] = category;
        using var provider = new Provider(errorBody: StreamBody(json.ToJsonString())); using var http = new HttpClient(provider);
        var deltas = new List<string>();
        var result = await Interpreter(http).InterpretStreamingAsync("Họp ngày mai", "{}", [], "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow,
            (delta, _) => { deltas.Add(delta); return Task.CompletedTask; }, TestContext.Current.CancellationToken);
        Assert.Equal(reply, result.Reply); Assert.Equal(reply, string.Concat(deltas)); Assert.True(deltas.Count > 1);
        Assert.Equal(expected, Assert.Single(result.Events).EventType);
        Assert.Equal("/v1/publishers/google/models/gemini-3.5-flash-lite:streamGenerateContent", provider.Url!.AbsolutePath);
        Assert.Equal("?key=cloud-key&alt=sse", provider.Url.Query); Assert.Equal("cloud-key", provider.Key);
        Assert.DoesNotContain("private-model-thought", string.Concat(deltas)); Assert.DoesNotContain("events", string.Concat(deltas));
    }
    [Fact] public async Task Streaming_adc_keeps_bearer_authentication_and_the_project_route_without_a_query_key()
    {
        using var provider = new Provider(errorBody: StreamBody(Command)); using var http = new HttpClient(provider);
        await Interpreter(http, new Tokens(), key: null, auth: "ADC", location: "us-central1").InterpretStreamingAsync("Họp", "{}", [], "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow,
            (_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);
        Assert.Equal("?alt=sse", provider.Url!.Query); Assert.Equal("us-central1-aiplatform.googleapis.com", provider.Url.Host);
        Assert.Contains("/projects/planora-511614/locations/us-central1/", provider.Url.AbsolutePath);
        Assert.Equal("cloud-access-token", provider.Bearer); Assert.Null(provider.Key);
    }
    [Fact] public async Task An_incomplete_google_stream_never_returns_an_actionable_intent()
    {
        using var provider = new Provider(errorBody: StreamBody(Command, false)); using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpreter(http).InterpretStreamingAsync("Họp", "{}", [], "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow,
            (_, _) => Task.CompletedTask, TestContext.Current.CancellationToken));
        Assert.Equal("AGENT_AI_INVALID_RESPONSE", error.Code);
    }
    [Fact] public async Task Voice_uses_the_existing_cloud_key_and_inline_audio_without_calendar_context()
    {
        using var provider = new Provider(output: """{"text":"Ngày mai họp từ 14 giờ đến 16 giờ."}"""); using var http = new HttpClient(provider);
        var result = await Interpreter(http).TranscribeAsync([0x1a, 0x45, 0xdf, 0xa3, 1, 2], "audio/webm", TestContext.Current.CancellationToken);
        Assert.Equal("Ngày mai họp từ 14 giờ đến 16 giờ.", result.Text);
        Assert.Equal("cloud-key", provider.Key); Assert.Contains(":generateContent", provider.Url!.AbsolutePath);
        using var body = JsonDocument.Parse(provider.Body);
        var parts = body.RootElement.GetProperty("contents")[0].GetProperty("parts");
        Assert.Equal("audio/webm", parts[1].GetProperty("inlineData").GetProperty("mimeType").GetString());
        Assert.Equal(new byte[] { 0x1a, 0x45, 0xdf, 0xa3, 1, 2 }, parts[1].GetProperty("inlineData").GetProperty("data").GetBytesFromBase64());
        Assert.DoesNotContain("context", provider.Body); Assert.DoesNotContain("scheduleTaskIds", provider.Body);
    }
    [Fact] public async Task Voice_rejects_invalid_uploads_before_calling_google()
    {
        using var provider = new Provider(); using var http = new HttpClient(provider); var interpreter = Interpreter(http);
        Assert.Equal("VOICE_AUDIO_FORMAT", (await Assert.ThrowsAsync<AuthException>(() => interpreter.TranscribeAsync([1, 2], "audio/webm", TestContext.Current.CancellationToken))).Code);
        Assert.Equal("VOICE_AUDIO_SIZE", (await Assert.ThrowsAsync<AuthException>(() => interpreter.TranscribeAsync(new byte[VoiceLimits.MaxAudioBytes + 1], "audio/webm", TestContext.Current.CancellationToken))).Code);
        Assert.Equal(0, provider.Calls);
    }
    [Theory]
    [InlineData("{\"text\":\"\"}", "VOICE_NO_SPEECH")]
    [InlineData("{\"text\":\"Xin chào\",\"tasks\":[]}", "VOICE_AI_INVALID_RESPONSE")]
    public async Task Voice_requires_a_complete_transcript_and_never_returns_action_commands(string output, string code)
    {
        using var provider = new Provider(output: output); using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpreter(http).TranscribeAsync([0x1a, 0x45, 0xdf, 0xa3], "audio/webm", TestContext.Current.CancellationToken));
        Assert.Equal(code, error.Code);
    }
    [Fact] public async Task Voice_provider_errors_are_sanitized_and_adc_is_supported()
    {
        using var provider = new Provider(status: HttpStatusCode.Unauthorized, errorBody: "{\"error\":{\"status\":\"UNAUTHENTICATED\",\"message\":\"private-secret\"}}");
        using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpreter(http, new Tokens(), key: null, auth: "ADC").TranscribeAsync([0x1a, 0x45, 0xdf, 0xa3], "audio/webm", TestContext.Current.CancellationToken));
        Assert.Equal("VOICE_AI_AUTH_FAILED", error.Code); Assert.DoesNotContain("private-secret", error.Message);
        Assert.Equal("cloud-access-token", provider.Bearer); Assert.Null(provider.Key);
    }
    [Fact] public async Task Cancelling_a_provider_stream_propagates_cancellation_instead_of_returning_commands()
    {
        using var provider = new Provider(errorBody: StreamBody(Command)); using var http = new HttpClient(provider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Interpreter(http).InterpretStreamingAsync("Họp", "{}", [], "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow,
            (_, _) => { cancellation.Cancel(); return Task.CompletedTask; }, cancellation.Token));
    }
}
