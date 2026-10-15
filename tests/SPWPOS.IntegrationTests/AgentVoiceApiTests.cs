using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class AgentVoiceApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Voice : IAgentVoiceTranscriber
    {
        public bool VoiceAvailable => true;
        public int Calls { get; private set; }
        public Task<VoiceTranscription> TranscribeAsync(byte[] audio, string mimeType, CancellationToken ct)
        { Calls++; return Task.FromResult(new VoiceTranscription("Ngày mai họp lúc 14 giờ.")); }
    }
    private sealed class Factory : ApiFactory
    {
        public Voice Transcriber { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        { base.ConfigureWebHost(builder); builder.ConfigureServices(s => s.AddSingleton<IAgentVoiceTranscriber>(Transcriber)); }
    }
    private static ByteArrayContent Audio(byte[]? bytes = null, string type = "audio/webm")
    { var content = new ByteArrayContent(bytes ?? [0x1a, 0x45, 0xdf, 0xa3, 1, 2]); content.Headers.ContentType = new MediaTypeHeaderValue(type); return content; }
    [Fact] public async Task Voice_is_authenticated_and_returns_text_without_creating_tasks_events_or_turns()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync();
        using var denied = await client.PostAsync("/api/agent/voice/transcribe", Audio(), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode); Assert.Equal(0, f.Transcriber.Calls);
        await WorkspaceFlowTests.Authorize(f, client);
        using var response = await client.PostAsync("/api/agent/voice/transcribe", Audio(), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("microphone=(self)", response.Headers.GetValues("Permissions-Policy").Single());
        var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Ngày mai họp lúc 14 giờ.", body.RootElement.GetProperty("data").GetProperty("text").GetString());
        var workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Empty(workspace.GetProperty("tasks").EnumerateArray()); Assert.Empty(workspace.GetProperty("events").EnumerateArray());
        Assert.Empty((await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/agent/turns")).GetProperty("data").EnumerateArray());
        Assert.Equal(1, f.Transcriber.Calls);
    }
    [Fact] public async Task Voice_validates_audio_before_provider_calls_and_has_a_separate_rate_limit()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        using var format = await client.PostAsync("/api/agent/voice/transcribe", Audio(type: "text/plain"), Ct);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, format.StatusCode);
        using var empty = await client.PostAsync("/api/agent/voice/transcribe", Audio([]), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        using var forged = await client.PostAsync("/api/agent/voice/transcribe", Audio([1, 2]), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        using var oversized = await client.PostAsync("/api/agent/voice/transcribe", Audio(new byte[VoiceLimits.MaxAudioBytes + 1]), Ct);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        Assert.Equal(0, f.Transcriber.Calls);
        for (var i = 0; i < 20; i++)
        {
            using var allowed = await client.PostAsync("/api/agent/voice/transcribe", Audio(), Ct);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }
        using var throttled = await client.PostAsync("/api/agent/voice/transcribe", Audio(), Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode); Assert.Equal(20, f.Transcriber.Calls);
    }
}
