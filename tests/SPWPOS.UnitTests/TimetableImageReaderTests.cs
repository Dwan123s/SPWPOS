using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Authentication;
using SPWPOS.Infrastructure.Workspace;
namespace SPWPOS.UnitTests;

public sealed class TimetableImageReaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 0];
    private const string Text = "Thứ 2: Toán, tiết 1–3, phòng A101. Chưa có ngày áp dụng và giờ theo tiết.";
    private sealed class Provider(string text = Text, string finish = "STOP", HttpStatusCode status = HttpStatusCode.OK, bool malformed = false) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Body = await request.Content!.ReadAsStringAsync(ct);
            Assert.Equal("test-image-key", request.Headers.GetValues("x-goog-api-key").Single());
            var output = malformed ? "{}" : JsonSerializer.Serialize(new { text, warnings = new[] { "Cần ngày áp dụng và giờ học theo tiết." }, needsReview = true });
            return new(status) { Content = new StringContent(JsonSerializer.Serialize(new {
                candidates = new[] { new { finishReason = finish, content = new { parts = new object[] {
                    new { thought = true, text = "private reasoning" }, new { text = output } } } } },
                usageMetadata = new { promptTokenCount = 20, candidatesTokenCount = 30 }
            })) };
        }
    }
    private static TaskInterpreter Reader(HttpClient http, string provider = "Gemini", AiUsageCollector? usage = null) => new(http,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = provider, ["AiSettings:Model"] = "gemini-3.5-flash-lite",
            ["AiSettings:GeminiApiKey"] = "test-image-key", ["AiSettings:VertexApiKey"] = "test-image-key",
            ["AiSettings:VertexProjectId"] = "image-test-project", ["AiSettings:ApiKey"] = "test-openai-key"
        }).Build(), NullLogger<TaskInterpreter>.Instance, usageCollector: usage);

    [Theory] [InlineData("Gemini")] [InlineData("VertexAI")]
    public async Task Reads_the_image_with_the_configured_provider_preserving_periods_and_uncertainty(string provider)
    {
        using var transport = new Provider(); using var http = new HttpClient(transport); var usage = new AiUsageCollector();
        var result = await Reader(http, provider, usage).ReadTimetableAsync(Png, "image/png", Ct);
        Assert.Equal(Text, result.Text); Assert.True(result.NeedsReview); Assert.Single(result.Warnings);
        Assert.DoesNotContain("07:00", result.Text); Assert.Equal(20, usage.InputTokens); Assert.Equal(30, usage.OutputTokens);
        using var body = JsonDocument.Parse(transport.Body);
        var image = body.RootElement.GetProperty("contents")[0].GetProperty("parts")[1].GetProperty("inlineData");
        Assert.Equal("image/png", image.GetProperty("mimeType").GetString());
        Assert.Equal(Png, Convert.FromBase64String(image.GetProperty("data").GetString()!));
        Assert.DoesNotContain("test-image-key", transport.Body);
    }

    [Theory] [InlineData("empty")] [InlineData("forged")] [InlineData("unsupported")] [InlineData("oversized")]
    public async Task Invalid_uploads_never_call_the_provider(string mode)
    {
        using var transport = new Provider(); using var http = new HttpClient(transport);
        var bytes = mode == "empty" ? [] : mode == "forged" ? new byte[] { 1, 2 } : mode == "oversized" ? new byte[5 * 1024 * 1024 + 1] : Png;
        var error = await Assert.ThrowsAsync<AuthException>(() => Reader(http).ReadTimetableAsync(bytes, mode == "unsupported" ? "image/svg+xml" : "image/png", Ct));
        Assert.Equal(mode is "empty" or "oversized" ? "IMAGE_SIZE" : "IMAGE_FORMAT", error.Code); Assert.Equal(0, transport.Calls);
    }

    [Theory] [InlineData("empty")] [InlineData("too-long")] [InlineData("truncated")] [InlineData("malformed")]
    public async Task Partial_or_unreadable_results_are_rejected_instead_of_silently_dropping_classes(string mode)
    {
        using var transport = new Provider(text: mode == "empty" ? "" : mode == "too-long" ? new string('x', 1601) : Text,
            finish: mode == "truncated" ? "MAX_TOKENS" : "STOP", malformed: mode == "malformed");
        using var http = new HttpClient(transport);
        var error = await Assert.ThrowsAsync<AuthException>(() => Reader(http).ReadTimetableAsync(Png, "image/png", Ct));
        Assert.Equal(mode == "empty" ? "IMAGE_UNREADABLE" : mode == "too-long" ? "IMAGE_TEXT_TOO_LONG" : "IMAGE_AI_INVALID_RESPONSE", error.Code);
    }

    [Fact] public async Task Unconfigured_image_support_cannot_send_to_an_unapproved_provider()
    {
        using var transport = new Provider(); using var http = new HttpClient(transport); var reader = Reader(http, "OpenAI");
        Assert.False(reader.ImageAvailable);
        Assert.Equal("IMAGE_NOT_CONFIGURED", (await Assert.ThrowsAsync<AuthException>(() => reader.ReadTimetableAsync(Png, "image/png", Ct))).Code);
        Assert.Equal(0, transport.Calls);
    }
}
