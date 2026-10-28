using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class AgentImagesApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Reader : IAgentTimetableImageReader
    {
        public bool ImageAvailable => true;
        public int Calls { get; private set; }
        public Task<TimetableImageReading> ReadTimetableAsync(byte[] image, string mimeType, CancellationToken ct)
        { Calls++; return Task.FromResult(new TimetableImageReading("Ngày mai: Toán, 14:00–15:00, chỉ một lần.", [], false)); }
    }
    private sealed class Factory : ApiFactory
    {
        public Reader Images { get; } = new();
        public AgentAiTestDouble Ai { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(s => { s.AddSingleton<IAgentTimetableImageReader>(Images); s.AddSingleton<ITaskInterpreter>(Ai); });
        }
    }
    private static HttpRequestMessage Upload(byte[]? bytes = null, string type = "image/png", bool consent = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/agent/images/timetable") {
            Content = new ByteArrayContent(bytes ?? [137, 80, 78, 71, 13, 10, 26, 10, 1]) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(type);
        if (consent) request.Headers.Add("X-SPWPOS-Image-Consent", "1");
        return request;
    }
    [Fact] public async Task Image_reading_requires_authentication_and_submit_consent_and_does_not_mutate_the_calendar()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync();
        using var denied = await client.SendAsync(Upload(), Ct); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        await WorkspaceFlowTests.Authorize(f, client);
        using var missingConsent = await client.SendAsync(Upload(consent: false), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingConsent.StatusCode); Assert.Equal(0, f.Images.Calls);
        using var response = await client.SendAsync(Upload(), Ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Contains("Toán", body.RootElement.GetProperty("data").GetProperty("text").GetString());
        var workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Empty(workspace.GetProperty("events").EnumerateArray()); Assert.Empty(workspace.GetProperty("tasks").EnumerateArray());
        Assert.Empty((await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/agent/turns")).GetProperty("data").EnumerateArray());
        var caps = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/agent/capabilities")).GetProperty("data");
        Assert.True(caps.GetProperty("imageAvailable").GetBoolean()); Assert.Equal(1, f.Images.Calls);
        Assert.Equal(1, (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/agent/usage")).GetProperty("data").GetProperty("used").GetInt32());
    }
    [Fact] public async Task Unsupported_empty_forged_and_oversized_images_are_rejected_before_AI_calls()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        using var unsupported = await client.SendAsync(Upload(type: "image/svg+xml"), Ct); Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);
        using var empty = await client.SendAsync(Upload([]), Ct); Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        using var forged = await client.SendAsync(Upload([1, 2]), Ct); Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        using var oversized = await client.SendAsync(Upload(new byte[TimetableImageLimits.MaxBytes + 1]), Ct); Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        Assert.Equal(0, f.Images.Calls);
    }
    [Fact] public async Task Image_uploads_have_their_own_rate_limit()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        for (var i = 0; i < 6; i++) { using var allowed = await client.SendAsync(Upload(), Ct); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode); }
        using var denied = await client.SendAsync(Upload(), Ct); Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode); Assert.Equal(6, f.Images.Calls);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task A_submitted_timetable_uses_the_existing_automatic_save_setting_and_can_be_undone(bool autoApply)
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        var start = new DateTimeOffset(f.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date.AddDays(1).AddHours(14), TimeSpan.FromHours(7));
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Toán", start, start.AddHours(1), EventType: "CLASS")] };
        using var uploaded = await client.SendAsync(Upload(), Ct); Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        using var data = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync(Ct));
        var result = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(),
            "Tạo lịch học. Thời khóa biểu từ ảnh (dữ liệu để đối chiếu):\n" + data.RootElement.GetProperty("data").GetProperty("text").GetString(), autoApply))).GetProperty("data");
        Assert.Equal(autoApply ? "APPLIED" : "PREVIEW", result.GetProperty("status").GetString());
        var workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        if (autoApply) {
            var item = Assert.Single(workspace.GetProperty("events").EnumerateArray()); Assert.Equal("CLASS", item.GetProperty("eventType").GetString());
            Assert.Equal(start, item.GetProperty("start").GetDateTimeOffset());
            await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/agent/turns/{result.GetProperty("id").GetGuid()}/undo");
            Assert.Empty((await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data").GetProperty("events").EnumerateArray());
        } else Assert.Empty(workspace.GetProperty("events").EnumerateArray());
    }
}
