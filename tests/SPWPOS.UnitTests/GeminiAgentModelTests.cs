using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Workspace;

namespace SPWPOS.UnitTests;

public sealed class GeminiAgentModelTests
{
    private sealed class Provider(string? id, bool malformed = false, HttpStatusCode status = HttpStatusCode.OK, bool assessment = false) : HttpMessageHandler
    {
        public JsonElement Body { get; private set; }
        public Uri? Url { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone(); Url = request.RequestUri;
            var args = new Dictionary<string, object?> { ["start"] = "2026-11-01T00:00:00+07:00", ["end"] = "2026-11-02T00:00:00+07:00" };
            if (assessment) args["requestUnderstanding"] = new { intent = "CalendarQuery", summary = "Xem lịch ngày mai.", needsPersonalData = true, missingDetails = Array.Empty<string>() };
            var function = new Dictionary<string, object?> { ["name"] = "get_calendar", ["args"] = malformed ? "bad" : args };
            if (id is not null) function["id"] = id;
            return new(status) { Content = new StringContent(JsonSerializer.Serialize(new {
                candidates = new[] { new { finishReason = "STOP", content = new { role = "model", parts = new[] { new { functionCall = function, thoughtSignature = "preserve-signature" } } } } },
                usageMetadata = new { promptTokenCount = 30, candidatesTokenCount = 10, totalTokenCount = 40 }
            })) };
        }
    }
    [Theory] [InlineData("Gemini", null)] [InlineData("VertexAI", "call-provider-1")]
    public async Task Native_function_calling_uses_tools_preserves_signature_and_provider_ids(string provider, string? id)
    {
        using var handler = new Provider(id); using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = provider, ["AiSettings:Model"] = "gemini-3.8-flash",
            ["AiSettings:GeminiApiKey"] = "test-key", ["AiSettings:VertexApiKey"] = "test-cloud-key",
            ["AiSettings:VertexProjectId"] = "test-project", ["AiSettings:VertexLocation"] = "global",
            ["AiSettings:VertexAuthentication"] = "ApiKey", ["AiSettings:VertexExpressMode"] = "true"
        }).Build();
        var model = new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance);
        var schema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { }, required = Array.Empty<string>() });
        var result = await model.NextAgentStepAsync(new("system", [JsonSerializer.SerializeToElement(new { role = "user", parts = new[] { new { text = "goal" } } })],
            [new("get_calendar", "Read calendar", schema, "Đọc lịch")]), TestContext.Current.CancellationToken);
        Assert.Equal(id, result.Calls.Single().ProviderId);
        Assert.Equal("preserve-signature", result.Content.GetProperty("parts")[0].GetProperty("thoughtSignature").GetString());
        Assert.Equal("ANY", handler.Body.GetProperty("toolConfig").GetProperty("functionCallingConfig").GetProperty("mode").GetString());
        Assert.True(handler.Body.GetProperty("tools")[0].GetProperty("functionDeclarations")[0].TryGetProperty("parametersJsonSchema", out _));
        var generation = handler.Body.GetProperty("generationConfig");
        Assert.False(generation.TryGetProperty("responseSchema", out _)); Assert.False(generation.TryGetProperty("responseMimeType", out _));
        Assert.False(generation.TryGetProperty("candidateCount", out _));
        Assert.Equal(16384, generation.GetProperty("maxOutputTokens").GetInt32());
        Assert.Equal("LOW", generation.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
        Assert.Contains("generateContent", handler.Url!.AbsolutePath);
    }

    [Fact] public async Task Provider_failure_is_sanitized_and_never_becomes_a_tool_observation()
    {
        using var handler = new Provider(null, status: HttpStatusCode.TooManyRequests); using var http = new HttpClient(handler);
        var model = new TaskInterpreter(http, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = "Gemini", ["AiSettings:Model"] = "gemini-3.8-flash", ["AiSettings:GeminiApiKey"] = "secret-key"
        }).Build(), NullLogger<TaskInterpreter>.Instance);
        var failure = await Assert.ThrowsAsync<AuthException>(() => model.NextAgentStepAsync(new("system", [], []), TestContext.Current.CancellationToken));
        Assert.Equal("AGENT_AI_QUOTA_EXCEEDED", failure.Code); Assert.DoesNotContain("secret-key", failure.Message);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task First_response_requires_understanding_in_the_same_native_call_and_preserves_signed_content(bool provided)
    {
        using var handler = new Provider("first-call", assessment: provided); using var http = new HttpClient(handler);
        var model = new TaskInterpreter(http, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = "Gemini", ["AiSettings:Model"] = "gemini-3.8-flash", ["AiSettings:GeminiApiKey"] = "test-key"
        }).Build(), NullLogger<TaskInterpreter>.Instance);
        var schema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { }, required = Array.Empty<string>() });
        var request = new NoraModelRequest("system", [], [new("get_calendar", "Read calendar", schema, "Đọc lịch"),
            new("prepare_changes", "Later revision only", schema, "Bản nháp"), new("evaluate_candidate", "Later comparison only", schema, "Kiểm tra")], RequireUnderstanding: true);
        if (!provided)
        {
            Assert.Equal("AGENT_AI_INVALID_RESPONSE", (await Assert.ThrowsAsync<AuthException>(() => model.NextAgentStepAsync(request, TestContext.Current.CancellationToken))).Code);
            return;
        }
        var result = await model.NextAgentStepAsync(request, TestContext.Current.CancellationToken);
        var parameters = handler.Body.GetProperty("tools")[0].GetProperty("functionDeclarations")[0].GetProperty("parametersJsonSchema");
        Assert.Equal(1, handler.Body.GetProperty("tools")[0].GetProperty("functionDeclarations").GetArrayLength());
        Assert.Contains(parameters.GetProperty("required").EnumerateArray(), x => x.GetString() == "requestUnderstanding");
        Assert.Equal("CalendarQuery", result.Understanding!.Intent);
        Assert.False(result.Calls[0].Arguments.TryGetProperty("requestUnderstanding", out _));
        Assert.True(result.Content.GetProperty("parts")[0].GetProperty("functionCall").GetProperty("args").TryGetProperty("requestUnderstanding", out _));
        Assert.Equal("preserve-signature", result.Content.GetProperty("parts")[0].GetProperty("thoughtSignature").GetString());
        Assert.True(model.SupportsRequestUnderstanding);
    }
}
