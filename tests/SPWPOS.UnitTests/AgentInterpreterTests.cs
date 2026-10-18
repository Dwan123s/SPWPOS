using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Workspace;
namespace SPWPOS.UnitTests;

public sealed class AgentInterpreterTests
{
    private const string Payload = """
        {"reply":"Thêm cuộc họp","resultKind":"MUTATION","question":"","options":[],"replaceWeeklyAvailability":false,"weeklySlots":[],"exceptions":[],
         "events":[{"name":"Họp","start":"2026-10-15T14:00:00+07:00","end":"2026-10-15T16:00:00+07:00","description":"","eventType":"MEETING","repeatWeeks":1,"existingId":null}],
         "tasks":[],"scheduleTaskIds":[],"reschedule":false,"horizonDays":14}
        """;
    private sealed class Provider(string output = Payload, string finish = "STOP", HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        public Uri? Url { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri; Body = await request.Content!.ReadAsStringAsync(ct);
            Assert.Equal("test-key", request.Headers.GetValues("x-goog-api-key").Single());
            return new(status) { Content = new StringContent(JsonSerializer.Serialize(new {
                candidates = new[] { new { finishReason = finish, content = new { parts = new object[] { new { thought = true, text = "private" }, new { text = output } } } } }
            })) };
        }
    }
    private static TaskInterpreter Interpreter(HttpClient client) => new(client,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = "Gemini", ["AiSettings:Model"] = "gemini-3.5-flash-lite", ["AiSettings:GeminiApiKey"] = "test-key"
        }).Build(), NullLogger<TaskInterpreter>.Instance);
    [Fact] public async Task Interpreter_sends_the_schema_context_and_timezone_and_preserves_exact_event_times()
    {
        using var provider = new Provider(); using var client = new HttpClient(provider);
        var result = await Interpreter(client).InterpretAsync("Họp ngày mai 14h–16h", "{\"availability\":[]}", [], "Asia/Ho_Chi_Minh", DateTimeOffset.Parse("2026-10-14T06:00:00Z"), TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromHours(7), Assert.Single(result.Events).Start.Offset);
        Assert.Equal(14, result.Events[0].Start.Hour); Assert.Equal(16, result.Events[0].End.Hour);
        Assert.DoesNotContain("test-key", provider.Body); Assert.Equal("", provider.Url!.Query);
        using var request = JsonDocument.Parse(provider.Body);
        var schema = request.RootElement.GetProperty("generationConfig").GetProperty("responseFormat").GetProperty("text").GetProperty("schema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Contains(schema.GetProperty("required").EnumerateArray(), x => x.GetString() == "events");
        Assert.Contains("Asia/Ho_Chi_Minh", provider.Body); Assert.Contains("availability", provider.Body);
    }
    [Theory] [InlineData("MAX_TOKENS", HttpStatusCode.OK)] [InlineData("SAFETY", HttpStatusCode.OK)] [InlineData("STOP", HttpStatusCode.TooManyRequests)]
    public async Task Incomplete_or_failed_provider_response_never_becomes_a_command(string finish, HttpStatusCode status)
    {
        using var provider = new Provider(finish: finish, status: status); using var client = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpreter(client).InterpretAsync("Họp", "{}", [], "UTC", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
        Assert.Equal(503, error.StatusCode);
    }
    [Theory] [InlineData("missing")] [InlineData("offset")] [InlineData("unknown")]
    public async Task Malformed_schema_or_offset_is_rejected(string mode)
    {
        var output = mode switch {
            "missing" => Payload.Replace("\"repeatWeeks\":1", "\"irrelevant\":1"),
            "offset" => Payload.Replace("14:00:00+07:00", "14:00:00"),
            _ => Payload.Replace("\"reschedule\":false", "\"reschedule\":false,\"userId\":\"other\"")
        };
        using var provider = new Provider(output); using var client = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpreter(client).InterpretAsync("Họp", "{}", [], "UTC", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
        Assert.Equal("AGENT_AI_INVALID_RESPONSE", error.Code);
    }
    [Theory] [InlineData("CUSTOM_ACTIVE", true)] [InlineData("DESIGN", false)]
    public async Task Task_type_schema_uses_only_active_runtime_codes(string type, bool supported)
    {
        var task = """
            {"taskId":null,"name":"Slide","deadline":"2026-10-21T22:00:00+07:00","estimatedMinutes":90,"priority":2,"taskType":"TYPE_CODE","splittable":true,
             "workWindowStart":null,"workWindowEnd":null,"durationEvidence":"90 phút","deadlineEvidence":"xong trước 22h ngày 21/10","workWindowEvidence":""}
            """.Replace("TYPE_CODE", type);
        var output = System.Text.Json.Nodes.JsonNode.Parse(Payload)!;
        output["events"] = new System.Text.Json.Nodes.JsonArray();
        output["tasks"] = System.Text.Json.Nodes.JsonNode.Parse("[" + task + "]");
        using var provider = new Provider(output.ToJsonString()); using var client = new HttpClient(provider);
        var call = Interpreter(client).InterpretAsync("Slide 90 phút, xong trước 22h ngày 21/10", """{"activeTaskTypes":["CUSTOM_ACTIVE","OTHER"]}""", [], "Asia/Ho_Chi_Minh", DateTimeOffset.Parse("2026-10-19T01:00:00Z"), TestContext.Current.CancellationToken);
        if (supported) Assert.Equal(type, Assert.Single((await call).Tasks).TaskType);
        else Assert.Equal("AGENT_AI_INVALID_RESPONSE", (await Assert.ThrowsAsync<AuthException>(() => call)).Code);
        using var request = JsonDocument.Parse(provider.Body);
        var codes = request.RootElement.GetProperty("generationConfig").GetProperty("responseFormat").GetProperty("text").GetProperty("schema")
            .GetProperty("properties").GetProperty("tasks").GetProperty("items").GetProperty("properties").GetProperty("taskType").GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("CUSTOM_ACTIVE", codes); Assert.DoesNotContain("DESIGN", codes);
    }
    [Theory] [InlineData("lúc 6 giờ", "06:00 sáng", "18:00 chiều/tối")]
    [InlineData("lúc sáu giờ rưỡi", "06:30 sáng", "18:30 chiều/tối")]
    [InlineData("trước 8h15", "08:15 sáng", "20:15 chiều/tối")]
    public async Task Ambiguous_clock_is_clarified_without_any_provider_call(string text, string morning, string evening)
    {
        using var provider = new Provider(); using var client = new HttpClient(provider);
        var result = await Interpreter(client).InterpretAsync("Có hẹn " + text, "{}", [], "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.Equal("CLARIFICATION", result.ResultKind); Assert.Equal(new[] { morning, evening }, result.Options);
        Assert.Empty(result.Events); Assert.Empty(result.Tasks); Assert.False(result.Reschedule); Assert.Null(provider.Url);
    }
}
