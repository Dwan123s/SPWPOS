using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private sealed class Provider(string output = Payload, string finish = "STOP", HttpStatusCode status = HttpStatusCode.OK, bool stream = false) : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        public Uri? Url { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri; Body = await request.Content!.ReadAsStringAsync(ct);
            Assert.Equal("test-key", request.Headers.GetValues("x-goog-api-key").Single());
            var payload = JsonSerializer.Serialize(new {
                candidates = new[] { new { finishReason = finish, content = new { parts = new object[] { new { thought = true, text = "private" }, new { text = output } } } } }
            });
            return new(status) { Content = new StringContent(stream ? "data: " + payload + "\n\n" : payload) };
        }
    }
    private static TaskInterpreter Interpreter(HttpClient client) => new(client,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = "Gemini", ["AiSettings:Model"] = "gemini-3.5-flash-lite", ["AiSettings:GeminiApiKey"] = "test-key"
        }).Build(), NullLogger<TaskInterpreter>.Instance);
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Vision_sends_image_bytes_in_the_same_planning_request_without_an_OCR_roundtrip(bool streaming)
    {
        using var provider = new Provider(stream: streaming); using var client = new HttpClient(provider);
        var image = new SPWPOS.Application.Workspace.AgentImage("Lịch.png", "image/png", "iVBORw0KGgo=");
        var result = await Interpreter(client).InterpretImageAsync("Xếp lịch từ ảnh", "{}", [], "Asia/Ho_Chi_Minh",
            DateTimeOffset.Parse("2026-10-14T06:00:00Z"), image, streaming ? (_, _) => Task.CompletedTask : null, TestContext.Current.CancellationToken);
        Assert.Single(result.Events);
        using var body = JsonDocument.Parse(provider.Body);
        var parts = body.RootElement.GetProperty("contents")[0].GetProperty("parts");
        Assert.Equal(2, parts.GetArrayLength());
        Assert.Equal(image.Data, parts[1].GetProperty("inlineData").GetProperty("data").GetString());
        Assert.Equal(image.MimeType, parts[1].GetProperty("inlineData").GetProperty("mimeType").GetString());
        using var prompt = JsonDocument.Parse(parts[0].GetProperty("text").GetString()!);
        Assert.Equal("Xếp lịch từ ảnh", prompt.RootElement.GetProperty("request").GetString());
        Assert.False(body.RootElement.GetProperty("store").GetBoolean());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Telex_reading_helps_the_provider_while_the_original_request_remains_available_for_evidence(bool streaming)
    {
        const string raw = "Tooi muoons lamf vieejc, vieetss baos caos 45 phuts truowsc 21h ngayf mai";
        using var provider = new Provider(stream: streaming); using var client = new HttpClient(provider);
        var interpreter = Interpreter(client); var ct = TestContext.Current.CancellationToken;
        if (streaming) await interpreter.InterpretStreamingAsync(raw, "{}", [], "Asia/Ho_Chi_Minh", DateTimeOffset.Parse("2026-10-15T01:00:00Z"), (_, _) => Task.CompletedTask, ct);
        else await interpreter.InterpretAsync(raw, "{}", [], "Asia/Ho_Chi_Minh", DateTimeOffset.Parse("2026-10-15T01:00:00Z"), ct);
        using var body = JsonDocument.Parse(provider.Body);
        var promptText = body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
        using var prompt = JsonDocument.Parse(promptText);
        Assert.Equal(raw, prompt.RootElement.GetProperty("request").GetString());
        Assert.Equal("Tôi muốn làm việc, viết báo cáo 45 phút trước 21h ngày mai", prompt.RootElement.GetProperty("telexReading").GetString());
        Assert.Contains("unconverted Telex", body.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
    }
    [Fact] public async Task Interpreter_sends_the_schema_context_and_timezone_and_preserves_exact_event_times()
    {
        using var provider = new Provider(); using var client = new HttpClient(provider);
        var result = await Interpreter(client).InterpretAsync("Họp ngày mai 14h–16h", "{\"availability\":[]}", [], "Asia/Ho_Chi_Minh", DateTimeOffset.Parse("2026-10-14T06:00:00Z"), TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromHours(7), Assert.Single(result.Events).Start.Offset);
        Assert.Equal(14, result.Events[0].Start.Hour); Assert.Equal(16, result.Events[0].End.Hour);
        Assert.Contains("urgent meeting/appointment at exact hours", provider.Body);
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
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Free_time_queries_require_explicit_offsets_and_are_sent_as_read_only_queries(bool explicitOffset)
    {
        var payload = JsonNode.Parse(Payload)!.AsObject();
        payload["events"] = new JsonArray(); payload["resultKind"] = "ANSWER";
        payload["freeTimeQuery"] = new JsonObject {
            ["start"] = explicitOffset ? "2026-10-15T00:00:00+07:00" : "2026-10-15T00:00:00",
            ["end"] = "2026-10-16T00:00:00+07:00" };
        using var provider = new Provider(payload.ToJsonString()); using var client = new HttpClient(provider);
        var call = Interpreter(client).InterpretAsync("Ngày mai tôi rảnh giờ nào?", "{}", [], "Asia/Ho_Chi_Minh",
            DateTimeOffset.Parse("2026-10-14T06:00:00Z"), TestContext.Current.CancellationToken);
        if (explicitOffset)
        {
            var result = await call;
            Assert.NotNull(result.FreeTimeQuery); Assert.Equal(TimeSpan.FromHours(7), result.FreeTimeQuery.Start.Offset);
            Assert.Empty(result.Events); Assert.Empty(result.Tasks);
            Assert.Contains("verifiedFreeTime", provider.Body);
            using var request = JsonDocument.Parse(provider.Body);
            Assert.Contains("19:00–20:00 and 21:00–22:00", request.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        }
        else Assert.Equal("AGENT_AI_INVALID_RESPONSE", (await Assert.ThrowsAsync<AuthException>(() => call)).Code);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Schedule_review_queries_have_their_own_scope_and_require_explicit_offsets(bool explicitOffset)
    {
        var payload = JsonNode.Parse(Payload)!.AsObject();
        payload["events"] = new JsonArray(); payload["resultKind"] = "ANSWER";
        payload["scheduleReviewQuery"] = new JsonObject {
            ["start"] = explicitOffset ? "2026-10-15T00:00:00+07:00" : "2026-10-15T00:00:00",
            ["end"] = "2026-10-16T00:00:00+07:00" };
        using var provider = new Provider(payload.ToJsonString()); using var client = new HttpClient(provider);
        var call = Interpreter(client).InterpretAsync("Phân tích lịch trình ngày mai của tôi.", "{}", [], "Asia/Ho_Chi_Minh",
            DateTimeOffset.Parse("2026-10-14T06:00:00Z"), TestContext.Current.CancellationToken);
        if (explicitOffset) {
            var result = await call; Assert.NotNull(result.ScheduleReviewQuery); Assert.Null(result.FreeTimeQuery);
            Assert.Empty(result.Events); Assert.Contains("verifiedScheduleReview", provider.Body);
        } else Assert.Equal("AGENT_AI_INVALID_RESPONSE", (await Assert.ThrowsAsync<AuthException>(() => call)).Code);
    }

    [Theory] [InlineData("missing")] [InlineData("offset")] [InlineData("unknown")]
    [InlineData("categoryNumber")] [InlineData("categoryObject")] [InlineData("categoryBoolean")]
    public async Task Malformed_schema_or_offset_is_rejected(string mode)
    {
        var output = mode switch {
            "missing" => Payload.Replace("\"repeatWeeks\":1", "\"irrelevant\":1"),
            "offset" => Payload.Replace("14:00:00+07:00", "14:00:00"),
            "categoryNumber" => Payload.Replace("\"eventType\":\"MEETING\"", "\"eventType\":42"),
            "categoryObject" => Payload.Replace("\"eventType\":\"MEETING\"", "\"eventType\":{}"),
            "categoryBoolean" => Payload.Replace("\"eventType\":\"MEETING\"", "\"eventType\":false"),
            _ => Payload.Replace("\"reschedule\":false", "\"reschedule\":false,\"userId\":\"other\"")
        };
        using var provider = new Provider(output.Replace("\"MEETING\"", "\"UNSUPPORTED\"")); using var client = new HttpClient(provider);
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
        Assert.Equal(supported ? type : "OTHER", Assert.Single((await call).Tasks).TaskType);
        using var request = JsonDocument.Parse(provider.Body);
        var codes = request.RootElement.GetProperty("generationConfig").GetProperty("responseFormat").GetProperty("text").GetProperty("schema")
            .GetProperty("properties").GetProperty("tasks").GetProperty("items").GetProperty("properties").GetProperty("taskType").GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("CUSTOM_ACTIVE", codes); Assert.DoesNotContain("DESIGN", codes);
    }
    private static JsonObject CategoryPayload(string kind, string? category, bool editing)
    {
        var payload = JsonNode.Parse(Payload)!.AsObject();
        JsonObject item;
        if (kind == "event") item = payload["events"]![0]!.AsObject();
        else
        {
            payload["events"] = new JsonArray();
            item = JsonNode.Parse("""
                {"taskId":null,"name":"Slide","deadline":"2026-10-21T22:00:00+07:00","estimatedMinutes":90,"priority":2,"taskType":"OTHER","splittable":true,
                 "workWindowStart":null,"workWindowEnd":null,"durationEvidence":"90 phút","deadlineEvidence":"xong trước 22h ngày 21/10","workWindowEvidence":""}
                """)!.AsObject();
            payload["tasks"] = new JsonArray(item);
        }
        var field = kind == "event" ? "eventType" : "taskType";
        if (category == "[missing]") item.Remove(field); else item[field] = category;
        if (editing) item[kind == "event" ? "existingId" : "taskId"] = Guid.NewGuid().ToString();
        return payload;
    }

    [Theory]
    [InlineData("event", "WORK", false, "WORK")]
    [InlineData("event", "UNSUPPORTED", false, "OTHER")]
    [InlineData("event", "CLASS", false, "CLASS")]
    [InlineData("event", "", false, "OTHER")]
    [InlineData("event", null, false, "OTHER")]
    [InlineData("event", "[missing]", false, "OTHER")]
    [InlineData("event", "[missing]", true, "")]
    [InlineData("event", "WORK", true, "WORK")]
    [InlineData("task", "WORK", false, "WORK")]
    [InlineData("task", "UNSUPPORTED", false, "OTHER")]
    [InlineData("task", "CUSTOM_ACTIVE", false, "CUSTOM_ACTIVE")]
    [InlineData("task", "", false, "OTHER")]
    [InlineData("task", null, false, "OTHER")]
    [InlineData("task", "[missing]", false, "OTHER")]
    [InlineData("task", "[missing]", true, "")]
    [InlineData("task", "WORK", true, "WORK")]
    public async Task Agent_categories_fall_back_to_other_and_omitted_edit_categories_preserve_the_existing_value(string kind, string? category, bool editing, string expected)
    {
        using var provider = new Provider(CategoryPayload(kind, category, editing).ToJsonString()); using var client = new HttpClient(provider);
        var result = await Interpreter(client).InterpretAsync("Xếp lịch ngày mai", """{"activeTaskTypes":["CUSTOM_ACTIVE","WORK","OTHER"]}""", [], "UTC", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.Equal(expected, kind == "event" ? Assert.Single(result.Events).EventType : Assert.Single(result.Tasks).TaskType);
        if (kind == "event")
        {
            Assert.Equal(DateTimeOffset.Parse("2026-10-15T14:00:00+07:00"), result.Events[0].Start);
            Assert.Equal(DateTimeOffset.Parse("2026-10-15T16:00:00+07:00"), result.Events[0].End);
        }
    }

    [Fact] public async Task Unknown_task_category_cannot_use_an_inactive_other_fallback()
    {
        using var provider = new Provider(CategoryPayload("task", "WORK", false).ToJsonString()); using var client = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<AuthException>(() => Interpreter(client).InterpretAsync("Slide 90 phút", """{"activeTaskTypes":["CUSTOM_ACTIVE"]}""", [], "UTC", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
        Assert.Equal("AGENT_AI_INVALID_RESPONSE", error.Code);
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
